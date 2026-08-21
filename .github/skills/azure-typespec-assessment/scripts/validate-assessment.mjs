#!/usr/bin/env node

import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

import {
  deriveCodeSafety,
  formatCodeSafety,
  formatConfidence,
  renderAssessment,
} from "./render-assessment.mjs";

const REQUIRED_HEADINGS = [
  "## 📌 Executive Summary",
  "## 🎯 Action Required",
  "## 🧠 Semantic Understanding",
  "## 🛡️ Compatibility Assessment",
  "## ☁️ Azure Compliance",
  "## 📎 Appendix",
];

function assert(condition, message, errors) {
  if (!condition) errors.push(message);
}

function countOccurrences(value, pattern) {
  return value.split(pattern).length - 1;
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

function validateOperations(items, errors) {
  for (const [itemIndex, item] of items.entries()) {
    const itemLabel = `semanticUnderstanding.items[${itemIndex}]`;
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

export function validateAssessment(document, markdown) {
  const errors = [];
  assert(document?.schemaVersion === 1, "schemaVersion must be 1", errors);
  assert(
    ["high", "medium", "low"].includes(document?.overallConfidence),
    "overallConfidence must be high, medium, or low",
    errors,
  );
  if (document?.assessmentDuration !== undefined) {
    if (document.assessmentDuration.toolchainSetupMs !== undefined) {
      assert(
        Number.isInteger(document.assessmentDuration.toolchainSetupMs) &&
          document.assessmentDuration.toolchainSetupMs >= 0,
        "assessmentDuration.toolchainSetupMs is invalid",
        errors,
      );
    }
    for (const field of ["preparationMs", "totalMs"]) {
      assert(
        Number.isInteger(document.assessmentDuration[field]) &&
          document.assessmentDuration[field] >= 0,
        `assessmentDuration.${field} is invalid`,
        errors,
      );
    }
    assert(
      document.assessmentDuration.documentationReviewMs === null ||
        (Number.isInteger(document.assessmentDuration.documentationReviewMs) &&
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
    if (document.assessmentDuration.documentationReviewMs === null) {
      assert(
        typeof document.assessmentDuration.note === "string" &&
          document.assessmentDuration.note.length > 0,
        "assessmentDuration.note is required when documentationReviewMs is unavailable",
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
  validateSourceReferences(
    semantic ?? [],
    "semanticUnderstanding.items",
    errors,
  );
  validateOperations(semantic ?? [], errors);
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
  if (markdown !== undefined) {
    assert(
      markdown.includes(
        `**Overall confidence:** ${formatConfidence(document.overallConfidence)}`,
      ),
      "assessment.md overall confidence must match assessment.json",
      errors,
    );
    assert(
      markdown.includes(
        `**Overall code safety:** ${formatCodeSafety(deriveCodeSafety(document))}`,
      ),
      "assessment.md overall code safety must match assessment findings",
      errors,
    );
    for (const heading of REQUIRED_HEADINGS) {
      assert(
        markdown.includes(heading),
        `assessment.md is missing heading: ${heading}`,
        errors,
      );
    }
    assert(
      !/^ +## /m.test(markdown),
      "assessment.md second-level headings must not be indented",
      errors,
    );
    assert(
      markdown.includes("### Timing"),
      "assessment.md is missing assessment timing evidence",
      errors,
    );
    assert(
      markdown.includes("### Change Overview"),
      "assessment.md is missing Semantic Understanding change overview",
      errors,
    );
    assert(
      markdown.includes("### Operation Details"),
      "assessment.md is missing Semantic Understanding operation details",
      errors,
    );
    for (let index = 1; index < REQUIRED_HEADINGS.length; index += 1) {
      assert(
        markdown.indexOf(REQUIRED_HEADINGS[index - 1]) <
          markdown.indexOf(REQUIRED_HEADINGS[index]),
        `assessment.md heading order is invalid near ${REQUIRED_HEADINGS[index]}`,
        errors,
      );
    }
    const operationTotal = (semantic ?? []).reduce(
      (total, item) => total + item.restRepresentation.operations.length,
      0,
    );
    assert(
      markdown.includes(
        `**Scope:** ${(semantic ?? []).length} intent(s), ${operationTotal} affected operation(s)`,
      ),
      "assessment.md scope counts must match assessment.json",
      errors,
    );
    const findings = [
      ...(rest ?? []),
      ...(downstream ?? []),
      ...(dimensions?.azureCompliance?.findings ?? []),
    ];
    for (const finding of findings) {
      assert(
        markdown.includes(finding.title),
        `assessment.md is missing finding: ${finding.title}`,
        errors,
      );
    }
    const operationCounts = new Map();
    for (const operation of (semantic ?? []).flatMap(
      (item) => item.restRepresentation.operations,
    )) {
      operationCounts.set(
        operation.operationId,
        (operationCounts.get(operation.operationId) ?? 0) + 1,
      );
    }
    for (const [operationId, expectedCount] of operationCounts) {
      assert(
        countOccurrences(markdown, `\`${operationId}\``) === expectedCount,
        `assessment.md must include ${operationId} ${expectedCount} time(s) in Semantic Understanding`,
        errors,
      );
    }
    if (errors.length === 0) {
      assert(
        markdown === renderAssessment(document),
        "assessment.md must be generated from assessment.json with render-assessment.mjs",
        errors,
      );
    }
  }
  return errors;
}

function main() {
  const [jsonPath, markdownPath] = process.argv.slice(2);
  if (!jsonPath) {
    process.stderr.write(
      "Usage: validate-assessment.mjs <assessment.json> [assessment.md]\n",
    );
    process.exitCode = 1;
    return;
  }
  const document = JSON.parse(readFileSync(resolve(jsonPath), "utf8"));
  const markdown = markdownPath
    ? readFileSync(resolve(markdownPath), "utf8")
    : undefined;
  const errors = validateAssessment(document, markdown);
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
