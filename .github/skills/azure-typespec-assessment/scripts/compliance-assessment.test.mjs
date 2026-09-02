import assert from "node:assert/strict";
import test from "node:test";
import {
  assembleCompliance,
  readComplianceCatalog,
} from "./compliance-assessment.mjs";
import { buildComplianceSearchRequests } from "./compliance-search-request.mjs";

const HASH = `sha256:${"a".repeat(64)}`;

function fixture() {
  const source = {
    id: "source-1",
    path: "specification/widgets/resource-manager/Microsoft.Widgets/main.tsp",
    hunks: [
      {
        id: "hunk-1",
        lines: [
          "+@parentResource(Widget)",
          "+model Child is ProxyResource<ChildProperties>;",
        ],
      },
    ],
    declarations: [
      {
        id: "declaration-1",
        kind: "model",
        qualifiedName: "Microsoft.Widgets.Child",
        decorators: ["@parentResource"],
        hunkIds: ["hunk-1"],
        source: {
          revision: "current",
          startLine: 10,
          endLine: 11,
          link: "https://example.test/main.tsp#L10-L11",
        },
      },
    ],
  };
  const requests = buildComplianceSearchRequests({
    semanticReviewUnits: [
      {
        id: "semantic-1",
        action: "add",
        sourceChangeIds: ["source-1"],
        hunkIds: ["hunk-1"],
      },
    ],
    sourceChanges: { "source-1": source },
  });
  const scoreValues = [
    {
      exactSymbol: 4,
      patternCategory: 3,
      servicePlane: 2,
      changeContext: 1,
      total: 10,
    },
    {
      exactSymbol: 4,
      patternCategory: 3,
      servicePlane: 2,
      changeContext: 0,
      total: 9,
    },
    {
      exactSymbol: 4,
      patternCategory: 3,
      servicePlane: 0,
      changeContext: 1,
      total: 8,
    },
    {
      exactSymbol: 4,
      patternCategory: 3,
      servicePlane: 0,
      changeContext: 0,
      total: 7,
    },
  ];
  const catalogRanking = readComplianceCatalog().map((item, index) => ({
    rank: index + 1,
    catalogOrder: item.catalogOrder,
    title: item.title,
    canonicalUrl: item.canonicalUrl,
    score: scoreValues[index] ?? {
      exactSymbol: 0,
      patternCategory: 0,
      servicePlane: 0,
      changeContext: 0,
      total: 0,
    },
    selectionRationale:
      index < 4
        ? "The document matches the changed ARM resource pattern."
        : "The document has lower relevance to this intent.",
  }));
  const documents = catalogRanking.slice(0, 4).map((item, index) => ({
    ...item,
    retrieval: {
      status: "fetched",
      retrievedAt: "2026-08-28T00:00:00.000Z",
      contentHash: HASH,
    },
    guidance:
      index === 0
        ? [
            {
              section: "Resource types",
              excerpt: "Guidance 1",
              queryTerms: ["ProxyResource"],
              examples: ["model Child is ProxyResource<ChildProperties>;"],
              applicableDeclarationIds: ["declaration-1"],
            },
          ]
        : [],
    noRelevantGuidance: index !== 0,
  }));
  const evidence = {
    schemaVersion: 1,
    intents: [
      {
        reviewUnitId: "semantic-1",
        queryProfile: requests[0].queryProfile,
        catalogRanking,
        rankedDocuments: documents,
        retrievalAttempts: [],
        blockers: [],
      },
    ],
    inputAccounting: {
      catalogEntriesScored: readComplianceCatalog().length,
      documentsFetched: 4,
      documentBytesFetched: 1000,
      guidanceExcerptsRetained: 1,
      guidanceExcerptBytesRetained: 100,
    },
  };
  const decisions = [
    {
      reviewUnitId: "semantic-1",
      applicableGuidance: [
        {
          canonicalDocumentUrl: documents[0].canonicalUrl,
          guidanceSection: "Resource types",
        },
      ],
      sourceChangeIds: ["source-1"],
      hunkIds: ["hunk-1"],
      declarationIds: ["declaration-1"],
      decision: "applicable-fail",
      title: "Child does not use the documented resource template",
      severity: "medium",
      expected: "Guidance 1",
      actual: "model Child is ProxyResource<ChildProperties>;",
      rationale: "The changed intent contradicts the documented requirement.",
    },
  ];
  return { source, requests, evidence, decisions };
}

