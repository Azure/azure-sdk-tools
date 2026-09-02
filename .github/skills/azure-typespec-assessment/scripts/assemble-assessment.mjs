import path from "node:path";
import fs from "node:fs";
import { parseArgs, isMain, readJson, runMain, writeJson } from "./cli.mjs";
import { deriveSafety, dimensionStatus } from "./assessment-display.mjs";
import { canonicalJson, stableId } from "./stable-id.mjs";
import {
  diffPublicParameters,
  publicParameterContract,
  semanticLroContract,
  typeIdentity,
} from "./sdk-method-delta.mjs";
import { assembleCompliance } from "./compliance-assessment.mjs";

function duplicates(values) {
  const seen = new Set();
  return values.filter((value) =>
    seen.has(value) ? true : (seen.add(value), false),
  );
}

function exactCoverage(expected, actual, label) {
  const duplicate = duplicates(actual);
  if (duplicate.length)
    throw new Error(
      `Duplicate ${label} IDs: ${[...new Set(duplicate)].join(", ")}`,
    );
  const expectedSet = new Set(expected);
  const unknown = actual.filter((item) => !expectedSet.has(item));
  const missing = expected.filter((item) => !actual.includes(item));
  if (unknown.length || missing.length) {
    throw new Error(
      `${label} coverage mismatch. Missing: ${missing.join(", ") || "none"}; unknown: ${unknown.join(", ") || "none"}.`,
    );
  }
}

function validateDecision(decision, candidate) {
  if (!["approve", "reject"].includes(decision.decision)) {
    throw new Error(`Invalid decision for ${candidate.id}.`);
  }
  if (!decision.rationale?.trim())
    throw new Error(`Missing rationale for ${candidate.id}.`);
  if (
    decision.decision === "approve" &&
    !["high", "medium", "low"].includes(decision.severity)
  ) {
    throw new Error(
      `An approve decision for candidate ${candidate.id} requires severity.`,
    );
  }
  if (decision.decision === "reject" && decision.severity !== undefined) {
    throw new Error(`Rejected candidate ${candidate.id} must omit severity.`);
  }
}

function assertKeys(value, allowed, label) {
  const unknown = Object.keys(value).filter((key) => !allowed.includes(key));
  if (unknown.length)
    throw new Error(`${label} contains unknown fields: ${unknown.join(", ")}.`);
}

function validateJudgment(answer) {
  if (!answer || typeof answer !== "object" || Array.isArray(answer)) {
    throw new Error("Judgment must be an object.");
  }
  assertKeys(
    answer,
    [
      "schemaVersion",
      "semanticIntents",
      "restDecisions",
      "downstreamDecisions",
      "complianceDecisions",
      "overallConfidence",
      "blockers",
    ],
    "Judgment",
  );
  if (answer.schemaVersion !== 1)
    throw new Error("Unsupported judgment schemaVersion.");
  for (const field of [
    "semanticIntents",
    "restDecisions",
    "downstreamDecisions",
    "complianceDecisions",
    "blockers",
  ]) {
    if (!Array.isArray(answer[field]))
      throw new Error(`Judgment.${field} must be an array.`);
  }
  if (!["high", "medium", "low"].includes(answer.overallConfidence)) {
    throw new Error("Judgment.overallConfidence is invalid.");
  }
  if (answer.blockers.some((item) => typeof item !== "string")) {
    throw new Error("Judgment.blockers must contain strings.");
  }
  for (const intent of answer.semanticIntents) {
    assertKeys(
      intent,
      ["reviewUnitId", "title", "summary"],
      `Semantic intent ${intent.reviewUnitId ?? "<unknown>"}`,
    );
    if (!intent.title?.trim() || !intent.summary?.trim()) {
      throw new Error(
        `Semantic intent ${intent.reviewUnitId ?? "<unknown>"} is incomplete.`,
      );
    }
  }
  for (const decision of [
    ...answer.restDecisions,
    ...answer.downstreamDecisions,
  ]) {
    assertKeys(
      decision,
      ["candidateId", "decision", "severity", "rationale"],
      `Decision ${decision.candidateId ?? "<unknown>"}`,
    );
  }
  for (const decision of answer.complianceDecisions) {
    assertKeys(
      decision,
      [
        "reviewUnitId",
        "applicableGuidance",
        "sourceChangeIds",
        "hunkIds",
        "declarationIds",
        "decision",
        "title",
        "severity",
        "expected",
        "actual",
        "rationale",
      ],
      `Compliance decision ${decision.reviewUnitId ?? "<unknown>"}`,
    );
  }
}

