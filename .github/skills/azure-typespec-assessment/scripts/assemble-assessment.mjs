#!/usr/bin/env node

import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

import { analyzeArtifacts } from "./analyze-artifacts.mjs";
import { renderAssessmentHtml } from "./render-assessment-html.mjs";
import { parseTypeSpecDiffHunks } from "./typespec-diff-hunks.mjs";
import { validateAssessment } from "./validate-assessment.mjs";

const CONFIDENCE = new Set(["high", "medium", "low"]);
const DECISIONS = new Set(["approve", "reject"]);
const COMPLIANCE_STATUSES = new Set(["passed", "failed", "not-assessed"]);
const INTERNAL_TERMS =
  /\bTCGC\b|cross-language definition IDs?|\bisUnionAsEnum\b|\bisFixed\b/gi;
const SCRIPT_DIRECTORY = dirname(fileURLToPath(import.meta.url));
const CANONICAL_REPORT_ROOT = resolve(
  SCRIPT_DIRECTORY,
  "..",
  "test-evidence",
  "assessments",
);

function elapsedMs(startedAt) {
  return Math.round(Number(process.hrtime.bigint() - startedAt) / 1_000_000);
}

function readJson(path, label) {
  try {
    return JSON.parse(readFileSync(path, "utf8"));
  } catch (error) {
    throw new Error(`Unable to read ${label} at ${path}: ${error.message}`, {
      cause: error,
    });
  }
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function assertObject(value, label) {
  assert(
    value !== null && typeof value === "object" && !Array.isArray(value),
    `${label} must be an object.`,
  );
}

function assertExactKeys(value, required, optional, label) {
  assertObject(value, label);
  const allowed = new Set([...required, ...optional]);
  const unknown = Object.keys(value).filter((key) => !allowed.has(key));
  assert(
    unknown.length === 0,
    `${label} contains unknown field(s): ${unknown.join(", ")}.`,
  );
  const missing = required.filter((key) => !(key in value));
  assert(
    missing.length === 0,
    `${label} is missing required field(s): ${missing.join(", ")}.`,
  );
}

function assertNonEmptyString(value, label) {
  assert(
    typeof value === "string" && value.trim().length > 0,
    `${label} must be a non-empty string.`,
  );
}

function assertUniqueStrings(value, label) {
  assert(Array.isArray(value), `${label} must be an array.`);
  for (const [index, item] of value.entries()) {
    assertNonEmptyString(item, `${label}[${index}]`);
  }
  assert(
    new Set(value).size === value.length,
    `${label} must not contain duplicate values.`,
  );
}

function normalizePositiveInteger(value, label) {
  const isNumericString =
    typeof value === "string" && /^0*[1-9][0-9]*$/.test(value);
  const normalized = isNumericString ? Number(value) : value;
  assert(
    Number.isSafeInteger(normalized) && normalized > 0,
    `${label} must be a positive integer or a digit-only positive numeric string.`,
  );
  return normalized;
}

function mapByUniqueId(values, label) {
  const result = new Map();
  for (const value of values) {
    assertNonEmptyString(value?.id, `${label} id`);
    if (result.has(value.id)) {
      assert(
        JSON.stringify(result.get(value.id)) === JSON.stringify(value),
        `Conflicting duplicate ${label} evidence ID: ${value.id}.`,
      );
      continue;
    }
    result.set(value.id, value);
  }
  return result;
}

function flattenedEvidence(modelInput) {
  const projects = modelInput.projects ?? [];
  const sources = (modelInput.sourceFiles ?? []).flatMap((file) =>
    (file.changes ?? []).map((change) => ({ ...change, path: file.path })),
  );
  return {
    sourceChanges: mapByUniqueId(sources, "source change"),
    sourcePaths: new Set([
      ...(modelInput.changedFiles ?? []),
      ...(modelInput.sourceFiles ?? []).map((file) => file.path),
    ]),
    operationChanges: mapByUniqueId(
      projects.flatMap((project) =>
        (project.rest?.operationChanges ?? []).map((change) => ({
          ...change,
          project: project.path,
        })),
      ),
      "operation change",
    ),
    operationGroups: mapByUniqueId(
      projects.flatMap((project) =>
        (project.rest?.operationGroups ?? []).map((group) => ({
          ...group,
          project: project.path,
        })),
      ),
      "operation group",
    ),
    restCandidates: mapByUniqueId(
      projects.flatMap((project) =>
        (project.rest?.breakingCandidates ?? []).map((candidate) => ({
          ...candidate,
          project: project.path,
        })),
      ),
      "REST candidate",
    ),
    downstreamCandidates: mapByUniqueId(
      projects.flatMap((project) =>
        (project.downstream?.candidates ?? []).map((candidate) => ({
          ...candidate,
          project: project.path,
        })),
      ),
      "downstream candidate",
    ),
    documents: new Map(
      (modelInput.complianceEvidence?.documents ?? []).map((document) => [
        document.url,
        document,
      ]),
    ),
  };
}

function validateReferences(ids, known, label) {
  for (const id of ids) {
    assert(known.has(id), `${label} references unknown evidence ID: ${id}.`);
  }
}

function validateDecisionArray(decisions, candidates, label) {
  assert(Array.isArray(decisions), `${label} must be an array.`);
  const seen = new Set();
  for (const [index, decision] of decisions.entries()) {
    const itemLabel = `${label}[${index}]`;
    assertExactKeys(
      decision,
      ["id", "decision", "rationale"],
      ["severity"],
      itemLabel,
    );
    assertNonEmptyString(decision.id, `${itemLabel}.id`);
    assert(
      !seen.has(decision.id),
      `${label} contains duplicate evidence ID: ${decision.id}.`,
    );
    seen.add(decision.id);
    assert(
      candidates.has(decision.id),
      `${itemLabel} references unknown evidence ID: ${decision.id}.`,
    );
    assert(
      DECISIONS.has(decision.decision),
      `${itemLabel}.decision must be exactly approve or reject.`,
    );
    assertNonEmptyString(decision.rationale, `${itemLabel}.rationale`);
    if (decision.severity !== undefined) {
      assert(
        decision.decision === "approve",
        `${itemLabel}.severity is only allowed for approve decisions.`,
      );
      assert(
        CONFIDENCE.has(decision.severity),
        `${itemLabel}.severity must be high, medium, or low.`,
      );
    }
  }
  const unreferenced = [...candidates.keys()].filter((id) => !seen.has(id));
  assert(
    unreferenced.length === 0,
    `${label} leaves evidence ID(s) unreferenced: ${unreferenced.join(", ")}.`,
  );
}

export function validateJudgment(judgment, modelInput) {
  assert(
    modelInput?.schemaVersion === 1,
    "model-input.json schemaVersion must be 1.",
  );
  const evidence = flattenedEvidence(modelInput);
  assertExactKeys(
    judgment,
    [
      "schemaVersion",
      "semanticIntents",
      "restCandidates",
      "downstreamCandidates",
      "compliance",
      "overallConfidence",
      "blockers",
    ],
    ["pr"],
    "assessment judgment",
  );
  assert(
    judgment.schemaVersion === 1,
    "assessment judgment schemaVersion must be exactly 1.",
  );
  if (judgment.pr !== undefined) {
    normalizePositiveInteger(judgment.pr, "assessment judgment pr");
  }
  assert(
    Array.isArray(judgment.semanticIntents) &&
      judgment.semanticIntents.length > 0,
    "semanticIntents must be a non-empty array.",
  );

  const semanticIds = new Set();
  const operationChangeUse = new Map();
  const operationGroupUse = new Map();
  for (const [index, intent] of judgment.semanticIntents.entries()) {
    const label = `semanticIntents[${index}]`;
    assertExactKeys(
      intent,
      [
        "id",
        "title",
        "rationale",
        "operationChangeIds",
        "operationGroupIds",
        "sourceChangeIds",
        "sourcePaths",
      ],
      ["aspects"],
      label,
    );
    for (const field of ["id", "title", "rationale"]) {
      assertNonEmptyString(intent[field], `${label}.${field}`);
    }
    assert(
      !semanticIds.has(intent.id),
      `Duplicate semantic intent ID: ${intent.id}.`,
    );
    semanticIds.add(intent.id);
    for (const field of [
      "operationChangeIds",
      "operationGroupIds",
      "sourceChangeIds",
      "sourcePaths",
    ]) {
      assertUniqueStrings(intent[field], `${label}.${field}`);
    }
    if (intent.aspects !== undefined) {
      assert(
        Array.isArray(intent.aspects) && intent.aspects.length > 0,
        `${label}.aspects must be a non-empty array.`,
      );
      for (const [aspectIndex, aspect] of intent.aspects.entries()) {
        const aspectLabel = `${label}.aspects[${aspectIndex}]`;
        assertExactKeys(aspect, ["field", "before", "after"], [], aspectLabel);
        for (const field of ["field", "before", "after"]) {
          assertNonEmptyString(aspect[field], `${aspectLabel}.${field}`);
        }
      }
    }
    assert(
      intent.operationChangeIds.length +
        intent.operationGroupIds.length +
        intent.sourceChangeIds.length +
        intent.sourcePaths.length >
        0,
      `${label} must reference bounded evidence.`,
    );
    validateReferences(
      intent.operationChangeIds,
      evidence.operationChanges,
      `${label}.operationChangeIds`,
    );
    validateReferences(
      intent.operationGroupIds,
      evidence.operationGroups,
      `${label}.operationGroupIds`,
    );
    validateReferences(
      intent.sourceChangeIds,
      evidence.sourceChanges,
      `${label}.sourceChangeIds`,
    );
    for (const sourcePath of intent.sourcePaths) {
      assert(
        evidence.sourcePaths.has(sourcePath),
        `${label}.sourcePaths references unknown source path: ${sourcePath}.`,
      );
    }
    for (const id of intent.operationChangeIds) {
      operationChangeUse.set(id, (operationChangeUse.get(id) ?? 0) + 1);
    }
    for (const id of intent.operationGroupIds) {
      operationGroupUse.set(id, (operationGroupUse.get(id) ?? 0) + 1);
    }
  }

  const compliance = judgment.compliance;
  assertExactKeys(
    compliance,
    ["status", "rationale", "documentUrls", "findings"],
    [],
    "compliance",
  );
  assert(
    COMPLIANCE_STATUSES.has(compliance.status),
    "compliance.status must be passed, failed, or not-assessed.",
  );
  assertNonEmptyString(compliance.rationale, "compliance.rationale");
  assertUniqueStrings(compliance.documentUrls, "compliance.documentUrls");
  for (const url of compliance.documentUrls) {
    assert(
      evidence.documents.has(url),
      `compliance.documentUrls references unknown document URL: ${url}.`,
    );
  }
  assert(
    Array.isArray(compliance.findings),
    "compliance.findings must be an array.",
  );
  const complianceIds = new Set();
  for (const [index, finding] of compliance.findings.entries()) {
    const label = `compliance.findings[${index}]`;
    assertExactKeys(
      finding,
      [
        "id",
        "title",
        "severity",
        "summary",
        "documentationUrl",
        "evidence",
        "sourceChangeIds",
        "sourcePaths",
      ],
      [],
      label,
    );
    for (const field of ["id", "title", "summary", "documentationUrl"]) {
      assertNonEmptyString(finding[field], `${label}.${field}`);
    }
    assert(
      !complianceIds.has(finding.id),
      `Duplicate compliance finding ID: ${finding.id}.`,
    );
    assert(
      !semanticIds.has(finding.id),
      `Duplicate report item ID: ${finding.id}.`,
    );
    complianceIds.add(finding.id);
    assert(
      CONFIDENCE.has(finding.severity),
      `${label}.severity must be high, medium, or low.`,
    );
    assert(
      compliance.documentUrls.includes(finding.documentationUrl),
      `${label}.documentationUrl must be listed in compliance.documentUrls.`,
    );
    if (Array.isArray(finding.evidence)) {
      assertUniqueStrings(finding.evidence, `${label}.evidence`);
      assert(
        finding.evidence.length > 0,
        `${label}.evidence must not be empty.`,
      );
    } else {
      assertNonEmptyString(finding.evidence, `${label}.evidence`);
    }
    assertUniqueStrings(finding.sourceChangeIds, `${label}.sourceChangeIds`);
    assertUniqueStrings(finding.sourcePaths, `${label}.sourcePaths`);
    validateReferences(
      finding.sourceChangeIds,
      evidence.sourceChanges,
      `${label}.sourceChangeIds`,
    );
    for (const sourcePath of finding.sourcePaths) {
      assert(
        evidence.sourcePaths.has(sourcePath),
        `${label}.sourcePaths references unknown source path: ${sourcePath}.`,
      );
    }
  }
  assert(
    compliance.status !== "failed" || compliance.findings.length > 0,
    "failed compliance requires at least one finding.",
  );
  assert(
    compliance.status === "failed" || compliance.findings.length === 0,
    `${compliance.status} compliance cannot contain findings.`,
  );
  assert(
    compliance.status === "not-assessed" || compliance.documentUrls.length > 0,
    `${compliance.status} compliance requires at least one document URL.`,
  );

  validateDecisionArray(
    judgment.restCandidates,
    evidence.restCandidates,
    "restCandidates",
  );
  validateDecisionArray(
    judgment.downstreamCandidates,
    evidence.downstreamCandidates,
    "downstreamCandidates",
  );
  assert(
    CONFIDENCE.has(judgment.overallConfidence),
    "overallConfidence must be high, medium, or low.",
  );
  assertUniqueStrings(judgment.blockers, "blockers");

  for (const [label, use] of [
    ["operation change", operationChangeUse],
    ["operation group", operationGroupUse],
  ]) {
    const duplicates = [...use]
      .filter(([, count]) => count !== 1)
      .map(([id]) => id);
    assert(
      duplicates.length === 0,
      `${label} evidence ID(s) must be referenced exactly once: ${duplicates.join(", ")}.`,
    );
  }
  return evidence;
}

function uniqueByJson(values) {
  const seen = new Set();
  return values.filter((value) => {
    const key = JSON.stringify(value);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function materializationData(
  materialization,
  artifactAnalysis,
  retainedEvidence,
) {
  const items = materialization.dimensions?.semanticUnderstanding?.items ?? [];
  return {
    items,
    operations: items.flatMap(
      (item) => item.restRepresentation?.operations ?? [],
    ),
    artifactProjects: artifactAnalysis?.projects ?? [],
    changes: items.flatMap((item) => item.changes ?? []),
    sourceReferences: uniqueByJson([
      ...(retainedEvidence?.sourceReferences ?? []),
      ...(materialization.assessmentEvidence?.changedTypeSpec ?? []),
      ...items.flatMap((item) => item.sourceReferences ?? []),
      ...items.flatMap((item) =>
        (item.changes ?? []).flatMap((change) => change.sourceReferences ?? []),
      ),
      ...items.flatMap((item) =>
        (item.restRepresentation?.operations ?? []).flatMap(
          (operation) => operation.sourceReferences ?? [],
        ),
      ),
    ]),
    typeSpecDiffs: uniqueByJson([
      ...(retainedEvidence?.typeSpecDiffs ?? []),
      ...items.flatMap((item) =>
        (item.changes ?? []).flatMap((change) => change.typeSpecDiffs ?? []),
      ),
    ]),
  };
}

function intentEvidence(intent, evidence) {
  return [
    ...(intent.operationChangeIds ?? []).map((id) =>
      evidence.operationChanges.get(id),
    ),
    ...(intent.operationGroupIds ?? []).map((id) =>
      evidence.operationGroups.get(id),
    ),
  ];
}

function operationDescriptors(intent, evidence) {
  return intentEvidence(intent, evidence).flatMap((item) => {
    if (item.operationId) {
      return [
        {
          kind: item.kind,
          project: item.project,
          operationId: item.operationId,
          apiVersion: item.apiVersion,
          operationKey: item.operationKey,
        },
      ];
    }
    return (item.operationIds ?? []).map((operationId) => ({
      kind: item.kind,
      project: item.project,
      operationId,
      apiVersion: item.apiVersion,
    }));
  });
}

function operationMatches(operation, descriptor) {
  if (operation.operationId !== descriptor.operationId) return false;
  if (
    descriptor.apiVersion &&
    operation.apiVersions?.includes(descriptor.apiVersion)
  ) {
    return true;
  }
  if (descriptor.operationKey) {
    const [, method, ...pathParts] = descriptor.operationKey.split(":");
    return (
      operation.method === method && operation.path === pathParts.join(":")
    );
  }
  return !descriptor.apiVersion;
}

function normalizedOperationMatches(operation, descriptor) {
  if (operation.operationId !== descriptor.operationId) return false;
  if (descriptor.operationKey && operation.key === descriptor.operationKey) {
    return true;
  }
  return (
    descriptor.apiVersion === undefined ||
    operation.apiVersion === descriptor.apiVersion
  );
}

function schemaList(content) {
  return (content ?? [])
    .map(({ mediaType, schema }) => `${mediaType} payload: ${schema}`)
    .join(", ");
}

function parameterDescription(parameter) {
  const details = [
    `${parameter.in} ${parameter.name}: ${parameter.type}`,
    parameter.required ? "required" : "optional",
  ];
  if (parameter.default !== undefined) {
    details.push(`default ${JSON.stringify(parameter.default)}`);
  }
  return details.join(", ");
}

function requestDescription(request) {
  const content = schemaList(request?.content);
  if (!content) return "none";
  return `${content}, ${request.required ? "required" : "optional"}`;
}

function responseDescription(response) {
  const content = schemaList(response.content) || "no payload";
  const headers = (response.headers ?? [])
    .map(
      (header) =>
        `${header.name}: ${header.type}${header.required ? ", required" : ""}`,
    )
    .join(", ");
  return `${response.status} ${content}${headers ? `; headers: ${headers}` : ""}`;
}

function pagingItemType(operation) {
  const itemName = operation.paging?.itemName;
  for (const response of operation.responses ?? []) {
    for (const content of response.content ?? []) {
      const item = content.contract?.value?.properties?.[itemName]?.items;
      const reference = item?.reference;
      if (reference) return reference.split("/").at(-1);
      if (item?.type) return item.type;
    }
  }
  const responseSchema = (operation.responses ?? [])
    .flatMap((response) => response.content ?? [])
    .map((content) => content.schema)
    .find((schema) => schema && schema !== "ErrorResponse");
  return responseSchema
    ? `${responseSchema}.${itemName ?? "items"}`
    : (itemName ?? "items");
}

function serviceBehavior(operation, richerOperation) {
  if (richerOperation?.serviceBehavior) return richerOperation.serviceBehavior;
  const behavior =
    operation.method === "GET"
      ? "Returns the requested service data."
      : operation.method === "DELETE"
        ? "Deletes the addressed resource."
        : operation.method === "PATCH"
          ? "Updates the addressed resource."
          : operation.method === "PUT"
            ? "Creates or replaces the addressed resource."
            : "Invokes the addressed service operation.";
  if (operation.lro.isLongRunning) {
    return `${behavior} Completion follows the declared long-running operation contract.`;
  }
  if (operation.paging.isPaged) {
    return `${behavior} Additional pages follow the declared continuation link.`;
  }
  return behavior;
}

function lroDescription(operation, richerOperation) {
  if (!operation.lro.isLongRunning) return { isLongRunning: false };
  const richer = richerOperation?.lro?.isLongRunning
    ? richerOperation.lro
    : undefined;
  const finalStateVia = operation.lro.finalStateVia ?? "unknown";
  return {
    isLongRunning: true,
    pattern: richer?.pattern ?? "OpenAPI long-running operation",
    finalStateVia,
    polling:
      richer?.polling ??
      `Poll according to the ${finalStateVia} long-running operation contract until it reaches a terminal state.`,
    finalResult:
      richer?.finalResult ??
      operation.lro.finalResult ??
      "Return the terminal operation response.",
  };
}

function pagingDescription(operation, richerOperation) {
  if (!operation.paging.isPaged) return { isPaged: false };
  const richer = richerOperation?.paging?.isPaged
    ? richerOperation.paging
    : undefined;
  return {
    isPaged: true,
    itemType: richer?.itemType ?? pagingItemType(operation),
    itemsProperty:
      richer?.itemsProperty ?? operation.paging.itemName ?? "value",
    nextLinkName:
      richer?.nextLinkName ?? operation.paging.nextLinkName ?? "nextLink",
    continuation:
      richer?.continuation ??
      operation.paging.continuation ??
      "Issue a GET request to the opaque continuation URL until it is absent.",
  };
}

function richerOperationFor(operation, materialized) {
  const operationIdentity = (operationId) =>
    operationId?.replace(/Segment$/, "");
  return (
    materialized.operations.find(
      (candidate) =>
        candidate.operationId === operation.operationId &&
        candidate.method === operation.method &&
        candidate.path === operation.path &&
        candidate.apiVersions?.includes(operation.apiVersion),
    ) ??
    materialized.operations.find(
      (candidate) =>
        candidate.operationId === operation.operationId &&
        candidate.method === operation.method &&
        candidate.path === operation.path,
    ) ??
    materialized.operations.find(
      (candidate) =>
        candidate.operationId === operation.operationId &&
        candidate.apiVersions?.includes(operation.apiVersion),
    ) ??
    materialized.operations.find(
      (candidate) =>
        operationIdentity(candidate.operationId) ===
        operationIdentity(operation.operationId),
    )
  );
}

function artifactSourceReferences(project, selection, richerOperation) {
  const selected = (project.sourceReferences ?? []).filter((reference) =>
    selection.paths.has(reference.path),
  );
  if (selected.length > 0) return selected;
  const richer = richerOperation?.sourceReferences ?? [];
  if (richer.length > 0) return richer;
  return project.sourceReferences ?? [];
}

function reportOperation(
  operation,
  revision,
  project,
  selection,
  materialized,
) {
  const richer = richerOperationFor(operation, materialized);
  const path =
    richer?.path &&
    (!operation.path?.startsWith("/") || operation.path.startsWith("/?"))
      ? richer.path
      : operation.path;
  return {
    operationId: operation.operationId,
    apiVersions: [operation.apiVersion],
    method: operation.method,
    path,
    signature: `${operation.method} ${path}`,
    parameters: operation.parameters.map(parameterDescription),
    requestPayload: requestDescription(operation.request),
    responsePayloads: operation.responses.map(responseDescription),
    serviceBehavior: serviceBehavior(operation, richer),
    lro: lroDescription(operation, richer),
    paging: pagingDescription(operation, richer),
    sourceReferences: artifactSourceReferences(project, selection, richer).map(
      (reference) => structuredClone(reference),
    ),
    artifactEvidence: {
      revision,
      sourceArtifact: operation.sourceArtifact,
    },
  };
}

function artifactOperations(descriptor, materialized, selection) {
  const projects = descriptor.project
    ? materialized.artifactProjects.filter(
        (project) => project.path === descriptor.project,
      )
    : materialized.artifactProjects;
  const kind = reportKind(descriptor.kind);
  const revisions =
    kind === "added"
      ? ["head"]
      : kind === "removed"
        ? ["baseline"]
        : ["baseline", "head"];
  return projects.flatMap((project) =>
    revisions.flatMap((revision) =>
      (project.rest?.[revision] ?? [])
        .filter((operation) =>
          normalizedOperationMatches(operation, descriptor),
        )
        .map((operation) =>
          reportOperation(
            operation,
            revision,
            project,
            selection,
            materialized,
          ),
        ),
    ),
  );
}

function selectOperations(intent, evidence, materialized, selection) {
  const descriptors = operationDescriptors(intent, evidence);
  let selected = descriptors.flatMap((descriptor) => {
    const analyzed = artifactOperations(descriptor, materialized, selection);
    if (analyzed.length > 0) return analyzed;
    const exact = materialized.operations.filter((operation) =>
      operationMatches(operation, descriptor),
    );
    if (exact.length > 0) return exact;
    return materialized.operations.filter(
      (operation) => operation.operationId === descriptor.operationId,
    );
  });
  if (descriptors.length === 0) {
    selected = materialized.items
      .filter((item) =>
        (item.sourceReferences ?? []).some((reference) =>
          selection.paths.has(reference.path),
        ),
      )
      .flatMap((item) => item.restRepresentation?.operations ?? []);
  }
  const missing = [
    ...new Set(descriptors.map((item) => item.operationId)),
  ].filter(
    (operationId) =>
      !selected.some((operation) => operation.operationId === operationId),
  );
  assert(
    missing.length === 0,
    `Deterministic materialization is missing complete operation contract(s): ${missing.join(", ")}.`,
  );
  return uniqueByJson(selected.map((operation) => structuredClone(operation)));
}

function sourceSelections(item, evidence) {
  const changes = item.sourceChangeIds.map((id) =>
    evidence.sourceChanges.get(id),
  );
  return {
    changes,
    paths: new Set([
      ...item.sourcePaths,
      ...changes.map((change) => change.path),
    ]),
    projectPaths: new Set(
      intentEvidence(item, evidence)
        .map((entry) => entry.project)
        .filter(Boolean),
    ),
  };
}

function rangeContains(start, count, line) {
  return count > 0 && line >= start && line < start + count;
}

function selectDiffs(selection, materialized) {
  const direct = selection.changes.flatMap((change) => {
    const matches = materialized.typeSpecDiffs
      .filter(
        (hunk) =>
          hunk.path === change.path &&
          (rangeContains(hunk.oldStart, hunk.oldCount, change.oldStart) ||
            rangeContains(hunk.newStart, hunk.newCount, change.newStart)),
      )
      .sort(
        (left, right) =>
          left.oldCount + left.newCount - (right.oldCount + right.newCount),
      );
    return matches.slice(0, 1);
  });
  if (direct.length > 0) return uniqueByJson(direct);
  const samePath = materialized.typeSpecDiffs.filter((hunk) =>
    selection.paths.has(hunk.path),
  );
  if (samePath.length > 0) return samePath;
  const projectParents = [...selection.projectPaths].map((projectPath) =>
    dirname(projectPath).replaceAll("\\", "/"),
  );
  return materialized.typeSpecDiffs.filter((hunk) =>
    projectParents.some((parent) => hunk.path.startsWith(`${parent}/`)),
  );
}

function sourceReferenceForHunk(hunk, materialized) {
  const revision = hunk.newCount > 0 ? "head" : "base";
  const startLine = revision === "base" ? hunk.oldStart : hunk.newStart;
  const count = revision === "base" ? hunk.oldCount : hunk.newCount;
  const endLine = startLine + Math.max(count, 1) - 1;
  const matchingReferences = materialized.sourceReferences.filter(
    (reference) =>
      reference.path === hunk.path && reference.revision === revision,
  );
  const template =
    matchingReferences.find((reference) =>
      reference.link?.startsWith("https://"),
    ) ?? matchingReferences[0];
  const linkBase = template?.link
    ? template.link.replace(/#L\d+(?:-L\d+)?$/, "")
    : hunk.path;
  return {
    path: hunk.path,
    revision,
    startLine,
    endLine,
    link: `${linkBase}#L${startLine}-L${endLine}`,
  };
}

function selectReferences(
  selection,
  operations,
  materialized,
  typeSpecDiffs = [],
) {
  const pathReferences = materialized.sourceReferences.filter((reference) =>
    selection.paths.has(reference.path),
  );
  const exactReferences = typeSpecDiffs
    .map((hunk) => sourceReferenceForHunk(hunk, materialized))
    .filter(Boolean);
  const sourceReferences =
    exactReferences.length > 0 ? exactReferences : pathReferences;
  const operationReferences =
    sourceReferences.length === 0
      ? operations.flatMap((operation) => operation.sourceReferences ?? [])
      : [];
  const projectParents = [...selection.projectPaths].map((projectPath) =>
    dirname(projectPath).replaceAll("\\", "/"),
  );
  const transitiveReferences =
    sourceReferences.length + operationReferences.length === 0
      ? materialized.sourceReferences.filter((reference) =>
          projectParents.some((parent) =>
            reference.path.startsWith(`${parent}/`),
          ),
        )
      : [];
  const references = new Map();
  for (const reference of [
    ...sourceReferences,
    ...operationReferences,
    ...transitiveReferences,
  ]) {
    const key = `${reference.path}:${reference.revision}:${reference.startLine}:${reference.endLine}`;
    const existing = references.get(key);
    if (!existing || reference.link?.startsWith("https://")) {
      references.set(key, structuredClone(reference));
    }
  }
  return [...references.values()];
}

function humanField(value) {
  return value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[-_]+/g, " ")
    .replace(/^./, (character) => character.toUpperCase());
}

function displayValue(value) {
  if (value === null || value === undefined) return null;
  if (typeof value === "string") return value;
  if (typeof value !== "object") return String(value);
  if (Array.isArray(value)) return value.map(displayValue).join("; ");
  return Object.entries(value)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, child]) => `${humanField(key)}: ${displayValue(child)}`)
    .join("; ");
}

function reportKind(kind) {
  if (kind === "added" || kind === "removed") return kind;
  return "modified";
}

function valuesDiffer(change) {
  return displayValue(change.before) !== displayValue(change.after);
}

function canonicalJson(value) {
  if (Array.isArray(value)) return value.map(canonicalJson);
  if (!value || typeof value !== "object") return value;
  return Object.fromEntries(
    Object.entries(value)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, child]) => [key, canonicalJson(child)]),
  );
}

