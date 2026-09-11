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

function presentationWithDocuments(dimension, downstream) {
  const input = assessment(downstream);
  input.dimensions.documentQuality = dimension;
  return renderReportSections(input, {
    escapeHtml, operationContractRows: () => [], complianceFindingGroups: () => [],
    renderSourceHunks: () => "", sourceLinks: () => "", directLegacyDownstreamFindings: () => [],
  }).html;
}

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
  assert.equal(documentQualitySummary(noDocs).label, "Passed");
  assert.doesNotMatch(noDocsHtml, /Document Quality and Agent Friendliness is not assessed/);
  const legacy = presentationWithDocuments({ status: "not-assessed", summary: "Historical documentation evidence unavailable." });
  const legacyQuality = legacy.slice(legacy.indexOf('<section id="document-quality">'), legacy.indexOf('<section id="semantic-intents">'));
  assert.match(legacyQuality, /Historical documentation evidence unavailable/);
  assert.doesNotMatch(legacyQuality, /0 findings|checks assessed|passed/);
  assert.equal(documentQualitySummary().label, "Not assessed");
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
  assert.match(html, /0\/1 intents assessed/);
  assert.match(html, /meaning review is blocked by missing evidence/);
  assert.match(html, /Documentation not assessed for:/);
  assert.match(html, /The full contract is unavailable/);
});

test("document snapshots show exact declaration source once when leading decorators already include @doc", () => {
  for (const declaration of [
    '@doc("The number of widgets.")\ncount: int32;',
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
