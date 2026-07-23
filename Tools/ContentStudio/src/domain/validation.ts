import { isAuthoringId } from "./ids";
import type {
  AuthoringDocument,
  AuthoringWorkspace,
  ContentPackageManifest,
  LayeredMapManifest,
  NpcAuthoringRecord,
  PackageEntry,
  RecordReference,
  SpriteAnimationManifest,
} from "./models";
import { normalizePackagePath, RASTER_MEDIA_TYPES } from "./security";
import { ordinalCompare } from "./canonical";

export type DiagnosticSeverity = "error" | "warning";
export interface Diagnostic {
  code: string;
  severity: DiagnosticSeverity;
  document_id: string;
  path: string;
  message: string;
}
type Add = (
  code: string,
  path: string,
  message: string,
  severity?: DiagnosticSeverity,
) => void;

const plainTextSafe = (value: string): boolean =>
  !/[<>]/.test(value) &&
  // eslint-disable-next-line no-control-regex -- required authoring-text security range
  !/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/.test(value);
const extensionKeysValid = (
  value: Record<string, unknown> | undefined,
): boolean => !value || Object.keys(value).every(isAuthoringId);
const finite = (value: number): boolean => Number.isFinite(value);

function duplicateValues(values: string[]): Set<string> {
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  values.forEach((value) =>
    seen.has(value) ? duplicates.add(value) : seen.add(value),
  );
  return duplicates;
}

function documentId(document: AuthoringDocument): string {
  if ("package_id" in document) return document.package_id;
  if ("npc_record_id" in document) return document.npc_record_id;
  if ("sprite_manifest_id" in document) return document.sprite_manifest_id;
  return document.map_manifest_id;
}

function validator(document: AuthoringDocument) {
  const result: Diagnostic[] = [];
  const id = documentId(document);
  const add: Add = (code, path, message, severity = "error") =>
    result.push({ code, severity, document_id: id, path, message });
  return { result, add };
}

function common(
  document: AuthoringDocument,
  expectedKind: string,
  id: string,
  displayName: string,
  add: Add,
): void {
  if (document.document_kind !== expectedKind)
    add(
      "contract.document-kind",
      "/document_kind",
      `Expected ${expectedKind}.`,
    );
  if (document.schema_version !== "1.0")
    add(
      "contract.schema-version",
      "/schema_version",
      "Only schema version 1.0 is supported.",
    );
  if (!isAuthoringId(id))
    add("identity.invalid", "/id", "Use a lowercase namespaced authoring ID.");
  if (!displayName.trim())
    add("field.required", "/display_name", "Display name is required.");
  if (!extensionKeysValid(document.extensions))
    add(
      "extensions.unnamespaced",
      "/extensions",
      "Extension keys must be namespaced.",
    );
}

function validateReference(
  reference: RecordReference,
  path: string,
  add: Add,
): void {
  if (!isAuthoringId(reference.package_id))
    add(
      "reference.package-id",
      `${path}/package_id`,
      "Invalid package authoring ID.",
    );
  if (!isAuthoringId(reference.record_id))
    add(
      "reference.record-id",
      `${path}/record_id`,
      "Invalid record authoring ID.",
    );
}

export function validatePackage(value: ContentPackageManifest): Diagnostic[] {
  const { result, add } = validator(value);
  common(
    value,
    "mythos.content-package",
    value.package_id,
    value.display_name,
    add,
  );
  if (!/^\d+\.\d+\.\d+$/.test(value.package_version))
    add("package.version", "/package_version", "Use major.minor.patch.");
  const ids = duplicateValues(
    value.entries.map((entry) => entry.id.toLowerCase()),
  );
  const paths = duplicateValues(
    value.entries.map((entry) => entry.path.toLowerCase()),
  );
  value.entries.forEach((entry, index) => {
    const path = `/entries/${index}`;
    if (!isAuthoringId(entry.id))
      add("identity.invalid", `${path}/id`, "Invalid entry ID.");
    if (ids.has(entry.id.toLowerCase()))
      add(
        "package.duplicate-id",
        `${path}/id`,
        "Duplicate or case-colliding entry ID.",
      );
    if (!normalizePackagePath(entry.path))
      add(
        "security.unsafe-path",
        `${path}/path`,
        "Path must remain package-relative and normalized.",
      );
    if (paths.has(entry.path.toLowerCase()))
      add(
        "package.duplicate-path",
        `${path}/path`,
        "Duplicate or case-colliding path.",
      );
    if (!Number.isInteger(entry.size) || entry.size < 0)
      add(
        "asset.invalid-size",
        `${path}/size`,
        "Size must be a non-negative integer.",
      );
    if (!entry.integrity.algorithm || !entry.integrity.digest)
      add(
        "asset.invalid-integrity",
        `${path}/integrity`,
        "Integrity metadata is required.",
      );
    if (
      entry.kind === "asset" &&
      !(RASTER_MEDIA_TYPES as readonly string[]).includes(entry.media_type)
    )
      add(
        "asset.unsupported-media",
        `${path}/media_type`,
        "Unsupported raster media type.",
      );
  });
  return result;
}

