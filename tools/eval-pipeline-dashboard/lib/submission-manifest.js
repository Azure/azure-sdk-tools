import { createHash } from "node:crypto";
import { IngestionError } from "./ingestion-error.js";

function text(value, field, { optional = false } = {}) {
  if (optional && value === undefined) return null;
  if (typeof value !== "string" || !value.trim() || value.length > 200 || /[\u0000-\u001f\u007f]/.test(value)) {
    throw new IngestionError(422, "invalid_manifest", `${field} must be a non-empty string of at most 200 characters.`);
  }
  return value.trim();
}

function identifier(value, field) {
  const id = typeof value === "number" && Number.isSafeInteger(value) ? String(value) : value;
  if (typeof id !== "string" || !/^[1-9][0-9]{0,19}$/.test(id)) {
    throw new IngestionError(422, "invalid_manifest", `${field} must be a positive numeric ID.`);
  }
  return id;
}

export function validateSubmissionManifest(source) {
  if (!source || typeof source !== "object" || Array.isArray(source) || source.schemaVersion !== 1) {
    throw new IngestionError(422, "invalid_manifest", "A manifest with schemaVersion 1 is required.");
  }
  const adoOrganization = text(source.adoOrganization, "adoOrganization").toLowerCase();
  const adoProject = text(source.adoProject, "adoProject");
  const repo = text(source.repo, "repo");
  const pipeline = text(source.pipeline, "pipeline");
  if (!/^[a-z0-9][a-z0-9-]{0,99}$/.test(adoOrganization) || /[\\/:]/.test(adoProject) || /^\.+$/.test(adoProject)) {
    throw new IngestionError(422, "invalid_manifest", "Invalid Azure DevOps organization or project.");
  }
  const pipelineDefinitionId = identifier(source.pipelineDefinitionId, "pipelineDefinitionId");
  const buildId = identifier(source.buildId, "buildId");
  if (!Number.isSafeInteger(source.summaryAttempt) || source.summaryAttempt < 1) {
    throw new IngestionError(422, "invalid_manifest", "summaryAttempt must be a positive integer.");
  }
  const timestamp = text(source.runTimestamp, "runTimestamp");
  if (!/^\d{4}-\d{2}-\d{2}T.*Z$/.test(timestamp) || Number.isNaN(Date.parse(timestamp))) {
    throw new IngestionError(422, "invalid_manifest", "runTimestamp must be an ISO 8601 UTC timestamp.");
  }
  const sourceVersion = text(source.sourceVersion, "sourceVersion", { optional: true });
  if (sourceVersion && !/^[a-fA-F0-9]{40,64}$/.test(sourceVersion)) {
    throw new IngestionError(422, "invalid_manifest", "sourceVersion must be a commit SHA.");
  }
  return {
    schemaVersion: 1,
    adoOrganization,
    adoProject,
    repo,
    pipeline,
    pipelineDefinitionId,
    buildId,
    summaryAttempt: source.summaryAttempt,
    branch: text(source.branch, "branch", { optional: true }),
    sourceVersion,
    runTimestamp: new Date(timestamp).toISOString(),
    buildUrl: `https://dev.azure.com/${adoOrganization}/${encodeURIComponent(adoProject)}/_build/results?buildId=${buildId}`,
  };
}

export function submissionId(manifest) {
  return createHash("sha256").update(JSON.stringify([
    manifest.adoOrganization,
    manifest.adoProject.toLowerCase(),
    manifest.pipelineDefinitionId,
    manifest.buildId,
    manifest.summaryAttempt,
  ])).digest("hex");
}

export function submissionBlobName(manifest) {
  return [
    "v1",
    manifest.adoOrganization,
    manifest.adoProject.toLowerCase(),
    manifest.pipelineDefinitionId,
    manifest.buildId,
    String(manifest.summaryAttempt),
  ].map(encodeURIComponent).join("/") + "/dashboard-bundle.zip";
}

export function submissionRunMetadata(manifest, id) {
  return {
    schemaVersion: 1,
    adoOrganization: manifest.adoOrganization,
    adoProject: manifest.adoProject,
    repository: manifest.repo,
    pipeline: manifest.pipeline,
    pipelineDefinitionId: manifest.pipelineDefinitionId,
    sourceRunId: manifest.buildId,
    branch: manifest.branch,
    sourceVersion: manifest.sourceVersion,
    buildId: manifest.buildId,
    buildUrl: manifest.buildUrl,
    summaryAttempt: manifest.summaryAttempt,
    runTimestamp: manifest.runTimestamp,
    dashboardRunId: `ingestion-${id}`,
  };
}