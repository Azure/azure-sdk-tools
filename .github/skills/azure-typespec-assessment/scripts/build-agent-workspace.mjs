import fs from "node:fs";
import path from "node:path";
import { isMain, parseArgs, readJson, runMain } from "./cli.mjs";
import {
  atomicWriteJson,
  comparisonIdentity,
  hashArtifacts,
  resolveWorkPath,
  transitionWorkflowState,
} from "./workflow-state.mjs";

const INDEX_FILE = "agent-workspace/agent-index.json";
const INFERENCE_DRAFT = "agent-workspace/inference.draft.json";
const JUDGMENT_DRAFT = "agent-workspace/assessment-judgment.draft.json";

function unique(values) {
  return [...new Set(values.filter(Boolean))].sort();
}

function evidenceFor(modelInput, item) {
  const evidence = modelInput.evidenceSets?.[item.evidenceSetId];
  if (!evidence) {
    throw new Error(
      `Missing evidence set ${item.evidenceSetId ?? "<missing>"} for ${item.id ?? item.reviewUnitId ?? item.requestId}.`,
    );
  }
  return evidence;
}

function validateBoundedEvidence(modelInput) {
  const referencedFactIds = unique(
    [
      ...modelInput.semanticReviewUnits,
      ...modelInput.restCandidates,
      ...modelInput.downstreamCandidates,
      ...modelInput.complianceSearchRequests,
    ].flatMap((item) => evidenceFor(modelInput, item).evidenceFactIds ?? []),
  );
  for (const id of referencedFactIds) {
    if (!modelInput.facts?.[id]) {
      throw new Error(`Referenced canonical fact ${id} is missing.`);
    }
  }
}

function inferenceDraft(requests) {
  return {
    schemaVersion: 1,
    results: requests.map((request) => ({
      requestId: request.requestId,
      reviewUnitId: request.reviewUnitId,
      hunkId: request.hunkId,
      decision: "__UNRESOLVED__",
      rationale: "",
      candidates: [],
    })),
  };
}

function judgmentDraft(modelInput) {
  const decision = (candidate) => ({
    candidateId: candidate.id,
    decision: "__UNRESOLVED__",
    rationale: "",
  });
  return {
    schemaVersion: 1,
    semanticIntents: modelInput.semanticReviewUnits.map((unit) => ({
      reviewUnitId: unit.reviewUnitId,
      title: "",
      summary: "",
    })),
    restDecisions: modelInput.restCandidates.map(decision),
    downstreamDecisions: modelInput.downstreamCandidates.map(decision),
    complianceDecisions: modelInput.complianceSearchRequests.map((request) => {
      const evidence = evidenceFor(modelInput, request);
      return {
        reviewUnitId: request.reviewUnitId,
        applicableGuidance: [],
        sourceChangeIds: evidence.sourceChangeIds,
        hunkIds: evidence.hunkIds,
        declarationIds: [],
        decision: "__UNRESOLVED__",
        actual: "",
        rationale: "",
      };
    }),
    overallConfidence: "__UNRESOLVED__",
    blockers: [],
  };
}

function canonicalPaths(modelInput) {
  return unique([
    "model-input.json",
    "preparation-manifest.json",
    ...Object.values(modelInput.artifactReferences ?? {}),
    "dimensions/document-quality-input.json",
  ]);
}

