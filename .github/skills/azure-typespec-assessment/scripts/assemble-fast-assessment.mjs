#!/usr/bin/env node

import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const LEVELS = new Set(["high", "medium", "low"]);
const STATUSES = new Set(["passed", "failed", "not-assessed"]);

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function nonEmpty(value, label) {
  assert(
    typeof value === "string" && value.trim().length > 0,
    `${label} must be a non-empty string.`,
  );
}

function uniqueStrings(value, label, { required = false } = {}) {
  assert(Array.isArray(value), `${label} must be an array.`);
  if (required) assert(value.length > 0, `${label} must not be empty.`);
  for (const [index, item] of value.entries()) {
    nonEmpty(item, `${label}[${index}]`);
  }
  assert(
    new Set(value).size === value.length,
    `${label} must not contain duplicates.`,
  );
}

function evidenceIndex(modelInput) {
  const sourceChanges = new Map();
  const sourcePaths = new Set(modelInput.changedFiles ?? []);
  for (const file of modelInput.sourceFiles ?? []) {
    sourcePaths.add(file.path);
    for (const change of file.changes ?? []) {
      sourceChanges.set(change.id, { ...change, path: file.path });
    }
  }
  const operationIds = new Set();
  const restCandidates = new Map();
  const downstreamCandidates = new Map();
  for (const project of modelInput.projects ?? []) {
    for (const change of project.rest?.operationChanges ?? []) {
      if (change.operationId) operationIds.add(change.operationId);
    }
    for (const group of project.rest?.operationGroups ?? []) {
      for (const operationId of group.operationIds ?? []) {
        operationIds.add(operationId);
      }
    }
    for (const candidate of project.rest?.breakingCandidates ?? []) {
      assert(
        !restCandidates.has(candidate.id),
        `Duplicate REST candidate ID: ${candidate.id}.`,
      );
      restCandidates.set(candidate.id, candidate);
    }
    for (const candidate of project.downstream?.candidates ?? []) {
      assert(
        !downstreamCandidates.has(candidate.id),
        `Duplicate downstream candidate ID: ${candidate.id}.`,
      );
      downstreamCandidates.set(candidate.id, candidate);
    }
  }
  const documents = new Map(
    (modelInput.complianceEvidence?.documents ?? []).map((document) => [
      document.url,
      document,
    ]),
  );
  return {
    sourceChanges,
    sourcePaths,
    operationIds,
    restCandidates,
    downstreamCandidates,
    documents,
  };
}

function validateSources(item, label, evidence) {
  uniqueStrings(item.sourceChangeIds, `${label}.sourceChangeIds`);
  uniqueStrings(item.sourcePaths, `${label}.sourcePaths`);
  assert(
    item.sourceChangeIds.length + item.sourcePaths.length > 0,
    `${label} must reference changed TypeSpec source.`,
  );
  for (const id of item.sourceChangeIds) {
    assert(
      evidence.sourceChanges.has(id),
      `${label} references unknown source change: ${id}.`,
    );
  }
  for (const path of item.sourcePaths) {
    assert(
      evidence.sourcePaths.has(path),
      `${label} references unknown source path: ${path}.`,
    );
  }
}

function validateFinding(item, label, evidence, { compliance = false } = {}) {
  for (const field of ["title", "actual", "expected"]) {
    nonEmpty(item[field], `${label}.${field}`);
  }
  assert(LEVELS.has(item.severity), `${label}.severity is invalid.`);
  assert(LEVELS.has(item.confidence), `${label}.confidence is invalid.`);
  uniqueStrings(item.evidence, `${label}.evidence`, { required: true });
  uniqueStrings(item.affectedOperationIds, `${label}.affectedOperationIds`);
  for (const operationId of item.affectedOperationIds) {
    assert(
      evidence.operationIds.has(operationId),
      `${label} references unknown operation: ${operationId}.`,
    );
  }
  validateSources(item, label, evidence);
  if (compliance) {
    nonEmpty(item.documentationUrl, `${label}.documentationUrl`);
    assert(
      evidence.documents.has(item.documentationUrl),
      `${label} references unavailable documentation: ${item.documentationUrl}.`,
    );
  }
}

