import { describe, expect, it } from "vitest";
import {
  canonicalJson,
  CommandHistory,
  createAuthoringId,
  deterministicWorkspace,
  duplicateWithNewId,
  exportReady,
  isAuthoringId,
  LocalStorageDraftAdapter,
  MemoryDraftAdapter,
  normalizePackagePath,
  sniffRasterMediaType,
  validateRasterImport,
  validateWorkspace,
  type AuthoringWorkspace,
} from ".";

const ref = (record_id: string) => ({ package_id: "studio.sample", record_id });
const workspace = (): AuthoringWorkspace => ({
  package: {
    document_kind: "mythos.content-package",
    schema_version: "1.0",
    package_id: "studio.sample",
    package_version: "1.0.0",
    display_name: "Sample",
    dependencies: [],
    entries: [
      {
        kind: "sprite-animation",
        id: "studio.sprite",
        path: "manifests/sprites/main.json",
        media_type: "application/json",
        size: 1,
        integrity: { algorithm: "sha256", digest: "a" },
      },
      {
        kind: "asset",
        id: "studio.body",
        path: "assets/characters/body.png",
        media_type: "image/png",
        size: 8,
        integrity: { algorithm: "sha256", digest: "b" },
      },
      {
        kind: "layered-map",
        id: "studio.map",
        path: "manifests/maps/main.json",
        media_type: "application/json",
        size: 1,
        integrity: { algorithm: "sha256", digest: "c" },
      },
      {
        kind: "npc",
        id: "studio.npc",
        path: "records/npcs/main.json",
        media_type: "application/json",
        size: 1,
        integrity: { algorithm: "sha256", digest: "d" },
      },
    ],
  },
  npcs: [
    {
      document_kind: "mythos.npc-authoring",
      schema_version: "1.0",
      npc_record_id: "studio.npc",
      display_name: "NPC",
      visual: {
        sprite_manifest: ref("studio.sprite"),
        options: { "studio.palette": "studio.blue" },
      },
      tags: ["studio.person"],
    },
  ],
  sprites: [
    {
      document_kind: "mythos.sprite-animation",
      schema_version: "1.0",
      sprite_manifest_id: "studio.sprite",
      display_name: "Sprite",
      layers: [
        {
          layer_id: "studio.body-layer",
          display_name: "Body",
          order: 0,
          asset: ref("studio.body"),
        },
      ],
      options: [
        {
          option_id: "studio.palette",
          display_name: "Palette",
          choices: [{ choice_id: "studio.blue", display_name: "Blue" }],
          default_choice_id: "studio.blue",
        },
      ],
      animations: [],
    },
  ],
  maps: [
    {
      document_kind: "mythos.layered-map",
      schema_version: "1.0",
      map_manifest_id: "studio.map",
      display_name: "Map",
      background_asset: ref("studio.body"),
      layers: [],
      markers: [],
    },
  ],
  assets: [
    {
      id: "studio.body",
      path: "assets/characters/body.png",
      media_type: "image/png",
      size: 8,
      width: 1,
      height: 1,
    },
  ],
});

describe("authoring identity", () => {
  it("accepts contract IDs and rejects runtime-shaped or unnamespaced IDs", () => {
    expect(isAuthoringId("studio-neutral.sample-record")).toBe(true);
    expect(isAuthoringId("Entity:42")).toBe(false);
    expect(isAuthoringId("sample")).toBe(false);
  });
  it("creates stable-format IDs and gives duplicates a new identity", () => {
    const id = createAuthoringId("My Studio", () => "ABC-123");
    expect(id).toBe("my-studio.record-abc123");
    const copy = duplicateWithNewId(
      { npc_record_id: "studio.old", name: "A" },
      "npc_record_id",
      "studio.new",
    );
    expect(copy).toEqual({ npc_record_id: "studio.new", name: "A" });
  });
});

describe("package path and media security", () => {
  it.each([
    "../secret",
    "assets/../secret",
    "/etc/passwd",
    "C:/secret",
    "assets\\bad.png",
    "assets//bad.png",
    "assets/con.png",
  ])("rejects unsafe path %s", (path) =>
    expect(normalizePackagePath(path)).toBeNull(),
  );
  it("accepts a normalized package path", () =>
    expect(normalizePackagePath("assets/maps/background.webp")).toBe(
      "assets/maps/background.webp",
    ));
  it("recognizes raster signatures and rejects active/spoofed content", () => {
    const png = new Uint8Array([
      0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
    ]);
    expect(sniffRasterMediaType(png)).toBe("image/png");
    expect(
      validateRasterImport({
        name: "x.svg",
        type: "image/svg+xml",
        bytes: new TextEncoder().encode("<svg>"),
        width: 10,
        height: 10,
      }),
    ).toEqual(
      expect.arrayContaining(["media.unsupported", "media.signature-mismatch"]),
    );
    expect(
      validateRasterImport({
        name: "fake.png",
        type: "image/png",
        bytes: new TextEncoder().encode("<html>"),
        width: 10,
        height: 10,
      }),
    ).toContain("media.signature-mismatch");
  });
  it("enforces dimensions, decoded bytes, and file bytes", () => {
    const png = new Uint8Array([
      0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
    ]);
    expect(
      validateRasterImport(
        { name: "x.png", type: "image/png", bytes: png, width: 4, height: 4 },
        {
          maxFileBytes: 4,
          maxWidth: 2,
          maxHeight: 2,
          maxDecodedBytes: 4,
          maxFileCount: 1,
          maxPackageBytes: 4,
        },
      ),
    ).toEqual(
      expect.arrayContaining([
        "media.file-too-large",
        "media.dimensions-too-large",
        "media.decoded-too-large",
      ]),
    );
  });
});

