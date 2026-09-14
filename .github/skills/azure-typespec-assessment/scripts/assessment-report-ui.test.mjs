import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";
import { documentQualitySummary, downstreamMethodData, downstreamPresentationDimension, renderReportSections, sdkTypeName } from "./assessment-report-ui.mjs";
import { escapeHtml, renderAssessmentHtml } from "./render-assessment-html.mjs";

function fixture() {
  const typeFact = (role) => ({
    id: `type-${role}`, projectId: "project", comparisonRole: role, sourceCommit: role,
    apiVersion: "v1", factKind: "model", identity: "Contoso.Widget",
    properties: role === "baseline" ? [{ name: "old", type: { kind: "string" }, optional: true }] : [],
  });
  const methodFact = (role, name = "updateAddressLocations") => ({
    id: `${name}-${role}`, projectId: "project", comparisonRole: role, sourceCommit: role,
    apiVersion: "v1", factKind: "method",
    crossLanguageDefinitionId: `Contoso.ServiceGateways.${name}${role === "baseline" ? "Lro" : ""}`,
    clientName: "ServiceGateways", name: `${name}${role === "baseline" ? "Lro" : ""}`,
    kind: role === "baseline" ? "lro" : "basic",
    parameters: [
      { name: "name", type: { kind: "string" } },
      { name: "afcManagedSync", optional: true, type: { kind: "boolean" } },
      { name: "parameters", type: { kind: "model", name: "Widget" } },
      { name: "contentType", type: { kind: "constant", value: "application/json" } },
    ],
    operation: {
      parameters: [
        { name: "name", kind: "path" },
        { name: "afcManagedSync", kind: "query" },
        { name: "contentType", kind: "header", serializedName: "Content-Type", type: { kind: "constant", value: "application/json" } },
      ],
      bodyParam: { name: "parameters", kind: "body" },
    },
    responseType: { kind: "model", name: "Widget" },
  });
  const before = typeFact("baseline");
  const after = typeFact("target");
  const methodBefore = methodFact("baseline");
  const methodAfter = methodFact("target");
  const listAfter = methodFact("target", "list");
  const typeFinding = {
    id: "type-change", rule: "model-property-removed",
    crossLanguageDefinitionId: "Contoso.Widget",
    expected: "Widget preserves property old.", actual: "Widget no longer exposes property old.",
    rationale: "Existing SDK callers use old.", relatedSemanticIntents: ["semantic-1"],
    rootCauseIds: ["root"], evidenceFactIds: [before.id, after.id], evidence: [before, after],
    severity: "high", sources: [{ path: "models.tsp", hunks: [{ id: "hunk", lines: ["- old?: string;"] }] }],
  };
  const directFinding = {
    id: "method-change", rule: "method-kind-changed",
    crossLanguageDefinitionId: methodBefore.crossLanguageDefinitionId,
    expected: "Existing method kind.", actual: "Changed method kind.",
    rationale: "The invocation changes.", relatedSemanticIntents: ["semantic-1"],
    evidenceFactIds: [methodBefore.id, methodAfter.id], evidence: [methodBefore, methodAfter],
    severity: "high", sources: typeFinding.sources,
  };
  const dimension = {
    status: "failed", findings: [typeFinding, directFinding],
    methodGroups: [{
      id: "direct-group", projectId: "project", symbol: methodBefore.crossLanguageDefinitionId,
      before: methodBefore, after: methodAfter, relatedSemanticIntents: ["semantic-1"],
      deltas: [
        { findingId: "method-change", field: "kind", before: "lro", after: "basic" },
      ],
    }],
    typeImpacts: [{
      id: "impact", projectId: "project", type: "Contoso.Widget", findingIds: ["type-change"],
      rootCauseIds: ["root"], relatedSemanticIntents: ["semantic-1"],
      locations: ["request-body", "response-body"],
      summary: "Recorded Widget type change.", affectedMethodCount: 3,
      affectedMethods: [
        { symbol: methodBefore.crossLanguageDefinitionId, locations: ["request-body", "response-body"] },
        { symbol: methodAfter.crossLanguageDefinitionId, locations: ["request-body", "response-body"] },
        { symbol: listAfter.crossLanguageDefinitionId, locations: ["request-body", "response-body"] },
      ],
    }],
  };
  const raw = {
    facts: Object.fromEntries([before, after, methodBefore, methodAfter, listAfter].map((fact) => [fact.id, structuredClone(fact)])),
    rootCauses: [{
      id: "root", directCandidateIds: ["type-change"], methodFactIds: [methodBefore.id, methodAfter.id, listAfter.id],
      referenceEvidence: [
        { fromFactId: methodBefore.id, toFactId: before.id, memberName: "body", location: "request-body" },
        { fromFactId: methodAfter.id, toFactId: after.id, memberName: "body", location: "request-body" },
        { fromFactId: listAfter.id, toFactId: after.id, location: "response-body" },
      ],
    }],
    candidates: [typeFinding, directFinding].map(({ id, rule, crossLanguageDefinitionId, evidenceFactIds }) => ({ id, rule, crossLanguageDefinitionId, evidenceFactIds })),
  };
  return { dimension, raw };
}

function assessment(downstream = { status: "passed", findings: [] }) {
  return {
    schemaVersion: 1, comparison: { baseCommit: "baseline", headCommit: "target" },
    confidence: "high", safety: { scope: "rest-and-downstream-only", status: downstream.status },
    dimensions: {
      semantic: { status: "assessed", sourceHunkIds: ["hunk"], items: [{
        id: "semantic-1", action: "modify", title: "Change the widget contract",
        summary: "Recorded intent, not a new judgment.", operations: [],
        relatedFindings: {
          downstream: (downstream.methodGroups ?? downstream.operationGroups ?? []).map((group) => group.id),
          typeImpact: (downstream.typeImpacts ?? downstream.sharedTypeImpacts ?? []).map((impact) => impact.id),
        },
        sources: [{ path: "models.tsp", hunks: [{ id: "hunk", lines: ["- old?: string;", "+ current?: string;"] }], declarations: [] }],
      }] },
      rest: { status: "passed", findings: [] }, downstream,
      compliance: {
        status: "not-assessed", summary: "Evidence unavailable.", findings: [], intentAssessments: [],
        coverage: { semanticIntentCount: 0, assessedIntentCount: 0, selectedDocumentCount: 0, unassessedIntentIds: [] },
        retrievalFailures: [], blockers: [{ message: "compliance-search-input-missing: test has no guidance input." }],
      },
      documentQuality: { status: "not-assessed", summary: "Document Quality and Agent Friendliness is not assessed." },
    },
    blockers: [], projects: [], changedFiles: [], provenance: {},
  };
}

