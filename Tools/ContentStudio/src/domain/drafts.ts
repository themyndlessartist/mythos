import type { AuthoringWorkspace } from "./models";

export interface DraftAdapter {
  load(key: string): Promise<AuthoringWorkspace | null>;
  save(key: string, workspace: AuthoringWorkspace): Promise<void>;
  remove(key: string): Promise<void>;
}

export class MemoryDraftAdapter implements DraftAdapter {
  private readonly drafts = new Map<string, string>();
  async load(key: string): Promise<AuthoringWorkspace | null> {
    const value = this.drafts.get(key);
    return value ? (JSON.parse(value) as AuthoringWorkspace) : null;
  }
  async save(key: string, workspace: AuthoringWorkspace): Promise<void> {
    this.drafts.set(key, JSON.stringify(workspace));
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
    return value ? (JSON.parse(value) as AuthoringWorkspace) : null;
  }
  async save(key: string, workspace: AuthoringWorkspace): Promise<void> {
    this.storage.setItem(this.prefix + key, JSON.stringify(workspace));
  }
  async remove(key: string): Promise<void> {
    this.storage.removeItem(this.prefix + key);
  }
}
