import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  downstreamTypeCards,
  escapeHtml,
  relatedImpactOperations,
  renderAssessmentHtml,
  restContractCards,
  representativeSource,
  visibleSharedTypeImpacts,
} from "./render-assessment-html.mjs";
import { readComplianceCatalog } from "./compliance-assessment.mjs";

function notAssessedCompliance() {
  return {
    status: "not-assessed",
    summary: "Compliance evidence is unavailable.",
    coverage: {
      semanticIntentCount: 0,
      assessedIntentCount: 0,
      selectedDocumentCount: 0,
      unassessedIntentIds: [],
    },
    intentAssessments: [],
    findings: [],
    retrievalFailures: [],
    blockers: [
      {
        message:
          "compliance-search-input-missing: test fixture has no search input.",
      },
    ],
  };
}

test("shows confirmed type impacts without operation reachability", () => {
  const impact = {
    findingIds: ["downstream-1"],
    affectedOperationCount: 0,
    affectedMethodCount: 0,
  };
  assert.deepEqual(visibleSharedTypeImpacts([impact]), [impact]);
  assert.deepEqual(
    visibleSharedTypeImpacts([
      {
        findingIds: [],
        affectedOperationCount: 0,
        affectedMethodCount: 0,
      },
    ]),
    [],
  );
  assert.deepEqual(
    relatedImpactOperations({ relatedSemanticIntents: ["semantic-1"] }, [
      {
        id: "semantic-1",
        operations: [
          {
            operationId: "Widgets_Get",
            apiVersion: "v1",
          },
        ],
      },
    ]).map(({ operationId }) => operationId),
    ["Widgets_Get"],
  );
});

test("renders one collapsed downstream card per SDK type", () => {
  const findings = [
    {
      id: "downstream-1",
      severity: "high",
      crossLanguageDefinitionId: "Contoso.Widget",
      relatedSemanticIntents: ["semantic-1"],
    },
    {
      id: "downstream-2",
      severity: "medium",
      crossLanguageDefinitionId: "Contoso.Widget",
      relatedSemanticIntents: ["semantic-1"],
    },
    {
      id: "downstream-3",
      severity: "high",
      crossLanguageDefinitionId: "Contoso.Gadget",
      relatedSemanticIntents: ["semantic-2"],
    },
  ];
  const cards = downstreamTypeCards({
    findings,
    sharedTypeImpacts: [
      {
        id: "shared-1",
        rootCauseId: "root-1",
        summary: "Shared change.",
        findingIds: findings.map((finding) => finding.id),
        types: ["Contoso.Widget", "Contoso.Gadget"],
        affectedMethods: [],
      },
    ],
  });

  assert.deepEqual(
    cards.map((card) => card.type),
    ["Contoso.Gadget", "Contoso.Widget"],
  );
  assert.equal(cards[1].findings.length, 2);
  assert.deepEqual(cards[1].relatedSemanticIntents, ["semantic-1"]);
  assert.deepEqual(cards[0].legacyImpactIds, ["shared-1"]);
});

test("renders SDK contract changes with collapsed affected operations", () => {
  const source = {
    id: "source-enum",
    path: "specification/hardwaresecuritymodules/models.tsp",
    origins: ["committed"],
    hunks: [
      {
        id: "hunk-enum",
        current: { startLine: 283, endLine: 296 },
        lines: [
          "-enum CloudHsmClusterSkuName {",
          "-  `Standard B10`,",
          "+union CloudHsmClusterSkuName {",
          "+  string,",
          '+  StandardB10: "Standard B10",',
        ],
      },
    ],
    declarations: [],
  };
  const enumFact = (comparisonRole, values, isFixed, isUnionAsEnum) => ({
    factKind: "enum",
    comparisonRole,
    isFixed,
    isUnionAsEnum,
    values,
  });
  const operations = ["CreateOrUpdate", "Get", "Update"].map((name) => ({
    operationId: `CloudHsmClusters_${name}`,
    apiVersion: "2025-03-31",
    method:
      name === "CreateOrUpdate" ? "put" : name === "Update" ? "patch" : "get",
    path: "/cloudHsmClusters/{cloudHsmClusterName}",
    changedAspects: [],
    restChanged: false,
    outcome: "HTTP contract unchanged.",
  }));
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    comparison: { baseCommit: "base", headCommit: "head" },
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "failed" },
    dimensions: {
      semantic: {
        status: "assessed",
        sourceHunkIds: ["hunk-enum"],
        items: [
          {
            id: "semantic-enum",
            action: "modify",
            title: "Make Cloud HSM SKU names extensible",
            summary: "Replace the fixed enum with an extensible enum.",
            sources: [source],
            operations,
            relatedFindings: {
              downstream: [],
              sharedTypeImpact: ["shared-enum"],
            },
          },
        ],
      },
      rest: { status: "passed", findings: [] },
      downstream: {
        status: "failed",
        findings: [
          {
            id: "downstream-enum",
            rule: "enum-values-removed",
            severity: "high",
            actual: "The generated enum member identity changed.",
            expected: "The generated enum member identity remains stable.",
            rationale:
              "Making the enum extensible is compatible by itself. Existing SDK source must migrate from `Standard B10` to `StandardB10`.",
            crossLanguageDefinitionId:
              "Microsoft.HardwareSecurityModules.CloudHsmClusterSkuName",
            evidence: [
              enumFact(
                "baseline",
                [
                  { name: "Standard B10", value: "Standard B10" },
                  { name: "Standard_B1", value: "Standard_B1" },
                ],
                true,
                false,
              ),
              enumFact(
                "target",
                [
                  { name: "StandardB10", value: "Standard B10" },
                  { name: "Standard_B1", value: "Standard_B1" },
                ],
                false,
                true,
              ),
            ],
            sources: [source],
            relatedSemanticIntents: ["semantic-enum"],
          },
        ],
        operationGroups: [],
        sharedTypeImpacts: [
          {
            id: "shared-enum",
            rootCauseId: "root-enum",
            summary: "The public SDK enum member changed.",
            findingIds: ["downstream-enum"],
            types: ["Microsoft.HardwareSecurityModules.CloudHsmClusterSkuName"],
            typeCount: 1,
            affectedMethods: [],
            affectedMethodCount: 0,
            affectedOperationCount: 0,
            relatedSemanticIntents: ["semantic-enum"],
          },
        ],
      },
      compliance: notAssessedCompliance(),
      documentQuality: {
        status: "not-assessed",
        summary: "Document quality is not assessed.",
      },
    },
    changedFiles: [source],
    projects: [],
    blockers: [],
    provenance: {},
  });

  assert.match(
    html,
    /<strong>CloudHsmClusterSkuName<\/strong><span class="contract-tag">SDK type<\/span><\/summary>/,
  );
  assert.doesNotMatch(
    html,
    /SDK contract changes — 3 affected REST operations/,
  );
  assert.doesNotMatch(html, /Mapped SDK methods/);
  assert.doesNotMatch(html, /Root-cause provenance/);
  assert.match(
    html,
    /fixed enum changed to an extensible enum, with 1 generated member renamed/,
  );
  assert.match(html, /SDK contract member<\/th><th>Before<\/th><th>After/);
  assert.match(html, /CloudHsmClusterSkuName\.Standard B10/);
  assert.match(html, /StandardB10/);
  assert.match(
    html,
    /<details class="affected-operations"><summary><strong>Affected REST operations \(3\)<\/strong><\/summary>/,
  );
  assert.doesNotMatch(html, /<details class="affected-operations" open>/);
  assert.match(html, /CloudHsmClusters_CreateOrUpdate/);
  assert.match(html, /class="http-method">PUT/);
  const downstream = html.match(
    /<section id="downstream-breaking">([\s\S]*?)<\/section>/,
  )[1];
  assert.doesNotMatch(downstream, /Changed TypeSpec:/);
  assert.match(downstream, /Related semantic intents:/);
});