test("merges direct methods and indirect types using target names and exact graph paths", () => {
  const { dimension, raw } = fixture();
  const input = structuredClone({ dimension, raw });
  const data = downstreamMethodData(dimension, raw);
  assert.equal(data.methods.length, 2);
  const method = data.methods.find((item) => item.name === "ServiceGateways.updateAddressLocations");
  assert.equal(method.cause, "mixed");
  assert.equal(method.types.length, 1);
  assert.deepEqual(method.types.flatMap((reference) => reference.paths).map((item) => [item.path, item.role, item.location]), [
    ["body", "baseline", "request-body"], ["body", "target", "request-body"],
  ]);
  const list = data.methods.find((item) => item.name === "ServiceGateways.list");
  assert.equal(list.cause, "indirect");
  assert.deepEqual(list.types[0].paths, [{ path: "(returned type)", role: "target", location: "response-body" }]);
  assert.deepEqual({ dimension, raw }, input);
});

test("does not promote root-wide locations to per-method evidence without raw graph", () => {
  const { dimension } = fixture();
  const data = downstreamMethodData(dimension);
  assert.ok(data.methods.every((method) => method.types.every((reference) => reference.paths.length === 0)));
  const html = renderAssessmentHtml(assessment(dimension));
  assert.match(html, /Aggregate root locations are not method-specific evidence/);
  assert.doesNotMatch(html, /class="report-reference-path"/);
});

test("breaking explanations separate distinct consequences without truncation or duplicates", () => {
  const { dimension } = fixture();
  const first = "The method now returns a collection; callers must iterate items instead of reading the result wrapper.";
  const second = "An optional input precedes the body; positional callers must update arguments in languages exposing that order. <unsafe>";
  const direct = dimension.findings.find((finding) => finding.id === "method-change");
  dimension.methodGroups[0].deltas = [first, second, first].map((rationale, index) => {
    const findingId = index === 0 ? direct.id : `${direct.id}-${index}`;
    if (index !== 0) dimension.findings.push({ ...direct, id: findingId, rationale });
    return { findingId, field: "kind", before: "lro", after: "basic", rationale };
  });
  const html = renderAssessmentHtml(assessment(dimension));
  assert.ok(html.includes(`<strong>Why this is breaking:</strong> <ul><li>${first}</li><li>${second.replace("<unsafe>", "&lt;unsafe&gt;")}</li></ul>`));
  assert.doesNotMatch(html, /<unsafe>/);
});

test("does not promote grouped type intent associations to each individual type", () => {
  const { dimension, raw } = fixture();
  dimension.typeImpacts[0].relatedSemanticIntents.push("semantic-for-another-type");
  const data = downstreamMethodData(dimension, raw);
  assert.deepEqual(data.types[0].semanticIds, ["semantic-1"]);
  assert.ok(data.methods.every((method) => !method.semanticIds.includes("semantic-for-another-type")));
});

test("rejects a raw graph from a mismatched evidence or root snapshot", () => {
  const { dimension, raw } = fixture();
  const wrongFact = structuredClone(raw);
  wrongFact.facts["type-target"].properties.push({ name: "invented" });
  assert.throws(() => downstreamMethodData(dimension, wrongFact), /snapshot mismatch: evidence fact/);
  const wrongRoot = structuredClone(raw);
  wrongRoot.rootCauses[0].id = "other-snapshot";
  assert.throws(() => downstreamMethodData(dimension, wrongRoot), /snapshot mismatch: missing root/);
  const wrongAssociation = structuredClone(raw);
  wrongAssociation.rootCauses[0].directCandidateIds = ["other-finding"];
  assert.throws(() => downstreamMethodData(dimension, wrongAssociation), /no matching confirmed finding/);
  const wrongCandidate = structuredClone(raw);
  wrongCandidate.candidates[0].rule = "different-rule";
  assert.throws(() => downstreamMethodData(dimension, wrongCandidate), /snapshot mismatch: candidate/);
});

test("legacy operationGroups and sharedTypeImpacts retain unmapped confirmed types", () => {
  const { dimension } = fixture();
  dimension.operationGroups = dimension.methodGroups;
  delete dimension.methodGroups;
  dimension.sharedTypeImpacts = dimension.typeImpacts.map(({ type, ...impact }) => ({ ...impact, types: [type], affectedMethods: [], affectedMethodCount: 0 }));
  delete dimension.typeImpacts;
  const data = downstreamMethodData(dimension);
  assert.equal(data.methods[0].cause, "direct");
  assert.equal(data.unmapped[0].type, "Contoso.Widget");
  assert.equal(data.unmapped[0].findings[0].id, "type-change");
  const html = renderAssessmentHtml(assessment(dimension));
  assert.match(html, /Method mapping unavailable/);
  assert.match(html, /Downstream: Widget \(SDK type\)/);
});

test("normalized method inputs exclude constants, retain locations and preserve unknown returns", () => {
  const { dimension } = fixture();
  dimension.methodGroups[0].before.parameters = dimension.methodGroups[0].before.parameters.filter((parameter) => parameter.name !== "afcManagedSync");
  dimension.methodGroups[0].before.responseType = null;
  const html = renderAssessmentHtml(assessment(dimension));
  assert.match(html, /1\. name \(path\): string/);
  assert.match(html, /2\. afcManagedSync\? \(query\): boolean/);
  assert.match(html, /3\. parameters \(body input\): Widget/);
  assert.doesNotMatch(html, /4\. contentType/);
  assert.match(html, /Constant headers \(not numbered caller inputs\)/);
  assert.match(html, /Content-Type \(header\): &quot;application\/json&quot;/);
  assert.match(html, /Return type \(body output\)/);
  assert.equal(sdkTypeName(undefined), "Not recorded");
  assert.equal(sdkTypeName(null), "void");
  const group = dimension.methodGroups[0];
  delete group.before.responseType;
  group.after.responseType = null;
  const rows = downstreamMethodData(dimension).methods.find((item) => item.group).rows;
  assert.deepEqual(rows.at(-1), { area: "SDK method", label: "Return type (body output)", before: "Not recorded", after: "void" });
});

test("comparison tables omit identical fields while retaining actual changes and evidence", () => {
  const { dimension } = fixture();
  const original = structuredClone(dimension);
  const html = renderAssessmentHtml(assessment(dimension));
  assert.doesNotMatch(html, /<strong>Normalized input order<\/strong>|<strong>Return type \(body output\)<\/strong>/);
  assert.match(html, /<strong>Normalized method name<\/strong>/);
  assert.match(html, /<strong>Method kind<\/strong>/);
  assert.match(html, /Why this is breaking/);
  for (const [, before, after] of html.matchAll(/<tr><td>[\s\S]*?<\/td><td><pre>([\s\S]*?)<\/pre><\/td><td><pre>([\s\S]*?)<\/pre><\/td><\/tr>/g)) {
    assert.notEqual(before, after);
  }
  assert.deepEqual(dimension, original);
});