test("builds a bounded Compliance query profile from Semantic intent evidence", () => {
  const { requests } = fixture();
  assert.equal(requests.length, 1);
  assert.equal(requests[0].queryProfile.servicePlane, "resource-manager");
  assert.equal(requests[0].queryProfile.action, "add");
  assert.deepEqual(requests[0].declarationIds, ["declaration-1"]);
  assert.ok(requests[0].queryProfile.categories.includes("resource"));
  assert.ok(requests[0].queryProfile.symbols.includes("@parentResource"));
  assert.equal(requests[0].queryProfile.representativeSourceExcerpts.length, 1);
  assert.equal(requests[0].queryProfile.affectedOperationCount, 0);
});

test("assembles one Compliance finding and coverage per Semantic intent", () => {
  const { source, requests, evidence, decisions } = fixture();
  const compliance = assembleCompliance({
    requests,
    evidence,
    decisions,
    sourceChanges: [source],
  });
  assert.equal(compliance.status, "failed");
  assert.equal(compliance.findings.length, 1);
  assert.equal(compliance.coverage.semanticIntentCount, 1);
  assert.equal(compliance.coverage.assessedIntentCount, 1);
  assert.equal(compliance.coverage.selectedDocumentCount, 4);
  assert.equal(compliance.intentAssessments[0].decision, "applicable-fail");
  assert.deepEqual(compliance.findings[0].codeSnippets[0].lines, [
    "+@parentResource(Widget)",
    "+model Child is ProxyResource<ChildProperties>;",
  ]);
});

test("rejects uncataloged Compliance evidence", () => {
  const { source, requests, evidence, decisions } = fixture();
  evidence.intents[0].rankedDocuments[0].canonicalUrl =
    "https://example.test/invented";
  assert.throws(
    () =>
      assembleCompliance({
        requests,
        evidence,
        decisions,
        sourceChanges: [source],
      }),
    /uncataloged URL/,
  );
});

test("rejects intent decisions that cite unknown guidance", () => {
  const { source, requests, evidence, decisions } = fixture();
  decisions[0].applicableGuidance[0].guidanceSection = "Unknown";
  assert.throws(
    () =>
      assembleCompliance({
        requests,
        evidence,
        decisions,
        sourceChanges: [source],
      }),
    /uses unfetched guidance/,
  );
});

test("rejects incomplete declaration source provenance", () => {
  const { source, requests, evidence, decisions } = fixture();
  decisions[0] = { ...decisions[0], sourceChangeIds: [], hunkIds: [] };
  assert.throws(
    () =>
      assembleCompliance({
        requests,
        evidence,
        decisions,
        sourceChanges: [source],
      }),
    /lacks applicable evidence/,
  );
});

test("allows not-assessed when fetched guidance does not govern the intent", () => {
  const { source, requests, evidence, decisions } = fixture();
  requests[0].declarationIds.push("declaration-2");
  evidence.intents[0].rankedDocuments[0].guidance[0].applicableDeclarationIds.push(
    "declaration-2",
  );
  decisions[0] = {
    reviewUnitId: "semantic-1",
    applicableGuidance: decisions[0].applicableGuidance,
    sourceChangeIds: ["source-1"],
    hunkIds: ["hunk-1"],
    declarationIds: ["declaration-1"],
    decision: "not-assessed",
    actual: "The intent uses a generator-specific decorator.",
    rationale:
      "The fetched page documents generic decorator syntax but does not define the generator-specific semantics.",
  };
  const compliance = assembleCompliance({
    requests,
    evidence,
    decisions,
    sourceChanges: [source],
  });
  assert.equal(compliance.status, "not-assessed");
  assert.equal(compliance.findings.length, 0);
  assert.equal(compliance.coverage.assessedIntentCount, 0);
  assert.deepEqual(compliance.coverage.unassessedIntentIds, ["semantic-1"]);
  assert.deepEqual(compliance.intentAssessments[0].declarationIds, [
    "declaration-1",
    "declaration-2",
  ]);
  assert.equal(compliance.intentAssessments[0].sourceLinks.length, 1);
});

test("does not pass Compliance when Semantic analysis is blocked", () => {
  const compliance = assembleCompliance({
    requests: [],
    evidence: {
      schemaVersion: 1,
      intents: [],
      inputAccounting: {
        catalogEntriesScored: 0,
        documentsFetched: 0,
        documentBytesFetched: 0,
        guidanceExcerptsRetained: 0,
        guidanceExcerptBytesRetained: 0,
      },
    },
    decisions: [],
    sourceChanges: [],
    initialBlockers: ["semantic-analysis-blocked: compiler failed."],
  });
  assert.equal(compliance.status, "not-assessed");
  assert.equal(compliance.blockers.length, 1);
});
