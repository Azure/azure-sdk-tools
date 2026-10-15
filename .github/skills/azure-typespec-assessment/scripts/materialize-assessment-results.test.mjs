import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { buildAgentWorkspace } from "./build-agent-workspace.mjs";
import { readJson, writeJson } from "./cli.mjs";
import {
  materializeAssessmentResults,
  validateCompactDecisions,
} from "./materialize-assessment-results.mjs";

/** @typedef {import("./agent-decisions.schema.js").CatalogScore} CatalogScore */
/** @typedef {import("./agent-decisions.schema.js").FetchedDocument} FetchedDocument */
/** @typedef {import("./assessment-judgment.schema.js").TypeSpecAssessmentJudgment} AssessmentJudgment */
/** @typedef {import("./compliance-search-evidence.schema.js").TypeSpecAzureGuidelinesSearchEvidence} ComplianceSearchEvidence */
/** @typedef {import("./runtime-types.js").AssessmentModelInput} AssessmentModelInput */
/**
 * @typedef {{
 *   catalogScores: CatalogScore[],
 *   fetchedDocuments: FetchedDocument[],
 *   semanticSummaries: import("./agent-decisions.schema.js").SemanticSummary[],
 *   sdkNamingReview: import("./agent-decisions.schema.js").SdkNamingReview,
 *   overallConfidence: "high" | "medium" | "low",
 *   [key: string]: unknown
 * }} TestAgentDecisions
 */

/**
 * @param {{
 *   inference?: boolean,
 *   sourceChanges?: import("./runtime-types.js").SourceChange[],
 *   semanticReviewUnits?: (import("./runtime-types.js").ModelSemanticReviewUnit & {
 *     qualifiedNames?: string[]
 *   })[],
 *   evidenceSets?: Record<string, import("./runtime-types.js").EvidenceSet>,
 *   facts?: Record<string, import("./runtime-types.js").AssessmentFact>
 * }} options
 */
function fixture({
  inference = false,
  sourceChanges = [],
  semanticReviewUnits = [],
  evidenceSets = {},
  facts = {},
} = {}) {
  const work = fs.mkdtempSync(path.join(process.cwd(), ".materializer-test-"));
  writeJson(path.join(work, "preparation-manifest.json"), {
    comparison: {
      mergeBaseCommit: "base",
      headCommit: "head",
      baseRef: "main",
      workingTree: { staged: false, unstaged: false, untracked: false },
    },
    projects: [],
  });
  writeJson(path.join(work, "source", "source-index.json"), {
    sourceChanges,
  });
  writeJson(path.join(work, "dimensions", "semantic-intents-input.json"), {
    status: "ready",
    reviewUnits: [],
    facts: {},
    blockers: [],
  });
  writeJson(path.join(work, "dimensions", "rest-breaking-input.json"), {
    status: "ready",
    candidates: [],
    facts: {},
    blockers: [],
  });
  writeJson(path.join(work, "dimensions", "downstream-breaking-input.json"), {
    status: "ready",
    candidates: [],
    facts: {},
    blockers: [],
  });
  writeJson(path.join(work, "dimensions", "document-quality-input.json"), {
    schemaVersion: 4,
    reviewUnits: [],
    blockers: [],
  });
  writeJson(path.join(work, "model-input.json"), {
    schemaVersion: 2,
    context: {
      sourceComparison: {
        baseCommit: "base",
        headCommit: "head",
        baseRef: "main",
        workingTree: { staged: false, unstaged: false, untracked: false },
      },
      projects: [],
    },
    artifactReferences: {
      sourceIndex: "source/source-index.json",
      semanticReviewUnits: "dimensions/semantic-intents-input.json",
      restCandidates: "dimensions/rest-breaking-input.json",
      downstreamCandidates: "dimensions/downstream-breaking-input.json",
    },
    evidenceSets,
    facts,
    semanticReviewUnits,
    restCandidates: [],
    downstreamCandidates: [],
    downstreamRootCauses: [],
    complianceSearchRequests: [],
    inferenceRequests: inference
      ? [
          {
            requestId: "inference-request-1",
            reviewUnitId: "semantic-1",
            sourceChangeId: "source-1",
            hunkId: "hunk-1",
            relatedOperationIds: [],
            allowedDimensions: ["rest"],
          },
        ]
      : [],
    blockers: [],
  });
  buildAgentWorkspace({ work });
  return work;
}

