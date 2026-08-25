import assert from "node:assert/strict";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  assembleAssessment,
  assembleAssessmentFiles,
  operationHasMaterialAspectChange,
  validateJudgment,
} from "./assemble-assessment.mjs";
import { renderAssessmentHtml } from "./render-assessment-html.mjs";
import { validateAssessment } from "./validate-assessment.mjs";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const sourceReference = {
  path: "specification/widget/main.tsp",
  revision: "head",
  startLine: 10,
  endLine: 13,
  link: "specification/widget/main.tsp#L10-L13",
};
const typeSpecDiff = {
  path: "specification/widget/main.tsp",
  oldStart: 10,
  oldCount: 4,
  newStart: 10,
  newCount: 4,
  context: "interface Widgets",
  lines: [
    " interface Widgets {",
    "-  @doc(\"Old behavior\")",
    "+  @doc(\"New behavior\")",
    "   get is ArmResourceRead<Widget>;",
  ],
};

function modelInput() {
  return {
    schemaVersion: 1,
    baseline: { ref: "origin/main", commit: "base-commit" },
    head: { commit: "head-commit", hasWorkingTreeChanges: false },
    changedFiles: ["specification/widget/main.tsp"],
    sourceFiles: [
      {
        path: "specification/widget/main.tsp",
        changes: [
          {
            id: "source-widget",
            oldStart: 10,
            newStart: 10,
            lines: ['@doc("New behavior")'],
          },
        ],
      },
    ],
    projects: [
      {
        path: "specification/widget",
        rest: {
          operationChanges: [
            {
              id: "operation-widget-get",
              kind: "modified",
              operationKey: "2026-01-01:GET:/widgets/{widgetName}",
              operationId: "Widgets_Get",
              apiVersion: "2026-01-01",
              changedAspects: ["responses"],
              aspectChanges: {
                responses: {
                  before: ["200 Widget"],
                  after: ["200 UpdatedWidget"],
                },
              },
            },
          ],
          operationGroups: [],
          breakingCandidates: [
            {
              id: "rest-approved",
              rule: "response-contract-changed",
              severity: "high",
              summary: "Widgets_Get changes its success response contract.",
              evidence: {
                operation: "2026-01-01:GET:/widgets/{widgetName}",
              },
            },
            {
              id: "rest-rejected",
              rule: "parameter-contract-changed",
              severity: "high",
              summary: "A parameter appears changed.",
              evidence: {
                operation: "2026-01-01:GET:/widgets/{widgetName}",
              },
            },
          ],
        },
        downstream: {
          candidates: [
            {
              id: "downstream-approved",
              rule: "sdk-enum-shape-changed",
              severity: "medium",
              summary:
                "TCGC isFixed metadata indicates a generated enum shape change.",
              evidence: [
                {
                  path: "specification/widget/main.tsp",
                  symbol: "WidgetKind",
                },
              ],
            },
            {
              id: "downstream-rejected",
              rule: "client-location-changed",
              severity: "medium",
              summary: "A generated method may move clients.",
              evidence: [
                {
                  path: "specification/widget/main.tsp",
                  symbol: "Widgets_Get",
                },
              ],
            },
          ],
        },
      },
    ],
    complianceEvidence: {
      documents: [
        {
          category: "Models",
          title: "TypeSpec models",
          url: "https://typespec.io/docs/language-basics/models/",
          matchingExcerpt:
            "Models are collections of named properties and their types.",
          candidateCodeBlocks: [
            "model Widget {\n  name: string;\n}",
          ],
        },
      ],
    },
    errors: [],
    assessmentDuration: {
      preparationMs: 100,
      deterministicAnalysisMs: 80,
      documentationEvidenceMs: 20,
      totalMs: 120,
    },
  };
}

