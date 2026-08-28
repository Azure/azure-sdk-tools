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

function duplicates(values) {
  const seen = new Set();
  return values.filter((value) => (seen.has(value) ? true : (seen.add(value), false)));
}

function exactCoverage(expected, actual, label) {
  const duplicate = duplicates(actual);
  if (duplicate.length) throw new Error(`Duplicate ${label} IDs: ${[...new Set(duplicate)].join(", ")}`);
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
  if (!decision.rationale?.trim()) throw new Error(`Missing rationale for ${candidate.id}.`);
  if (decision.decision === "approve" && !["high", "medium", "low"].includes(decision.severity)) {
    throw new Error(`Approved candidate ${candidate.id} requires severity.`);
  }
  if (decision.decision === "reject" && decision.severity !== undefined) {
    throw new Error(`Rejected candidate ${candidate.id} must omit severity.`);
  }
}

function assertKeys(value, allowed, label) {
  const unknown = Object.keys(value).filter((key) => !allowed.includes(key));
  if (unknown.length) throw new Error(`${label} contains unknown fields: ${unknown.join(", ")}.`);
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
      "overallConfidence",
      "blockers",
    ],
    "Judgment",
  );
  if (answer.schemaVersion !== 1) throw new Error("Unsupported judgment schemaVersion.");
  for (const field of ["semanticIntents", "restDecisions", "downstreamDecisions", "blockers"]) {
    if (!Array.isArray(answer[field])) throw new Error(`Judgment.${field} must be an array.`);
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
      ["reviewUnitId", "title", "summary", "sourceChangeIds", "operationIds"],
      `Semantic intent ${intent.reviewUnitId ?? "<unknown>"}`,
    );
    if (!intent.title?.trim() || !intent.summary?.trim()) {
      throw new Error(`Semantic intent ${intent.reviewUnitId ?? "<unknown>"} is incomplete.`);
    }
    if (!Array.isArray(intent.sourceChangeIds) || !Array.isArray(intent.operationIds)) {
      throw new Error(`Semantic intent ${intent.reviewUnitId ?? "<unknown>"} has invalid ID arrays.`);
    }
  }
  for (const decision of [...answer.restDecisions, ...answer.downstreamDecisions]) {
    assertKeys(
      decision,
      ["candidateId", "decision", "severity", "rationale"],
      `Decision ${decision.candidateId ?? "<unknown>"}`,
    );
  }
}

function sourceMap(sourceIndex) {
  return Object.fromEntries(sourceIndex.sourceChanges.map((source) => [source.id, source]));
}

function joinFindings(candidates, decisions, facts, sources) {
  const decisionMap = new Map(decisions.map((decision) => [decision.candidateId, decision]));
  return candidates.flatMap((candidate) => {
    const decision = decisionMap.get(candidate.id);
    validateDecision(decision, candidate);
    if (decision.decision === "reject") return [];
    return [{
      ...candidate,
      severity: decision.severity,
      rationale: decision.rationale,
      evidence: candidate.evidenceFactIds.map((id) => facts[id]).filter(Boolean),
      sources: candidate.sourceChangeIds.map((id) => sources[id]).filter(Boolean),
    }];
  });
}

function changedFields(before, after, fields) {
  const equal = (left, right) => left === undefined || right === undefined
    ? left === right
    : canonicalJson(left) === canonicalJson(right);
  return fields.filter((field) => !equal(before?.[field], after?.[field]));
}

