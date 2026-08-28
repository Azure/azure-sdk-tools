import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { assembleAssessment } from "./assemble-assessment.mjs";
import { readJson, writeJson } from "./cli.mjs";
import { validateAssessment } from "./validate-assessment.mjs";

function fixture() {
  const work = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-assessment-"));
  writeJson(path.join(work, "preparation-manifest.json"), {
    schemaVersion: 1,
    repository: { root: "repo" },
    comparison: {
      baseRef: "origin/main",
      mergeBaseCommit: "base",
      headCommit: "head",
      workingTree: {},
    },
    changedFiles: [{ path: "specification/a/main.tsp", origins: ["unstaged"] }],
    projects: [],
    blockers: [],
    timings: {},
  });
  writeJson(path.join(work, "source", "source-index.json"), {
    sourceChanges: [{
      id: "source-1",
      path: "specification/a/main.tsp",
      hunks: [{ id: "hunk-1" }],
      declarations: [{
        source: { revision: "current", startLine: 1, endLine: 2 },
      }],
    }],
  });
  writeJson(path.join(work, "dimensions", "semantic-intents-input.json"), {
    status: "ready",
    facts: {
      "operation-1": {
        id: "operation-1",
        revision: "current",
        operationId: "Widgets_Get",
        apiVersion: "v1",
        method: "get",
        path: "/widgets",
      },
      "operation-2": {
        id: "operation-2",
        revision: "current",
        operationId: "Widgets_List",
        apiVersion: "v1",
        method: "get",
        path: "/widgets/all",
      },
    },
    reviewUnits: [{
      id: "semantic-1",
      action: "modify",
      sourceChangeIds: ["source-1"],
      hunkIds: ["hunk-1"],
      operationIds: ["operation-1", "operation-2"],
    }],
    blockers: [],
  });
  writeJson(path.join(work, "dimensions", "rest-breaking-input.json"), {
    status: "ready",
    facts: { "rest-fact-1": { id: "rest-fact-1" } },
    candidates: [{
      id: "rest-1",
      rule: "required-property-added",
      actual: "mode is required",
      expected: "mode remains optional",
      operationIds: ["Widgets_Get"],
      sourceChangeIds: ["source-1"],
      evidenceFactIds: ["rest-fact-1"],
    }],
    blockers: [],
  });
  writeJson(path.join(work, "dimensions", "downstream-breaking-input.json"), {
    status: "ready",
    facts: {},
    candidates: [],
    blockers: [],
  });
  return work;
}

test("assembler joins approved evidence and derives scoped safety", () => {
  const work = fixture();
  const assessment = assembleAssessment({
    work,
    judgment: {
      schemaVersion: 1,
      semanticIntents: [{
        reviewUnitId: "semantic-1",
        title: "Require mode",
        summary: "The request now requires mode.",
        sourceChangeIds: ["source-1"],
        operationIds: ["operation-1"],
      }],
      restDecisions: [{
        candidateId: "rest-1",
        decision: "approve",
        severity: "high",
        rationale: "Existing requests fail.",
      }],
      downstreamDecisions: [],
      overallConfidence: "high",
      blockers: [],
    },
  });
  assert.equal(assessment.safety.status, "failed");
  assert.equal(assessment.dimensions.semantic.items[0].action, "add");
  assert.equal(assessment.dimensions.semantic.items[0].changeKind, "add");
  assert.deepEqual(
    assessment.dimensions.semantic.items[0].operations.map((operation) => operation.operationId),
    ["Widgets_Get"],
  );
  assert.equal(assessment.dimensions.rest.findings.length, 1);
  assert.deepEqual(validateAssessment(assessment), []);
});

test("assembler rejects incomplete candidate coverage", () => {
  const work = fixture();
  assert.throws(
    () =>
      assembleAssessment({
        work,
        judgment: {
          schemaVersion: 1,
          semanticIntents: [{
            reviewUnitId: "semantic-1",
            title: "Title",
            summary: "Summary",
            sourceChangeIds: ["source-1"],
            operationIds: ["operation-1"],
          }],
          restDecisions: [],
          downstreamDecisions: [],
          overallConfidence: "high",
          blockers: [],
        },
      }),
    /coverage mismatch/,
  );
});

