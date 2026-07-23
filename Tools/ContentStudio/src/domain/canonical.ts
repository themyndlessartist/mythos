import type { AuthoringWorkspace, PackageEntry } from "./models";

export function ordinalCompare(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

export function compareEntries(a: PackageEntry, b: PackageEntry): number {
  return (
    ordinalCompare(a.kind, b.kind) ||
    ordinalCompare(a.id, b.id) ||
    ordinalCompare(a.path, b.path)
  );
}

function canonicalValue(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalValue);
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>)
        .filter(([, child]) => child !== undefined)
        .sort(([a], [b]) => ordinalCompare(a, b))
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
      .sort((a, b) => ordinalCompare(a.npc_record_id, b.npc_record_id))
      .map((npc) => ({
        ...npc,
        tags: npc.tags
          ? [...new Set(npc.tags)].sort(ordinalCompare)
          : undefined,
      })),
    sprites: [...workspace.sprites]
      .sort((a, b) =>
        ordinalCompare(a.sprite_manifest_id, b.sprite_manifest_id),
      )
      .map((sprite) => ({
        ...sprite,
        layers: [...sprite.layers].sort(
          (a, b) => a.order - b.order || ordinalCompare(a.layer_id, b.layer_id),
        ),
      })),
    maps: [...workspace.maps]
      .sort((a, b) => ordinalCompare(a.map_manifest_id, b.map_manifest_id))
      .map((map) => ({
        ...map,
        layers: [...map.layers].sort(
          (a, b) => a.order - b.order || ordinalCompare(a.layer_id, b.layer_id),
        ),
      })),
    assets: [...workspace.assets].sort(
      (a, b) => ordinalCompare(a.id, b.id) || ordinalCompare(a.path, b.path),
    ),
  };
}
