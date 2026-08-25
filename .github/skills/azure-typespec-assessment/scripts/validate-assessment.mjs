#!/usr/bin/env node

import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const INTERNAL_GENERATOR_TERMS =
  /\bTCGC\b|cross-language definition IDs?|\bisUnionAsEnum\b|\bisFixed\b/i;

function assert(condition, message, errors) {
  if (!condition) errors.push(message);
}

function validateSourceReferences(items, label, errors) {
  for (const [index, item] of items.entries()) {
    const references = item.sourceReferences;
    assert(
      Array.isArray(references) && references.length > 0,
      `${label}[${index}] requires sourceReferences`,
      errors,
    );
    for (const [referenceIndex, reference] of (references ?? []).entries()) {
      assert(
        typeof reference.path === "string" && reference.path.endsWith(".tsp"),
        `${label}[${index}].sourceReferences[${referenceIndex}].path must be a .tsp file`,
        errors,
      );
      assert(
        Number.isInteger(reference.startLine) && reference.startLine > 0,
        `${label}[${index}].sourceReferences[${referenceIndex}].startLine is invalid`,
        errors,
      );
      assert(
        Number.isInteger(reference.endLine) &&
          reference.endLine >= reference.startLine,
        `${label}[${index}].sourceReferences[${referenceIndex}].endLine is invalid`,
        errors,
      );
      assert(
        typeof reference.link === "string" && reference.link.includes("#L"),
        `${label}[${index}].sourceReferences[${referenceIndex}].link is invalid`,
        errors,
      );
    }
  }
}

