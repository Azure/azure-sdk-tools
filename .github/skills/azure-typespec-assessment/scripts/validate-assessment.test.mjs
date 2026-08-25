import assert from "node:assert/strict";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  correctHistoricalSemanticChains,
  normalizeAssessmentSourceLinks,
} from "../test-evidence/scripts/finalize-rerun-assessments.mjs";
import { buildCompliance } from "../test-evidence/scripts/document-compliance.mjs";
import { deriveOperationChanges } from "../test-evidence/scripts/operation-changes.mjs";
import { deriveCodeSafety } from "./assessment-display.mjs";
import { renderAssessmentHtml } from "./render-assessment-html.mjs";
import { validateAssessment } from "./validate-assessment.mjs";

const sourceReference = {
  path: "spec/main.tsp",
  revision: "head",
  startLine: 2,
  endLine: 4,
  link: "spec/main.tsp#L2-L4",
};

const focusedCodeSnippets = [
  {
    path: "spec/main.tsp",
    startLine: 2,
    endLine: 4,
    lines: ["model Widget {", "  name: string;", "}"],
  },
];

function validDocument() {
  const operation = {
    operationId: "Widgets_Get",
    apiVersions: ["2026-01-01"],
    method: "GET",
    path: "/widgets/{widgetName}",
    signature: "GET /widgets/{widgetName}",
    parameters: ["path widgetName: string, required"],
    requestPayload: "none",
    responsePayloads: [
      "200 application/json payload: Widget",
      "default application/json payload: ErrorResponse",
    ],
    serviceBehavior: "Returns one widget.",
    lro: { isLongRunning: false },
    paging: { isPaged: false },
    sourceReferences: [sourceReference],
  };
  const item = {
    id: "semantic-1",
    intent: "Add a default value.",
    transformationChain: [],
    restRepresentation: {
      summary: "Returns one widget.",
      operations: [operation],
    },
    confidence: "high",
    sourceReferences: [sourceReference],
  };
  item.changes = deriveOperationChanges(item, [operation], {
    typeSpecDiffs: [
      {
        path: "spec/main.tsp",
        oldStart: 2,
        oldCount: 0,
        newStart: 2,
        newCount: 3,
        context: "model Widget",
        lines: [
          "+@added(Versions.v2)",
          "+model Widget {",
          '+  name: string = "widget";',
        ],
      },
    ],
    linkedFindingIds: [],
  });
  return {
    schemaVersion: 2,
    baseline: { ref: "origin/main", commit: "base-commit" },
    head: { commit: "head-commit", hasWorkingTreeChanges: false },
    overallConfidence: "high",
    assessmentEvidence: {},
    dimensions: {
      semanticUnderstanding: {
        items: [item],
      },
      restBreakingChanges: { findings: [] },
      restCompatibleDownstreamBreakingChanges: { findings: [] },
      azureCompliance: {
        status: "passed",
        summary: {
          patternsAssessed: 1,
          findingCount: 0,
        },
        documents: [
          {
            title: "Models",
            url: "https://typespec.io/docs/language-basics/models/",
            section: "Model properties",
            guidanceExcerpt:
              "Models are collections of named properties and their types.",
            applicableGuidance: "Models define properties and defaults.",
            evidence: "The changed property uses the documented model syntax.",
            expectedCodeStatus: "available",
            expectedCodeSnippets: [
              {
                language: "tsp",
                caption: "Documented model example",
                url: "https://typespec.io/docs/language-basics/models/",
                section: "Model properties",
                lines: ["model Widget {", "  name: string;", "}"],
              },
            ],
            sourceReferences: [sourceReference],
          },
        ],
        findings: [],
      },
    },
  };
}

test("valid assessment passes", () => {
  assert.deepEqual(validateAssessment(validDocument()), []);
});

test("finalized GitHub assessments use commit-pinned source links", () => {
  const document = validDocument();
  document.url = "https://github.com/Azure/example/pull/123";
  document.baseline = { commit: "abc123" };
  document.head = { commit: "def456" };
  normalizeAssessmentSourceLinks(document);
  assert.equal(
    document.dimensions.semanticUnderstanding.items[0].sourceReferences[0].link,
    "https://github.com/Azure/example/blob/def456/spec/main.tsp#L2-L4",
  );
  document.dimensions.semanticUnderstanding.items[0].sourceReferences[0].revision =
    "base";
  normalizeAssessmentSourceLinks(document);
  assert.equal(
    document.dimensions.semanticUnderstanding.items[0].sourceReferences[0].link,
    "https://github.com/Azure/example/blob/abc123/spec/main.tsp#L2-L4",
  );
});

test("overall code safety reflects assessment risk", () => {
  const document = validDocument();
  assert.equal(deriveCodeSafety(document), "High");
  document.dimensions.azureCompliance.status = "not-assessed";
  assert.equal(deriveCodeSafety(document), "Medium");
  document.dimensions.restBreakingChanges.findings.push({
    id: "rest-high",
    title: "High severity REST break",
    severity: "high",
    confidence: "high",
    summary: "An existing wire contract is removed.",
    evidence: ["AutoRest diff"],
    sourceReferences: [sourceReference],
  });
  assert.equal(deriveCodeSafety(document), "Low");
});

test("findings require source references", () => {
  const document = validDocument();
  document.dimensions.restCompatibleDownstreamBreakingChanges.findings.push({
    id: "downstream-1",
    title: "Method moved",
    sourceReferences: [],
  });
  assert.match(
    validateAssessment(document).join("\n"),
    /requires sourceReferences/,
  );
});

