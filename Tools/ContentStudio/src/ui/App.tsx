import { ChangeEvent, ReactNode, useEffect, useRef, useState } from "react";
import {
  Archive,
  Box,
  ChevronDown,
  ChevronUp,
  Copy,
  Download,
  Eye,
  EyeOff,
  FileImage,
  Image,
  Lock,
  Map,
  Menu,
  Plus,
  Redo2,
  Save,
  Trash2,
  Undo2,
  Unlock,
  Users,
  X,
} from "lucide-react";

type View = "npcs" | "assets" | "maps" | "package";
type Ref = { package_id: string; record_id: string };
type Npc = {
  document_kind: "mythos.npc-authoring";
  schema_version: "1.0";
  npc_record_id: string;
  display_name: string;
  visual: { sprite_manifest: Ref; options: Record<string, string> };
  tags: string[];
  notes: string;
  extensions: Record<string, unknown>;
};
type Asset = {
  id: string;
  name: string;
  path: string;
  url: string;
  type: string;
  size: number;
  width: number;
  height: number;
};
type VisualLayer = {
  layer_id: string;
  display_name: string;
  order: number;
  assetId: string;
  visible: boolean;
  locked: boolean;
};
type MapLayer = VisualLayer & {
  x: number;
  y: number;
  scale: number;
  rotation: number;
  opacity: number;
};
type Marker = {
  marker_id: string;
  kind: "region" | "spawn" | "reference";
  display_name: string;
  x: number;
  y: number;
};
type Draft = {
  npcs: Npc[];
  assets: Asset[];
  characterLayers: VisualLayer[];
  mapBackground: string;
  mapLayers: MapLayer[];
  markers: Marker[];
};

const PACKAGE_ID = "mythos.local-workspace";
const initialNpc = (id = "mythos.npc-record"): Npc => ({
  document_kind: "mythos.npc-authoring",
  schema_version: "1.0",
  npc_record_id: id,
  display_name: "Untitled NPC",
  visual: {
    sprite_manifest: {
      package_id: PACKAGE_ID,
      record_id: "mythos.character-visuals",
    },
    options: {},
  },
  tags: [],
  notes: "",
  extensions: {},
});
const initial: Draft = {
  npcs: [initialNpc()],
  assets: [],
  characterLayers: [],
  mapBackground: "",
  mapLayers: [],
  markers: [],
};
const MAX_FILE = 10 * 1024 * 1024;
const allowed = new Set(["image/png", "image/jpeg", "image/webp"]);
const idPattern = /^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)+$/;
const uid = (prefix: string) => `${prefix}.${crypto.randomUUID().slice(0, 8)}`;

function IconButton({
  label,
  disabled,
  onClick,
  children,
}: {
  label: string;
  disabled?: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      className="icon-button"
      title={label}
      aria-label={label}
      disabled={disabled}
      onClick={onClick}
    >
      {children}
    </button>
  );
}
function Field({
  label,
  children,
  hint,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {hint && <small>{hint}</small>}
    </label>
  );
}
function Empty({
  icon: Icon,
  title,
  body,
  action,
}: {
  icon: typeof Image;
  title: string;
  body: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="empty">
      <Icon />
      <strong>{title}</strong>
      <p>{body}</p>
      {action}
    </div>
  );
}

