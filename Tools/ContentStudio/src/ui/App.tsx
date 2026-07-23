import {
  ChangeEvent,
  ReactNode,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  Archive,
  Box,
  ChevronDown,
  ChevronUp,
  Download,
  FileImage,
  Image,
  Map,
  Menu,
  Plus,
  Redo2,
  Save,
  Trash2,
  Undo2,
  Users,
  X,
} from "lucide-react";
import {
  assembleExportBundle,
  BUNDLE_MEDIA_TYPE,
  canonicalJson,
  CommandHistory,
  exportReadinessDiagnostics,
  IndexedDbAssetByteAdapter,
  LocalStorageDraftAdapter,
  ordinalCompare,
  validateNpc,
  validateRasterBatch,
  validateWorkspace,
  type AssetMetadata,
  type AuthoringWorkspace,
  type Diagnostic,
  type RasterImportCandidate,
} from "../domain";

type View = "npcs" | "assets" | "maps" | "package";
const DRAFT_KEY = "default";
const draftAdapter = new LocalStorageDraftAdapter(localStorage);
const byteAdapter = new IndexedDbAssetByteAdapter();
const ref = (record_id: string) => ({
  package_id: "mythos.local-workspace",
  record_id,
});
const initialWorkspace = (): AuthoringWorkspace => ({
  package: {
    document_kind: "mythos.content-package",
    schema_version: "1.0",
    package_id: "mythos.local-workspace",
    package_version: "0.1.0",
    display_name: "Local workspace",
    entries: [],
    dependencies: [],
  },
  npcs: [
    {
      document_kind: "mythos.npc-authoring",
      schema_version: "1.0",
      npc_record_id: "mythos.npc-record",
      display_name: "Untitled NPC",
      visual: { sprite_manifest: ref("mythos.character-visuals"), options: {} },
      tags: [],
      notes: "",
    },
  ],
  sprites: [
    {
      document_kind: "mythos.sprite-animation",
      schema_version: "1.0",
      sprite_manifest_id: "mythos.character-visuals",
      display_name: "Character visuals",
      layers: [],
      options: [],
      animations: [],
    },
  ],
  maps: [
    {
      document_kind: "mythos.layered-map",
      schema_version: "1.0",
      map_manifest_id: "mythos.local-map",
      display_name: "Local map",
      background_asset: ref("mythos.missing-background"),
      layers: [],
      markers: [],
    },
  ],
  assets: [],
});
const uid = (prefix: string) =>
  `${prefix}.${crypto.randomUUID().replaceAll("-", "").slice(0, 10)}`;

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
function Diagnostics({ values }: { values: Diagnostic[] }) {
  if (!values.length) return null;
  return (
    <div className="diagnostics" role="status">
      <b>Validation</b>
      {values.map((d, i) => (
        <p key={`${d.code}-${d.path}-${i}`}>
          ● {d.code} · {d.path} — {d.message}
        </p>
      ))}
    </div>
  );
}
function Title({
  code,
  title,
  text,
  children,
}: {
  code: string;
  title: string;
  text: string;
  children?: ReactNode;
}) {
  return (
    <div className="workspace-title">
      <div>
        <span>{code}</span>
        <h1>{title}</h1>
        <p>{text}</p>
      </div>
      <div className="actions">{children}</div>
    </div>
  );
}

