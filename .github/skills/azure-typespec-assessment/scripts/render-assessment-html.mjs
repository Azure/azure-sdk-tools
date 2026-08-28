import fs from "node:fs";
import path from "node:path";
import { isMain, readJson, runMain } from "./cli.mjs";
import { validateAssessment } from "./validate-assessment.mjs";

export function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function sourceLinks(sources = []) {
  return sources
    .map((source) => {
      const current = source.declarations?.find((item) => item.source?.revision === "current");
      const label = `${source.path}${current ? `:${current.source.startLine}` : ""}`;
      return current?.source?.link
        ? `<a href="${escapeHtml(current.source.link)}">${escapeHtml(label)}</a>`
        : `<code>${escapeHtml(label)}</code>`;
    })
    .join(", ");
}

function anchor(value) {
  return String(value).replaceAll(/[^A-Za-z0-9_-]/g, "-");
}

function artifactLabel(selection = {}) {
  const commit = selection.commit ?? "unknown commit";
  return `${commit}@${selection.apiVersion ?? "unversioned"}`;
}

function headerTitle(assessment) {
  if (assessment.displayTitle) return assessment.displayTitle;
  const intents = assessment.dimensions.semantic.items ?? [];
  if (intents.length === 1) return intents[0].title;
  const services = [...new Set((assessment.projects ?? []).map((project) =>
    project.path?.split(/[\\/]/).filter(Boolean).at(-1)).filter(Boolean))];
  if (intents.length && services.length) {
    return `${intents.length} TypeSpec changes for ${services.join(" and ")}`;
  }
  return assessment.title ?? "Current TypeSpec changes";
}

function headerSummary(assessment) {
  const semanticItems = assessment.dimensions.semantic.items ?? [];
  const actionCounts = { add: 0, modify: 0, remove: 0 };
  for (const item of semanticItems) {
    if (item.action in actionCounts) actionCounts[item.action] += 1;
  }
  const operationCount = new Set(semanticItems.flatMap((item) =>
    (item.operations ?? []).map((operation) =>
      `${operation.projectId ?? ""}:${operation.operationId}`))).size;
  const restCount = assessment.dimensions.rest.findings.length;
  const downstreamMethodCount =
    assessment.dimensions.downstream.operationGroups?.length ?? 0;
  const downstreamFindingCount =
    assessment.dimensions.downstream.findings?.length ?? 0;
  const safety = assessment.safety.status;
  const safetyTarget = restCount
    ? "#rest-breaking"
    : downstreamMethodCount || downstreamFindingCount
      ? "#downstream-breaking"
      : "#rest-breaking";
  return {
    semanticItems,
    actionCounts,
    operationCount,
    restCount,
    downstreamMethodCount,
    downstreamFindingCount,
    safety,
    safetyTarget,
    safetyIcon: safety === "passed" ? "✓" : safety === "failed" ? "×" : "i",
    safetyLabel:
      safety === "passed" ? "Passed" :
      safety === "failed" ? "Failed" :
      "Not assessed",
  };
}

function headerComparison(assessment, comparisons) {
  if ((assessment.projects ?? []).length !== 1) {
    return '<a href="#projects-and-compiler-status">See Projects and compiler status in Appendix</a>';
  }
  const project = assessment.projects[0];
  const comparison = comparisons.get(project.id) ?? project.artifactComparison;
  if (!comparison?.baseline || !comparison?.target) {
    return '<a href="#projects-and-compiler-status">See Projects and compiler status in Appendix</a>';
  }
  return `<code>${escapeHtml(artifactLabel(comparison.baseline))}</code> → <code>${escapeHtml(artifactLabel(comparison.target))}</code>`;
}

function semanticLinks(ids = []) {
  return ids.length
    ? ids.map((id) => `<a href="#intent-${anchor(id)}"><code>${escapeHtml(id)}</code></a>`).join(", ")
    : "None";
}

