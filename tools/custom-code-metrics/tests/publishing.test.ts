import { test } from "node:test";
import assert from "node:assert/strict";
import { gunzipSync } from "node:zlib";
import { createHash } from "node:crypto";
import { AzureBlobStore, publish, type BlobStore, type BlobDocument, type BlobRequestOptions } from "../publishing.ts";
import { acceptHistoryMonth, acceptReportIndex } from "../dashboard/report.ts";
import { snapshot, legacySnapshot, library } from "../dashboard/tests/fixtures.ts";

class Store implements BlobStore {
  blobs = new Map<string, BlobDocument>();
  operations: string[] = [];
  revision = 0;
  fail = "";
  async read(container: string, path: string, optional = false): Promise<BlobDocument | null> {
    const value = this.blobs.get(`${container}/${path}`);
    if (!value && !optional) throw new Error("Missing referenced blob.");
    return value ?? null;
  }
  async immutable(container: string, path: string, text: string): Promise<void> {
    const key = `${container}/${path}`;
    this.operations.push(key);
    if (key.includes(this.fail) && this.fail) throw new Error("Simulated upload failure.");
    const prior = this.blobs.get(key);
    if (prior && prior.text !== text) throw new Error("Immutable content conflict.");
    this.blobs.set(key, { text, etag: `"${++this.revision}"` });
  }
  async index(text: string, etag: string | null): Promise<void> {
    if (this.fail === "index") throw new Error("Simulated index failure.");
    assert.equal(etag, this.blobs.get("reports/dotnet/index.json")?.etag ?? null);
    const index = acceptReportIndex(JSON.parse(text));
    assert.ok(this.blobs.has(`reports/dotnet/${index.latest}`));
    for (const reference of index.history) assert.ok(this.blobs.has(`reports/dotnet/${reference.path}`));
    this.operations.push("index");
    this.blobs.set("reports/dotnet/index.json", { text, etag: `"${++this.revision}"` });
  }
}
const blob = (store: Store, key: string): BlobDocument => {
  const value = store.blobs.get(key);
  assert.ok(value, `Missing test blob: ${key}`);
  return value;
};
const latestIndex = (store: Store) => acceptReportIndex(JSON.parse(blob(store, "reports/dotnet/index.json").text));
const month = (store: Store, name: string) => {
  const reference = latestIndex(store).history.find((entry) => entry.month === name);
  assert.ok(reference, `Missing test history month: ${name}`);
  return acceptHistoryMonth(JSON.parse(blob(store, `reports/dotnet/${reference.path}`).text));
};

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
  const raw = JSON.parse(blob(store, store.operations[0]).text);
  const publicValue = JSON.parse(blob(store, `reports/dotnet/${latestIndex(store).latest}`).text);
  assert.equal(raw.libraries[0].files.length, 1);
  assert.equal(publicValue.libraries[0].files, undefined);
  assert.equal("files" in month(store, "2026-10").observations[0].libraries[0], false);
});
test("invalid, dirty and inconsistent-identity inputs are rejected before uploading", async () => {
  for (const input of [{}, snapshot(undefined, undefined, "1", true), { ...snapshot(), snapshotId: snapshot(undefined, undefined, "2").snapshotId },
    { ...snapshot(), collectedAt: "2026-10-02T12:00:00Z" }]) {
    const store = new Store();
    await assert.rejects(publish(input, store));
    assert.equal(store.operations.length, 0);
  }
});
test("old versions and prototype-1 input cannot be republished as the initial format or modify its index", async () => {
  const store = new Store();
  await publish(snapshot(), store);
  const before = structuredClone(store.blobs);
  store.operations = [];
  for (const prior of [...["0.0", "2.0", "3.0"].map((schemaVersion) =>
    ({ ...snapshot(undefined, "2026-10-02T12:00:00Z", "2"), schemaVersion })), legacySnapshot()]) {
    const original = structuredClone(prior);
    await assert.rejects(publish(prior, store), /Invalid snapshot/);
    assert.deepEqual(prior, original);
  }
  assert.deepEqual(store.blobs, before);
  assert.deepEqual(store.operations, []);
});
test("publication rejects prior-version history months before writes, including older months behind the current latest", async () => {
  for (const [schemaVersion, oldMonth] of ["0.0", "2.0", "3.0"]
    .flatMap((version) => ["2026-10", "2026-11"].map((month) => [version, month]))) {
    const store = new Store();
    await publish(snapshot(), store);
    await publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store);
    const index = latestIndex(store);
    const reference = index.history.find((reference) => reference.month === oldMonth);
    assert.ok(reference);
    const document = month(store, oldMonth);
    Object.assign(document.observations[0], { schemaVersion });
    const text = JSON.stringify(document);
    reference.path = `history/${createHash("sha256").update(text).digest("hex")}/${oldMonth}.json`;
    store.blobs.set(`reports/dotnet/${reference.path}`, { text, etag: '"v2"' });
    blob(store, "reports/dotnet/index.json").text = JSON.stringify(index);
    const before = structuredClone(store.blobs);
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-11-02T12:00:00Z", "3"), store), /Invalid history/);
    assert.deepEqual(store.blobs, before, "Incompatible immutable history or index was rewritten.");
    assert.deepEqual(store.operations, []);
  }
});
test("a prototype latest is rejected even when all months contain current observations", async () => {
  for (const schemaVersion of ["0.0", "2.0", "3.0"]) {
    const store = new Store();
    await publish(snapshot(), store);
    const key = `reports/dotnet/${latestIndex(store).latest}`;
    const prior = JSON.parse(blob(store, key).text);
    prior.schemaVersion = schemaVersion;
    blob(store, key).text = JSON.stringify(prior);
    const before = structuredClone(store.blobs);
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store), /Invalid snapshot/);
    assert.deepEqual(store.blobs, before);
    assert.deepEqual(store.operations, []);
  }
});
test("missing or corrupt historical months outside the incoming month cannot be retained in a new index", async () => {
  for (const failure of ["missing", "hash", "dirty"]) {
    const store = new Store();
    await publish(snapshot(), store);
    const index = latestIndex(store);
    const reference = index.history[0];
    const key = `reports/dotnet/${reference.path}`;
    if (failure === "missing") store.blobs.delete(key);
    else if (failure === "hash") blob(store, key).text += " ";
    else {
      const document = month(store, reference.month);
      document.observations[0].repository.isDirty = true;
      const text = JSON.stringify(document);
      reference.path = `history/${createHash("sha256").update(text).digest("hex")}/${reference.month}.json`;
      store.blobs.set(`reports/dotnet/${reference.path}`, { text, etag: '"dirty"' });
      blob(store, "reports/dotnet/index.json").text = JSON.stringify(index);
    }
    const before = structuredClone(store.blobs);
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store), /Missing|address|inconsistent/);
    assert.deepEqual(store.blobs, before);
    assert.deepEqual(store.operations, []);
  }
});
test("prior latest must exist exactly once and agree with its compact monthly history before any uploads", async () => {
  for (const failure of ["missing", "counts", "service", "duplicate"]) {
    const store = new Store();
    const original = snapshot();
    await publish(original, store);
    const index = latestIndex(store);
    const reference = index.history[0];
    const document = month(store, reference.month);
    if (failure === "missing") {
      const alternate = snapshot(undefined, "2026-10-02T12:00:00Z", "2");
      document.observations = [{
        schemaVersion: alternate.schemaVersion, snapshotId: alternate.snapshotId, collectedAt: alternate.collectedAt,
        repository: alternate.repository, libraries: alternate.libraries.map(({ projectPath, targetFrameworks, ...member }) => member),
      }];
    } else if (failure === "counts") {
      document.observations[0].libraries[0].metrics = library("Azure.One", 11, 34).metrics;
    } else if (failure === "service") {
      document.observations[0].libraries[0].service = "changed";
    } else {
      document.observations.push(structuredClone(document.observations[0]));
    }
    const text = JSON.stringify(document);
    reference.path = `history/${createHash("sha256").update(text).digest("hex")}/${reference.month}.json`;
    store.blobs.set(`reports/dotnet/${reference.path}`, { text, etag: '"changed-history"' });
    blob(store, "reports/dotnet/index.json").text = JSON.stringify(index);
    const before = structuredClone(store.blobs);
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-11-01T12:00:00Z", "3"), store), /exactly once|Conflicting measurements/);
    assert.deepEqual(store.blobs, before);
    assert.deepEqual(store.operations, []);
  }
});
test("latest/history comparison accepts reordered compact fields but still validates their counts", async () => {
  const store = new Store();
  await publish(snapshot([library("Azure.One", 10, 35), library("Azure.Two", 20, 30)]), store);
  const index = latestIndex(store);
  const reference = index.history[0];
  const document = month(store, reference.month);
  document.observations[0].libraries.reverse();
  document.observations[0].repository = { isDirty: false, commit: "1".repeat(40), name: "Azure/azure-sdk-for-net" };
  const text = JSON.stringify(document);
  reference.path = `history/${createHash("sha256").update(text).digest("hex")}/${reference.month}.json`;
  store.blobs.set(`reports/dotnet/${reference.path}`, { text, etag: '"reordered"' });
  blob(store, "reports/dotnet/index.json").text = JSON.stringify(index);
  await publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store);
  assert.equal(latestIndex(store).history.length, 2);
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
  const before = blob(store, "reports/dotnet/index.json");
  for (const item of [library("Azure.One", 20, 25), library("Azure.One", 10, 35, "renamed")]) {
    await assert.rejects(publish(snapshot([item], "2026-10-01T14:00:00Z"), store), /Conflicting measurements/);
    assert.equal(blob(store, "reports/dotnet/index.json"), before);
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
    const before = blob(store, "reports/dotnet/index.json");
    store.fail = failure;
    await assert.rejects(publish(snapshot(undefined, "2026-10-02T12:00:00Z", "2"), store), /Simulated/);
    assert.equal(blob(store, "reports/dotnet/index.json"), before);
    store.fail = "";
    await publish(snapshot(undefined, "2026-10-02T12:00:00Z", "2"), store);
    assert.equal(month(store, "2026-10").observations.length, 2);
  }
});
test("missing or tampered referenced history and absent ETag terminate before upload", async () => {
  for (const tamper of ["missing", "content", "etag"]) {
    const store = new Store(); await publish(snapshot(), store);
    const before = blob(store, "reports/dotnet/index.json");
    const key = `reports/dotnet/${latestIndex(store).history[0].path}`;
    if (tamper === "missing") store.blobs.delete(key);
    if (tamper === "content") blob(store, key).text += " ";
    if (tamper === "etag") before.etag = null;
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-10-02T12:00:00Z", "2"), store), /Missing|address|ETag/);
    assert.equal(store.operations.length, 0);
    assert.equal(blob(store, "reports/dotnet/index.json"), before);
  }
});
test("a store returning a missing required document fails explicitly before any publication writes", async () => {
  for (const target of ["history", "latest"]) {
    const store = new Store();
    await publish(snapshot(), store);
    const index = latestIndex(store);
    const missing = `dotnet/${target === "history" ? index.history[0].path : index.latest}`;
    const originalRead = store.read.bind(store);
    store.read = async (container, path, optional) =>
      path === missing ? null : originalRead(container, path, optional);
    const before = structuredClone(store.blobs);
    store.operations = [];
    await assert.rejects(publish(snapshot(undefined, "2026-11-01T12:00:00Z", "2"), store), /Missing referenced blob/);
    assert.deepEqual(store.blobs, before);
    assert.deepEqual(store.operations, []);
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
  assert.equal(month(store, "2026-10").observations.at(-1)!.repository.commit, "3".repeat(40));
});
test("REST transport uses Entra, gzip JSON, immutable caches and conditional writes", async () => {
  const requests: { url: string; options: BlobRequestOptions }[] = [];
  const store = new AzureBlobStore("testaccount", "test-only-token", async (url, options) => {
    requests.push({ url, options });
    return new Response("", { status: 201 });
  });
  await store.immutable("reports", "dotnet/snapshots/test.json", '{"measured":true}');
  const { options } = requests[0];
  assert.ok(options.headers);
  assert.equal(options.headers.Authorization, "Bearer test-only-token");
  assert.equal(options.headers["If-None-Match"], "*");
  assert.equal(options.headers["x-ms-blob-content-encoding"], "gzip");
  assert.ok(options.body instanceof Uint8Array);
  assert.equal(gunzipSync(options.body).toString(), '{"measured":true}');
  assert.match(options.headers["x-ms-blob-cache-control"], /immutable/);
  await store.immutable("archive", "dotnet/snapshots/test.json", "{}");
  assert.equal(requests[1].options.headers!["x-ms-blob-cache-control"], "private, no-store");
  await store.index("{}", '"old-etag"');
  assert.equal(requests[2].options.headers!["If-Match"], '"old-etag"');
  assert.match(requests[2].options.headers!["x-ms-blob-cache-control"], /no-cache/);
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