function validateOperations(
  items,
  impactFindingIds,
  linkedImpactFindingIds,
  errors,
) {
  for (const [itemIndex, item] of items.entries()) {
    const itemLabel = `semanticUnderstanding.items[${itemIndex}]`;
    const semanticNarrative = [
      item.intent,
      ...(item.transformationChain ?? []),
      item.restRepresentation?.summary,
      ...(item.changes ?? []).flatMap((change) => [
        change.summary,
        change.effect,
        change.typeSpecCause,
        ...(change.aspects ?? []).flatMap((aspect) => [
          aspect.field,
          aspect.before,
          aspect.after,
        ]),
      ]),
    ]
      .filter((value) => typeof value === "string")
      .join(" ");
    assert(
      !INTERNAL_GENERATOR_TERMS.test(semanticNarrative),
      `${itemLabel} must explain TypeSpec and REST behavior without internal generator terminology`,
      errors,
    );
    assert(
      typeof item.id === "string" && item.id.length > 0,
      `${itemLabel}.id is required`,
      errors,
    );
    assert(
      typeof item.intent === "string" && item.intent.length > 0,
      `${itemLabel}.intent is required`,
      errors,
    );
    assert(
      Array.isArray(item.transformationChain) &&
        item.transformationChain.every(
          (step) => typeof step === "string" && step.length > 0,
        ),
      `${itemLabel}.transformationChain must be an array of non-empty strings`,
      errors,
    );
    assert(
      ["high", "medium", "low"].includes(item.confidence),
      `${itemLabel}.confidence is invalid`,
      errors,
    );
    const restRepresentation = item.restRepresentation;
    assert(
      restRepresentation && typeof restRepresentation === "object",
      `${itemLabel}.restRepresentation is required`,
      errors,
    );
    assert(
      typeof restRepresentation?.summary === "string" &&
        restRepresentation.summary.length > 0,
      `${itemLabel}.restRepresentation.summary is required`,
      errors,
    );
    const operations = restRepresentation?.operations;
    assert(
      Array.isArray(operations) && operations.length > 0,
      `${itemLabel}.restRepresentation requires operations`,
      errors,
    );
    for (const [operationIndex, operation] of (operations ?? []).entries()) {
      const label = `semanticUnderstanding.items[${itemIndex}].operations[${operationIndex}]`;
      assert(
        typeof operation.operationId === "string" &&
          operation.operationId.length > 0,
        `${label}.operationId is required`,
        errors,
      );
      assert(
        Array.isArray(operation.apiVersions) &&
          operation.apiVersions.length > 0,
        `${label}.apiVersions is required`,
        errors,
      );
      assert(
        typeof operation.method === "string" &&
          /^(GET|PUT|POST|PATCH|DELETE|HEAD|OPTIONS|TRACE)$/.test(
            operation.method,
          ),
        `${label}.method is invalid`,
        errors,
      );
      assert(
        typeof operation.path === "string" && operation.path.startsWith("/"),
        `${label}.path is invalid`,
        errors,
      );
      assert(
        operation.signature === `${operation.method} ${operation.path}`,
        `${label}.signature must match method and path`,
        errors,
      );
      assert(
        Array.isArray(operation.parameters),
        `${label}.parameters must be an array`,
        errors,
      );
      assert(
        typeof operation.requestPayload === "string" &&
          operation.requestPayload.length > 0,
        `${label}.requestPayload is required`,
        errors,
      );
      assert(
        Array.isArray(operation.responsePayloads) &&
          operation.responsePayloads.length > 0,
        `${label}.responsePayloads is required`,
        errors,
      );
      assert(
        typeof operation.serviceBehavior === "string" &&
          operation.serviceBehavior.length > 0,
        `${label}.serviceBehavior is required`,
        errors,
      );
      assert(
        typeof operation.lro?.isLongRunning === "boolean",
        `${label}.lro.isLongRunning is required`,
        errors,
      );
      if (operation.lro?.isLongRunning) {
        for (const field of [
          "pattern",
          "finalStateVia",
          "polling",
          "finalResult",
        ]) {
          assert(
            typeof operation.lro[field] === "string" &&
              operation.lro[field].length > 0,
            `${label}.lro.${field} is required for an LRO`,
            errors,
          );
        }
      }
      assert(
        typeof operation.paging?.isPaged === "boolean",
        `${label}.paging.isPaged is required`,
        errors,
      );
      if (operation.paging?.isPaged) {
        for (const field of ["itemType", "nextLinkName", "continuation"]) {
          assert(
            typeof operation.paging[field] === "string" &&
              operation.paging[field].length > 0,
            `${label}.paging.${field} is required for paging`,
            errors,
          );
        }
      }
      validateSourceReferences([operation], label, errors);
    }
    const operationIds = new Set(
      (operations ?? []).map((operation) => operation.operationId),
    );
    const coveredOperationIds = [];
    assert(
      Array.isArray(item.changes) && item.changes.length > 0,
      `${itemLabel}.changes must be a non-empty array`,
      errors,
    );
    for (const [changeIndex, change] of (item.changes ?? []).entries()) {
      const label = `${itemLabel}.changes[${changeIndex}]`;
      assert(
        ["added", "modified", "removed"].includes(change.kind),
        `${label}.kind is invalid`,
        errors,
      );
      for (const field of ["summary", "effect", "typeSpecCause"]) {
        assert(
          typeof change[field] === "string" && change[field].length > 0,
          `${label}.${field} is required`,
          errors,
        );
      }
      assert(
        Array.isArray(change.operationIds) && change.operationIds.length > 0,
        `${label}.operationIds must be a non-empty array`,
        errors,
      );
      assert(
        new Set(change.operationIds ?? []).size ===
          (change.operationIds ?? []).length,
        `${label}.operationIds must be unique`,
        errors,
      );
      for (const operationId of change.operationIds ?? []) {
        assert(
          operationIds.has(operationId),
          `${label}.operationIds contains unknown operation ${operationId}`,
          errors,
        );
        coveredOperationIds.push(operationId);
      }
      assert(
        Array.isArray(change.apiVersions) && change.apiVersions.length > 0,
        `${label}.apiVersions must be a non-empty array`,
        errors,
      );
      const coveredVersions = new Set(
        (operations ?? [])
          .filter((operation) =>
            (change.operationIds ?? []).includes(operation.operationId),
          )
          .flatMap((operation) => operation.apiVersions),
      );
      for (const apiVersion of change.apiVersions ?? []) {
        assert(
          coveredVersions.has(apiVersion),
          `${label}.apiVersions contains unknown version ${apiVersion}`,
          errors,
        );
      }
      assert(
        Array.isArray(change.aspects) && change.aspects.length > 0,
        `${label}.aspects must be a non-empty array`,
        errors,
      );
      for (const [aspectIndex, aspect] of (change.aspects ?? []).entries()) {
        const aspectLabel = `${label}.aspects[${aspectIndex}]`;
        assert(
          typeof aspect.field === "string" && aspect.field.length > 0,
          `${aspectLabel}.field is required`,
          errors,
        );
        assert(
          aspect.before === null ||
            (typeof aspect.before === "string" && aspect.before.length > 0),
          `${aspectLabel}.before must be null or a non-empty string`,
          errors,
        );
        assert(
          aspect.after === null ||
            (typeof aspect.after === "string" && aspect.after.length > 0),
          `${aspectLabel}.after must be null or a non-empty string`,
          errors,
        );
        if (change.kind === "added") {
          assert(
            aspect.before === null && typeof aspect.after === "string",
            `${aspectLabel} must contain only after for an added change`,
            errors,
          );
        } else if (change.kind === "removed") {
          assert(
            typeof aspect.before === "string" && aspect.after === null,
            `${aspectLabel} must contain only before for removed`,
            errors,
          );
        } else {
          assert(
            typeof aspect.before === "string" ||
              typeof aspect.after === "string",
            `${aspectLabel} must contain before or after for modified`,
            errors,
          );
        }
      }
      assert(
        Array.isArray(change.typeSpecDiffs) && change.typeSpecDiffs.length > 0,
        `${label}.typeSpecDiffs must be a non-empty array`,
        errors,
      );
      for (const [hunkIndex, hunk] of (change.typeSpecDiffs ?? []).entries()) {
        const hunkLabel = `${label}.typeSpecDiffs[${hunkIndex}]`;
        assert(
          typeof hunk.path === "string" && hunk.path.endsWith(".tsp"),
          `${hunkLabel}.path must be a .tsp file`,
          errors,
        );
        for (const field of ["oldStart", "oldCount", "newStart", "newCount"]) {
          assert(
            Number.isInteger(hunk[field]) && hunk[field] >= 0,
            `${hunkLabel}.${field} is invalid`,
            errors,
          );
        }
        if (change.kind === "added") {
          const hasAddedDeclaration = (change.typeSpecDiffs ?? []).some(
            (hunk) =>
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
          const hasAddedVersionLineage =
            /version-lineage/.test(item.id) &&
            (change.typeSpecDiffs ?? []).some((candidateHunk) =>
              candidateHunk.lines.some((line) =>
                /^\+\s*v\d{4}_\d{2}_\d{2}/.test(line),
              ),
            );
          assert(
            hasAddedDeclaration || hasAddedVersionLineage,
            `${label} must show @added with its added operation or model declaration`,
            errors,
          );
        }
        assert(
          Array.isArray(hunk.lines) &&
            hunk.lines.length > 0 &&
            hunk.lines.every(
              (line) =>
                typeof line === "string" &&
                (/^[ +\-]/.test(line) ||
                  line === "\\ No newline at end of file"),
            ) &&
            hunk.lines.some((line) => /^[+-]/.test(line)),
          `${hunkLabel}.lines must contain Git diff lines and at least one change`,
          errors,
        );
      }
      assert(
        Array.isArray(change.linkedFindingIds),
        `${label}.linkedFindingIds must be an array`,
        errors,
      );
      assert(
        new Set(change.linkedFindingIds ?? []).size ===
          (change.linkedFindingIds ?? []).length,
        `${label}.linkedFindingIds must be unique`,
        errors,
      );
      for (const findingId of change.linkedFindingIds ?? []) {
        assert(
          impactFindingIds.has(findingId),
          `${label}.linkedFindingIds contains unknown impact finding ${findingId}`,
          errors,
        );
        linkedImpactFindingIds.add(findingId);
      }
      validateSourceReferences([change], label, errors);
    }
    for (const operationId of operationIds) {
      assert(
        coveredOperationIds.filter((value) => value === operationId).length ===
          1,
        `${itemLabel} must cover operation ${operationId} exactly once`,
        errors,
      );
    }
  }
}

function validateBreakingFindings(findings, label, errors) {
  const ids = new Set();
  for (const [index, finding] of findings.entries()) {
    const itemLabel = `${label}[${index}]`;
    assert(
      typeof finding.id === "string" && finding.id.length > 0,
      `${itemLabel}.id is required`,
      errors,
    );
    assert(!ids.has(finding.id), `${itemLabel}.id must be unique`, errors);
    ids.add(finding.id);
    assert(
      typeof finding.title === "string" && finding.title.length > 0,
      `${itemLabel}.title is required`,
      errors,
    );
    assert(
      ["high", "medium", "low"].includes(finding.severity),
      `${itemLabel}.severity is invalid`,
      errors,
    );
    assert(
      ["high", "medium", "low"].includes(finding.confidence),
      `${itemLabel}.confidence is invalid`,
      errors,
    );
    assert(
      typeof finding.summary === "string" && finding.summary.length > 0,
      `${itemLabel}.summary is required`,
      errors,
    );
    assert(
      (Array.isArray(finding.evidence) && finding.evidence.length > 0) ||
        (typeof finding.evidence === "string" && finding.evidence.length > 0),
      `${itemLabel}.evidence is required`,
      errors,
    );
    const findingNarrative = [
      finding.title,
      finding.summary,
      ...(Array.isArray(finding.evidence)
        ? finding.evidence
        : [finding.evidence]),
    ]
      .filter((value) => typeof value === "string")
      .join(" ");
    assert(
      !INTERNAL_GENERATOR_TERMS.test(findingNarrative),
      `${itemLabel} must describe public API or SDK behavior without internal generator terminology`,
      errors,
    );
  }
}

function validateCompliance(compliance, errors) {
  assert(
    compliance && typeof compliance === "object",
    "azureCompliance is required",
    errors,
  );
  assert(
    ["passed", "failed", "not-assessed"].includes(compliance?.status),
    "azureCompliance.status must be passed, failed, or not-assessed",
    errors,
  );
  assert(
    Array.isArray(compliance?.findings),
    "azureCompliance.findings must be an array",
    errors,
  );
  assert(
    Array.isArray(compliance?.documents),
    "azureCompliance.documents must be an array",
    errors,
  );
  validateSourceReferences(
    compliance?.documents ?? [],
    "azureCompliance.documents",
    errors,
  );
  validateSourceReferences(
    compliance?.findings ?? [],
    "azureCompliance.findings",
    errors,
  );
  const findingIds = new Set();
  const findingDocumentUrls = new Set(
    (compliance?.findings ?? []).map((finding) => finding.documentationUrl),
  );
  const documentUrls = new Set(
    (compliance?.documents ?? []).map((document) => document.url),
  );
  for (const [index, document] of (compliance?.documents ?? []).entries()) {
    const label = `azureCompliance.documents[${index}]`;
    assert(
      typeof document.title === "string" && document.title.length > 0,
      `${label}.title is required`,
      errors,
    );
    assert(
      typeof document.url === "string" &&
        /^https:\/\/(azure\.github\.io\/typespec-azure|typespec\.io)\//.test(
          document.url,
        ),
      `${label}.url must be an authoritative TypeSpec documentation URL`,
      errors,
    );
    assert(
      typeof document.applicableGuidance === "string" &&
        document.applicableGuidance.length > 0,
      `${label}.applicableGuidance is required`,
      errors,
    );
    assert(
      typeof document.section === "string" && document.section.length > 0,
      `${label}.section is required`,
      errors,
    );
    assert(
      typeof document.guidanceExcerpt === "string" &&
        document.guidanceExcerpt.length > 0 &&
        document.guidanceExcerpt.length <= 500,
      `${label}.guidanceExcerpt must be a short fetched-content excerpt`,
      errors,
    );
    assert(
      typeof document.evidence === "string" && document.evidence.length > 0,
      `${label}.evidence is required`,
      errors,
    );
    if (findingDocumentUrls.has(document.url)) {
      assert(
        ["available", "not-present"].includes(document.expectedCodeStatus),
        `${label}.expectedCodeStatus must be available or not-present`,
        errors,
      );
      if (document.expectedCodeStatus === "available") {
        assert(
          Array.isArray(document.expectedCodeSnippets) &&
            document.expectedCodeSnippets.length > 0 &&
            document.expectedCodeSnippets.length <= 2,
          `${label}.expectedCodeSnippets must contain one or two documented snippets`,
          errors,
        );
        for (const [snippetIndex, snippet] of (
          document.expectedCodeSnippets ?? []
        ).entries()) {
          const snippetLabel = `${label}.expectedCodeSnippets[${snippetIndex}]`;
          assert(
            snippet.language === "tsp",
            `${snippetLabel}.language must be tsp`,
            errors,
          );
          assert(
            snippet.url === document.url,
            `${snippetLabel}.url must match its compliance document`,
            errors,
          );
          assert(
            snippet.section === document.section,
            `${snippetLabel}.section must match its compliance document`,
            errors,
          );
          assert(
            typeof snippet.caption === "string" && snippet.caption.length > 0,
            `${snippetLabel}.caption is required`,
            errors,
          );
          assert(
            Array.isArray(snippet.lines) &&
              snippet.lines.length > 0 &&
              snippet.lines.length <= 12 &&
              snippet.lines.every((line) => typeof line === "string"),
            `${snippetLabel}.lines must contain at most 12 documented lines`,
            errors,
          );
        }
      }
      if (document.expectedCodeStatus === "not-present") {
        assert(
          document.expectedCodeSnippets === undefined ||
            document.expectedCodeSnippets.length === 0,
          `${label}.expectedCodeSnippets must be empty when no example is present`,
          errors,
        );
        assert(
          typeof document.expectedCodeReason === "string" &&
            document.expectedCodeReason.length > 0,
          `${label}.expectedCodeReason is required when no example is present`,
          errors,
        );
      }
    }
  }
  for (const [index, finding] of (compliance?.findings ?? []).entries()) {
    const label = `azureCompliance.findings[${index}]`;
    assert(
      typeof finding.id === "string" && finding.id.length > 0,
      `${label}.id is required`,
      errors,
    );
    assert(!findingIds.has(finding.id), `${label}.id must be unique`, errors);
    findingIds.add(finding.id);
    assert(
      typeof finding.title === "string" && finding.title.length > 0,
      `${label}.title is required`,
      errors,
    );
    assert(
      ["high", "medium", "low"].includes(finding.severity),
      `${label}.severity is invalid`,
      errors,
    );
    assert(
      typeof finding.summary === "string" && finding.summary.length > 0,
      `${label}.summary is required`,
      errors,
    );
    assert(
      typeof finding.documentationUrl === "string" &&
        /^https:\/\/(azure\.github\.io\/typespec-azure|typespec\.io)\//.test(
          finding.documentationUrl,
        ),
      `${label}.documentationUrl must be authoritative`,
      errors,
    );
    assert(
      documentUrls.has(finding.documentationUrl),
      `${label}.documentationUrl must match a fetched compliance document`,
      errors,
    );
    assert(
      Array.isArray(finding.evidence) && finding.evidence.length > 0,
      `${label}.evidence is required`,
      errors,
    );
    assert(
      Array.isArray(finding.codeSnippets) &&
        finding.codeSnippets.length > 0 &&
        finding.codeSnippets.length <= 2,
      `${label}.codeSnippets must contain one or two focused snippets`,
      errors,
    );
    if (Array.isArray(finding.codeSnippets)) {
      for (const [snippetIndex, snippet] of (
        finding.codeSnippets ?? []
      ).entries()) {
        const snippetLabel = `${label}.codeSnippets[${snippetIndex}]`;
        assert(
          typeof snippet.path === "string" && snippet.path.endsWith(".tsp"),
          `${snippetLabel}.path must be a .tsp file`,
          errors,
        );
        assert(
          Number.isInteger(snippet.startLine) && snippet.startLine > 0,
          `${snippetLabel}.startLine is invalid`,
          errors,
        );
        assert(
          Number.isInteger(snippet.endLine) &&
            snippet.endLine >= snippet.startLine,
          `${snippetLabel}.endLine is invalid`,
          errors,
        );
        assert(
          Array.isArray(snippet.lines) &&
            snippet.lines.length === snippet.endLine - snippet.startLine + 1 &&
            snippet.lines.every((line) => typeof line === "string"),
          `${snippetLabel}.lines must cover the declared line range`,
          errors,
        );
        assert(
          snippet.lines.length <= 12,
          `${snippetLabel} must contain at most 12 focused lines`,
          errors,
        );
        assert(
          (finding.sourceReferences ?? []).some(
            (reference) =>
              reference.path === snippet.path &&
              reference.startLine <= snippet.startLine &&
              reference.endLine >= snippet.endLine,
          ),
          `${snippetLabel} must be covered by a finding source reference`,
          errors,
        );
      }
    }
  }
  if (["passed", "failed"].includes(compliance?.status)) {
    assert(
      (compliance.documents ?? []).length > 0,
      `azureCompliance.status ${compliance.status} requires fetched documents`,
      errors,
    );
    assert(
      compliance.summary?.patternsAssessed ===
        (compliance.documents ?? []).length,
      "azureCompliance.summary.patternsAssessed must match documents",
      errors,
    );
    assert(
      compliance.summary?.findingCount === (compliance.findings ?? []).length,
      "azureCompliance.summary.findingCount must match findings",
      errors,
    );
  }
  if (compliance?.status === "failed") {
    assert(
      (compliance.findings ?? []).length > 0,
      "azureCompliance.status failed requires a finding",
      errors,
    );
  }
  if (compliance?.status === "passed") {
    assert(
      (compliance.findings ?? []).length === 0,
      "azureCompliance.status passed cannot include findings",
      errors,
    );
  }
  if (compliance?.status === "not-assessed") {
    assert(
      typeof compliance.reason === "string" && compliance.reason.length > 0,
      "azureCompliance.reason is required when not assessed",
      errors,
    );
    assert(
      (compliance.findings ?? []).length === 0,
      "azureCompliance.status not-assessed cannot include findings",
      errors,
    );
  }
}

