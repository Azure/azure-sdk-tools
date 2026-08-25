import assert from "node:assert/strict";
import test from "node:test";

import {
  renderFastAssessmentHtml,
  validateFastJudgment,
} from "./assemble-fast-assessment.mjs";

function input() {
  return {
    schemaVersion: 1,
    mode: "impact-only",
    changedFiles: ["spec/main.tsp"],
    sourceFiles: [
      {
        path: "spec/main.tsp",
        changes: [
          {
            id: "source-change",
            newStart: 10,
            lines: [
              "@added(Versions.v2)",
              "get is ArmResourceRead<NewWidget>;",
            ],
            diffLines: [
              {
                kind: "remove",
                text: "get is ArmResourceRead<Widget>;",
              },
              {
                kind: "add",
                text: "get is ArmResourceRead<NewWidget>;",
              },
            ],
          },
        ],
      },
    ],
    projects: [
      {
        path: "spec",
        rest: {
          operationChanges: [
            {
              operationId: "Widgets_Get",
            },
          ],
          breakingCandidates: [
            {
              id: "rest-change",
              rule: "response-contract-changed",
            },
          ],
        },
        downstream: { candidates: [] },
      },
    ],
    complianceEvidence: { documents: [] },
  };
}

function judgment() {
  return {
    schemaVersion: 1,
    restCandidates: [
      {
        id: "rest-change",
        decision: "approve",
        title: "Widget response changes",
        severity: "high",
        confidence: "high",
        actual: "The response uses NewWidget.",
        expected: "The released response must remain Widget.",
        evidence: ["The response schema differs in the same API version."],
        affectedOperationIds: ["Widgets_Get"],
        sourceChangeIds: ["source-change"],
        sourcePaths: [],
      },
    ],
    downstreamCandidates: [],
    compliance: {
      status: "passed",
      rationale: "No documented mismatch was found.",
      findings: [],
    },
    overallConfidence: "high",
    blockers: [],
  };
}

function downstreamJudgment() {
  const value = judgment();
  const approved = value.restCandidates[0];
  value.restCandidates = [];
  value.downstreamCandidates = [
    {
      ...approved,
      id: "downstream-change",
    },
  ];
  return value;
}

test("fast assessment renders full-style REST finding cards", () => {
  const modelInput = input();
  const value = judgment();
  assert.doesNotThrow(() => validateFastJudgment(value, modelInput));
  const html = renderFastAssessmentHtml(modelInput, value);
  assert.match(html, /Fast TypeSpec Assessment/);
  assert.match(html, /The response uses NewWidget/);
  assert.match(html, /TypeSpec source/);
  assert.match(html, /class="hero"/);
  assert.match(html, /<nav>/);
  assert.match(html, /Overall code safety/);
  assert.doesNotMatch(html, /Semantic intents/);
  assert.doesNotMatch(html, /Actual behavior|Expected behavior/);
  assert.match(html, /white-space:pre-wrap/);
});

test("fast assessment requires a decision for every candidate", () => {
  const value = judgment();
  value.restCandidates = [];
  assert.throws(
    () => validateFastJudgment(value, input()),
    /leaves candidate\(s\) undecided: rest-change/,
  );
});

test("fast assessment accepts affected operations retained in operation groups", () => {
  const modelInput = input();
  modelInput.projects[0].rest.operationChanges = [];
  modelInput.projects[0].rest.operationGroups = [
    {
      id: "version-modified-v2-widgets",
      operationIds: ["Widgets_Get"],
    },
  ];
  assert.doesNotThrow(() => validateFastJudgment(judgment(), modelInput));
});

test("fast downstream findings match full finding cards and add source diff", () => {
  const modelInput = input();
  modelInput.projects[0].rest.breakingCandidates = [];
  modelInput.projects[0].downstream.candidates = [
    {
      id: "downstream-change",
      rule: "sdk-shape-changed",
    },
  ];
  const html = renderFastAssessmentHtml(modelInput, downstreamJudgment());
  assert.match(html, /<p>The response uses NewWidget\.<\/p>/);
  assert.match(
    html,
    /<p><strong>Evidence:<\/strong> The response schema differs in the same API version\.<\/p>/,
  );
  assert.match(html, /<strong>TypeSpec source:<\/strong>/);
  assert.match(html, /TypeSpec code change/);
  assert.match(html, /<details class="source-details" open>/);
  assert.doesNotMatch(html, /Actual behavior|Expected behavior/);
  assert.match(html, /diff-line remove/);
  assert.match(html, /diff-line add/);
});

test("fast downstream findings omit unrelated source hunks", () => {
  const modelInput = input();
  modelInput.sourceFiles[0].changes[0].diffLines = [
    {
      kind: "remove",
      text: "@Azure.ClientGenerator.Core.Legacy.flattenProperty",
    },
    {
      kind: "add",
      text: '@Azure.ClientGenerator.Core.Legacy.flattenProperty("!javascript")',
    },
  ];
  modelInput.sourceFiles[0].changes.push({
    id: "unrelated-source-change",
    newStart: 50,
    lines: ["@added(Versions.v2)"],
    diffLines: [{ kind: "add", text: "@added(Versions.v2)" }],
  });
  modelInput.projects[0].rest.breakingCandidates = [];
  modelInput.projects[0].downstream.candidates = [
    {
      id: "downstream-change",
      rule: "client-property-flattening-changed",
      summary: "Flattening excludes JavaScript.",
      evidence: [{ path: "spec/main.tsp" }],
    },
  ];
  const value = downstreamJudgment();
  value.downstreamCandidates[0].sourceChangeIds.push(
    "unrelated-source-change",
  );
  const html = renderFastAssessmentHtml(modelInput, value);
  assert.match(html, /flattenProperty/);
  assert.doesNotMatch(html, /@added\(Versions\.v2\)/);
});

test("fast compliance uses the full report comparison structure", () => {
  const modelInput = input();
  const documentationUrl = "https://example.com/guidance";
  modelInput.complianceEvidence.documents = [{ url: documentationUrl }];
  const value = judgment();
  value.restCandidates = [
    {
      id: "rest-change",
      decision: "reject",
      rationale: "The candidate is not breaking.",
    },
  ];
  const { decision, ...finding } = judgment().restCandidates[0];
  value.compliance = {
    status: "failed",
    rationale: "The changed declaration differs from guidance.",
    findings: [
      {
        ...finding,
        id: "compliance-change",
        documentationUrl,
      },
    ],
  };
  const html = renderFastAssessmentHtml(modelInput, value);
  assert.match(html, /class="finding compliance-finding/);
  assert.match(html, /<strong>Gap:<\/strong>/);
  assert.match(html, /class="comparison-details expected-details"/);
  assert.match(html, /class="comparison-details actual-details"/);
  assert.match(html, /<summary>Expected<\/summary>/);
  assert.match(html, /<summary>Actual<\/summary>/);
  assert.match(html, /The response uses NewWidget/);
  assert.match(html, /The released response must remain Widget/);
  assert.match(html, /diff-line remove/);
  assert.match(html, /diff-line add/);
});
