export type NamespacedId = string;

export interface RecordReference {
  package_id: NamespacedId;
  record_id: NamespacedId;
}

export type PassiveExtensions = Record<string, unknown>;

export interface Integrity {
  algorithm: string;
  digest: string;
}

export type PackageEntryKind =
  "npc" | "sprite-animation" | "layered-map" | "asset";

export interface PackageEntry {
  kind: PackageEntryKind;
  id: NamespacedId;
  path: string;
  media_type: string;
  size: number;
  integrity: Integrity;
}

export interface PackageDependency {
  package_id: NamespacedId;
  version_range: string;
}

export interface ContentPackageManifest {
  document_kind: "mythos.content-package";
  schema_version: "1.0";
  package_id: NamespacedId;
  package_version: string;
  display_name: string;
  entries: PackageEntry[];
  dependencies: PackageDependency[];
  extensions?: PassiveExtensions;
}

export interface NpcAuthoringRecord {
  document_kind: "mythos.npc-authoring";
  schema_version: "1.0";
  npc_record_id: NamespacedId;
  display_name: string;
  visual: {
    sprite_manifest: RecordReference;
    options: Record<string, NamespacedId>;
  };
  tags?: NamespacedId[];
  notes?: string;
  extensions?: PassiveExtensions;
}

export interface SpriteLayer {
  layer_id: NamespacedId;
  display_name: string;
  order: number;
  asset: RecordReference;
  option_condition?: { option_id: NamespacedId; choice_id: NamespacedId };
  offset?: { x: number; y: number };
}

export interface VisualOptionChoice {
  choice_id: NamespacedId;
  display_name: string;
}
export interface VisualOption {
  option_id: NamespacedId;
  display_name: string;
  choices: VisualOptionChoice[];
  default_choice_id?: NamespacedId;
}

export interface AnimationFrame {
  assets: RecordReference[];
  duration_units: number;
}
export interface SpriteAnimation {
  animation_id: NamespacedId;
  display_name: string;
  loop: boolean;
  frames: AnimationFrame[];
}

export interface SpriteAnimationManifest {
  document_kind: "mythos.sprite-animation";
  schema_version: "1.0";
  sprite_manifest_id: NamespacedId;
  display_name: string;
  layers: SpriteLayer[];
  options: VisualOption[];
  animations: SpriteAnimation[];
  extensions?: PassiveExtensions;
}

export interface MapLayer {
  layer_id: NamespacedId;
  display_name: string;
  order: number;
  asset: RecordReference;
  visible: boolean;
  locked: boolean;
  transform: {
    position: { x: number; y: number };
    scale: { x: number; y: number };
    rotation_degrees: number;
    opacity?: number;
  };
}

export type MapMarkerKind = "region" | "spawn" | "reference";
export interface MapMarker {
  marker_id: NamespacedId;
  kind: MapMarkerKind;
  display_name: string;
  position: { x: number; y: number };
  notes?: string;
  target?: RecordReference;
}

export interface LayeredMapManifest {
  document_kind: "mythos.layered-map";
  schema_version: "1.0";
  map_manifest_id: NamespacedId;
  display_name: string;
  background_asset: RecordReference;
  layers: MapLayer[];
  markers: MapMarker[];
  extensions?: PassiveExtensions;
}

export interface AssetMetadata {
  id: NamespacedId;
  path: string;
  media_type: "image/png" | "image/jpeg" | "image/webp";
  size: number;
  width: number;
  height: number;
}

export interface AuthoringWorkspace {
  package: ContentPackageManifest;
  npcs: NpcAuthoringRecord[];
  sprites: SpriteAnimationManifest[];
  maps: LayeredMapManifest[];
  assets: AssetMetadata[];
}

export type AuthoringDocument =
  | ContentPackageManifest
  | NpcAuthoringRecord
  | SpriteAnimationManifest
  | LayeredMapManifest;
