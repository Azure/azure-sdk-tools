import path from "node:path";
import { isMain, readJson, runMain } from "./cli.mjs";
import { deriveSafety, dimensionStatus } from "./assessment-display.mjs";

function uniqueIds(items, pathName, errors) {
  const seen = new Set();
  for (const item of items) {
    if (!item?.id) errors.push(`${pathName} contains an item without id.`);
    else if (seen.has(item.id)) errors.push(`${pathName} contains duplicate id ${item.id}.`);
    else seen.add(item.id);
  }
}

function duplicateValues(values) {
  return [...new Set(values.filter((value, index) => values.indexOf(value) !== index))];
}

function validateFinding(finding, dimension, errors) {
  const prefix = `${dimension} finding ${finding.id ?? "<unknown>"}`;
  if (!finding.actual?.trim()) errors.push(`${prefix} is missing actual behavior.`);
  if (!finding.expected?.trim()) errors.push(`${prefix} is missing expected behavior.`);
  if (!["high", "medium", "low"].includes(finding.severity)) {
    errors.push(`${prefix} has invalid severity.`);
  }
  if (!finding.rationale?.trim()) errors.push(`${prefix} is missing rationale.`);
  if (!finding.sources?.length) errors.push(`${prefix} has no changed TypeSpec source.`);
  for (const source of finding.sources ?? []) {
    if (!source.hunks?.length) errors.push(`${prefix} links source ${source.id} without changed hunks.`);
  }
  if (dimension === "REST" && !finding.operationIds?.length) {
    errors.push(`${prefix} has no affected operation.`);
  }
  if (
    dimension === "downstream" &&
    !finding.crossLanguageDefinitionId &&
    !finding.symbol
  ) {
    errors.push(`${prefix} has no affected SDK symbol.`);
  }
  if (!finding.evidence?.length) errors.push(`${prefix} has no deterministic evidence.`);
}

function validateLegacy(assessment) {
  const errors = [];
  if (!assessment.baseline?.commit) errors.push("Legacy assessment is missing baseline.commit.");
  if (!assessment.head?.commit) errors.push("Legacy assessment is missing head.commit.");
  const dimensions = assessment.dimensions;
  if (!dimensions?.semanticUnderstanding?.items) {
    errors.push("Legacy assessment is missing semanticUnderstanding.items.");
  }
  if (!dimensions?.restBreakingChanges?.findings) {
    errors.push("Legacy assessment is missing restBreakingChanges.findings.");
  }
  if (!dimensions?.restCompatibleDownstreamBreakingChanges?.findings) {
    errors.push("Legacy assessment is missing downstream findings.");
  }
  if (!dimensions?.azureCompliance?.findings) {
    errors.push("Legacy assessment is missing azureCompliance.findings.");
  }
  return errors;
}

