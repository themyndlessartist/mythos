export interface AssetByteAdapter {
  load(draftKey: string, assetId: string): Promise<Uint8Array | null>;
  saveBatch(
    draftKey: string,
    values: ReadonlyMap<string, Uint8Array>,
  ): Promise<void>;
  remove(draftKey: string, assetId: string): Promise<void>;
}

export class MemoryAssetByteAdapter implements AssetByteAdapter {
  private values = new Map<string, Uint8Array>();
  private key(draft: string, asset: string) {
    return `${draft}\0${asset}`;
  }
  async load(draft: string, asset: string) {
    return this.values.get(this.key(draft, asset))?.slice() ?? null;
  }
  async saveBatch(draft: string, values: ReadonlyMap<string, Uint8Array>) {
    const next = new Map(this.values);
    values.forEach((bytes, id) => next.set(this.key(draft, id), bytes.slice()));
    this.values = next;
  }
  async remove(draft: string, asset: string) {
    this.values.delete(this.key(draft, asset));
  }
}

export class IndexedDbAssetByteAdapter implements AssetByteAdapter {
  constructor(
    private readonly name = "mythos-content-studio",
    private readonly indexedDb: IDBFactory = indexedDB,
  ) {}
  private open(): Promise<IDBDatabase> {
    return new Promise<IDBDatabase>((resolve, reject) => {
      const request = this.indexedDb.open(this.name, 1);
      request.onupgradeneeded = () =>
        request.result.createObjectStore("assets");
      request.onsuccess = () => resolve(request.result);
      request.onerror = () =>
        reject(request.error ?? new Error("storage.open-failed"));
    });
  }
  async load(draft: string, asset: string): Promise<Uint8Array | null> {
    const db = await this.open();
    return new Promise<Uint8Array | null>((resolve, reject) => {
      const request = db
        .transaction("assets")
        .objectStore("assets")
        .get([draft, asset]);
      request.onsuccess = () =>
        resolve(
          request.result ? new Uint8Array(request.result as ArrayBuffer) : null,
        );
      request.onerror = () =>
        reject(request.error ?? new Error("storage.load-failed"));
    }).finally(() => db.close());
  }
  async saveBatch(
    draft: string,
    values: ReadonlyMap<string, Uint8Array>,
  ): Promise<void> {
    const db = await this.open();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction("assets", "readwrite");
      values.forEach((bytes, id) =>
        tx.objectStore("assets").put(bytes.slice().buffer, [draft, id]),
      );
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error ?? new Error("storage.save-failed"));
      tx.onabort = () => reject(tx.error ?? new Error("storage.save-aborted"));
    }).finally(() => db.close());
  }
  async remove(draft: string, asset: string): Promise<void> {
    const db = await this.open();
    await new Promise<void>((resolve, reject) => {
      const request = db
        .transaction("assets", "readwrite")
        .objectStore("assets")
        .delete([draft, asset]);
      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    }).finally(() => db.close());
  }
}