test("breaking findings require the complete finding contract", () => {
  const document = validDocument();
  document.dimensions.restBreakingChanges.findings.push({
    id: "rest-incomplete",
    title: "Incomplete finding",
    confidence: "high",
    summary: "Severity is missing.",
    evidence: ["Source diff"],
    sourceReferences: [sourceReference],
  });
  assert.match(
    validateAssessment(document).join("\n"),
    /restBreakingChanges\.findings\[0\]\.severity is invalid/,
  );
});

test("semantic operations require complete REST behavior", () => {
  const document = validDocument();
  delete document.dimensions.semanticUnderstanding.items[0].restRepresentation
    .operations[0].responsePayloads;
  assert.match(
    validateAssessment(document).join("\n"),
    /responsePayloads is required/,
  );
});

test("semantic items require every renderer input", () => {
  for (const field of ["id", "intent", "transformationChain", "confidence"]) {
    const document = validDocument();
    delete document.dimensions.semanticUnderstanding.items[0][field];
    assert.match(
      validateAssessment(document).join("\n"),
      new RegExp(
        `semanticUnderstanding\\.items\\[0\\]\\.${field} (?:is required|is invalid|must be)`,
      ),
    );
  }
  const document = validDocument();
  document.dimensions.semanticUnderstanding.items[0].transformationChain = [42];
  assert.match(
    validateAssessment(document).join("\n"),
    /transformationChain must be an array of non-empty strings/,
  );
});

test("semantic understanding excludes internal generator terminology", () => {
  const document = validDocument();
  const change = document.dimensions.semanticUnderstanding.items[0].changes[0];
  change.aspects[0].before = "TCGC exposes a fixed enum.";
  assert.match(
    validateAssessment(document).join("\n"),
    /must explain TypeSpec and REST behavior without internal generator terminology/,
  );
});

test("breaking findings exclude internal generator terminology", () => {
  const document = validDocument();
  document.dimensions.restCompatibleDownstreamBreakingChanges.findings.push({
    id: "sdk-shape",
    title: "Generated shape changes",
    severity: "high",
    confidence: "high",
    summary: "The generated enum becomes extensible.",
    evidence: ["TCGC changes isFixed from true to false."],
    sourceReferences: [sourceReference],
  });
  assert.match(
    validateAssessment(document).join("\n"),
    /must describe public API or SDK behavior without internal generator terminology/,
  );
});

test("structured changes require complete kind-specific before and after data", () => {
  const document = validDocument();
  const change = document.dimensions.semanticUnderstanding.items[0].changes[0];
  change.kind = "modified";
  change.aspects[0].before = null;
  change.aspects[0].after = null;
  assert.match(
    validateAssessment(document).join("\n"),
    /must contain before or after for modified/,
  );
});

test("added changes require @added with the corresponding declaration", () => {
  const document = validDocument();
  const change = document.dimensions.semanticUnderstanding.items[0].changes[0];
  change.typeSpecDiffs[0].lines = [
    "+operationWithoutVersioning is ArmResourceRead<Widget>;",
  ];
  assert.match(
    validateAssessment(document).join("\n"),
    /must show @added with its added operation or model declaration/,
  );
});

test("changes without @added declarations are not classified as added", () => {
  const item = {
    id: "client-customization",
    intent: "Add a stable API version while preserving client placement.",
    transformationChain: ["Update a client customization."],
    restRepresentation: { summary: "The wire operation already exists." },
    sourceReferences: [sourceReference],
  };
  const operation =
    validDocument().dimensions.semanticUnderstanding.items[0].restRepresentation
      .operations[0];
  const changes = deriveOperationChanges(item, [operation], {
    typeSpecDiffs: [
      {
        path: "spec/client.tsp",
        oldStart: 1,
        oldCount: 1,
        newStart: 1,
        newCount: 1,
        lines: [
          '-@@clientLocation(Widget.read, "!csharp")',
          '+@@clientLocation(Widget.read, "!csharp,!go")',
        ],
      },
    ],
  });
  assert.equal(changes[0].kind, "modified");
});

test("semantic changes require real TypeSpec diff hunks", () => {
  const document = validDocument();
  document.dimensions.semanticUnderstanding.items[0].changes[0].typeSpecDiffs =
    [];
  assert.match(
    validateAssessment(document).join("\n"),
    /typeSpecDiffs must be a non-empty array/,
  );
});