export function validateAssessment(assessment) {
  if (assessment?.schemaVersion !== 1) return validateLegacy(assessment);
  const errors = [];
  if (!assessment.comparison?.baseCommit) errors.push("comparison.baseCommit is required.");
  if (!assessment.comparison?.headCommit) errors.push("comparison.headCommit is required.");
  const projectIds = new Set((assessment.projects ?? []).map((project) => project.id));
  if ((assessment.artifactComparisons ?? []).length !== projectIds.size) {
    errors.push("artifactComparisons must contain exactly one entry per project.");
  }
  uniqueIds(
    (assessment.artifactComparisons ?? []).map((item) => ({ ...item, id: item.projectId })),
    "artifact comparisons",
    errors,
  );
  for (const comparison of assessment.artifactComparisons ?? []) {
    if (!projectIds.has(comparison.projectId)) {
      errors.push(`Artifact comparison references unknown project ${comparison.projectId}.`);
    }
    if (!["new-api-version", "existing-api-version", "unversioned", "legacy"].includes(comparison.mode)) {
      errors.push(`Artifact comparison ${comparison.projectId} has invalid mode.`);
    }
    for (const role of ["baseline", "target"]) {
      const selection = comparison[role];
      if (!selection || !["base", "current"].includes(selection.sourceRevision)) {
        errors.push(`Artifact comparison ${comparison.projectId} has invalid ${role} source revision.`);
      }
      if (!selection?.commit) {
        errors.push(`Artifact comparison ${comparison.projectId} is missing ${role} commit.`);
      }
      if (!selection?.reason) {
        errors.push(`Artifact comparison ${comparison.projectId} is missing ${role} selection reason.`);
      }
    }
    if (comparison.mode === "new-api-version" &&
        (comparison.baseline?.sourceRevision !== "current" ||
          comparison.target?.sourceRevision !== "current" ||
          comparison.baseline?.commit !== assessment.comparison.headCommit ||
          comparison.target?.commit !== assessment.comparison.headCommit)) {
      errors.push(
        `New-version artifact comparison ${comparison.projectId} must compile both roles from the head source.`,
      );
    }
  }
  if (!["high", "medium", "low"].includes(assessment.confidence)) {
    errors.push("confidence is invalid.");
  }
  const dimensions = assessment.dimensions ?? {};
  for (const name of ["semantic", "rest", "downstream", "compliance", "documentQuality"]) {
    if (!dimensions[name]) errors.push(`dimensions.${name} is required.`);
  }
  if (dimensions.compliance?.status !== "planned") {
    errors.push("Compliance must be planned in the MVP.");
  }
  if (dimensions.documentQuality?.status !== "planned") {
    errors.push("Document Quality must be planned in the MVP.");
  }
  uniqueIds(dimensions.semantic?.items ?? [], "semantic items", errors);
  uniqueIds(dimensions.rest?.findings ?? [], "REST findings", errors);
  uniqueIds(dimensions.downstream?.findings ?? [], "downstream findings", errors);
  for (const finding of dimensions.rest?.findings ?? []) validateFinding(finding, "REST", errors);
  for (const finding of dimensions.downstream?.findings ?? []) {
    validateFinding(finding, "downstream", errors);
  }
  if (dimensions.semantic?.status === "assessed") {
    const coveredHunks = [];
    for (const item of dimensions.semantic.items ?? []) {
      if (!item.title?.trim() || !item.summary?.trim()) {
        errors.push(`Semantic item ${item.id} is incomplete.`);
      }
      if (!["add", "remove", "modify"].includes(item.action)) {
        errors.push(`Semantic item ${item.id} has invalid action.`);
      }
      if (!item.sources?.length) errors.push(`Semantic item ${item.id} has no changed source.`);
      for (const source of item.sources ?? []) {
        for (const hunk of source.hunks ?? []) coveredHunks.push(hunk.id);
      }
      for (const operation of item.operations ?? []) {
        if (!operation.operationId || !operation.method || !operation.path) {
          errors.push(`Semantic item ${item.id} has incomplete REST operation evidence.`);
        }
        if (typeof operation.restChanged !== "boolean" || !operation.outcome?.trim()) {
          errors.push(`Semantic item ${item.id} has incomplete REST change outcome.`);
        }
      }
    }
    const duplicateHunks = coveredHunks.filter((id, index) => coveredHunks.indexOf(id) !== index);
    if (duplicateHunks.length) {
      errors.push(`Semantic source hunks are covered more than once: ${[...new Set(duplicateHunks)].join(", ")}.`);
    }
    const expectedHunks = dimensions.semantic.sourceHunkIds ?? [];
    const missingHunks = expectedHunks.filter((id) => !coveredHunks.includes(id));
    const unknownHunks = coveredHunks.filter((id) => !expectedHunks.includes(id));
    if (missingHunks.length || unknownHunks.length) {
      errors.push(`Semantic source hunk coverage mismatch. Missing: ${missingHunks.join(", ") || "none"}; unknown: ${unknownHunks.join(", ") || "none"}.`);
    }
  }
  const semanticIds = new Set((dimensions.semantic?.items ?? []).map((item) => item.id));
  const restIds = new Set((dimensions.rest?.findings ?? []).map((item) => item.id));
  const downstreamGroupIds = new Set((dimensions.downstream?.operationGroups ?? []).map((item) => item.id));
  uniqueIds(dimensions.downstream?.operationGroups ?? [], "downstream operation groups", errors);
  uniqueIds(dimensions.downstream?.sharedTypeImpacts ?? [], "shared type impacts", errors);
  for (const impact of dimensions.downstream?.sharedTypeImpacts ?? []) {
    downstreamGroupIds.add(impact.id);
  }
  for (const finding of [
    ...(dimensions.rest?.findings ?? []),
    ...(dimensions.downstream?.findings ?? []),
  ]) {
    for (const id of finding.relatedSemanticIntents ?? []) {
      if (!semanticIds.has(id)) errors.push(`Finding ${finding.id} links unknown semantic intent ${id}.`);
    }
    if (finding.semanticMatchBasis &&
        !["operation-identity", "http-method-path", "unique-source"].includes(finding.semanticMatchBasis)) {
      errors.push(`Finding ${finding.id} has unsupported semantic match basis.`);
    }
  }
  for (const item of dimensions.semantic?.items ?? []) {
    for (const [kind, ids] of Object.entries(item.relatedFindings ?? {})) {
      const duplicates = duplicateValues(ids);
      if (duplicates.length) {
        errors.push(`Semantic item ${item.id} has duplicate ${kind} links: ${duplicates.join(", ")}.`);
      }
    }
    for (const id of item.relatedFindings?.rest ?? []) {
      if (!restIds.has(id)) errors.push(`Semantic item ${item.id} links unknown REST finding ${id}.`);
      const finding = (dimensions.rest?.findings ?? []).find((candidate) => candidate.id === id);
      if (finding && !finding.relatedSemanticIntents?.includes(item.id)) {
        errors.push(`Semantic item ${item.id} and REST finding ${id} are not reciprocal.`);
      }
    }
    for (const id of [
      ...(item.relatedFindings?.downstream ?? []),
      ...(item.relatedFindings?.sharedTypeImpact ?? []),
    ]) {
      if (!downstreamGroupIds.has(id)) {
        errors.push(`Semantic item ${item.id} links unknown downstream group ${id}.`);
      }
    }
  }
  const downstreamFindingIds = new Set((dimensions.downstream?.findings ?? []).map((item) => item.id));
  const aggregatedFindingIds = [];
  for (const group of dimensions.downstream?.operationGroups ?? []) {
    if (!group.symbol || !group.deltas?.length) errors.push(`Downstream group ${group.id} is incomplete.`);
    for (const delta of group.deltas ?? []) {
      aggregatedFindingIds.push(delta.findingId);
      if (!downstreamFindingIds.has(delta.findingId)) {
        errors.push(`Downstream group ${group.id} links unknown finding ${delta.findingId}.`);
      }
    }
    for (const id of group.relatedSemanticIntents ?? []) {
      const intent = (dimensions.semantic?.items ?? []).find((item) => item.id === id);
      if (!intent?.relatedFindings?.downstream?.includes(group.id)) {
        errors.push(`Downstream group ${group.id} and semantic item ${id} are not reciprocal.`);
      }
    }
  }
  for (const impact of dimensions.downstream?.sharedTypeImpacts ?? []) {
    if (!impact.summary?.trim() || !impact.typeCount || !impact.types?.length) {
      errors.push(`Shared type impact ${impact.id} is incomplete.`);
    }
    if (impact.typeCount !== new Set(impact.types ?? []).size) {
      errors.push(`Shared type impact ${impact.id} has inconsistent type count.`);
    }
    if (impact.affectedMethodCount !== new Set(
      (impact.affectedMethods ?? []).map((item) => item.symbol),
    ).size) {
      errors.push(`Shared type impact ${impact.id} has inconsistent method count.`);
    }
    if (impact.affectedOperationCount !== new Set(
      (impact.affectedMethods ?? []).map((item) => item.operationId).filter(Boolean),
    ).size) {
      errors.push(`Shared type impact ${impact.id} has inconsistent operation count.`);
    }
    for (const id of impact.findingIds ?? []) {
      aggregatedFindingIds.push(id);
      if (!downstreamFindingIds.has(id)) {
        errors.push(`Shared type impact ${impact.id} links unknown finding ${id}.`);
      }
    }
    for (const id of impact.relatedSemanticIntents ?? []) {
      const intent = (dimensions.semantic?.items ?? []).find((item) => item.id === id);
      if (!intent?.relatedFindings?.sharedTypeImpact?.includes(impact.id)) {
        errors.push(`Shared type impact ${impact.id} and semantic item ${id} are not reciprocal.`);
      }
    }
  }
  for (const group of dimensions.downstream?.impliedByRest ?? []) {
    for (const delta of group.deltas ?? []) aggregatedFindingIds.push(delta.findingId);
  }
  const duplicateAggregates = duplicateValues(aggregatedFindingIds);
  const missingAggregates = [...downstreamFindingIds].filter((id) => !aggregatedFindingIds.includes(id));
  if (duplicateAggregates.length || missingAggregates.length) {
    errors.push(`Downstream aggregate coverage mismatch. Missing: ${missingAggregates.join(", ") || "none"}; duplicate: ${duplicateAggregates.join(", ") || "none"}.`);
  }
  const expectedRestStatus = dimensionStatus(
    dimensions.rest?.status === "not-assessed",
    dimensions.rest?.findings ?? [],
  );
  if (dimensions.rest?.status !== expectedRestStatus) {
    errors.push(`REST status must be ${expectedRestStatus}.`);
  }
  const expectedDownstreamStatus = dimensionStatus(
    dimensions.downstream?.status === "not-assessed",
    dimensions.downstream?.findings ?? [],
  );
  if (dimensions.downstream?.status !== expectedDownstreamStatus) {
    errors.push(`Downstream status must be ${expectedDownstreamStatus}.`);
  }
  const expectedSafety = deriveSafety(dimensions.rest, dimensions.downstream);
  if (
    assessment.safety?.scope !== expectedSafety.scope ||
    assessment.safety?.status !== expectedSafety.status
  ) {
    errors.push("Scoped safety is inconsistent with REST/downstream dimensions.");
  }
  return errors;
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const file = process.argv[2];
    if (!file) throw new Error("Usage: validate-assessment.mjs <assessment.json>");
    const errors = validateAssessment(readJson(path.resolve(file)));
    if (errors.length) throw new Error(errors.join("\n"));
    console.log(`${path.resolve(file)} is valid.`);
  });
}