export function App() {
  const [history, setHistory] = useState(
    () => new CommandHistory(initialWorkspace()),
  );
  const [workspace, setWorkspace] = useState(() => history.value);
  const [view, setView] = useState<View>("npcs");
  const [selectedNpc, setSelectedNpc] = useState(
    workspace.npcs[0]?.npc_record_id ?? "",
  );
  const [urls, setUrls] = useState<Record<string, string>>({});
  const [storageMessage, setStorageMessage] = useState("Opening local draft…");
  const [sidebar, setSidebar] = useState(false);

  useEffect(() => {
    let live = true;
    void draftAdapter
      .load(DRAFT_KEY)
      .then((saved) => {
        if (!live) return;
        const next = saved ?? initialWorkspace();
        setHistory(new CommandHistory(next));
        setWorkspace(next);
        setSelectedNpc(next.npcs[0]?.npc_record_id ?? "");
        setStorageMessage(saved ? "Draft reopened locally" : "New local draft");
      })
      .catch(() => {
        if (live)
          setStorageMessage("Draft data was malformed; recovered safely");
      });
    return () => {
      live = false;
    };
  }, []);
  useEffect(() => {
    const timer = window.setTimeout(
      () =>
        void draftAdapter
          .save(DRAFT_KEY, workspace)
          .then(() => setStorageMessage("Draft saved locally"))
          .catch(() => setStorageMessage("Draft storage failed")),
      150,
    );
    return () => clearTimeout(timer);
  }, [workspace]);
  useEffect(() => {
    let live = true;
    const created: string[] = [];
    void Promise.all(
      workspace.assets.map(async (asset) => {
        const bytes = await byteAdapter.load(DRAFT_KEY, asset.id);
        if (!bytes || !live) return null;
        const url = URL.createObjectURL(
          new Blob([bytes.slice()], { type: asset.media_type }),
        );
        created.push(url);
        return [asset.id, url] as const;
      }),
    )
      .then((pairs) => {
        if (live)
          setUrls(
            Object.fromEntries(
              pairs.filter((x): x is readonly [string, string] => x !== null),
            ),
          );
      })
      .catch(() => {
        if (live) setStorageMessage("Raster storage failed");
      });
    return () => {
      live = false;
      created.forEach(URL.revokeObjectURL);
    };
  }, [workspace.assets]);
  const change = (update: (value: AuthoringWorkspace) => AuthoringWorkspace) =>
    setWorkspace(history.execute(update));
  const diagnostics = useMemo(
    () => exportReadinessDiagnostics(workspace),
    [workspace],
  );
  const undo = () => setWorkspace(history.undo());
  const redo = () => setWorkspace(history.redo());
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
          aria-label="Open navigation"
          onClick={() => setSidebar(true)}
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
          <IconButton label="Undo" disabled={!history.canUndo} onClick={undo}>
            <Undo2 />
          </IconButton>
          <IconButton label="Redo" disabled={!history.canRedo} onClick={redo}>
            <Redo2 />
          </IconButton>
          <span>
            <Save />
            {storageMessage}
          </span>
        </div>
      </header>
      <aside className={sidebar ? "open" : ""}>
        <button
          className="close-nav"
          aria-label="Close navigation"
          onClick={() => setSidebar(false)}
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
          <strong>AuthoringWorkspace</strong>
          <span>Preview only · No runtime entities</span>
        </div>
      </aside>
      {sidebar && (
        <button
          className="scrim"
          aria-label="Close navigation"
          onClick={() => setSidebar(false)}
        />
      )}
      <main>
        {view === "npcs" && (
          <NpcView
            workspace={workspace}
            selected={selectedNpc}
            select={setSelectedNpc}
            change={change}
          />
        )}
        {view === "assets" && (
          <AssetView
            workspace={workspace}
            urls={urls}
            change={change}
            report={setStorageMessage}
          />
        )}
        {view === "maps" && (
          <MapView workspace={workspace} urls={urls} change={change} />
        )}
        {view === "package" && (
          <PackageView workspace={workspace} diagnostics={diagnostics} />
        )}
      </main>
    </div>
  );
}

