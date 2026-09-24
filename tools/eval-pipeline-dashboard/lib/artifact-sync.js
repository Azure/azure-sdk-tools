import { mkdir, mkdtemp, rm } from "node:fs/promises";
import { join, relative, resolve, sep } from "node:path";
import { ingestDirectory } from "@microsoft/vally-server";
import {
  extractDashboardBundle,
  readDashboardBundleManifest,
} from "./dashboard-bundle.js";
import {
  downloadLocalBlob,
  getLocalBlobPath,
  listLocalBlobs,
} from "./local-blob-store.js";
import {
  getBlobIngestion,
  normalizeRunManifest,
  upsertRunMetadata,
} from "./run-metadata.js";

function toBlobName(blobRoot, path) {
  return relative(resolve(blobRoot), resolve(path)).split(sep).join("/");
}

export async function syncArtifactRuns({ db, blobRoot, stagingRoot, log = console.error }) {
  await mkdir(stagingRoot, { recursive: true });
  const blobs = await listLocalBlobs(blobRoot);
  const report = { listed: blobs.length, downloaded: 0, ingested: 0, skipped: 0 };

  for (const blob of blobs) {
    const existing = getBlobIngestion(db, blob.name);
    if (existing) {
      if (existing.blob_etag !== blob.etag) {
        throw new Error(
          `Immutable Blob '${blob.name}' changed from ETag '${existing.blob_etag}' to '${blob.etag}'.`
        );
      }
      report.skipped++;
      continue;
    }

    const workRoot = await mkdtemp(join(stagingRoot, "blob-"));
    try {
      const bundlePath = join(workRoot, "dashboard-bundle.zip");
      await downloadLocalBlob(blob, bundlePath);
      report.downloaded++;

      const rawManifest = await readDashboardBundleManifest(bundlePath);
      const expectedName = toBlobName(blobRoot, getLocalBlobPath(blobRoot, rawManifest));
      if (blob.name !== expectedName) {
        throw new Error(
          `Blob path '${blob.name}' does not match manifest identity '${expectedName}'.`
        );
      }

      const metadata = normalizeRunManifest(rawManifest);
      const runDirectory = join(workRoot, metadata.dashboardRunId);
      await extractDashboardBundle(bundlePath, runDirectory);
      const results = await ingestDirectory(db, runDirectory);
      if (results.length !== 1 || results[0].runId !== metadata.dashboardRunId) {
        throw new Error(
          `Expected one staged run '${metadata.dashboardRunId}', received '${results.map((result) => result.runId).join(", ")}'.`
        );
      }

      upsertRunMetadata(db, metadata, blob.name, blob);
      report.ingested++;
    } finally {
      await rm(workRoot, { force: true, recursive: true });
    }
  }

  if (report.ingested > 0) {
    log(
      `[blob-sync] ingested ${report.ingested} new bundle(s); skipped ${report.skipped} known bundle(s).`
    );
  }
  return report;
}

export function startArtifactSync({ db, blobRoot, stagingRoot, pollMs = 30000, log = console.error }) {
  let running = false;
  let rerun = false;

  async function sync(reason) {
    if (running) {
      rerun = true;
      return;
    }

    running = true;
    try {
      await syncArtifactRuns({ db, blobRoot, stagingRoot, log });
    } catch (error) {
      log(`[artifact-sync] ${reason} failed: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      running = false;
      if (rerun) {
        rerun = false;
        void sync("coalesced");
      }
    }
  }

  void sync("startup");
  const poll = setInterval(() => void sync("poll"), pollMs);

  return () => clearInterval(poll);
}