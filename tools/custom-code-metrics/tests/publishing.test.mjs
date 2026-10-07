import { test } from "node:test";
import assert from "node:assert/strict";
import { gunzipSync } from "node:zlib";
import { createHash } from "node:crypto";
import { AzureBlobStore, publish } from "../publishing.mjs";
import { snapshot, library } from "../dashboard/tests/fixtures.mjs";

class Store {
  blobs = new Map();
  operations = [];
  revision = 0;
  fail = "";
  async read(container, path, optional = false) {
    const value = this.blobs.get(`${container}/${path}`);
    if (!value && !optional) throw new Error("Missing referenced blob.");
    return value ?? null;
  }
  async immutable(container, path, text) {
    const key = `${container}/${path}`;
    this.operations.push(key);
    if (key.includes(this.fail) && this.fail) throw new Error("Simulated upload failure.");
    const prior = this.blobs.get(key);
    if (prior && prior.text !== text) throw new Error("Immutable content conflict.");
    this.blobs.set(key, { text, etag: `"${++this.revision}"` });
  }
  async index(text, etag) {
    if (this.fail === "index") throw new Error("Simulated index failure.");
    assert.equal(etag, this.blobs.get("reports/dotnet/index.json")?.etag ?? null);
    const index = JSON.parse(text);
    assert.ok(this.blobs.has(`reports/dotnet/${index.latest}`));
    for (const reference of index.history) assert.ok(this.blobs.has(`reports/dotnet/${reference.path}`));
    this.operations.push("index");
    this.blobs.set("reports/dotnet/index.json", { text, etag: `"${++this.revision}"` });
  }
}
const latestIndex = (store) => JSON.parse(store.blobs.get("reports/dotnet/index.json").text);
const month = (store, name) => JSON.parse(store.blobs.get(`reports/dotnet/${latestIndex(store).history.find((reference) => reference.month === name).path}`).text);