function validateDecisions(decisions, candidates, label, evidence) {
  assert(Array.isArray(decisions), `${label} must be an array.`);
  const seen = new Set();
  for (const [index, decision] of decisions.entries()) {
    const itemLabel = `${label}[${index}]`;
    nonEmpty(decision.id, `${itemLabel}.id`);
    assert(candidates.has(decision.id), `${itemLabel} references an unknown candidate.`);
    assert(!seen.has(decision.id), `${itemLabel} duplicates candidate ${decision.id}.`);
    seen.add(decision.id);
    assert(
      ["approve", "reject"].includes(decision.decision),
      `${itemLabel}.decision must be approve or reject.`,
    );
    if (decision.decision === "approve") {
      validateFinding(decision, itemLabel, evidence);
    } else {
      nonEmpty(decision.rationale, `${itemLabel}.rationale`);
    }
  }
  const missing = [...candidates.keys()].filter((id) => !seen.has(id));
  assert(
    missing.length === 0,
    `${label} leaves candidate(s) undecided: ${missing.join(", ")}.`,
  );
}

export function validateFastJudgment(judgment, modelInput) {
  assert(
    modelInput?.schemaVersion === 1 && modelInput.mode === "impact-only",
    "fast-model-input.json must be an impact-only schemaVersion 1 document.",
  );
  assert(judgment?.schemaVersion === 1, "Fast judgment schemaVersion must be 1.");
  const evidence = evidenceIndex(modelInput);
  validateDecisions(
    judgment.restCandidates,
    evidence.restCandidates,
    "restCandidates",
    evidence,
  );
  validateDecisions(
    judgment.downstreamCandidates,
    evidence.downstreamCandidates,
    "downstreamCandidates",
    evidence,
  );
  assert(
    judgment.compliance && STATUSES.has(judgment.compliance.status),
    "compliance.status must be passed, failed, or not-assessed.",
  );
  nonEmpty(judgment.compliance.rationale, "compliance.rationale");
  assert(
    Array.isArray(judgment.compliance.findings),
    "compliance.findings must be an array.",
  );
  for (const [index, finding] of judgment.compliance.findings.entries()) {
    nonEmpty(finding.id, `compliance.findings[${index}].id`);
    validateFinding(
      finding,
      `compliance.findings[${index}]`,
      evidence,
      { compliance: true },
    );
  }
  assert(
    judgment.compliance.status !== "failed" ||
      judgment.compliance.findings.length > 0,
    "Failed compliance requires at least one finding.",
  );
  assert(
    judgment.compliance.status === "failed" ||
      judgment.compliance.findings.length === 0,
    `${judgment.compliance.status} compliance cannot contain findings.`,
  );
  assert(
    LEVELS.has(judgment.overallConfidence),
    "overallConfidence must be high, medium, or low.",
  );
  uniqueStrings(judgment.blockers, "blockers");
  return evidence;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function sourceSnippets(item, evidence) {
  const snippets = item.sourceChangeIds.map((id) => evidence.sourceChanges.get(id));
  const referencedPaths = new Set(snippets.map((snippet) => snippet.path));
  for (const path of item.sourcePaths) {
    if (!referencedPaths.has(path)) snippets.push({ path, lines: [] });
  }
  return snippets;
}

function downstreamSourceTerms(candidate) {
  const terms = [];
  switch (candidate?.rule) {
    case "client-property-flattening-changed":
      terms.push("flattenProperty");
      break;
    case "client-location-changed":
      terms.push("clientLocation");
      break;
    case "sdk-lro-recognition-changed":
      terms.push(
        "useFinalStateVia",
        "pollingOperation",
        "x-ms-long-running-operation",
      );
      break;
    case "paging-metadata-added":
      terms.push("@list", "@pageItems", "@nextLink");
      break;
    case "sdk-lro-to-synchronous":
      terms.push("ArmResourceActionAsync", "ArmResourceActionSync");
      break;
  }
  for (const entry of candidate?.evidence ?? []) {
    if (entry.symbol) terms.push(entry.symbol);
    for (const symbol of entry.symbols ?? []) terms.push(symbol);
  }
  return [...new Set(terms)];
}

const complianceStopWords = new Set([
  "actual",
  "added",
  "adding",
  "behavior",
  "changed",
  "code",
  "documented",
  "expected",
  "finding",
  "guidance",
  "model",
  "resource",
  "should",
  "source",
  "typespec",
  "version",
]);

function focusedComplianceSnippets(item, snippets) {
  const terms = [
    item.title,
    item.actual,
    item.expected,
    ...item.evidence,
  ]
    .join(" ")
    .match(/[@A-Za-z_][A-Za-z0-9_.-]{3,}/g)
    ?.filter((term) => !complianceStopWords.has(term.toLowerCase())) ?? [];
  const scored = snippets
    .map((snippet, index) => {
      const source = [
        ...(snippet.lines ?? []),
        ...(snippet.diffLines ?? []).map(({ text }) => text),
      ].join("\n");
      return {
        index,
        score: [...new Set(terms)].filter((term) => source.includes(term)).length,
        snippet,
      };
    })
    .filter(({ score }) => score > 0)
    .sort((left, right) => right.score - left.score || left.index - right.index)
    .slice(0, 2)
    .map(({ snippet }) => snippet);
  return scored.length > 0 ? scored : snippets.slice(0, 2);
}

function findingSourceSnippets(
  item,
  evidence,
  { downstream = false, compliance = false } = {},
) {
  const snippets = sourceSnippets(item, evidence);
  if (compliance) return focusedComplianceSnippets(item, snippets);
  if (!downstream) return snippets;
  const candidate = evidence.downstreamCandidates.get(item.id);
  const terms = downstreamSourceTerms(candidate);
  if (terms.length === 0) return snippets;
  const focused = snippets.filter((snippet) => {
    const source = [
      ...(snippet.lines ?? []),
      ...(snippet.diffLines ?? []).map(({ text }) => text),
    ].join("\n");
    return terms.some((term) => source.includes(term));
  });
  return focused.length > 0 ? focused : snippets;
}

function renderFinding(
  item,
  evidence,
  { downstream = false, compliance = false } = {},
) {
  const operations =
    compliance && item.affectedOperationIds.length > 0
      ? `<div class="operations"><strong>Affected operations</strong><div class="operation-list">${item.affectedOperationIds
          .map((operation) => `<code>${escapeHtml(operation)}</code>`)
          .join("")}</div></div>`
      : "";
  const guidance = item.documentationUrl
    ? `<p class="guidance"><strong>Guidance</strong><a href="${escapeHtml(item.documentationUrl)}">${escapeHtml(item.documentationUrl)}</a></p>`
    : "";
  const selectedSnippets = findingSourceSnippets(item, evidence, {
    downstream,
    compliance,
  });
  const snippets = selectedSnippets
    .map((snippet) => {
      const location = snippet.newStart
        ? `${snippet.path}:${snippet.newStart}`
        : snippet.path;
      const diffLines =
        snippet.diffLines ??
        (snippet.lines ?? []).map((text) => ({ kind: "context", text }));
      const code =
        diffLines.length > 0
          ? `<div class="code-block"><pre><code>${diffLines
              .slice(0, 24)
              .map(
                ({ kind, text }) =>
                  `<span class="diff-line ${escapeHtml(kind)}"><span class="diff-marker">${kind === "add" ? "+" : kind === "remove" ? "-" : " "}</span>${escapeHtml(text)}</span>`,
              )
              .join("")}</code></pre></div>`
          : `<p class="empty">Changed file: <code>${escapeHtml(snippet.path)}</code></p>`;
      return `<details class="source-details"${downstream ? " open" : ""}><summary><span>${escapeHtml(location)}</span></summary>${code}</details>`;
    })
    .join("");
  const sourceLocations = [
    ...new Set(
      selectedSnippets.map((snippet) =>
        snippet.newStart
          ? `${snippet.path}:${snippet.newStart}`
          : snippet.path,
      ),
    ),
  ];
  if (compliance) {
    const document = evidence.documents.get(item.documentationUrl) ?? {
      title: "Referenced guidance",
      section: "Unavailable",
      matchingExcerpt: item.expected,
      candidateCodeBlocks: [],
    };
    const expectedCode =
      document.candidateCodeBlocks?.length > 0
        ? document.candidateCodeBlocks
            .map(
              (block) =>
                `<div class="code-block expected-code"><pre><code>${escapeHtml((block.lines ?? []).join("\n"))}</code></pre></div>`,
            )
            .join("")
        : '<p class="empty-state">The bounded official document evidence did not contain an example block.</p>';
    return `<details class="finding compliance-finding severity-border-${escapeHtml(item.severity)}">
<summary><span class="severity severity-${escapeHtml(item.severity)}">${escapeHtml(item.severity)}</span><h3>${escapeHtml(item.title)}</h3></summary>
<div class="finding-body">
<p><strong>Gap:</strong> ${escapeHtml(item.actual)}</p>
<details class="comparison-details expected-details">
<summary>Expected</summary>
<div class="comparison-body">
<p>${escapeHtml(item.expected)}</p>
<p><strong>Guidance:</strong> <a href="${escapeHtml(item.documentationUrl)}">${escapeHtml(document.title)} — ${escapeHtml(document.section ?? document.category ?? "Retained authoritative evidence")}</a></p>
${expectedCode}
</div>
</details>
<details class="comparison-details actual-details">
<summary>Actual</summary>
<div class="comparison-body">
<p>${escapeHtml(item.evidence.join("; "))}</p>
<div class="compliance-code"><h4>TypeSpec code</h4>${snippets}</div>
</div>
</details>
</div>
</details>`;
  }
  const impact = !compliance;
  const behavior = impact
    ? `<p>${escapeHtml(item.actual)}</p>
<p><strong>Evidence:</strong> ${escapeHtml(downstream ? (evidence.downstreamCandidates.get(item.id)?.summary ?? item.evidence[0]) : item.evidence.join("; "))}</p>
<p class="sources"><strong>TypeSpec source:</strong> ${sourceLocations.map((location) => `<code>${escapeHtml(location)}</code>`).join(", ")}</p>`
    : `<div class="behavior-grid"><div class="behavior-card actual"><span>Actual behavior</span><p>${escapeHtml(item.actual)}</p></div><div class="behavior-card expected"><span>Expected behavior</span><p>${escapeHtml(item.expected)}</p></div></div>`;
  const findingEvidence = compliance
    ? `<div class="evidence"><strong>Evidence</strong><ul>${item.evidence.map((entry) => `<li>${escapeHtml(entry)}</li>`).join("")}</ul></div>`
    : "";
  const sourceCode = downstream || compliance
    ? `<div class="source-code"><h4>TypeSpec code change ${compliance ? '<span class="hint">expand to inspect</span>' : ""}</h4>${snippets}</div>`
    : "";
  return `<article class="finding severity-border-${escapeHtml(item.severity)}">
<div class="finding-heading"><span class="severity severity-${escapeHtml(item.severity)}">${escapeHtml(item.severity)}</span><h3>${escapeHtml(item.title)}</h3></div>
${behavior}
${operations}
${findingEvidence}
${guidance}
${sourceCode}
</article>`;
}

function approved(decisions) {
  return decisions.filter((decision) => decision.decision === "approve");
}

function renderSection(id, title, findings, evidence, status = "") {
  return `<section id="${id}"><h2>${escapeHtml(title)} <span class="count">${findings.length}</span>${status}</h2>${
    findings.length > 0
      ? findings
          .map((finding) =>
            renderFinding(finding, evidence, {
              downstream: id === "downstream",
              compliance: id === "compliance",
            }),
          )
          .join("")
      : `<div class="panel empty-state good">No ${escapeHtml(title.toLowerCase())} detected.</div>`
  }</section>`;
}

export function renderFastAssessmentHtml(modelInput, judgment) {
  const evidence = validateFastJudgment(judgment, modelInput);
  const rest = approved(judgment.restCandidates);
  const downstream = approved(judgment.downstreamCandidates);
  const compliance = judgment.compliance.findings;
  const blockers =
    judgment.blockers.length > 0
      ? `<section class="blockers"><h2>Assessment blockers</h2><div class="panel"><ul>${judgment.blockers.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}</ul></div></section>`
      : "";
  const baseline = String(
    modelInput.baseline?.commit ?? modelInput.baseline?.ref ?? "baseline",
  ).slice(0, 12);
  const head = String(
    modelInput.head?.commit ?? modelInput.head?.ref ?? "head",
  ).slice(0, 12);
  const complianceStatus = judgment.compliance.status;
  const severityLevels = [...rest, ...downstream, ...compliance].map(
    ({ severity }) => severity,
  );
  const codeSafety = severityLevels.includes("high")
    ? "Low"
    : severityLevels.includes("medium")
      ? "Medium"
      : "High";
  const complianceIcon =
    complianceStatus === "passed"
      ? "&#10003;"
      : complianceStatus === "failed"
        ? "&#10005;"
        : "&#9888;";
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Fast TypeSpec Impact Assessment</title>
<style>
:root{color-scheme:light dark;--bg:#f5f7fb;--panel:#fff;--text:#172033;--muted:#64748b;--line:#dbe3ef;--accent:#2563eb;--accent-soft:#eff6ff;--good:#047857;--warn:#b45309;--danger:#b91c1c;--code:#111827;--code-text:#e5e7eb;--add-bg:#163d2b;--add:#9ae6b4;--remove-bg:#4a2028;--remove:#feb2b2}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}a{color:var(--accent);text-decoration:none}a:hover{text-decoration:underline}.container{width:min(1180px,calc(100% - 32px));margin:auto}.hero{background:linear-gradient(125deg,#172554,#1d4ed8);color:#fff;padding:44px 0 34px}.eyebrow{text-transform:uppercase;letter-spacing:.12em;font-size:12px;font-weight:700;opacity:.8}.hero h1{font-size:clamp(28px,4vw,42px);line-height:1.15;margin:8px 0 10px}.hero-meta{opacity:.82}.metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-top:28px}.metric{display:block;min-width:0;padding:16px;background:#ffffff16;border:1px solid #ffffff2b;border-radius:12px;color:inherit;text-decoration:none}.metric:hover{background:#ffffff24;text-decoration:none}.metric strong{display:flex;align-items:center;gap:8px;font-size:24px}.metric-title{display:block;font-size:15px;font-weight:650;opacity:.9}.metric-detail{display:block;margin-top:4px;font-size:12px;opacity:.75}.metric-icon.good{color:#86efac}.metric-icon.danger{color:#fecaca}.metric-icon.warn{color:#fde68a}nav{position:sticky;top:0;z-index:5;background:color-mix(in srgb,var(--panel) 92%,transparent);backdrop-filter:blur(12px);border-bottom:1px solid var(--line)}nav .container{display:flex;gap:22px;padding:12px 0;overflow:auto}main{padding:30px 0 60px}section{scroll-margin-top:64px;margin-bottom:34px}h2{font-size:25px;margin:0 0 16px}h3{line-height:1.3}.count{display:inline-block;margin-left:5px;padding:1px 8px;background:var(--line);border-radius:999px;padding:1px 8px;font-size:12px;vertical-align:middle}.panel,.finding{background:var(--panel);border:1px solid var(--line);border-radius:14px;box-shadow:0 4px 18px #0f172a0a}.panel{padding:20px}.finding{min-width:0;margin:12px 0;padding:20px 22px}.severity-border-high{border-left:4px solid var(--danger)}.severity-border-medium{border-left:4px solid var(--warn)}.severity-border-low{border-left:4px solid #64748b}.finding-heading{display:flex;align-items:flex-start;gap:10px}.finding-heading h3{margin:2px 0 0;font-size:19px}.severity,.compliance-status{display:inline-flex;border-radius:999px;padding:3px 9px;font-size:12px;font-weight:700;text-transform:uppercase;white-space:nowrap}.severity-high,.compliance-status.failed{color:#991b1b;background:#fee2e2}.severity-medium,.compliance-status.not-assessed{color:#92400e;background:#fef3c7}.severity-low{color:#475569;background:#e2e8f0}.compliance-status.passed{color:#166534;background:#dcfce7}.compliance-finding>summary{display:flex;align-items:center;gap:10px;cursor:pointer;list-style:none}.compliance-finding>summary::-webkit-details-marker{display:none}.compliance-finding>summary::after{content:"\\25B8";margin-left:auto;color:var(--muted);font-size:18px}.compliance-finding[open]>summary::after{content:"\\25BE"}.compliance-finding>summary h3{margin:0}.finding-body{padding-top:12px}.comparison-details{margin:10px 0;border:1px solid var(--line);border-radius:10px;background:var(--bg)}.comparison-details>summary{cursor:pointer;padding:11px 13px;font-weight:750}.comparison-body{padding:0 13px 13px}.compliance-code{margin-top:12px}.compliance-code h4{margin:14px 0 6px}.behavior-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin:18px 0}.behavior-card{min-width:0;padding:14px 16px;border:1px solid var(--line);border-radius:10px;background:var(--bg)}.behavior-card>span{display:block;margin-bottom:6px;color:var(--muted);font-size:12px;font-weight:750;text-transform:uppercase;letter-spacing:.04em}.behavior-card.actual{border-top:3px solid var(--danger)}.behavior-card.expected{border-top:3px solid var(--good)}.behavior-card p{margin:0}.sources{color:var(--muted);font-size:13px}.sources code{overflow-wrap:anywhere}.operations,.evidence,.guidance{margin:16px 0}.operations>strong,.evidence>strong,.guidance>strong{display:block;margin-bottom:8px}.operation-list{display:flex;flex-wrap:wrap;gap:6px}.operation-list code{max-width:100%;padding:4px 8px;border:1px solid var(--line);border-radius:999px;background:var(--accent-soft);color:var(--accent);overflow-wrap:anywhere}.evidence{padding:14px 16px;border-radius:10px;background:var(--bg)}.evidence ul{margin:8px 0 0;padding-left:22px}.guidance a{display:block;overflow-wrap:anywhere}.source-code{margin-top:18px;padding-top:14px;border-top:1px solid var(--line)}.source-code h4{margin:0 0 10px}.hint{color:var(--muted);font-size:12px;font-weight:400}.source-details{min-width:0;margin:8px 0;border:1px solid var(--line);border-radius:10px;background:var(--bg);overflow:hidden}.source-details>summary{display:flex;align-items:center;cursor:pointer;padding:11px 13px;font:13px/1.4 ui-monospace,SFMono-Regular,Consolas,monospace;list-style:none}.source-details>summary::-webkit-details-marker{display:none}.source-details>summary::before{content:"\\25B8";margin-right:8px;color:var(--muted)}.source-details[open]>summary::before{content:"\\25BE"}.source-details>summary span{min-width:0;overflow-wrap:anywhere}.code-block{max-width:100%;overflow:hidden;border-top:1px solid #334155;background:var(--code)}.code-block pre{max-width:100%;margin:0;padding:10px 0;color:var(--code-text);font:13px/1.55 ui-monospace,SFMono-Regular,Consolas,monospace;white-space:pre-wrap;overflow-wrap:anywhere;word-break:break-word}.diff-line{display:block;min-height:1.55em;padding:1px 14px}.diff-line.add{background:var(--add-bg);color:var(--add)}.diff-line.remove{background:var(--remove-bg);color:var(--remove)}.diff-marker{display:inline-block;width:18px;font-weight:800;user-select:none}.empty-state{color:var(--good);font-weight:600}.blockers .panel{border-left:4px solid var(--warn)}.blockers ul{margin:0;padding-left:22px}@media(max-width:760px){.hero{padding-top:28px}.metrics,.behavior-grid{grid-template-columns:1fr}.finding{padding:16px}.finding-heading{align-items:flex-start}}@media(prefers-color-scheme:dark){:root{--bg:#0f172a;--panel:#172033;--text:#e5e7eb;--muted:#9ca3af;--line:#334155;--accent:#93c5fd;--accent-soft:#1e3a5f}.severity-high,.compliance-status.failed{background:#4a2028;color:#fecaca}.severity-medium,.compliance-status.not-assessed{background:#473b16;color:#fde68a}.severity-low{background:#334155;color:#cbd5e1}.compliance-status.passed{background:#163d2b;color:#9ae6b4}}
</style></head><body><header class="hero"><div class="container"><div class="eyebrow">Impact-only TypeSpec assessment</div><h1>Fast TypeSpec Assessment</h1>
<div class="hero-meta">Overall confidence: <strong>${escapeHtml(judgment.overallConfidence)}</strong> &middot; Baseline <code>${escapeHtml(baseline)}</code> &rarr; Head <code>${escapeHtml(head)}</code></div>
<div class="metrics"><div class="metric"><strong><span class="metric-icon ${codeSafety === "High" ? "good" : codeSafety === "Medium" ? "warn" : "danger"}">${codeSafety === "High" ? "&#10003;" : codeSafety === "Medium" ? "&#9888;" : "&#10005;"}</span>${codeSafety}</strong><span class="metric-title">Overall code safety</span></div><a class="metric" href="#rest"><strong><span class="metric-icon ${rest.length > 0 ? "danger" : "good"}">${rest.length > 0 ? "&#10005;" : "&#10003;"}</span>${rest.length}</strong><span class="metric-title">REST breaking changes</span></a><a class="metric" href="#downstream"><strong><span class="metric-icon ${downstream.length > 0 ? "danger" : "good"}">${downstream.length > 0 ? "&#10005;" : "&#10003;"}</span>${downstream.length}</strong><span class="metric-title">Downstream breaking changes</span></a><a class="metric" href="#compliance"><strong><span class="metric-icon ${complianceStatus === "passed" ? "good" : complianceStatus === "failed" ? "danger" : "warn"}">${complianceIcon}</span>${compliance.length}</strong><span class="metric-title">Azure compliance</span><span class="metric-detail">${escapeHtml(complianceStatus)}</span></a></div>
</div></header><nav><div class="container"><a href="#rest">REST breaking changes</a><a href="#downstream">SDK/downstream changes</a><a href="#compliance">Azure compliance</a></div></nav><main class="container">${blockers}
${renderSection("rest", "REST breaking changes", rest, evidence)}
${renderSection("downstream", "SDK/downstream breaking changes", downstream, evidence)}
${renderSection("compliance", "Azure compliance", compliance, evidence, `<span class="compliance-status ${escapeHtml(complianceStatus)}">${escapeHtml(complianceStatus)}</span>`)}
</main></body></html>`;
}

export function assembleFastAssessmentFiles(
  modelInputPath,
  judgmentPath,
  outputPath,
) {
  const modelInput = JSON.parse(readFileSync(resolve(modelInputPath), "utf8"));
  const judgment = JSON.parse(readFileSync(resolve(judgmentPath), "utf8"));
  const resolvedOutput = outputPath
    ? resolve(outputPath)
    : resolve(dirname(resolve(judgmentPath)), "fast-assessment.html");
  assert(
    !existsSync(resolvedOutput),
    `Refusing to overwrite existing report: ${resolvedOutput}.`,
  );
  writeFileSync(
    resolvedOutput,
    renderFastAssessmentHtml(modelInput, judgment),
  );
  return resolvedOutput;
}

function main() {
  const [modelInputPath, judgmentPath, outputPath] = process.argv.slice(2);
  assert(
    modelInputPath && judgmentPath,
    "Usage: assemble-fast-assessment.mjs <fast-model-input.json> <fast-assessment-judgment.json> [fast-assessment.html]",
  );
  const output = assembleFastAssessmentFiles(
    modelInputPath,
    judgmentPath,
    outputPath,
  );
  process.stdout.write(`Wrote ${output}\n`);
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  try {
    main();
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