export function validateAssessment(document) {
  const errors = [];
  assert(document?.schemaVersion === 2, "schemaVersion must be 2", errors);
  assert(
    typeof document?.baseline?.commit === "string" &&
      document.baseline.commit.length > 0,
    "baseline.commit is required",
    errors,
  );
  assert(
    typeof document?.head?.commit === "string" &&
      document.head.commit.length > 0,
    "head.commit is required",
    errors,
  );
  if (!document?.pr && document?.head?.hasWorkingTreeChanges) {
    assert(
      document.head.changeScope &&
        ["staged", "unstaged", "untracked"].every(
          (field) => typeof document.head.changeScope[field] === "boolean",
        ),
      "local working-tree assessments require staged, unstaged, and untracked changeScope flags",
      errors,
    );
  }
  assert(
    ["high", "medium", "low"].includes(document?.overallConfidence),
    "overallConfidence must be high, medium, or low",
    errors,
  );
  if (document?.assessmentDuration !== undefined) {
    assert(
      Number.isInteger(document.assessmentDuration.totalMs) &&
        document.assessmentDuration.totalMs >= 0,
      "assessmentDuration.totalMs is invalid",
      errors,
    );
    const hasComponents = [
      "toolchainSetupMs",
      "preparationMs",
      "documentationReviewMs",
    ].some((field) => field in document.assessmentDuration);
    if (hasComponents) {
      if (document.assessmentDuration.toolchainSetupMs !== undefined) {
        assert(
          Number.isInteger(document.assessmentDuration.toolchainSetupMs) &&
            document.assessmentDuration.toolchainSetupMs >= 0,
          "assessmentDuration.toolchainSetupMs is invalid",
          errors,
        );
      }
      assert(
        Number.isInteger(document.assessmentDuration.preparationMs) &&
          document.assessmentDuration.preparationMs >= 0,
        "assessmentDuration.preparationMs is invalid",
        errors,
      );
      assert(
        document.assessmentDuration.documentationReviewMs === null ||
          (Number.isInteger(
            document.assessmentDuration.documentationReviewMs,
          ) &&
            document.assessmentDuration.documentationReviewMs >= 0),
        "assessmentDuration.documentationReviewMs is invalid",
        errors,
      );
      assert(
        document.assessmentDuration.totalMs ===
          (document.assessmentDuration.toolchainSetupMs ?? 0) +
            document.assessmentDuration.preparationMs +
            (document.assessmentDuration.documentationReviewMs ?? 0),
        "assessmentDuration.totalMs must equal toolchainSetupMs + preparationMs + documentationReviewMs",
        errors,
      );
    }
    if (document.assessmentDuration.documentationReviewMs === null) {
      assert(
        typeof document.assessmentDuration.note === "string" &&
          document.assessmentDuration.note.length > 0,
        "assessmentDuration.note is required when documentationReviewMs is unavailable",
        errors,
      );
    }
    const breakdown = document.assessmentDuration.breakdown;
    if (breakdown !== undefined) {
      const durationFields = [
        "semanticUnderstandingMs",
        "restBreakingMs",
        "downstreamBreakingMs",
        "complianceMs",
        "overheadMs",
        "totalMs",
      ];
      for (const field of durationFields) {
        assert(
          Number.isInteger(breakdown[field]) && breakdown[field] >= 0,
          `assessmentDuration.breakdown.${field} is invalid`,
          errors,
        );
      }
      const qualityFields = [
        "semanticUnderstandingQuality",
        "restBreakingQuality",
        "downstreamBreakingQuality",
        "complianceQuality",
        "overheadQuality",
        "totalQuality",
      ];
      for (const field of qualityFields) {
        assert(
          ["measured", "estimated", "derived", "estimated/measured"].includes(
            breakdown[field],
          ),
          `assessmentDuration.breakdown.${field} is invalid`,
          errors,
        );
      }
      const componentTotal =
        breakdown.semanticUnderstandingMs +
        breakdown.restBreakingMs +
        breakdown.downstreamBreakingMs +
        breakdown.complianceMs +
        breakdown.overheadMs;
      assert(
        Math.abs(componentTotal - breakdown.totalMs) < 1000,
        "assessmentDuration.breakdown.totalMs must match the rounded phase durations",
        errors,
      );
      assert(
        breakdown.totalMs === document.assessmentDuration.totalMs,
        "assessmentDuration.breakdown.totalMs must match assessmentDuration.totalMs",
        errors,
      );
      assert(
        typeof breakdown.searchRoute === "string" &&
          breakdown.searchRoute.length > 0,
        "assessmentDuration.breakdown.searchRoute is required",
        errors,
      );
    }
  }
  const dimensions = document?.dimensions;
  assert(
    dimensions && typeof dimensions === "object",
    "dimensions is required",
    errors,
  );
  const semantic = dimensions?.semanticUnderstanding?.items;
  const rest = dimensions?.restBreakingChanges?.findings;
  const downstream =
    dimensions?.restCompatibleDownstreamBreakingChanges?.findings;
  assert(
    Array.isArray(semantic),
    "semanticUnderstanding.items must be an array",
    errors,
  );
  assert(
    Array.isArray(rest),
    "restBreakingChanges.findings must be an array",
    errors,
  );
  assert(
    Array.isArray(downstream),
    "restCompatibleDownstreamBreakingChanges.findings must be an array",
    errors,
  );
  assert(
    !Array.isArray(rest) ||
      rest.length === 0 ||
      (Array.isArray(downstream) && downstream.length > 0),
    "REST breaking changes require a downstream breaking finding",
    errors,
  );
  validateSourceReferences(
    semantic ?? [],
    "semanticUnderstanding.items",
    errors,
  );
  const impactFindingIds = new Set(
    [
      ...(rest ?? []),
      ...(downstream ?? []),
      ...(dimensions?.azureCompliance?.findings ?? []),
    ].map((finding) => finding.id),
  );
  const linkedImpactFindingIds = new Set();
  validateOperations(
    semantic ?? [],
    impactFindingIds,
    linkedImpactFindingIds,
    errors,
  );
  for (const findingId of impactFindingIds) {
    assert(
      linkedImpactFindingIds.has(findingId),
      `impact finding ${findingId} must be linked from a semantic change`,
      errors,
    );
  }
  validateSourceReferences(rest ?? [], "restBreakingChanges.findings", errors);
  validateBreakingFindings(rest ?? [], "restBreakingChanges.findings", errors);
  validateSourceReferences(
    downstream ?? [],
    "restCompatibleDownstreamBreakingChanges.findings",
    errors,
  );
  validateBreakingFindings(
    downstream ?? [],
    "restCompatibleDownstreamBreakingChanges.findings",
    errors,
  );
  validateCompliance(dimensions?.azureCompliance, errors);
  return errors;
}

function main() {
  const [jsonPath] = process.argv.slice(2);
  if (!jsonPath) {
    process.stderr.write("Usage: validate-assessment.mjs <assessment.json>\n");
    process.exitCode = 1;
    return;
  }
  const document = JSON.parse(readFileSync(resolve(jsonPath), "utf8"));
  const errors = validateAssessment(document);
  if (errors.length > 0) {
    process.stderr.write(`${errors.join("\n")}\n`);
    process.exitCode = 1;
    return;
  }
  process.stdout.write("Assessment output is valid.\n");
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) main();