function NpcView({
  workspace,
  selected,
  select,
  change,
}: {
  workspace: AuthoringWorkspace;
  selected: string;
  select: (id: string) => void;
  change: (fn: (w: AuthoringWorkspace) => AuthoringWorkspace) => void;
}) {
  const current =
    workspace.npcs.find((n) => n.npc_record_id === selected) ??
    workspace.npcs[0];
  const issues = current ? validateNpc(current) : [];
  const create = () => {
    const npc = structuredClone(
      workspace.npcs[0] ?? initialWorkspace().npcs[0],
    );
    npc.npc_record_id = uid("mythos.npc");
    npc.display_name = "Untitled NPC";
    change((w) => ({ ...w, npcs: [...w.npcs, npc] }));
    select(npc.npc_record_id);
  };
  const update = (patch: Partial<NonNullable<typeof current>>) =>
    current &&
    change((w) => ({
      ...w,
      npcs: w.npcs.map((n) =>
        n.npc_record_id === current.npc_record_id ? { ...n, ...patch } : n,
      ),
    }));
  return (
    <>
      <Title
        code="DATA-002"
        title="NPC authoring"
        text="Structured authoring records validated by the domain service."
      >
        <button className="primary" onClick={create}>
          <Plus />
          New NPC
        </button>
      </Title>
      <div className="split">
        <section className="list-panel">
          <div className="panel-heading">
            <b>Records</b>
            <span>{workspace.npcs.length}</span>
          </div>
          {workspace.npcs.map((n) => (
            <button
              className={`record ${n.npc_record_id === current?.npc_record_id ? "selected" : ""}`}
              key={n.npc_record_id}
              onClick={() => select(n.npc_record_id)}
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
          {current && (
            <>
              <div className="panel-heading">
                <div>
                  <b>{current.display_name || "Unnamed NPC"}</b>
                  <span className={issues.length ? "status error" : "status"}>
                    {issues.length ? `${issues.length} issues` : "Valid draft"}
                  </span>
                </div>
                <IconButton
                  label="Delete NPC"
                  onClick={() =>
                    change((w) => ({
                      ...w,
                      npcs: w.npcs.filter(
                        (n) => n.npc_record_id !== current.npc_record_id,
                      ),
                    }))
                  }
                >
                  <Trash2 />
                </IconButton>
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
                  hint="Stable content identity; never a runtime Entity ID."
                >
                  <input
                    value={current.npc_record_id}
                    onChange={(e) => {
                      const id = e.target.value;
                      update({ npc_record_id: id });
                      select(id);
                    }}
                  />
                </Field>
                <Field label="Tags">
                  <input
                    value={(current.tags ?? []).join(", ")}
                    onChange={(e) =>
                      update({
                        tags: [
                          ...new Set(
                            e.target.value
                              .split(",")
                              .map((x) => x.trim())
                              .filter(Boolean),
                          ),
                        ].sort(ordinalCompare),
                      })
                    }
                  />
                </Field>
                <Field label="Notes">
                  <textarea
                    rows={5}
                    value={current.notes ?? ""}
                    onChange={(e) => update({ notes: e.target.value })}
                  />
                </Field>
              </div>
              <Diagnostics values={issues} />
              <details className="json">
                <summary>Canonical JSON preview</summary>
                <pre>{canonicalJson(current)}</pre>
              </details>
            </>
          )}
        </section>
      </div>
    </>
  );
}

async function decodeCandidate(
  file: File,
  path: string,
): Promise<RasterImportCandidate> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  const url = URL.createObjectURL(file);
  try {
    const dimensions = await new Promise<{ width: number; height: number }>(
      (resolve, reject) => {
        const image = new window.Image();
        image.onload = () =>
          resolve({ width: image.naturalWidth, height: image.naturalHeight });
        image.onerror = () => reject(new Error("media.decode-failed"));
        image.src = url;
      },
    );
    return {
      id: uid("mythos.asset"),
      name: file.name,
      type: file.type,
      bytes,
      path,
      ...dimensions,
    };
  } finally {
    URL.revokeObjectURL(url);
  }
}
function AssetView({
  workspace,
  urls,
  change,
  report,
}: {
  workspace: AuthoringWorkspace;
  urls: Record<string, string>;
  change: (fn: (w: AuthoringWorkspace) => AuthoringWorkspace) => void;
  report: (value: string) => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");
  const pick = async (event: ChangeEvent<HTMLInputElement>) => {
    const files = [...(event.target.files ?? [])];
    event.target.value = "";
    try {
      const proposed = await Promise.all(
        files.map((file) =>
          decodeCandidate(
            file,
            `assets/characters/${file.name.replace(/[^a-zA-Z0-9._-]/g, "-")}`,
          ),
        ),
      );
      const result = validateRasterBatch(proposed, workspace.assets);
      if (!result.accepted) throw new Error(result.diagnostics.join("; "));
      const values = new globalThis.Map(
        proposed.map((item) => [item.id, item.bytes] as const),
      );
      await byteAdapter.saveBatch(DRAFT_KEY, values);
      const metadata: AssetMetadata[] = proposed.map(
        ({ id, path, type, bytes, width, height }) => ({
          id,
          path,
          media_type: type as AssetMetadata["media_type"],
          size: bytes.byteLength,
          width,
          height,
        }),
      );
      change((w) => ({
        ...w,
        assets: [...w.assets, ...metadata],
        sprites: w.sprites.map((s, si) =>
          si
            ? s
            : {
                ...s,
                layers: [
                  ...s.layers,
                  ...metadata.map((a, i) => ({
                    layer_id: uid("mythos.layer"),
                    display_name: a.path.split("/").at(-1) ?? a.id,
                    order: s.layers.length + i,
                    asset: ref(a.id),
                  })),
                ],
              },
        ),
      }));
      setError("");
      report("Raster batch saved locally");
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Import failed atomically",
      );
    }
  };
  const sorted = [...workspace.sprites[0].layers].sort(
    (a, b) => a.order - b.order || ordinalCompare(a.layer_id, b.layer_id),
  );
  const reorder = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= sorted.length) return;
    const copy = [...sorted];
    [copy[index], copy[target]] = [copy[target], copy[index]];
    change((w) => ({
      ...w,
      sprites: w.sprites.map((s, i) =>
        i ? s : { ...s, layers: copy.map((l, order) => ({ ...l, order })) },
      ),
    }));
  };
  return (
    <>
      <Title
        code="DATA-003"
        title="Character assets"
        text="Safe raster ingestion and non-authoritative ordinal layer preview."
      >
        <button className="primary" onClick={() => input.current?.click()}>
          <Plus />
          Import images
        </button>
        <input
          ref={input}
          hidden
          multiple
          type="file"
          accept="image/png,image/jpeg,image/webp"
          onChange={pick}
        />
      </Title>
      {error && (
        <div className="diagnostics" role="alert">
          {error}
        </div>
      )}
      <div className="canvas-layout">
        <section className="layer-panel">
          <div className="panel-heading">
            <b>Visual layers</b>
            <span>{sorted.length}</span>
          </div>
          {sorted.map((layer, i) => (
            <div className="layer-row" key={layer.layer_id}>
              <span className="drag">{i + 1}</span>
              <span>
                <b>{layer.display_name}</b>
                <small>{layer.asset.record_id}</small>
              </span>
              <IconButton label="Move layer up" onClick={() => reorder(i, -1)}>
                <ChevronUp />
              </IconButton>
              <IconButton label="Move layer down" onClick={() => reorder(i, 1)}>
                <ChevronDown />
              </IconButton>
            </div>
          ))}
        </section>
        <Preview
          title="Character composition"
          ids={sorted.map((l) => l.asset.record_id)}
          urls={urls}
        />
      </div>
    </>
  );
}
function Preview({
  title,
  ids,
  urls,
}: {
  title: string;
  ids: string[];
  urls: Record<string, string>;
}) {
  return (
    <section className="preview-panel">
      <div className="panel-heading">
        <div>
          <b>{title}</b>
          <span className="status muted">Approximation · Studio only</span>
        </div>
      </div>
      <div className="preview">
        {ids.map((id) =>
          urls[id] ? (
            <img key={id} src={urls[id]} alt="Imported raster preview" />
          ) : null,
        )}
        {!ids.some((id) => urls[id]) && (
          <div className="canvas-empty">
            <Image />
            <span>Preview appears here</span>
          </div>
        )}
      </div>
    </section>
  );
}
function MapView({
  workspace,
  urls,
  change,
}: {
  workspace: AuthoringWorkspace;
  urls: Record<string, string>;
  change: (fn: (w: AuthoringWorkspace) => AuthoringWorkspace) => void;
}) {
  const map = workspace.maps[0];
  const setBackground = (id: string) =>
    change((w) => ({
      ...w,
      maps: w.maps.map((m, i) => (i ? m : { ...m, background_asset: ref(id) })),
    }));
  return (
    <>
      <Title
        code="DATA-004"
        title="Layered maps"
        text="Dimensionless preview composition; no runtime or world-coordinate meaning."
      />
      <div className="canvas-layout">
        <section className="layer-panel">
          <div className="panel-heading">
            <b>Composition</b>
          </div>
          <div className="form-grid">
            <Field label="Background raster">
              <select
                value={map.background_asset.record_id}
                onChange={(e) => setBackground(e.target.value)}
              >
                <option value="mythos.missing-background">
                  Select raster…
                </option>
                {workspace.assets.map((a) => (
                  <option value={a.id} key={a.id}>
                    {a.path}
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <Diagnostics
            values={validateWorkspace(workspace).filter(
              (d) => d.document_id === map.map_manifest_id,
            )}
          />
        </section>
        <Preview
          title="Layered map preview"
          ids={[
            map.background_asset.record_id,
            ...map.layers.map((l) => l.asset.record_id),
          ]}
          urls={urls}
        />
      </div>
    </>
  );
}
function PackageView({
  workspace,
  diagnostics,
}: {
  workspace: AuthoringWorkspace;
  diagnostics: Diagnostic[];
}) {
  const [message, setMessage] = useState("");
  const ready = !diagnostics.some((item) => item.severity === "error");
  const download = async () => {
    try {
      const result = await assembleExportBundle(
        workspace,
        DRAFT_KEY,
        byteAdapter,
      );
      const url = URL.createObjectURL(
        new Blob([result.bytes.slice()], { type: BUNDLE_MEDIA_TYPE }),
      );
      try {
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = `${workspace.package.package_id}.mythos-bundle.json`;
        anchor.click();
      } finally {
        URL.revokeObjectURL(url);
      }
      setMessage(
        `Complete bundle: ${result.bundle.files.length} files, ${result.bytes.byteLength} bytes.`,
      );
    } catch (reason) {
      setMessage(
        reason instanceof Error ? reason.message : "Export failed atomically",
      );
    }
  };
  return (
    <>
      <Title
        code="DATA-001"
        title="Package export"
        text="Complete dependency-free bundle with canonical files, actual byte sizes, and SHA-256 integrity."
      >
        <button
          className="primary"
          disabled={!ready}
          onClick={() => void download()}
        >
          <Download />
          Export complete bundle
        </button>
      </Title>
      <section className="summary">
        <div className="panel-heading">
          <div>
            <b>Export readiness</b>
            <span className={ready ? "status" : "status error"}>
              {ready
                ? "Ready"
                : `${diagnostics.filter((d) => d.severity === "error").length} blockers`}
            </span>
          </div>
        </div>
        {message && (
          <p className="package-note" role="status">
            {message}
          </p>
        )}
        <Diagnostics values={diagnostics} />
      </section>
    </>
  );
}