function validateInference(inference, requests, modelInput) {
  if (!inference || typeof inference !== "object" || Array.isArray(inference)) {
    throw new Error("Inference must be an object.");
  }
  assertKeys(inference, ["schemaVersion", "results"], "Inference");
  if (inference.schemaVersion !== 1)
    throw new Error("Unsupported inference schemaVersion.");
  if (!Array.isArray(inference.results))
    throw new Error("Inference.results must be an array.");
  exactCoverage(
    requests.map((item) => item.requestId),
    inference.results.map((item) => item.requestId),
    "inference request",
  );
  const requestsById = new Map(
    requests.map((request) => [request.requestId, request]),
  );
  const candidatesById = new Map();
  for (const result of inference.results) {
    assertKeys(
      result,
      [
        "requestId",
        "reviewUnitId",
        "hunkId",
        "decision",
        "rationale",
        "candidates",
      ],
      `Inference result ${result.requestId ?? "<unknown>"}`,
    );
    const request = requestsById.get(result.requestId);
    if (
      result.reviewUnitId !== request.reviewUnitId ||
      result.hunkId !== request.hunkId
    ) {
      throw new Error(
        `Inference result ${result.requestId} does not match its request.`,
      );
    }
    if (!["candidates", "no-impact", "blocked"].includes(result.decision)) {
      throw new Error(
        `Inference result ${result.requestId} has an invalid decision.`,
      );
    }
    if (!result.rationale?.trim()) {
      throw new Error(
        `Inference result ${result.requestId} requires a rationale.`,
      );
    }
    if (!Array.isArray(result.candidates)) {
      throw new Error(
        `Inference result ${result.requestId}.candidates must be an array.`,
      );
    }
    if (result.decision === "candidates" && !result.candidates.length) {
      throw new Error(
        `Inference result ${result.requestId} requires candidates.`,
      );
    }
    if (result.decision !== "candidates" && result.candidates.length) {
      throw new Error(
        `Inference result ${result.requestId} must not contain candidates.`,
      );
    }
    for (const candidate of result.candidates) {
      assertKeys(
        candidate,
        [
          "id",
          "dimension",
          "rule",
          "defaultSeverity",
          "actual",
          "expected",
          "crossLanguageDefinitionId",
          "sourceChangeIds",
          "hunkIds",
          "operationIds",
          "evidenceFactIds",
          "reviewRequired",
        ],
        `Inferred candidate ${candidate.id ?? "<unknown>"}`,
      );
      if (!request.allowedDimensions.includes(candidate.dimension)) {
        throw new Error(
          `Inferred candidate ${candidate.id} uses a disallowed dimension.`,
        );
      }
      if (!candidate.id?.startsWith(`inferred-${candidate.dimension}-`)) {
        throw new Error(
          `Inferred candidate ${candidate.id ?? "<unknown>"} has an invalid ID.`,
        );
      }
      if (
        !candidate.rule?.trim() ||
        !candidate.actual?.trim() ||
        !candidate.expected?.trim()
      ) {
        throw new Error(`Inferred candidate ${candidate.id} is incomplete.`);
      }
      if (!["high", "medium", "low"].includes(candidate.defaultSeverity)) {
        throw new Error(
          `Inferred candidate ${candidate.id} has an invalid default severity.`,
        );
      }
      if (candidate.reviewRequired !== true) {
        throw new Error(
          `Inferred candidate ${candidate.id} must require review.`,
        );
      }
      const allowedHunkIds = new Set(
        requests
          .filter(
            (item) =>
              item.reviewUnitId === request.reviewUnitId &&
              item.sourceChangeId === request.sourceChangeId,
          )
          .map((item) => item.hunkId),
      );
      if (
        candidate.sourceChangeIds.length !== 1 ||
        candidate.sourceChangeIds[0] !== request.sourceChangeId ||
        !candidate.hunkIds.includes(request.hunkId) ||
        candidate.hunkIds.some((id) => !allowedHunkIds.has(id))
      ) {
        throw new Error(
          `Inferred candidate ${candidate.id} is outside its source request.`,
        );
      }
      if (
        (candidate.operationIds ?? []).some(
          (id) => !request.relatedOperationIds.includes(id),
        )
      ) {
        throw new Error(
          `Inferred candidate ${candidate.id} uses an unknown operation.`,
        );
      }
      if (
        (candidate.evidenceFactIds ?? []).some(
          (id) => modelInput.facts?.[id] === undefined,
        )
      ) {
        throw new Error(
          `Inferred candidate ${candidate.id} uses an unknown fact.`,
        );
      }
      if (
        candidate.dimension === "downstream" &&
        !candidate.crossLanguageDefinitionId?.trim()
      ) {
        throw new Error(
          `Inferred downstream candidate ${candidate.id} requires an SDK symbol.`,
        );
      }
      const existing = candidatesById.get(candidate.id);
      if (existing && canonicalJson(existing) !== canonicalJson(candidate)) {
        throw new Error(
          `Inferred candidate ${candidate.id} has conflicting definitions.`,
        );
      }
      candidatesById.set(candidate.id, candidate);
    }
  }
}

function validateInferenceRequests(modelInput) {
  const units = modelInput.semanticReviewUnits ?? [];
  const requests = modelInput.inferenceRequests ?? [];
  const expected = units.flatMap((unit) =>
    (unit.deterministicCoverage?.uncoveredHunkIds ?? []).map(
      (hunkId) => `${unit.reviewUnitId}\u0000${hunkId}`,
    ),
  );
  const actual = requests.map(
    (request) => `${request.reviewUnitId}\u0000${request.hunkId}`,
  );
  exactCoverage(expected, actual, "inference request target");
  const unitsById = new Map(units.map((unit) => [unit.reviewUnitId, unit]));
  for (const request of requests) {
    const unit = unitsById.get(request.reviewUnitId);
    const source = modelInput.sourceChanges?.[request.sourceChangeId];
    if (!unit || !source) {
      throw new Error(
        `Inference request ${request.requestId} has unknown source evidence.`,
      );
    }
    if (!source.hunks?.some((hunk) => hunk.id === request.hunkId)) {
      throw new Error(
        `Inference request ${request.requestId} has an unknown hunk.`,
      );
    }
    if (!request.sourceExcerpt?.trim()) {
      throw new Error(
        `Inference request ${request.requestId} has no source excerpt.`,
      );
    }
  }
}