export function App() {
  const [view, setView] = useState<View>("npcs");
  const [draft, setDraft] = useState<Draft>(() => {
    try {
      return (
        JSON.parse(
          localStorage.getItem("mythos.content-studio.draft") || "null",
        ) || initial
      );
    } catch {
      return initial;
    }
  });
  const [selectedNpc, setSelectedNpc] = useState(
    draft.npcs[0]?.npc_record_id || "",
  );
  const [sidebar, setSidebar] = useState(false);
  const [past, setPast] = useState<Draft[]>([]);
  const [future, setFuture] = useState<Draft[]>([]);
  const current =
    draft.npcs.find((n) => n.npc_record_id === selectedNpc) || draft.npcs[0];
  useEffect(() => {
    const safe = {
      ...draft,
      assets: draft.assets.map((a) => ({ ...a, url: "" })),
    };
    localStorage.setItem("mythos.content-studio.draft", JSON.stringify(safe));
  }, [draft]);
  const change = (next: Draft) => {
    setPast((p) => [...p.slice(-29), draft]);
    setFuture([]);
    setDraft(next);
  };
  const undo = () => {
    const prev = past.at(-1);
    if (!prev) return;
    setFuture((f) => [draft, ...f]);
    setDraft(prev);
    setPast((p) => p.slice(0, -1));
  };
  const redo = () => {
    const next = future[0];
    if (!next) return;
    setPast((p) => [...p, draft]);
    setDraft(next);
    setFuture((f) => f.slice(1));
  };
  const updateNpc = (patch: Partial<Npc>) =>
    current &&
    change({
      ...draft,
      npcs: draft.npcs.map((n) => (n === current ? { ...n, ...patch } : n)),
    });
  const createNpc = () => {
    let id = uid("mythos.npc");
    while (draft.npcs.some((n) => n.npc_record_id === id))
      id = uid("mythos.npc");
    const n = initialNpc(id);
    change({ ...draft, npcs: [...draft.npcs, n] });
    setSelectedNpc(id);
  };
  const duplicate = () => {
    if (!current) return;
    const id = uid("mythos.npc");
    change({
      ...draft,
      npcs: [
        ...draft.npcs,
        {
          ...current,
          npc_record_id: id,
          display_name: `${current.display_name} copy`,
          visual: { ...current.visual, options: { ...current.visual.options } },
        },
      ],
    });
    setSelectedNpc(id);
  };
  const removeNpc = () => {
    if (!current) return;
    const rest = draft.npcs.filter((n) => n !== current);
    change({ ...draft, npcs: rest });
    setSelectedNpc(rest[0]?.npc_record_id || "");
  };
  const nav = [
    ["npcs", "NPCs", Users],
    ["assets", "Character Assets", FileImage],
    ["maps", "Maps", Map],
    ["package", "Package", Archive],
  ] as const;
  return (
    <div className="app-shell">
      <header>
        <button
          className="mobile-menu"
          onClick={() => setSidebar(true)}
          aria-label="Open navigation"
        >
          <Menu />
        </button>
        <div className="brand">
          <Box />
          <div>
            <b>Mythos</b>
            <span>Content Studio</span>
          </div>
        </div>
        <div className="history">
          <IconButton label="Undo" disabled={!past.length} onClick={undo}>
            <Undo2 />
          </IconButton>
          <IconButton label="Redo" disabled={!future.length} onClick={redo}>
            <Redo2 />
          </IconButton>
          <span>
            <Save /> Draft saved locally
          </span>
        </div>
      </header>
      <aside className={sidebar ? "open" : ""}>
        <button
          className="close-nav"
          onClick={() => setSidebar(false)}
          aria-label="Close navigation"
        >
          <X />
        </button>
        <nav aria-label="Workspace">
          {nav.map(([id, label, Icon]) => (
            <button
              key={id}
              className={view === id ? "active" : ""}
              onClick={() => {
                setView(id);
                setSidebar(false);
              }}
            >
              <Icon />
              {label}
            </button>
          ))}
        </nav>
        <div className="boundary">
          <strong>Authoring workspace</strong>
          <span>Preview only · No runtime entities</span>
        </div>
      </aside>
      {sidebar && (
        <button
          className="scrim"
          onClick={() => setSidebar(false)}
          aria-label="Close navigation"
        />
      )}
      <main>
        {view === "npcs" && (
          <NpcWorkspace
            draft={draft}
            current={current}
            selected={selectedNpc}
            setSelected={setSelectedNpc}
            create={createNpc}
            duplicate={duplicate}
            remove={removeNpc}
            update={updateNpc}
          />
        )}{" "}
        {view === "assets" && <AssetWorkspace draft={draft} change={change} />}{" "}
        {view === "maps" && <MapWorkspace draft={draft} change={change} />}{" "}
        {view === "package" && <PackageWorkspace draft={draft} />}
      </main>
    </div>
  );
}

