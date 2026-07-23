export class CommandHistory<T> {
  private past: T[] = [];
  private future: T[] = [];
  constructor(
    private current: T,
    private readonly clone: (state: T) => T = (state) => structuredClone(state),
  ) {}
  get value(): T {
    return this.clone(this.current);
  }
  get canUndo(): boolean {
    return this.past.length > 0;
  }
  get canRedo(): boolean {
    return this.future.length > 0;
  }
  execute(update: (state: T) => T): T {
    this.past.push(this.clone(this.current));
    this.current = this.clone(update(this.clone(this.current)));
    this.future = [];
    return this.value;
  }
  undo(): T {
    const previous = this.past.pop();
    if (previous !== undefined) {
      this.future.push(this.clone(this.current));
      this.current = previous;
    }
    return this.value;
  }
  redo(): T {
    const next = this.future.pop();
    if (next !== undefined) {
      this.past.push(this.clone(this.current));
      this.current = next;
    }
    return this.value;
  }
}
