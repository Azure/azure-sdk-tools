#!/usr/bin/env node

import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const REQUIRED_HEADINGS = [
  "## Semantic Understanding",
  "## REST Breaking Changes",
  "## REST-Compatible Downstream Breaking Changes",
  "## Azure Compliance",
  "## Assessment Errors",
  "## Assessment Evidence",
];

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

function validateOperations(items, errors) {
  for (const [itemIndex, item] of items.entries()) {
    const restRepresentation = item.restRepresentation;
    assert(
      restRepresentation && typeof restRepresentation === "object",
      `semanticUnderstanding.items[${itemIndex}].restRepresentation is required`,
      errors,
    );
    assert(
      typeof restRepresentation?.summary === "string" &&
        restRepresentation.summary.length > 0,
      `semanticUnderstanding.items[${itemIndex}].restRepresentation.summary is required`,
      errors,
    );
    const operations = restRepresentation?.operations;
    assert(
      Array.isArray(operations) && operations.length > 0,
      `semanticUnderstanding.items[${itemIndex}].restRepresentation requires operations`,
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

export function validateAssessment(document, markdown) {
  const errors = [];
  assert(document?.schemaVersion === 1, "schemaVersion must be 1", errors);
  assert(
    ["high", "medium", "low"].includes(document?.overallConfidence),
    "overallConfidence must be high, medium, or low",
    errors,
  );
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
  validateSourceReferences(
    downstream ?? [],
    "restCompatibleDownstreamBreakingChanges.findings",
    errors,
  );
  assert(
    dimensions?.azureCompliance?.status === "not-assessed",
    "azureCompliance.status must be not-assessed",
    errors,
  );
  assert(
    typeof dimensions?.azureCompliance?.reason === "string" &&
      dimensions.azureCompliance.reason.length > 0,
    "azureCompliance.reason is required",
    errors,
  );
  if (markdown !== undefined) {
    assert(
      markdown.includes(
        `**Overall confidence:** ${document.overallConfidence}`,
      ),
      "assessment.md overall confidence must match assessment.json",
      errors,
    );
    for (const heading of REQUIRED_HEADINGS) {
      assert(
        markdown.includes(heading),
        `assessment.md is missing heading: ${heading}`,
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