test("groups REST contract deltas by schema identity and retains affected operations", () => {
  const operation = (comparisonRole, operationId) => ({
    comparisonRole,
    operationId,
    apiVersion: "v1",
    method: "get",
    path: "/widgets",
    responses: [
      {
        status: "200",
        headers: [],
        schema: {
          kind: "object",
          properties:
            comparisonRole === "baseline"
              ? [
                  {
                    name: "state",
                    schema: {
                      kind: "enum",
                      type: "string",
                      reference: "stable/v1.json#/definitions/WidgetState",
                      values: ["Ready", "Deleted"],
                    },
                  },
                ]
              : [],
        },
      },
    ],
  });
  const findings = ["Widgets_Get", "Widgets_List"].map(
    (operationId, index) => ({
      id: `rest-${index}`,
      rule: "serialized-property-removed",
      severity: "high",
      contractChange: {
        rule: "serialized-property-removed",
        location: "response 200.state",
      },
      operationIds: [operationId],
      relatedSemanticIntents: ["semantic-1"],
      sources: [],
      evidence: [
        operation("baseline", operationId),
        operation("target", operationId),
      ],
    }),
  );

  const cards = restContractCards(findings);

  assert.equal(cards.length, 1);
  assert.equal(cards[0].identity, "WidgetState");
  assert.equal(cards[0].operations.length, 2);
  assert.equal(
    cards[0].findings[0].contractDelta.before,
    "WidgetState { Ready | Deleted }",
  );
  assert.equal(cards[0].findings[0].contractDelta.after, "removed");
});