function judgment() {
  return {
    schemaVersion: 1,
    pr: 123,
    semanticIntents: [
      {
        id: "update-widget-response",
        title: "Update the widget read response.",
        rationale:
          "The changed TypeSpec updates the existing GET response while preserving its route.",
        operationChangeIds: ["operation-widget-get"],
        operationGroupIds: [],
        sourceChangeIds: ["source-widget"],
        sourcePaths: [],
      },
    ],
    restCandidates: [
      {
        id: "rest-approved",
        decision: "approve",
        severity: "medium",
        rationale:
          "Existing callers can observe a different success response contract.",
      },
      {
        id: "rest-rejected",
        decision: "reject",
        rationale:
          "The apparent parameter change is an artifact comparison mismatch.",
      },
    ],
    downstreamCandidates: [
      {
        id: "downstream-approved",
        decision: "approve",
        rationale:
          "Generated clients expose a different public enum representation.",
      },
      {
        id: "downstream-rejected",
        decision: "reject",
        rationale: "The generated method remains on the same client.",
      },
    ],
    compliance: {
      status: "passed",
      rationale:
        "The changed declaration follows the fetched TypeSpec model guidance.",
      documentUrls: ["https://typespec.io/docs/language-basics/models/"],
      findings: [],
    },
    overallConfidence: "high",
    blockers: [],
  };
}

function materialization() {
  const operation = {
    operationId: "Widgets_Get",
    apiVersions: ["2026-01-01"],
    method: "GET",
    path: "/widgets/{widgetName}",
    signature: "GET /widgets/{widgetName}",
    parameters: ["path widgetName: string, required"],
    requestPayload: "none",
    responsePayloads: [
      "200 application/json payload: UpdatedWidget",
      "default application/json payload: ErrorResponse",
    ],
    serviceBehavior: "Returns one widget.",
    lro: { isLongRunning: false },
    paging: { isPaged: false },
    sourceReferences: [sourceReference],
  };
  return {
    schemaVersion: 2,
    pr: 123,
    title: "Update widget response",
    url: "https://github.com/Azure/azure-rest-api-specs/pull/123",
    baseline: { ref: "origin/main", commit: "base-commit" },
    head: { commit: "head-commit", hasWorkingTreeChanges: false },
    assessmentEvidence: {
      changedTypeSpec: [sourceReference],
      emitterRuns: [],
    },
    dimensions: {
      semanticUnderstanding: {
        items: [
          {
            id: "historical-conclusion-must-not-be-copied",
            intent: "Historical conclusion must not be copied.",
            transformationChain: ["Historical model reasoning."],
            confidence: "low",
            sourceReferences: [sourceReference],
            changes: [
              {
                kind: "modified",
                summary: "Historical summary.",
                operationIds: ["Widgets_Get"],
                apiVersions: ["2026-01-01"],
                aspects: [
                  {
                    field: "Historical aspect",
                    before: "Old",
                    after: "New",
                  },
                ],
                effect: "Historical effect.",
                typeSpecCause: "Historical cause.",
                sourceReferences: [sourceReference],
                typeSpecDiffs: [typeSpecDiff],
                linkedFindingIds: [],
              },
            ],
            restRepresentation: {
              summary: "Historical REST conclusion.",
              operations: [operation],
            },
          },
        ],
      },
      restBreakingChanges: {
        findings: [
          {
            id: "historical-rest-finding",
            title: "Historical finding",
          },
        ],
      },
      restCompatibleDownstreamBreakingChanges: { findings: [] },
      azureCompliance: { status: "not-assessed", documents: [], findings: [] },
    },
    errors: [],
  };
}