function findingCards(findings, downstream = false) {
  if (!findings.length) return '<p class="empty good">No breaking changes detected.</p>';
  return findings
    .map(
      (finding) => `<article class="finding ${escapeHtml(finding.severity)}" id="finding-${anchor(finding.id)}">
<h3>${escapeHtml(finding.rule)} <span>${escapeHtml(finding.severity)}</span></h3>
${downstream ? `<p><strong>SDK symbol:</strong> <code>${escapeHtml(finding.crossLanguageDefinitionId ?? finding.symbol)}</code></p>` : ""}
<dl><dt>Actual</dt><dd>${escapeHtml(finding.actual)}</dd><dt>Expected</dt><dd>${escapeHtml(finding.expected)}</dd></dl>
<p>${escapeHtml(finding.rationale)}</p>
<p><strong>Related semantic intents:</strong> ${semanticLinks(finding.relatedSemanticIntents)}</p>
<p class="sources"><strong>Changed TypeSpec:</strong> ${sourceLinks(finding.sources)}</p>
</article>`,
    )
    .join("\n");
}

function contractSummary(value, field) {
  if (value === undefined) return "—";
  if (field === "parameters") {
    return (value ?? []).map((item) =>
      `${item.in ?? "body"}:${item.name}${item.required ? " (required)" : ""}`,
    ).join(", ") || "None";
  }
  if (field === "responses") {
    return (value ?? []).map((item) => item.status).join(", ") || "None";
  }
  if (field === "request") return value ? value.kind ?? "present" : "None";
  if (field === "paging" || field === "lro") return value ? "present" : "None";
  return String(value);
}

function renderSourceHunks(sources = []) {
  return sources.flatMap((source) => (source.hunks ?? []).map((hunk) =>
    `<div class="diff"><div class="diff-path">${escapeHtml(source.path)}</div><pre>${(hunk.lines ?? [])
      .map((line) => {
        const kind = line.startsWith("+") ? "add" : line.startsWith("-") ? "remove" : "context";
        return `<span class="${kind}">${escapeHtml(line)}</span>`;
      }).join("\n")}</pre></div>`,
  )).join("");
}

function substantiveChange(hunk) {
  return (hunk.lines ?? []).some((line) => {
    if (!line.startsWith("+") && !line.startsWith("-")) return false;
    const code = line.slice(1).trim();
    return code &&
      !/^import\s/.test(code) &&
      !/^using\s/.test(code);
  });
}

function sourceStartLine(source, hunk) {
  const hunkStartLine = hunk.current?.startLine ?? hunk.base?.startLine;
  if (Number.isFinite(hunkStartLine)) return hunkStartLine;
  const declarationLines = (source.declarations ?? [])
    .flatMap((declaration) => declaration.hunkIds?.includes(hunk.id)
      ? [declaration.source?.startLine]
      : [])
    .filter(Number.isFinite);
  return Math.min(...declarationLines, Number.MAX_SAFE_INTEGER);
}

export function representativeSource(item) {
  const operationHunkIds = new Set((item.operations ?? []).flatMap((operation) =>
    (operation.sources ?? []).flatMap((source) =>
      (source.hunks ?? []).map((hunk) => hunk.id))));
  const candidates = (item.sources ?? []).flatMap((source) =>
    (source.hunks ?? []).map((hunk) => {
      const declaration = (source.declarations ?? []).some((item) =>
        item.hunkIds?.includes(hunk.id));
      return {
        source,
        hunk,
        score: [
          operationHunkIds.has(hunk.id) ? 1 : 0,
          declaration ? 1 : 0,
          substantiveChange(hunk) ? 1 : 0,
        ],
        startLine: sourceStartLine(source, hunk),
      };
    }));
  candidates.sort((left, right) => {
    for (let index = 0; index < left.score.length; index += 1) {
      if (left.score[index] !== right.score[index]) {
        return right.score[index] - left.score[index];
      }
    }
    return String(left.source.path ?? "").localeCompare(String(right.source.path ?? "")) ||
      left.startLine - right.startLine ||
      String(left.hunk.id ?? "").localeCompare(String(right.hunk.id ?? ""));
  });
  if (!candidates.length) return undefined;
  const selected = candidates[0];
  return {
    ...selected.source,
    hunks: [selected.hunk],
    declarations: (selected.source.declarations ?? []).filter((declaration) =>
      declaration.hunkIds?.includes(selected.hunk.id)),
  };
}

function representativeExample(item) {
  const source = representativeSource(item);
  if (!source) {
    return '<p class="sources">No representative TypeSpec example available.</p>';
  }
  return `<details class="representative-example"><summary><strong>Representative TypeSpec example</strong></summary>
${renderSourceHunks([source])}
<p class="sources"><strong>Source:</strong> ${sourceLinks([source])}. Complete TypeSpec evidence is retained in <a href="#complete-typespec-evidence">Appendix</a>.</p></details>`;
}