describe("contract and reference validation", () => {
  it("accepts a valid cross-referenced workspace", () => {
    expect(
      validateWorkspace(workspace()).filter((d) => d.severity === "error"),
    ).toEqual([]);
    expect(exportReady(workspace())).toBe(true);
  });
  it("reports unresolved and wrong-kind references", () => {
    const value = workspace();
    value.npcs[0].visual.sprite_manifest = ref("studio.body");
    expect(validateWorkspace(value).map((d) => d.code)).toContain(
      "reference.wrong-kind",
    );
    value.maps[0].background_asset = ref("studio.missing");
    expect(validateWorkspace(value).map((d) => d.code)).toContain(
      "reference.missing",
    );
  });
  it("rejects malformed options, unsafe notes, nonfinite transforms, and duplicate paths", () => {
    const value = workspace();
    value.npcs[0].notes = "<script>alert(1)</script>";
    value.npcs[0].visual.options["studio.palette"] = "studio.unknown";
    value.maps[0].layers.push({
      layer_id: "studio.layer",
      display_name: "Layer",
      order: 0,
      asset: ref("studio.body"),
      visible: true,
      locked: false,
      transform: {
        position: { x: Number.NaN, y: 0 },
        scale: { x: 0, y: 1 },
        rotation_degrees: 0,
        opacity: 2,
      },
    });
    value.package.entries.push({
      ...value.package.entries[0],
      id: "studio.other",
      path: value.package.entries[0].path.toUpperCase(),
    });
    const codes = validateWorkspace(value).map((d) => d.code);
    expect(codes).toEqual(
      expect.arrayContaining([
        "security.unsafe-text",
        "npc.invalid-visual-option",
        "map.nonfinite-transform",
        "map.zero-scale",
        "map.invalid-opacity",
        "package.duplicate-path",
      ]),
    );
  });
});

describe("deterministic export", () => {
  it("sorts inventory, records, tags, and layer ties without mutating input", () => {
    const value = workspace();
    value.npcs[0].tags = ["studio.zed", "studio.alpha", "studio.alpha"];
    value.sprites[0].layers.unshift({
      ...value.sprites[0].layers[0],
      layer_id: "studio.alpha-layer",
    });
    const result = deterministicWorkspace(value);
    expect(result.package.entries.map((entry) => entry.kind)).toEqual([
      "asset",
      "layered-map",
      "npc",
      "sprite-animation",
    ]);
    expect(result.npcs[0].tags).toEqual(["studio.alpha", "studio.zed"]);
    expect(result.sprites[0].layers.map((layer) => layer.layer_id)).toEqual([
      "studio.alpha-layer",
      "studio.body-layer",
    ]);
    expect(value.npcs[0].tags).toHaveLength(3);
  });
  it("canonicalizes object keys while preserving meaningful array order", () => {
    expect(canonicalJson({ z: 1, a: { d: 2, c: 1 }, frames: [2, 1] })).toBe(
      '{\n  "a": {\n    "c": 1,\n    "d": 2\n  },\n  "frames": [\n    2,\n    1\n  ],\n  "z": 1\n}\n',
    );
  });
});

describe("draft adapters and command history", () => {
  it("round trips drafts by value", async () => {
    const adapter = new MemoryDraftAdapter();
    const value = workspace();
    await adapter.save("one", value);
    value.package.display_name = "Changed after save";
    expect((await adapter.load("one"))?.package.display_name).toBe("Sample");
    await adapter.remove("one");
    expect(await adapter.load("one")).toBeNull();
  });
  it("supports a replaceable web storage boundary", async () => {
    const data = new Map<string, string>();
    const storage = {
      getItem: (k: string) => data.get(k) ?? null,
      setItem: (k: string, v: string) => {
        data.set(k, v);
      },
      removeItem: (k: string) => {
        data.delete(k);
      },
    };
    const adapter = new LocalStorageDraftAdapter(storage);
    await adapter.save("one", workspace());
    expect((await adapter.load("one"))?.package.package_id).toBe(
      "studio.sample",
    );
  });
  it("undoes, redoes, and clears redo after a new command", () => {
    const history = new CommandHistory({ count: 0 });
    history.execute((state) => ({ count: state.count + 1 }));
    history.undo();
    expect(history.value.count).toBe(0);
    expect(history.canRedo).toBe(true);
    history.redo();
    expect(history.value.count).toBe(1);
    history.undo();
    history.execute(() => ({ count: 4 }));
    expect(history.canRedo).toBe(false);
  });
});