function WorkspaceTitle({
  eyebrow,
  title,
  description,
  actions,
}: {
  eyebrow: string;
  title: string;
  description: string;
  actions?: ReactNode;
}) {
  return (
    <div className="workspace-title">
      <div>
        <span>{eyebrow}</span>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      <div className="actions">{actions}</div>
    </div>
  );
}
function NpcWorkspace({
  draft,
  current,
  selected,
  setSelected,
  create,
  duplicate,
  remove,
  update,
}: {
  draft: Draft;
  current?: Npc;
  selected: string;
  setSelected: (s: string) => void;
  create: () => void;
  duplicate: () => void;
  remove: () => void;
  update: (p: Partial<Npc>) => void;
}) {
  const diagnostics = current
    ? [
        ...(!idPattern.test(current.npc_record_id)
          ? ["Use a lowercase namespaced authoring ID."]
          : []),
        ...(!current.display_name.trim() ? ["Display name is required."] : []),
        ...(draft.npcs.filter((n) => n.npc_record_id === current.npc_record_id)
          .length > 1
          ? ["Authoring ID must be unique."]
          : []),
      ]
    : [];
  return (
    <>
      <WorkspaceTitle
        eyebrow="DATA-002"
        title="NPC authoring"
        description="Create setting-independent content records and connect them to visual manifests."
        actions={
          <button className="primary" onClick={create}>
            <Plus />
            New NPC
          </button>
        }
      />
      <div className="split">
        <section className="list-panel">
          <div className="panel-heading">
            <b>Records</b>
            <span>{draft.npcs.length}</span>
          </div>
          {draft.npcs.map((n) => (
            <button
              className={`record ${selected === n.npc_record_id ? "selected" : ""}`}
              key={n.npc_record_id}
              onClick={() => setSelected(n.npc_record_id)}
            >
              <span className="avatar">
                {n.display_name.slice(0, 2).toUpperCase()}
              </span>
              <span>
                <b>{n.display_name || "Unnamed NPC"}</b>
                <small>{n.npc_record_id}</small>
              </span>
            </button>
          ))}
        </section>
        <section className="editor-panel">
          {!current ? (
            <Empty
              icon={Users}
              title="No NPC records"
              body="Create a record to begin authoring."
              action={
                <button className="primary" onClick={create}>
                  <Plus />
                  New NPC
                </button>
              }
            />
          ) : (
            <>
              <div className="panel-heading">
                <div>
                  <b>{current.display_name || "Unnamed NPC"}</b>
                  <span
                    className={diagnostics.length ? "status error" : "status"}
                  >
                    {diagnostics.length
                      ? `${diagnostics.length} issues`
                      : "Valid draft"}
                  </span>
                </div>
                <div>
                  <IconButton label="Duplicate NPC" onClick={duplicate}>
                    <Copy />
                  </IconButton>
                  <IconButton label="Delete NPC" onClick={remove}>
                    <Trash2 />
                  </IconButton>
                </div>
              </div>
              <div className="form-grid">
                <Field label="Display name">
                  <input
                    value={current.display_name}
                    onChange={(e) => update({ display_name: e.target.value })}
                  />
                </Field>
                <Field
                  label="Authoring ID"
                  hint="Immutable after first export; never a runtime Entity ID."
                >
                  <input
                    value={current.npc_record_id}
                    onChange={(e) => {
                      const old = current.npc_record_id;
                      update({ npc_record_id: e.target.value });
                      if (selected === old) setSelected(e.target.value);
                    }}
                  />
                </Field>
                <Field label="Sprite manifest package">
                  <input
                    value={current.visual.sprite_manifest.package_id}
                    onChange={(e) =>
                      update({
                        visual: {
                          ...current.visual,
                          sprite_manifest: {
                            ...current.visual.sprite_manifest,
                            package_id: e.target.value,
                          },
                        },
                      })
                    }
                  />
                </Field>
                <Field label="Sprite manifest record">
                  <input
                    value={current.visual.sprite_manifest.record_id}
                    onChange={(e) =>
                      update({
                        visual: {
                          ...current.visual,
                          sprite_manifest: {
                            ...current.visual.sprite_manifest,
                            record_id: e.target.value,
                          },
                        },
                      })
                    }
                  />
                </Field>
                <Field
                  label="Tags"
                  hint="Comma-separated, unique namespaced tags."
                >
                  <input
                    value={current.tags.join(", ")}
                    onChange={(e) =>
                      update({
                        tags: [
                          ...new Set(
                            e.target.value
                              .split(",")
                              .map((x) => x.trim())
                              .filter(Boolean),
                          ),
                        ].sort(),
                      })
                    }
                  />
                </Field>
                <Field label="Author notes">
                  <textarea
                    rows={5}
                    value={current.notes}
                    onChange={(e) => update({ notes: e.target.value })}
                  />
                </Field>
              </div>
              {diagnostics.length > 0 && (
                <div className="diagnostics">
                  <b>Validation</b>
                  {diagnostics.map((d) => (
                    <p key={d}>● {d}</p>
                  ))}
                </div>
              )}
              <details className="json">
                <summary>Deterministic JSON preview</summary>
                <pre>
                  {JSON.stringify(current, Object.keys(current).sort(), 2)}
                </pre>
              </details>
            </>
          )}
        </section>
      </div>
    </>
  );
}