test("comparison tables are omitted when all their fields are unchanged", () => {
  const { dimension } = fixture();
  const group = dimension.methodGroups[0];
  group.before.name = group.after.name;
  group.before.kind = group.after.kind;
  group.deltas[0].before = group.deltas[0].after;
  group.deltas[0].rationale = "Retained recorded explanation.";
  dimension.typeImpacts = [];
  dimension.findings = dimension.findings.filter((finding) => finding.id === "method-change");
  const html = renderAssessmentHtml(assessment(dimension));
  const downstream = html.slice(html.indexOf('<section id="downstream-breaking">'), html.indexOf('<section id="azure-compliance">'));
  assert.doesNotMatch(downstream, /class="report-table"|<tbody><\/tbody>/);
  assert.match(downstream, /Why this is breaking/);
});

test("semantic relationships are static, title-based and independent of operations", () => {
  const { dimension } = fixture();
  const html = renderAssessmentHtml(assessment(dimension));
  const semantic = html.slice(html.indexOf('<section id="semantic-intents">'), html.indexOf('<section id="appendix">'));
  const summary = semantic.match(/<details class="report-card intent"[^>]*>(<summary>[\s\S]*?<\/summary>)/)[1];
  assert.match(summary, /Impacts \(2\)/);
  assert.match(summary, /Downstream: ServiceGateways\.updateAddressLocations/);
  assert.doesNotMatch(summary, /Affected operations|SDK:|<button|>semantic-1</);
  assert.ok(semantic.indexOf("Changed TypeSpec source") < semantic.indexOf("Affected operations"));
  assert.match(semantic, /<details class="report-source" open>/);
  assert.doesNotMatch(semantic, /<details class="report-subdetails"[^>]* open/);
  const methods = html.slice(html.indexOf('<section id="downstream-breaking">'), html.indexOf('<section id="azure-compliance">'));
  const header = methods.match(/<summary>[\s\S]*?<\/summary>/)[0];
  assert.match(header, />Change the widget contract<\/a>/);
});