test("renderer shows fetched Compliance guidance and expands failures", () => {
  const catalog = readComplianceCatalog();
  const scores = [10, 9, 8, 7];
  const catalogRanking = catalog.map((item, index) => ({
    rank: index + 1,
    catalogOrder: item.catalogOrder,
    title: item.title,
    canonicalUrl: item.canonicalUrl,
    score:
      index < 4
        ? {
            exactSymbol: 4,
            patternCategory: 3,
            servicePlane: index < 2 ? 2 : 0,
            changeContext: index === 0 || index === 2 ? 1 : 0,
            total: scores[index],
          }
        : {
            exactSymbol: 0,
            patternCategory: 0,
            servicePlane: 0,
            changeContext: 0,
            total: 0,
          },
    selectionRationale:
      index < 4
        ? "Matches the changed resource pattern."
        : "Lower relevance to the changed resource pattern.",
  }));
  const documents = catalogRanking.slice(0, 4).map((item, index) => ({
    ...item,
    retrievedAt: "2026-08-28T00:00:00.000Z",
    contentHash: `sha256:${"a".repeat(64)}`,
    guidance:
      index === 0
        ? [
            {
              section: "Resource types",
              excerpt: "Use the standard resource template.",
              queryTerms: ["ProxyResource"],
              examples: ["model Child is ProxyResource<ChildProperties>;"],
              applicableDeclarationIds: ["declaration-1"],
            },
          ]
        : [],
    noRelevantGuidance: index !== 0,
  }));
  const sourceLinks = [
    {
      path: "specification/widgets/main.tsp",
      startLine: 1,
      endLine: 1,
      link: "https://example.test/main.tsp#L1",
    },
  ];
  const codeSnippets = [
    {
      path: "specification/widgets/main.tsp",
      hunkId: "hunk-version",
      lines: ['+v2026_01_01: "2026-01-01",'],
    },
    {
      path: "specification/widgets/main.tsp",
      hunkId: "hunk-1",
      lines: ["+model Child extends LegacyResource {}"],
    },
    {
      path: "specification/widgets/main.tsp",
      hunkId: "hunk-operations",
      lines: ["+interface ChildOperations {}"],
    },
  ];
  const intentAssessment = {
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
    expected: "Use the standard resource template.",
    actual: "The intent introduces a child resource using LegacyResource.",
    rationale: "The changed intent does not use the documented template.",
    gap: "The changed intent does not use the documented template.",
    sourceLinks,
    codeSnippets,
  };
  const finding = {
    id: "compliance-1",
    semanticIntentId: "semantic-1",
    ...intentAssessment,
    applicableGuidance: [
      {
        canonicalDocumentUrl: documents[0].canonicalUrl,
        guidanceSection: "Resource types",
        excerpt: "Use the standard resource template.",
      },
    ],
  };
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    pullRequest: {
      number: 123,
      url: "https://github.com/Azure/azure-rest-api-specs/pull/123",
    },
    comparison: {
      baseRef: "origin/main",
      baseCommit: "base",
      headCommit: "head",
      workingTree: {},
    },
    artifactComparisons: [],
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "failed" },
    dimensions: {
      semantic: {
        status: "assessed",
        sourceHunkIds: ["hunk-1"],
        items: [
          {
            id: "semantic-1",
            action: "add",
            title: "Add child resource",
            summary: "Adds a child resource.",
            sourceChangeIds: ["source-1"],
            hunkIds: ["hunk-1"],
            operations: [],
            sources: [
              {
                id: "source-1",
                path: "specification/widgets/main.tsp",
                hunks: [{ id: "hunk-1", lines: codeSnippets[0].lines }],
                declarations: [
                  {
                    id: "declaration-1",
                    hunkIds: ["hunk-1"],
                    source: { revision: "current", startLine: 1, endLine: 1 },
                  },
                ],
              },
            ],
            relatedFindings: {
              rest: ["rest-1"],
              downstream: [],
              sharedTypeImpact: [],
            },
          },
        ],
        blockers: [],
      },
      rest: {
        status: "failed",
        findings: [
          {
            id: "rest-1",
            rule: "response-contract-changed",
            severity: "high",
            actual: "The response contract changed.",
            expected: "The response contract remains compatible.",
            rationale: "Existing clients may observe a different payload.",
            relatedSemanticIntents: ["semantic-1"],
            operationIds: ["Widgets_Get"],
            sources: [
              {
                id: "source-1",
                path: "specification/widgets/main.tsp",
                hunks: [{ id: "hunk-1", lines: codeSnippets[0].lines }],
                declarations: [],
              },
            ],
            evidence: [{ id: "fact-1" }],
          },
        ],
        blockers: [],
      },
      downstream: {
        status: "passed",
        findings: [],
        operationGroups: [],
        sharedTypeImpacts: [],
        impliedByRest: [],
        blockers: [],
      },
      compliance: {
        status: "failed",
        summary: "1 documentation-grounded compliance finding.",
        coverage: {
          semanticIntentCount: 1,
          assessedIntentCount: 1,
          selectedDocumentCount: 4,
          unassessedIntentIds: [],
        },
        intentAssessments: [
          {
            semanticIntentId: "semantic-1",
            sourceChangeIds: ["source-1"],
            hunkIds: ["hunk-1"],
            declarationIds: ["declaration-1"],
            catalogRanking,
            documents,
            ...intentAssessment,
            blockers: [],
          },
        ],
        findings: [finding],
        retrievalFailures: [],
        blockers: [],
      },
      documentQuality: { status: "not-assessed", summary: "Not assessed." },
    },
    blockers: [],
    projects: [],
    changedFiles: [],
    provenance: {},
  });
  assert.match(html, /Azure Guidelines/);
  assert.match(
    html,
    /<section id="azure-compliance"><h2>Azure Guidelines \(1\)<\/h2>/,
  );
  assert.doesNotMatch(html, /class="panel compliance-summary/);
  assert.doesNotMatch(
    html,
    /class="finding compliance-finding medium"[^>]* open/,
  );
  assert.match(html, /Failed/);
  const complianceHtml = html.slice(
    html.indexOf('<section id="azure-compliance">'),
    html.indexOf('<section id="semantic-intents">'),
  );
  assert.ok(
    html.indexOf('<section id="azure-compliance">') <
      html.indexOf('<section id="semantic-intents">'),
  );
  assert.ok(
    html.indexOf('<a class="summary-card" href="#azure-compliance">') <
      html.indexOf('<a class="summary-card" href="#semantic-intents">'),
  );
  assert.doesNotMatch(complianceHtml, /Ranked official documents/);
  assert.doesNotMatch(complianceHtml, /class="compliance-intent"/);
  const appendixHtml = html.slice(html.indexOf('<section id="appendix">'));
  assert.match(appendixHtml, /Guidance fetched/);
  assert.match(
    appendixHtml,
    /<details class="compliance-intent" id="compliance-intent-semantic-1">/,
  );
  assert.match(
    appendixHtml,
    new RegExp(
      `<li><a href="${documents[0].canonicalUrl.replaceAll("/", "\\/")}">${documents[0].title}<\\/a><\\/li>`,
    ),
  );
  assert.doesNotMatch(
    appendixHtml,
    /applicable fail|finding\(s\)|score 10\/10/,
  );
  assert.match(
    html,
    /<details class="intent" id="intent-semantic-1"><summary>/,
  );
  assert.match(
    html,
    /class="intent-finding-badge rest" href="#finding-rest-1">REST breaking changes<\/a>/,
  );
  assert.match(
    html,
    /class="intent-finding-badge compliance" href="#compliance-finding-compliance-1">Azure Guidelines<\/a>/,
  );
  assert.match(
    html,
    /id="related-findings-semantic-1"><strong>Related findings:<\/strong> <a href="#finding-rest-1">rest-1<\/a>, <a href="#compliance-finding-compliance-1">compliance-1<\/a>/,
  );
  assert.doesNotMatch(html, /View compliance assessment/);
  assert.match(
    html,
    /if \(element\.tagName === "DETAILS"\) element\.open = true/,
  );
  assert.match(html, /Use the standard resource template/);
  assert.match(html, /Child does not use the documented resource template/);
  assert.doesNotMatch(html, /class="severity/);
  assert.doesNotMatch(html, />medium<\/span>/);
  assert.match(html, /<strong>Gap:<\/strong>/);
  assert.match(html, /<strong>Expected<\/strong>/);
  assert.match(html, /Documented TypeSpec example/);
  assert.match(html, /<strong>Actual<\/strong>/);
  assert.match(html, /<h4>TypeSpec code<\/h4>/);
  const findingHtml = html.slice(
    html.indexOf('id="compliance-finding-compliance-1"'),
    html.indexOf('<section id="semantic-intents">'),
  );
  assert.doesNotMatch(findingHtml, /TypeSpec source:/);
  assert.doesNotMatch(findingHtml, /v2026_01_01/);
  assert.doesNotMatch(
    findingHtml,
    /The intent introduces a child resource using LegacyResource/,
  );
  assert.match(findingHtml, /model Child extends LegacyResource/);
  assert.match(findingHtml, /interface ChildOperations/);
  assert.match(
    html,
    /<details class="compliance-intent" id="compliance-intent-semantic-1">/,
  );
  assert.match(html, /<a href="#intent-semantic-1">/);
});