test("HTML report prioritizes semantic changes and linked impacts", () => {
  const document = validDocument();
  document.dimensions.restBreakingChanges.findings.push({
    id: "html-impact",
    title: "HTML linked impact",
    severity: "medium",
    confidence: "high",
    summary: "The changed contract affects existing callers.",
    evidence: ["Artifact diff"],
    sourceReferences: [sourceReference],
  });

  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds =
    ["html-impact"];
  const html = renderAssessmentHtml(document);
  assert.ok(
    html.indexOf('<section id="rest-breaking">') <
      html.indexOf('<section id="downstream-breaking">') &&
      html.indexOf('<section id="downstream-breaking">') <
        html.indexOf('<section id="azure-compliance">') &&
      html.indexOf('<section id="azure-compliance">') <
        html.indexOf('<section id="semantic-intents">'),
  );
  assert.match(html, /<!doctype html>/);
  assert.match(html, /<aside class="preview-notice">/);
  assert.match(
    html,
    /The TypeSpec Assessment Assistant is currently in preview/,
  );
  assert.match(html, /does not replace official ARM API/);
  assert.ok(
    html.indexOf('<aside class="preview-notice">') < html.indexOf("<nav>"),
  );
  assert.match(html, /<section id="semantic-intents">/);
  assert.match(html, /<section id="rest-breaking">/);
  assert.match(html, /<section id="downstream-breaking">/);
  assert.match(html, /<section id="azure-compliance">/);
  assert.match(html, /<section id="appendix">/);
  assert.match(html, /<details class="appendix-details">/);
  assert.doesNotMatch(html, /<details class="appendix-details" open>/);
  assert.match(html, /Assessment Errors/);
  assert.doesNotMatch(html, /Code-to-Guidance Evidence/);
  assert.match(html, /Tooling Used/);
  assert.match(html, /Artifact Evidence/);
  assert.doesNotMatch(html, /Impact overview|Detailed findings/);
  assert.match(html, /Overall code safety/);
  assert.doesNotMatch(html, /Code safety: <strong/);
  assert.match(html, /Overall confidence: <strong[^>]*>High<\/strong>/);
  assert.doesNotMatch(html, /<span>Overall confidence<\/span>/);
  assert.equal((html.match(/class="metric(?: [^"]*)?"/g) ?? []).length, 5);
  assert.equal((html.match(/class="metric-title"/g) ?? []).length, 5);
  assert.match(html, /metric-info[^>]*[^<]*ℹ/);
  assert.match(html, /metric-good[^>]*[^<]*✓/);
  assert.match(html, /metric-danger[^>]*[^<]*✕/);
  assert.match(
    html,
    /<span class="metric-title">Semantic intents<\/span><span class="metric-detail">1 operations<br>1 Added/,
  );
  assert.match(
    html,
    /href="#azure-compliance"><strong>[\s\S]*?<\/span>0<\/strong><span class="metric-title">Azure compliance<\/span>/,
  );
  assert.match(html, /1 Added, 0 Modified, 0 Removed/);
  assert.doesNotMatch(html, /A\/M\/R/);
  assert.equal(
    (html.match(/<details class="intent-details"/g) ?? []).length,
    1,
  );
  assert.doesNotMatch(html, /<details class="intent-details"[^>]* open>/);
  assert.match(html, /TypeSpec change:/);
  assert.match(html, /href="#finding-html-impact"/);
  assert.match(html, /class="add">\+  name: string = &quot;widget&quot;;/);
  assert.match(
    html,
    /<th>After<\/th><th>Impact<\/th>[\s\S]*href="#finding-html-impact"/,
  );
  assert.ok(
    html.indexOf('<div class="operation-changes">') <
      html.indexOf('<div class="diff">'),
  );
  assert.equal(
    (html.match(/<details class="operation-details">/g) ?? []).length,
    1,
  );
  assert.doesNotMatch(html, /<details class="operation-details" open>/);
  assert.match(html, /<code>Widgets_Get<\/code>/);
  assert.match(
    html,
    /class="operation-path tooltip" data-tooltip="GET \/widgets\/\{widgetName\}">GET \/widgets\/\{widgetName\}<\/span>/,
  );
  assert.match(
    html,
    /class="operation-version tooltip" data-tooltip="2026-01-01">2026-01-01<\/span>/,
  );
  assert.doesNotMatch(html, /cursor:help/);
  assert.match(html, /\.tooltip::after\{[^}]*background:var\(--panel\)/);
  assert.match(html, /\.change-card\{[^}]*overflow:visible/);
  assert.match(
    html,
    /<strong>REST API signature<\/strong><code>GET \/widgets\/\{widgetName\}<\/code>/,
  );
  assert.doesNotMatch(
    html,
    /REST details prompt|Need complete REST representations/,
  );

  document.dimensions.semanticUnderstanding.items[0].changes[0].kind =
    "modified";
  document.dimensions.semanticUnderstanding.items[0].changes[0].effect =
    "The generated method moves to a different client; its REST contract is unchanged.";
  const outcomeHtml = renderAssessmentHtml(document);
  assert.match(
    outcomeHtml,
    /<strong>Change outcomes<\/strong><p>The generated method moves to a different client; its REST contract is unchanged\.<\/p>/,
  );

  document.dimensions.semanticUnderstanding.items[0].changes[0].kind =
    "modified";
  document.dimensions.semanticUnderstanding.items[0].changes[0].aspects = [
    {
      field: "paging",
      before: "Response is not pageable.",
      after: "Response uses nextLink paging.",
    },
  ];
  document.dimensions.semanticUnderstanding.items[0].restRepresentation.operations[0].paging =
    {
      isPaged: true,
      itemType: "Widget",
      nextLinkName: "nextLink",
      continuation: "Follow nextLink.",
    };
  const modifiedHtml = renderAssessmentHtml(document);
  assert.match(
    modifiedHtml,
    /<strong>HTTP signature and represented payload contract unchanged<\/strong>/,
  );
  assert.match(
    modifiedHtml,
    /<strong>Change outcomes<\/strong>[\s\S]*<span class="behavior-title">Paging:<\/span>[\s\S]*generated clients now expose Widget values as pageable items/,
  );
  assert.match(modifiedHtml, /follow nextLink\./);
  assert.doesNotMatch(modifiedHtml, /class="typespec-summary"/);
  assert.doesNotMatch(
    modifiedHtml,
    /<span class="behavior-title">LRO:<\/span>/,
  );
  document.dimensions.semanticUnderstanding.items[0].changes[0].aspects = [
    {
      field: "Responses",
      before: "202 with Location.",
      after: "200 with Widget.",
    },
  ];
  assert.match(
    renderAssessmentHtml(document),
    /<strong>REST contract changes<\/strong>[\s\S]*HTTP method and path remain unchanged/,
  );
  document.dimensions.semanticUnderstanding.items[0].changes[0].kind = "added";
  assert.doesNotMatch(
    renderAssessmentHtml(document),
    /<strong>Change outcomes<\/strong>/,
  );
  document.dimensions.semanticUnderstanding.items[0].changes[0].kind =
    "modified";

  const currentOperation =
    document.dimensions.semanticUnderstanding.items[0].restRepresentation
      .operations[0];
  currentOperation.apiVersions = ["2026-01-01"];
  currentOperation.lro = { isLongRunning: false };
  const previousOperation = structuredClone(currentOperation);
  previousOperation.apiVersions = ["2025-01-01"];
  previousOperation.paging = { isPaged: false };
  previousOperation.lro = {
    isLongRunning: true,
    finalStateVia: "location",
  };
  document.dimensions.semanticUnderstanding.items[0].restRepresentation.operations.unshift(
    previousOperation,
  );
  const groupedHtml = renderAssessmentHtml(document);
  assert.equal(
    (groupedHtml.match(/<details class="operation-details">/g) ?? []).length,
    1,
  );
  assert.match(
    groupedHtml,
    /class="operation-version tooltip" data-tooltip="2025-01-01, 2026-01-01"/,
  );
  const signatureChange =
    groupedHtml.match(
      /<div class="rest-signature rest-signature-change">([\s\S]*?)<\/div>/,
    )?.[1] ?? "";
  assert.equal((signatureChange.match(/class="remove"/g) ?? []).length, 1);
  assert.equal((signatureChange.match(/class="add"/g) ?? []).length, 1);
  assert.match(
    signatureChange,
    /LRO = long-running; final-state-via=location; paging = not paged/,
  );
  assert.match(
    signatureChange,
    /LRO = synchronous; paging = paged; item=Widget; next-link=nextLink/,
  );
  document.dimensions.semanticUnderstanding.items[0].restRepresentation.operations =
    [currentOperation];

  document.dimensions.semanticUnderstanding.items[0].changes[0].aspects = [
    {
      field: "client location",
      before: "Old client.",
      after: "New client.",
    },
  ];
  const metadataHtml = renderAssessmentHtml(document);
  assert.match(
    metadataHtml,
    /HTTP signature and represented payload contract unchanged/,
  );
});

test("HTML renders local source locations without stale links", () => {
  const document = validDocument();
  const localReference = {
    ...sourceReference,
    link: "spec/main.tsp#L2-L4",
  };
  const item = document.dimensions.semanticUnderstanding.items[0];
  item.sourceReferences = [localReference];
  item.restRepresentation.operations[0].sourceReferences = [localReference];
  item.changes[0].sourceReferences = [localReference];
  document.dimensions.azureCompliance.documents[0].sourceReferences = [
    localReference,
  ];
  const html = renderAssessmentHtml(document);
  assert.match(html, /<code>main\.tsp:L2-L4<\/code>/);
  assert.doesNotMatch(html, /href="spec\/main\.tsp#L2-L4"/);
});

test("HTML downstream findings include their linked TypeSpec source diff", () => {
  const document = validDocument();
  document.dimensions.restCompatibleDownstreamBreakingChanges.findings.push({
    id: "downstream-source-diff",
    title: "Generated client shape changes",
    severity: "high",
    confidence: "high",
    summary: "The generated client surface changes.",
    evidence: ["The TypeSpec declaration changed."],
    sourceReferences: [sourceReference],
  });
  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds =
    ["downstream-source-diff"];
  const html = renderAssessmentHtml(document);
  assert.match(html, /class="finding-source-diff"/);
  assert.match(
    html,
    /TypeSpec source change <span>expand to inspect why this breaks clients/,
  );
  assert.match(html, /class="add"[^>]*>\+\s+name: string/);
});

test("HTML shows up to ten collapsed intents and sorts impacted intents first", () => {
  const document = validDocument();
  const items = Array.from({ length: 12 }, (_, index) => {
    const item = structuredClone(
      document.dimensions.semanticUnderstanding.items[0],
    );
    item.intent = `Intent ${index + 1}`;
    item.changes[0].linkedFindingIds = index === 11 ? ["impact-last"] : [];
    return item;
  });
  document.dimensions.semanticUnderstanding.items = items;
  document.dimensions.restBreakingChanges.findings.push({
    id: "impact-last",
    title: "Impacted last intent",
    severity: "medium",
    confidence: "high",
    summary: "The last source intent has impact.",
    evidence: ["REST diff"],
    sourceReferences: [sourceReference],
  });
  const html = renderAssessmentHtml(document);
  assert.equal(
    (html.match(/<details class="intent-details"/g) ?? []).length,
    12,
  );
  assert.match(html, /<details class="more-intents">/);
  assert.match(html, /Show 2 more semantic intents/);
  assert.doesNotMatch(
    html,
    /<details class="(?:intent-details|more-intents)"[^>]* open>/,
  );
  assert.ok(html.indexOf("⚠️</span> Intent 12") < html.indexOf("Intent 1"));
});

test("HTML does not group fewer than ten semantic intents", () => {
  const document = validDocument();
  document.dimensions.semanticUnderstanding.items.push(
    structuredClone(document.dimensions.semanticUnderstanding.items[0]),
  );
  const html = renderAssessmentHtml(document);
  assert.equal(
    (html.match(/<details class="intent-details"/g) ?? []).length,
    2,
  );
  assert.doesNotMatch(html, /<details class="more-intents">/);
});

test("HTML collapses compliance findings with linked TypeSpec code", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.findings.push({
    id: "widget-compliance-code",
    title: "Widget does not follow documented guidance",
    severity: "medium",
    summary: "The changed declaration does not use the documented pattern.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["The fetched model guidance requires the documented pattern."],
    sourceReferences: [sourceReference],
    codeSnippets: focusedCodeSnippets,
  });
  document.dimensions.azureCompliance.summary.findingCount = 1;
  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds =
    ["widget-compliance-code"];
  const html = renderAssessmentHtml(document);
  assert.match(
    html,
    /href="#azure-compliance"><strong>[\s\S]*?<\/span>1<\/strong><span class="metric-title">Azure compliance<\/span>/,
  );
  assert.match(
    html,
    /<details class="finding compliance-finding severity-border-medium" id="finding-widget-compliance-code">/,
  );
  assert.doesNotMatch(
    html,
    /<details class="finding compliance-finding[^>]* open>/,
  );
  assert.match(html, /<h4>TypeSpec code<\/h4>/);
  assert.match(html, /class="add">\+  name: string = &quot;widget&quot;;/);
  assert.match(html, /<details class="comparison-details expected-details">/);
  assert.match(html, /<details class="comparison-details actual-details">/);
  assert.doesNotMatch(
    html,
    /<details class="comparison-details (?:expected|actual)-details" open>/,
  );
  assert.match(html, /<strong>Gap:<\/strong>/);
  assert.match(
    html,
    /<strong>TypeSpec source:<\/strong> <a href="[^"]*spec\/main\.tsp#L2-L4">main\.tsp:L2-L4<\/a>/,
  );
  const appendix = html.slice(html.indexOf('<section id="appendix">'));
  assert.doesNotMatch(appendix, /<a href=/);
  assert.doesNotMatch(appendix, /Models — Model properties|main\.tsp:L2-L4/);
});

