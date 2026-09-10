export class DataStore {
  constructor(baseUrl = "data") {
    this.baseUrl = baseUrl;
    this.snapshot = null;
    this.cache = new Map();
  }

  async initialize() {
    this.snapshot = await this.fetchJson("snapshot.json", false);
    return this.snapshot;
  }

  plan(id) {
    return this.fetchJson(this.snapshot.paths.plan.replace("{id}", id));
  }

  async fetchJson(path, immutable = true) {
    const url = `${this.baseUrl}/${path}`;
    if (immutable && this.cache.has(url)) return this.cache.get(url);
    const request = fetch(url, immutable ? undefined : { cache: "no-cache" }).then(async (response) => {
      if (!response.ok)
        throw new Error(`${response.status} while loading ${path}`);
      return response.json();
    });
    if (immutable) this.cache.set(url, request);
    return request;
  }
}