export function validateNpc(value: NpcAuthoringRecord): Diagnostic[] {
  const { result, add } = validator(value);
  common(
    value,
    "mythos.npc-authoring",
    value.npc_record_id,
    value.display_name,
    add,
  );
  validateReference(
    value.visual.sprite_manifest,
    "/visual/sprite_manifest",
    add,
  );
  const tags = value.tags ?? [];
  tags.forEach((tag, index) => {
    if (!isAuthoringId(tag))
      add("npc.invalid-tag", `/tags/${index}`, "Tags must be namespaced IDs.");
  });
  if (
    duplicateValues(tags).size ||
    tags.some(
      (tag, index) => index > 0 && ordinalCompare(tags[index - 1], tag) >= 0,
    )
  )
    add("npc.tags-order", "/tags", "Tags must be sorted and unique.");
  if (value.notes !== undefined && !plainTextSafe(value.notes))
    add(
      "security.unsafe-text",
      "/notes",
      "Notes must be plain text without markup.",
    );
  return result;
}

function validateSprite(value: SpriteAnimationManifest): Diagnostic[] {
  const { result, add } = validator(value);
  common(
    value,
    "mythos.sprite-animation",
    value.sprite_manifest_id,
    value.display_name,
    add,
  );
  const optionIds = duplicateValues(
    value.options.map((option) => option.option_id),
  );
  value.options.forEach((option, oi) => {
    if (!isAuthoringId(option.option_id) || optionIds.has(option.option_id))
      add(
        "sprite.invalid-option-id",
        `/options/${oi}/option_id`,
        "Option IDs must be valid and unique.",
      );
    if (!option.choices.length)
      add(
        "sprite.empty-choices",
        `/options/${oi}/choices`,
        "At least one choice is required.",
      );
    const choices = option.choices.map((choice) => choice.choice_id);
    if (duplicateValues(choices).size)
      add(
        "sprite.duplicate-choice",
        `/options/${oi}/choices`,
        "Choice IDs must be unique.",
      );
    if (option.default_choice_id && !choices.includes(option.default_choice_id))
      add(
        "sprite.invalid-default",
        `/options/${oi}/default_choice_id`,
        "Default must reference a declared choice.",
      );
  });
  const layerIds = duplicateValues(value.layers.map((layer) => layer.layer_id));
  value.layers.forEach((layer, li) => {
    if (!isAuthoringId(layer.layer_id) || layerIds.has(layer.layer_id))
      add(
        "sprite.invalid-layer-id",
        `/layers/${li}/layer_id`,
        "Layer IDs must be valid and unique.",
      );
    if (!Number.isInteger(layer.order))
      add(
        "sprite.invalid-order",
        `/layers/${li}/order`,
        "Layer order must be an integer.",
      );
    validateReference(layer.asset, `/layers/${li}/asset`, add);
    if (layer.offset && (!finite(layer.offset.x) || !finite(layer.offset.y)))
      add(
        "sprite.invalid-offset",
        `/layers/${li}/offset`,
        "Offsets must be finite.",
      );
    if (layer.option_condition) {
      const option = value.options.find(
        (item) => item.option_id === layer.option_condition?.option_id,
      );
      if (
        !option ||
        !option.choices.some(
          (choice) => choice.choice_id === layer.option_condition?.choice_id,
        )
      )
        add(
          "sprite.invalid-condition",
          `/layers/${li}/option_condition`,
          "Condition must reference a declared option choice.",
        );
    }
  });
  const animationIds = duplicateValues(
    value.animations.map((animation) => animation.animation_id),
  );
  value.animations.forEach((animation, ai) => {
    if (
      !isAuthoringId(animation.animation_id) ||
      animationIds.has(animation.animation_id)
    )
      add(
        "sprite.invalid-animation-id",
        `/animations/${ai}/animation_id`,
        "Animation IDs must be valid and unique.",
      );
    if (!animation.frames.length)
      add(
        "sprite.empty-animation",
        `/animations/${ai}/frames`,
        "Animation requires at least one frame.",
      );
    animation.frames.forEach((frame, fi) => {
      if (!Number.isInteger(frame.duration_units) || frame.duration_units <= 0)
        add(
          "sprite.invalid-duration",
          `/animations/${ai}/frames/${fi}/duration_units`,
          "Duration must be a positive integer.",
        );
      if (!frame.assets.length)
        add(
          "sprite.empty-frame",
          `/animations/${ai}/frames/${fi}/assets`,
          "Frame requires at least one layer asset.",
        );
      frame.assets.forEach((asset, ri) =>
        validateReference(
          asset,
          `/animations/${ai}/frames/${fi}/assets/${ri}`,
          add,
        ),
      );
    });
  });
  return result;
}

