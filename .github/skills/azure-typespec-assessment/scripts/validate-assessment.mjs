import path from "node:path";
import { isMain, readJson, runMain } from "./cli.mjs";
import { deriveSafety, dimensionStatus } from "./assessment-display.mjs";
import { readComplianceCatalog } from "./compliance-assessment.mjs";

function uniqueIds(items, pathName, errors) {
  const seen = new Set();
  for (const item of items) {
    if (!item?.id) errors.push(`${pathName} contains an item without id.`);
    else if (seen.has(item.id))
      errors.push(`${pathName} contains duplicate id ${item.id}.`);
    else seen.add(item.id);
  }
}

function duplicateValues(values) {
  return [
    ...new Set(
      values.filter((value, index) => values.indexOf(value) !== index),
    ),
  ];
}

function validateFinding(finding, dimension, errors) {
  const prefix = `${dimension} finding ${finding.id ?? "<unknown>"}`;
  if (!finding.actual?.trim())
    errors.push(`${prefix} is missing actual behavior.`);
  if (!finding.expected?.trim())
    errors.push(`${prefix} is missing expected behavior.`);
  if (!["high", "medium", "low"].includes(finding.severity)) {
    errors.push(`${prefix} has invalid severity.`);
  }
  if (!finding.rationale?.trim())
    errors.push(`${prefix} is missing rationale.`);
  if (!finding.sources?.length)
    errors.push(`${prefix} has no changed TypeSpec source.`);
  for (const source of finding.sources ?? []) {
    if (!source.hunks?.length)
      errors.push(`${prefix} links source ${source.id} without changed hunks.`);
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
  if (!finding.evidence?.length && !finding.inferred) {
    errors.push(`${prefix} has no deterministic evidence.`);
  }
  if (
    finding.inferred &&
    (!finding.inferenceRequestIds?.length || !finding.hunkIds?.length)
  ) {
    errors.push(`${prefix} has incomplete inference provenance.`);
  }
}

function validateLegacy(assessment) {
  const errors = [];
  if (!assessment.baseline?.commit)
    errors.push("Legacy assessment is missing baseline.commit.");
  if (!assessment.head?.commit)
    errors.push("Legacy assessment is missing head.commit.");
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

function validateComplianceDimension(compliance, semanticItems, errors) {
  if (!["passed", "failed", "not-assessed"].includes(compliance?.status)) {
    errors.push("Compliance status is invalid.");
    return;
  }
  for (const field of [
    "intentAssessments",
    "findings",
    "retrievalFailures",
    "blockers",
  ]) {
    if (!Array.isArray(compliance[field]))
      errors.push(`Compliance ${field} must be an array.`);
  }
  if (!compliance.coverage || typeof compliance.coverage !== "object") {
    errors.push("Compliance coverage is required.");
    return;
  }
  const catalog = readComplianceCatalog();
  const catalogUrls = new Set(catalog.map((item) => item.canonicalUrl));
  const catalogByUrl = new Map(
    catalog.map((item) => [item.canonicalUrl, item]),
  );
  const semanticMap = new Map(semanticItems.map((item) => [item.id, item]));
  const assessments = compliance.intentAssessments ?? [];
  const assessmentIds = assessments.map((item) => item.semanticIntentId);
  if (duplicateValues(assessmentIds).length) {
    errors.push("Compliance contains duplicate intent assessments.");
  }
  const migrationBlocked = (compliance.blockers ?? []).some((item) =>
    String(item?.message ?? item).startsWith(
      "compliance-search-input-missing:",
    ),
  );
  const semanticIds = semanticItems.map((item) => item.id);
  if (
    !migrationBlocked &&
    (semanticIds.some((id) => !assessmentIds.includes(id)) ||
      assessmentIds.some((id) => !semanticIds.includes(id)))
  ) {
    errors.push("Compliance intent coverage does not match Semantic intents.");
  }

  let selectedDocumentCount = 0;
  const assessedIntentIds = [];
  const failedIntentIds = [];
  let incompleteEvidence = (compliance.blockers ?? []).length > 0;
  for (const item of assessments) {
    if (!semanticMap.has(item.semanticIntentId)) {
      errors.push(
        `Compliance references unknown semantic intent ${item.semanticIntentId}.`,
      );
      continue;
    }
    const documents = item.documents ?? [];
    const ranking = item.catalogRanking ?? [];
    selectedDocumentCount += documents.length;
    const hasExhaustion = (item.blockers ?? []).some((value) =>
      value.startsWith("catalog-exhausted:"),
    );
    if (documents.length !== 4 && !hasExhaustion) {
      errors.push(
        `Compliance intent ${item.semanticIntentId} requires four documents.`,
      );
      incompleteEvidence = true;
    }
    if (
      ranking.length !== catalog.length ||
      duplicateValues(ranking.map((entry) => entry.canonicalUrl)).length
    ) {
      errors.push(
        `Compliance intent ${item.semanticIntentId} has incomplete catalog ranking.`,
      );
    }
    const sortedRanking = [...ranking].sort(
      (left, right) =>
        right.score.total - left.score.total ||
        left.catalogOrder - right.catalogOrder,
    );
    if (
      ranking.some((entry, index) => {
        const catalogEntry = catalogByUrl.get(entry.canonicalUrl);
        const score = entry.score ?? {};
        return (
          entry.rank !== index + 1 ||
          !catalogEntry ||
          entry.catalogOrder !== catalogEntry.catalogOrder ||
          entry.title !== catalogEntry.title ||
          ![0, 4].includes(score.exactSymbol) ||
          ![0, 3].includes(score.patternCategory) ||
          ![0, 2].includes(score.servicePlane) ||
          ![0, 1].includes(score.changeContext) ||
          score.total !==
            score.exactSymbol +
              score.patternCategory +
              score.servicePlane +
              score.changeContext
        );
      }) ||
      JSON.stringify(sortedRanking.map((entry) => entry.canonicalUrl)) !==
        JSON.stringify(ranking.map((entry) => entry.canonicalUrl))
    ) {
      errors.push(
        `Compliance intent ${item.semanticIntentId} has invalid catalog ranking.`,
      );
    }
    const failedUrls = new Set(
      (compliance.retrievalFailures ?? [])
        .filter((failure) => failure.reviewUnitId === item.semanticIntentId)
        .map((failure) => failure.canonicalUrl),
    );
    const expectedUrls = ranking
      .filter((entry) => !failedUrls.has(entry.canonicalUrl))
      .slice(0, 4)
      .map((entry) => entry.canonicalUrl);
    const urls = documents.map((document) => document.canonicalUrl);
    if (
      duplicateValues(urls).length ||
      JSON.stringify(expectedUrls) !== JSON.stringify(urls)
    ) {
      errors.push(
        `Compliance intent ${item.semanticIntentId} selected invalid documents.`,
      );
    }
    for (const document of documents) {
      if (
        !catalogUrls.has(document.canonicalUrl) ||
        !document.retrievedAt ||
        !/^sha256:[0-9a-f]{64}$/i.test(document.contentHash ?? "") ||
        typeof document.noRelevantGuidance !== "boolean" ||
        document.noRelevantGuidance === (document.guidance ?? []).length > 0
      ) {
        errors.push(`Compliance document ${document.canonicalUrl} is invalid.`);
      }
      for (const guidance of document.guidance ?? []) {
        if (
          !guidance.section?.trim() ||
          !guidance.excerpt?.trim() ||
          !Array.isArray(guidance.queryTerms) ||
          !Array.isArray(guidance.applicableDeclarationIds) ||
          !guidance.applicableDeclarationIds.length ||
          guidance.applicableDeclarationIds.some(
            (id) => !(item.declarationIds ?? []).includes(id),
          )
        ) {
          errors.push(
            `Compliance document ${document.canonicalUrl} has incomplete guidance.`,
          );
        }
      }
    }
    if (
      ![
        "applicable-pass",
        "applicable-fail",
        "no-applicable-guidance",
        "not-assessed",
      ].includes(item.decision) ||
      !item.actual?.trim() ||
      !item.gap?.trim() ||
      !Array.isArray(item.applicableGuidance)
    ) {
      errors.push(
        `Compliance intent ${item.semanticIntentId} has an invalid decision.`,
      );
      incompleteEvidence = true;
      continue;
    }
    if (
      item.decision === "applicable-pass" ||
      item.decision === "applicable-fail"
    ) {
      assessedIntentIds.push(item.semanticIntentId);
      if (
        !item.expected?.trim() ||
        !item.applicableGuidance.length ||
        !(item.sourceLinks ?? []).length ||
        !(item.codeSnippets ?? []).length
      ) {
        errors.push(
          `Compliance intent ${item.semanticIntentId} lacks applicable evidence.`,
        );
      }
      for (const applicable of item.applicableGuidance) {
        const document = documents.find(
          (candidate) =>
            candidate.canonicalUrl === applicable.canonicalDocumentUrl,
        );
        if (
          !document?.guidance?.some(
            (guidance) => guidance.section === applicable.guidanceSection,
          )
        ) {
          errors.push(
            `Compliance intent ${item.semanticIntentId} uses unknown guidance.`,
          );
        }
      }
    } else if (item.decision === "no-applicable-guidance") {
      assessedIntentIds.push(item.semanticIntentId);
      if (item.applicableGuidance.length || (item.blockers ?? []).length) {
        errors.push(
          `Compliance intent ${item.semanticIntentId} cannot use no-applicable-guidance with applicable guidance or blockers.`,
        );
      }
    } else {
      incompleteEvidence = true;
    }
    if (item.decision === "applicable-fail") {
      failedIntentIds.push(item.semanticIntentId);
      if (
        !item.title?.trim() ||
        !["high", "medium", "low"].includes(item.severity)
      ) {
        errors.push(
          `Compliance intent ${item.semanticIntentId} lacks finding presentation.`,
        );
      }
    }
    if ((item.blockers ?? []).length) incompleteEvidence = true;
  }

  for (const failure of compliance.retrievalFailures ?? []) {
    if (
      !semanticMap.has(failure.reviewUnitId) ||
      !catalogUrls.has(failure.canonicalUrl) ||
      failure.status !== "failed" ||
      !failure.error?.trim()
    ) {
      errors.push("Compliance contains an invalid retrieval failure.");
    }
  }
  const findingIntentIds = (compliance.findings ?? []).map(
    (item) => item.semanticIntentId,
  );
  if (
    duplicateValues(findingIntentIds).length ||
    failedIntentIds.some((id) => !findingIntentIds.includes(id)) ||
    findingIntentIds.some((id) => !failedIntentIds.includes(id))
  ) {
    errors.push(
      "Compliance findings must exactly match failed intent assessments.",
    );
  }
  for (const finding of compliance.findings ?? []) {
    if (
      !finding.expected?.trim() ||
      !finding.actual?.trim() ||
      !finding.gap?.trim() ||
      !finding.title?.trim() ||
      !["high", "medium", "low"].includes(finding.severity) ||
      !Array.isArray(finding.applicableGuidance) ||
      !finding.applicableGuidance.length ||
      !(finding.sourceLinks ?? []).length ||
      !(finding.codeSnippets ?? []).length
    ) {
      errors.push(
        `Compliance finding ${finding.id ?? "<unknown>"} is incomplete.`,
      );
    }
  }
  uniqueIds(compliance.findings ?? [], "Compliance findings", errors);
  const coverageSemanticIds = migrationBlocked ? [] : semanticIds;
  const unassessedIntentIds = coverageSemanticIds.filter(
    (id) => !assessedIntentIds.includes(id),
  );
  const coverage = compliance.coverage;
  if (
    coverage.semanticIntentCount !== coverageSemanticIds.length ||
    coverage.assessedIntentCount !== assessedIntentIds.length ||
    coverage.selectedDocumentCount !== selectedDocumentCount ||
    JSON.stringify([...(coverage.unassessedIntentIds ?? [])].sort()) !==
      JSON.stringify(unassessedIntentIds.sort())
  ) {
    errors.push("Compliance coverage counts are inconsistent.");
  }
  const expectedStatus = failedIntentIds.length
    ? "failed"
    : incompleteEvidence || unassessedIntentIds.length
      ? "not-assessed"
      : "passed";
  if (compliance.status !== expectedStatus) {
    errors.push(`Compliance status must be ${expectedStatus}.`);
  }
}
export function validateAssessment(assessment) {
  if (assessment?.schemaVersion !== 1) return validateLegacy(assessment);
  const errors = [];
  if (!assessment.comparison?.baseCommit)
    errors.push("comparison.baseCommit is required.");
  if (!assessment.comparison?.headCommit)
    errors.push("comparison.headCommit is required.");
  if (assessment.pullRequest) {
    if (
      !Number.isInteger(assessment.pullRequest.number) ||
      assessment.pullRequest.number < 1
    ) {
      errors.push("pullRequest.number must be a positive integer.");
    }
    try {
      const url = new URL(assessment.pullRequest.url);
      if (!["http:", "https:"].includes(url.protocol)) {
        errors.push("pullRequest.url must use HTTP or HTTPS.");
      }
    } catch {
      errors.push("pullRequest.url must be a valid URL.");
    }
  }
  const projectIds = new Set(
    (assessment.projects ?? []).map((project) => project.id),
  );
  if ((assessment.artifactComparisons ?? []).length !== projectIds.size) {
    errors.push(
      "artifactComparisons must contain exactly one entry per project.",
    );
  }
  uniqueIds(
    (assessment.artifactComparisons ?? []).map((item) => ({
      ...item,
      id: item.projectId,
    })),
    "artifact comparisons",
    errors,
  );
  for (const comparison of assessment.artifactComparisons ?? []) {
    if (!projectIds.has(comparison.projectId)) {
      errors.push(
        `Artifact comparison references unknown project ${comparison.projectId}.`,
      );
    }
    if (
      ![
        "new-api-version",
        "existing-api-version",
        "unversioned",
        "legacy",
      ].includes(comparison.mode)
    ) {
      errors.push(
        `Artifact comparison ${comparison.projectId} has invalid mode.`,
      );
    }
    for (const role of ["baseline", "target"]) {
      const selection = comparison[role];
      if (
        !selection ||
        !["base", "current"].includes(selection.sourceRevision)
      ) {
        errors.push(
          `Artifact comparison ${comparison.projectId} has invalid ${role} source revision.`,
        );
      }
      if (!selection?.commit) {
        errors.push(
          `Artifact comparison ${comparison.projectId} is missing ${role} commit.`,
        );
      }
      if (!selection?.reason) {
        errors.push(
          `Artifact comparison ${comparison.projectId} is missing ${role} selection reason.`,
        );
      }
    }
    if (
      comparison.mode === "new-api-version" &&
      (comparison.baseline?.sourceRevision !== "current" ||
        comparison.target?.sourceRevision !== "current" ||
        comparison.baseline?.commit !== assessment.comparison.headCommit ||
        comparison.target?.commit !== assessment.comparison.headCommit)
    ) {
      errors.push(
        `New-version artifact comparison ${comparison.projectId} must compile both roles from the head source.`,
      );
    }
  }
  if (!["high", "medium", "low"].includes(assessment.confidence)) {
    errors.push("confidence is invalid.");
  }
  const dimensions = assessment.dimensions ?? {};
  for (const name of [
    "semantic",
    "rest",
    "downstream",
    "compliance",
    "documentQuality",
  ]) {
    if (!dimensions[name]) errors.push(`dimensions.${name} is required.`);
  }
  if (dimensions.documentQuality?.status !== "not-assessed") {
    errors.push("Document Quality must remain not-assessed.");
  }
  uniqueIds(dimensions.semantic?.items ?? [], "semantic items", errors);
  uniqueIds(dimensions.rest?.findings ?? [], "REST findings", errors);
  uniqueIds(
    dimensions.downstream?.findings ?? [],
    "downstream findings",
    errors,
  );
  for (const finding of dimensions.rest?.findings ?? [])
    validateFinding(finding, "REST", errors);
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
      if (!item.sources?.length)
        errors.push(`Semantic item ${item.id} has no changed source.`);
      for (const source of item.sources ?? []) {
        for (const hunk of source.hunks ?? []) coveredHunks.push(hunk.id);
      }
      for (const operation of item.operations ?? []) {
        if (!operation.operationId || !operation.method || !operation.path) {
          errors.push(
            `Semantic item ${item.id} has incomplete REST operation evidence.`,
          );
        }
        if (
          typeof operation.restChanged !== "boolean" ||
          !operation.outcome?.trim()
        ) {
          errors.push(
            `Semantic item ${item.id} has incomplete REST change outcome.`,
          );
        }
      }
    }
    const duplicateHunks = coveredHunks.filter(
      (id, index) => coveredHunks.indexOf(id) !== index,
    );
    if (duplicateHunks.length) {
      errors.push(
        `Semantic source hunks are covered more than once: ${[...new Set(duplicateHunks)].join(", ")}.`,
      );
    }
    const expectedHunks = dimensions.semantic.sourceHunkIds ?? [];
    const missingHunks = expectedHunks.filter(
      (id) => !coveredHunks.includes(id),
    );
    const unknownHunks = coveredHunks.filter(
      (id) => !expectedHunks.includes(id),
    );
    if (missingHunks.length || unknownHunks.length) {
      errors.push(
        `Semantic source hunk coverage mismatch. Missing: ${missingHunks.join(", ") || "none"}; unknown: ${unknownHunks.join(", ") || "none"}.`,
      );
    }
  }
  const semanticIds = new Set(
    (dimensions.semantic?.items ?? []).map((item) => item.id),
  );
  const restIds = new Set(
    (dimensions.rest?.findings ?? []).map((item) => item.id),
  );
  const downstreamGroupIds = new Set(
    (dimensions.downstream?.operationGroups ?? []).map((item) => item.id),
  );
  uniqueIds(
    dimensions.downstream?.operationGroups ?? [],
    "downstream operation groups",
    errors,
  );
  uniqueIds(
    dimensions.downstream?.sharedTypeImpacts ?? [],
    "shared type impacts",
    errors,
  );
  validateComplianceDimension(
    dimensions.compliance,
    dimensions.semantic?.items ?? [],
    errors,
  );
  for (const impact of dimensions.downstream?.sharedTypeImpacts ?? []) {
    downstreamGroupIds.add(impact.id);
  }
  for (const finding of [
    ...(dimensions.rest?.findings ?? []),
    ...(dimensions.downstream?.findings ?? []),
  ]) {
    for (const id of finding.relatedSemanticIntents ?? []) {
      if (!semanticIds.has(id))
        errors.push(
          `Finding ${finding.id} links unknown semantic intent ${id}.`,
        );
    }
    if (
      finding.semanticMatchBasis &&
      ![
        "operation-identity",
        "http-method-path",
        "declaration-identity",
        "unique-source",
      ].includes(finding.semanticMatchBasis)
    ) {
      errors.push(
        `Finding ${finding.id} has unsupported semantic match basis.`,
      );
    }
  }
  for (const item of dimensions.semantic?.items ?? []) {
    for (const [kind, ids] of Object.entries(item.relatedFindings ?? {})) {
      const duplicates = duplicateValues(ids);
      if (duplicates.length) {
        errors.push(
          `Semantic item ${item.id} has duplicate ${kind} links: ${duplicates.join(", ")}.`,
        );
      }
    }
    for (const id of item.relatedFindings?.rest ?? []) {
      if (!restIds.has(id))
        errors.push(
          `Semantic item ${item.id} links unknown REST finding ${id}.`,
        );
      const finding = (dimensions.rest?.findings ?? []).find(
        (candidate) => candidate.id === id,
      );
      if (finding && !finding.relatedSemanticIntents?.includes(item.id)) {
        errors.push(
          `Semantic item ${item.id} and REST finding ${id} are not reciprocal.`,
        );
      }
    }
    for (const id of [
      ...(item.relatedFindings?.downstream ?? []),
      ...(item.relatedFindings?.sharedTypeImpact ?? []),
    ]) {
      if (!downstreamGroupIds.has(id)) {
        errors.push(
          `Semantic item ${item.id} links unknown downstream group ${id}.`,
        );
      }
    }
  }
  const downstreamFindingIds = new Set(
    (dimensions.downstream?.findings ?? []).map((item) => item.id),
  );
  const aggregatedFindingIds = [];
  for (const group of dimensions.downstream?.operationGroups ?? []) {
    if (!group.symbol || !group.deltas?.length)
      errors.push(`Downstream group ${group.id} is incomplete.`);
    for (const delta of group.deltas ?? []) {
      aggregatedFindingIds.push(delta.findingId);
      if (!downstreamFindingIds.has(delta.findingId)) {
        errors.push(
          `Downstream group ${group.id} links unknown finding ${delta.findingId}.`,
        );
      }
    }
    for (const id of group.relatedSemanticIntents ?? []) {
      const intent = (dimensions.semantic?.items ?? []).find(
        (item) => item.id === id,
      );
      if (!intent?.relatedFindings?.downstream?.includes(group.id)) {
        errors.push(
          `Downstream group ${group.id} and semantic item ${id} are not reciprocal.`,
        );
      }
    }
  }
  for (const impact of dimensions.downstream?.sharedTypeImpacts ?? []) {
    if (!impact.summary?.trim() || !impact.typeCount || !impact.types?.length) {
      errors.push(`Shared type impact ${impact.id} is incomplete.`);
    }
    if (impact.typeCount !== new Set(impact.types ?? []).size) {
      errors.push(
        `Shared type impact ${impact.id} has inconsistent type count.`,
      );
    }
    if (
      impact.affectedMethodCount !==
      new Set((impact.affectedMethods ?? []).map((item) => item.symbol)).size
    ) {
      errors.push(
        `Shared type impact ${impact.id} has inconsistent method count.`,
      );
    }
    if (
      impact.affectedOperationCount !==
      new Set(
        (impact.affectedMethods ?? [])
          .map((item) => item.operationId)
          .filter(Boolean),
      ).size
    ) {
      errors.push(
        `Shared type impact ${impact.id} has inconsistent operation count.`,
      );
    }
    for (const id of impact.findingIds ?? []) {
      aggregatedFindingIds.push(id);
      if (!downstreamFindingIds.has(id)) {
        errors.push(
          `Shared type impact ${impact.id} links unknown finding ${id}.`,
        );
      }
    }
    for (const id of impact.relatedSemanticIntents ?? []) {
      const intent = (dimensions.semantic?.items ?? []).find(
        (item) => item.id === id,
      );
      if (!intent?.relatedFindings?.sharedTypeImpact?.includes(impact.id)) {
        errors.push(
          `Shared type impact ${impact.id} and semantic item ${id} are not reciprocal.`,
        );
      }
    }
  }
  for (const group of dimensions.downstream?.impliedByRest ?? []) {
    for (const delta of group.deltas ?? [])
      aggregatedFindingIds.push(delta.findingId);
  }
  const duplicateAggregates = duplicateValues(aggregatedFindingIds);
  const missingAggregates = [...downstreamFindingIds].filter(
    (id) => !aggregatedFindingIds.includes(id),
  );
  if (duplicateAggregates.length || missingAggregates.length) {
    errors.push(
      `Downstream aggregate coverage mismatch. Missing: ${missingAggregates.join(", ") || "none"}; duplicate: ${duplicateAggregates.join(", ") || "none"}.`,
    );
  }
  const expectedRestStatus = dimensionStatus(
    (dimensions.rest?.blockers ?? []).length > 0,
    dimensions.rest?.findings ?? [],
  );
  if (dimensions.rest?.status !== expectedRestStatus) {
    errors.push(`REST status must be ${expectedRestStatus}.`);
  }
  const expectedDownstreamStatus = dimensionStatus(
    (dimensions.downstream?.blockers ?? []).length > 0,
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
    errors.push(
      "Scoped safety is inconsistent with REST/downstream dimensions.",
    );
  }
  return errors;
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const file = process.argv[2];
    if (!file)
      throw new Error("Usage: validate-assessment.mjs <assessment.json>");
    const errors = validateAssessment(readJson(path.resolve(file)));
    if (errors.length) throw new Error(errors.join("\n"));
    console.log(`${path.resolve(file)} is valid.`);
  });
}