test("escapeHtml escapes Agent and source text", () => {
  assert.equal(
    escapeHtml('<script x="1">&'),
    "&lt;script x=&quot;1&quot;&gt;&amp;",
  );
});

test("renderer labels active Compliance and scoped safety", () => {
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    pullRequest: {
      number: 123,
      url: "https://github.com/Azure/azure-rest-api-specs/pull/123",
    },
    comparison: {
      baseRef: "origin/main",
      baseCommit: "9f0ad696cc186c2d16cb522abc0fbd4aa3854ca5",
      headCommit: "780a61ace56c22ce10dd01caa8ab95ca4514ac2e",
      workingTree: {},
    },
    artifactComparisons: [
      {
        projectId: "new-version",
        mode: "new-api-version",
        baseline: {
          sourceRevision: "current",
          commit: "780a61ace56c22ce10dd01caa8ab95ca4514ac2e",
          apiVersion: "2025-07-01",
          reason: "newest-added-version",
        },
        target: {
          sourceRevision: "current",
          commit: "780a61ace56c22ce10dd01caa8ab95ca4514ac2e",
          apiVersion: "2025-09-01",
          reason: "newest-current-version",
        },
      },
      {
        projectId: "existing-version",
        mode: "existing-api-version",
        baseline: {
          sourceRevision: "base",
          commit: "9f0ad696cc186c2d16cb522abc0fbd4aa3854ca5",
          apiVersion: "2018-10-01",
          reason: "affected-existing-version",
        },
        target: {
          sourceRevision: "current",
          commit: "780a61ace56c22ce10dd01caa8ab95ca4514ac2e",
          apiVersion: "2018-10-01",
          reason: "same-existing-version",
        },
      },
    ],
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "passed" },
    dimensions: {
      semantic: { status: "assessed", items: [], blockers: [] },
      rest: { status: "passed", findings: [], blockers: [] },
      downstream: { status: "passed", findings: [], blockers: [] },
      compliance: notAssessedCompliance(),
      documentQuality: { status: "not-assessed", summary: "Not assessed." },
    },
    blockers: [],
    projects: [
      {
        id: "new-version",
        path: "specification/widgets/new-version",
        artifacts: {},
      },
      {
        id: "existing-version",
        path: "specification/widgets/existing-version",
        artifacts: {},
      },
    ],
    changedFiles: [],
    provenance: {},
  });
  assert.match(html, /Azure Guidelines/);
  assert.match(html, /Not assessed/);
  assert.match(html, /Document Quality/);
  assert.match(html, /class="eyebrow">TypeSpec Assessment/);
  assert.match(html, /class="summary-grid"/);
  assert.match(html, /<a class="summary-card" href="#rest-breaking">/);
  assert.match(html, /<a class="summary-card" href="#semantic-intents">/);
  assert.match(html, /<a class="summary-card" href="#downstream-breaking">/);
  assert.match(html, /<a class="summary-card" href="#azure-compliance">/);
  assert.match(html, /<a class="summary-card" href="#document-quality">/);
  assert.match(html, /Overall code quality/);
  assert.match(html, /REST, downstream, and Azure Guidelines/);
  assert.match(
    html,
    /> Not assessed<\/div><div class="summary-label">Overall code quality/,
  );
  assert.match(html, /> 0<\/div><div class="summary-label">Semantic intents/);
  assert.match(html, /0 operations<br>0 Added, 0 Modified, 0 Removed/);
  assert.match(
    html,
    /> 0<\/div><div class="summary-label">REST breaking changes/,
  );
  assert.match(
    html,
    /> 0<\/div><div class="summary-label">Downstream breaking changes/,
  );
  assert.match(html, /Azure Guidelines/);
  assert.match(html, /0\/0 intents assessed/);
  assert.match(
    html,
    /<section id="document-quality"><h2>Document Quality<\/h2><div class="panel not-assessed"><strong>Not assessed<\/strong>/,
  );
  assert.match(
    html,
    /TypeSpec source diff: <code>9f0ad696cc186c2d16cb522abc0fbd4aa3854ca5<\/code> → <code>780a61ace56c22ce10dd01caa8ab95ca4514ac2e<\/code>/,
  );
  assert.match(
    html,
    /<h3 id="projects-and-compiler-status">Projects and compiler status<\/h3>/,
  );
  assert.match(
    html,
    /<section id="appendix"><details class="dimension-details"><summary><h2>Appendix<\/h2><\/summary>/,
  );
  assert.doesNotMatch(
    html,
    /<section id="appendix"><details class="dimension-details" open>/,
  );
  assert.match(
    html,
    /<strong>Pull request:<\/strong> <a href="https:\/\/github\.com\/Azure\/azure-rest-api-specs\/pull\/123">#123<\/a>/,
  );
  assert.doesNotMatch(html, /Assessment comparison/);
});