test("HTML prioritizes decorators named by the TypeSpec change summary", () => {
  const document = validDocument();
  const change = document.dimensions.semanticUnderstanding.items[0].changes[0];
  change.typeSpecCause =
    "Mark `oldOperation` with `@removed`, then add an `@added` replacement.";
  change.typeSpecDiffs = [
    {
      ...structuredClone(change.typeSpecDiffs[0]),
      context: "unrelated suppression",
      lines: [
        '-#suppress "rule" "placeholder"',
        '+#suppress "rule" "explanation"',
      ],
    },
    {
      ...structuredClone(change.typeSpecDiffs[0]),
      context: "old operation",
      lines: ["+@removed(Versions.v2)", "+oldOperation is ActionAsync;"],
    },
    {
      ...structuredClone(change.typeSpecDiffs[0]),
      context: "new operation",
      lines: ["+@added(Versions.v2)", "+newOperation is ActionSync;"],
    },
  ];
  const html = renderAssessmentHtml(document);
  assert.match(html, /\+@removed\(Versions\.v2\)/);
  assert.match(html, /class="add version-decorator">\+@added\(Versions\.v2\)/);
  assert.doesNotMatch(html, /placeholder/);
  assert.deepEqual(validateAssessment(document), []);
});

test("added, modified, and removed changes have distinct icons", () => {
  const markers = {
    added: "➕ Added",
    modified: "✏️ Modified",
    removed: "➖ Removed",
  };
  for (const [kind, marker] of Object.entries(markers)) {
    const document = validDocument();
    const change =
      document.dimensions.semanticUnderstanding.items[0].changes[0];
    change.kind = kind;
    change.aspects[0] =
      kind === "added"
        ? { field: "operation", before: null, after: "Added." }
        : kind === "removed"
          ? { field: "operation", before: "Present.", after: null }
          : { field: "operation", before: "Before.", after: "After." };
    const [icon, label] = marker.split(" ");
    assert.match(
      renderAssessmentHtml(document),
      new RegExp(`<span class="change-badge ${kind}">${icon} ${label}</span>`),
    );
    assert.deepEqual(validateAssessment(document), []);
  }
});