/** @param {string} work */
function completedDecisions(work) {
  const decisions = /** @type {TestAgentDecisions} */ (
    readJson(path.join(work, "agent-workspace", "agent-decisions.draft.json"))
  );
  decisions.catalogScores = decisions.catalogScores.map((score) => ({
    ...score,
    rationale: "No changed semantic intent requires this document.",
  }));
  decisions.fetchedDocuments = decisions.catalogScores.slice(0, 4).map(({ catalogId }) => ({
    catalogId,
    retrievedAt: "2026-09-18T00:00:00.000Z",
    contentHash: `sha256:${"0".repeat(64)}`,
    bytes: 0,
    guidance: [],
    noRelevantGuidance: true,
  }));
  decisions.semanticSummaries = decisions.semanticSummaries.map((summary) => ({
    ...summary,
    title: "Change Widget naming",
    summary: "The changed Widget declaration affects its SDK-visible name.",
  }));
  decisions.overallConfidence = "high";
  decisions.sdkNamingReview = {
    summary: "C# ARM naming was reviewed.",
    coverage: [
      {
        language: "C#",
        serviceType: "arm",
        profile: "csharp-arm",
        status: "reviewed",
        rationale: "The supplied project is an ARM service and the C# ARM profile applies.",
      },
    ],
    findings: [],
    blockers: [],
  };
  return decisions;
}

/** @returns {import("./runtime-types.js").SourceChange} */
function sourceChange() {
  return {
    id: "source-1",
    path: "specification/widget/models.tsp",
    status: "modified",
    origins: ["committed"],
    hunks: [],
    declarations: [
      {
        id: "declaration-1",
        kind: "modelProperty",
        qualifiedName: "ScenarioParameter.required",
        decorators: [],
        versionedMembers: [],
        hunkIds: ["hunk-1"],
        source: {
          revision: "current",
          startLine: 12,
          endLine: 12,
          link: "https://example.test/specification/widget/models.tsp#L12",
        },
      },
    ],
  };
}

/** @returns {import("./runtime-types.js").ModelSemanticReviewUnit & {qualifiedNames: string[]}} */
function semanticUnit() {
  return {
    reviewUnitId: "semantic-1",
    qualifiedNames: ["ScenarioParameter.required"],
    evidenceSetId: "evidence-semantic-1",
  };
}

/**
 * @param {Partial<import("./agent-decisions.schema.js").SdkNamingFinding>} overrides
 * @returns {import("./agent-decisions.schema.js").SdkNamingFinding}
 */
function namingFinding(overrides = {}) {
  return {
    declarationId: "declaration-1",
    declaration: "ScenarioParameter.required",
    currentSdkName: "Required",
    recommendedSdkName: "IsRequired",
    languageScope: "C#",
    decision: "recommend",
    rule: "Boolean properties should use an Is prefix.",
    rationale: "The generated member represents a boolean state.",
    compatibilityEvidence: "A later supplied review commit applies the same rename.",
    verification: "proposed",
    ...overrides,
  };
}

/**
 * @param {TestAgentDecisions} decisions
 * @param {Record<string, unknown>} finding
 */
function setInvalidNamingFinding(decisions, finding) {
  decisions.sdkNamingReview.findings = /** @type {import("./agent-decisions.schema.js").SdkNamingFinding[]} */ ([
    finding,
  ]);
}

void test("materializes guideline evidence and judgment without inference", () => {
  const work = fixture();
  try {
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), completedDecisions(work));
    const result = materializeAssessmentResults({ work });
    assert.equal(result.inferencePath, null);
    assert.equal(fs.existsSync(path.join(work, "compliance-search-evidence.json")), true);
    const judgment = /** @type {AssessmentJudgment} */ (readJson(result.judgmentPath));
    assert.equal(judgment.schemaVersion, 1);
    assert.deepEqual(judgment.complianceDecisions, []);
    assert.equal(judgment.sdkNaming?.status, "passed");
    assert.equal(judgment.sdkNaming?.coverage[0].profile, "csharp-arm");
    const evidence = /** @type {ComplianceSearchEvidence} */ (
      readJson(path.join(work, "compliance-search-evidence.json"))
    );
    assert.deepEqual(evidence.catalogRanking, []);
    assert.deepEqual(evidence.rankedDocuments, []);
    assert.equal(evidence.inputAccounting.catalogEntriesScored, 0);
    assert.equal(
      JSON.stringify(readJson(path.join(work, "workflow-state.json"))).includes("guideline"),
      true,
    );
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});

