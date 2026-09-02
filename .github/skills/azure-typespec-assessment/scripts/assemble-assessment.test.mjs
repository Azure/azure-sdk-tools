import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import {
  assembleAssessment,
  matchTypeFindingIntents,
} from "./assemble-assessment.mjs";
import { readJson, writeJson } from "./cli.mjs";
import { readComplianceCatalog } from "./compliance-assessment.mjs";
import { validateAssessment } from "./validate-assessment.mjs";

function fixture() {
  const work = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-assessment-"));
  writeJson(path.join(work, "preparation-manifest.json"), {
    schemaVersion: 1,
    repository: { root: "repo" },
    pullRequest: {
      number: 123,
      url: "https://github.com/Azure/azure-rest-api-specs/pull/123",
    },
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
      hunks: [{ id: "hunk-1", lines: ["+model Widget {}"] }],
      declarations: [{
        id: "declaration-1",
        kind: "model",
        qualifiedName: "Contoso.Widget",
        hunkIds: ["hunk-1"],
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

function addComplianceInput(work) {
  const request = {
    reviewUnitId: "semantic-1",
    sourceChangeIds: ["source-1"],
    hunkIds: ["hunk-1"],
    declarationIds: ["declaration-1"],
    queryProfile: {
      servicePlane: "data-plane",
      action: "modify",
      declarationKinds: ["model"],
      qualifiedNames: ["Contoso.Widget"],
      symbols: [],
      categories: ["models"],
      changedTokens: ["Widget"],
    },
  };
  writeJson(path.join(work, "model-input.json"), {
    complianceSearchRequests: [request],
    inputAccounting: {},
  });
  const scores = [10, 9, 8, 7];
  const catalogRanking = readComplianceCatalog().map((item, index) => ({
    rank: index + 1,
    catalogOrder: item.catalogOrder,
    title: item.title,
    canonicalUrl: item.canonicalUrl,
    score: index < 4 ? {
      exactSymbol: 4,
      patternCategory: 3,
      servicePlane: index < 2 ? 2 : 0,
      changeContext: index === 0 || index === 2 ? 1 : 0,
      total: scores[index],
    } : {
      exactSymbol: 0,
      patternCategory: 0,
      servicePlane: 0,
      changeContext: 0,
      total: 0,
    },
    selectionRationale: index < 4
      ? "Relevant to the changed model."
      : "Lower relevance to the changed model.",
  }));
  const documents = catalogRanking.slice(0, 4).map((item, index) => ({
    ...item,
    retrieval: {
      status: "fetched",
      retrievedAt: "2026-08-28T00:00:00.000Z",
      contentHash: `sha256:${"a".repeat(64)}`,
    },
    guidance: index === 0 ? [{
      section: "Resource types",
      excerpt: "Use the standard resource template.",
      queryTerms: ["Widget"],
      examples: [],
      applicableDeclarationIds: ["declaration-1"],
    }] : [],
    noRelevantGuidance: index !== 0,
  }));
  writeJson(path.join(work, "compliance-search-evidence.json"), {
    schemaVersion: 1,
    intents: [{
      reviewUnitId: "semantic-1",
      queryProfile: request.queryProfile,
      catalogRanking,
      rankedDocuments: documents,
      retrievalAttempts: [],
      blockers: [],
    }],
    inputAccounting: {
      catalogEntriesScored: readComplianceCatalog().length,
      documentsFetched: 4,
      documentBytesFetched: 100,
      guidanceExcerptsRetained: 1,
      guidanceExcerptBytesRetained: 35,
    },
  });
  return [{
    reviewUnitId: "semantic-1",
    applicableGuidance: [{
      canonicalDocumentUrl: documents[0].canonicalUrl,
      guidanceSection: "Resource types",
    }],
    sourceChangeIds: ["source-1"],
    hunkIds: ["hunk-1"],
    declarationIds: ["declaration-1"],
    decision: "applicable-fail",
    title: "Widget does not use the documented resource template",
    severity: "medium",
    expected: "Use the standard resource template.",
    actual: "model Widget {}",
    rationale: "The model does not use the documented resource template.",
  }];
}

test("assembler joins confirmed evidence and derives scoped safety", () => {
  const work = fixture();
  const assessment = assembleAssessment({
    work,
    judgment: {
      schemaVersion: 1,
      semanticIntents: [{
        reviewUnitId: "semantic-1",
        title: "Require mode",
        summary: "The request now requires mode.",
      }],
      restDecisions: [{
        candidateId: "rest-1",
        decision: "approve",
        severity: "high",
        rationale: "Existing requests fail.",
      }],
      downstreamDecisions: [],
      complianceDecisions: [],
      overallConfidence: "high",
      blockers: [],
    },
  });
  assert.equal(assessment.safety.status, "failed");
  assert.deepEqual(assessment.pullRequest, {
    number: 123,
    url: "https://github.com/Azure/azure-rest-api-specs/pull/123",
  });
  assert.equal(assessment.dimensions.semantic.items[0].action, "add");
  assert.equal(assessment.dimensions.semantic.items[0].changeKind, "add");
  assert.deepEqual(
    assessment.dimensions.semantic.items[0].operations.map((operation) => operation.operationId),
    ["Widgets_Get", "Widgets_List"],
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
          }],
          restDecisions: [],
          downstreamDecisions: [],
          complianceDecisions: [],
          overallConfidence: "high",
          blockers: [],
        },
      }),
    /coverage mismatch/,
  );
});