test("aggregates direct SDK deltas by method and links them to semantic REST operations", () => {
  const work = fixture();
  const before = {
    id: "sdk-before",
    projectId: "project-1",
    revision: "base",
    factKind: "method",
    kind: "basic",
    crossLanguageDefinitionId: "Contoso.Widgets.get",
    parameters: [{ name: "id", type: "string" }],
    responseType: undefined,
    operation: { verb: "get", path: "/widgets" },
    apiVersions: ["v1"],
  };
  const after = {
    ...before,
    id: "sdk-after",
    revision: "current",
    kind: "lro",
    responseType: "Widget",
    lro: {
      finalStateVia: "location",
      operation: { kind: "http", path: "/widgets", verb: "get" },
      logicalResult: { kind: "model", name: "Widget" },
    },
  };
  const candidate = (id, rule) => ({
    id,
    rule,
    actual: `${rule} actual`,
    expected: `${rule} expected`,
    crossLanguageDefinitionId: "Contoso.Widgets.get",
    sourceChangeIds: ["source-1"],
    evidenceFactIds: ["sdk-before", "sdk-after"],
  });
  writeJson(path.join(work, "dimensions", "downstream-breaking-input.json"), {
    status: "ready",
    facts: { "sdk-before": before, "sdk-after": after },
    candidates: [
      candidate("downstream-kind", "method-kind-changed"),
      candidate("downstream-response", "method-response-changed"),
      candidate("downstream-lro", "method-lro-changed"),
    ],
    blockers: [],
  });
  const assessment = assembleAssessment({
    work,
    judgment: {
      schemaVersion: 1,
      semanticIntents: [{
        reviewUnitId: "semantic-1",
        title: "Modify get",
        summary: "Modify the SDK projection while preserving REST.",
        sourceChangeIds: ["source-1"],
        operationIds: ["operation-1"],
      }],
      restDecisions: [{
        candidateId: "rest-1",
        decision: "reject",
        rationale: "REST remains compatible.",
      }],
      downstreamDecisions: [
        {
          candidateId: "downstream-kind",
          decision: "approve",
          severity: "high",
          rationale: "Method kind changed.",
        },
        {
          candidateId: "downstream-response",
          decision: "approve",
          severity: "high",
          rationale: "Response changed.",
        },
        {
          candidateId: "downstream-lro",
          decision: "approve",
          severity: "medium",
          rationale: "LRO behavior changed.",
        },
      ],
      overallConfidence: "high",
      blockers: [],
    },
  });

  assert.equal(assessment.dimensions.downstream.operationGroups.length, 1);
  assert.equal(assessment.dimensions.downstream.operationGroups[0].deltas.length, 3);
  assert.equal(assessment.dimensions.downstream.operationGroups[0].operationId, "Widgets_Get");
  assert.equal(assessment.dimensions.downstream.operationGroups[0].parametersUnchanged, true);
  assert.equal(assessment.dimensions.downstream.operationGroups[0].deltas[0].before, "basic");
  assert.equal(assessment.dimensions.downstream.operationGroups[0].deltas[0].after, "lro");
  assert.deepEqual(
    assessment.dimensions.semantic.items[0].relatedFindings.downstream,
    [assessment.dimensions.downstream.operationGroups[0].id],
  );
  assert.deepEqual(validateAssessment(assessment), []);
});