function catalogOnlyInputs() {
  const input = modelInput();
  input.projects[0].rest.operationChanges = [];
  input.projects[0].rest.operationGroups = [
    {
      id: "version-modified-widget-list",
      kind: "version-modified",
      apiVersion: "2026-01-01",
      family: "Widgets",
      operationIds: ["Widgets_List"],
      changedAspects: ["responses"],
      changes: [
        {
          operationId: "Widgets_List",
          aspects: {
            responses: {
              before: [{ status: "200", schemas: ["WidgetList"] }],
              after: [{ status: "200", schemas: ["UpdatedWidgetList"] }],
            },
          },
        },
      ],
    },
  ];
  input.projects[0].rest.breakingCandidates = [];
  input.projects[0].downstream.candidates = [];

  const value = judgment();
  value.semanticIntents[0].operationChangeIds = [];
  value.semanticIntents[0].operationGroupIds = [
    "version-modified-widget-list",
  ];
  value.restCandidates = [];
  value.downstreamCandidates = [];
  return { input, value };
}

function openApiDocument(responseSchema) {
  return {
    swagger: "2.0",
    info: { title: "Widgets", version: "2026-01-01" },
    paths: {
      "/widgets": {
        get: {
          operationId: "Widgets_List",
          parameters: [
            {
              name: "api-version",
              in: "query",
              required: true,
              type: "string",
            },
          ],
          responses: {
            200: {
              schema: { $ref: `#/definitions/${responseSchema}` },
            },
          },
        },
      },
    },
    definitions: {
      [responseSchema]: {
        type: "object",
        properties: {
          value: {
            type: "array",
            items: { $ref: "#/definitions/Widget" },
          },
        },
      },
      Widget: {
        type: "object",
        properties: { name: { type: "string" } },
      },
    },
  };
}

test("strict decision enums reject normalized alternatives", () => {
  const input = modelInput();
  const value = judgment();
  value.restCandidates[0].decision = "accepted";
  assert.throws(
    () => validateJudgment(value, input),
    /decision must be exactly approve or reject/,
  );
});

test("artifact ordering and identity noise is not a material operation change", () => {
  assert.equal(
    operationHasMaterialAspectChange({
      aspects: {
        responses: {
          before: [
            { status: "200", schemas: ["Widget"] },
            { status: "default", schemas: ["ErrorResponse"] },
          ],
          after: [
            { schemas: ["ErrorResponse"], status: "default" },
            { schemas: ["Widget"], status: "200" },
          ],
        },
      },
    }),
    false,
  );
  assert.equal(
    operationHasMaterialAspectChange({
      aspects: {
        parameters: {
          before: [{ in: "path", name: "widgetName", required: true }],
          after: [
            { in: "path", name: "widgetName", required: true },
            {
              in: "query",
              name: "afcManagedSync",
              required: false,
            },
          ],
        },
      },
    }),
    true,
  );
});

test("numeric string PRs are accepted and normalized to integers", () => {
  const value = judgment();
  value.pr = "000123";
  validateJudgment(value, modelInput());
  const assessment = assembleAssessment({
    modelInput: modelInput(),
    judgment: value,
    materialization: materialization(),
  });
  assert.equal(assessment.pr, 123);
  assert.equal(typeof assessment.pr, "number");
});

test("malformed and nonpositive PR strings are rejected", () => {
  for (const pr of ["", "0", "000", "-1", "1.5", "12x", " 12", "12 "]) {
    const value = judgment();
    value.pr = pr;
    assert.throws(
      () => validateJudgment(value, modelInput()),
      /must be a positive integer or a digit-only positive numeric string/,
      pr,
    );
  }
});

test("unknown, duplicate, and unreferenced evidence IDs are rejected", () => {
  const input = modelInput();

  const unknown = judgment();
  unknown.semanticIntents[0].operationChangeIds = ["missing-operation"];
  assert.throws(
    () => validateJudgment(unknown, input),
    /unknown evidence ID: missing-operation/,
  );

  const duplicate = judgment();
  duplicate.restCandidates.push(structuredClone(duplicate.restCandidates[0]));
  assert.throws(
    () => validateJudgment(duplicate, input),
    /duplicate evidence ID: rest-approved/,
  );

  const unreferenced = judgment();
  unreferenced.restCandidates.pop();
  assert.throws(
    () => validateJudgment(unreferenced, input),
    /leaves evidence ID\(s\) unreferenced: rest-rejected/,
  );
});