test("semantic impacts link to breaking and compliance findings", () => {
  const document = validDocument();
  document.dimensions.restBreakingChanges.findings.push({
    id: "widget-default-break",
    title: "Widget default changes",
    severity: "medium",
    confidence: "high",
    summary: "Existing clients observe a different default.",
    evidence: ["The TypeSpec default changed."],
    sourceReferences: [sourceReference],
  });

  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds =
    ["widget-default-break"];
  assert.match(
    validateAssessment(document).join("\n"),
    /REST breaking changes require a downstream breaking finding/,
  );
  document.dimensions.restCompatibleDownstreamBreakingChanges.findings.push({
    id: "widget-sdk-break",
    title: "Generated widget clients observe a breaking change",
    severity: "medium",
    confidence: "high",
    summary: "Existing generated client code observes a different default.",
    evidence: ["The generated public client behavior changes."],
    sourceReferences: [sourceReference],
  });
  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds.push(
    "widget-sdk-break",
  );
  const rendered = renderAssessmentHtml(document);
  assert.match(
    rendered,
    /href="#finding-widget-default-break">Widget default changes<\/a>/,
  );
  assert.deepEqual(validateAssessment(document), []);

  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.findings.push({
    id: "widget-compliance-impact",
    title: "Widget does not follow documented guidance",
    severity: "medium",
    summary: "The changed declaration does not use the documented pattern.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["Changed TypeSpec declaration", "Fetched model guidance"],
    sourceReferences: [sourceReference],
    codeSnippets: focusedCodeSnippets,
  });
  document.dimensions.azureCompliance.summary.findingCount = 1;
  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds.push(
    "widget-compliance-impact",
  );
  const renderedWithCompliance = renderAssessmentHtml(document);
  assert.match(
    renderedWithCompliance,
    /href="#finding-widget-compliance-impact">Widget does not follow documented guidance<\/a>/,
  );
  assert.deepEqual(validateAssessment(document), []);
});

test("impact findings must link back to a semantic change", () => {
  const document = validDocument();
  document.dimensions.restBreakingChanges.findings.push({
    id: "unlinked-break",
    title: "Unlinked break",
    severity: "medium",
    confidence: "high",
    summary: "A breaking change without semantic linkage.",
    evidence: ["The TypeSpec changed."],
    sourceReferences: [sourceReference],
  });
  assert.match(
    validateAssessment(document).join("\n"),
    /impact finding unlinked-break must be linked/,
  );
});