function compactAspect(field, change) {
  if (!valuesDiffer(change)) return [];
  if (Array.isArray(change.before) && Array.isArray(change.after)) {
    const beforeValues = new Map(
      change.before.map((value) => [
        JSON.stringify(canonicalJson(value)),
        value,
      ]),
    );
    const afterValues = new Map(
      change.after.map((value) => [
        JSON.stringify(canonicalJson(value)),
        value,
      ]),
    );
    const removed = [...beforeValues]
      .filter(([key]) => !afterValues.has(key))
      .map(([, value]) => value);
    const added = [...afterValues]
      .filter(([key]) => !beforeValues.has(key))
      .map(([, value]) => value);
    if (removed.length === 0 && added.length === 0) return [];
    return [
      {
        field: humanField(field),
        before: removed.length > 0 ? displayValue(removed) : null,
        after: added.length > 0 ? displayValue(added) : null,
      },
    ];
  }
  return [
    {
      field: humanField(field),
      before: displayValue(change.before),
      after: displayValue(change.after),
    },
  ];
}

export function operationHasMaterialAspectChange(operation) {
  return Object.entries(operation.aspects ?? {}).some(
    ([field, change]) => compactAspect(field, change).length > 0,
  );
}

function evidenceAspects(items, kind, operationCount) {
  const values = new Map();
  for (const item of items) {
    for (const [field, change] of Object.entries(item.aspectChanges ?? {})) {
      values.set(field, change);
    }
    for (const operation of item.changes ?? []) {
      for (const [field, change] of Object.entries(operation.aspects ?? {})) {
        values.set(field, change);
      }
    }
  }
  if (values.size > 0) {
    const aspects = [...values].flatMap(([field, change]) =>
      compactAspect(field, change),
    );
    if (aspects.length > 0) return aspects;
  }
  if (kind === "added") {
    return [
      {
        field: "Operation family",
        before: null,
        after: `${operationCount} REST operation${operationCount === 1 ? "" : "s"} added.`,
      },
    ];
  }
  if (kind === "removed") {
    return [
      {
        field: "Operation or contract surface",
        before: `${operationCount} REST operation${operationCount === 1 ? "" : "s"} exposed this surface.`,
        after: null,
      },
    ];
  }
  return [
    {
      field: humanField(
        items.flatMap((item) => item.changedAspects ?? [])[0] ?? "API contract",
      ),
      before: "Baseline operation contract.",
      after: "The operation contract reflects the judged TypeSpec change.",
    },
  ];
}