test("fragment handlers reveal nested details without toggling relation labels", () => {
  const html = renderAssessmentHtml(assessment());
  const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
  const listeners = {};
  const outer = { tagName: "DETAILS", open: false, parentElement: null };
  const inner = { tagName: "DETAILS", open: false, parentElement: outer };
  let scrolls = 0;
  const target = { tagName: "SPAN", parentElement: inner, scrollIntoView: () => scrolls++ };
  class Element {
    constructor(kind) { this.kind = kind; }
    closest(selector) {
      if (selector.includes("report-intent-relations")) return this.kind !== "title" ? this : null;
      if (selector === "a" || selector === 'a[href^="#"]') return this.kind === "link" ? this : null;
      return null;
    }
    getAttribute() { return "#target"; }
  }
  const context = {
    Element, location: { hash: "#target" },
    document: { getElementById: (id) => id === "target" ? target : null, addEventListener: (event, callback) => { listeners[event] = callback; } },
    window: { addEventListener: (event, callback) => { listeners[event] = callback; }, setTimeout: (callback) => callback() },
  };
  vm.runInNewContext(script, context);
  assert.equal(outer.open, true);
  assert.equal(inner.open, true);
  outer.open = inner.open = false;
  let prevented = false;
  listeners.click({ target: new Element("label"), preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true);
  assert.equal(inner.open, false);
  prevented = false;
  listeners.click({ target: new Element("title"), preventDefault: () => { prevented = true; } });
  assert.equal(prevented, false);
  listeners.click({ target: new Element("link"), preventDefault: () => {} });
  assert.equal(inner.open, true);
  assert.equal(scrolls, 2);
});

test("retains every finding anchor and escapes intent and source text", () => {
  const { dimension, raw } = fixture();
  const input = assessment(dimension);
  input.dimensions.semantic.items[0].title = '<img src=x onerror="alert(1)">';
  input.dimensions.semantic.items[0].sources[0].hunks[0].lines = ['+ "</script><script>alert(1)</script>"'];
  const html = renderAssessmentHtml(input, { downstreamInput: raw });
  assert.doesNotMatch(html, /<img src=x|<script>alert/);
  assert.match(html, /&lt;img src=x onerror=&quot;alert\(1\)&quot;&gt;/);
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map((match) => match[1]);
  assert.equal(ids.length, new Set(ids).size);
  for (const finding of dimension.findings) assert.ok(ids.includes(`finding-${finding.id}`));
  for (const [, id] of html.matchAll(/href="#([^"]+)"/g)) assert.ok(ids.includes(id), `Missing anchor ${id}`);
});

test("guideline cards retain every recorded diff once with distinct expected and actual sections", () => {
  const input = JSON.parse(readFileSync(new URL("../evals/assessments/44742/assessment.json", import.meta.url), "utf8"));
  const first = input.dimensions.compliance.findings[0];
  first.actual = first.codeSnippets[0].lines.join("\n");
  const intent = input.dimensions.compliance.intentAssessments.find((item) => item.semanticIntentId === first.semanticIntentId);
  intent.actual = first.actual;
  const html = renderAssessmentHtml(input);
  const guidelines = html.slice(html.indexOf('<section id="azure-compliance">'), html.indexOf('<section id="document-quality">'));
  assert.equal((guidelines.match(/<h3>Expected<\/h3>/g) ?? []).length, 1);
  assert.equal((guidelines.match(/<h3>Actual<\/h3>/g) ?? []).length, 1);
  assert.equal((guidelines.match(/class="diff"/g) ?? []).length, input.dimensions.compliance.findings.reduce((count, finding) => count + new Set(finding.codeSnippets.map((snippet) => JSON.stringify(snippet))).size, 0));
  assert.doesNotMatch(guidelines, /<table|comparison-details|class="severity|>high<|>medium<|>low</);
  assert.match(guidelines, /class="remove"/);
  const firstBody = guidelines.slice(guidelines.indexOf(`id="compliance-finding-${first.id}"`), guidelines.indexOf(`id="compliance-finding-${input.dimensions.compliance.findings[1].id}"`));
  assert.doesNotMatch(firstBody, /<p>-/);
  assert.equal((firstBody.match(/class="diff"/g) ?? []).length, first.codeSnippets.length);
  const semantic = html.slice(html.indexOf('<section id="semantic-intents">'), html.indexOf('<section id="appendix">'));
  assert.match(semantic, /class="report-link" href="#compliance-finding-[^"]+">Azure Guidelines/);
  assert.ok(html.includes('.report-link.impact,.report-link[href^="#compliance-finding-"]{color:#b91c1c;border-color:#fecaca;background:#fff1f2}'));
  assert.ok(html.includes('.report-link.impact,.report-link[href^="#compliance-finding-"]{color:#fecaca;border-color:#7f1d1d;background:#431f29}'));
  for (const [, header] of semantic.matchAll(/<details class="report-card intent"[^>]*>(<summary>[\s\S]*?<\/summary>)/g)) {
    const impacts = Number(header.match(/Impacts \((\d+)\)/)?.[1] ?? 0);
    assert.equal(impacts, (header.match(/class="report-link impact"/g) ?? []).length);
    assert.doesNotMatch(header, /class="report-link impact"[^>]*>Azure Guidelines/);
  }
});

test("five dimension headers and empty not-assessed states use the shared layout", () => {
  const input = assessment();
  input.dimensions.rest.status = "not-assessed";
  input.dimensions.rest.blockers = ["REST evidence unavailable."];
  input.dimensions.downstream.status = "not-assessed";
  input.dimensions.downstream.blockers = ["Downstream evidence unavailable."];
  input.safety.status = "not-assessed";
  const html = renderAssessmentHtml(input);
  assert.equal((html.match(/class="report-section-head"/g) ?? []).length, 5);
  assert.match(html, /REST breaking changes were not fully assessed/);
  assert.match(html, /Downstream breaking changes were not fully assessed/);
  assert.match(html, /Azure Guidelines could not be fully assessed/);
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  assert.match(quality, /not assessed/);
  assert.doesNotMatch(quality, /passed|0 findings/);
  assert.match(html, /@media\(max-width:760px\)/);
  assert.match(html, /@media\(prefers-color-scheme:dark\)/);
  assert.match(html, /grid-template-columns:minmax\(0,1fr\) max-content/);
});

test("legacy type findings remain visible when method mappings are unavailable", () => {
  const { dimension } = fixture();
  dimension.findings = dimension.findings.filter((finding) => finding.id === "type-change");
  dimension.methodGroups = [];
  dimension.typeImpacts[0].affectedMethods = [];
  dimension.typeImpacts[0].affectedMethodCount = 0;
  const snapshot = structuredClone(dimension);
  const data = downstreamMethodData(dimension);
  assert.equal(data.methods.length, 0);
  assert.equal(data.unmapped.length, 1);
  assert.deepEqual(data.unmapped.flatMap((type) => type.findings.map((finding) => finding.id)), ["type-change"]);
  assert.ok(data.unmapped.every((type) => type.mapped.length === 0));
  const html = renderAssessmentHtml(assessment(dimension));
  assert.match(html, /Method mapping unavailable/);
  assert.match(html, /id="finding-type-change"/);
  assert.deepEqual(dimension, snapshot);
});

test("explicit provenance sidecar bridges only root IDs and never changes authoritative data", () => {
  const { dimension, raw } = fixture();
  const input = assessment(dimension);
  input.repository = { remoteUrl: "https://github.com/Azure/azure-rest-api-specs" };
  const replay = structuredClone(input);
  input.dimensions.downstream.findings[0].rootCauseIds = ["unresolved-root"];
  input.dimensions.downstream.typeImpacts[0].rootCauseIds = ["unresolved-root"];
  const snapshots = structuredClone({ input, replay, raw });
  assert.throws(() => downstreamMethodData(input.dimensions.downstream, raw), /missing root unresolved-root/);
  const presentation = downstreamPresentationDimension(input, { downstreamInput: raw, downstreamAssessment: replay });
  const data = downstreamMethodData(presentation, raw);
  assert.equal(data.methods.length, 2);
  assert.equal(data.unmapped.length, 0);
  assert.deepEqual(presentation.findings[0].rootCauseIds, ["root"]);
  const html = renderAssessmentHtml(input, { downstreamInput: raw, downstreamAssessment: replay });
  assert.match(html, /Representative method paths from recorded graph edges/);
  assert.deepEqual({ input, replay, raw }, snapshots);
  const wrongJudgment = structuredClone(replay);
  wrongJudgment.dimensions.downstream.findings[0].rationale = "Different judgment";
  assert.throws(() => downstreamPresentationDimension(input, { downstreamInput: raw, downstreamAssessment: wrongJudgment }), /provenance mismatch: finding/);
  const wrongCommit = structuredClone(replay);
  wrongCommit.comparison.headCommit = "different";
  assert.throws(() => downstreamPresentationDimension(input, { downstreamInput: raw, downstreamAssessment: wrongCommit }), /repository or comparison/);
  const missingFinding = structuredClone(replay);
  missingFinding.dimensions.downstream.findings.pop();
  assert.throws(() => downstreamPresentationDimension(input, { downstreamInput: raw, downstreamAssessment: missingFinding }), /finding coverage/);
  assert.throws(() => downstreamPresentationDimension(input, { downstreamAssessment: replay }), /requires downstreamInput/);
});

function documentDimension(decision = "fail") {
  const document = {
    id: "doc-widget", sourceChangeId: "source-widget", qualifiedName: "Contoso.Widget.count", kind: "property",
    before: { doc: "The number of widgets.", declaration: '@doc("The number of widgets.")\ncount: int32;', source: { path: "models.tsp", revision: "base", startLine: 4, endLine: 5 } },
    after: { doc: "The number of widgets.", declaration: '@doc("The number of widgets.")\n@minValue(1)\ncount: int32;', source: { path: "models.tsp", revision: "current", startLine: 4, endLine: 6 } },
  };
  const check = {
    reviewUnitId: "semantic-1", documentId: document.id, check: "meaning", decision,
    title: "Widget count documentation omits the positive-only constraint",
    expected: "Describe the count as a positive integer.", rationale: "The declaration now excludes zero; the doc does not explain this constraint.", docQuote: "The number of widgets.",
  };
  const status = decision === "pass" ? "passed" : decision === "fail" ? "failed" : "not-assessed";
  return {
    status, summary: "Recorded @doc meaning assessment.",
    coverage: { semanticIntentCount: 1, assessedIntentCount: decision === "not-assessed" ? 0 : 1, documentCount: 1, assessedDocumentCount: decision === "not-assessed" ? 0 : 1, checkCount: 1, assessedCheckCount: decision === "not-assessed" ? 0 : 1, unassessedIntentIds: decision === "not-assessed" ? ["semantic-1"] : [], notApplicableIntentIds: [] },
    intentAssessments: [{ reviewUnitId: "semantic-1", status, documents: [document], checks: [check] }],
    findings: decision === "fail" ? [{ ...check, id: "document-finding-widget", actual: document.after.doc, document, semanticIntentIds: ["semantic-1"], sources: [] }] : [],
    blockers: [],
  };
}

function presentationWithDocuments(dimension, downstream, sources) {
  const input = assessment(downstream);
  input.dimensions.documentQuality = dimension;
  if (sources) input.dimensions.semantic.items[0].sources = sources;
  return renderReportSections(input, {
    escapeHtml, operationContractRows: () => [], complianceFindingGroups: () => [],
    renderSourceHunks: () => "", sourceLinks: () => "", directLegacyDownstreamFindings: () => [],
  }).html;
}

function referencedDocumentationFixture() {
  const dimension = documentDimension();
  dimension.assessmentVersion = 2;
  const unit = dimension.intentAssessments[0];
  const document = unit.documents[0];
  Object.assign(document, {
    qualifiedName: "Contoso.Response.body", before: null,
    after: {
      doc: "Empty response body.",
      declaration: '/** Empty response body. */\n@body\nbody: ResponseBody;',
      source: { path: "models.tsp", revision: "current", startLine: 69, endLine: 71 },
    },
  });
  const related = {
    id: "doc-response-body", sourceChangeId: document.sourceChangeId,
    qualifiedName: "Contoso.ResponseBody", kind: "model", before: null,
    after: {
      doc: "Empty success response.",
      declaration: '@doc("Empty success response.")\nmodel ResponseBody {\n  /** Operation status. */\n  @visibility(Lifecycle.Read)\n  status?: string;\n}',
      source: { path: "models.tsp", revision: "current", startLine: 56, endLine: 61 },
    },
  };
  unit.documents.push(related);
  unit.checks[0].check = dimension.findings[0].check = "description";
  unit.checks.push({ documentId: related.id, check: "description", decision: "pass", rationale: "Not displayed." });
  const sources = [{
    id: document.sourceChangeId, path: "models.tsp",
    declarations: [document, related].map((item) => ({
      kind: item.kind, qualifiedName: item.qualifiedName.replace("Contoso.", ""),
      source: { ...item.after.source },
      compilerEvidence: { kind: "semantic-type", referencedNames: item === document ? ["ResponseBody", "ResponseBody.status", "string"] : ["ResponseBody.status", "string"] },
    })),
  }];
  return { dimension, sources, document, related };
}

test("new documentation failures show current code full-width with referenced model evidence", () => {
  const { dimension, sources, document, related } = referencedDocumentationFixture();
  const original = structuredClone({ dimension, sources });
  const html = presentationWithDocuments(dimension, undefined, sources);
  const card = html.slice(html.indexOf('id="document-quality-document-finding-widget"'), html.indexOf('<details class="report-subdetails document-quality-file">'));
  assert.match(card, /class="report-document-snapshots single-snapshot"/);
  assert.match(card, /<h4>Current declaration<\/h4>/);
  assert.doesNotMatch(card, /No before snapshot|<h4>Before<\/h4>/);
  assert.match(card, /Related type definitions/);
  assert.ok(card.includes(escapeHtml(document.after.declaration)));
  assert.ok(card.includes(escapeHtml(related.after.declaration)));
  assert.match(card, /status\?: string;/);
  assert.match(card, /models.tsp:56-61 \(current\)/);
  assert.equal((card.match(/model ResponseBody/g) ?? []).length, 1);
  assert.deepEqual({ dimension, sources }, original);
});

test("documentation type context never substitutes current source into a baseline snapshot", () => {
  const { dimension, sources, document } = referencedDocumentationFixture();
  document.before = { ...structuredClone(document.after), source: { ...document.after.source, revision: "base" } };
  const html = presentationWithDocuments(dimension, undefined, sources);
  const card = html.slice(html.indexOf('id="document-quality-document-finding-widget"'), html.indexOf('<details class="report-subdetails document-quality-file">'));
  assert.doesNotMatch(card, /single-snapshot/);
  assert.doesNotMatch(card.slice(0, card.indexOf("<h4>After</h4>")), /Related type definitions|model ResponseBody/);
  assert.match(card.slice(card.indexOf("<h4>After</h4>")), /Related type definitions/);
});

test("documentation context requires unambiguous compiler references and exact source ownership", () => {
  for (const invalidate of [
    ({ sources }) => { sources[0].declarations[0].compilerEvidence.referencedNames = []; },
    ({ sources }) => { sources[0].declarations[1].source.revision = "base"; },
    ({ sources }) => { sources[0].declarations[1].source.startLine = 1; },
    ({ related }) => { related.after.source.path = "different.tsp"; },
    ({ dimension, sources, related }) => {
      const duplicate = structuredClone(related);
      duplicate.id = "ambiguous-doc";
      duplicate.qualifiedName = "Other.ResponseBody";
      duplicate.sourceChangeId = "other-source";
      duplicate.after.source.path = "other.tsp";
      dimension.intentAssessments[0].documents.push(duplicate);
      sources.push({
        id: "other-source", path: "other.tsp",
        declarations: [{ ...sources[0].declarations[1], source: { ...duplicate.after.source } }],
      });
    },
  ]) {
    const fixture = referencedDocumentationFixture();
    invalidate(fixture);
    const html = presentationWithDocuments(fixture.dimension, undefined, fixture.sources);
    assert.doesNotMatch(html, /Related type definitions/);
  }
});

test("document issues use collapsed Expected/Actual cards and readable bidirectional intent links", () => {
  const dimension = documentDimension();
  const original = structuredClone(dimension);
  const html = presentationWithDocuments(dimension);
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  const header = quality.match(/<details class="report-card document-quality-check"[^>]*>(<summary>[\s\S]*?<\/summary>)/)[1];
  assert.match(header, /Widget count documentation omits the positive-only constraint/);
  assert.match(header, /Affected intents \(1\)/);
  assert.match(header, /href="#intent-semantic-1">Change the widget contract/);
  assert.doesNotMatch(header, /<pre>|Expected|<table|>semantic-1</);
  assert.match(quality, /<h3>Expected<\/h3><p>Describe the count as a positive integer\./);
  assert.match(quality, /<h3>Actual<\/h3>/);
  for (const side of ["before", "after"]) {
    const value = dimension.findings[0].document[side];
    assert.ok(quality.includes(`<code>${escapeHtml(value.declaration)}</code>`));
    assert.ok(quality.includes(`models.tsp:4-${value.source.endLine} (${value.source.revision})`));
  }
  assert.match(quality, /Assessment rationale/);
  assert.equal((quality.match(/<pre><code>/g) ?? []).length, 2);
  assert.doesNotMatch(quality, /Recorded @doc text/);
  assert.doesNotMatch(quality, /<table|class="severity|>high<|>medium<|>low<|document-quality-check"[^>]* open/);
  const semantic = html.slice(html.indexOf('<section id="semantic-intents">'));
  assert.match(semantic, /aria-label="Document quality findings"/);
  assert.match(semantic, /Document Quality: Widget count documentation/);
  assert.doesNotMatch(semantic, /Impacts \(|class="report-link impact"/);
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map((match) => match[1]);
  assert.equal(ids.length, new Set(ids).size);
  for (const [, id] of html.matchAll(/href="#([^"]+)"/g)) assert.ok(ids.includes(id), `Missing document relation ${id}`);
  assert.deepEqual(dimension, original);
});

test("document coverage distinguishes passed checks, no applicable docs, and legacy not-assessed", () => {
  const passed = documentDimension("pass");
  passed.intentAssessments[0].checks[0].title = "Widget count meaning is accurate";
  const passedHtml = presentationWithDocuments(passed);
  assert.match(passedHtml, /1\/1 checks assessed/);
  assert.match(passedHtml, /1\/1 @doc documents assessed/);
  assert.match(passedHtml, /report-badge add">passed/);
  assert.equal(documentQualitySummary(passed).label, "Passed");
  const noDocs = {
    status: "passed", summary: "No applicable @doc on this changed declaration.",
    coverage: { semanticIntentCount: 1, assessedIntentCount: 1, documentCount: 0, assessedDocumentCount: 0, checkCount: 0, assessedCheckCount: 0, unassessedIntentIds: [], notApplicableIntentIds: ["semantic-1"] },
    intentAssessments: [{ reviewUnitId: "semantic-1", status: "not-applicable", reason: "No @doc is attached to the changed declaration.", documents: [], checks: [] }], findings: [], blockers: [],
  };
  const noDocsHtml = presentationWithDocuments(noDocs);
  assert.match(noDocsHtml, /0\/0 checks assessed/);
  assert.match(noDocsHtml, /1 intents with no applicable @doc/);
  assert.match(noDocsHtml, /not applicable/);
  assert.match(noDocsHtml, /No @doc is attached to the changed declaration/);
  assert.equal(documentQualitySummary(noDocs).label, "No applicable documentation");
  const noDocsQuality = noDocsHtml.slice(noDocsHtml.indexOf('<section id="document-quality">'), noDocsHtml.indexOf('<section id="semantic-intents">'));
  assert.doesNotMatch(noDocsQuality, /report-badge add|>passed</);
  assert.match(noDocsQuality, /No applicable documentation/);
  assert.doesNotMatch(noDocsHtml, /Document Quality and Agent Friendliness is not assessed/);
  const legacy = presentationWithDocuments({ status: "not-assessed", summary: "Historical documentation evidence unavailable." });
  const legacyQuality = legacy.slice(legacy.indexOf('<section id="document-quality">'), legacy.indexOf('<section id="semantic-intents">'));
  assert.match(legacyQuality, /Historical documentation evidence unavailable/);
  assert.doesNotMatch(legacyQuality, /0 findings|checks assessed|passed/);
  assert.equal(documentQualitySummary().label, "Not assessed");
});

test("41 passing descriptions stay inside collapsed intent and bounded file groups", () => {
  const dimension = documentDimension("pass");
  dimension.assessmentVersion = 3;
  const intent = dimension.intentAssessments[0];
  const document = intent.documents[0];
  const check = intent.checks[0];
  intent.documents = Array.from({ length: 41 }, (_, index) => ({
    ...document, id: `document-${index}`, qualifiedName: `Contoso.Model${index}`, kind: "model",
  }));
  intent.checks = intent.documents.map((item) => ({
    ...check, documentId: item.id, check: "description", rationale: "Individual passing rationale.",
  }));
  Object.assign(dimension.coverage, {
    documentCount: 41, assessedDocumentCount: 41, checkCount: 41, assessedCheckCount: 41,
  });
  const original = structuredClone(dimension);
  const html = presentationWithDocuments(dimension);
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  assert.equal((quality.match(/class="report-card document-quality-intent"/g) ?? []).length, 1);
  assert.equal((quality.match(/class="report-subdetails document-quality-file"/g) ?? []).length, 1);
  assert.equal((quality.match(/class="document-quality-document"/g) ?? []).length, 41);
  assert.match(quality, /<div class="document-quality-scope"><details class="report-card document-quality-passed-group" id="document-quality-passed-intents">/);
  assert.match(quality, /Passed intents \(1\)/);
  assert.doesNotMatch(quality, /Coverage by change intent|41 descriptions passed/);
  assert.match(quality, /class="report-badge add">41 passed/);
  assert.match(quality, /41 local descriptions \/ 1 file/);
  assert.match(quality, /class="document-quality-list" role="region" aria-label="Descriptions in models.tsp" tabindex="0"/);
  assert.doesNotMatch(quality, /<details[^>]*\bopen|Check results:|Individual passing rationale|document-quality-check|No recorded documentation checks/);
  assert.match(quality, /41\/41 descriptions assessed/);
  assert.match(quality, /title="Contoso.Model0">Model0<\/span>/);
  assert.match(quality, /Current local description|View TypeSpec and full source path/);
  for (const item of intent.documents) assert.ok(quality.includes(item.qualifiedName));
  assert.deepEqual(dimension, original);
});

test("mixed documentation hides passes but retains failures and unresolved reasons", () => {
  const dimension = documentDimension();
  const intent = dimension.intentAssessments[0];
  const passed = { ...intent.checks[0], check: "correctness", decision: "pass", rationale: "Hidden passing rationale." };
  const pending = { ...intent.checks[0], documentId: "document-pending", decision: "not-assessed", rationale: "Distinct missing contract." };
  intent.checks.push(passed, pending);
  intent.documents.push({ ...intent.documents[0], id: pending.documentId, qualifiedName: "Contoso.Pending" });
  const html = presentationWithDocuments(dimension);
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  assert.equal((quality.match(/class="report-card document-quality-check"/g) ?? []).length, 1);
  assert.doesNotMatch(quality, /Hidden passing rationale|document-quality-check-summary/);
  assert.match(quality, /1 check passed \/ 1 failed \/ 1 not assessed/);
  assert.match(quality, /<h3>Expected|<h3>Actual|Distinct missing contract/);
  assert.match(quality, /class="report-card document-quality-intent" open/);
  assert.doesNotMatch(quality, /class="report-card document-quality-check"[^>]* open/);
});

test("intent groups distinguish same-named files and keep full paths out of declaration summaries", () => {
  const dimension = documentDimension("pass");
  dimension.assessmentVersion = 3;
  const intent = dimension.intentAssessments[0];
  intent.checks[0].check = "description";
  intent.documents[0].after.source.path = "specification/service/Alpha/main.tsp";
  const second = structuredClone(intent.documents[0]);
  second.id = "doc-other";
  second.after.source.path = "specification/service/Beta/main.tsp";
  intent.documents.push(second);
  intent.checks.push({ ...intent.checks[0], documentId: second.id });
  Object.assign(dimension.coverage, { documentCount: 2, assessedDocumentCount: 2, checkCount: 2, assessedCheckCount: 2 });
  const original = structuredClone(dimension);
  const html = presentationWithDocuments(dimension);
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  assert.match(quality, /2 local descriptions \/ 2 files/);
  assert.match(quality, /<summary title="specification\/service\/Alpha\/main.tsp">Alpha\/main.tsp /);
  assert.match(quality, /<summary title="specification\/service\/Beta\/main.tsp">Beta\/main.tsp /);
  const summaries = [...quality.matchAll(/<details class="document-quality-document">(<summary>[\s\S]*?<\/summary>)/g)];
  assert.equal(summaries.length, 2);
  for (const [, text] of summaries) {
    assert.match(text, /title="Contoso.Widget.count">Widget.count/);
    assert.doesNotMatch(text, /specification\/|<pre|rationale/);
  }
  assert.deepEqual(dimension, original);
});

test("failure cards without an intent assessment remain visible with their stable anchors", () => {
  const dimension = documentDimension();
  dimension.intentAssessments = [];
  const html = presentationWithDocuments(dimension);
  assert.match(html, /id="document-quality-document-finding-widget"/);
  assert.match(html, /<h3>Expected<\/h3>/);
  assert.match(html, /Widget count documentation omits/);
});

test("mixed local and inherited coverage does not count inherited descriptions as passed", () => {
  const dimension = documentDimension("pass");
  dimension.assessmentVersion = 3;
  dimension.coverage.inheritedDocumentCount = 2;
  dimension.intentAssessments[0].inheritedDocumentIds = ["inherited-one", "inherited-two"];
  const html = presentationWithDocuments(dimension);
  assert.match(html, /1 local description \/ 1 file \/ 2 inherited descriptions not reviewed/);
  assert.match(html, /class="report-badge add">1 passed/);
  assert.doesNotMatch(html, /3 passed|3 local descriptions/);
});

test("a blocked intent reason is shown once while distinct blockers remain visible", () => {
  const dimension = documentDimension("not-assessed");
  const intent = dimension.intentAssessments[0];
  intent.reason = "Unique unresolved declaration.";
  dimension.blockers = [
    { reviewUnitId: intent.reviewUnitId, reason: intent.reason },
    { reason: "Separate source-read failure." },
  ];
  const html = presentationWithDocuments(dimension);
  assert.equal(html.split(intent.reason).length - 1, 1);
  assert.match(html, /Separate source-read failure/);
});

test("documentation status groups collapse passes and blockers without hiding mixed failures", () => {
  const dimension = documentDimension("fail");
  const passed = documentDimension("pass").intentAssessments[0];
  passed.reviewUnitId = "semantic-pass";
  const pending = documentDimension("not-assessed").intentAssessments[0];
  pending.reviewUnitId = "semantic-pending";
  pending.reason = "Missing <compiler> context.";
  dimension.intentAssessments.unshift(passed, pending);
  dimension.intentAssessments.push({
    reviewUnitId: "semantic-blocked", status: "not-assessed",
    reason: "Unresolved documentation ownership.", documents: [], checks: [],
  });
  const original = structuredClone(dimension);
  const html = presentationWithDocuments(dimension);
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  assert.match(quality, /<div class="document-quality-scope"><details class="report-card document-quality-intent" open>/);
  assert.match(quality, /<details class="report-card document-quality-not-assessed-group" id="document-quality-not-assessed-intents">/);
  assert.match(quality, /<details class="report-card document-quality-passed-group" id="document-quality-passed-intents">/);
  assert.match(quality, /Not assessed intents \(2\)/);
  assert.match(quality, /Passed intents \(1\)/);
  const blockedGroup = quality.slice(quality.indexOf('id="document-quality-not-assessed-intents"'), quality.indexOf('id="document-quality-passed-intents"'));
  assert.match(blockedGroup, /Assessment blocker \/ rationale:.*Missing &lt;compiler&gt; context/);
  assert.match(blockedGroup, /Unresolved documentation ownership/);
  assert.match(blockedGroup, /Not assessed reason:/);
  assert.doesNotMatch(blockedGroup, /document-quality-check"/);
  assert.ok(quality.indexOf('id="document-quality-document-finding-widget"') < quality.indexOf('id="document-quality-not-assessed-intents"'));
  assert.deepEqual(dimension, original);
});

test("passing description counts distinguish blocked and non-applicable intent scopes", () => {
  const dimension = documentDimension("pass");
  dimension.assessmentVersion = 3;
  dimension.status = "not-assessed";
  dimension.intentAssessments.push(
    { reviewUnitId: "semantic-blocked", status: "not-assessed", reason: "Unresolved documentation ownership.", documents: [], checks: [] },
    { reviewUnitId: "semantic-empty", status: "not-applicable", reason: "No local description.", documents: [], checks: [] },
  );
  Object.assign(dimension.coverage, {
    semanticIntentCount: 3, assessedIntentCount: 2,
    unassessedIntentIds: ["semantic-blocked"], notApplicableIntentIds: ["semantic-empty"],
  });
  const html = presentationWithDocuments(dimension);
  assert.match(html, /2\/3 intent scopes resolved/);
  assert.match(html, /class="report-badge add">1 passed/);
  assert.match(html, /class="report-badge unknown">Not assessed/);
  assert.match(html, /class="report-badge neutral">No applicable documentation/);
  assert.doesNotMatch(html, /0 not assessed|0 descriptions not assessed/);
  assert.match(html, /Unresolved documentation ownership|No local description/);
});

test("inherited-only documentation is present but not reviewed, never missing or passed", () => {
  const dimension = {
    assessmentVersion: 3, status: "not-applicable",
    summary: "Inherited documentation is present; its quality is not reviewed in v1.",
    coverage: {
      semanticIntentCount: 1, assessedIntentCount: 1, documentCount: 0,
      assessedDocumentCount: 0, checkCount: 0, assessedCheckCount: 0,
      inheritedDocumentCount: 1, unassessedIntentIds: [], notApplicableIntentIds: ["semantic-1"],
    },
    intentAssessments: [{
      reviewUnitId: "semantic-1", status: "not-applicable", reason: "Inherited description not reviewed.",
      documents: [], checks: [], inheritedDocumentIds: ["document-inherited"],
    }],
    findings: [], blockers: [],
  };
  assert.equal(documentQualitySummary(dimension).label, "Inherited documentation not reviewed");
  const html = presentationWithDocuments(dimension);
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  assert.match(quality, /1 inherited descriptions not reviewed|inherited not reviewed/);
  assert.doesNotMatch(quality, /No applicable documentation|no applicable @doc|>passed<|Assessment blocked|document-quality-check/);
});

test("v3 inherited descriptions are labeled and shown separately despite tag-only declaration comments", () => {
  const dimension = documentDimension();
  dimension.assessmentVersion = 3;
  const intent = dimension.intentAssessments[0];
  intent.checks[0].check = dimension.findings[0].check = "description";
  for (const side of ["before", "after"]) {
    Object.assign(intent.documents[0][side], {
      documentationOrigin: "inherited",
      doc: "Gets the <Widget> resource.",
      declaration: "/** @param id Resource identifier. */\nop get is Read<Widget>;",
    });
  }
  const html = presentationWithDocuments(dimension);
  assert.match(html, /title="Contoso.Widget.count">Widget.count<\/span> <span class="report-small">Inherited description/);
  assert.match(html, /1\/1 descriptions assessed/);
  assert.doesNotMatch(html, /Legacy Correctness|Meaning \(legacy\)/);
  assert.equal((html.match(/Compiler-resolved inherited @doc text/g) ?? []).length, 2);
  assert.equal((html.match(/Exact associated declaration source/g) ?? []).length, 2);
  assert.equal((html.match(/<code>Gets the &lt;Widget&gt; resource\.<\/code>/g) ?? []).length, 2);
  assert.ok(html.includes(escapeHtml(intent.documents[0].after.declaration)));
  const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
  assert.match(quality, /class="report-card document-quality-intent" open/);
  assert.doesNotMatch(quality, /class="report-card document-quality-check"[^>]*\bopen/);
});

test("incomplete doc evidence renders recorded checks and blocked reasons without fabricating source", () => {
  const dimension = documentDimension("not-assessed");
  dimension.intentAssessments[0].documents[0].before = null;
  dimension.intentAssessments[0].reason = "Baseline declaration evidence unavailable.";
  dimension.intentAssessments[0].checks[0].rationale = "Meaning cannot be confirmed without the old contract.";
  delete dimension.intentAssessments[0].checks[0].expected;
  dimension.blockers = [{ reason: "Baseline @doc could not be read." }];
  const html = presentationWithDocuments(dimension);
  assert.match(html, /0\/1 checks assessed/);
  assert.doesNotMatch(html, /report-document-snapshot/);
  assert.match(html, /Baseline declaration evidence unavailable/);
  assert.match(html, /Meaning cannot be confirmed without the old contract/);
  assert.doesNotMatch(html, /Expected meaning or contract was not recorded|<h3>Expected<\/h3>/);
  assert.match(html, /Assessment blocked/);
  assert.match(html, /Baseline @doc could not be read/);
  assert.match(html, /Examples, external documentation, and agent execution are not assessed/);
  assert.equal(documentQualitySummary(dimension).label, "Not assessed");
});

test("every recorded doc, declaration, issue, rationale and source label is HTML escaped", () => {
  const dimension = documentDimension();
  const attack = '</code><img src=x onerror="alert(1)">&\'';
  const finding = dimension.findings[0];
  finding.title = finding.expected = finding.rationale = finding.actual = attack;
  finding.document.qualifiedName = attack;
  finding.document.after.doc = finding.document.after.declaration = attack;
  finding.document.after.source.path = attack;
  dimension.summary = attack;
  dimension.blockers = [{ message: attack }];
  const html = presentationWithDocuments(dimension);
  assert.doesNotMatch(html, /<img src=x|onerror="alert/);
  assert.ok(html.includes(escapeHtml(attack)));
  assert.ok(html.includes(`<code>${escapeHtml(attack)}</code>`));
  assert.ok(html.includes(`<strong>${escapeHtml(attack)}</strong>`));
});

test("document links do not change REST and downstream impact counts", () => {
  const { dimension } = fixture();
  const html = presentationWithDocuments(documentDimension(), dimension);
  const semantic = html.slice(html.indexOf('<section id="semantic-intents">'));
  const header = semantic.match(/<details class="report-card intent"[^>]*>(<summary>[\s\S]*?<\/summary>)/)[1];
  assert.match(header, /Impacts \(2\)/);
  assert.equal((header.match(/class="report-link impact"/g) ?? []).length, 2);
  assert.match(header, /aria-label="Document quality findings"/);
  assert.doesNotMatch(header, /class="report-link impact"[^>]*>Document Quality/);
});

test("failed doc findings retain incomplete-check coverage and blocked intent reasons", () => {
  const dimension = documentDimension();
  const intent = dimension.intentAssessments[0];
  intent.reason = "The meaning review is blocked by missing evidence.";
  intent.checks.push({ reviewUnitId: intent.reviewUnitId, documentId: intent.documents[0].id, check: "correctness", decision: "not-assessed", rationale: "The full contract is unavailable." });
  Object.assign(dimension.coverage, { checkCount: 2, assessedCheckCount: 1, assessedIntentCount: 0, assessedDocumentCount: 0, unassessedIntentIds: [intent.reviewUnitId] });
  const html = presentationWithDocuments(dimension);
  assert.equal(documentQualitySummary(dimension).label, "Failed");
  assert.match(html, /1\/2 checks assessed/);
  assert.match(html, /0\/1 intent scopes resolved/);
  assert.match(html, /meaning review is blocked by missing evidence/);
  assert.match(html, /Documentation not assessed for:/);
  assert.match(html, /The full contract is unavailable/);
});

test("document snapshots show exact declaration source once when leading decorators already include @doc", () => {
  for (const declaration of [
    '@doc("The number of widgets.")\ncount: int32;',
    '/** The number of widgets. */\ncount: int32;',
    '/* source context */\n@extension("literal @doc(\\"unrelated\\")")\n@TypeSpec . doc("""\n  The number of widgets.\n  """)\ncount: int32;',
    '@extension(fn("nested"), { value: ")" })\n// context\n@doc ("The number of widgets.")\ncount: int32;',
  ]) {
    const dimension = documentDimension();
    dimension.findings[0].document.after.declaration = declaration;
    const html = presentationWithDocuments(dimension);
    const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
    assert.equal((quality.match(/<pre><code>/g) ?? []).length, 2);
    assert.equal(quality.split(`<code>${escapeHtml(declaration)}</code>`).length - 1, declaration === dimension.findings[0].document.before.declaration ? 2 : 1);
    assert.doesNotMatch(quality, /Recorded @doc text/);
  }
});

test("declarations without their own @doc retain separately recorded doc text without source reconstruction", () => {
  for (const declaration of [
    "count: int32;",
    '/* @doc("comment, not evidence") */\ncount: int32;',
    '@extension("literal @doc(\\"not evidence\\")")\ncount: int32;',
    'model Widget {\n  @doc("Nested property documentation.")\n  count: int32;\n}',
  ]) {
    const dimension = documentDimension();
    const after = dimension.findings[0].document.after;
    after.doc = 'Augmented documentation with <markup> & "quotes".';
    after.declaration = declaration;
    const html = presentationWithDocuments(dimension);
    const quality = html.slice(html.indexOf('<section id="document-quality">'), html.indexOf('<section id="semantic-intents">'));
    assert.match(quality, /Recorded @doc text/);
    assert.ok(quality.includes(`<code>${escapeHtml(after.doc)}</code>`));
    assert.ok(quality.includes(`<code>${escapeHtml(declaration)}</code>`));
    assert.equal((quality.match(/<pre><code>/g) ?? []).length, 3);
    assert.doesNotMatch(quality, /<markup>/);
  }
});
