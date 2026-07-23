import type { AuthoringWorkspace } from "./models";

export interface DraftAdapter {
  load(key: string): Promise<AuthoringWorkspace | null>;
  save(key: string, workspace: AuthoringWorkspace): Promise<void>;
  remove(key: string): Promise<void>;
}

const DRAFT_VERSION = 1;
interface DraftEnvelope {
  version: 1;
  workspace: AuthoringWorkspace;
}

function encode(workspace: AuthoringWorkspace): string {
  return JSON.stringify({ version: DRAFT_VERSION, workspace });
}
function decode(value: string): AuthoringWorkspace {
  const parsed: unknown = JSON.parse(value);
  if (
    !parsed ||
    typeof parsed !== "object" ||
    (parsed as { version?: unknown }).version !== DRAFT_VERSION
  )
    throw new Error("draft.unsupported-version");
  const workspace = (parsed as DraftEnvelope).workspace;
  if (
    !workspace ||
    typeof workspace !== "object" ||
    !Array.isArray(workspace.assets) ||
    !Array.isArray(workspace.npcs) ||
    !workspace.package
  )
    throw new Error("draft.malformed");
  return structuredClone(workspace);
}

export class MemoryDraftAdapter implements DraftAdapter {
  private readonly drafts = new Map<string, string>();
  async load(key: string): Promise<AuthoringWorkspace | null> {
    const value = this.drafts.get(key);
    return value ? decode(value) : null;
  }
  async save(key: string, workspace: AuthoringWorkspace): Promise<void> {
    this.drafts.set(key, encode(workspace));
  }
  async remove(key: string): Promise<void> {
    this.drafts.delete(key);
  }
}

export class LocalStorageDraftAdapter implements DraftAdapter {
  constructor(
    private readonly storage: Pick<
      Storage,
      "getItem" | "setItem" | "removeItem"
    >,
    private readonly prefix = "mythos.content-studio.draft.",
  ) {}
  async load(key: string): Promise<AuthoringWorkspace | null> {
    const value = this.storage.getItem(this.prefix + key);
    return value ? decode(value) : null;
  }
  async save(key: string, workspace: AuthoringWorkspace): Promise<void> {
    this.storage.setItem(this.prefix + key, encode(workspace));
  }
  async remove(key: string): Promise<void> {
    this.storage.removeItem(this.prefix + key);
  }
}