function materializeSemanticIntent(intent, evidence, materialized, confidence) {
  const selection = sourceSelections(intent, evidence);
  let operations = selectOperations(intent, evidence, materialized, selection);
  assert(
    operations.length > 0,
    `Semantic intent ${intent.id} has no matching complete operation contract.`,
  );
  if (selection.paths.size === 0) {
    for (const operation of operations) {
      for (const reference of operation.sourceReferences ?? []) {
        selection.paths.add(reference.path);
      }
    }
  }
  const typeSpecDiffs = selectDiffs(selection, materialized);
  assert(
    typeSpecDiffs.length > 0,
    `Semantic intent ${intent.id} has no matching deterministic TypeSpec diff.`,
  );
  const sourceReferences = selectReferences(
    selection,
    operations,
    materialized,
    typeSpecDiffs,
  );
  assert(
    sourceReferences.length > 0,
    `Semantic intent ${intent.id} has no matching deterministic source reference.`,
  );
  for (const operation of operations) {
    operation.sourceReferences = sourceReferences.map((reference) =>
      structuredClone(reference),
    );
  }
  const selectedEvidence = intentEvidence(intent, evidence);
  const retainVersionPropagation = /version-lineage/.test(intent.id);
  const byKind = new Map();
  for (const originalItem of selectedEvidence) {
    let item = originalItem;
    if (
      !retainVersionPropagation &&
      reportKind(item.kind) === "modified" &&
      (item.changes ?? []).length > 0
    ) {
      const materialChanges = item.changes.filter(
        operationHasMaterialAspectChange,
      );
      if (materialChanges.length === 0) continue;
      item = {
        ...item,
        changes: materialChanges,
        operationIds: materialChanges.map((operation) => operation.operationId),
      };
    }
    const kind = reportKind(item.kind);
    if (!byKind.has(kind)) byKind.set(kind, []);
    byKind.get(kind).push(item);
  }
  if (byKind.size === 0) {
    if (selectedEvidence.length > 0) return null;
    const addedDeclaration = typeSpecDiffs.some((hunk) =>
      hunk.lines.some((line, index) => {
        if (!/^\+\s*@added\(/.test(line)) return false;
        return hunk.lines
          .slice(index + 1, index + 10)
          .some((candidate) =>
            /^\+\s*(?:(?:model|interface|enum|union|scalar|alias|op)\s+[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*\s+is\b)/.test(
              candidate,
            ),
          );
      }),
    );
    const hasAddition = typeSpecDiffs.some((hunk) =>
      hunk.lines.some((line) => line.startsWith("+")),
    );
    const hasRemoval = typeSpecDiffs.some((hunk) =>
      hunk.lines.some((line) => line.startsWith("-")),
    );
    const kind = /version-lineage/.test(intent.id)
      ? "modified"
      : addedDeclaration
        ? "added"
        : hasRemoval && !hasAddition
          ? "removed"
          : "modified";
    byKind.set(kind, []);
  }
  if (!retainVersionPropagation) {
    const materialOperationIds = new Set(
      [...byKind.values()].flatMap((items) =>
        items.flatMap((item) =>
          item.operationId ? [item.operationId] : (item.operationIds ?? []),
        ),
      ),
    );
    if (materialOperationIds.size > 0) {
      operations = operations.filter((operation) =>
        materialOperationIds.has(operation.operationId),
      );
    }
  }
  const changes = [...byKind].map(([kind, items]) => {
    if (/version-lineage/.test(intent.id)) kind = "modified";
    const evidencedOperationIds = [
      ...new Set(
        items.flatMap((item) =>
          item.operationId ? [item.operationId] : (item.operationIds ?? []),
        ),
      ),
    ];
    const operationIds =
      evidencedOperationIds.length > 0
        ? evidencedOperationIds
        : [...new Set(operations.map((operation) => operation.operationId))];
    const changeOperations = operations.filter((operation) =>
      operationIds.includes(operation.operationId),
    );
    return {
      kind,
      summary: intent.title,
      operationIds,
      apiVersions: [
        ...new Set(
          changeOperations.flatMap((operation) => operation.apiVersions),
        ),
      ],
      aspects:
        intent.aspects ??
        (retainVersionPropagation
          ? [
              {
                field: "API version availability",
                before: null,
                after: `${operationIds.length} existing operation${operationIds.length === 1 ? "" : "s"} are exposed in the new API version; this does not imply a wire-behavior change.`,
              },
            ]
          : evidenceAspects(items, kind, operationIds.length)),
      effect: intent.rationale,
      typeSpecCause: intent.rationale,
      sourceReferences,
      typeSpecDiffs,
      linkedFindingIds: [],
    };
  });
  return {
    id: intent.id,
    intent: intent.title,
    transformationChain: [intent.rationale],
    changes,
    restRepresentation: {
      summary: intent.rationale,
      operations,
    },
    confidence,
    sourceReferences,
  };
}

function sanitizeNarrative(value) {
  return value
    .replace(INTERNAL_TERMS, (term) => {
      if (/TCGC/i.test(term)) return "generated SDK behavior";
      if (/cross-language/i.test(term)) return "generated SDK type";
      if (/isUnionAsEnum/i.test(term)) return "enum representation";
      return "enum extensibility";
    })
    .replace(/\s+/g, " ")
    .trim();
}

const RULE_TITLES = {
  "parameter-contract-changed": "Operation parameter contract changes",
  "response-contract-changed": "Operation response contract changes",
  "paging-metadata-added": "Generated SDK result becomes pageable",
  "client-location-changed": "Generated SDK method moves between clients",
  "sdk-lro-recognition-changed":
    "Generated SDK long-running operation behavior changes",
  "sdk-enum-shape-changed": "Generated SDK enum shape changes",
  "client-property-flattening-changed":
    "Generated SDK model property shape changes",
};

function findingTitle(candidate) {
  return (
    RULE_TITLES[candidate.rule] ??
    sanitizeNarrative(
      candidate.summary ?? humanField(candidate.rule ?? "API impact"),
    )
      .replace(/\.$/, "")
      .slice(0, 140)
  );
}

function candidatePaths(candidate) {
  const evidence = Array.isArray(candidate.evidence)
    ? candidate.evidence
    : [candidate.evidence];
  return new Set(
    evidence
      .filter((item) => item && typeof item === "object")
      .map((item) => item.path)
      .filter(Boolean),
  );
}

function candidateOperations(candidate, evidence) {
  const keys = new Set();
  if (candidate.evidence?.operation) keys.add(candidate.evidence.operation);
  const operationIds = new Set();
  for (const change of evidence.operationChanges.values()) {
    if (keys.has(change.operationKey)) operationIds.add(change.operationId);
  }
  if (operationIds.size === 0 && typeof candidate.summary === "string") {
    for (const change of evidence.operationChanges.values()) {
      if (candidate.summary.includes(change.operationId)) {
        operationIds.add(change.operationId);
      }
    }
  }
  return operationIds;
}

function relatedSemanticItems(candidate, items, evidence) {
  const paths = candidatePaths(candidate);
  const operationIds = candidateOperations(candidate, evidence);
  const related = items.filter(
    (item) =>
      item.sourceReferences.some((reference) => paths.has(reference.path)) ||
      item.restRepresentation.operations.some((operation) =>
        operationIds.has(operation.operationId),
      ),
  );
  if (related.length > 0) return related;
  return items.length === 1 ? items : [];
}

function findingEvidence(candidate) {
  const result = [];
  if (candidate.summary) result.push(sanitizeNarrative(candidate.summary));
  const paths = candidatePaths(candidate);
  for (const path of paths) result.push(`Changed TypeSpec source: ${path}.`);
  if (candidate.evidence?.operation) {
    result.push(`Compared REST operation: ${candidate.evidence.operation}.`);
  }
  return result.length > 0
    ? result
    : ["Bounded deterministic change evidence."];
}

function materializeCandidateFindings(decisions, candidates, items, evidence) {
  return decisions
    .filter((decision) => decision.decision === "approve")
    .map((decision) => {
      const candidate = candidates.get(decision.id);
      const related = relatedSemanticItems(candidate, items, evidence);
      assert(
        related.length > 0,
        `Approved candidate ${decision.id} cannot be linked to a semantic intent.`,
      );
      const sourceReferences = uniqueByJson(
        related.flatMap((item) => item.sourceReferences),
      );
      return {
        finding: {
          id: decision.id,
          title: findingTitle(candidate),
          severity: decision.severity ?? candidate.severity ?? "medium",
          confidence: items[0]?.confidence ?? "medium",
          summary: sanitizeNarrative(decision.rationale),
          evidence: findingEvidence(candidate),
          sourceReferences,
        },
        related,
      };
    });
}

function derivedRestDownstreamFinding(restEntries, confidence) {
  if (restEntries.length === 0) return undefined;
  const severityOrder = { high: 0, medium: 1, low: 2 };
  const severity = restEntries
    .map((entry) => entry.finding.severity)
    .sort(
      (left, right) =>
        (severityOrder[left] ?? 99) - (severityOrder[right] ?? 99),
    )[0];
  return {
    finding: {
      id: "derived-rest-contract-sdk-impact",
      title: "REST contract changes require generated-client updates",
      severity,
      confidence,
      summary:
        "The approved REST contract changes also alter generated client request or response handling and can break callers compiled against the previous contract.",
      evidence: restEntries.map(
        (entry) => `Approved REST finding: ${entry.finding.id}.`,
      ),
      sourceReferences: uniqueByJson(
        restEntries.flatMap((entry) => entry.finding.sourceReferences),
      ),
    },
    related: uniqueByJson(restEntries.flatMap((entry) => entry.related)),
  };
}

function decodeHtml(value) {
  return value
    .replace(/&#x3C;/gi, "<")
    .replace(/&#x3E;/gi, ">")
    .replace(/&#x26;/gi, "&")
    .replace(/&quot;/gi, '"')
    .replace(/&#39;|&apos;/gi, "'")
    .replace(/&amp;/gi, "&");
}

function findingCodeAnchors(finding) {
  const evidence = Array.isArray(finding.evidence)
    ? finding.evidence.join(" ")
    : finding.evidence;
  const text = `${finding.title} ${evidence}`;
  const declarationPhrases = [
    ...text.matchAll(
      /\b(model|interface|alias|enum|union)\s+([A-Za-z_][A-Za-z0-9_]*)/g,
    ),
  ].map((match) => `${match[1]} ${match[2]}`);
  for (const match of text.matchAll(
    /\b([A-Za-z_][A-Za-z0-9_]*)\s+aliases\b/g,
  )) {
    declarationPhrases.push(`alias ${match[1]}`);
  }
  return {
    declarationPhrases: [...new Set(declarationPhrases)],
    tokens: [
      ...new Set(
        `${finding.title} ${evidence}`.match(/[A-Z][A-Za-z0-9_]{3,}/g) ?? [],
      ),
    ].sort((left, right) => right.length - left.length),
  };
}

function snippetFromDiff(
  hunks,
  references,
  anchors = { declarationPhrases: [], tokens: [] },
) {
  const candidates = [];
  for (const reference of references) {
    for (const hunk of hunks.filter(
      (candidate) => candidate.path === reference.path,
    )) {
      let oldLine = hunk.oldStart;
      let newLine = hunk.newStart;
      const rows = [];
      for (const rawLine of hunk.lines) {
        if (rawLine === "\\ No newline at end of file") continue;
        const prefix = rawLine[0];
        if (reference.revision === "base" && prefix !== "+") {
          rows.push({
            number: oldLine,
            line: rawLine.slice(1),
            changed: prefix === "-",
          });
        }
        if (reference.revision !== "base" && prefix !== "-") {
          rows.push({
            number: newLine,
            line: rawLine.slice(1),
            changed: prefix === "+",
          });
        }
        if (prefix !== "+") oldLine += 1;
        if (prefix !== "-") newLine += 1;
      }
      const eligible = rows.filter(
        (row) =>
          row.number >= reference.startLine && row.number <= reference.endLine,
      );
      if (eligible.length === 0) continue;
      const scoredRows = eligible.map((row, index) => ({
        index,
        score:
          anchors.declarationPhrases.reduce(
            (score, phrase) =>
              score +
              (new RegExp(`\\b${phrase.replace(" ", "\\s+")}\\b`).test(row.line)
                ? 100
                : 0),
            0,
          ) +
          anchors.tokens.reduce(
            (score, anchor) => score + (row.line.includes(anchor) ? 1 : 0),
            0,
          ),
      }));
      const anchored = scoredRows.sort(
        (left, right) => right.score - left.score,
      )[0];
      const changedIndex = Math.max(
        0,
        eligible.findIndex((row) => row.changed),
      );
      candidates.push({
        eligible,
        focusIndex: anchored?.score > 0 ? anchored.index : changedIndex,
        score: anchored?.score ?? 0,
        reference,
      });
    }
  }
  const best = candidates.sort((left, right) => right.score - left.score)[0];
  if (best) {
    const startIndex = Math.max(
      0,
      Math.min(best.focusIndex - 3, best.eligible.length - 12),
    );
    const selected = best.eligible.slice(startIndex, startIndex + 12);
    return {
      path: best.reference.path,
      startLine: selected[0].number,
      endLine: selected.at(-1).number,
      lines: selected.map((row) => row.line),
    };
  }
  return undefined;
}

function complianceSources(finding, evidence, materialized) {
  const selection = sourceSelections(finding, evidence);
  const references = selectReferences(selection, [], materialized);
  const diffs = selectDiffs(selection, materialized);
  const snippet = snippetFromDiff(
    diffs,
    references,
    findingCodeAnchors(finding),
  );
  assert(
    references.length > 0 && snippet,
    `Compliance finding ${finding.id} has no exact deterministic source evidence.`,
  );
  return { references, snippet };
}

function materializeCompliance(
  judgment,
  evidence,
  materialized,
  semanticItems,
) {
  const findingData = judgment.findings.map((finding) => {
    const sources = complianceSources(finding, evidence, materialized);
    return {
      ...finding,
      sources,
      output: {
        id: finding.id,
        title: finding.title,
        severity: finding.severity,
        summary: finding.summary,
        documentationUrl: finding.documentationUrl,
        evidence: Array.isArray(finding.evidence)
          ? finding.evidence
          : [finding.evidence],
        sourceReferences: sources.references,
        codeSnippets: [sources.snippet],
      },
    };
  });
  const fallbackReferences = uniqueByJson(
    semanticItems.flatMap((item) => item.sourceReferences),
  );
  const documents = judgment.documentUrls.map((url) => {
    const document = evidence.documents.get(url);
    const relatedFindings = findingData.filter(
      (finding) => finding.documentationUrl === url,
    );
    const sourceReferences = uniqueByJson(
      relatedFindings.length > 0
        ? relatedFindings.flatMap((finding) => finding.sources.references)
        : fallbackReferences,
    );
    const output = {
      title: document.title,
      url,
      section: document.category ?? "Matched documentation evidence",
      guidanceExcerpt: (document.matchingExcerpt ?? "").slice(0, 500),
      applicableGuidance: judgment.rationale,
      evidence:
        relatedFindings.flatMap((finding) => finding.evidence).join(" ") ||
        judgment.rationale,
      sourceReferences,
    };
    if (relatedFindings.length > 0) {
      const codeBlock = document.candidateCodeBlocks?.[0];
      if (codeBlock) {
        output.expectedCodeStatus = "available";
        output.expectedCodeSnippets = [
          {
            language: "tsp",
            caption: "Documented TypeSpec example",
            url,
            section: output.section,
            lines: decodeHtml(codeBlock).split(/\r?\n/).slice(0, 12),
          },
        ];
      } else {
        output.expectedCodeStatus = "not-present";
        output.expectedCodeReason =
          "The bounded official document evidence did not contain an example block.";
      }
    }
    return output;
  });
  const result = {
    status: judgment.status,
    summary: {
      patternsAssessed: documents.length,
      findingCount: findingData.length,
    },
    documents,
    findings: findingData.map((finding) => finding.output),
  };
  if (judgment.status === "not-assessed") result.reason = judgment.rationale;
  return { result, findingData };
}

function linkFinding(
  items,
  findingId,
  relatedItems,
  { preferAdded = false } = {},
) {
  for (const item of relatedItems) {
    const added = item.changes.filter((change) => change.kind === "added");
    const changes = preferAdded && added.length > 0 ? added : item.changes;
    for (const change of changes) {
      if (!change.linkedFindingIds.includes(findingId)) {
        change.linkedFindingIds.push(findingId);
      }
    }
  }
}

function deterministicPreparationMs(modelInput) {
  const duration = modelInput.assessmentDuration ?? {};
  const value = duration.totalMs ?? duration.preparationMs ?? 0;
  assert(
    Number.isInteger(value) && value >= 0,
    "model-input assessmentDuration must contain a non-negative integer timing.",
  );
  return value;
}

function allocateJudgmentMs(modelInput, judgmentMs) {
  const semanticProjects = (modelInput.projects ?? []).map((project) => ({
    path: project.path,
    operationChanges: project.rest?.operationChanges ?? [],
    operationGroups: project.rest?.operationGroups ?? [],
  }));
  const values = {
    semanticUnderstandingMs: [modelInput.sourceFiles ?? [], semanticProjects],
    restBreakingMs: (modelInput.projects ?? []).flatMap(
      (project) => project.rest?.breakingCandidates ?? [],
    ),
    downstreamBreakingMs: (modelInput.projects ?? []).flatMap(
      (project) => project.downstream?.candidates ?? [],
    ),
    complianceMs: modelInput.complianceEvidence ?? {},
  };
  const weighted = Object.entries(values).map(([name, value]) => ({
    name,
    weight: Math.max(1, Buffer.byteLength(JSON.stringify(value), "utf8")),
  }));
  const totalWeight = weighted.reduce((sum, item) => sum + item.weight, 0);
  let assigned = 0;
  return Object.fromEntries(
    weighted.map((item, index) => {
      const allocation =
        index === weighted.length - 1
          ? judgmentMs - assigned
          : Math.floor((judgmentMs * item.weight) / totalWeight);
      assigned += allocation;
      return [item.name, allocation];
    }),
  );
}

function timing(modelInput, judgmentElapsedMs, assemblyMs, renderMs) {
  const preparationMs = deterministicPreparationMs(modelInput);
  const judgmentMs = judgmentElapsedMs ?? 0;
  const judgmentAllocation = allocateJudgmentMs(modelInput, judgmentMs);
  const documentationEvidenceMs = Math.min(
    preparationMs,
    modelInput.assessmentDuration?.documentationEvidenceMs ?? 0,
  );
  const totalMs = preparationMs + judgmentMs + assemblyMs + renderMs;
  return {
    totalMs,
    note:
      judgmentElapsedMs === undefined
        ? "Deterministic preparation, assembly, and renderer timings are measured. Judgment time was not supplied and is excluded from the total. Exact phases are in assessmentDuration.phases."
        : "Deterministic preparation, aggregate judgment, assembly, and renderer timings are measured. Judgment time is allocated across dimensions by serialized evidence size and labeled estimated.",
    phases: {
      deterministicPreparationMs: preparationMs,
      deterministicAssemblyMs: assemblyMs,
      judgmentMs: judgmentElapsedMs ?? null,
      renderMs,
    },
    breakdown: {
      semanticUnderstandingMs: judgmentAllocation.semanticUnderstandingMs,
      semanticUnderstandingQuality: "estimated",
      restBreakingMs: judgmentAllocation.restBreakingMs,
      restBreakingQuality: "estimated",
      downstreamBreakingMs: judgmentAllocation.downstreamBreakingMs,
      downstreamBreakingQuality: "estimated",
      complianceMs: judgmentAllocation.complianceMs + documentationEvidenceMs,
      complianceQuality: "estimated/measured",
      overheadMs:
        preparationMs - documentationEvidenceMs + assemblyMs + renderMs,
      overheadQuality: "measured",
      totalMs,
      totalQuality:
        judgmentElapsedMs === undefined ? "estimated/measured" : "measured",
      searchRoute:
        (modelInput.complianceEvidence?.documents ?? []).length > 0
          ? "bounded official document evidence"
          : "no applicable document evidence",
    },
  };
}

export function assembleAssessment({
  modelInput,
  judgment,
  materialization,
  artifactAnalysis,
  retainedEvidence,
  judgmentElapsedMs,
  deterministicAssemblyMs = 0,
  renderMs = 0,
}) {
  const evidence = validateJudgment(judgment, modelInput);
  const pr =
    judgment.pr === undefined
      ? undefined
      : normalizePositiveInteger(judgment.pr, "assessment judgment pr");
  assert(
    materialization?.schemaVersion === 2,
    "Deterministic materialization assessment schemaVersion must be 2.",
  );
  if (pr !== undefined && materialization.pr !== undefined) {
    const materializationPr = normalizePositiveInteger(
      materialization.pr,
      "materialization pr",
    );
    assert(
      materializationPr === pr,
      `Judgment PR ${judgment.pr} does not match materialization PR ${materialization.pr}.`,
    );
  }
  const materialized = materializationData(
    materialization,
    artifactAnalysis,
    retainedEvidence,
  );
  const semanticItems = judgment.semanticIntents
    .map((intent) =>
      materializeSemanticIntent(
        intent,
        evidence,
        materialized,
        judgment.overallConfidence,
      ),
    )
    .filter(Boolean);
  const operationOwners = new Map();
  for (const item of semanticItems) {
    for (const operation of item.restRepresentation.operations) {
      const owner = operationOwners.get(operation.operationId);
      assert(
        owner === undefined || owner === item.id,
        `Operation ${operation.operationId} is assigned to multiple semantic intents: ${owner}, ${item.id}.`,
      );
      operationOwners.set(operation.operationId, item.id);
    }
  }
  const rest = materializeCandidateFindings(
    judgment.restCandidates,
    evidence.restCandidates,
    semanticItems,
    evidence,
  );
  const downstream = materializeCandidateFindings(
    judgment.downstreamCandidates,
    evidence.downstreamCandidates,
    semanticItems,
    evidence,
  );
  if (rest.length > 0 && downstream.length === 0) {
    downstream.push(
      derivedRestDownstreamFinding(rest, judgment.overallConfidence),
    );
  }
  for (const entry of [...rest, ...downstream]) {
    linkFinding(semanticItems, entry.finding.id, entry.related);
  }
  const compliance = materializeCompliance(
    judgment.compliance,
    evidence,
    materialized,
    semanticItems,
  );
  for (const finding of compliance.findingData) {
    const findingSourceChangeIds = new Set(finding.sourceChangeIds);
    let related = semanticItems.filter((_, index) =>
      judgment.semanticIntents[index].sourceChangeIds.some((id) =>
        findingSourceChangeIds.has(id),
      ),
    );
    if (related.length === 0) {
      const findingPaths = new Set(finding.sourcePaths);
      related = semanticItems.filter((_, index) =>
        judgment.semanticIntents[index].sourcePaths.some((path) =>
          findingPaths.has(path),
        ),
      );
    }
    assert(
      related.length > 0,
      `Compliance finding ${finding.id} cannot be linked to a semantic intent.`,
    );
    linkFinding(semanticItems, finding.id, related, { preferAdded: true });
  }

  const metadataFields = ["title", "url", "state", "createdAt"];
  const assessment = {
    schemaVersion: 2,
    overallConfidence: judgment.overallConfidence,
  };
  if (pr !== undefined) assessment.pr = pr;
  for (const field of pr === undefined ? [] : metadataFields) {
    if (materialization[field] !== undefined) {
      assessment[field] = structuredClone(materialization[field]);
    }
  }
  assessment.baseline = structuredClone(
    pr === undefined
      ? (modelInput.baseline ?? modelInput.comparison?.baseline)
      : (materialization.baseline ??
          modelInput.baseline ??
          modelInput.comparison?.baseline),
  );
  assessment.head = structuredClone(
    pr === undefined
      ? (modelInput.head ?? modelInput.comparison?.head)
      : (materialization.head ??
          modelInput.head ??
          modelInput.comparison?.head),
  );
  assessment.projects = (modelInput.projects ?? []).map(
    (project) => project.path,
  );
  if (pr === undefined) {
    assessment.assessmentEvidence = {
      changedTypeSpec: materialized.sourceReferences.map((reference) =>
        structuredClone(reference),
      ),
      emitterRuns: structuredClone(
        materialization.assessmentEvidence?.emitterRuns ?? [],
      ),
    };
  } else if (materialization.assessmentEvidence) {
    assessment.assessmentEvidence = structuredClone(
      materialization.assessmentEvidence,
    );
  }
  if (materialization.artifactEvidence) {
    assessment.artifactEvidence = structuredClone(
      materialization.artifactEvidence,
    );
  }
  assessment.dimensions = {
    semanticUnderstanding: { items: semanticItems },
    restBreakingChanges: {
      findings: rest.map((entry) => entry.finding),
    },
    restCompatibleDownstreamBreakingChanges: {
      findings: downstream.map((entry) => entry.finding),
    },
    azureCompliance: compliance.result,
  };
  assessment.errors = [
    ...new Set([...(modelInput.errors ?? []), ...judgment.blockers]),
  ];
  assessment.assessmentDuration = timing(
    modelInput,
    judgmentElapsedMs,
    deterministicAssemblyMs,
    renderMs,
  );
  return assessment;
}

function isInside(parent, child) {
  const normalizedParent =
    process.platform === "win32" ? parent.toLowerCase() : parent;
  const normalizedChild =
    process.platform === "win32" ? child.toLowerCase() : child;
  const value = relative(normalizedParent, normalizedChild);
  return value === "" || (!value.startsWith(`..${sep}`) && value !== "..");
}

function assertSafeOutputDirectory(outputDirectory, materializationPath) {
  assert(
    !isInside(CANONICAL_REPORT_ROOT, outputDirectory),
    `Refusing to overwrite canonical report directory: ${outputDirectory}.`,
  );
  assert(
    outputDirectory !== dirname(materializationPath),
    "Output directory must differ from the deterministic materialization directory.",
  );
  for (const name of ["assessment.json", "assessment.html"]) {
    const reportPath = join(outputDirectory, name);
    assert(
      !existsSync(reportPath),
      `Refusing to overwrite existing report file: ${reportPath}.`,
    );
  }
}

function retainedTypeSpecDiffs(evidence, sourcePaths) {
  if (Array.isArray(evidence.typeSpecDiffs)) return evidence.typeSpecDiffs;
  const repositoryRoot = evidence.repositoryRoot;
  if (
    typeof repositoryRoot !== "string" ||
    !existsSync(repositoryRoot) ||
    sourcePaths.length === 0
  ) {
    return [];
  }
  const result = spawnSync(
    "git",
    [
      "-C",
      repositoryRoot,
      "--no-pager",
      "diff",
      "--unified=20",
      evidence.baseline.commit,
      evidence.head.commit,
      "--",
      ...sourcePaths,
    ],
    {
      encoding: "utf8",
      windowsHide: true,
    },
  );
  assert(
    result.status === 0,
    `Unable to recover retained TypeSpec source diffs: ${result.stderr.trim() || `git exited with ${result.status}`}.`,
  );
  return parseTypeSpecDiffHunks(result.stdout);
}

export function assembleAssessmentFiles({
  modelInputPath,
  judgmentPath,
  materializationPath,
  outputDirectory,
  evidenceDirectory,
  judgmentElapsedMs,
}) {
  const resolvedModelInput = resolve(modelInputPath);
  const resolvedJudgment = resolve(judgmentPath);
  const resolvedMaterialization = resolve(materializationPath);
  const resolvedOutput = resolve(outputDirectory);
  assertSafeOutputDirectory(resolvedOutput, resolvedMaterialization);

  const assemblyStartedAt = process.hrtime.bigint();
  const modelInput = readJson(resolvedModelInput, "model input");
  const judgment = readJson(resolvedJudgment, "assessment judgment");
  const materialization = readJson(
    resolvedMaterialization,
    "deterministic materialization",
  );
  let artifactAnalysis;
  let retainedMaterialization;
  if (evidenceDirectory !== undefined) {
    const resolvedEvidenceDirectory = resolve(evidenceDirectory);
    const retainedEvidence = readJson(
      join(resolvedEvidenceDirectory, "evidence.json"),
      "retained deterministic evidence",
    );
    assert(
      retainedEvidence?.schemaVersion === 1,
      "Retained evidence schemaVersion must be 1.",
    );
    assert(
      retainedEvidence.baseline?.commit === modelInput.baseline?.commit &&
        retainedEvidence.head?.commit === modelInput.head?.commit,
      "Retained evidence comparison does not match model-input.json.",
    );
    artifactAnalysis = analyzeArtifacts(
      retainedEvidence,
      resolvedEvidenceDirectory,
    );
    retainedMaterialization = {
      sourceReferences: retainedEvidence.sourceReferences ?? [],
      typeSpecDiffs: retainedTypeSpecDiffs(
        retainedEvidence,
        (modelInput.sourceFiles ?? []).map((source) => source.path),
      ),
    };
  }
  let assessment = assembleAssessment({
    modelInput,
    judgment,
    materialization,
    artifactAnalysis,
    retainedEvidence: retainedMaterialization,
    judgmentElapsedMs,
  });
  const deterministicAssemblyMs = elapsedMs(assemblyStartedAt);

  const renderStartedAt = process.hrtime.bigint();
  renderAssessmentHtml(assessment);
  const renderMs = elapsedMs(renderStartedAt);
  assessment = assembleAssessment({
    modelInput,
    judgment,
    materialization,
    artifactAnalysis,
    retainedEvidence: retainedMaterialization,
    judgmentElapsedMs,
    deterministicAssemblyMs,
    renderMs,
  });
  const html = renderAssessmentHtml(assessment);
  const validationErrors = validateAssessment(assessment);
  assert(
    validationErrors.length === 0,
    `Assembled assessment is invalid:\n${validationErrors.join("\n")}`,
  );

  mkdirSync(resolvedOutput, { recursive: true });
  writeFileSync(
    join(resolvedOutput, "assessment.json"),
    `${JSON.stringify(assessment, null, 2)}\n`,
  );
  writeFileSync(join(resolvedOutput, "assessment.html"), html);
  return assessment;
}

function parseArguments(values) {
  const positional = [];
  let judgmentElapsedMs;
  let evidenceDirectory;
  for (let index = 0; index < values.length; index += 1) {
    if (values[index] === "--judgment-elapsed-ms") {
      const raw = values[index + 1];
      assert(raw !== undefined, "--judgment-elapsed-ms requires a value.");
      judgmentElapsedMs = Number(raw);
      assert(
        Number.isInteger(judgmentElapsedMs) && judgmentElapsedMs >= 0,
        "--judgment-elapsed-ms must be a non-negative integer.",
      );
      index += 1;
    } else if (values[index] === "--evidence-directory") {
      const raw = values[index + 1];
      assert(raw !== undefined, "--evidence-directory requires a value.");
      evidenceDirectory = raw;
      index += 1;
    } else {
      positional.push(values[index]);
    }
  }
  assert(
    positional.length === 4,
    "Usage: assemble-assessment.mjs <model-input.json> <assessment-judgment.json> <materialization-assessment.json> <output-directory> [--evidence-directory <rerun-pr-folder>] [--judgment-elapsed-ms <ms>]",
  );
  return {
    modelInputPath: positional[0],
    judgmentPath: positional[1],
    materializationPath: positional[2],
    outputDirectory: positional[3],
    evidenceDirectory,
    judgmentElapsedMs,
  };
}

function main() {
  try {
    const assessment = assembleAssessmentFiles(
      parseArguments(process.argv.slice(2)),
    );
    process.stdout.write(
      assessment.pr === undefined
        ? "Assembled and validated local assessment.\n"
        : `Assembled and validated assessment for PR ${assessment.pr}.\n`,
    );
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  main();
}
