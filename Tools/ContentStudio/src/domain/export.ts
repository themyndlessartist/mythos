import {
  canonicalJson,
  compareEntries,
  deterministicWorkspace,
  ordinalCompare,
} from "./canonical";
import type { AssetByteAdapter } from "./assets";
import type {
  AuthoringWorkspace,
  ContentPackageManifest,
  PackageEntry,
} from "./models";
import { validateWorkspace, type Diagnostic } from "./validation";

export const BUNDLE_MEDIA_TYPE = "application/vnd.mythos.content-bundle+json";
export interface BundleFile {
  path: string;
  media_type: string;
  size: number;
  integrity: { algorithm: "sha256"; digest: string };
  content_base64: string;
}
export interface DeterministicExportBundle {
  bundle_kind: "mythos.content-bundle";
  bundle_version: "1.0";
  package_id: string;
  files: BundleFile[];
}
export class ExportFailure extends Error {
  constructor(readonly diagnostics: Diagnostic[]) {
    super("Workspace is not export ready.");
  }
}

const utf8 = (value: string) => new TextEncoder().encode(value);
const base64 = (bytes: Uint8Array): string => {
  let binary = "";
  for (let i = 0; i < bytes.length; i += 0x8000)
    binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(binary);
};
async function sha256(bytes: Uint8Array): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", bytes.slice().buffer);
  return [...new Uint8Array(digest)]
    .map((value) => value.toString(16).padStart(2, "0"))
    .join("");
}
const recordFiles = (
  workspace: AuthoringWorkspace,
): Array<{
  kind: PackageEntry["kind"];
  id: string;
  path: string;
  bytes: Uint8Array;
}> => [
  ...workspace.npcs.map((value) => ({
    kind: "npc" as const,
    id: value.npc_record_id,
    path: `records/npcs/${value.npc_record_id}.json`,
    bytes: utf8(canonicalJson(value)),
  })),
  ...workspace.sprites.map((value) => ({
    kind: "sprite-animation" as const,
    id: value.sprite_manifest_id,
    path: `manifests/sprites/${value.sprite_manifest_id}.json`,
    bytes: utf8(canonicalJson(value)),
  })),
  ...workspace.maps.map((value) => ({
    kind: "layered-map" as const,
    id: value.map_manifest_id,
    path: `manifests/maps/${value.map_manifest_id}.json`,
    bytes: utf8(canonicalJson(value)),
  })),
];

export function exportReadinessDiagnostics(
  workspace: AuthoringWorkspace,
): Diagnostic[] {
  const entries: PackageEntry[] = [
    ...workspace.npcs.map((value) => ({
      kind: "npc" as const,
      id: value.npc_record_id,
      path: `records/npcs/${value.npc_record_id}.json`,
      media_type: "application/json",
      size: 0,
      integrity: { algorithm: "sha256", digest: "pending" },
    })),
    ...workspace.sprites.map((value) => ({
      kind: "sprite-animation" as const,
      id: value.sprite_manifest_id,
      path: `manifests/sprites/${value.sprite_manifest_id}.json`,
      media_type: "application/json",
      size: 0,
      integrity: { algorithm: "sha256", digest: "pending" },
    })),
    ...workspace.maps.map((value) => ({
      kind: "layered-map" as const,
      id: value.map_manifest_id,
      path: `manifests/maps/${value.map_manifest_id}.json`,
      media_type: "application/json",
      size: 0,
      integrity: { algorithm: "sha256", digest: "pending" },
    })),
    ...workspace.assets.map((value) => ({
      kind: "asset" as const,
      id: value.id,
      path: value.path,
      media_type: value.media_type,
      size: value.size,
      integrity: { algorithm: "sha256", digest: "pending" },
    })),
  ].sort(compareEntries);
  return validateWorkspace({
    ...workspace,
    package: { ...workspace.package, entries },
  });
}

export async function assembleExportBundle(
  workspaceInput: AuthoringWorkspace,
  draftKey: string,
  bytesAdapter: AssetByteAdapter,
): Promise<{
  bundle: DeterministicExportBundle;
  bytes: Uint8Array;
  manifest: ContentPackageManifest;
}> {
  const workspace = deterministicWorkspace(structuredClone(workspaceInput));
  const diagnostics = exportReadinessDiagnostics(workspace).filter(
    (item) => item.severity === "error",
  );
  if (diagnostics.length) throw new ExportFailure(diagnostics);
  const records = recordFiles(workspace);
  const assets = await Promise.all(
    workspace.assets.map(async (asset) => {
      const bytes = await bytesAdapter.load(draftKey, asset.id);
      if (!bytes)
        throw new ExportFailure([
          {
            code: "asset.bytes-missing",
            severity: "error",
            document_id: asset.id,
            path: asset.path,
            message: "Persisted raster bytes are missing.",
          },
        ]);
      return {
        kind: "asset" as const,
        id: asset.id,
        path: asset.path,
        media_type: asset.media_type,
        bytes,
      };
    }),
  );
  const sources = [
    ...records.map((r) => ({ ...r, media_type: "application/json" })),
    ...assets,
  ];
  const entries = await Promise.all(
    sources.map(async (source): Promise<PackageEntry> => ({
      kind: source.kind,
      id: source.id,
      path: source.path,
      media_type: source.media_type,
      size: source.bytes.byteLength,
      integrity: { algorithm: "sha256", digest: await sha256(source.bytes) },
    })),
  );
  entries.sort(compareEntries);
  const manifest: ContentPackageManifest = { ...workspace.package, entries };
  const finalDiagnostics = validateWorkspace({
    ...workspace,
    package: manifest,
  }).filter((item) => item.severity === "error");
  const expected = new Set(sources.map((item) => item.path));
  const declared = new Set(entries.map((item) => item.path));
  if (
    finalDiagnostics.length ||
    expected.size !== declared.size ||
    [...expected].some((path) => !declared.has(path))
  )
    throw new ExportFailure(
      finalDiagnostics.length
        ? finalDiagnostics
        : [
            {
              code: "package.inventory-mismatch",
              severity: "error",
              document_id: manifest.package_id,
              path: "/entries",
              message: "Inventory and content must correspond exactly.",
            },
          ],
    );
  const packageBytes = utf8(canonicalJson(manifest));
  const files = await Promise.all(
    [
      {
        path: "package.json",
        media_type: "application/json",
        bytes: packageBytes,
      },
      ...sources,
    ]
      .sort((a, b) => ordinalCompare(a.path, b.path))
      .map(async (file): Promise<BundleFile> => ({
        path: file.path,
        media_type: file.media_type,
        size: file.bytes.byteLength,
        integrity: { algorithm: "sha256", digest: await sha256(file.bytes) },
        content_base64: base64(file.bytes),
      })),
  );
  const bundle: DeterministicExportBundle = {
    bundle_kind: "mythos.content-bundle",
    bundle_version: "1.0",
    package_id: manifest.package_id,
    files,
  };
  return { bundle, bytes: utf8(canonicalJson(bundle)), manifest };
}