function operationCard(operation) {
  const changes = operation.changedAspects.length
    ? `<table><thead><tr><th>Aspect</th><th>Before</th><th>After</th></tr></thead><tbody>${operation.changedAspects
      .map((field) => `<tr><td>${escapeHtml(field)}</td><td>${escapeHtml(contractSummary(operation.before?.[field], field))}</td><td>${escapeHtml(contractSummary(operation.after?.[field], field))}</td></tr>`)
      .join("")}</tbody></table>`
    : `<p class="good"><strong>${escapeHtml(operation.outcome)}</strong></p>`;
  return `<details class="operation"><summary><strong>${escapeHtml(operation.operationId)}</strong> <code>${escapeHtml((operation.method ?? "").toUpperCase())} ${escapeHtml(operation.path)}</code> <span>${escapeHtml(operation.apiVersion)}</span></summary>
<div class="operation-body">${changes}
<p><strong>Change outcome:</strong> ${escapeHtml(operation.outcome)}</p></div></details>`;
}

function relatedFindingLinks(item) {
  const links = [
    ...(item.relatedFindings?.rest ?? []).map((id) => `<a href="#finding-${anchor(id)}">${escapeHtml(id)}</a>`),
    ...(item.relatedFindings?.downstream ?? []).map((id) => `<a href="#downstream-${anchor(id)}">${escapeHtml(id)}</a>`),
    ...(item.relatedFindings?.sharedTypeImpact ?? []).map((id) => `<a href="#downstream-${anchor(id)}">${escapeHtml(id)}</a>`),
  ];
  return links.length ? links.join(", ") : "None";
}

function semanticCard(item) {
  const all = item.operations ?? [];
  const shown = all.length > 15 ? all.slice(0, 3) : all;
  const operationContent = all.length
    ? `${all.length > 15 ? `<p><strong>Operation impact:</strong> ${all.length} REST operations are affected; 3 representative operations are shown below and ${all.length - 3} are omitted from HTML. The complete inventory is retained in assessment.json.</p>` : ""}
${shown.map((operation) => operationCard(operation)).join("\n")}`
    : '<p class="empty">No directly affected REST operation.</p>';
  return `<details class="intent" id="intent-${anchor(item.id)}"><summary><strong><span class="action">${escapeHtml(item.action)}</span> ${escapeHtml(item.title)}</strong></summary><div class="intent-body">
<p>${escapeHtml(item.summary)}</p>
${representativeExample(item)}
<h4>Affected REST operations (${all.length})</h4>
${operationContent}
<p><strong>Related findings:</strong> ${relatedFindingLinks(item)}</p>
</div></details>`;
}

function completeTypeSpecEvidence(items = []) {
  const content = items.map((item) =>
    `<section><h4>${escapeHtml(item.title)}</h4>
${renderSourceHunks(item.sources)}
<p class="sources"><strong>Sources:</strong> ${sourceLinks(item.sources)}</p></section>`,
  ).join("");
  return `<details id="complete-typespec-evidence"><summary><strong>Complete TypeSpec source evidence</strong></summary>
${content || '<p class="empty">No TypeSpec source evidence.</p>'}</details>`;
}