test("LRO metadata changes cover every affected operation as modified", () => {
  const item = {
    id: "pr-43308-intent",
    intent: "Make actions visible as LROs and remove cancel.",
    transformationChain: ["Replace OpenAPI-only LRO extensions."],
    restRepresentation: {
      summary: "LRO metadata changes while cancel is removed.",
    },
    sourceReferences: [sourceReference],
  };
  const operation =
    validDocument().dimensions.semanticUnderstanding.items[0].restRepresentation
      .operations[0];
  const changes = deriveOperationChanges(
    item,
    [
      { ...operation, operationId: "ScenarioConfigurations_Execute" },
      { ...operation, operationId: "ScenarioConfigurations_Cancel" },
    ],
    {
      typeSpecDiffs: [],
    },
  );
  assert.deepEqual(
    changes.map((change) => change.kind),
    ["modified"],
  );
  assert.deepEqual(changes[0].operationIds, [
    "ScenarioConfigurations_Execute",
    "ScenarioConfigurations_Cancel",
  ]);
});

test("HTML distinguishes LRO metadata from service behavior", () => {
  const document = validDocument();
  const item = document.dimensions.semanticUnderstanding.items[0];
  const operation = item.restRepresentation.operations[0];
  operation.method = "POST";
  operation.path = "/widgets/{widgetName}:start";
  operation.signature = `${operation.method} ${operation.path}`;
  operation.responsePayloads = [
    "202: no body; Location and Retry-After headers",
    "default application/json payload: ErrorResponse",
  ];
  operation.lro = {
    isLongRunning: true,
    pattern: "arm",
    finalStateVia: "location",
    polling: "Poll Location after Retry-After until terminal completion.",
    finalResult: "Widget result from the Location endpoint.",
  };
  const change = item.changes[0];
  change.kind = "modified";
  change.aspects = [
    {
      field: "TypeSpec LRO metadata",
      before: "Raw OpenAPI extensions.",
      after: '@Azure.Core.useFinalStateVia("location").',
    },
  ];
  const html = renderAssessmentHtml(document);
  assert.match(html, /<strong>Change outcomes<\/strong>/);
  assert.match(
    html,
    /<span class="behavior-title">LRO:<\/span>[\s\S]*Service behavior is unchanged:/,
  );
  assert.match(html, /202: no body; Location and Retry-After headers/);
  assert.match(html, /Widget result from the Location endpoint/);
  assert.doesNotMatch(html, /completion\.,/);
  assert.doesNotMatch(html, /endpoint\.\./);
  assert.doesNotMatch(html, /<dl>/);
});

test("HTML intent headings show every represented change kind", () => {
  const document = validDocument();
  const item = document.dimensions.semanticUnderstanding.items[0];
  const added = structuredClone(item.changes[0]);
  added.kind = "added";
  item.changes[0].kind = "modified";
  const removed = structuredClone(item.changes[0]);
  removed.kind = "removed";
  item.changes = [added, item.changes[0], removed];

  const html = renderAssessmentHtml(document);

  assert.match(html, /intent-kind-icon" title="Added">➕/);
  assert.match(html, /intent-kind-icon" title="Modified">✏️/);
  assert.match(html, /intent-kind-icon" title="Removed">➖/);
});

test("failed compliance requires documented source-linked findings", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.findings.push({
    id: "compliance-standard-operation",
    title: "Operation does not use the documented template",
    severity: "medium",
    summary: "The operation duplicates a standard resource operation.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["Changed operation signature", "Fetched operation guidance"],
    sourceReferences: [sourceReference],
    codeSnippets: focusedCodeSnippets,
  });
  document.dimensions.azureCompliance.summary.findingCount = 1;
  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds =
    ["compliance-standard-operation"];
  assert.deepEqual(validateAssessment(document), []);
  const rendered = renderAssessmentHtml(document);
  assert.match(rendered, /<strong>Gap:<\/strong> The operation duplicates/);
  assert.match(rendered, /<summary>Expected<\/summary>/);
  assert.match(rendered, /<summary>Actual<\/summary>/);
  assert.match(rendered, /Documented model example/);
});

test("expected code must retain authoritative document provenance", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.summary.findingCount = 1;
  document.dimensions.azureCompliance.findings.push({
    id: "widget-expected-code",
    title: "Widget expected code",
    severity: "medium",
    summary: "The widget differs from the documented example.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["The declaration uses another pattern."],
    sourceReferences: [sourceReference],
    codeSnippets: focusedCodeSnippets,
  });
  const expected =
    document.dimensions.azureCompliance.documents[0].expectedCodeSnippets[0];
  expected.url = "https://typespec.io/docs/";
  assert.match(
    validateAssessment(document).join("\n"),
    /url must match its compliance document/,
  );
  expected.url = document.dimensions.azureCompliance.documents[0].url;
  expected.lines = Array.from({ length: 13 }, (_, index) => `line ${index}`);
  assert.match(
    validateAssessment(document).join("\n"),
    /lines must contain at most 12 documented lines/,
  );
});

test("missing expected code requires an explicit reason", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.summary.findingCount = 1;
  document.dimensions.azureCompliance.findings.push({
    id: "widget-no-expected-code",
    title: "Widget without expected code",
    severity: "medium",
    summary: "The guidance requires deleting the declaration.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["The declaration remains."],
    sourceReferences: [sourceReference],
    codeSnippets: focusedCodeSnippets,
  });
  const guidance = document.dimensions.azureCompliance.documents[0];
  guidance.expectedCodeStatus = "not-present";
  delete guidance.expectedCodeSnippets;
  assert.match(
    validateAssessment(document).join("\n"),
    /expectedCodeReason is required/,
  );
  guidance.expectedCodeReason =
    "The guidance requires deletion and has no replacement example.";
  assert.doesNotMatch(
    validateAssessment(document).join("\n"),
    /expectedCodeReason is required/,
  );
});