test("first publication validates and archives privately, then exposes complete immutable report and index last", async () => {
  const store = new Store();
  const input = snapshot();
  const result = await publish(input, store);
  assert.equal(result.snapshotId, input.snapshotId);
  assert.equal(store.operations.at(-1), "index");
  assert.match(store.operations[0], /^archive\/dotnet\/snapshots\//);
  assert.equal(latestIndex(store).latest, `snapshots/${input.snapshotId}.json`);
  assert.equal(month(store, "2026-10").observations.length, 1);
  assert.deepEqual(input, snapshot(), "Publishing mutated the measured snapshot.");
});
test("full file audit remains only in the private archive", async () => {
  const store = new Store();
  const input = snapshot([library("Azure.One", 10)]);
  input.libraries[0].files = [{ path: "sdk/alpha/Azure.One/src/One.cs", lines: 10, provenance: "custom", evidence: "no-generated-signal" }];
  await publish(input, store);
  const raw = JSON.parse(store.blobs.get(store.operations[0]).text);
  const publicValue = JSON.parse(store.blobs.get(`reports/dotnet/${latestIndex(store).latest}`).text);
  assert.equal(raw.libraries[0].files.length, 1);
  assert.equal(publicValue.libraries[0].files, undefined);
  assert.equal(month(store, "2026-10").observations[0].libraries[0].files, undefined);
});
test("invalid, dirty and inconsistent-identity inputs are rejected before uploading", async () => {
  for (const input of [{}, snapshot(undefined, undefined, "1", true), { ...snapshot(), snapshotId: snapshot(undefined, undefined, "2").snapshotId },
    { ...snapshot(), collectedAt: "2026-10-02T12:00:00Z" }]) {
    const store = new Store();
    await assert.rejects(publish(input, store));
    assert.equal(store.operations.length, 0);
  }
});
test("v2 input cannot be republished as v3 or modify the existing index", async () => {
  const store = new Store();
  await publish(snapshot(), store);
  const prior = { ...snapshot(undefined, "2026-10-02T12:00:00Z", "2"), schemaVersion: "2.0" };
  const before = structuredClone(store.blobs);
  store.operations = [];
  await assert.rejects(publish(prior, store), /Invalid snapshot/);
  assert.equal(prior.schemaVersion, "2.0");
  assert.deepEqual(store.blobs, before);
  assert.deepEqual(store.operations, []);
});
test("publication rejects any referenced v2 history month before writes, including older months behind a v3 latest", async () => {
  for (const oldMonth of ["2026-10", "2026-11"]) {
    const store = new Store();
    await publish(snapshot(), store);
    await publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store);
    const index = latestIndex(store);
    const reference = index.history.find((reference) => reference.month === oldMonth);
    const document = month(store, oldMonth);
    document.observations[0].schemaVersion = "2.0";
    const text = JSON.stringify(document);
    reference.path = `history/${createHash("sha256").update(text).digest("hex")}/${oldMonth}.json`;
    store.blobs.set(`reports/dotnet/${reference.path}`, { text, etag: '"v2"' });
    store.blobs.get("reports/dotnet/index.json").text = JSON.stringify(index);
    const before = structuredClone(store.blobs);
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-11-02T12:00:00Z", "3"), store), /Invalid history/);
    assert.deepEqual(store.blobs, before, "Incompatible immutable history or index was rewritten.");
    assert.deepEqual(store.operations, []);
  }
});
test("a prior v2 latest is rejected even when all referenced months contain v3 observations", async () => {
  const store = new Store();
  await publish(snapshot(), store);
  const key = `reports/dotnet/${latestIndex(store).latest}`;
  const prior = JSON.parse(store.blobs.get(key).text);
  prior.schemaVersion = "2.0";
  store.blobs.get(key).text = JSON.stringify(prior);
  const before = structuredClone(store.blobs);
  store.operations = [];
  await assert.rejects(publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store), /Invalid snapshot/);
  assert.deepEqual(store.blobs, before);
  assert.deepEqual(store.operations, []);
});
test("missing or corrupt historical months outside the incoming month cannot be retained in a new index", async () => {
  for (const failure of ["missing", "hash", "dirty"]) {
    const store = new Store();
    await publish(snapshot(), store);
    const index = latestIndex(store);
    const reference = index.history[0];
    const key = `reports/dotnet/${reference.path}`;
    if (failure === "missing") store.blobs.delete(key);
    else if (failure === "hash") store.blobs.get(key).text += " ";
    else {
      const document = month(store, reference.month);
      document.observations[0].repository.isDirty = true;
      const text = JSON.stringify(document);
      reference.path = `history/${createHash("sha256").update(text).digest("hex")}/${reference.month}.json`;
      store.blobs.set(`reports/dotnet/${reference.path}`, { text, etag: '"dirty"' });
      store.blobs.get("reports/dotnet/index.json").text = JSON.stringify(index);
    }
    const before = structuredClone(store.blobs);
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store), /Missing|address|inconsistent/);
    assert.deepEqual(store.blobs, before);
    assert.deepEqual(store.operations, []);
  }
});
test("retry is idempotent and same-day later collection replaces only the compact daily observation", async () => {
  const store = new Store();
  const first = snapshot();
  await publish(first, store);
  const blobs = store.blobs.size;
  await publish(first, store);
  assert.equal(store.blobs.size, blobs);
  const retry = snapshot(first.libraries, "2026-10-01T14:00:00Z");
  await publish(retry, store);
  assert.equal(month(store, "2026-10").observations.length, 1);
  assert.equal(month(store, "2026-10").observations[0].snapshotId, retry.snapshotId);
  assert.ok(store.blobs.has(`reports/dotnet/snapshots/${first.snapshotId}.json`));
});
test("same-day conflicting counts or metadata cannot corrupt the last-good index", async () => {
  const store = new Store();
  await publish(snapshot(), store);
  const before = store.blobs.get("reports/dotnet/index.json");
  for (const item of [library("Azure.One", 20, 25), library("Azure.One", 10, 35, "renamed")]) {
    await assert.rejects(publish(snapshot([item], "2026-10-01T14:00:00Z"), store), /Conflicting measurements/);
    assert.equal(store.blobs.get("reports/dotnet/index.json"), before);
  }
});
test("new revisions and months preserve previous membership and historical references", async () => {
  const store = new Store();
  await publish(snapshot(), store);
  const original = latestIndex(store).history[0].path;
  await publish(snapshot([library("Azure.New", 20, 80, "beta", "management")], "2026-11-01T12:00:00Z", "2"), store);
  assert.equal(latestIndex(store).history.length, 2);
  assert.equal(latestIndex(store).history[0].path, original);
  assert.equal(month(store, "2026-10").observations[0].libraries[0].library, "Azure.One");
  assert.equal(month(store, "2026-11").observations[0].libraries[0].library, "Azure.New");
});
test("out-of-order collection extends history without moving latest backward", async () => {
  const store = new Store();
  const newer = snapshot(undefined, "2026-11-01T12:00:00Z", "2");
  await publish(newer, store);
  await publish(snapshot(), store);
  assert.equal(latestIndex(store).latest, `snapshots/${newer.snapshotId}.json`);
  assert.equal(latestIndex(store).history.length, 2);
});
test("archive, snapshot, history and index failure each preserve the previous index", async () => {
  for (const failure of ["archive/", "reports/dotnet/snapshots/", "reports/dotnet/history/", "index"]) {
    const store = new Store();
    await publish(snapshot(), store);
    const before = store.blobs.get("reports/dotnet/index.json");
    store.fail = failure;
    await assert.rejects(publish(snapshot(undefined, "2026-10-02T12:00:00Z", "2"), store), /Simulated/);
    assert.equal(store.blobs.get("reports/dotnet/index.json"), before);
    store.fail = "";
    await publish(snapshot(undefined, "2026-10-02T12:00:00Z", "2"), store);
    assert.equal(month(store, "2026-10").observations.length, 2);
  }
});
test("missing or tampered referenced history and absent ETag terminate before upload", async () => {
  for (const tamper of ["missing", "content", "etag"]) {
    const store = new Store(); await publish(snapshot(), store);
    const before = store.blobs.get("reports/dotnet/index.json");
    const key = `reports/dotnet/${latestIndex(store).history[0].path}`;
    if (tamper === "missing") store.blobs.delete(key);
    if (tamper === "content") store.blobs.get(key).text += " ";
    if (tamper === "etag") before.etag = null;
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-10-02T12:00:00Z", "2"), store), /Missing|address|ETag/);
    assert.equal(store.operations.length, 0);
    assert.equal(store.blobs.get("reports/dotnet/index.json"), before);
  }
});
test("ETag races cannot overwrite concurrently published history", async () => {
  const store = new Store(); await publish(snapshot(), store);
  const prior = store.index.bind(store);
  store.index = async (text, etag) => {
    await publish(snapshot(undefined, "2026-10-03T12:00:00Z", "3"), { ...store,
      read: store.read.bind(store), immutable: store.immutable.bind(store), index: prior });
    return prior(text, etag);
  };
  await assert.rejects(publish(snapshot(undefined, "2026-10-02T12:00:00Z", "2"), store));
  assert.equal(month(store, "2026-10").observations.at(-1).repository.commit, "3".repeat(40));
});
test("REST transport uses Entra, gzip JSON, immutable caches and conditional writes", async () => {
  const requests = [];
  const store = new AzureBlobStore("testaccount", "test-only-token", async (url, options) => {
    requests.push({ url, options });
    return new Response("", { status: 201 });
  });
  await store.immutable("reports", "dotnet/snapshots/test.json", '{"measured":true}');
  const { options } = requests[0];
  assert.equal(options.headers.Authorization, "Bearer test-only-token");
  assert.equal(options.headers["If-None-Match"], "*");
  assert.equal(options.headers["x-ms-blob-content-encoding"], "gzip");
  assert.equal(gunzipSync(options.body).toString(), '{"measured":true}');
  assert.match(options.headers["x-ms-blob-cache-control"], /immutable/);
  await store.immutable("archive", "dotnet/snapshots/test.json", "{}");
  assert.equal(requests[1].options.headers["x-ms-blob-cache-control"], "private, no-store");
  await store.index("{}", '"old-etag"');
  assert.equal(requests[2].options.headers["If-Match"], '"old-etag"');
  assert.match(requests[2].options.headers["x-ms-blob-cache-control"], /no-cache/);
});
test("REST transport distinguishes missing bootstrap from forbidden/error reads and validates targets", async () => {
  const absent = new AzureBlobStore("testaccount", "test-only", async () => new Response("", { status: 404 }));
  assert.equal(await absent.read("reports", "dotnet/index.json", true), null);
  await assert.rejects(absent.read("reports", "dotnet/index.json"), /404/);
  const denied = new AzureBlobStore("testaccount", "test-only", async () => new Response("", { status: 403 }));
  await assert.rejects(denied.read("reports", "dotnet/index.json", true), /403/);
  assert.throws(() => new AzureBlobStore("https://other.invalid", "x"), /required/);
  await assert.rejects(absent.read("reports", "../another-container/key"), /Invalid/);
});
test("REST immutable retries verify actual bytes; index conflicts and upload failures remain explicit", async () => {
  for (const same of [true, false]) {
    const store = new AzureBlobStore("testaccount", "test-only", async (url, options) =>
      options.method === "PUT" ? new Response("", { status: 412 }) : new Response(same ? "{}" : '{"different":true}', { status: 200 }));
    if (same) await store.immutable("reports", "dotnet/history/test.json", "{}");
    else await assert.rejects(store.immutable("reports", "dotnet/history/test.json", "{}"), /different content/);
    await assert.rejects(store.index("{}", '"stale"'), /Another publisher/);
  }
  const store = new AzureBlobStore("testaccount", "test-only", async () => new Response("", { status: 503 }));
  await assert.rejects(store.immutable("reports", "dotnet/snapshots/test.json", "{}"), /503/);
  await assert.rejects(store.index("{}", null), /503/);
});
