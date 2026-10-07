import { createHash } from "node:crypto";
import { gzipSync } from "node:zlib";
import { readFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { acceptSnapshot, withoutFileEvidence } from "./dashboard/generated/data.mjs";
import {
  acceptReportIndex, acceptHistoryMonth, compactObservation, mergeObservations,
} from "./dashboard/generated/report.mjs";

const hash = (text) => createHash("sha256").update(text).digest("hex");
const serialize = (value) => JSON.stringify(value);

export class AzureBlobStore {
  constructor(account, token, fetcher = fetch) {
    if (!/^[a-z0-9]{3,24}$/.test(account) || !token) throw new Error("Storage account and access token are required.");
    this.base = `https://${account}.blob.core.windows.net/`;
    this.token = token;
    this.fetcher = fetcher;
  }

  async request(container, path, options = {}) {
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

  async read(container, path, optional = false) {
    const response = await this.request(container, path);
    if (optional && response.status === 404) return null;
    if (!response.ok) throw new Error(`Blob read failed: HTTP ${response.status}${response.headers.get("x-ms-error-code") ?
      ` ${response.headers.get("x-ms-error-code")}` : ""} (${container}/${path}).`);
    return { text: await response.text(), etag: response.headers.get("etag") };
  }

  async put(container, path, text, condition, cacheControl) {
    const response = await this.request(container, path, {
      method: "PUT", body: gzipSync(text),
      headers: {
        "x-ms-blob-type": "BlockBlob", "Content-Type": "application/json",
        "x-ms-blob-content-type": "application/json", "x-ms-blob-content-encoding": "gzip",
        "x-ms-blob-cache-control": cacheControl, ...condition,
      },
    });
    return response;
  }

  async immutable(container, path, text) {
    const response = await this.put(container, path, text, { "If-None-Match": "*" },
      container === "reports" ? "public, max-age=31536000, immutable" : "private, no-store");
    if (response.status === 409 || response.status === 412) {
      const existing = await this.read(container, path);
      if (existing.text !== text) throw new Error(`Immutable blob already exists with different content: ${container}/${path}.`);
    } else if (!response.ok) {
      throw new Error(`Blob upload failed: HTTP ${response.status} (${container}/${path}).`);
    }
  }

  async index(text, etag) {
    const response = await this.put("reports", "dotnet/index.json", text,
      etag === null ? { "If-None-Match": "*" } : { "If-Match": etag }, "no-cache, must-revalidate");
    if (response.status === 412) throw new Error("Another publisher changed the index. Rerun against the new index; no history was overwritten.");
    if (!response.ok) throw new Error(`Index publication failed: HTTP ${response.status}.`);
  }
}

export async function publish(value, store) {
  const source = acceptSnapshot(value);
  if (source.repository.isDirty) throw new Error("Official publishing requires a clean tracked checkout.");
  if (!source.snapshotId.endsWith(`-${source.repository.commit}`)) throw new Error("Snapshot identity and commit disagree.");
  const observation = compactObservation(source);
  const month = new Date(source.collectedAt).toISOString().slice(0, 7);
  const before = await store.read("reports", "dotnet/index.json", true);
  const prior = before ? acceptReportIndex(JSON.parse(before.text)) : null;
  if (before && !before.etag) throw new Error("Existing index has no ETag; cannot safely publish.");
  let existing = [];
  for (const reference of prior?.history ?? []) {
    const document = await store.read("reports", `dotnet/${reference.path}`);
    if (reference.path.split("/")[1] !== hash(document.text)) throw new Error("Published history content address does not match.");
    const history = acceptHistoryMonth(JSON.parse(document.text));
    if (history.month !== reference.month || history.observations.some((entry) => entry.repository.isDirty)) {
      throw new Error("Existing official history month is inconsistent.");
    }
    if (reference.month === month) existing = history.observations;
  }
  const history = acceptHistoryMonth({
    schemaVersion: "1.0", month, observations: mergeObservations([...existing, observation]),
  });
  const publicSnapshot = withoutFileEvidence(source);
  const latestPath = `snapshots/${source.snapshotId}.json`;
  let latest = latestPath;
  if (prior) {
    const document = await store.read("reports", `dotnet/${prior.latest}`);
    const previous = acceptSnapshot(JSON.parse(document.text));
    if (previous.repository.isDirty || prior.latest !== `snapshots/${previous.snapshotId}.json`) {
      throw new Error("Existing latest snapshot is inconsistent.");
    }
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
  if (!snapshotPath || !account) throw new Error("Usage: node publishing.mjs <snapshot path> <storage account>");
  const token = process.env.AZURE_STORAGE_ACCESS_TOKEN;
  const store = new AzureBlobStore(account, token);
  console.log(JSON.stringify(await publish(JSON.parse(await readFile(snapshotPath, "utf8")), store)));
}