test("compliance findings must cite a fetched document", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.findings.push({
    id: "unsupported-guidance",
    title: "Finding without fetched guidance",
    severity: "medium",
    summary: "The finding cites a page that was not fetched.",
    documentationUrl: "https://typespec.io/docs/",
    evidence: ["Changed source"],
    sourceReferences: [sourceReference],
    codeSnippets: focusedCodeSnippets,
  });
  document.dimensions.azureCompliance.summary.findingCount = 1;
  assert.match(
    validateAssessment(document).join("\n"),
    /must match a fetched compliance document/,
  );
});

test("compliance documents require fetched guidance evidence", () => {
  const document = validDocument();
  delete document.dimensions.azureCompliance.documents[0].guidanceExcerpt;
  assert.match(
    validateAssessment(document).join("\n"),
    /guidanceExcerpt must be a short fetched-content excerpt/,
  );
});

test("compliance code snippets must match and stay within source references", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.summary.findingCount = 1;
  document.dimensions.azureCompliance.findings.push({
    id: "widget-code-evidence",
    title: "Widget code evidence",
    severity: "medium",
    summary: "The widget declaration does not follow the documented pattern.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["The declaration uses a different pattern."],
    sourceReferences: [sourceReference],
    codeSnippets: [
      {
        path: "spec/main.tsp",
        startLine: 2,
        endLine: 4,
        lines: ["model Widget {", "  name: string;", "}"],
      },
    ],
  });
  document.dimensions.semanticUnderstanding.items[0].changes[0].linkedFindingIds.push(
    "widget-code-evidence",
  );
  assert.deepEqual(validateAssessment(document), []);

  document.dimensions.azureCompliance.findings[0].codeSnippets[0].lines.pop();
  assert.match(
    validateAssessment(document).join("\n"),
    /lines must cover the declared line range/,
  );

  document.dimensions.azureCompliance.findings[0].codeSnippets[0] = {
    path: "spec/main.tsp",
    startLine: 1,
    endLine: 1,
    lines: ["namespace Widgets;"],
  };
  assert.match(
    validateAssessment(document).join("\n"),
    /must be covered by a finding source reference/,
  );
});

test("failed compliance requires at most two focused snippets", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.summary.findingCount = 1;
  document.dimensions.azureCompliance.findings.push({
    id: "widget-focused-code",
    title: "Widget code evidence",
    severity: "medium",
    summary: "The widget declaration does not follow the documented pattern.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["The declaration uses a different pattern."],
    sourceReferences: [sourceReference],
  });
  assert.match(
    validateAssessment(document).join("\n"),
    /codeSnippets must contain one or two focused snippets/,
  );

  const finding = document.dimensions.azureCompliance.findings[0];
  finding.codeSnippets = [1, 2, 3].map(() =>
    structuredClone(focusedCodeSnippets[0]),
  );
  assert.match(
    validateAssessment(document).join("\n"),
    /codeSnippets must contain one or two focused snippets/,
  );

  finding.codeSnippets = [
    {
      path: "spec/main.tsp",
      startLine: 2,
      endLine: 14,
      lines: Array.from({ length: 13 }, (_, index) => `line ${index + 2}`),
    },
  ];
  finding.sourceReferences = [
    {
      ...sourceReference,
      endLine: 14,
      link: "spec/main.tsp#L2-L14",
    },
  ];
  assert.match(
    validateAssessment(document).join("\n"),
    /must contain at most 12 focused lines/,
  );
});

test("not-assessed compliance requires a reason and empty findings", () => {
  const document = validDocument();
  document.dimensions.azureCompliance = {
    status: "not-assessed",
    reason: "Relevant authoritative documentation could not be retrieved.",
    documents: [],
    findings: [],
  };
  assert.deepEqual(validateAssessment(document), []);
});

test("overall confidence is required", () => {
  const document = validDocument();
  delete document.overallConfidence;
  assert.match(
    validateAssessment(document).join("\n"),
    /overallConfidence must be high, medium, or low/,
  );
});

test("HTML renders the execution-time dimension breakdown in the Appendix", () => {
  const document = validDocument();
  document.assessmentDuration = {
    totalMs: 40000,
    note: "Timing quality is retained.",
    breakdown: {
      semanticUnderstandingMs: 10000,
      semanticUnderstandingQuality: "estimated",
      restBreakingMs: 5000,
      restBreakingQuality: "estimated",
      downstreamBreakingMs: 5000,
      downstreamBreakingQuality: "estimated",
      complianceMs: 15000,
      complianceQuality: "measured",
      overheadMs: 5000,
      overheadQuality: "derived",
      totalMs: 40000,
      totalQuality: "measured",
      searchRoute: "catalog only",
    },
  };
  const html = renderAssessmentHtml(document);
  assert.match(
    html,
    /<details class="appendix-details">[\s\S]*<h3>Execution time breakdown<\/h3>[\s\S]*Semantic understanding[\s\S]*<\/details>/,
  );
  assert.doesNotMatch(html, /href="#execution-time"/);
  assert.doesNotMatch(html, /<section id="execution-time">/);
  assert.match(html, /Semantic understanding/);
  assert.match(html, /Compliance/);
  assert.match(html, /40s/);
  assert.match(html, /catalog only/);
});

