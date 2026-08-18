#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const skillRoot = resolve(scriptDirectory, "..");
const evidenceRoot = resolve(skillRoot, "test-evidence");
const assessmentRoot = resolve(evidenceRoot, "assessments");
const cases = JSON.parse(
  readFileSync(resolve(scriptDirectory, "fixtures", "recent-pr-cases.json")),
);
const operationsByPr = JSON.parse(
  readFileSync(
    resolve(scriptDirectory, "fixtures", "recent-pr-operations.json"),
  ),
);

function withSources(value, sourceReferences) {
  return { ...value, sourceReferences };
}

function operationsFor(item) {
  return operationsByPr[item.pr].map(
    ({ request, responses, ...operation }) => ({
      ...operation,
      requestPayload: request,
      responsePayloads: responses,
      lro: operation.lro.isLongRunning
        ? {
            polling:
              "Poll the emitted async endpoint after Retry-After until a terminal state.",
            finalResult:
              "Use the final response contract described for this operation.",
            ...operation.lro,
          }
        : operation.lro,
      signature: `${operation.method} ${operation.path}`,
      sourceReferences: item.sourceReferences,
    }),
  );
}

function projectsFor(item) {
  return [
    ...new Set(
      item.sourceReferences.map((reference) =>
        reference.path.slice(0, reference.path.lastIndexOf("/")),
      ),
    ),
  ];
}

function emitterRunsFor(item) {
  const emitters = [
    {
      id: "autorest",
      name: "@azure-tools/typespec-autorest",
      output: "OpenAPI",
    },
    {
      id: "tcgc",
      name: "@azure-tools/typespec-client-generator-core",
      output: "SDK metadata (generic)",
    },
  ];

  return projectsFor(item).flatMap((project) =>
    ["baseline", "head"].flatMap((revision) =>
      emitters.map((emitter) => ({
        project,
        revision,
        emitter: emitter.name,
        emitterId: emitter.id,
        output: emitter.output,
        status: item.errors.some((error) =>
          error
            .toLowerCase()
            .includes(`${revision} ${emitter.id} compilation failed`),
        )
          ? "failed"
          : "succeeded",
        evidence: item.artifactEvidence[emitter.id],
      })),
    ),
  );
}

function buildAssessment(item) {
  return {
    schemaVersion: 1,
    overallConfidence: item.errors.length === 0 ? "high" : "low",
    pr: item.pr,
    title: item.title,
    url: item.url,
    state: item.state,
    createdAt: item.createdAt,
    baseline: { commit: item.baseCommit },
    head: { commit: item.headCommit },
    result: item.result,
    artifactEvidence: item.artifactEvidence,
    assessmentEvidence: {
      changedTypeSpec: item.sourceReferences,
      emitterRuns: emitterRunsFor(item),
    },
    dimensions: {
      semanticUnderstanding: {
        items: [
          {
            id: `pr-${item.pr}-intent`,
            intent: item.intent,
            transformationChain: item.transformationChain,
            restRepresentation: {
              summary: item.restRepresentation,
              operations: operationsFor(item),
            },
            confidence: "high",
            sourceReferences: item.sourceReferences,
          },
        ],
      },
      restBreakingChanges: {
        findings: item.restFindings.map((finding) =>
          withSources(finding, item.sourceReferences),
        ),
      },
      restCompatibleDownstreamBreakingChanges: {
        findings: item.downstreamFindings.map((finding) =>
          withSources(finding, item.sourceReferences),
        ),
      },
      azureCompliance: {
        status: "not-assessed",
        reason: "Deferred from MVP.",
      },
    },
    errors: item.errors,
  };
}

function sourceLinks(references) {
  return references
    .map(
      (reference) =>
        `[${reference.path}:L${reference.startLine}-L${reference.endLine}](${reference.link})`,
    )
    .join(", ");
}

function describeLro(lro) {
  if (!lro.isLongRunning) return "No.";
  return [
    `Yes (${lro.pattern})`,
    `final-state-via: ${lro.finalStateVia}`,
    `polling: ${lro.polling}`,
    `final result: ${lro.finalResult}`,
  ].join("; ");
}

function describePaging(paging) {
  if (!paging.isPaged) return "No.";
  return [
    `Yes; item type: ${paging.itemType}`,
    paging.itemsProperty ? `items property: ${paging.itemsProperty}` : null,
    `continuation field: ${paging.nextLinkName}`,
    paging.continuation,
  ]
    .filter(Boolean)
    .join("; ");
}

function findingSection(findings, incompleteMessage) {
  if (findings.length === 0) {
    return incompleteMessage ?? "None detected.";
  }
  return findings
    .map(
      (finding) => `### ${finding.title}

- **Severity:** ${finding.severity}
- **Confidence:** ${finding.confidence}
- **Summary:** ${finding.summary}
- **Evidence:** ${finding.evidence.join("; ")}
- **TypeSpec source:** ${sourceLinks(finding.sourceReferences)}`,
    )
    .join("\n\n");
}

