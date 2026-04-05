export class MemoryCache<V> {
  private map = new Map<string, { v: V; exp: number }>();

  constructor(private ttlMs: number) {}

  get(k: string): V | undefined {
    const e = this.map.get(k);
    if (!e) return undefined;
    if (e.exp <= Date.now()) {
      this.map.delete(k);
      return undefined;
    }
    return e.v;
  }

  set(k: string, v: V): void {
    this.map.set(k, { v, exp: Date.now() + this.ttlMs });
  }

  delete(k: string): void {
    this.map.delete(k);
  }

  clear(): void {
    this.map.clear();
  }
}