test("generated fixture reports are reproducible in a clean checkout", () => {
  const output = mkdtempSync(join(tmpdir(), "typespec-assessment-reports-"));
  try {
    const result = spawnSync(
      process.execPath,
      [
        fileURLToPath(
          new URL(
            "../test-evidence/scripts/generate-test-evidence.mjs",
            import.meta.url,
          ),
        ),
        output,
      ],
      { encoding: "utf8" },
    );
    assert.equal(result.status, 0, result.stderr);
    const cases = JSON.parse(
      readFileSync(
        new URL(
          "../test-evidence/fixtures/recent-pr-cases.json",
          import.meta.url,
        ),
        "utf8",
      ),
    );
    assert.equal(cases.length, 10);
    for (const assessmentCase of cases) {
      const directory = join(output, "assessments", String(assessmentCase.pr));
      const htmlPath = join(directory, "assessment.html");
      const jsonPath = join(directory, "assessment.json");
      assert.ok(existsSync(htmlPath));
      assert.ok(existsSync(jsonPath));
      assert.equal(existsSync(join(directory, "assessment.md")), false);
      const assessment = JSON.parse(readFileSync(jsonPath, "utf8"));
      const rendered = readFileSync(htmlPath, "utf8");
      assert.deepEqual(validateAssessment(assessment), []);
      assert.equal(rendered, renderAssessmentHtml(assessment));
      assert.match(rendered, /<!doctype html>/);
      assert.match(rendered, /TypeSpec Assessment/);
    }
    assert.equal(existsSync(join(output, "assessments.json")), false);
    assert.equal(existsSync(join(output, "assessment-summary.md")), false);
  } finally {
    rmSync(output, { recursive: true, force: true });
  }
});

test("PR 44988 compliance fixture retains both documented mismatches", () => {
  const fixture = JSON.parse(
    readFileSync(
      new URL(
        "../test-evidence/fixtures/recent-pr-compliance.json",
        import.meta.url,
      ),
      "utf8",
    ),
  )["44988"];
  assert.equal(fixture.status, "failed");
  assert.equal(fixture.findings.length, 2);
  const operationFinding = fixture.findings.find(
    (finding) =>
      finding.id ===
      "compliance-connection-analyzer-standard-resource-operations",
  );
  assert.deepEqual(
    operationFinding.sourceReferences.map(({ startLine, endLine }) => [
      startLine,
      endLine,
    ]),
    [
      [41, 64],
      [470, 552],
    ],
  );
});

test("PR 44988 classifies versioned service gateway LRO changes as downstream breaking", () => {
  const assessment = JSON.parse(
    readFileSync(
      new URL(
        "../test-evidence/fixtures/assessments/44988.json",
        import.meta.url,
      ),
      "utf8",
    ),
  );
  correctHistoricalSemanticChains(assessment);
  correctHistoricalSemanticChains(assessment);

  const findingId =
    "source-service-gateway-actions-change-from-lro-to-synchronous";
  const findings =
    assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings;
  assert.equal(findings.filter(({ id }) => id === findingId).length, 1);
  assert.equal(assessment.dimensions.restBreakingChanges.findings.length, 0);

  const intention = assessment.dimensions.semanticUnderstanding.items.find(
    ({ id }) => id === "make-service-gateway-update-actions-synchronous",
  );
  assert.ok(intention);
  assert.deepEqual(intention.changes[0].linkedFindingIds, [findingId]);
  assert.match(
    findings.find(({ id }) => id === findingId).summary,
    /synchronous methods instead of long-running poller/,
  );
});

test("PR 44200 compliance findings retain finding-specific code evidence", () => {
  const fixture = JSON.parse(
    readFileSync(
      new URL(
        "../test-evidence/fixtures/recent-pr-compliance.json",
        import.meta.url,
      ),
      "utf8",
    ),
  )["44200"];
  const compliance = buildCompliance(validDocument(), fixture);
  const codeByFinding = new Map(
    compliance.findings.map((finding) => [
      finding.id,
      finding.codeSnippets.flatMap((snippet) => snippet.lines).join("\n"),
    ]),
  );
  assert.match(
    codeByFinding.get("compliance-44200-stable-after-preview"),
    /v2025_10_01_preview[\s\S]*v2026_03_01/,
  );
  assert.match(
    codeByFinding.get("compliance-44200-private-endpoint"),
    /model PrivateEndpointConnection[\s\S]*interface PrivateEndpointConnectionsInterface/,
  );
  assert.match(
    codeByFinding.get("compliance-44200-private-link"),
    /model PrivateLinkResource[\s\S]*interface PrivateLinkResourcesInterface/,
  );
  assert.match(
    codeByFinding.get("compliance-44200-flattening"),
    /PrivateEndpointConnection\.properties[\s\S]*PrivateLinkResource\.properties/,
  );
});

test("compliance fixtures select exact source evidence", () => {
  const fixture = JSON.parse(
    readFileSync(
      new URL(
        "../test-evidence/fixtures/recent-pr-compliance.json",
        import.meta.url,
      ),
      "utf8",
    ),
  );
  for (const [pr, specification] of Object.entries(fixture)) {
    for (const [index, document] of specification.documents.entries()) {
      assert.ok(
        document.sourceReferences?.length > 0 ||
          document.semanticItemIds?.length > 0 ||
          document.sourcePathIncludes?.length > 0,
        `PR ${pr} compliance document ${index + 1} lacks an explicit source selection`,
      );
    }
    for (const [index, finding] of (specification.findings ?? []).entries()) {
      assert.ok(
        finding.sourceReferences?.length > 0 ||
          finding.semanticItemIds?.length > 0 ||
          finding.sourcePathIncludes?.length > 0,
        `PR ${pr} compliance finding ${index + 1} lacks an explicit source selection`,
      );
    }
  }
});