test("assembler requires and joins active Compliance evidence", () => {
  const work = fixture();
  const complianceDecisions = addComplianceInput(work);
  const assessment = assembleAssessment({
    work,
    judgment: {
      schemaVersion: 1,
      semanticIntents: [{
        reviewUnitId: "semantic-1",
        title: "Change Widget",
        summary: "Changes the Widget model.",
      }],
      restDecisions: [{
        candidateId: "rest-1",
        decision: "reject",
        rationale: "The REST contract remains compatible.",
      }],
      downstreamDecisions: [],
      complianceDecisions,
      overallConfidence: "high",
      blockers: [],
    },
  });
  assert.equal(assessment.dimensions.compliance.status, "failed");
  assert.equal(assessment.dimensions.compliance.findings.length, 1);
  assert.equal(assessment.dimensions.compliance.intentAssessments[0].documents.length, 4);
  assert.equal(
    assessment.provenance.complianceSearchEvidence,
    "compliance-search-evidence.json",
  );
  assert.deepEqual(validateAssessment(assessment), []);
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
      complianceDecisions: [],
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
      complianceDecisions: [],
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
      }],
      restDecisions: [{
        candidateId: "rest-1",
        decision: "reject",
        rationale: "REST remains compatible.",
      }],
      downstreamDecisions: [],
      complianceDecisions: [],
      overallConfidence: "high",
      blockers: [],
    },
  });

  const operationSources = assessment.dimensions.semantic.items[0].operations[0].sources;
  assert.deepEqual(operationSources.map((source) => source.id), ["source-1"]);
  assert.deepEqual(operationSources[0].hunks.map((hunk) => hunk.id), ["hunk-1"]);
});

test("matches type findings to semantic intents by changed declaration identity", () => {
  const semanticItems = [{
    id: "semantic-file-items",
    declarationIds: ["file-item", "file-item-name"],
    sources: [{
      declarations: [
        { id: "file-item", qualifiedName: "FileItem" },
        { id: "file-item-name", qualifiedName: "FileItem.name" },
        { id: "directory-item", qualifiedName: "DirectoryItem" },
      ],
    }],
  }, {
    id: "semantic-directory-items",
    declarationIds: ["directory-item"],
    sources: [{
      declarations: [
        { id: "file-item", qualifiedName: "FileItem" },
        { id: "directory-item", qualifiedName: "DirectoryItem" },
      ],
    }],
  }];

  const matches = matchTypeFindingIntents({
    crossLanguageDefinitionId: "Storage.File.FileItem",
    evidence: [],
  }, semanticItems);

  assert.deepEqual(matches.map((intent) => intent.id), ["semantic-file-items"]);
});