test("renderer shows expandable REST operations and aggregated downstream methods", () => {
  const source = {
    id: "source-1",
    path: "specification/widgets/main.tsp",
    hunks: [{ id: "hunk-1", lines: ["-  get is Basic;", "+  get is Lro;"] }],
    declarations: [],
  };
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    comparison: { baseCommit: "base", headCommit: "head" },
    artifactComparisons: [
      {
        projectId: "project-1",
        mode: "existing-api-version",
        baseline: {
          sourceRevision: "base",
          commit: "base",
          apiVersion: "v1",
          reason: "affected-version",
        },
        target: {
          sourceRevision: "current",
          commit: "head",
          apiVersion: "v1",
          reason: "affected-version",
        },
      },
    ],
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "failed" },
    dimensions: {
      semantic: {
        status: "assessed",
        sourceHunkIds: ["hunk-1"],
        items: [
          {
            id: "semantic-1",
            action: "modify",
            title: "Modify get",
            summary: "Change the SDK projection.",
            sources: [source],
            operations: [
              {
                operationId: "Widgets_Get",
                apiVersion: "v1",
                method: "get",
                path: "/widgets",
                changedAspects: [],
                restChanged: false,
                outcome:
                  "HTTP signature and represented payload contract unchanged.",
              },
            ],
            relatedFindings: { downstream: ["downstream-group-1"] },
          },
        ],
      },
      rest: { status: "passed", findings: [] },
      downstream: {
        status: "failed",
        findings: [
          {
            id: "downstream-finding-1",
            rule: "method-kind-changed",
            actual: "Method kind changed.",
            expected: "Method kind remains stable.",
            severity: "high",
            rationale: "Generated callers observe a different method shape.",
            crossLanguageDefinitionId: "Contoso.Widgets.get",
            sources: [source],
            evidence: [{ id: "fact-1" }],
            relatedSemanticIntents: ["semantic-1"],
            semanticMatchBasis: "http-method-path",
          },
          {
            id: "downstream-parameters",
            rule: "method-parameters-changed",
            actual: "Method parameters changed.",
            expected: "Method parameters remain stable.",
            severity: "high",
            rationale: "Generated callers observe an added parameter.",
            crossLanguageDefinitionId: "Contoso.Widgets.get",
            sources: [source],
            evidence: [{ id: "fact-1" }],
            relatedSemanticIntents: ["semantic-1"],
            semanticMatchBasis: "http-method-path",
          },
        ],
        operationGroups: [
          {
            id: "downstream-group-1",
            operationId: "Widgets_Get",
            symbol: "Contoso.Widgets.get",
            method: "get",
            path: "/widgets",
            parametersUnchanged: false,
            deltas: [
              {
                findingId: "downstream-parameters",
                rule: "method-parameters-changed",
                field: "parameters",
                severity: "high",
                rationale: "Generated callers observe an added parameter.",
                changes: {
                  added: [
                    {
                      parameter: {
                        name: "afcManagedSync",
                        optional: true,
                        type: "boolean",
                      },
                      index: 3,
                    },
                  ],
                  removed: [],
                  modified: [],
                  reordered: [],
                  unchangedCount: 3,
                },
              },
              {
                findingId: "downstream-finding-1",
                rule: "method-kind-changed",
                field: "kind",
                severity: "high",
                rationale:
                  "Generated callers observe a different method shape.",
                before: "basic",
                after: "lro",
              },
            ],
            relatedSemanticIntents: ["semantic-1"],
          },
        ],
      },
      compliance: notAssessedCompliance(),
      documentQuality: { status: "not-assessed", summary: "Not assessed." },
    },
    blockers: [],
    projects: [
      {
        id: "project-1",
        path: "specification/widgets",
        artifacts: {},
      },
    ],
    changedFiles: [],
    provenance: {},
  });

  assert.match(html, /Affected REST operations \(1\)/);
  assert.match(
    html,
    /HTTP signature and represented payload contract unchanged/,
  );
  assert.match(html, /Contoso\.Widgets\.get/);
  assert.match(
    html,
    /<strong>Widgets_Get<\/strong><span class="contract-tag">SDK method<\/span>/,
  );
  assert.match(html, /2 SDK contract changes/);
  assert.match(html, /SDK method member<\/th><th>Before<\/th><th>After/);
  assert.match(html, /Parameters:<\/strong> 1 added, 3 unchanged/);
  assert.match(html, /afcManagedSync/);
  assert.match(html, /boolean\?/);
  assert.match(html, /not present/);
  assert.match(html, /Method kind/);
  assert.match(html, /Why this is breaking/);
  const downstream = html.match(
    /<section id="downstream-breaking">([\s\S]*?)<\/section>/,
  )[1];
  assert.doesNotMatch(downstream, /Changed TypeSpec:/);
  assert.match(downstream, /Related semantic intents:/);
  assert.match(
    html,
    /<a class="summary-card" href="#downstream-breaking"><div class="summary-value"><span class="fail">×<\/span> 1/,
  );
  assert.match(
    html,
    /TypeSpec source diff: <code>base<\/code> → <code>head<\/code>/,
  );
  assert.doesNotMatch(html, /method-parameters-changed/);
  assert.doesNotMatch(html, /&quot;name&quot;:&quot;afcManagedSync&quot;/);
  assert.match(html, /get is Lro/);
  assert.match(html, /Representative TypeSpec example/);
  assert.match(html, /Complete TypeSpec source evidence/);
  assert.match(
    html,
    /<details class="intent" id="intent-semantic-1"><summary>/,
  );
  assert.match(
    html,
    /class="intent-finding-badge downstream" href="#downstream-downstream-group-1">Downstream breaking changes<\/a>/,
  );
  assert.match(
    html,
    /id="related-findings-semantic-1"><strong>Related findings:<\/strong> <a href="#downstream-downstream-group-1">downstream-group-1<\/a>/,
  );
  assert.doesNotMatch(html, /<details class="intent"[^>]* open/);
  assert.match(html, /<details class="representative-example"><summary>/);
  assert.doesNotMatch(
    html,
    /<details class="representative-example"[^>]* open/,
  );
  const operationCards =
    html.match(/<details class="operation">.*?<\/details>/gs) ?? [];
  assert.equal(operationCards.length, 1);
  assert.doesNotMatch(operationCards[0], /class="diff"/);
});