async function readImages(files: FileList): Promise<Asset[]> {
  const out: Asset[] = [];
  for (const file of [...files]) {
    if (!allowed.has(file.type) || file.size > MAX_FILE)
      throw new Error(`${file.name}: only PNG, JPEG, or WebP up to 10 MB`);
    const bytes = new Uint8Array(await file.slice(0, 12).arrayBuffer());
    const png =
      bytes[0] === 137 && bytes[1] === 80 && bytes[2] === 78 && bytes[3] === 71;
    const jpeg = bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255;
    const webp =
      String.fromCharCode(...bytes.slice(0, 4)) === "RIFF" &&
      String.fromCharCode(...bytes.slice(8, 12)) === "WEBP";
    if (!(png || jpeg || webp))
      throw new Error(
        `${file.name}: file signature does not match an allowed raster format`,
      );
    const url = URL.createObjectURL(file);
    const dimensions = await new Promise<{ width: number; height: number }>(
      (resolve, reject) => {
        const img = new window.Image();
        img.onload = () =>
          resolve({ width: img.naturalWidth, height: img.naturalHeight });
        img.onerror = reject;
        img.src = url;
      },
    );
    if (dimensions.width > 4096 || dimensions.height > 4096) {
      URL.revokeObjectURL(url);
      throw new Error(`${file.name}: dimensions exceed 4096 × 4096`);
    }
    out.push({
      id: uid("asset.image"),
      name: file.name,
      path: `assets/characters/${file.name.replace(/[^a-zA-Z0-9._-]/g, "-")}`,
      url,
      type: file.type,
      size: file.size,
      ...dimensions,
    });
  }
  return out;
}
function Uploader({
  onFiles,
  label = "Import images",
}: {
  onFiles: (a: Asset[]) => void;
  label?: string;
}) {
  const ref = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");
  const pick = async (e: ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files) return;
    try {
      setError("");
      onFiles(await readImages(e.target.files));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Import failed");
    }
    e.target.value = "";
  };
  return (
    <>
      <button className="primary" onClick={() => ref.current?.click()}>
        <Plus />
        {label}
      </button>
      <input
        ref={ref}
        hidden
        type="file"
        accept="image/png,image/jpeg,image/webp"
        multiple
        onChange={pick}
      />
      {error && (
        <span className="inline-error" role="alert">
          {error}
        </span>
      )}
    </>
  );
}
function reorder<T extends { order: number }>(
  items: T[],
  index: number,
  delta: number,
) {
  const sorted = [...items].sort((a, b) => a.order - b.order);
  const target = index + delta;
  if (target < 0 || target >= sorted.length) return items;
  [sorted[index], sorted[target]] = [sorted[target], sorted[index]];
  return sorted.map((x, i) => ({ ...x, order: i }));
}
function AssetWorkspace({
  draft,
  change,
}: {
  draft: Draft;
  change: (d: Draft) => void;
}) {
  const add = (assets: Asset[]) =>
    change({
      ...draft,
      assets: [...draft.assets, ...assets],
      characterLayers: [
        ...draft.characterLayers,
        ...assets.map((a, i) => ({
          layer_id: uid("layer.character"),
          display_name: a.name,
          order: draft.characterLayers.length + i,
          assetId: a.id,
          visible: true,
          locked: false,
        })),
      ],
    });
  return (
    <>
      <WorkspaceTitle
        eyebrow="DATA-003"
        title="Character assets"
        description="Order safe raster layers into a non-authoritative visual manifest preview."
        actions={<Uploader onFiles={add} />}
      />
      <div className="canvas-layout">
        <section className="layer-panel">
          <div className="panel-heading">
            <b>Visual layers</b>
            <span>{draft.characterLayers.length}</span>
          </div>
          {!draft.characterLayers.length ? (
            <Empty
              icon={FileImage}
              title="No visual layers"
              body="Import PNG, JPEG, or WebP images to create ordered layers."
            />
          ) : (
            draft.characterLayers
              .sort((a, b) => a.order - b.order)
              .map((l, i) => (
                <LayerRow
                  key={l.layer_id}
                  layer={l}
                  up={() =>
                    change({
                      ...draft,
                      characterLayers: reorder(draft.characterLayers, i, -1),
                    })
                  }
                  down={() =>
                    change({
                      ...draft,
                      characterLayers: reorder(draft.characterLayers, i, 1),
                    })
                  }
                  patch={(p) =>
                    change({
                      ...draft,
                      characterLayers: draft.characterLayers.map((x) =>
                        x === l ? { ...x, ...p } : x,
                      ),
                    })
                  }
                />
              ))
          )}
        </section>
        <Preview
          title="Character composition"
          layers={draft.characterLayers}
          assets={draft.assets}
        />
      </div>
    </>
  );
}
function LayerRow({
  layer,
  up,
  down,
  patch,
}: {
  layer: VisualLayer;
  up: () => void;
  down: () => void;
  patch: (p: Partial<VisualLayer>) => void;
}) {
  return (
    <div className="layer-row">
      <span className="drag">{layer.order + 1}</span>
      <span>
        <b>{layer.display_name}</b>
        <small>{layer.layer_id}</small>
      </span>
      <IconButton label="Move layer up" onClick={up}>
        <ChevronUp />
      </IconButton>
      <IconButton label="Move layer down" onClick={down}>
        <ChevronDown />
      </IconButton>
      <IconButton
        label={layer.visible ? "Hide layer" : "Show layer"}
        onClick={() => patch({ visible: !layer.visible })}
      >
        {layer.visible ? <Eye /> : <EyeOff />}
      </IconButton>
      <IconButton
        label={layer.locked ? "Unlock layer" : "Lock layer"}
        onClick={() => patch({ locked: !layer.locked })}
      >
        {layer.locked ? <Lock /> : <Unlock />}
      </IconButton>
    </div>
  );
}
function Preview({
  title,
  layers,
  assets,
  map = false,
  background,
}: {
  title: string;
  layers: (VisualLayer | MapLayer)[];
  assets: Asset[];
  map?: boolean;
  background?: string;
}) {
  return (
    <section className="preview-panel">
      <div className="panel-heading">
        <div>
          <b>{title}</b>
          <span className="status muted">Approximation · Studio only</span>
        </div>
      </div>
      <div className={`preview ${map ? "map-preview" : ""}`}>
        {background && (
          <img
            className="background"
            src={assets.find((a) => a.id === background)?.url}
          />
        )}{" "}
        {layers
          .filter((l) => l.visible)
          .sort(
            (a, b) => a.order - b.order || a.layer_id.localeCompare(b.layer_id),
          )
          .map((l) => {
            const a = assets.find((x) => x.id === l.assetId);
            if (!a?.url) return null;
            const m = l as MapLayer;
            return (
              <img
                key={l.layer_id}
                src={a.url}
                alt=""
                style={
                  map
                    ? {
                        transform: `translate(${m.x}px,${m.y}px) rotate(${m.rotation}deg) scale(${m.scale})`,
                        opacity: m.opacity,
                      }
                    : {}
                }
              />
            );
          })}
        {!background &&
          !layers.some(
            (l) => l.visible && assets.find((a) => a.id === l.assetId)?.url,
          ) && (
            <div className="canvas-empty">
              <Image />
              <span>Preview appears here</span>
            </div>
          )}
      </div>
    </section>
  );
}