test("assembles changed-only parameters and suppresses URI-template-only LRO deltas", () => {
  const work = fixture();
  const parameter = (name, kind, optional = false) => ({
    name,
    optional,
    onClient: false,
    isApiVersionParam: false,
    type: { kind },
  });
  const before = {
    id: "sdk-before",
    projectId: "project-1",
    revision: "base",
    factKind: "method",
    kind: "lro",
    crossLanguageDefinitionId: "Contoso.Widgets.get",
    parameters: [
      parameter("subscriptionId", "string"),
      parameter("resourceGroupName", "string"),
      parameter("widgetName", "string"),
    ],
    operation: { verb: "get", path: "/widgets" },
    apiVersions: ["v1"],
    lro: {
      finalStateVia: "azure-async-operation",
      operation: {
        kind: "http",
        path: "/widgets",
        verb: "get",
        uriTemplate: "/widgets?api-version",
      },
      logicalResult: { kind: "model", name: "Widget" },
    },
  };
  const after = {
    ...before,
    id: "sdk-after",
    revision: "current",
    parameters: [
      ...before.parameters,
      parameter("afcManagedSync", "boolean", true),
    ],
    lro: {
      ...before.lro,
      operation: {
        ...before.lro.operation,
        uriTemplate: "/widgets?api-version,afcManagedSync",
      },
    },
  };
  const candidate = (id, rule) => ({
    id,
    rule,
    actual: `${rule} actual`,
    expected: `${rule} expected`,
    crossLanguageDefinitionId: "Contoso.Widgets.get",
    sourceChangeIds: ["source-1"],
    evidenceFactIds: ["sdk-before", "sdk-after"],
  });
  writeJson(path.join(work, "dimensions", "downstream-breaking-input.json"), {
    status: "ready",
    facts: { "sdk-before": before, "sdk-after": after },
    candidates: [
      candidate("downstream-parameters", "method-parameters-changed"),
      candidate("downstream-lro", "method-lro-changed"),
    ],
    blockers: [],
  });
  const assessment = assembleAssessment({
    work,
    judgment: {
      schemaVersion: 1,
      semanticIntents: [{
        reviewUnitId: "semantic-1",
        title: "Modify get",
        summary: "Add an optional request parameter.",
        sourceChangeIds: ["source-1"],
        operationIds: ["operation-1"],
      }],
      restDecisions: [{
        candidateId: "rest-1",
        decision: "reject",
        rationale: "REST remains compatible.",
      }],
      downstreamDecisions: [
        {
          candidateId: "downstream-parameters",
          decision: "approve",
          severity: "high",
          rationale: "Public method parameters changed.",
        },
        {
          candidateId: "downstream-lro",
          decision: "approve",
          severity: "medium",
          rationale: "The source candidate duplicated URI metadata.",
        },
      ],
      overallConfidence: "high",
      blockers: [],
    },
  });

  const group = assessment.dimensions.downstream.operationGroups[0];
  assert.equal(assessment.dimensions.downstream.findings.length, 1);
  assert.equal(group.deltas.length, 1);
  assert.equal(group.deltas[0].field, "parameters");
  assert.equal(group.deltas[0].before, undefined);
  assert.equal(group.deltas[0].after, undefined);
  assert.deepEqual(
    group.deltas[0].changes.added.map((item) => item.parameter),
    [{
      name: "afcManagedSync",
      optional: true,
      onClient: false,
      isApiVersionParam: false,
      type: "boolean",
    }],
  );
  assert.equal(group.deltas[0].changes.unchangedCount, 3);
  assert.deepEqual(validateAssessment(assessment), []);
});

test("uses only version-governance hunks for legacy publication operations", () => {
  const work = fixture();
  const manifestPath = path.join(work, "preparation-manifest.json");
  const manifest = readJson(manifestPath);
  manifest.projects = [{
    id: "project-1",
    path: "specification/a",
    sourceChangeIds: ["source-1"],
  }, {
    id: "project-unrelated",
    path: "specification/b",
    sourceChangeIds: ["source-unrelated"],
  }];
  writeJson(manifestPath, manifest);
  const sourceIndexPath = path.join(work, "source", "source-index.json");
  const sourceIndex = readJson(sourceIndexPath);
  sourceIndex.sourceChanges[0].declarations = [{
    id: "version-declaration",
    kind: "enum",
    qualifiedName: "Contoso.Versions",
    hunkIds: ["hunk-1"],
    source: { revision: "current", startLine: 1, endLine: 2 },
  }];
  sourceIndex.sourceChanges.push({
    id: "source-unrelated",
    path: "specification/b/main.tsp",
    hunks: [{ id: "hunk-unrelated" }],
    declarations: [{
      id: "unrelated-declaration",
      kind: "enum",
      qualifiedName: "Other.Versions",
      hunkIds: ["hunk-unrelated"],
      source: { revision: "current", startLine: 1, endLine: 2 },
    }],
  });
  writeJson(sourceIndexPath, sourceIndex);
  const semanticPath = path.join(work, "dimensions", "semantic-intents-input.json");
  const semantic = readJson(semanticPath);
  semantic.facts["operation-1"].projectId = "project-1";
  semantic.reviewUnits[0].sourceChangeIds = ["source-1", "source-unrelated"];
  semantic.reviewUnits[0].hunkIds = ["hunk-1", "hunk-unrelated"];
  semantic.reviewUnits[0].groupingEvidence = {
    reasons: ["cross-project:api-version-publication"],
  };
  writeJson(semanticPath, semantic);

  const assessment = assembleAssessment({
    work,
    judgment: {
      schemaVersion: 1,
      semanticIntents: [{
        reviewUnitId: "semantic-1",
        title: "Publish v1",
        summary: "Publish the API version.",
        sourceChangeIds: ["source-1"],
        operationIds: ["operation-1"],
      }],
      restDecisions: [{
        candidateId: "rest-1",
        decision: "reject",
        rationale: "REST remains compatible.",
      }],
      downstreamDecisions: [],
      overallConfidence: "high",
      blockers: [],
    },
  });

  const operationSources = assessment.dimensions.semantic.items[0].operations[0].sources;
  assert.deepEqual(operationSources.map((source) => source.id), ["source-1"]);
  assert.deepEqual(operationSources[0].hunks.map((hunk) => hunk.id), ["hunk-1"]);
});
