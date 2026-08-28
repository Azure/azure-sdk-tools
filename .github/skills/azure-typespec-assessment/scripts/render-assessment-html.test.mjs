import assert from "node:assert/strict";
import test from "node:test";
import {
  escapeHtml,
  renderAssessmentHtml,
  representativeSource,
} from "./render-assessment-html.mjs";

test("escapeHtml escapes Agent and source text", () => {
  assert.equal(escapeHtml('<script x="1">&'), "&lt;script x=&quot;1&quot;&gt;&amp;");
});

test("renderer labels deferred dimensions and scoped coverage", () => {
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    comparison: {
      baseRef: "origin/main",
      baseCommit: "9f0ad696cc186c2d16cb522abc0fbd4aa3854ca5",
      headCommit: "780a61ace56c22ce10dd01caa8ab95ca4514ac2e",
      workingTree: {},
    },
    artifactComparisons: [{
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
    }, {
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
    }],
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "passed" },
    dimensions: {
      semantic: { status: "assessed", items: [], blockers: [] },
      rest: { status: "passed", findings: [], blockers: [] },
      downstream: { status: "passed", findings: [], blockers: [] },
      compliance: { status: "planned", summary: "Deferred from the MVP." },
      documentQuality: { status: "planned", summary: "Planned." },
    },
    blockers: [],
    projects: [{
      id: "new-version",
      path: "specification/widgets/new-version",
      artifacts: {},
    }, {
      id: "existing-version",
      path: "specification/widgets/existing-version",
      artifacts: {},
    }],
    changedFiles: [],
    provenance: {},
  });
  assert.match(html, /Azure Compliance/);
  assert.match(html, /Planned \/ Not assessed/);
  assert.match(html, /Document Quality/);
  assert.match(html, /class="eyebrow">TypeSpec Assessment/);
  assert.match(html, /class="summary-grid"/);
  assert.match(html, /<a class="summary-card" href="#rest-breaking">/);
  assert.match(html, /<a class="summary-card" href="#semantic-intents">/);
  assert.match(html, /<a class="summary-card" href="#downstream-breaking">/);
  assert.match(html, /<a class="summary-card" href="#azure-compliance">/);
  assert.match(html, /Overall code safety/);
  assert.match(html, /> 0<\/div><div class="summary-label">Semantic intents/);
  assert.match(html, /0 operations<br>0 Added, 0 Modified, 0 Removed/);
  assert.match(html, /> 0<\/div><div class="summary-label">REST breaking changes/);
  assert.match(html, /> 0<\/div><div class="summary-label">Downstream breaking changes/);
  assert.match(html, /Azure compliance/);
  assert.match(html, /Planned \/ Not assessed/);
  assert.match(
    html,
    /TypeSpec source diff: <a href="#projects-and-compiler-status">See Projects and compiler status in Appendix<\/a>/,
  );
  assert.match(
    html,
    /<h3 id="projects-and-compiler-status">Projects and compiler status<\/h3>/,
  );
  assert.doesNotMatch(
    html.slice(0, html.indexOf("</header>")),
    /9f0ad696cc186c2d16cb522abc0fbd4aa3854ca5|780a61ace56c22ce10dd01caa8ab95ca4514ac2e/,
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
    artifactComparisons: [{
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
    }],
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "failed" },
    dimensions: {
      semantic: {
        status: "assessed",
        sourceHunkIds: ["hunk-1"],
        items: [{
          id: "semantic-1",
          action: "modify",
          title: "Modify get",
          summary: "Change the SDK projection.",
          sources: [source],
          operations: [{
            operationId: "Widgets_Get",
            apiVersion: "v1",
            method: "get",
            path: "/widgets",
            changedAspects: [],
            restChanged: false,
            outcome: "HTTP signature and represented payload contract unchanged.",
          }],
          relatedFindings: { downstream: ["downstream-group-1"] },
        }],
      },
      rest: { status: "passed", findings: [] },
      downstream: {
        status: "failed",
        findings: [{
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
        }, {
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
        }],
        operationGroups: [{
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
              changes: {
                added: [{
                  parameter: {
                    name: "afcManagedSync",
                    optional: true,
                    type: "boolean",
                  },
                  index: 3,
                }],
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
              before: "basic",
              after: "lro",
            },
          ],
          relatedSemanticIntents: ["semantic-1"],
        }],
      },
      compliance: { status: "planned", summary: "Deferred." },
      documentQuality: { status: "planned", summary: "Planned." },
    },
    blockers: [],
    projects: [{
      id: "project-1",
      path: "specification/widgets",
      artifacts: {},
    }],
    changedFiles: [],
    provenance: {},
  });

  assert.match(html, /Affected REST operations \(1\)/);
  assert.match(html, /HTTP signature and represented payload contract unchanged/);
  assert.match(html, /Contoso\.Widgets\.get/);
  assert.match(html, /Parameters:<\/strong> 1 added, 3 unchanged/);
  assert.match(html, /afcManagedSync\?: boolean/);
  assert.match(html, /class="parameter-line add"/);
  assert.match(html, /Method kind/);
  assert.match(
    html,
    /<a class="summary-card" href="#downstream-breaking"><div class="summary-value"><span class="fail">×<\/span> Failed/,
  );
  assert.match(
    html,
    /TypeSpec source diff: <code>base@v1<\/code> → <code>head@v1<\/code>/,
  );
  assert.doesNotMatch(html, /method-parameters-changed/);
  assert.doesNotMatch(html, /&quot;name&quot;:&quot;afcManagedSync&quot;/);
  assert.match(html, /get is Lro/);
  assert.match(html, /Representative TypeSpec example/);
  assert.match(html, /Complete TypeSpec source evidence/);
  assert.match(html, /<details class="intent" id="intent-semantic-1"><summary>/);
  assert.doesNotMatch(html, /<details class="intent"[^>]* open/);
  assert.match(html, /<details class="representative-example"><summary>/);
  assert.doesNotMatch(html, /<details class="representative-example"[^>]* open/);
  const operationCards = html.match(/<details class="operation">.*?<\/details>/gs) ?? [];
  assert.equal(operationCards.length, 1);
  assert.doesNotMatch(operationCards[0], /class="diff"/);
});