void test("materializes SDK naming findings with stable IDs and evidence state", () => {
  const work = fixture({
    sourceChanges: [sourceChange()],
    semanticReviewUnits: [semanticUnit()],
    evidenceSets: {
      "evidence-semantic-1": {
        sourceChangeIds: ["source-1"],
        evidenceFactIds: [],
      },
    },
  });
  try {
    const decisions = completedDecisions(work);
    decisions.sdkNamingReview.findings = [
      namingFinding({ reviewUnitId: "semantic-1" }),
    ];
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
    const result = materializeAssessmentResults({ work });
    const judgment = /** @type {AssessmentJudgment} */ (readJson(result.judgmentPath));
    assert.equal(judgment.sdkNaming?.status, "failed");
    assert.match(judgment.sdkNaming?.findings[0].id ?? "", /^sdk-naming-[0-9a-f]{16}$/);
    assert.equal(judgment.sdkNaming?.findings[0].recommendedSdkName, "IsRequired");
    assert.equal(judgment.sdkNaming?.findings[0].verification, "proposed");
    assert.equal(
      judgment.sdkNaming?.findings[0].sourceLocation,
      "specification/widget/models.tsp:12",
    );
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});

void test("materializes SDK naming findings backed by referenced SDK artifacts", () => {
  const work = fixture({
    evidenceSets: {
      "evidence-sdk-1": {
        sourceChangeIds: [],
        evidenceFactIds: ["sdk-fact-1"],
      },
    },
    facts: {
      "sdk-fact-1": {
        comparisonRole: "target",
        factKind: "model",
        crossLanguageDefinitionId: "Widget.ScenarioParameter",
        name: "ScenarioParameter",
        properties: [{ name: "Required", serializedName: "required" }],
      },
    },
  });
  try {
    const decisions = completedDecisions(work);
    decisions.sdkNamingReview.findings = [
      namingFinding({
        declarationId: undefined,
        sdkArtifactFactId: "sdk-fact-1",
      }),
    ];
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
    const result = materializeAssessmentResults({ work });
    const judgment = /** @type {AssessmentJudgment} */ (readJson(result.judgmentPath));
    assert.equal(judgment.sdkNaming?.findings[0].sdkArtifactFactId, "sdk-fact-1");
    assert.equal(judgment.sdkNaming?.findings[0].sourceLocation, undefined);
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});

/** @type {{
 *   name: string,
 *   configure: (finding: import("./agent-decisions.schema.js").SdkNamingFinding) => Record<string, unknown>,
 *   message: RegExp
 * }[]} */
const invalidNamingFindingCases = [
  {
    name: "missing canonical evidence",
    configure: (finding) => ({ ...finding, declarationId: undefined }),
    message: /must reference canonical declaration or SDK artifact evidence/,
  },
  {
    name: "unknown declaration with an empty source index",
    configure: (finding) => finding,
    message: /references unknown declaration declaration-1/,
  },
  {
    name: "fabricated declaration text",
    configure: (finding) => ({ ...finding, declaration: "Unrelated.Nonexistent.enabled" }),
    message: /does not match canonical declaration/,
  },
  {
    name: "model-supplied source location",
    configure: (finding) => ({ ...finding, sourceLocation: "nonexistent.tsp:999" }),
    message: /contains unknown fields: sourceLocation/,
  },
];
for (const testCase of invalidNamingFindingCases) {
  void test(`rejects SDK naming findings with ${testCase.name}`, () => {
    const needsSource =
      testCase.name !== "unknown declaration with an empty source index";
    const work = fixture({
      sourceChanges: needsSource ? [sourceChange()] : [],
    });
    try {
      const decisions = completedDecisions(work);
      setInvalidNamingFinding(decisions, testCase.configure(namingFinding()));
      writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
      assert.throws(
        () => materializeAssessmentResults({ work }),
        testCase.message,
      );
    } finally {
      fs.rmSync(work, { recursive: true, force: true });
    }
  });
}

void test("rejects declaration evidence outside the referenced review unit", () => {
  const work = fixture({
    sourceChanges: [sourceChange()],
    semanticReviewUnits: [semanticUnit()],
    evidenceSets: {
      "evidence-semantic-1": {
        sourceChangeIds: ["source-other"],
        evidenceFactIds: [],
      },
    },
  });
  try {
    const decisions = completedDecisions(work);
    decisions.sdkNamingReview.findings = [
      namingFinding({ reviewUnitId: "semantic-1" }),
    ];
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
    assert.throws(
      () => materializeAssessmentResults({ work }),
      /is not owned by review unit semantic-1/,
    );
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});

/** @type {{
 *   name: string,
 *   facts: Record<string, import("./runtime-types.js").AssessmentFact>,
 *   evidenceFactIds: string[],
 *   message: RegExp
 * }[]} */
const invalidSdkArtifactCases = [
  {
    name: "unknown SDK artifact",
    facts: {},
    evidenceFactIds: ["sdk-fact-1"],
    message: /unknown or non-SDK artifact sdk-fact-1/,
  },
  {
    name: "non-SDK fact",
    facts: {
      "sdk-fact-1": {
        factKind: "http-operation",
        operationId: "Widgets_Get",
      },
    },
    evidenceFactIds: ["sdk-fact-1"],
    message: /unknown or non-SDK artifact sdk-fact-1/,
  },
  {
    name: "unreferenced SDK artifact",
    facts: {
      "sdk-fact-1": {
        comparisonRole: "target",
        factKind: "model",
        name: "ScenarioParameter",
      },
    },
    evidenceFactIds: [],
    message: /is not referenced by canonical evidence/,
  },
];
for (const testCase of invalidSdkArtifactCases) {
  void test(`rejects ${testCase.name} evidence for SDK naming findings`, () => {
    const work = fixture({
      evidenceSets: {
        "evidence-sdk-1": {
          sourceChangeIds: [],
          evidenceFactIds: testCase.evidenceFactIds,
        },
      },
      facts: testCase.facts,
    });
    try {
      const decisions = completedDecisions(work);
      decisions.sdkNamingReview.findings = [
        namingFinding({
          declarationId: undefined,
          sdkArtifactFactId: "sdk-fact-1",
        }),
      ];
      writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
      assert.throws(
        () => materializeAssessmentResults({ work }),
        testCase.message,
      );
    } finally {
      fs.rmSync(work, { recursive: true, force: true });
    }
  });
}

void test("materializes unsupported naming coverage as not assessed", () => {
  const work = fixture();
  try {
    const decisions = completedDecisions(work);
    decisions.sdkNamingReview = {
      summary: "Java data-plane naming is outside the available naming profile.",
      coverage: [
        {
          language: "Java",
          serviceType: "data-plane",
          status: "not-covered",
          rationale: "No applicable profile is available.",
        },
      ],
      findings: [],
      blockers: ["sdk-naming-profile-unavailable: Java data-plane is not covered."],
    };
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
    const result = materializeAssessmentResults({ work });
    const judgment = /** @type {AssessmentJudgment} */ (readJson(result.judgmentPath));
    assert.equal(judgment.sdkNaming?.status, "not-assessed");
    assert.equal(judgment.sdkNaming?.coverage[0].status, "not-covered");
    assert.equal(judgment.sdkNaming?.blockers.length, 1);
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});

void test("rejects naming recommendations without a recommended SDK name", () => {
  const work = fixture();
  try {
    const decisions = completedDecisions(work);
    decisions.sdkNamingReview.findings = [
      /** @type {import("./agent-decisions.schema.js").SdkNamingFinding} */ ({
        declarationId: "declaration-1",
        declaration: "ScenarioParameter.required",
        currentSdkName: "Required",
        languageScope: "C#",
        decision: "recommend",
        rule: "Boolean properties should use an Is prefix.",
        rationale: "The generated member represents a boolean state.",
        compatibilityEvidence: "No shipped SDK name was supplied.",
        verification: "proposed",
      }),
    ];
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
    assert.throws(
      () => materializeAssessmentResults({ work }),
      /recommendedSdkName/,
    );
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});

void test("rejects unresolved SDK naming draft blockers", () => {
  const work = fixture();
  try {
    const decisions = completedDecisions(work);
    decisions.sdkNamingReview = {
      summary: "SDK naming review is incomplete.",
      coverage: [],
      findings: [],
      blockers: ["unresolved: record SDK naming coverage"],
    };
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), decisions);
    assert.throws(
      () => materializeAssessmentResults({ work }),
      /SDK naming review\.blockers\[0\] is unresolved/,
    );
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});

void test("rejects unknown fields in compact decisions", () => {
  assert.throws(
    () =>
      validateCompactDecisions({
        schemaVersion: 1,
        semanticSummaries: [],
        restDecisions: [],
        downstreamDecisions: [],
        catalogScores: [],
        fetchedDocuments: [],
        failedRetrievals: [],
        searchBlockers: [],
        complianceJudgments: [],
        overallConfidence: "high",
        blockers: [],
        complianceDecisions: [],
      }),
    /unknown fields: complianceDecisions/,
  );
});

void test("rejects changed canonical input before materialization", () => {
  const work = fixture();
  try {
    writeJson(path.join(work, "agent-workspace", "agent-decisions.json"), completedDecisions(work));
    const input = /** @type {AssessmentModelInput} */ (
      readJson(path.join(work, "model-input.json"))
    );
    /** @type {unknown[]} */ (input.blockers).push("changed");
    writeJson(path.join(work, "model-input.json"), input);
    assert.throws(
      () => materializeAssessmentResults({ work }),
      /Canonical assessment inputs changed/,
    );
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
});