function buildMarkdown(assessment) {
  const semantic = assessment.dimensions.semanticUnderstanding.items[0];
  const operations = semantic.restRepresentation.operations
    .map(
      (operation) => `#### \`${operation.operationId}\`

- **HTTP path:** \`${operation.signature}\`
- **API versions:** ${operation.apiVersions.map((version) => `\`${version}\``).join(", ")}
- **Parameters:** ${operation.parameters.join("; ") || "None."}
- **Request payload:** ${operation.requestPayload}
- **Response payloads:** ${operation.responsePayloads.join("; ")}
- **Service behavior:** ${operation.serviceBehavior}
- **LRO:** ${describeLro(operation.lro)}
- **Paging:** ${describePaging(operation.paging)}
- **TypeSpec source:** ${sourceLinks(operation.sourceReferences)}`,
    )
    .join("\n\n");
  const incompleteMessage =
    assessment.errors.length > 0
      ? "Not fully assessed because compilation did not complete; see Assessment Errors."
      : undefined;
  const emitterRuns = assessment.assessmentEvidence.emitterRuns
    .map(
      (run) =>
        `| \`${run.project}\` | ${run.revision} | \`${run.emitter}\` (\`${run.emitterId}\`) | ${run.output} | ${run.status} | ${run.evidence} |`,
    )
    .join("\n");
  const changedTypeSpec = assessment.assessmentEvidence.changedTypeSpec
    .map((reference) => `- ${sourceLinks([reference])}`)
    .join("\n");

  return `# TypeSpec Assessment

**PR:** [#${assessment.pr} — ${assessment.title}](${assessment.url})

**Overall confidence:** ${assessment.overallConfidence}

**Baseline:** \`${assessment.baseline.commit}\`  
**Head:** \`${assessment.head.commit}\`

## Semantic Understanding

### Intent: ${semantic.intent}

**Confidence:** ${semantic.confidence}

**Transformation chain:**

${semantic.transformationChain.map((step, index) => `${index + 1}. ${step}`).join("\n")}

**REST representation:** ${semantic.restRepresentation.summary}

${operations}

## REST Breaking Changes

${findingSection(assessment.dimensions.restBreakingChanges.findings, incompleteMessage)}

## REST-Compatible Downstream Breaking Changes

${findingSection(assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings, incompleteMessage)}

## Azure Compliance

\`not-assessed\` — Deferred from MVP.

## Assessment Errors

${assessment.errors.length === 0 ? "None." : assessment.errors.map((error) => `- ${error}`).join("\n")}

## Assessment Evidence

**Compared revisions:**

- **Baseline:** \`${assessment.baseline.commit}\`
- **Head:** \`${assessment.head.commit}\`
- **Changed TypeSpec:**

${changedTypeSpec}

### Emitter Runs

| Project | Revision | Emitter | Output | Status | Evidence |
| --- | --- | --- | --- | --- | --- |
${emitterRuns}

### Artifact Evidence

- **AutoRest:** ${assessment.artifactEvidence.autorest}
- **TCGC:** ${assessment.artifactEvidence.tcgc}
- **Source-only evidence:** TypeSpec decorators and declarations were inspected at the changed-source links above.
`;
}

mkdirSync(assessmentRoot, { recursive: true });
const assessments = cases.map(buildAssessment);

for (const assessment of assessments) {
  const outputDirectory = resolve(assessmentRoot, String(assessment.pr));
  mkdirSync(outputDirectory, { recursive: true });
  writeFileSync(
    resolve(outputDirectory, "assessment.json"),
    `${JSON.stringify(assessment, null, 2)}\n`,
  );
  writeFileSync(
    resolve(outputDirectory, "assessment.md"),
    buildMarkdown(assessment),
  );
}

writeFileSync(
  resolve(evidenceRoot, "assessments.json"),
  `${JSON.stringify(
    {
      schemaVersion: 1,
      generatedAt: new Date().toISOString(),
      repository: "Azure/azure-rest-api-specs",
      assessments,
    },
    null,
    2,
  )}\n`,
);

writeFileSync(
  resolve(evidenceRoot, "assessment-summary.md"),
  `# TypeSpec Assessment Test Evidence

Ten representative TypeSpec PRs created within the previous year. Each result
uses the skill's current Markdown and JSON output contracts.

| PR | Case | State | Result | Assessment |
| --- | --- | --- | --- | --- |
${cases
  .map(
    (item) =>
      `| [#${item.pr}](${item.url}) | ${item.case} | ${item.state} | ${item.result} | [Markdown](assessments/${item.pr}/assessment.md) · [JSON](assessments/${item.pr}/assessment.json) |`,
  )
  .join("\n")}
`,
);
