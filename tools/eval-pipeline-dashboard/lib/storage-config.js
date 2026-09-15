import { resolve } from "node:path";
import { createSubmissionStore } from "./submission-store.js";
import { openIngestionJournal } from "./ingestion-journal.js";

export function storageUrl(value, name) {
  let url;
  try { url = new URL(value); } catch { throw new Error(`${name} must be a full Azure container/queue URL.`); }
  if (url.protocol !== "https:" || url.username || url.password || url.search || url.hash ||
      !/^\/[a-z0-9](?:[a-z0-9-]{1,61})[a-z0-9]\/?$/.test(url.pathname) || url.pathname.includes("--")) {
    throw new Error(`${name} must be an HTTPS container/queue URL without credentials, SAS, or extra path segments.`);
  }
  return url.href.replace(/\/$/, "");
}

export function readStorageConfig(env, root) {
  const provider = env.VALLY_STORAGE_PROVIDER || "local";
  if (!["local", "azure"].includes(provider)) throw new Error("VALLY_STORAGE_PROVIDER must be local or azure.");
  const config = {
    provider,
    dbPath: resolve(root, env.VALLY_DB || "eval.db"),
    journalPath: resolve(root, env.VALLY_INGESTION_DB || "ingestions.db"),
    stagingRoot: resolve(root, env.VALLY_STAGING_ROOT || ".blob-staging"),
    archiveRoot: resolve(root, env.VALLY_LOCAL_BLOB_ROOT || ".ingestion-archive"),
  };
  if (config.dbPath.toLowerCase() === config.journalPath.toLowerCase()) throw new Error("The ingestion journal and Vally cache must use separate database files.");
  if (provider === "azure") {
    config.containerUrl = storageUrl(env.VALLY_AZURE_CONTAINER_URL, "VALLY_AZURE_CONTAINER_URL");
    config.queueUrl = storageUrl(env.VALLY_AZURE_QUEUE_URL, "VALLY_AZURE_QUEUE_URL");
  }
  return config;
}

export async function openStorage(config, { clients, log = console.error } = {}) {
  if (config.provider === "local") {
    return { store: createSubmissionStore(config.archiveRoot), journal: openIngestionJournal(config.journalPath) };
  }
  const { createAzureBlobStore } = await import("./azure-blob-store.js");
  const { createAzureIngestionJournal } = await import("./azure-ingestion-journal.js");
  if (!clients) {
    const [{ DefaultAzureCredential }, { ContainerClient }, { QueueClient }] = await Promise.all([
      import("@azure/identity"), import("@azure/storage-blob"), import("@azure/storage-queue"),
    ]);
    const credential = new DefaultAzureCredential();
    const options = { retryOptions: { maxTries: 3, tryTimeoutInMs: 15_000 } };
    clients = { containerClient: new ContainerClient(config.containerUrl, credential, options),
      queueClient: new QueueClient(config.queueUrl, credential, options) };
  }
  return { store: createAzureBlobStore(clients.containerClient), journal: createAzureIngestionJournal({ ...clients, log }) };
}