test("renderer reports operations omitted from large semantic intents", () => {
  const source = {
    id: "source-1",
    path: "specification/widgets/main.tsp",
    hunks: [{ id: "hunk-1", lines: ['+  v1: "v1",'] }],
    declarations: [],
  };
  const operations = Array.from({ length: 16 }, (_, index) => ({
    operationId: `Widgets_Operation${String(index).padStart(2, "0")}`,
    apiVersion: "v1",
    method: "get",
    path: `/widgets/${index}`,
    changedAspects: [],
    restChanged: false,
    outcome: "HTTP signature and represented payload contract unchanged.",
  }));
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    comparison: { baseCommit: "base", headCommit: "head" },
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "passed" },
    dimensions: {
      semantic: {
        status: "assessed",
        sourceHunkIds: ["hunk-1"],
        items: [
          {
            id: "semantic-1",
            action: "modify",
            title: "Publish v1",
            summary: "Publish the new version.",
            sources: [source],
            operations,
            relatedFindings: {},
          },
        ],
      },
      rest: { status: "passed", findings: [] },
      downstream: { status: "passed", findings: [] },
      compliance: notAssessedCompliance(),
      documentQuality: { status: "not-assessed", summary: "Not assessed." },
    },
    blockers: [],
    projects: [],
    changedFiles: [],
    provenance: {},
  });

  assert.match(
    html,
    /3 representative operation\(s\) are shown below and 13 are omitted from HTML/,
  );
  assert.doesNotMatch(html, /Widgets_Operation03/);
});

test("representative source prefers operation evidence and stable ordering", () => {
  const item = {
    sources: [
      {
        id: "source-b",
        path: "specification/widgets/z-context.tsp",
        hunks: [{ id: "hunk-context", lines: ['+import "./feature.tsp";'] }],
        declarations: [],
      },
      {
        id: "source-a",
        path: "specification/widgets/feature.tsp",
        hunks: [
          { id: "hunk-other", lines: ["+model Other {}"] },
          { id: "hunk-operation", lines: ["+op create(): void;"] },
        ],
        declarations: [
          {
            hunkIds: ["hunk-operation"],
            source: { revision: "current", startLine: 20 },
          },
        ],
      },
    ],
    operations: [
      {
        sources: [
          {
            id: "source-a",
            hunks: [{ id: "hunk-operation" }],
          },
        ],
      },
    ],
  };

  assert.equal(
    representativeSource(item).path,
    "specification/widgets/feature.tsp",
  );
  assert.deepEqual(
    representativeSource(item).hunks.map((hunk) => hunk.id),
    ["hunk-operation"],
  );
});

test("representative source uses hunk position before hunk ID", () => {
  const selected = representativeSource({
    sources: [
      {
        id: "source-1",
        path: "specification/widgets/main.tsp",
        hunks: [
          {
            id: "hunk-a",
            current: { startLine: 100 },
            lines: ["+model Later {}"],
          },
          {
            id: "hunk-z",
            current: { startLine: 10 },
            lines: ["+model Earlier {}"],
          },
        ],
        declarations: [],
      },
    ],
    operations: [],
  });

  assert.equal(selected.hunks[0].id, "hunk-z");
});

