import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";
import { downstreamMethodData, downstreamPresentationDimension, sdkTypeName } from "./assessment-report-ui.mjs";
import { renderAssessmentHtml } from "./render-assessment-html.mjs";

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