function MapWorkspace({
  draft,
  change,
}: {
  draft: Draft;
  change: (d: Draft) => void;
}) {
  const add = (assets: Asset[]) => {
    const all = [...draft.assets, ...assets];
    const bg = draft.mapBackground || assets[0]?.id || "";
    const layerAssets = draft.mapBackground ? assets : assets.slice(1);
    change({
      ...draft,
      assets: all,
      mapBackground: bg,
      mapLayers: [
        ...draft.mapLayers,
        ...layerAssets.map((a, i) => ({
          layer_id: uid("layer.map"),
          display_name: a.name,
          order: draft.mapLayers.length + i,
          assetId: a.id,
          visible: true,
          locked: false,
          x: 0,
          y: 0,
          scale: 1,
          rotation: 0,
          opacity: 1,
        })),
      ],
    });
  };
  const addMarker = () =>
    change({
      ...draft,
      markers: [
        ...draft.markers,
        {
          marker_id: uid("marker.reference"),
          kind: "reference",
          display_name: "Reference marker",
          x: 50,
          y: 50,
        },
      ],
    });
  return (
    <>
      <WorkspaceTitle
        eyebrow="DATA-004"
        title="Layered maps"
        description="Compose a background, visual layers, and generic dimensionless markers."
        actions={
          <>
            <Uploader onFiles={add} label="Import map images" />
            <button onClick={addMarker}>
              <Plus />
              Add marker
            </button>
          </>
        }
      />
      <div className="canvas-layout">
        <section className="layer-panel scroll">
          <div className="panel-heading">
            <b>Composition</b>
          </div>
          <Field label="Background">
            {draft.assets.length ? (
              <select
                value={draft.mapBackground}
                onChange={(e) =>
                  change({ ...draft, mapBackground: e.target.value })
                }
              >
                <option value="">Select raster…</option>
                {draft.assets.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                  </option>
                ))}
              </select>
            ) : (
              <small>Import a raster background first.</small>
            )}
          </Field>
          {draft.mapLayers
            .sort((a, b) => a.order - b.order)
            .map((l, i) => (
              <div key={l.layer_id}>
                <LayerRow
                  layer={l}
                  up={() =>
                    change({
                      ...draft,
                      mapLayers: reorder(draft.mapLayers, i, -1),
                    })
                  }
                  down={() =>
                    change({
                      ...draft,
                      mapLayers: reorder(draft.mapLayers, i, 1),
                    })
                  }
                  patch={(p) =>
                    change({
                      ...draft,
                      mapLayers: draft.mapLayers.map((x) =>
                        x === l ? { ...x, ...p } : x,
                      ),
                    })
                  }
                />
                <div className="transform-grid">
                  {(["x", "y", "scale", "rotation", "opacity"] as const).map(
                    (k) => (
                      <Field key={k} label={k}>
                        <input
                          type="number"
                          step={k === "opacity" ? "0.1" : "1"}
                          value={l[k]}
                          disabled={l.locked}
                          onChange={(e) =>
                            change({
                              ...draft,
                              mapLayers: draft.mapLayers.map((x) =>
                                x === l
                                  ? { ...x, [k]: Number(e.target.value) }
                                  : x,
                              ),
                            })
                          }
                        />
                      </Field>
                    ),
                  )}
                </div>
              </div>
            ))}
          <div className="panel-heading">
            <b>Markers</b>
            <span>{draft.markers.length}</span>
          </div>
          {draft.markers.map((m) => (
            <div className="marker" key={m.marker_id}>
              <select
                value={m.kind}
                onChange={(e) =>
                  change({
                    ...draft,
                    markers: draft.markers.map((x) =>
                      x === m
                        ? { ...x, kind: e.target.value as Marker["kind"] }
                        : x,
                    ),
                  })
                }
              >
                <option>region</option>
                <option>spawn</option>
                <option>reference</option>
              </select>
              <input
                value={m.display_name}
                onChange={(e) =>
                  change({
                    ...draft,
                    markers: draft.markers.map((x) =>
                      x === m ? { ...x, display_name: e.target.value } : x,
                    ),
                  })
                }
              />
              <IconButton
                label="Delete marker"
                onClick={() =>
                  change({
                    ...draft,
                    markers: draft.markers.filter((x) => x !== m),
                  })
                }
              >
                <Trash2 />
              </IconButton>
            </div>
          ))}
        </section>
        <Preview
          map
          title="Layered map preview"
          layers={draft.mapLayers}
          assets={draft.assets}
          background={draft.mapBackground}
        />
      </div>
    </>
  );
}
function PackageWorkspace({ draft }: { draft: Draft }) {
  const issues = [
    ...draft.npcs.flatMap((n) =>
      !idPattern.test(n.npc_record_id)
        ? [`${n.display_name}: invalid authoring ID`]
        : [],
    ),
    ...(!draft.mapBackground ? ["Map background is not selected"] : []),
    ...(draft.assets.some((a) => !allowed.has(a.type))
      ? ["Unsupported asset media type"]
      : []),
  ];
  const inventory = [
    ...draft.npcs.map((n) => ({
      kind: "npc",
      id: n.npc_record_id,
      path: `records/npcs/${n.npc_record_id}.json`,
    })),
    ...draft.assets.map((a) => ({ kind: "asset", id: a.id, path: a.path })),
  ].sort(
    (a, b) =>
      a.kind.localeCompare(b.kind) ||
      a.id.localeCompare(b.id) ||
      a.path.localeCompare(b.path),
  );
  const exportJson = () => {
    const payload = {
      document_kind: "mythos.content-package",
      schema_version: "1.0",
      package_id: PACKAGE_ID,
      package_version: "0.1.0",
      display_name: "Local workspace",
      entries: inventory,
      dependencies: [],
      extensions: {},
    };
    const blob = new Blob([JSON.stringify(payload, null, 2) + "\n"], {
      type: "application/json",
    });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "package.json";
    a.click();
    URL.revokeObjectURL(a.href);
  };
  return (
    <>
      <WorkspaceTitle
        eyebrow="DATA-001"
        title="Package readiness"
        description="Inspect deterministic inventory and resolve blocking diagnostics before export."
        actions={
          <button
            className="primary"
            disabled={issues.length > 0}
            onClick={exportJson}
          >
            <Download />
            Export package.json
          </button>
        }
      />
      <div className="package-grid">
        <section className="summary">
          <div className={`readiness ${issues.length ? "blocked" : ""}`}>
            <Archive />
            <div>
              <span>
                {issues.length ? "Export blocked" : "Ready to export"}
              </span>
              <b>
                {issues.length
                  ? `${issues.length} diagnostic${issues.length === 1 ? "" : "s"}`
                  : `${inventory.length} declared entries`}
              </b>
            </div>
          </div>
          <h2>Validation diagnostics</h2>
          {issues.length ? (
            issues.map((i) => (
              <p className="diagnostic" key={i}>
                ● {i}
              </p>
            ))
          ) : (
            <p className="success">All current structural checks passed.</p>
          )}
        </section>
        <section className="inventory">
          <div className="panel-heading">
            <b>Manifest inventory</b>
            <span>{inventory.length}</span>
          </div>
          {inventory.length ? (
            inventory.map((e) => (
              <div className="inventory-row" key={`${e.kind}:${e.id}`}>
                <span>{e.kind}</span>
                <b>{e.id}</b>
                <small>{e.path}</small>
              </div>
            ))
          ) : (
            <Empty
              icon={Archive}
              title="Package is empty"
              body="Create records and import assets to populate the inventory."
            />
          )}
        </section>
      </div>
    </>
  );
}