test("renderer shows one intent example and keeps complete appendix evidence", () => {
  const sources = [
    {
      id: "source-1",
      path: "specification/widgets/main.tsp",
      hunks: [
        { id: "hunk-1", lines: ["+model Widget {}"] },
        { id: "hunk-2", lines: ["+op create(): Widget;"] },
      ],
      declarations: [],
    },
  ];
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    comparison: { baseCommit: "base", headCommit: "head" },
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "passed" },
    dimensions: {
      semantic: {
        status: "assessed",
        sourceHunkIds: ["hunk-1", "hunk-2"],
        items: [
          {
            id: "semantic-1",
            action: "add",
            title: "Add widgets",
            summary: "Adds the widget API.",
            sources,
            operations: [],
            relatedFindings: {},
            deterministicCoverage: {
              coveredHunkIds: ["hunk-1"],
              uncoveredHunkIds: ["hunk-2"],
              classifications: [
                { hunkId: "hunk-1", status: "no-impact" },
                { hunkId: "hunk-2", status: "unknown" },
              ],
            },
            inferenceRequired: true,
            inferenceResults: [
              {
                requestId: "inference-request-1",
                hunkId: "hunk-2",
                decision: "no-impact",
                rationale: "No contract impact.",
                candidateIds: [],
              },
            ],
          },
        ],
      },
      rest: { status: "passed", findings: [] },
      downstream: { status: "passed", findings: [] },
      compliance: notAssessedCompliance(),
      documentQuality: { status: "not-assessed", summary: "Not assessed." },
    },
    blockers: [],
    projects: [],
    changedFiles: [],
    provenance: {},
  });

  assert.equal(
    (html.match(/<details class="representative-example">/g) ?? []).length,
    1,
  );
  assert.match(
    html,
    /Deterministic coverage:<\/strong> 1 of 2 changed hunks classified\. AI inference used for 1 request\./,
  );
  const appendixHtml = html.slice(
    html.indexOf('id="complete-typespec-evidence"'),
  );
  assert.equal((appendixHtml.match(/class="diff"/g) ?? []).length, 2);
});

test("refreshed baseline preserves semantic and REST-derived downstream links", () => {
  const assessment = JSON.parse(
    readFileSync(
      new URL("../evals/assessments/44742/assessment.json", import.meta.url),
      "utf8",
    ),
  );
  const html = renderAssessmentHtml(assessment);
  const complianceFinding = assessment.dimensions.compliance.findings[0];
  const complianceIntent = assessment.dimensions.semantic.items.find(
    (item) => item.id === complianceFinding.semanticIntentId,
  );
  assert.ok(
    html.includes(`href="#compliance-finding-${complianceFinding.id}"`),
  );
  assert.ok(html.includes(`href="#intent-${complianceIntent.id}"`));

  assert.match(html, /Semantic intents \(4\)/);
  assert.match(html, /Remove NFS file and handle response fields/);
  assert.doesNotMatch(
    html,
    /REST contract changes require generated-client updates/,
  );
  assert.match(html, /Downstream breaking changes \(17\)/);
  assert.match(html, /6 from REST breaking/);
  assert.match(
    html,
    /<span class="origin-tag rest-breaking-tag">REST breaking<\/span>/,
  );
  assert.doesNotMatch(html, /REST-compatible downstream changes/);
  assert.doesNotMatch(
    html,
    /<summary><strong><span class="action"><\/span>\s*<\/strong><\/summary>/,
  );
  assert.ok(
    Object.values(assessment.dimensions).some((dimension) =>
      (dimension.findings ?? []).some((finding) => finding.severity),
    ),
  );
  assert.doesNotMatch(html, /<span class="severity/);
  assert.doesNotMatch(html, /class="finding [^"]*\b(?:high|medium|low)\b/);
  assert.match(html, /\.finding\{border-left:1px solid var\(--line\)\}/);
});

test("renderer links assessed intents with no applicable guidance by title", () => {
  const assessment = JSON.parse(
    readFileSync(
      new URL("../evals/assessments/42853/assessment.json", import.meta.url),
      "utf8",
    ),
  );
  const noGuidance = assessment.dimensions.compliance.intentAssessments.filter(
    (item) => item.decision === "no-applicable-guidance",
  );
  assert.equal(noGuidance.length, 1);
  const intent = assessment.dimensions.semantic.items.find(
    (item) => item.id === noGuidance[0].semanticIntentId,
  );
  const html = renderAssessmentHtml(assessment);
  const complianceHtml = html.slice(
    html.indexOf('<section id="azure-compliance">'),
    html.indexOf('<section id="semantic-intents">'),
  );
  assert.ok(
    complianceHtml.includes(
      `Azure Guidelines were assessed. They passed for the other intents, and no applicable guideline was found for intent <a href="#intent-${intent.id}">${intent.title}</a>.`,
    ),
  );
  assert.doesNotMatch(complianceHtml, /Unassessed intents/);
  assert.doesNotMatch(complianceHtml, /<code>semantic-[^<]+<\/code>/);

  const another = assessment.dimensions.compliance.intentAssessments.find(
    (item) => item.decision === "applicable-pass",
  );
  another.decision = "no-applicable-guidance";
  another.applicableGuidance = [];
  delete another.expected;
  const anotherIntent = assessment.dimensions.semantic.items.find(
    (item) => item.id === another.semanticIntentId,
  );
  const multipleHtml = renderAssessmentHtml(assessment);
  const multipleComplianceHtml = multipleHtml.slice(
    multipleHtml.indexOf('<section id="azure-compliance">'),
    multipleHtml.indexOf('<section id="semantic-intents">'),
  );
  assert.match(
    multipleComplianceHtml,
    /no applicable guideline was found for intents /,
  );
  assert.ok(
    multipleComplianceHtml.includes(
      `<a href="#intent-${intent.id}">${intent.title}</a>`,
    ),
  );
  assert.ok(
    multipleComplianceHtml.includes(
      `<a href="#intent-${anotherIntent.id}">${anotherIntent.title}</a>`,
    ),
  );
});