function downstreamOperationGroups(dimension) {
  const labels = {
    kind: "Method kind",
    parameters: "Parameters",
    responseType: "Response type",
    access: "Access",
    paging: "Paging",
    lro: "Long-running behavior",
    client: "Client",
  };
  const value = (item) =>
    typeof item === "string" ? item : JSON.stringify(item);
  const parameterSignature = (parameter) =>
    `${parameter.name}${parameter.optional ? "?" : ""}: ${parameter.type ?? "unknown"}`;
  const parameterSummary = (changes) => {
    const parts = [
      ["added", changes.added?.length],
      ["removed", changes.removed?.length],
      ["modified", changes.modified?.length],
      ["reordered", changes.reordered?.length],
      ["unchanged", changes.unchangedCount],
    ].filter(([, count]) => count);
    return parts.map(([label, count]) => `${count} ${label}`).join(", ") || "unchanged";
  };
  const parameterChanges = (changes) => [
    ...(changes.removed ?? []).map((item) =>
      `<div class="parameter-line remove"><span>-</span><code>${escapeHtml(parameterSignature(item.parameter))}</code></div>`),
    ...(changes.added ?? []).map((item) =>
      `<div class="parameter-line add"><span>+</span><code>${escapeHtml(parameterSignature(item.parameter))}</code></div>`),
    ...(changes.modified ?? []).map((item) =>
      `<div class="parameter-line modify"><span>~</span><code>${escapeHtml(item.name)}</code> ${item.changedFields.map((field) =>
        `<span class="parameter-attribute">${escapeHtml(field)}: <del>${escapeHtml(value(item.before[field]))}</del> → <ins>${escapeHtml(value(item.after[field]))}</ins></span>`,
      ).join(" ")}</div>`),
    ...(changes.reordered ?? []).map((item) =>
      `<div class="parameter-line reorder"><span>↕</span><code>${escapeHtml(item.name)}</code> position ${item.beforeIndex + 1} → ${item.afterIndex + 1}</div>`),
  ].join("");
  const groups = (dimension.operationGroups ?? []).map((group) =>
    `<article class="finding high" id="downstream-${anchor(group.id)}"><h3>${escapeHtml(group.operationId ?? group.symbol)}</h3>
<p><strong>SDK method:</strong> <code>${escapeHtml(group.symbol)}</code></p>
<p><strong>HTTP:</strong> <code>${escapeHtml((group.method ?? "").toUpperCase())} ${escapeHtml(group.path)}</code></p>
${group.deltas.filter((delta) => delta.field === "parameters").map((delta) =>
      `<div class="parameter-diff"><p><strong>Parameters:</strong> ${escapeHtml(parameterSummary(delta.changes))}</p>${parameterChanges(delta.changes)}</div>`,
    ).join("") || `<p><strong>Parameters:</strong> ${group.parametersUnchanged ? '<span class="good">unchanged</span>' : "changed"}</p>`}
${group.deltas.some((delta) => delta.field !== "parameters") ? `<table><thead><tr><th>SDK delta</th><th>Before</th><th>After</th></tr></thead><tbody>${group.deltas.filter((delta) => delta.field !== "parameters").map((delta) =>
      `<tr><td>${escapeHtml(labels[delta.field] ?? delta.rule)}</td><td><code class="delta-before">${escapeHtml(value(delta.before))}</code></td><td><code class="delta-after">${escapeHtml(value(delta.after))}</code></td></tr>`,
    ).join("")}</tbody></table>` : ""}
<p><strong>Related semantic intents:</strong> ${semanticLinks(group.relatedSemanticIntents)}</p></article>`,
  ).join("\n");
  const shared = (dimension.sharedTypeImpacts ?? []).map((impact) =>
    `<details class="panel" id="downstream-${anchor(impact.id)}"><summary><strong>Shared SDK type impact</strong> — ${impact.typeCount} types, ${impact.affectedOperationCount} REST operations, ${impact.affectedMethodCount} SDK methods</summary>
<p>${escapeHtml(impact.summary)}</p>
<p><strong>Representative types:</strong> ${impact.sampleTypes.map((item) => `<code>${escapeHtml(item)}</code>`).join(", ")}</p>
<p><strong>Representative operations/methods:</strong> ${impact.sampleMethods.map((item) => `<code>${escapeHtml(item.operationId ?? "unmapped")} / ${escapeHtml(item.symbol)}</code>`).join(", ") || "None"}</p>
<details><summary>Complete affected type and method list</summary>
<p><strong>Types:</strong> ${impact.types.map((item) => `<code>${escapeHtml(item)}</code>`).join(", ")}</p>
<p><strong>Methods:</strong> ${impact.affectedMethods.map((item) => `<code>${escapeHtml(item.operationId ?? "unmapped")} / ${escapeHtml(item.symbol)}</code>`).join(", ") || "None"}</p>
</details>
<p><strong>Related semantic intents:</strong> ${semanticLinks(impact.relatedSemanticIntents)}</p></details>`,
  ).join("\n");
  const implied = dimension.impliedByRest?.length
    ? `<div class="panel"><strong>REST breaking impact:</strong> ${dimension.impliedByRest.length} SDK method group(s) are omitted here because their REST operations are already breaking; downstream SDK impact is implied.</div>`
    : "";
  return groups || shared || implied
    ? `${implied}${groups}${shared}`
    : '<p class="empty good">No REST-compatible downstream SDK breaking changes detected.</p>';
}