function sourceMap(sourceIndex) {
  return Object.fromEntries(
    sourceIndex.sourceChanges.map((source) => [source.id, source]),
  );
}

function joinFindings(candidates, decisions, facts, sources) {
  const decisionMap = new Map(
    decisions.map((decision) => [decision.candidateId, decision]),
  );
  return candidates.flatMap((candidate) => {
    const decision = decisionMap.get(candidate.id);
    validateDecision(decision, candidate);
    if (decision.decision === "reject") return [];
    return [
      {
        ...candidate,
        severity: decision.severity,
        rationale: decision.rationale,
        evidence: candidate.evidenceFactIds
          .map((id) => facts[id])
          .filter(Boolean),
        sources: candidate.sourceChangeIds
          .map((id) => {
            const source = sources[id];
            return source && candidate.hunkIds?.length
              ? sourceForUnit(source, candidate.hunkIds)
              : source;
          })
          .filter(Boolean),
      },
    ];
  });
}

function changedFields(before, after, fields) {
  const equal = (left, right) =>
    left === undefined || right === undefined
      ? left === right
      : canonicalJson(left) === canonicalJson(right);
  return fields.filter((field) => !equal(before?.[field], after?.[field]));
}

function operationPresentation(operation, facts) {
  const before = operation.beforeFactId
    ? facts[operation.beforeFactId]
    : undefined;
  const after = operation.afterFactId
    ? facts[operation.afterFactId]
    : undefined;
  const current = after ?? before;
  const changed = changedFields(before, after, [
    "method",
    "path",
    "parameters",
    "request",
    "responses",
    "paging",
    "lro",
  ]);
  return {
    ...operation,
    apiVersion: current?.apiVersion,
    method: current?.method,
    path: current?.path,
    restChanged: changed.length > 0,
    changedAspects: changed,
    before,
    after,
    outcome: changed.length
      ? `REST contract changed: ${changed.join(", ")}.`
      : "HTTP signature and represented payload contract unchanged.",
  };
}

function semanticAction(unit, operations) {
  if (
    operations.length &&
    operations.every(
      (operation) => !operation.beforeFactId && operation.afterFactId,
    )
  ) {
    return "add";
  }
  if (
    operations.length &&
    operations.every(
      (operation) => operation.beforeFactId && !operation.afterFactId,
    )
  ) {
    return "remove";
  }
  if (operations.length) return "modify";
  return unit.action ?? unit.changeKind;
}

function sourceForUnit(source, hunkIds) {
  const allowed = new Set(hunkIds);
  return {
    ...source,
    hunks: (source.hunks ?? []).filter((hunk) => allowed.has(hunk.id)),
    declarations: (source.declarations ?? []).filter((declaration) =>
      declaration.hunkIds?.some((id) => allowed.has(id)),
    ),
  };
}

function sourcesForOperation(operation, unit, sources, facts, projectsById) {
  let sourceChangeIds = operation.sourceChangeIds ?? [];
  let hunkIds = operation.hunkIds ?? [];
  const publication = (unit.groupingEvidence?.reasons ?? []).some(
    (reason) =>
      reason === "publication" || reason.includes("api-version-publication"),
  );
  if (!sourceChangeIds.length && publication) {
    const unitHunkIds = new Set(unit.hunkIds ?? []);
    const operationFact =
      facts[operation.afterFactId ?? operation.beforeFactId];
    const projectPath = projectsById.get(operationFact?.projectId)?.path;
    const inProject = (source) =>
      !projectPath ||
      source?.path === projectPath ||
      source?.path?.startsWith(`${projectPath}/`);
    const governance = (unit.sourceChangeIds ?? []).flatMap((sourceId) => {
      const source = sources[sourceId];
      if (!inProject(source)) return [];
      const versionHunkIds = (source?.declarations ?? [])
        .filter(
          (declaration) =>
            declaration.qualifiedName === "Versions" ||
            declaration.qualifiedName?.endsWith(".Versions"),
        )
        .flatMap((declaration) => declaration.hunkIds ?? [])
        .filter((hunkId) => unitHunkIds.has(hunkId));
      return versionHunkIds.length
        ? [{ sourceId, hunkIds: versionHunkIds }]
        : [];
    });
    sourceChangeIds = governance.map((item) => item.sourceId);
    hunkIds = [...new Set(governance.flatMap((item) => item.hunkIds))];
  }
  if (!sourceChangeIds.length && unit.sourceChangeIds?.length === 1) {
    sourceChangeIds = unit.sourceChangeIds;
    hunkIds = unit.hunkIds;
  }
  return sourceChangeIds
    .map((id) => sources[id] && sourceForUnit(sources[id], hunkIds))
    .filter(Boolean);
}