test("downstream section lists and links REST breaking changes without duplicating details", () => {
  const assessment = JSON.parse(
    readFileSync(
      new URL("../evals/assessments/44742/assessment.json", import.meta.url),
      "utf8",
    ),
  );
  const html = renderAssessmentHtml(assessment);
  assert.match(
    html,
    /<div class="rest-operation-line"><strong><code>Directory_ListFilesAndDirectoriesSegment<\/code><\/strong><span>2026-12-06<\/span><code>GET \?restype=directory&amp;comp=list<\/code><\/div>/,
  );
  assert.match(html, /1 contract changes — 1 affected REST operations/);
  assert.doesNotMatch(html, /contract\(s\)|Compared REST operation:/);
  const downstream = html.slice(
    html.indexOf('<section id="downstream-breaking">'),
    html.indexOf('<section id="azure-compliance">'),
  );

  assert.match(downstream, /<h2>Downstream breaking changes \(17\)<\/h2>/);
  assert.equal(
    (
      downstream.match(
        /class="origin-tag rest-breaking-tag">REST breaking<\/span>/g,
      ) ?? []
    ).length,
    6,
  );
  assert.equal((downstream.match(/href="#rest-contract-/g) ?? []).length, 6);
  assert.doesNotMatch(downstream, /Approved REST finding/);
  assert.doesNotMatch(downstream, /REST-compatible downstream changes/);
});

test("renderer keeps API versions in the appendix", () => {
  const assessment = JSON.parse(
    readFileSync(
      new URL("../evals/assessments/42853/assessment.json", import.meta.url),
      "utf8",
    ),
  );
  const html = renderAssessmentHtml(assessment);

  assert.match(
    html,
    /TypeSpec source diff: <code>519e87e016492a37ce9ea6ac0fdf80d26767f47d<\/code> → <code>efe76fb07ac03d9c54e2c64de15ef3ff90fc4030<\/code>/,
  );
  assert.match(
    html,
    /<code>efe76fb07ac03d9c54e2c64de15ef3ff90fc4030@2026-01-01<\/code>/,
  );
  assert.match(
    html,
    /<code>efe76fb07ac03d9c54e2c64de15ef3ff90fc4030@2026-02-01<\/code>/,
  );
  assert.match(html, /new-api-version/);
  assert.match(html, /previous-latest-stable/);
  assert.match(html, /newest-added-version/);
});

test("renderer presents assessment blockers as potential limits in the appendix", () => {
  const assessment = JSON.parse(
    readFileSync(
      new URL("../evals/assessments/42853/assessment.json", import.meta.url),
      "utf8",
    ),
  );
  assessment.blockers = [
    "Before/after evidence is incomplete for <one> candidate.",
  ];
  const html = renderAssessmentHtml(assessment);

  assert.doesNotMatch(html, /<section id="blockers">/);
  assert.doesNotMatch(html, /<h2>Blockers<\/h2>/);
  assert.match(
    html,
    /<section id="appendix"><details class="dimension-details"><summary><h2>Appendix<\/h2><\/summary><div class="panel"><h3 id="potential-limits">Potential limits<\/h3>/,
  );
  assert.match(
    html,
    /Before\/after evidence is incomplete for &lt;one&gt; candidate\./,
  );
});

test("renderer derives overall code quality from assessed dimensions", () => {
  const highAssessment = JSON.parse(
    readFileSync(
      new URL("../evals/assessments/45348/assessment.json", import.meta.url),
      "utf8",
    ),
  );
  const mediumAssessment = JSON.parse(
    readFileSync(
      new URL("../evals/assessments/45536/assessment.json", import.meta.url),
      "utf8",
    ),
  );
  const compliance = mediumAssessment.dimensions.compliance;
  compliance.status = "not-assessed";
  compliance.summary = "Compliance evidence is incomplete.";
  compliance.coverage.assessedIntentCount = 0;
  compliance.coverage.unassessedIntentIds = compliance.intentAssessments.map(
    (item) => item.semanticIntentId,
  );
  compliance.blockers = compliance.intentAssessments.map((item) => ({
    reviewUnitId: item.semanticIntentId,
    message: "test evidence blocker",
  }));
  for (const item of compliance.intentAssessments) {
    item.decision = "not-assessed";
    item.applicableGuidance = [];
    item.blockers = ["test evidence blocker"];
  }

  assert.match(
    renderAssessmentHtml(highAssessment),
    /<span class="pass">✓<\/span> Passed<\/div><div class="summary-label">Overall code quality/,
  );
  assert.match(
    renderAssessmentHtml(mediumAssessment),
    /<span class="">i<\/span> Not assessed<\/div><div class="summary-label">Overall code quality/,
  );
});