test("renderer reports operations omitted from large semantic intents", () => {
  const source = {
    id: "source-1",
    path: "specification/widgets/main.tsp",
    hunks: [{ id: "hunk-1", lines: ["+  v1: \"v1\","] }],
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
        items: [{
          id: "semantic-1",
          action: "modify",
          title: "Publish v1",
          summary: "Publish the new version.",
          sources: [source],
          operations,
          relatedFindings: {},
        }],
      },
      rest: { status: "passed", findings: [] },
      downstream: { status: "passed", findings: [] },
      compliance: { status: "planned", summary: "Deferred." },
      documentQuality: { status: "planned", summary: "Planned." },
    },
    blockers: [],
    projects: [],
    changedFiles: [],
    provenance: {},
  });

  assert.match(html, /3 representative operations are shown below and 13 are omitted from HTML/);
  assert.doesNotMatch(html, /Widgets_Operation03/);
});

test("representative source prefers operation evidence and stable ordering", () => {
  const item = {
    sources: [{
      id: "source-b",
      path: "specification/widgets/z-context.tsp",
      hunks: [{ id: "hunk-context", lines: ['+import "./feature.tsp";'] }],
      declarations: [],
    }, {
      id: "source-a",
      path: "specification/widgets/feature.tsp",
      hunks: [
        { id: "hunk-other", lines: ["+model Other {}"] },
        { id: "hunk-operation", lines: ["+op create(): void;"] },
      ],
      declarations: [{
        hunkIds: ["hunk-operation"],
        source: { revision: "current", startLine: 20 },
      }],
    }],
    operations: [{
      sources: [{
        id: "source-a",
        hunks: [{ id: "hunk-operation" }],
      }],
    }],
  };

  assert.equal(representativeSource(item).path, "specification/widgets/feature.tsp");
  assert.deepEqual(
    representativeSource(item).hunks.map((hunk) => hunk.id),
    ["hunk-operation"],
  );
});

test("representative source uses hunk position before hunk ID", () => {
  const selected = representativeSource({
    sources: [{
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
    }],
    operations: [],
  });

  assert.equal(selected.hunks[0].id, "hunk-z");
});

test("renderer shows one intent example and keeps complete appendix evidence", () => {
  const sources = [{
    id: "source-1",
    path: "specification/widgets/main.tsp",
    hunks: [
      { id: "hunk-1", lines: ["+model Widget {}"] },
      { id: "hunk-2", lines: ["+op create(): Widget;"] },
    ],
    declarations: [],
  }];
  const html = renderAssessmentHtml({
    schemaVersion: 1,
    comparison: { baseCommit: "base", headCommit: "head" },
    confidence: "high",
    safety: { scope: "rest-and-downstream-only", status: "passed" },
    dimensions: {
      semantic: {
        status: "assessed",
        sourceHunkIds: ["hunk-1", "hunk-2"],
        items: [{
          id: "semantic-1",
          action: "add",
          title: "Add widgets",
          summary: "Adds the widget API.",
          sources,
          operations: [],
          relatedFindings: {},
        }],
      },
      rest: { status: "passed", findings: [] },
      downstream: { status: "passed", findings: [] },
      compliance: { status: "planned", summary: "Deferred." },
      documentQuality: { status: "planned", summary: "Planned." },
    },
    blockers: [],
    projects: [],
    changedFiles: [],
    provenance: {},
  });
  assert.equal((html.match(/<details class="representative-example">/g) ?? []).length, 1);
  const appendixHtml = html.slice(html.indexOf('id="complete-typespec-evidence"'));
  assert.equal((appendixHtml.match(/class="diff"/g) ?? []).length, 2);
});