function validateMap(value: LayeredMapManifest): Diagnostic[] {
  const { result, add } = validator(value);
  common(
    value,
    "mythos.layered-map",
    value.map_manifest_id,
    value.display_name,
    add,
  );
  validateReference(value.background_asset, "/background_asset", add);
  const layerIds = duplicateValues(value.layers.map((layer) => layer.layer_id));
  value.layers.forEach((layer, index) => {
    const path = `/layers/${index}`;
    if (!isAuthoringId(layer.layer_id) || layerIds.has(layer.layer_id))
      add(
        "map.invalid-layer-id",
        `${path}/layer_id`,
        "Layer IDs must be valid and unique.",
      );
    if (!Number.isInteger(layer.order))
      add(
        "map.invalid-order",
        `${path}/order`,
        "Layer order must be an integer.",
      );
    validateReference(layer.asset, `${path}/asset`, add);
    const values = [
      layer.transform.position.x,
      layer.transform.position.y,
      layer.transform.scale.x,
      layer.transform.scale.y,
      layer.transform.rotation_degrees,
    ];
    if (!values.every(finite))
      add(
        "map.nonfinite-transform",
        `${path}/transform`,
        "Transform values must be finite.",
      );
    if (layer.transform.scale.x === 0 || layer.transform.scale.y === 0)
      add("map.zero-scale", `${path}/transform/scale`, "Scale cannot be zero.");
    if (
      layer.transform.opacity !== undefined &&
      (!finite(layer.transform.opacity) ||
        layer.transform.opacity < 0 ||
        layer.transform.opacity > 1)
    )
      add(
        "map.invalid-opacity",
        `${path}/transform/opacity`,
        "Opacity must be between zero and one.",
      );
    if (!layer.visible)
      add(
        "map.hidden-layer",
        `${path}/visible`,
        "Layer is excluded from preview.",
        "warning",
      );
    if (layer.locked)
      add(
        "map.locked-layer",
        `${path}/locked`,
        "Layer is protected from editing.",
        "warning",
      );
  });
  const markerIds = duplicateValues(
    value.markers.map((marker) => marker.marker_id),
  );
  value.markers.forEach((marker, index) => {
    const path = `/markers/${index}`;
    if (!isAuthoringId(marker.marker_id) || markerIds.has(marker.marker_id))
      add(
        "map.invalid-marker-id",
        `${path}/marker_id`,
        "Marker IDs must be valid and unique.",
      );
    if (!["region", "spawn", "reference"].includes(marker.kind))
      add(
        "map.invalid-marker-kind",
        `${path}/kind`,
        "Unsupported marker kind.",
      );
    if (!finite(marker.position.x) || !finite(marker.position.y))
      add(
        "map.nonfinite-position",
        `${path}/position`,
        "Position must be finite.",
      );
    if (marker.notes !== undefined && !plainTextSafe(marker.notes))
      add(
        "security.unsafe-text",
        `${path}/notes`,
        "Notes must be plain text without markup.",
      );
    if (marker.target) validateReference(marker.target, `${path}/target`, add);
  });
  return result;
}

