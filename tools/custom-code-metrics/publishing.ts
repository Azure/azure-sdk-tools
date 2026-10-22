import { createHash } from "node:crypto";
import { gzipSync } from "node:zlib";
import { readFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { acceptSnapshot, measurementKey, withoutFileEvidence, type Observation } from "./dashboard/data.ts";
import {
  acceptReportIndex, acceptHistoryMonth, compactObservation, mergeObservations,
} from "./dashboard/report.ts";

const hash = (text: string): string => createHash("sha256").update(text).digest("hex");
const serialize = (value: unknown): string => JSON.stringify(value);

export type BlobDocument = { text: string; etag: string | null };
export type BlobRequestOptions = Omit<RequestInit, "headers"> & { headers?: Record<string, string> };
export type BlobFetcher = (url: string, options: BlobRequestOptions) => Promise<Response>;
export interface BlobStore {
  read(container: string, path: string, optional?: boolean): Promise<BlobDocument | null>;
  immutable(container: string, path: string, text: string): Promise<void>;
  index(text: string, etag: string | null): Promise<void>;
}

export class AzureBlobStore implements BlobStore {
  private readonly base: string;
  private readonly token: string;
  private readonly fetcher: BlobFetcher;

  constructor(account: string, token: string | undefined, fetcher: BlobFetcher = fetch) {
    if (!/^[a-z0-9]{3,24}$/.test(account) || !token) throw new Error("Storage account and access token are required.");
    this.base = `https://${account}.blob.core.windows.net/`;
    this.token = token;
    this.fetcher = fetcher;
  }

  async request(container: string, path: string, options: BlobRequestOptions = {}): Promise<Response> {
    if (!["archive", "reports"].includes(container) || !/^[a-zA-Z0-9./-]+$/.test(path) ||
      path.includes("..") || path.startsWith("/")) throw new Error("Invalid publication target.");
    return this.fetcher(`${this.base}${container}/${path}`, {
      ...options,
      headers: {
        "Authorization": `Bearer ${this.token}`, "x-ms-version": "2023-11-03",
        "x-ms-date": new Date().toUTCString(), ...options.headers,
      },
      signal: AbortSignal.timeout(120000),
    });
  }

  async read(container: string, path: string, optional = false): Promise<BlobDocument | null> {
    const response = await this.request(container, path);
    if (optional && response.status === 404) return null;
    if (!response.ok) throw new Error(`Blob read failed: HTTP ${response.status}${response.headers.get("x-ms-error-code") ?
      ` ${response.headers.get("x-ms-error-code")}` : ""} (${container}/${path}).`);
    return { text: await response.text(), etag: response.headers.get("etag") };
  }

  async put(container: string, path: string, text: string, condition: Record<string, string>, cacheControl: string): Promise<Response> {
    const response = await this.request(container, path, {
      method: "PUT", body: new Uint8Array(gzipSync(text)),
      headers: {
        "x-ms-blob-type": "BlockBlob", "Content-Type": "application/json",
        "x-ms-blob-content-type": "application/json", "x-ms-blob-content-encoding": "gzip",
        "x-ms-blob-cache-control": cacheControl, ...condition,
      },
    });
    return response;
  }

  async immutable(container: string, path: string, text: string): Promise<void> {
    const response = await this.put(container, path, text, { "If-None-Match": "*" },
      container === "reports" ? "public, max-age=31536000, immutable" : "private, no-store");
    if (response.status === 409 || response.status === 412) {
      const existing = await this.read(container, path);
      if (!existing) throw new Error("Missing referenced blob.");
      if (existing.text !== text) throw new Error(`Immutable blob already exists with different content: ${container}/${path}.`);
    } else if (!response.ok) {
      throw new Error(`Blob upload failed: HTTP ${response.status} (${container}/${path}).`);
    }
  }

  async index(text: string, etag: string | null): Promise<void> {
    const response = await this.put("reports", "dotnet/index.json", text,
      etag === null ? { "If-None-Match": "*" } : { "If-Match": etag }, "no-cache, must-revalidate");
    if (response.status === 412) throw new Error("Another publisher changed the index. Rerun against the new index; no history was overwritten.");
    if (!response.ok) throw new Error(`Index publication failed: HTTP ${response.status}.`);
  }
}

export async function publish(value: unknown, store: BlobStore): Promise<{ snapshotId: string; latest: string; historyMonths: number }> {
  const source = acceptSnapshot(value);
  if (source.repository.name !== "Azure/azure-sdk-for-net") {
    throw new Error("Publication for this repository is not configured. Prototype observations must not enter the .NET feed.");
  }
  if (source.repository.isDirty) throw new Error("Official publishing requires a clean tracked checkout.");
  if (!source.snapshotId.endsWith(`-${source.repository.commit}`)) throw new Error("Snapshot identity and commit disagree.");
  const observation = compactObservation(source);
  const month = new Date(source.collectedAt).toISOString().slice(0, 7);
  const before = await store.read("reports", "dotnet/index.json", true);
  const prior = before ? acceptReportIndex(JSON.parse(before.text)) : null;
  if (before && !before.etag) throw new Error("Existing index has no ETag; cannot safely publish.");
  let existing: Observation[] = [];
  const priorObservations: Observation[] = [];
  for (const reference of prior?.history ?? []) {
    const document = await store.read("reports", `dotnet/${reference.path}`);
    if (!document) throw new Error("Missing referenced blob.");
    if (reference.path.split("/")[1] !== hash(document.text)) throw new Error("Published history content address does not match.");
    const history = acceptHistoryMonth(JSON.parse(document.text));
    if (history.month !== reference.month ||
        history.observations.some((entry) => entry.repository.isDirty || measurementKey(entry) !== measurementKey(source))) {
      throw new Error("Existing official history month is inconsistent.");
    }
    if (reference.month === month) existing = history.observations;
    priorObservations.push(...history.observations);
  }
  const history = acceptHistoryMonth({
    schemaVersion: "1.0", month, observations: mergeObservations([...existing, observation]),
  });
  const publicSnapshot = withoutFileEvidence(source);
  const latestPath = `snapshots/${source.snapshotId}.json`;
  let latest = latestPath;
  if (prior) {
    const document = await store.read("reports", `dotnet/${prior.latest}`);
    if (!document) throw new Error("Missing referenced blob.");
    const previous = acceptSnapshot(JSON.parse(document.text));
    if (previous.repository.isDirty || measurementKey(previous) !== measurementKey(source) ||
        prior.latest !== `snapshots/${previous.snapshotId}.json`) {
      throw new Error("Existing latest snapshot is inconsistent.");
    }
    const matching = priorObservations.filter((entry) => entry.snapshotId === previous.snapshotId);
    if (matching.length !== 1) throw new Error("Existing latest snapshot must appear exactly once in its monthly history.");
    mergeObservations([matching[0], compactObservation(previous)]);
    mergeObservations([previous, observation]);
    if (Date.parse(previous.collectedAt) > Date.parse(source.collectedAt) ||
      (Date.parse(previous.collectedAt) === Date.parse(source.collectedAt) && previous.snapshotId > source.snapshotId)) {
      latest = prior.latest;
    }
  }
  const historyText = serialize(history);
  const historyPath = `history/${hash(historyText)}/${month}.json`;
  const index = acceptReportIndex({
    schemaVersion: "1.0", latest,
    history: [...(prior?.history.filter((entry) => entry.month !== month) ?? []), { month, path: historyPath }]
      .sort((a, b) => a.month.localeCompare(b.month)),
  });
  const raw = serialize(source);
  await store.immutable("archive", `dotnet/snapshots/${source.snapshotId}/${hash(raw)}.json`, raw);
  await store.immutable("reports", `dotnet/${latestPath}`, serialize(publicSnapshot));
  await store.immutable("reports", `dotnet/${historyPath}`, historyText);
  await store.index(serialize(index), before?.etag ?? null);
  return { snapshotId: source.snapshotId, latest: index.latest, historyMonths: index.history.length };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [snapshotPath, account] = process.argv.slice(2);
  if (!snapshotPath || !account) throw new Error("Usage: node --experimental-strip-types publishing.ts <snapshot path> <storage account>");
  const token = process.env.AZURE_STORAGE_ACCESS_TOKEN;
  const store = new AzureBlobStore(account, token);
  console.log(JSON.stringify(await publish(JSON.parse(await readFile(snapshotPath, "utf8")), store)));
}