function renderCurrent(assessment) {
  const { dimensions } = assessment;
  const summary = headerSummary(assessment);
  const comparisons = new Map(
    (assessment.artifactComparisons ?? []).map((item) => [item.projectId, item]),
  );
  const comparisonHeader = headerComparison(assessment, comparisons);
  const projects = assessment.projects
    .map(
      (project) => {
        const comparison = comparisons.get(project.id) ?? project.artifactComparison ?? {};
        const baselineArtifact = project.artifacts?.baseline ?? project.artifacts?.base;
        const targetArtifact = project.artifacts?.target ?? project.artifacts?.current;
        return `<tr><td><code>${escapeHtml(project.path)}</code></td><td>${escapeHtml(comparison.mode ?? "legacy")}</td><td><code>${escapeHtml(artifactLabel(comparison.baseline))}</code><br><small>${escapeHtml(comparison.baseline?.sourceRevision ?? "base")} · ${escapeHtml(comparison.baseline?.reason ?? "")}</small></td><td><code>${escapeHtml(artifactLabel(comparison.target))}</code><br><small>${escapeHtml(comparison.target?.sourceRevision ?? "current")} · ${escapeHtml(comparison.target?.reason ?? "")}</small></td><td>${escapeHtml(baselineArtifact?.autorest?.status ?? "n/a")} / ${escapeHtml(baselineArtifact?.tcgc?.status ?? "n/a")}</td><td>${escapeHtml(targetArtifact?.autorest?.status ?? "n/a")} / ${escapeHtml(targetArtifact?.tcgc?.status ?? "n/a")}</td></tr>`;
      },
    )
    .join("");
  const semantic = dimensions.semantic.items.map(semanticCard).join("\n");
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>TypeSpec Assessment</title>
<style>
:root{color-scheme:light dark;--bg:#f5f7fb;--panel:#fff;--text:#172033;--muted:#64748b;--line:#dbe3ef;--accent:#2563eb;--good:#047857;--warn:#b45309;--danger:#b91c1c;--add-bg:#dcfce7;--add-text:#166534;--remove-bg:#fee2e2;--remove-text:#991b1b}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 system-ui,sans-serif}.container{width:min(1100px,calc(100% - 32px));margin:auto}.hero{padding:38px 0 42px;background:linear-gradient(120deg,#172554,#2554d8);color:white}.hero .eyebrow{text-transform:uppercase;letter-spacing:.12em;font-size:13px;font-weight:800;color:#dbeafe}.hero h1{margin:.35em 0 .55em;font-size:clamp(34px,4.2vw,58px);line-height:1.08;letter-spacing:-.025em}.hero-meta{font-size:16px;color:#e0e7ff}.hero-meta strong{color:#86efac;text-transform:capitalize}.hero a{color:#dbeafe}.summary-grid{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:12px;margin-top:28px}.summary-card{display:block;min-height:150px;padding:18px;border:1px solid rgba(255,255,255,.2);border-radius:12px;background:rgba(255,255,255,.09);color:white!important;text-decoration:none;transition:background .15s,border-color .15s,transform .15s}.summary-card:hover,.summary-card:focus-visible{background:rgba(255,255,255,.16);border-color:rgba(255,255,255,.5);transform:translateY(-2px);outline:none}.summary-card:focus-visible{box-shadow:0 0 0 3px #93c5fd}.summary-value{display:flex;gap:10px;align-items:center;font-size:25px;font-weight:800}.summary-value .pass{color:#86efac}.summary-value .fail{color:#fecaca}.summary-label{margin-top:8px;font-size:16px;font-weight:750}.summary-detail{margin-top:7px;color:#dbeafe;font-size:13px}.notice{padding:16px 0;background:#fffbeb;color:#713f12;border-bottom:1px solid #fde68a}main{padding:28px 0}section{margin:0 0 30px}.panel,.finding,.intent,.operation{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:18px;margin:10px 0}.finding{border-left:4px solid var(--warn)}.finding.high{border-left-color:var(--danger)}.finding.low{border-left-color:var(--muted)}.intent>summary,.representative-example>summary,.operation summary{cursor:pointer}.intent>summary{font-size:18px}.intent-body{padding-top:12px}.representative-example{margin:14px 0;padding:12px;border:1px solid var(--line);border-radius:9px}.operation summary{display:flex;gap:12px;align-items:center}.operation summary span,.action{margin-left:auto;border-radius:999px;padding:2px 8px;background:#dbeafe;color:#1e40af}.operation-body{padding-top:12px}.diff{background:#111827;color:#e5e7eb;border-radius:10px;overflow:auto;margin:12px 0}.diff-path{padding:7px 12px;background:#1f2937}.diff pre{padding:12px;margin:0}.diff pre span{display:block}.diff .add{background:#123d2a;color:#bbf7d0}.diff .remove{background:#51212a;color:#fecaca}.parameter-diff{margin:12px 0}.parameter-line{display:flex;gap:8px;align-items:baseline;padding:5px 9px;margin:3px 0;border-radius:6px}.parameter-line.add,.delta-after,ins{background:var(--add-bg);color:var(--add-text)}.parameter-line.remove,.delta-before,del{background:var(--remove-bg);color:var(--remove-text)}.parameter-line.modify,.parameter-line.reorder{background:#fef3c7;color:#92400e}.parameter-line>span:first-child{font-weight:800}.parameter-attribute{white-space:nowrap}.delta-before,.delta-after,del,ins{padding:2px 5px;border-radius:4px;text-decoration:none}dl{display:grid;grid-template-columns:max-content 1fr;gap:8px 14px}dt{font-weight:700}dd{margin:0}.planned{color:var(--warn);font-weight:700}.good{color:var(--good)}.sources{color:var(--muted);font-size:13px}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:8px;border-bottom:1px solid var(--line)}code{overflow-wrap:anywhere}@media(max-width:900px){.summary-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:540px){.summary-grid{grid-template-columns:1fr}.hero h1{font-size:32px}}@media(prefers-color-scheme:dark){:root{--bg:#0f172a;--panel:#172033;--text:#e5e7eb;--muted:#9ca3af;--line:#334155;--add-bg:#123d2a;--add-text:#bbf7d0;--remove-bg:#51212a;--remove-text:#fecaca}.notice{background:#422006;color:#fde68a}.parameter-line.modify,.parameter-line.reorder{background:#422006;color:#fde68a}}
</style></head><body>
<header class="hero"><div class="container"><div class="eyebrow">TypeSpec Assessment</div><h1>${escapeHtml(headerTitle(assessment))}</h1>
<p class="hero-meta">Overall confidence: <strong>${escapeHtml(assessment.confidence)}</strong> · TypeSpec source diff: ${comparisonHeader}</p>
<div class="summary-grid">
<a class="summary-card" href="${summary.safetyTarget}"><div class="summary-value"><span class="${summary.safety === "passed" ? "pass" : summary.safety === "failed" ? "fail" : ""}">${summary.safetyIcon}</span> ${escapeHtml(summary.safetyLabel)}</div><div class="summary-label">Overall code safety</div><div class="summary-detail">REST and downstream scope</div></a>
<a class="summary-card" href="#semantic-intents"><div class="summary-value"><span>i</span> ${summary.semanticItems.length}</div><div class="summary-label">Semantic intents</div><div class="summary-detail">${summary.operationCount} operations<br>${summary.actionCounts.add} Added, ${summary.actionCounts.modify} Modified, ${summary.actionCounts.remove} Removed</div></a>
<a class="summary-card" href="#rest-breaking"><div class="summary-value"><span class="${summary.restCount ? "fail" : "pass"}">${summary.restCount ? "×" : "✓"}</span> ${summary.restCount}</div><div class="summary-label">REST breaking changes</div></a>
<a class="summary-card" href="#downstream-breaking"><div class="summary-value"><span class="${summary.downstreamMethodCount ? "fail" : "pass"}">${summary.downstreamMethodCount ? "×" : "✓"}</span> ${summary.downstreamMethodCount}</div><div class="summary-label">Downstream breaking changes</div><div class="summary-detail">${summary.downstreamFindingCount} findings</div></a>
<a class="summary-card" href="#azure-compliance"><div class="summary-value"><span>—</span></div><div class="summary-label">Azure compliance</div><div class="summary-detail">Planned / Not assessed</div></a>
</div></div></header>
<aside class="notice"><div class="container"><strong>Preview Notice.</strong> This report is a review aid, not official approval. MVP safety covers REST and language-neutral downstream SDK impact only; Compliance and Document Quality are not assessed.</div></aside>
<main class="container">
<section id="rest-breaking"><h2>REST breaking changes (${dimensions.rest.findings.length})</h2>${findingCards(dimensions.rest.findings)}</section>
<section id="downstream-breaking"><h2>REST-compatible downstream SDK breaking changes (${dimensions.downstream.operationGroups?.length ?? 0} methods)</h2>${downstreamOperationGroups(dimensions.downstream)}</section>
<section id="semantic-intents"><h2>Semantic intents (${dimensions.semantic.items.length})</h2>${semantic || '<div class="panel">No semantic review units.</div>'}</section>
<section id="azure-compliance"><h2>Azure Compliance</h2><div class="panel planned">Planned / Not assessed — ${escapeHtml(dimensions.compliance.summary)}</div></section>
<section id="document-quality"><h2>Document Quality</h2><div class="panel planned">Planned / Not assessed — ${escapeHtml(dimensions.documentQuality.summary)}</div></section>
<section id="blockers"><h2>Blockers</h2><div class="panel">${assessment.blockers.length ? `<ul>${assessment.blockers.map((blocker) => `<li>${escapeHtml(blocker.message ?? blocker)}</li>`).join("")}</ul>` : "None"}</div></section>
<section id="appendix"><h2>Appendix</h2><div class="panel"><h3 id="projects-and-compiler-status">Projects and compiler status</h3><table><thead><tr><th>Project</th><th>Mode</th><th>Baseline commit@version</th><th>Target commit@version</th><th>Baseline AutoRest / TCGC</th><th>Target AutoRest / TCGC</th></tr></thead><tbody>${projects}</tbody></table>
${completeTypeSpecEvidence(dimensions.semantic.items)}
<h3>Changed files</h3><ul>${assessment.changedFiles.map((file) => `<li><code>${escapeHtml(file.path)}</code> (${escapeHtml(file.origins.join(", "))})</li>`).join("")}</ul>
<h3>Timing and model input</h3><pre>${escapeHtml(JSON.stringify({ timings: assessment.timings, inputAccounting: assessment.inputAccounting }, null, 2))}</pre>
<p><strong>Provenance:</strong> ${Object.values(assessment.provenance).map(escapeHtml).join(", ")}</p></div></section>
</main></body></html>\n`;
}

function adaptLegacy(assessment) {
  const dimensions = assessment.dimensions;
  const source = assessment.assessmentEvidence?.changedTypeSpec ?? [];
  const toSource = (item, index) => ({
    id: `legacy-source-${index}`,
    path: item.path,
    hunks: [{}],
    declarations: [{
      source: {
        revision: item.revision === "head" ? "current" : "base",
        startLine: item.startLine,
        endLine: item.endLine,
        link: item.link,
      },
    }],
  });
  const sources = source.map(toSource);
  return {
    schemaVersion: 1,
    title: assessment.title,
    comparison: {
      baseCommit: assessment.baseline.commit,
      headCommit: assessment.head.commit,
    },
    confidence: assessment.overallConfidence ?? "medium",
    safety: { status: assessment.overallCodeSafety?.toLowerCase?.() ?? "not-assessed" },
    dimensions: {
      semantic: {
        items: dimensions.semanticUnderstanding.items.map((item) => ({
          title: item.title,
          summary: item.summary,
          operations: item.operations ?? item.affectedOperations ?? [],
          sources,
        })),
      },
      rest: { findings: dimensions.restBreakingChanges.findings },
      downstream: { findings: dimensions.restCompatibleDownstreamBreakingChanges.findings },
      compliance: { summary: "Historical result; MVP assessment is planned." },
      documentQuality: { summary: "Planned by the design document." },
    },
    blockers: assessment.errors ?? [],
    projects: (assessment.projects ?? []).map((project) => ({ path: project })),
    changedFiles: source.map((item) => ({ path: item.path, origins: ["historical"] })),
    provenance: { source: "legacy assessment adapter" },
  };
}

export function renderAssessmentHtml(assessment) {
  const errors = validateAssessment(assessment);
  if (errors.length) throw new Error(errors.join("\n"));
  return renderCurrent(assessment.schemaVersion === 1 ? assessment : adaptLegacy(assessment));
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const [input, output] = process.argv.slice(2);
    if (!input || !output) {
      throw new Error("Usage: render-assessment-html.mjs <assessment.json> <assessment.html>");
    }
    const html = renderAssessmentHtml(readJson(path.resolve(input)));
    fs.mkdirSync(path.dirname(path.resolve(output)), { recursive: true });
    fs.writeFileSync(path.resolve(output), html);
    console.log(path.resolve(output));
  });
}