function methodFacts(finding) {
  const role = (fact) =>
    fact.comparisonRole ??
    (fact.revision === "base"
      ? "baseline"
      : fact.revision === "current"
        ? "target"
        : undefined);
  return {
    before: finding.evidence.find(
      (fact) => fact.factKind === "method" && role(fact) === "baseline",
    ),
    after: finding.evidence.find(
      (fact) => fact.factKind === "method" && role(fact) === "target",
    ),
  };
}

function methodDeltaValue(fact, field) {
  const value = fact?.[field];
  if (field === "responseType") {
    return typeIdentity(value) ?? "void";
  }
  if (field === "lro") {
    const semantic = semanticLroContract(value);
    return value
      ? {
          finalStateVia: semantic.finalStateVia,
          logicalResult: typeIdentity(semantic.logicalResult),
          pollingStep:
            semantic.pollingStep?.kind ??
            semantic.pollingStep?.responseBody?.kind,
          finalStep: semantic.finalStep?.kind,
          statusMonitorStep: semantic.statusMonitorStep?.kind,
        }
      : "none";
  }
  if (field === "paging") {
    return value
      ? { nextLinkName: value.nextLinkName, itemName: value.itemName }
      : "none";
  }
  return value ?? "none";
}

const METHOD_RULE_FIELDS = {
  "method-kind-changed": "kind",
  "method-location-changed": "client",
  "method-parameters-changed": "parameters",
  "method-response-changed": "responseType",
  "method-access-changed": "access",
  "method-paging-changed": "paging",
  "method-lro-changed": "lro",
};

function meaningfulDownstreamFinding(finding) {
  const field = METHOD_RULE_FIELDS[finding.rule];
  if (!field) return true;
  const { before, after } = methodFacts(finding);
  if (!before && !after) return true;
  if (field === "parameters") {
    return (
      canonicalJson(publicParameterContract(before?.parameters)) !==
      canonicalJson(publicParameterContract(after?.parameters))
    );
  }
  const beforeValue =
    field === "lro"
      ? semanticLroContract(before?.lro)
      : methodDeltaValue(before, field);
  const afterValue =
    field === "lro"
      ? semanticLroContract(after?.lro)
      : methodDeltaValue(after, field);
  return (
    canonicalJson(beforeValue ?? null) !== canonicalJson(afterValue ?? null)
  );
}

function semanticOperationIndex(semanticItems) {
  const operations = [];
  for (const intent of semanticItems) {
    for (const operation of intent.operations) {
      operations.push({ intent, operation });
    }
  }
  return operations;
}

function matchMethodOperation(finding, semanticOperations) {
  const { before, after } = methodFacts(finding);
  const protocol = (after ?? before)?.operation;
  if (!protocol?.path || !protocol?.verb) return undefined;
  const matches = semanticOperations.filter(
    ({ operation }) =>
      operation.path === protocol.path &&
      operation.method === protocol.verb &&
      (!operation.apiVersion ||
        !(after ?? before)?.apiVersions?.length ||
        (after ?? before).apiVersions.includes(operation.apiVersion)),
  );
  const direct = matches.filter(
    ({ operation }) => operation.matchBasis === "operation-identity",
  );
  if (direct.length === 1) return direct[0];
  return matches.length === 1 ? matches[0] : undefined;
}

export function matchTypeFindingIntents(finding, semanticItems) {
  const typeNames = new Set(
    [
      finding.crossLanguageDefinitionId?.split(".").at(-1),
      finding.symbol?.split(".").at(-1),
      ...(finding.evidence ?? []).map((fact) => fact.name),
    ].filter(Boolean),
  );
  if (!typeNames.size) return [];
  return semanticItems.filter((intent) => {
    const declarationIds = new Set(intent.declarationIds ?? []);
    return (intent.sources ?? []).some((source) =>
      (source.declarations ?? []).some((declaration) => {
        if (declarationIds.size && !declarationIds.has(declaration.id)) {
          return false;
        }
        const declarationSegments = declaration.qualifiedName?.split(".") ?? [];
        return declarationSegments.some((segment) => typeNames.has(segment));
      }),
    );
  });
}

function findingRelations(restFindings, downstreamFindings, semanticItems) {
  const semanticOperations = semanticOperationIndex(semanticItems);
  for (const finding of restFindings) {
    const matches = semanticOperations.filter(({ operation }) =>
      finding.operationIds.includes(operation.operationId),
    );
    finding.relatedSemanticIntents = [
      ...new Set(matches.map(({ intent }) => intent.id)),
    ];
    finding.semanticMatchBasis = finding.relatedSemanticIntents.length
      ? "operation-identity"
      : undefined;
  }
  for (const finding of downstreamFindings) {
    const match =
      methodFacts(finding).before || methodFacts(finding).after
        ? matchMethodOperation(finding, semanticOperations)
        : undefined;
    if (match) {
      finding.relatedSemanticIntents = [match.intent.id];
      finding.semanticMatchBasis = "http-method-path";
      continue;
    }
    const declarationMatches = matchTypeFindingIntents(finding, semanticItems);
    if (declarationMatches.length) {
      finding.relatedSemanticIntents = declarationMatches.map(
        (intent) => intent.id,
      );
      finding.semanticMatchBasis = "declaration-identity";
      continue;
    }
    const sourceIds = new Set(finding.sourceChangeIds);
    const sourceMatches = semanticItems.filter((intent) =>
      intent.sourceChangeIds.some((id) => sourceIds.has(id)),
    );
    finding.relatedSemanticIntents =
      sourceMatches.length === 1 ? [sourceMatches[0].id] : [];
    finding.semanticMatchBasis =
      sourceMatches.length === 1 ? "unique-source" : undefined;
  }
}

