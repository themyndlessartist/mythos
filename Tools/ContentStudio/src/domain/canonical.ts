import type { AuthoringWorkspace, PackageEntry } from "./models";

export function compareEntries(a: PackageEntry, b: PackageEntry): number {
  return (
    a.kind.localeCompare(b.kind) ||
    a.id.localeCompare(b.id) ||
    a.path.localeCompare(b.path)
  );
}

function canonicalValue(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalValue);
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>)
        .filter(([, child]) => child !== undefined)
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([key, child]) => [key, canonicalValue(child)]),
    );
  }
  return value;
}

export function canonicalJson(value: unknown): string {
  return `${JSON.stringify(canonicalValue(value), null, 2)}\n`;
}

export function deterministicWorkspace(
  workspace: AuthoringWorkspace,
): AuthoringWorkspace {
  return {
    ...workspace,
    package: {
      ...workspace.package,
      entries: [...workspace.package.entries].sort(compareEntries),
    },
    npcs: [...workspace.npcs]
      .sort((a, b) => a.npc_record_id.localeCompare(b.npc_record_id))
      .map((npc) => ({
        ...npc,
        tags: npc.tags ? [...new Set(npc.tags)].sort() : undefined,
      })),
    sprites: [...workspace.sprites]
      .sort((a, b) => a.sprite_manifest_id.localeCompare(b.sprite_manifest_id))
      .map((sprite) => ({
        ...sprite,
        layers: [...sprite.layers].sort(
          (a, b) => a.order - b.order || a.layer_id.localeCompare(b.layer_id),
        ),
      })),
    maps: [...workspace.maps]
      .sort((a, b) => a.map_manifest_id.localeCompare(b.map_manifest_id))
      .map((map) => ({
        ...map,
        layers: [...map.layers].sort(
          (a, b) => a.order - b.order || a.layer_id.localeCompare(b.layer_id),
        ),
      })),
    assets: [...workspace.assets].sort(
      (a, b) => a.id.localeCompare(b.id) || a.path.localeCompare(b.path),
    ),
  };
}