function operationPresentation(operation, facts) {
  const before = operation.beforeFactId ? facts[operation.beforeFactId] : undefined;
  const after = operation.afterFactId ? facts[operation.afterFactId] : undefined;
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
  if (operations.length &&
      operations.every((operation) => !operation.beforeFactId && operation.afterFactId)) {
    return "add";
  }
  if (operations.length &&
      operations.every((operation) => operation.beforeFactId && !operation.afterFactId)) {
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
  const publication = (unit.groupingEvidence?.reasons ?? []).some((reason) =>
    reason === "publication" || reason.includes("api-version-publication"));
  if (!sourceChangeIds.length && publication) {
    const unitHunkIds = new Set(unit.hunkIds ?? []);
    const operationFact = facts[operation.afterFactId ?? operation.beforeFactId];
    const projectPath = projectsById.get(operationFact?.projectId)?.path;
    const inProject = (source) => !projectPath ||
      source?.path === projectPath ||
      source?.path?.startsWith(`${projectPath}/`);
    const governance = (unit.sourceChangeIds ?? []).flatMap((sourceId) => {
      const source = sources[sourceId];
      if (!inProject(source)) return [];
      const versionHunkIds = (source?.declarations ?? [])
        .filter((declaration) =>
          declaration.qualifiedName === "Versions" ||
          declaration.qualifiedName?.endsWith(".Versions"))
        .flatMap((declaration) => declaration.hunkIds ?? [])
        .filter((hunkId) => unitHunkIds.has(hunkId));
      return versionHunkIds.length ? [{ sourceId, hunkIds: versionHunkIds }] : [];
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
  const role = (fact) => fact.comparisonRole ??
    (fact.revision === "base" ? "baseline" : fact.revision === "current" ? "target" : undefined);
  return {
    before: finding.evidence.find((fact) =>
      fact.factKind === "method" && role(fact) === "baseline",
    ),
    after: finding.evidence.find((fact) =>
      fact.factKind === "method" && role(fact) === "target",
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
          pollingStep: semantic.pollingStep?.kind ??
            semantic.pollingStep?.responseBody?.kind,
          finalStep: semantic.finalStep?.kind,
          statusMonitorStep: semantic.statusMonitorStep?.kind,
        }
      : "none";
  }
  if (field === "paging") {
    return value ? { nextLinkName: value.nextLinkName, itemName: value.itemName } : "none";
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
    return canonicalJson(publicParameterContract(before?.parameters)) !==
      canonicalJson(publicParameterContract(after?.parameters));
  }
  const beforeValue = field === "lro"
    ? semanticLroContract(before?.lro)
    : methodDeltaValue(before, field);
  const afterValue = field === "lro"
    ? semanticLroContract(after?.lro)
    : methodDeltaValue(after, field);
  return canonicalJson(beforeValue ?? null) !== canonicalJson(afterValue ?? null);
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
  const matches = semanticOperations.filter(({ operation }) =>
    operation.path === protocol.path &&
    operation.method === protocol.verb &&
    (!operation.apiVersion ||
      !(after ?? before)?.apiVersions?.length ||
      (after ?? before).apiVersions.includes(operation.apiVersion)),
  );
  const direct = matches.filter(({ operation }) => operation.matchBasis === "operation-identity");
  if (direct.length === 1) return direct[0];
  return matches.length === 1 ? matches[0] : undefined;
}

function findingRelations(restFindings, downstreamFindings, semanticItems) {
  const semanticOperations = semanticOperationIndex(semanticItems);
  for (const finding of restFindings) {
    const matches = semanticOperations.filter(({ operation }) =>
      finding.operationIds.includes(operation.operationId),
    );
    finding.relatedSemanticIntents = [...new Set(matches.map(({ intent }) => intent.id))];
    finding.semanticMatchBasis = finding.relatedSemanticIntents.length ? "operation-identity" : undefined;
  }
  for (const finding of downstreamFindings) {
    const match = methodFacts(finding).before || methodFacts(finding).after
      ? matchMethodOperation(finding, semanticOperations)
      : undefined;
    if (match) {
      finding.relatedSemanticIntents = [match.intent.id];
      finding.semanticMatchBasis = "http-method-path";
      continue;
    }
    const sourceIds = new Set(finding.sourceChangeIds);
    const sourceMatches = semanticItems.filter((intent) =>
      intent.sourceChangeIds.some((id) => sourceIds.has(id)),
    );
    finding.relatedSemanticIntents = sourceMatches.length === 1 ? [sourceMatches[0].id] : [];
    finding.semanticMatchBasis = sourceMatches.length === 1 ? "unique-source" : undefined;
  }
}

function downstreamGroups(downstreamFindings, restFindings, semanticItems, rootCauses = []) {
  const semanticOperations = semanticOperationIndex(semanticItems);
  const restBreakingOperations = new Set(restFindings.flatMap((finding) =>
    (finding.operationIds ?? []).map((operationId) => `${finding.projectId ?? ""}:${operationId}`),
  ));
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
  const operationGroups = [...byMethod.values()].map((group) => {
    const representative = group.findings[0];
    const match = matchMethodOperation(representative, semanticOperations);
    const operationId = match?.operation.operationId;
    const value = {
      ...group,
      operationId,
      method: match?.operation.method ?? (group.after ?? group.before)?.operation?.verb,
      path: match?.operation.path ?? (group.after ?? group.before)?.operation?.path,
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
      rootCauseIds: [...new Set(group.findings.flatMap((finding) => finding.rootCauseIds ?? []))].sort(),
      relatedSemanticIntents: representative.relatedSemanticIntents ?? [],
    };
    if (operationId && restBreakingOperations.has(`${group.projectId ?? ""}:${operationId}`)) {
      impliedByRest.push(value);
    }
    return value;
  }).filter((group) => !impliedByRest.includes(group));
  operationGroups.sort((left, right) =>
    `${left.operationId ?? ""}:${left.symbol}`.localeCompare(`${right.operationId ?? ""}:${right.symbol}`),
  );
  const typeRootGroups = new Map();
  for (const finding of typeFindings) {
    const { before, after } = {
      before: finding.evidence.find((fact) =>
        (fact.comparisonRole ?? (fact.revision === "base" ? "baseline" : undefined)) === "baseline"),
      after: finding.evidence.find((fact) =>
        (fact.comparisonRole ?? (fact.revision === "current" ? "target" : undefined)) === "target"),
    };
    const changed = changedFields(before, after, ["access", "usage", "reachable"]);
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
  const sharedTypeImpacts = [...typeRootGroups.values()].map((group) => {
    const findingIds = group.findings.map((finding) => finding.id).sort();
    const projectIds = new Set(group.findings.flatMap((finding) =>
      finding.evidence.map((fact) => fact.projectId).filter(Boolean),
    ));
    const affectedMethods = operationGroups
      .filter((item) =>
        item.rootCauseIds?.includes(group.rootCauseId) ||
        (!group.rootCauseId.startsWith("downstream-root-cause-") &&
          (!projectIds.size || projectIds.has(item.projectId))))
      .map((item) => ({
        operationId: item.operationId,
        symbol: item.symbol,
        method: item.method,
        path: item.path,
      }));
    const types = [...new Set(group.findings.map(
      (finding) => finding.crossLanguageDefinitionId ?? finding.symbol,
    ))].sort();
    const relatedSemanticIntents = [...new Set([
      ...group.findings.flatMap((finding) => finding.relatedSemanticIntents ?? []),
      ...affectedMethods.flatMap((method) =>
        operationGroups.find((item) => item.symbol === method.symbol)?.relatedSemanticIntents ?? [],
      ),
    ])].sort();
    return {
      id: stableId("shared-type-impact", { rootCauseId: group.rootCauseId, findingIds }),
      rootCauseId: group.rootCauseId,
      rootCause: group.rootCause,
      summary: group.rootCause === "method-return-propagation"
        ? "These public types gained additional SDK usage because affected methods now return them."
        : "These SDK types share the same generated public-surface change.",
      findingIds,
      typeCount: types.length,
      types,
      sampleTypes: types.slice(0, 3),
      affectedOperationCount: new Set(affectedMethods.map((item) => item.operationId).filter(Boolean)).size,
      affectedMethodCount: new Set(affectedMethods.map((item) => item.symbol)).size,
      affectedMethods,
      sampleMethods: affectedMethods.slice(0, 3),
      relatedSemanticIntents,
    };
  }).sort((left, right) => left.id.localeCompare(right.id));
  return { operationGroups, sharedTypeImpacts, impliedByRest };
}

function addReciprocalRelations(semanticItems, restFindings, downstream) {
  for (const intent of semanticItems) {
    intent.relatedFindings = {
      rest: restFindings
        .filter((finding) => finding.relatedSemanticIntents?.includes(intent.id))
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
  const semantic = readJson(path.join(work, "dimensions", "semantic-intents-input.json"));
  const rest = readJson(path.join(work, "dimensions", "rest-breaking-input.json"));
  const downstream = readJson(path.join(work, "dimensions", "downstream-breaking-input.json"));
  const answer = typeof judgment === "string" ? readJson(judgment) : judgment;
  const modelInputPath = path.join(work, "model-input.json");
  const modelInput = fs.existsSync(modelInputPath) ? readJson(modelInputPath) : {};
  validateJudgment(answer);
  exactCoverage(
    semantic.reviewUnits.map((item) => item.id),
    answer.semanticIntents.map((item) => item.reviewUnitId),
    "semantic review unit",
  );
  exactCoverage(
    rest.candidates.map((item) => item.id),
    answer.restDecisions.map((item) => item.candidateId),
    "REST candidate",
  );
  exactCoverage(
    downstream.candidates.map((item) => item.id),
    answer.downstreamDecisions.map((item) => item.candidateId),
    "downstream candidate",
  );
  const sources = sourceMap(sourceIndex);
  const projectsById = new Map((manifest.projects ?? []).map((project) => [project.id, project]));
  const semanticUnits = new Map(semantic.reviewUnits.map((unit) => [unit.id, unit]));
  const semanticItems = answer.semanticIntents.map((intent) => {
    const unit = semanticUnits.get(intent.reviewUnitId);
    const allowedSources = new Set(unit.sourceChangeIds);
    const allowedOperations = new Set(unit.operationIds);
    if (intent.sourceChangeIds.some((id) => !allowedSources.has(id))) {
      throw new Error(`Semantic result ${unit.id} invented a source ID.`);
    }
    if (intent.operationIds.some((id) => !allowedOperations.has(id))) {
      throw new Error(`Semantic result ${unit.id} invented an operation ID.`);
    }
    const operations = (unit.operations ?? unit.operationIds.map((id) => ({
      operationId: semantic.facts[id]?.operationId,
      beforeFactId: unit.beforeFactIds?.find((factId) =>
        semantic.facts[factId]?.operationId === semantic.facts[id]?.operationId),
      afterFactId: unit.afterFactIds?.find((factId) =>
        semantic.facts[factId]?.operationId === semantic.facts[id]?.operationId) ??
        ((semantic.facts[id]?.comparisonRole ?? semantic.facts[id]?.revision) === "baseline" ||
          semantic.facts[id]?.revision === "base" ? undefined : id),
    })))
      .filter((operation) =>
        intent.operationIds.includes(operation.afterFactId ?? operation.beforeFactId))
      .map((operation) => ({
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
      operations,
      sources: unit.sourceChangeIds
        .map((id) => sources[id] && sourceForUnit(sources[id], unit.hunkIds ?? sources[id].hunks.map((hunk) => hunk.id)))
        .filter(Boolean),
    };
  });
  const restFindings = joinFindings(
    rest.candidates,
    answer.restDecisions,
    rest.facts,
    sources,
  );
  const downstreamFindings = joinFindings(
    downstream.candidates,
    answer.downstreamDecisions,
    downstream.facts,
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
    status: dimensionStatus(rest.status === "blocked", restFindings),
    findings: restFindings,
    rejectedCandidateCount: answer.restDecisions.filter((item) => item.decision === "reject").length,
    blockers: rest.blockers,
  };
  const downstreamDimension = {
    status: dimensionStatus(downstream.status === "blocked", downstreamFindings),
    findings: downstreamFindings,
    operationGroups: downstreamAggregation.operationGroups,
    sharedTypeImpacts: downstreamAggregation.sharedTypeImpacts,
    rootCauses: downstream.rootCauses ?? [],
    impliedByRest: downstreamAggregation.impliedByRest,
    rejectedCandidateCount: answer.downstreamDecisions.filter((item) => item.decision === "reject")
      .length,
    blockers: downstream.blockers,
  };
  return {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    title: `TypeSpec assessment: ${manifest.projects.map((project) => project.path).join(", ")}`,
    repository: manifest.repository,
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
        sourceHunkIds: sourceIndex.sourceChanges.flatMap((source) =>
          (source.hunks ?? []).map((hunk) => hunk.id),
        ).sort(),
        blockers: semantic.blockers,
      },
      rest: restDimension,
      downstream: downstreamDimension,
      compliance: { status: "planned", summary: "Deferred from the MVP." },
      documentQuality: {
        status: "planned",
        summary: "Planned by the design document.",
      },
    },
    changedFiles: manifest.changedFiles,
    projects: manifest.projects,
    blockers: [...manifest.blockers, ...answer.blockers],
    provenance: {
      modelInput: "model-input.json",
      judgment: "assessment-judgment.json",
      preparationManifest: "preparation-manifest.json",
    },
    inputAccounting: modelInput.inputAccounting,
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