function downstreamGroups(
  downstreamFindings,
  restFindings,
  semanticItems,
  rootCauses = [],
) {
  const semanticOperations = semanticOperationIndex(semanticItems);
  const restBreakingOperations = new Set(
    restFindings.flatMap((finding) =>
      (finding.operationIds ?? []).map(
        (operationId) => `${finding.projectId ?? ""}:${operationId}`,
      ),
    ),
  );
  const byMethod = new Map();
  const typeFindings = [];
  for (const finding of downstreamFindings) {
    const facts = methodFacts(finding);
    if (!facts.before && !facts.after) {
      typeFindings.push(finding);
      continue;
    }
    const symbol = finding.crossLanguageDefinitionId ?? finding.symbol;
    const projectId = (facts.after ?? facts.before)?.projectId;
    const key = `${projectId ?? ""}:${symbol}`;
    const group = byMethod.get(key) ?? {
      id: stableId("downstream-group", { projectId, symbol }),
      projectId,
      symbol,
      before: facts.before,
      after: facts.after,
      findings: [],
    };
    group.before ??= facts.before;
    group.after ??= facts.after;
    group.findings.push(finding);
    byMethod.set(key, group);
  }
  const impliedByRest = [];
  const operationGroups = [...byMethod.values()]
    .map((group) => {
      const representative = group.findings[0];
      const match = matchMethodOperation(representative, semanticOperations);
      const operationId = match?.operation.operationId;
      const value = {
        ...group,
        operationId,
        method:
          match?.operation.method ??
          (group.after ?? group.before)?.operation?.verb,
        path:
          match?.operation.path ??
          (group.after ?? group.before)?.operation?.path,
        apiVersion: match?.operation.apiVersion,
        parametersUnchanged:
          canonicalJson(publicParameterContract(group.before?.parameters)) ===
          canonicalJson(publicParameterContract(group.after?.parameters)),
        deltas: group.findings.map((finding) => {
          const field = METHOD_RULE_FIELDS[finding.rule];
          const delta = {
            findingId: finding.id,
            rule: finding.rule,
            field,
            severity: finding.severity,
            actual: finding.actual,
            expected: finding.expected,
            rationale: finding.rationale,
          };
          if (field === "parameters") {
            delta.changes = diffPublicParameters(
              group.before?.parameters,
              group.after?.parameters,
            );
          } else if (field) {
            delta.before = methodDeltaValue(group.before, field);
            delta.after = methodDeltaValue(group.after, field);
          }
          return delta;
        }),
        rootCauseIds: [
          ...new Set(
            group.findings.flatMap((finding) => finding.rootCauseIds ?? []),
          ),
        ].sort(),
        relatedSemanticIntents: representative.relatedSemanticIntents ?? [],
      };
      if (
        operationId &&
        restBreakingOperations.has(`${group.projectId ?? ""}:${operationId}`)
      ) {
        impliedByRest.push(value);
      }
      return value;
    })
    .filter((group) => !impliedByRest.includes(group));
  operationGroups.sort((left, right) =>
    `${left.operationId ?? ""}:${left.symbol}`.localeCompare(
      `${right.operationId ?? ""}:${right.symbol}`,
    ),
  );
  const typeRootGroups = new Map();
  for (const finding of typeFindings) {
    const { before, after } = {
      before: finding.evidence.find(
        (fact) =>
          (fact.comparisonRole ??
            (fact.revision === "base" ? "baseline" : undefined)) === "baseline",
      ),
      after: finding.evidence.find(
        (fact) =>
          (fact.comparisonRole ??
            (fact.revision === "current" ? "target" : undefined)) === "target",
      ),
    };
    const changed = changedFields(before, after, [
      "access",
      "usage",
      "reachable",
    ]);
    const ids = finding.rootCauseIds?.length
      ? finding.rootCauseIds
      : [`unresolved:${finding.rule}:${changed.join(",") || "contract"}`];
    for (const rootCauseId of ids) {
      const rootCause = rootCauses.find((item) => item.id === rootCauseId);
      const group = typeRootGroups.get(rootCauseId) ?? {
        rootCauseId,
        rootCause: rootCause?.kind ?? rootCauseId,
        findings: [],
      };
      group.findings.push(finding);
      typeRootGroups.set(rootCauseId, group);
    }
  }
  const sharedTypeImpacts = [...typeRootGroups.values()]
    .map((group) => {
      const findingIds = group.findings.map((finding) => finding.id).sort();
      const projectIds = new Set(
        group.findings.flatMap((finding) =>
          finding.evidence.map((fact) => fact.projectId).filter(Boolean),
        ),
      );
      const affectedMethods = operationGroups
        .filter(
          (item) =>
            item.rootCauseIds?.includes(group.rootCauseId) ||
            (!group.rootCauseId.startsWith("downstream-root-cause-") &&
              (!projectIds.size || projectIds.has(item.projectId))),
        )
        .map((item) => ({
          operationId: item.operationId,
          symbol: item.symbol,
          method: item.method,
          path: item.path,
        }));
      const types = [
        ...new Set(
          group.findings.map(
            (finding) => finding.crossLanguageDefinitionId ?? finding.symbol,
          ),
        ),
      ].sort();
      const relatedSemanticIntents = [
        ...new Set([
          ...group.findings.flatMap(
            (finding) => finding.relatedSemanticIntents ?? [],
          ),
          ...affectedMethods.flatMap(
            (method) =>
              operationGroups.find((item) => item.symbol === method.symbol)
                ?.relatedSemanticIntents ?? [],
          ),
        ]),
      ].sort();
      return {
        id: stableId("shared-type-impact", {
          rootCauseId: group.rootCauseId,
          findingIds,
        }),
        rootCauseId: group.rootCauseId,
        rootCause: group.rootCause,
        summary:
          group.rootCause === "method-return-propagation"
            ? "These public types gained additional SDK usage because affected methods now return them."
            : "These SDK types share the same generated public-surface change.",
        findingIds,
        typeCount: types.length,
        types,
        sampleTypes: types.slice(0, 3),
        affectedOperationCount: new Set(
          affectedMethods.map((item) => item.operationId).filter(Boolean),
        ).size,
        affectedMethodCount: new Set(affectedMethods.map((item) => item.symbol))
          .size,
        affectedMethods,
        sampleMethods: affectedMethods.slice(0, 3),
        relatedSemanticIntents,
      };
    })
    .sort((left, right) => left.id.localeCompare(right.id));
  return { operationGroups, sharedTypeImpacts, impliedByRest };
}