export function buildAgentWorkspace({ work }) {
  const started = performance.now();
  const root = path.resolve(work);
  const modelInputPath = path.join(root, "model-input.json");
  if (!fs.existsSync(modelInputPath)) throw new Error("Missing model-input.json.");
  const modelInput = readJson(modelInputPath);
  validateBoundedEvidence(modelInput);

  fs.mkdirSync(path.join(root, "agent-workspace"), { recursive: true });
  if (modelInput.inferenceRequests.length) {
    atomicWriteJson(
      resolveWorkPath(root, INFERENCE_DRAFT),
      inferenceDraft(modelInput.inferenceRequests),
    );
  } else {
    fs.rmSync(resolveWorkPath(root, INFERENCE_DRAFT), { force: true });
  }
  atomicWriteJson(
    resolveWorkPath(root, JUDGMENT_DRAFT),
    judgmentDraft(modelInput),
  );

  const artifacts = canonicalPaths(modelInput).filter((relativePath) =>
    fs.existsSync(resolveWorkPath(root, relativePath)),
  );
  const artifactHashes = hashArtifacts(root, artifacts);
  const modelInputBytes = fs.statSync(modelInputPath).size;
  const index = {
    schemaVersion: 1,
    comparisonIdentity: comparisonIdentity(modelInput),
    input: {
      path: "model-input.json",
      bytes: modelInputBytes,
      readExactlyOnce: true,
    },
    counts: {
      assessedSemanticIntents: modelInput.semanticReviewUnits.length,
      informationalSemanticIntents:
        modelInput.informationalSemanticIntentIds?.length ?? 0,
      restCandidates: modelInput.restCandidates.length,
      downstreamCandidates: modelInput.downstreamCandidates.length,
      inferenceRequests: modelInput.inferenceRequests.length,
      guidelineRequests: modelInput.complianceSearchRequests.length,
    },
    requiredOutputs: {
      inference:
        modelInput.inferenceRequests.length > 0 ? "inference.json" : null,
      guidelineEvidence: "compliance-search-evidence.json",
      judgment: "assessment-judgment.json",
      structuredResult: "assessment.json",
      report: "assessment.html",
      schemas: {
        inference: "scripts/inference.schema.json",
        guidelineEvidence: "scripts/compliance-search-evidence.schema.json",
        judgment: "scripts/assessment-judgment.schema.json",
      },
    },
    drafts: {
      inference:
        modelInput.inferenceRequests.length > 0 ? INFERENCE_DRAFT : null,
      judgment: JUDGMENT_DRAFT,
    },
    coverage: {
      semanticIntentIds: modelInput.semanticReviewUnits.map(
        (unit) => unit.reviewUnitId,
      ),
      informationalSemanticIntentIds:
        modelInput.informationalSemanticIntentIds ?? [],
      restCandidateIds: modelInput.restCandidates.map((candidate) => candidate.id),
      downstreamCandidateIds: modelInput.downstreamCandidates.map(
        (candidate) => candidate.id,
      ),
      inferenceRequestIds: modelInput.inferenceRequests.map(
        (request) => request.requestId,
      ),
      guidelineRequestIds: modelInput.complianceSearchRequests.map(
        (request) => request.requestId,
      ),
    },
    completionChecklist: [
      "Read model-input.json exactly once.",
      ...(modelInput.inferenceRequests.length
        ? ["Resolve every inference request and write inference.json."]
        : []),
      "Analyze the shared Azure Guidelines documents once and write compliance-search-evidence.json.",
      "Complete every prefilled judgment entry and write assessment-judgment.json.",
      "Run finalize-assessment.mjs and require validated assessment.json and assessment.html.",
    ],
    canonicalArtifactHashes: artifactHashes,
  };
  atomicWriteJson(resolveWorkPath(root, INDEX_FILE), index);
  const indexBytes = fs.statSync(resolveWorkPath(root, INDEX_FILE)).size;
  const workspaceMs = Math.round(performance.now() - started);
  transitionWorkflowState(root, "awaiting-agent-judgment", {
    comparisonIdentity: index.comparisonIdentity,
    artifacts: {
      modelInput: "model-input.json",
      agentIndex: INDEX_FILE,
      inferenceDraft:
        modelInput.inferenceRequests.length > 0 ? INFERENCE_DRAFT : null,
      judgmentDraft: JUDGMENT_DRAFT,
    },
    artifactHashes,
    failure: undefined,
    telemetry: {
      deterministicReadyAt: new Date().toISOString(),
      agentWorkspaceMs: workspaceMs,
      agentIndexBytes: indexBytes,
      modelInputBytes,
    },
  });
  return { index, indexPath: resolveWorkPath(root, INDEX_FILE) };
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const args = parseArgs(process.argv.slice(2), { required: ["work"] });
    const result = buildAgentWorkspace({ work: args.work });
    console.log(result.indexPath);
  });
}