test("semantic judgment aspects override noisy deterministic summaries", () => {
  const input = modelInput();
  const value = judgment();
  value.semanticIntents[0].aspects = [
    {
      field: "Response schema",
      before: "200 returns Widget.",
      after: "200 returns UpdatedWidget.",
    },
  ];
  const assessment = assembleAssessment({
    modelInput: input,
    judgment: value,
    materialization: materialization(),
  });
  assert.deepEqual(
    assessment.dimensions.semanticUnderstanding.items[0].changes[0].aspects,
    value.semanticIntents[0].aspects,
  );
});

test("approved candidates become findings and rejected candidates do not", () => {
  const assessment = assembleAssessment({
    modelInput: modelInput(),
    judgment: judgment(),
    materialization: materialization(),
    judgmentElapsedMs: 45,
    deterministicAssemblyMs: 6,
    renderMs: 4,
  });
  assert.deepEqual(
    assessment.dimensions.restBreakingChanges.findings.map((item) => item.id),
    ["rest-approved"],
  );
  assert.deepEqual(
    assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings.map(
      (item) => item.id,
    ),
    ["downstream-approved"],
  );
  assert.equal(
    assessment.dimensions.restBreakingChanges.findings[0].severity,
    "medium",
  );
  assert.doesNotMatch(JSON.stringify(assessment), /historical-rest-finding/);
  assert.equal(
    assessment.dimensions.semanticUnderstanding.items[0].intent,
    "Update the widget read response.",
  );
  assert.deepEqual(
    assessment.dimensions.semanticUnderstanding.items[0].restRepresentation
      .operations[0],
    materialization().dimensions.semanticUnderstanding.items[0]
      .restRepresentation.operations[0],
  );
  assert.deepEqual(
    assessment.dimensions.semanticUnderstanding.items[0].changes[0]
      .typeSpecDiffs,
    [typeSpecDiff],
  );
  assert.doesNotMatch(
    JSON.stringify(
      assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings,
    ),
    /\bTCGC\b|\bisFixed\b/,
  );
});

test("approved REST findings derive their generated-client impact", () => {
  const input = modelInput();
  const value = judgment();
  for (const decision of value.downstreamCandidates) {
    decision.decision = "reject";
    delete decision.severity;
  }
  const assessment = assembleAssessment({
    modelInput: input,
    judgment: value,
    materialization: materialization(),
  });
  assert.deepEqual(
    assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings.map(
      (finding) => finding.id,
    ),
    ["derived-rest-contract-sdk-impact"],
  );
  assert.equal(
    assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings[0]
      .severity,
    "medium",
  );
});

test("compliance findings use bounded documents and deterministic source", () => {
  const value = judgment();
  value.compliance = {
    status: "failed",
    rationale:
      "The fetched model guidance applies to the changed declaration.",
    documentUrls: ["https://typespec.io/docs/language-basics/models/"],
    findings: [
      {
        id: "model-guidance-gap",
        title: "Changed model does not follow the documented pattern",
        severity: "medium",
        summary: "The changed declaration differs from the documented model pattern.",
        documentationUrl:
          "https://typespec.io/docs/language-basics/models/",
        evidence: [
          "The fetched guidance and changed declaration use different patterns.",
        ],
        sourceChangeIds: ["source-widget"],
        sourcePaths: [],
      },
    ],
  };
  const assessment = assembleAssessment({
    modelInput: modelInput(),
    judgment: value,
    materialization: materialization(),
  });
  const compliance = assessment.dimensions.azureCompliance;
  assert.equal(compliance.status, "failed");
  assert.equal(compliance.documents[0].expectedCodeStatus, "available");
  assert.deepEqual(compliance.documents[0].expectedCodeSnippets[0].lines, [
    "model Widget {",
    "  name: string;",
    "}",
  ]);
  assert.deepEqual(compliance.findings[0].sourceReferences, [sourceReference]);
  assert.deepEqual(compliance.findings[0].codeSnippets[0].lines, [
    "interface Widgets {",
    '  @doc("New behavior")',
    "  get is ArmResourceRead<Widget>;",
  ]);
  const html = renderAssessmentHtml(assessment);
  assert.match(
    html,
    /The fetched guidance and changed declaration use different patterns\./,
  );
  assert.doesNotMatch(
    html.match(
      /<details class="comparison-details actual-details">[\s\S]*?<\/details>/,
    )[0],
    /The changed declaration follows the fetched TypeSpec model guidance\./,
  );
  assert.deepEqual(validateAssessment(assessment), []);
});