export function validateDocument(document: AuthoringDocument): Diagnostic[] {
  switch (document.document_kind) {
    case "mythos.content-package":
      return validatePackage(document);
    case "mythos.npc-authoring":
      return validateNpc(document);
    case "mythos.sprite-animation":
      return validateSprite(document);
    case "mythos.layered-map":
      return validateMap(document);
  }
}

function resolve(
  reference: RecordReference,
  packageId: string,
  entries: PackageEntry[],
  expected?: PackageEntry["kind"],
): "ok" | "missing" | "wrong-kind" {
  if (reference.package_id !== packageId) return "missing";
  const entry = entries.find((item) => item.id === reference.record_id);
  if (!entry) return "missing";
  return expected && entry.kind !== expected ? "wrong-kind" : "ok";
}

export function validateWorkspace(workspace: AuthoringWorkspace): Diagnostic[] {
  const diagnostics = [
    validatePackage(workspace.package),
    ...workspace.npcs.map(validateNpc),
    ...workspace.sprites.map(validateSprite),
    ...workspace.maps.map(validateMap),
  ].flat();
  const addReference = (
    document_id: string,
    path: string,
    status: "ok" | "missing" | "wrong-kind",
  ) => {
    if (status !== "ok")
      diagnostics.push({
        code: `reference.${status}`,
        severity: "error",
        document_id,
        path,
        message:
          status === "missing"
            ? "Reference cannot be resolved."
            : "Reference targets the wrong entry kind.",
      });
  };
  workspace.npcs.forEach((npc) => {
    addReference(
      npc.npc_record_id,
      "/visual/sprite_manifest",
      resolve(
        npc.visual.sprite_manifest,
        workspace.package.package_id,
        workspace.package.entries,
        "sprite-animation",
      ),
    );
    const sprite = workspace.sprites.find(
      (item) =>
        item.sprite_manifest_id === npc.visual.sprite_manifest.record_id,
    );
    if (sprite)
      sprite.options.forEach((option) => {
        const selected =
          npc.visual.options[option.option_id] ?? option.default_choice_id;
        if (
          !selected ||
          !option.choices.some((choice) => choice.choice_id === selected)
        )
          diagnostics.push({
            code: "npc.invalid-visual-option",
            severity: "error",
            document_id: npc.npc_record_id,
            path: `/visual/options/${option.option_id}`,
            message: "Select a declared visual option choice.",
          });
      });
  });
  const assetRefs: Array<[string, string, RecordReference]> = [];
  workspace.sprites.forEach((sprite) => {
    sprite.layers.forEach((layer, i) =>
      assetRefs.push([
        sprite.sprite_manifest_id,
        `/layers/${i}/asset`,
        layer.asset,
      ]),
    );
    sprite.animations.forEach((a, ai) =>
      a.frames.forEach((f, fi) =>
        f.assets.forEach((r, ri) =>
          assetRefs.push([
            sprite.sprite_manifest_id,
            `/animations/${ai}/frames/${fi}/assets/${ri}`,
            r,
          ]),
        ),
      ),
    );
  });
  workspace.maps.forEach((map) => {
    assetRefs.push([
      map.map_manifest_id,
      "/background_asset",
      map.background_asset,
    ]);
    map.layers.forEach((layer, i) =>
      assetRefs.push([map.map_manifest_id, `/layers/${i}/asset`, layer.asset]),
    );
    map.markers.forEach((marker, i) => {
      if (marker.target)
        addReference(
          map.map_manifest_id,
          `/markers/${i}/target`,
          resolve(
            marker.target,
            workspace.package.package_id,
            workspace.package.entries,
          ),
        );
    });
  });
  assetRefs.forEach(([id, path, ref]) =>
    addReference(
      id,
      path,
      resolve(
        ref,
        workspace.package.package_id,
        workspace.package.entries,
        "asset",
      ),
    ),
  );
  return diagnostics;
}

export const exportReady = (workspace: AuthoringWorkspace): boolean =>
  !validateWorkspace(workspace).some((item) => item.severity === "error");