function addReciprocalRelations(semanticItems, restFindings, downstream) {
  for (const intent of semanticItems) {
    intent.relatedFindings = {
      rest: restFindings
        .filter((finding) =>
          finding.relatedSemanticIntents?.includes(intent.id),
        )
        .map((finding) => finding.id)
        .sort(),
      downstream: downstream.operationGroups
        .filter((group) => group.relatedSemanticIntents.includes(intent.id))
        .map((group) => group.id)
        .sort(),
      sharedTypeImpact: downstream.sharedTypeImpacts
        .filter((group) => group.relatedSemanticIntents.includes(intent.id))
        .map((group) => group.id)
        .sort(),
    };
  }
}

export function assembleAssessment({ work, judgment }) {
  const manifest = readJson(path.join(work, "preparation-manifest.json"));
  const sourceIndex = readJson(path.join(work, "source", "source-index.json"));
  const semantic = readJson(
    path.join(work, "dimensions", "semantic-intents-input.json"),
  );
  const rest = readJson(
    path.join(work, "dimensions", "rest-breaking-input.json"),
  );
  const downstream = readJson(
    path.join(work, "dimensions", "downstream-breaking-input.json"),
  );
  const answer = typeof judgment === "string" ? readJson(judgment) : judgment;
  const modelInputPath = path.join(work, "model-input.json");
  const modelInput = fs.existsSync(modelInputPath)
    ? readJson(modelInputPath)
    : {};
  validateInferenceRequests(modelInput);
  const inferenceRequests = modelInput.inferenceRequests ?? [];
  const inferencePath = path.join(work, "inference.json");
  if (inferenceRequests.length && !fs.existsSync(inferencePath)) {
    throw new Error("Missing inference.json.");
  }
  if (!inferenceRequests.length && fs.existsSync(inferencePath)) {
    throw new Error("Unexpected inference.json without inference requests.");
  }
  const inference = inferenceRequests.length
    ? readJson(inferencePath)
    : undefined;
  if (inference) validateInference(inference, inferenceRequests, modelInput);
  const inferredCandidates = [
    ...new Map(
      (inference?.results ?? []).flatMap((result) =>
        result.candidates.map((candidate) => [
          candidate.id,
          {
            ...candidate,
            inferred: true,
            inferenceRequestIds: (inference.results ?? [])
              .filter((item) =>
                item.candidates.some((value) => value.id === candidate.id),
              )
              .map((item) => item.requestId)
              .sort(),
          },
        ]),
      ),
    ).values(),
  ];
  const restCandidates = [
    ...rest.candidates,
    ...inferredCandidates.filter((candidate) => candidate.dimension === "rest"),
  ];
  const downstreamCandidates = [
    ...downstream.candidates,
    ...inferredCandidates.filter(
      (candidate) => candidate.dimension === "downstream",
    ),
  ];
  const deterministicCandidateIds = new Set([
    ...rest.candidates.map((candidate) => candidate.id),
    ...downstream.candidates.map((candidate) => candidate.id),
  ]);
  const conflictingCandidateIds = inferredCandidates
    .map((candidate) => candidate.id)
    .filter((id) => deterministicCandidateIds.has(id));
  if (conflictingCandidateIds.length) {
    throw new Error(
      `Inferred candidate IDs conflict with deterministic candidates: ${conflictingCandidateIds.join(", ")}.`,
    );
  }
  const inferenceRequestsById = new Map(
    inferenceRequests.map((request) => [request.requestId, request]),
  );
  const inferenceBlockers = (inference?.results ?? [])
    .filter((result) => result.decision === "blocked")
    .map((result) => {
      const request = inferenceRequestsById.get(result.requestId);
      return {
        code: "inference-blocked",
        reviewUnitId: result.reviewUnitId,
        hunkId: result.hunkId,
        allowedDimensions: request.allowedDimensions,
        message: result.rationale,
      };
    });
  const complianceEvidencePath = path.join(
    work,
    "compliance-search-evidence.json",
  );
  const hasComplianceInput = Array.isArray(modelInput.complianceSearchRequests);
  if (hasComplianceInput && !fs.existsSync(complianceEvidencePath)) {
    throw new Error("Missing compliance-search-evidence.json.");
  }
  const complianceEvidence = hasComplianceInput
    ? readJson(complianceEvidencePath)
    : undefined;
  validateJudgment(answer);
  exactCoverage(
    semantic.reviewUnits.map((item) => item.id),
    answer.semanticIntents.map((item) => item.reviewUnitId),
    "semantic review unit",
  );
  exactCoverage(
    restCandidates.map((item) => item.id),
    answer.restDecisions.map((item) => item.candidateId),
    "REST candidate",
  );
  exactCoverage(
    downstreamCandidates.map((item) => item.id),
    answer.downstreamDecisions.map((item) => item.candidateId),
    "downstream candidate",
  );
  const sources = sourceMap(sourceIndex);
  const projectsById = new Map(
    (manifest.projects ?? []).map((project) => [project.id, project]),
  );
  const semanticUnits = new Map(
    semantic.reviewUnits.map((unit) => [unit.id, unit]),
  );
  const modelSemanticUnits = new Map(
    (modelInput.semanticReviewUnits ?? []).map((unit) => [
      unit.reviewUnitId,
      unit,
    ]),
  );
  const inferenceResultsByUnit = new Map();
  for (const result of inference?.results ?? []) {
    const values = inferenceResultsByUnit.get(result.reviewUnitId) ?? [];
    values.push(result);
    inferenceResultsByUnit.set(result.reviewUnitId, values);
  }
  const semanticItems = answer.semanticIntents.map((intent) => {
    const unit = semanticUnits.get(intent.reviewUnitId);
    const modelUnit = modelSemanticUnits.get(intent.reviewUnitId);
    const operations = (
      unit.operations ??
      unit.operationIds.map((id) => ({
        operationId: semantic.facts[id]?.operationId,
        beforeFactId: unit.beforeFactIds?.find(
          (factId) =>
            semantic.facts[factId]?.operationId ===
            semantic.facts[id]?.operationId,
        ),
        afterFactId:
          unit.afterFactIds?.find(
            (factId) =>
              semantic.facts[factId]?.operationId ===
              semantic.facts[id]?.operationId,
          ) ??
          ((semantic.facts[id]?.comparisonRole ??
            semantic.facts[id]?.revision) === "baseline" ||
          semantic.facts[id]?.revision === "base"
            ? undefined
            : id),
      }))
    ).map((operation) => ({
      ...operationPresentation(operation, semantic.facts),
      sources: sourcesForOperation(
        operation,
        unit,
        sources,
        semantic.facts,
        projectsById,
      ),
    }));
    const action = semanticAction(unit, operations);
    return {
      ...unit,
      action,
      changeKind: action,
      title: intent.title,
      summary: intent.summary,
      ...(modelUnit?.deterministicCoverage
        ? {
            deterministicCoverage: modelUnit.deterministicCoverage,
            inferenceRequired: modelUnit.inferenceRequired,
          }
        : {}),
      ...(inferenceResultsByUnit.has(intent.reviewUnitId)
        ? {
            inferenceResults: inferenceResultsByUnit
              .get(intent.reviewUnitId)
              .map((result) => ({
                requestId: result.requestId,
                hunkId: result.hunkId,
                decision: result.decision,
                rationale: result.rationale,
                candidateIds: result.candidates.map(
                  (candidate) => candidate.id,
                ),
              })),
          }
        : {}),
      operations,
      sources: unit.sourceChangeIds
        .map(
          (id) =>
            sources[id] &&
            sourceForUnit(
              sources[id],
              unit.hunkIds ?? sources[id].hunks.map((hunk) => hunk.id),
            ),
        )
        .filter(Boolean),
    };
  });
  const restFindings = joinFindings(
    restCandidates,
    answer.restDecisions,
    { ...modelInput.facts, ...rest.facts },
    sources,
  );
  const downstreamFindings = joinFindings(
    downstreamCandidates,
    answer.downstreamDecisions,
    { ...modelInput.facts, ...downstream.facts },
    sources,
  ).filter(meaningfulDownstreamFinding);
  findingRelations(restFindings, downstreamFindings, semanticItems);
  const downstreamAggregation = downstreamGroups(
    downstreamFindings,
    restFindings,
    semanticItems,
    downstream.rootCauses,
  );
  addReciprocalRelations(semanticItems, restFindings, downstreamAggregation);
  const restDimension = {
    status: dimensionStatus(
      rest.status === "blocked" ||
        inferenceBlockers.some((blocker) =>
          blocker.allowedDimensions.includes("rest"),
        ),
      restFindings,
    ),
    findings: restFindings,
    rejectedCandidateCount: answer.restDecisions.filter(
      (item) => item.decision === "reject",
    ).length,
    blockers: [
      ...rest.blockers,
      ...inferenceBlockers.filter((blocker) =>
        blocker.allowedDimensions.includes("rest"),
      ),
    ],
  };
  const downstreamDimension = {
    status: dimensionStatus(
      downstream.status === "blocked" ||
        inferenceBlockers.some((blocker) =>
          blocker.allowedDimensions.includes("downstream"),
        ),
      downstreamFindings,
    ),
    findings: downstreamFindings,
    operationGroups: downstreamAggregation.operationGroups,
    sharedTypeImpacts: downstreamAggregation.sharedTypeImpacts,
    rootCauses: downstream.rootCauses ?? [],
    impliedByRest: downstreamAggregation.impliedByRest,
    rejectedCandidateCount: answer.downstreamDecisions.filter(
      (item) => item.decision === "reject",
    ).length,
    blockers: [
      ...downstream.blockers,
      ...inferenceBlockers.filter((blocker) =>
        blocker.allowedDimensions.includes("downstream"),
      ),
    ],
  };
  const complianceDimension = hasComplianceInput
    ? assembleCompliance({
        requests: modelInput.complianceSearchRequests,
        evidence: complianceEvidence,
        decisions: answer.complianceDecisions,
        sourceChanges: sourceIndex.sourceChanges,
        initialBlockers:
          semantic.status === "blocked"
            ? semantic.blockers.length
              ? semantic.blockers
              : [
                  "semantic-analysis-blocked: Compliance requires Semantic intents.",
                ]
            : [],
      })
    : {
        status: "not-assessed",
        summary: "Compliance search input was not available.",
        coverage: {
          semanticIntentCount: 0,
          assessedIntentCount: 0,
          selectedDocumentCount: 0,
          unassessedIntentIds: [],
        },
        intentAssessments: [],
        findings: [],
        retrievalFailures: [],
        blockers: [
          {
            message:
              "compliance-search-input-missing: rerun deterministic analysis.",
          },
        ],
      };
  return {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    title: `TypeSpec assessment: ${manifest.projects.map((project) => project.path).join(", ")}`,
    repository: manifest.repository,
    ...(manifest.pullRequest ? { pullRequest: manifest.pullRequest } : {}),
    comparison: {
      baseRef: manifest.comparison.baseRef,
      baseCommit: manifest.comparison.mergeBaseCommit,
      headCommit: manifest.comparison.headCommit,
      workingTree: manifest.comparison.workingTree,
    },
    artifactComparisons: manifest.projects.map((project) => ({
      projectId: project.id,
      ...(project.artifactComparison ?? {
        mode: "legacy",
        baseline: {
          sourceRevision: "base",
          commit: manifest.comparison.mergeBaseCommit,
          apiVersion: project.apiVersions?.base,
          reason: project.apiVersions?.baseReason ?? "legacy-selection",
        },
        target: {
          sourceRevision: "current",
          commit: manifest.comparison.headCommit,
          apiVersion: project.apiVersions?.current,
          reason: project.apiVersions?.currentReason ?? "legacy-selection",
        },
      }),
    })),
    confidence: answer.overallConfidence,
    safety: deriveSafety(restDimension, downstreamDimension),
    dimensions: {
      semantic: {
        status: semantic.status === "blocked" ? "not-assessed" : "assessed",
        items: semanticItems,
        sourceHunkIds: sourceIndex.sourceChanges
          .flatMap((source) => (source.hunks ?? []).map((hunk) => hunk.id))
          .sort(),
        blockers: semantic.blockers,
      },
      rest: restDimension,
      downstream: downstreamDimension,
      compliance: complianceDimension,
      documentQuality: {
        status: "not-assessed",
        summary: "Document quality is not assessed.",
      },
    },
    changedFiles: manifest.changedFiles,
    projects: manifest.projects,
    blockers: [...manifest.blockers, ...answer.blockers, ...inferenceBlockers],
    provenance: {
      modelInput: "model-input.json",
      ...(inference ? { inference: "inference.json" } : {}),
      ...(hasComplianceInput
        ? { complianceSearchEvidence: "compliance-search-evidence.json" }
        : {}),
      judgment: "assessment-judgment.json",
      preparationManifest: "preparation-manifest.json",
    },
    inputAccounting: {
      ...modelInput.inputAccounting,
      ...(inference
        ? {
            inference: {
              requestCount: inferenceRequests.length,
              inferredCandidateCount: inferredCandidates.length,
              noImpactCount: inference.results.filter(
                (result) => result.decision === "no-impact",
              ).length,
              blockedCount: inference.results.filter(
                (result) => result.decision === "blocked",
              ).length,
            },
          }
        : {}),
      ...(hasComplianceInput
        ? { compliance: complianceEvidence.inputAccounting }
        : {}),
    },
    timings: manifest.timings,
  };
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const args = parseArgs(process.argv.slice(2), {
      required: ["work", "judgment", "output"],
    });
    const assessment = assembleAssessment({
      work: path.resolve(args.work),
      judgment: path.resolve(args.judgment),
    });
    writeJson(path.resolve(args.output), assessment);
    console.log(path.resolve(args.output));
  });
}
