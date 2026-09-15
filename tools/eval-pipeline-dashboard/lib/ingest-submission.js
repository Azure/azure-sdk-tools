import { createReadStream } from "node:fs";
import { mkdtemp, mkdir, rm } from "node:fs/promises";
import { join } from "node:path";
import { createInterface } from "node:readline";
import { initializeDatabase, ingestDirectory } from "@microsoft/vally-server";
import { extractDashboardBundle } from "./dashboard-bundle.js";
import { IngestionError } from "./ingestion-error.js";
import { submissionId, submissionRunMetadata, validateSubmissionManifest } from "./submission-manifest.js";
import { upsertRunMetadata } from "./run-metadata.js";

async function countTrials(path) {
  let count = 0;
  const stream = createReadStream(path, { encoding: "utf8" });
  const lines = createInterface({ input: stream, crlfDelay: Infinity });
  try {
    for await (const line of lines) {
      if (!line.trim()) continue;
      const record = JSON.parse(line);
      if (!record || typeof record !== "object" || Array.isArray(record)) throw new Error("Invalid JSONL record.");
      if (["run-summary", "log"].includes(record.type)) continue;
      if (record.type !== undefined && record.type !== "trial-result") throw new Error("Unsupported JSONL record type.");
      if (!["success", "error"].includes(record.status)) throw new Error("Invalid trial status.");
      if (record.experiment) throw new Error("Experiment bundles require a multi-variant contract; submit merged plain-eval results for now.");
      count++;
    }
    if (count === 0) throw new Error("Bundle contains no trial results.");
    return count;
  } catch {
    throw new IngestionError(422, "invalid_results", "Results must contain valid plain-eval trial records; experiment bundles are not supported yet.");
  } finally {
    lines.close();
    stream.destroy();
  }
}

function importStagedRun(db, stagePath, metadata, receipt) {
  db.prepare("ATTACH DATABASE ? AS submitted").run(stagePath);
  try {
    db.transaction(() => {
      for (const table of ["runs", "outcomes", "grader_results", "tool_calls"]) {
        const columns = db.prepare(`PRAGMA main.table_info(${table})`).all()
          .filter((column) => !(["grader_results", "tool_calls"].includes(table) && column.name === "id"))
          .map((column) => `"${column.name}"`).join(", ");
        db.exec(`INSERT INTO main.${table} (${columns}) SELECT ${columns} FROM submitted.${table}`);
      }
      db.prepare("UPDATE runs SET source = ?, started_at = ?, eval_name = ? WHERE id = ?")
        .run(receipt.blob_name, metadata.runTimestamp, metadata.pipeline, metadata.dashboardRunId);
      upsertRunMetadata(db, metadata, receipt.blob_name, {
        name: receipt.blob_name,
        etag: receipt.blob_etag ?? receipt.content_hash,
        sha256: receipt.content_hash,
        size: receipt.blob_size,
        lastModified: receipt.accepted_at,
      });
    })();
  } finally {
    db.exec("DETACH DATABASE submitted");
  }
}

export async function ingestSubmission({ db, store, stagingRoot, receipt, beforeCommit = async () => {} }) {
  const metadata = submissionRunMetadata(JSON.parse(receipt.manifest_json), receipt.id);
  metadata.acceptedAt = receipt.accepted_at;
  const existing = db.prepare(`SELECT m.blob_name, m.blob_etag, m.blob_sha256 FROM run_metadata m
    JOIN runs r ON r.id = m.run_id WHERE m.run_id = ?`).get(metadata.dashboardRunId);
  if (existing) {
    if (existing.blob_name !== receipt.blob_name || (existing.blob_sha256 ?? existing.blob_etag) !== receipt.content_hash) {
      throw new IngestionError(409, "stored_result_conflict", "Stored results do not match the submission receipt.");
    }
    return metadata.dashboardRunId;
  }
  await mkdir(stagingRoot, { recursive: true });
  const workRoot = await mkdtemp(join(stagingRoot, "ingest-"));
  let stagedDb;
  try {
    const bundlePath = join(workRoot, "dashboard-bundle.zip");
    await store.download(receipt.blob_name, bundlePath, receipt.content_hash);
    const runDirectory = join(workRoot, metadata.dashboardRunId);
    let rawManifest;
    try {
      rawManifest = await extractDashboardBundle(bundlePath, runDirectory);
    } catch {
      throw new IngestionError(422, "invalid_bundle", "Stored bundle failed archive validation.");
    }
    if (submissionId(validateSubmissionManifest(rawManifest)) !== receipt.id) {
      throw new IngestionError(422, "identity_mismatch", "Stored manifest does not match the submission identity.");
    }
    const expected = await countTrials(join(runDirectory, "results.jsonl"));
    const stagePath = join(workRoot, "validated.db");
    stagedDb = initializeDatabase(stagePath);
    const results = await ingestDirectory(stagedDb, runDirectory);
    if (results.length !== 1 || results[0].runId !== metadata.dashboardRunId || results[0].outcomeCount !== expected) {
      throw new IngestionError(422, "invalid_results", "Vally could not ingest every submitted trial; no results were published.");
    }
    stagedDb.close();
    stagedDb = null;
    await beforeCommit();
    importStagedRun(db, stagePath, metadata, receipt);
    return metadata.dashboardRunId;
  } finally {
    stagedDb?.close();
    await rm(workRoot, { recursive: true, force: true });
  }
}