test("compliance finding evidence accepts a non-empty string", () => {
  const value = judgment();
  value.compliance = {
    status: "failed",
    rationale: "The fetched guidance applies.",
    documentUrls: ["https://typespec.io/docs/language-basics/models/"],
    findings: [
      {
        id: "string-evidence",
        title: "Changed model differs from guidance",
        severity: "medium",
        summary: "The changed declaration differs from the documented pattern.",
        documentationUrl:
          "https://typespec.io/docs/language-basics/models/",
        evidence: "The changed declaration uses a different model pattern.",
        sourceChangeIds: ["source-widget"],
        sourcePaths: [],
      },
    ],
  };
  const assessment = assembleAssessment({
    modelInput: modelInput(),
    judgment: value,
    materialization: materialization(),
  });
  assert.deepEqual(
    assessment.dimensions.azureCompliance.findings[0].evidence,
    ["The changed declaration uses a different model pattern."],
  );
});

test("assembler writes renderer output that passes report validation", () => {
  const root = mkdtempSync(join(scriptDirectory, ".assembler-test-"));
  try {
    const inputPath = join(root, "model-input.json");
    const judgmentPath = join(root, "assessment-judgment.json");
    const materializationPath = join(root, "materialization.json");
    const outputDirectory = join(root, "output");
    writeFileSync(inputPath, JSON.stringify(modelInput()));
    writeFileSync(judgmentPath, JSON.stringify(judgment()));
    writeFileSync(materializationPath, JSON.stringify(materialization()));

    const result = spawnSync(process.execPath, [
      join(scriptDirectory, "assemble-assessment.mjs"),
      inputPath,
      judgmentPath,
      materializationPath,
      outputDirectory,
      "--judgment-elapsed-ms",
      "45",
    ], {
      encoding: "utf8",
    });
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /Assembled and validated assessment for PR 123/);
    const html = readFileSync(join(outputDirectory, "assessment.html"), "utf8");

    assert.equal(existsSync(join(outputDirectory, "assessment.json")), false);
    assert.equal(existsSync(join(outputDirectory, "assessment.md")), false);
    assert.match(html, /<!doctype html>/);
    assert.match(html, /Update widget response/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("CLI materializes operations found only in retained artifact evidence", () => {
  const root = mkdtempSync(join(scriptDirectory, ".assembler-test-"));
  try {
    const { input, value } = catalogOnlyInputs();
    const inputPath = join(root, "model-input.json");
    const judgmentPath = join(root, "assessment-judgment.json");
    const materializationPath = join(root, "materialization.json");
    const evidenceDirectory = join(root, "evidence");
    const outputDirectory = join(root, "output");
    const artifactRoot = join(
      evidenceDirectory,
      "artifacts",
      "specification__widget",
    );
    for (const [revision, responseSchema] of [
      ["base", "WidgetList"],
      ["head", "UpdatedWidgetList"],
    ]) {
      const autorest = join(artifactRoot, revision, "autorest");
      mkdirSync(autorest, { recursive: true });
      writeFileSync(
        join(autorest, "openapi.json"),
        JSON.stringify(openApiDocument(responseSchema)),
      );
    }
    mkdirSync(evidenceDirectory, { recursive: true });
    writeFileSync(
      join(evidenceDirectory, "evidence.json"),
      JSON.stringify({
        schemaVersion: 1,
        baseline: input.baseline,
        head: input.head,
        sourceReferences: [sourceReference],
        projects: [{ path: "specification/widget" }],
      }),
    );
    writeFileSync(inputPath, JSON.stringify(input));
    writeFileSync(judgmentPath, JSON.stringify(value));
    writeFileSync(materializationPath, JSON.stringify(materialization()));

    assert.throws(
      () =>
        assembleAssessment({
          modelInput: input,
          judgment: value,
          materialization: materialization(),
        }),
      /missing complete operation contract.*Widgets_List/,
    );

    const assessment = assembleAssessmentFiles({
      modelInputPath: inputPath,
      judgmentPath,
      materializationPath,
      outputDirectory,
      evidenceDirectory,
    });
    const operations =
      assessment.dimensions.semanticUnderstanding.items[0].restRepresentation
        .operations;
    assert.deepEqual(
      operations.map((operation) => operation.operationId),
      ["Widgets_List", "Widgets_List"],
    );
    assert.deepEqual(
      operations.map((operation) => operation.artifactEvidence.revision),
      ["baseline", "head"],
    );
    assert.match(operations[0].responsePayloads[0], /WidgetList/);
    assert.match(operations[1].responsePayloads[0], /UpdatedWidgetList/);
    assert.deepEqual(validateAssessment(assessment), []);
    assert.equal(existsSync(join(outputDirectory, "assessment.json")), false);
    assert.equal(existsSync(join(outputDirectory, "assessment.md")), false);
    assert.ok(existsSync(join(outputDirectory, "assessment.html")));
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("timing includes deterministic, judgment, assembly, and render phases", () => {
  const assessment = assembleAssessment({
    modelInput: modelInput(),
    judgment: judgment(),
    materialization: materialization(),
    judgmentElapsedMs: 45,
    deterministicAssemblyMs: 6,
    renderMs: 4,
  });
  assert.deepEqual(assessment.assessmentDuration.phases, {
    deterministicPreparationMs: 120,
    deterministicAssemblyMs: 6,
    judgmentMs: 45,
    renderMs: 4,
  });
  assert.equal(assessment.assessmentDuration.totalMs, 175);
  assert.equal(assessment.assessmentDuration.breakdown.totalMs, 175);
  assert.equal(
    assessment.assessmentDuration.breakdown.semanticUnderstandingQuality,
    "estimated",
  );
  assert.equal(
    assessment.assessmentDuration.breakdown.complianceQuality,
    "estimated/measured",
  );
  assert.equal(
    assessment.assessmentDuration.breakdown.semanticUnderstandingMs +
      assessment.assessmentDuration.breakdown.restBreakingMs +
      assessment.assessmentDuration.breakdown.downstreamBreakingMs +
      assessment.assessmentDuration.breakdown.complianceMs +
      assessment.assessmentDuration.breakdown.overheadMs,
    175,
  );
});

test("canonical report directories cannot be overwritten", () => {
  const root = mkdtempSync(join(scriptDirectory, ".assembler-test-"));
  try {
    const inputPath = join(root, "model-input.json");
    const judgmentPath = join(root, "assessment-judgment.json");
    const materializationPath = join(root, "materialization.json");
    writeFileSync(inputPath, JSON.stringify(modelInput()));
    writeFileSync(judgmentPath, JSON.stringify(judgment()));
    writeFileSync(materializationPath, JSON.stringify(materialization()));
    const canonicalOutput = join(
      scriptDirectory,
      "..",
      "test-evidence",
      "assessments",
      "123",
    );
    mkdirSync(dirname(canonicalOutput), { recursive: true });
    assert.throws(
      () =>
        assembleAssessmentFiles({
          modelInputPath: inputPath,
          judgmentPath,
          materializationPath,
          outputDirectory: canonicalOutput,
        }),
      /Refusing to overwrite canonical report directory/,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
