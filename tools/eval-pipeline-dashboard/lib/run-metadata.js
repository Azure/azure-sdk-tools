import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { basename, join } from "node:path";

const DEFAULT_REPOSITORY = "local";
const DEFAULT_PIPELINE = "unclassified";

function asOptionalString(value) {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
}

function slugify(value) {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "") || "unknown";
}

function buildRunId({ repository, pipeline, sourceRunId }) {
  const identity = `${repository}\u0000${pipeline}\u0000${sourceRunId}`;
  const fingerprint = createHash("sha256").update(identity).digest("hex").slice(0, 10);
  return `${slugify(repository)}--${slugify(pipeline)}--${slugify(sourceRunId)}--${fingerprint}`;
}

export function readRunManifest(runDirectory, defaults = {}) {
  const sourceRunId = basename(runDirectory);
  const manifestPath = join(runDirectory, "manifest.json");
  let source = {};

  if (existsSync(manifestPath)) {
    try {
      source = JSON.parse(readFileSync(manifestPath, "utf8"));
    } catch (error) {
      throw new Error(
        `Invalid manifest.json in '${runDirectory}': ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }

  return normalizeRunManifest(source, { ...defaults, sourceRunId });
}

export function normalizeRunManifest(source, defaults = {}) {
  if (source === null || typeof source !== "object" || Array.isArray(source)) {
    throw new Error("manifest.json must contain a JSON object.");
  }
  const schemaVersion = source.schemaVersion ?? 1;
  if (schemaVersion !== 1) {
    throw new Error(`Unsupported manifest schemaVersion: '${schemaVersion}'.`);
  }

  const repository =
    asOptionalString(source.repo) ?? asOptionalString(defaults.repository) ?? DEFAULT_REPOSITORY;
  const pipeline =
    asOptionalString(source.pipeline) ?? asOptionalString(defaults.pipeline) ?? DEFAULT_PIPELINE;
  const sourceRunId = asOptionalString(source.runId) ?? asOptionalString(defaults.sourceRunId);
  if (!sourceRunId) {
    throw new Error("manifest.json must define a non-empty runId.");
  }

  return {
    schemaVersion,
    adoProject: asOptionalString(source.adoProject),
    repository,
    pipeline,
    pipelineDefinitionId: asOptionalString(source.pipelineDefinitionId),
    sourceRunId,
    branch: asOptionalString(source.branch),
    sourceVersion: asOptionalString(source.sourceVersion),
    buildId: asOptionalString(source.buildId),
    buildUrl: asOptionalString(source.buildUrl),
    summaryAttempt: Number.isInteger(source.summaryAttempt) ? source.summaryAttempt : null,
    runTimestamp: asOptionalString(source.runTimestamp),
    dashboardRunId: buildRunId({ repository, pipeline, sourceRunId }),
  };
}

export function initializeRunMetadata(db) {
  db.exec(`
    CREATE TABLE IF NOT EXISTS run_metadata (
      run_id TEXT PRIMARY KEY,
      source_run_id TEXT NOT NULL,
      repository TEXT NOT NULL,
      pipeline TEXT NOT NULL,
      ado_project TEXT,
      pipeline_definition_id TEXT,
      branch TEXT,
      source_version TEXT,
      build_id TEXT,
      build_url TEXT,
      summary_attempt INTEGER,
      run_timestamp TEXT,
      source_path TEXT NOT NULL,
      blob_name TEXT,
      blob_etag TEXT,
      blob_size INTEGER,
      blob_last_modified TEXT,
      ingested_at TEXT NOT NULL
    );

    CREATE INDEX IF NOT EXISTS idx_run_metadata_repository_pipeline
      ON run_metadata (repository, pipeline);
  `);

  const columns = db.prepare("PRAGMA table_info(run_metadata)").all();
  const migrations = [
    ["run_timestamp", "TEXT"],
    ["ado_project", "TEXT"],
    ["pipeline_definition_id", "TEXT"],
    ["source_version", "TEXT"],
    ["summary_attempt", "INTEGER"],
    ["blob_name", "TEXT"],
    ["blob_etag", "TEXT"],
    ["blob_size", "INTEGER"],
    ["blob_last_modified", "TEXT"],
  ];
  for (const [name, type] of migrations) {
    if (!columns.some((column) => column.name === name)) {
      db.exec(`ALTER TABLE run_metadata ADD COLUMN ${name} ${type}`);
    }
  }
  db.exec(`
    CREATE UNIQUE INDEX IF NOT EXISTS idx_run_metadata_blob
      ON run_metadata (blob_name)
      WHERE blob_name IS NOT NULL;
  `);
}

export function getBlobIngestion(db, blobName) {
  return db.prepare(`
    SELECT run_id, blob_etag
    FROM run_metadata
    WHERE blob_name = ?
  `).get(blobName);
}

export function upsertRunMetadata(db, metadata, sourcePath, blob = {}) {
  db.prepare(`
    INSERT INTO run_metadata (
      run_id, source_run_id, repository, pipeline, ado_project,
      pipeline_definition_id, branch, source_version, build_id, build_url,
      summary_attempt, run_timestamp, source_path, blob_name, blob_etag,
      blob_size, blob_last_modified, ingested_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON CONFLICT(run_id) DO UPDATE SET
      source_run_id = excluded.source_run_id,
      repository = excluded.repository,
      pipeline = excluded.pipeline,
      ado_project = excluded.ado_project,
      pipeline_definition_id = excluded.pipeline_definition_id,
      branch = excluded.branch,
      source_version = excluded.source_version,
      build_id = excluded.build_id,
      build_url = excluded.build_url,
      summary_attempt = excluded.summary_attempt,
      run_timestamp = excluded.run_timestamp,
      source_path = excluded.source_path,
      blob_name = excluded.blob_name,
      blob_etag = excluded.blob_etag,
      blob_size = excluded.blob_size,
      blob_last_modified = excluded.blob_last_modified,
      ingested_at = excluded.ingested_at
  `).run(
    metadata.dashboardRunId,
    metadata.sourceRunId,
    metadata.repository,
    metadata.pipeline,
    metadata.adoProject,
    metadata.pipelineDefinitionId,
    metadata.branch,
    metadata.sourceVersion,
    metadata.buildId,
    metadata.buildUrl,
    metadata.summaryAttempt,
    metadata.runTimestamp,
    sourcePath,
    blob.name ?? null,
    blob.etag ?? null,
    blob.size ?? null,
    blob.lastModified ?? null,
    new Date().toISOString()
  );
}

export function getPipelineSummaries(db) {
  return db.prepare(`
    SELECT
      metadata.repository,
      metadata.pipeline,
      COUNT(*) AS run_count,
      MAX(COALESCE(runs.started_at, metadata.run_timestamp, metadata.ingested_at)) AS last_run_at
    FROM run_metadata AS metadata
    JOIN runs ON runs.id = metadata.run_id
    GROUP BY metadata.repository, metadata.pipeline
    ORDER BY metadata.repository COLLATE NOCASE, metadata.pipeline COLLATE NOCASE
  `).all();
}

export function getPipelineRuns(db, repository, pipeline) {
  return db.prepare(`
    SELECT
      runs.id,
      runs.source,
      runs.eval_name,
      COALESCE(runs.started_at, metadata.run_timestamp, metadata.ingested_at) AS started_at,
      metadata.build_url,
      (SELECT COUNT(*) FROM outcomes WHERE outcomes.run_id = runs.id) AS outcome_count,
      (SELECT COUNT(DISTINCT stimulus_name) FROM outcomes WHERE outcomes.run_id = runs.id) AS stimulus_count,
      (
        SELECT CAST(SUM(CASE WHEN passed THEN 1 ELSE 0 END) AS REAL) / MAX(COUNT(*), 1)
        FROM outcomes
        WHERE outcomes.run_id = runs.id
      ) AS pass_rate
    FROM run_metadata AS metadata
    JOIN runs ON runs.id = metadata.run_id
    WHERE metadata.repository = ? AND metadata.pipeline = ?
    ORDER BY started_at DESC, runs.id DESC
  `).all(repository, pipeline).map((row) => ({
    id: row.id,
    source: row.source,
    evalName: row.eval_name ?? undefined,
    models: db.prepare(`
      SELECT DISTINCT model
      FROM outcomes
      WHERE run_id = ?
      ORDER BY model
    `).all(row.id).map((model) => model.model),
    stimulusCount: row.stimulus_count,
    outcomeCount: row.outcome_count,
    passRate: row.pass_rate ?? 0,
    startedAt: row.started_at ?? undefined,
    buildUrl: row.build_url ?? undefined,
  }));
}