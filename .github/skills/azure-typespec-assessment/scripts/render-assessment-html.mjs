import fs from "node:fs";
import path from "node:path";
import {
  extractApiVersions,
  selectApiVersionPair,
} from "./api-version-selection.mjs";
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
      const current = source.declarations?.find(
        (item) => item.source?.revision === "current",
      );
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
  const services = [
    ...new Set(
      (assessment.projects ?? [])
        .map((project) => project.path?.split(/[\\/]/).filter(Boolean).at(-1))
        .filter(Boolean),
    ),
  ];
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
  const operationCount = new Set(
    semanticItems.flatMap((item) =>
      (item.operations ?? []).map(
        (operation) => `${operation.projectId ?? ""}:${operation.operationId}`,
      ),
    ),
  ).size;
  const restCount =
    restContractCards(assessment.dimensions.rest.findings).length +
    (assessment.dimensions.rest.legacyFindings?.length ?? 0);
  const directDownstreamCount =
    (assessment.dimensions.downstream.operationGroups?.length ?? 0) +
    downstreamTypeCards(assessment.dimensions.downstream).length +
    directLegacyDownstreamFindings(
      assessment.dimensions.downstream.legacyFindings,
    ).length;
  const downstreamCount = directDownstreamCount + restCount;
  const compliance = assessment.dimensions.compliance;
  const complianceFindingCount = compliance.legacyFindings
    ? compliance.legacyFindings.length
    : (compliance.findings?.length ?? 0);
  const complianceCoveredCount = compliance.coverage?.assessedIntentCount ?? 0;
  const complianceMaterialCount = compliance.coverage?.semanticIntentCount ?? 0;
  const codeQuality =
    assessment.safety.status === "failed" || compliance.status === "failed"
      ? "failed"
      : assessment.safety.status === "passed" && compliance.status === "passed"
        ? "passed"
        : "not-assessed";
  const codeQualityTarget =
    assessment.safety.status === "failed"
      ? restCount
        ? "#rest-breaking"
        : "#downstream-breaking"
      : compliance.status !== "passed"
        ? "#azure-compliance"
        : "#rest-breaking";
  return {
    semanticItems,
    actionCounts,
    operationCount,
    restCount,
    directDownstreamCount,
    downstreamCount,
    complianceStatus: compliance.status,
    complianceFindingCount,
    complianceCoveredCount,
    complianceMaterialCount,
    complianceCoverageDetail: compliance.legacyDocuments
      ? `${compliance.legacyDocuments.length} documents assessed`
      : `${complianceCoveredCount}/${complianceMaterialCount} intents assessed`,
    codeQuality,
    codeQualityTarget,
    codeQualityIcon:
      codeQuality === "passed" ? "✓" : codeQuality === "failed" ? "×" : "i",
    codeQualityLabel:
      codeQuality === "passed"
        ? "Passed"
        : codeQuality === "failed"
          ? "Failed"
          : "Not assessed",
  };
}

function complianceStatus(status) {
  if (status === "passed")
    return { icon: "✓", label: "Passed", className: "pass" };
  if (status === "failed")
    return { icon: "×", label: "Failed", className: "fail" };
  return { icon: "i", label: "Not assessed", className: "" };
}

function pullRequestLink(pullRequest) {
  return pullRequest?.url
    ? `<a href="${escapeHtml(pullRequest.url)}">#${escapeHtml(pullRequest.number)}</a>`
    : "Not available for this local pre-PR assessment.";
}

function complianceSourceLinks(links = []) {
  return links
    .map((source) => {
      const label = `${source.path}:${source.startLine ?? "?"}-${source.endLine ?? "?"}`;
      return source.link
        ? `<a href="${escapeHtml(source.link)}">${escapeHtml(label)}</a>`
        : `<code>${escapeHtml(label)}</code>`;
    })
    .join(", ");
}

function complianceCode(snippets = []) {
  return snippets
    .map((snippet) => {
      const lines = Array.isArray(snippet)
        ? snippet
        : (snippet.lines ?? [snippet]);
      const startLine = Number.isFinite(snippet.startLine)
        ? snippet.startLine
        : undefined;
      const label =
        snippet.caption ??
        (snippet.path
          ? `${snippet.path}${startLine ? `:${startLine}-${snippet.endLine ?? startLine}` : ""}`
          : "Changed TypeSpec");
      const heading =
        (snippet.url ?? snippet.link)
          ? `<a href="${escapeHtml(snippet.url ?? snippet.link)}">${escapeHtml(label)}</a>`
          : escapeHtml(label);
      return `<div class="diff"><div class="diff-path">${heading}</div><pre>${lines
        .map((line, index) => {
          const kind = String(line).startsWith("+")
            ? "add"
            : String(line).startsWith("-")
              ? "remove"
              : "";
          const lineNumber = startLine
            ? `<span class="line-number">${startLine + index}</span>`
            : "";
          return `<span class="${kind}">${lineNumber}${escapeHtml(line)}</span>`;
        })
        .join("\n")}</pre></div>`;
    })
    .join("");
}

function complianceComparison(comparison) {
  const label = comparison.decision.replaceAll("-", " ");
  return `<details class="compliance-comparison ${escapeHtml(comparison.decision)}">
<summary><strong>${escapeHtml(label)}</strong></summary>
<div class="compliance-comparison-body">
${comparison.expected ? `<p><strong>Expected guidance:</strong> ${escapeHtml(comparison.expected)}</p>` : ""}
<p><strong>Actual intent:</strong> ${escapeHtml(comparison.actual)}</p>
<p><strong>Assessment:</strong> ${escapeHtml(comparison.gap ?? comparison.rationale)}</p>
<p class="sources"><strong>Changed TypeSpec:</strong> ${complianceSourceLinks(comparison.sourceLinks)}</p>
${complianceCode(comparison.codeSnippets)}
</div></details>`;
}

function mostRelevantCodeSnippets(snippets, actual, limit = 2) {
  const ignored = new Set([
    "actual",
    "adds",
    "added",
    "change",
    "changed",
    "changes",
    "intent",
    "manually",
    "that",
    "their",
    "these",
    "this",
    "types",
    "using",
    "with",
  ]);
  const tokens = [
    ...new Set(
      String(actual ?? "")
        .toLowerCase()
        .match(/[a-z][a-z0-9_.@-]{3,}/g)
        ?.filter((token) => !ignored.has(token))
        .map((token) => token.replace(/[.@-]+$/, ""))
        .map((token) =>
          token.length > 5 && token.endsWith("s") ? token.slice(0, -1) : token,
        ) ?? [],
    ),
  ];
  const ranked = snippets.map((snippet, index) => {
    const code = (snippet.lines ?? []).join("\n").toLowerCase();
    return {
      snippet,
      index,
      score: tokens.reduce(
        (score, token) => score + (code.includes(token) ? 1 : 0),
        0,
      ),
    };
  });
  const relevant = ranked.filter((item) => item.score > 0);
  return (relevant.length ? relevant : ranked)
    .sort((left, right) => right.score - left.score || left.index - right.index)
    .slice(0, limit)
    .map((item) => item.snippet);
}

function complianceFindingCard(finding, context = {}) {
  const document = context.document ?? {};
  const guidance = context.guidance ?? {};
  const expectedCode =
    context.expectedCode ??
    guidance.examples?.map((example) => ({
      caption: "Documented TypeSpec example",
      url: finding.canonicalDocumentUrl ?? document.canonicalUrl,
      lines: String(example).split(/\r?\n/),
    })) ??
    [];
  const findingSources = finding.sourceLinks ?? finding.sourceReferences ?? [];
  const actual = finding.actual ?? context.actual ?? "";
  const actualCode = mostRelevantCodeSnippets(
    (finding.codeSnippets ?? []).map((snippet) => {
      const source = findingSources.find((item) => item.path === snippet.path);
      return {
        ...snippet,
        startLine: snippet.startLine ?? source?.startLine,
        endLine: snippet.endLine ?? source?.endLine,
        link: snippet.link ?? source?.link,
      };
    }),
    actual,
  );
  const guidanceTitle =
    context.guidanceTitle ?? document.title ?? "Official guidance";
  const guidanceSection = finding.section ?? context.guidanceSection ?? "";
  const guidanceUrl =
    finding.canonicalDocumentUrl ??
    finding.documentationUrl ??
    document.canonicalUrl;
  const severity = finding.severity ?? "medium";
  const relatedSemanticIntents =
    finding.relatedSemanticIntents ??
    (finding.semanticIntentId ? [finding.semanticIntentId] : []);
  return `<details class="finding compliance-finding ${escapeHtml(severity)}" id="compliance-finding-${anchor(finding.id)}">
<summary><span class="severity ${escapeHtml(severity)}">${escapeHtml(severity)}</span><strong>${escapeHtml(finding.title)}</strong></summary>
<div class="finding-body">
<p><strong>Gap:</strong> ${escapeHtml(finding.gap ?? finding.summary)}</p>
<details class="comparison-details expected-details" open><summary><strong>Expected</strong></summary><div class="comparison-body">
<p>${escapeHtml(finding.expected ?? context.expected ?? "")}</p>
${guidanceUrl ? `<p><strong>Guidance:</strong> <a href="${escapeHtml(guidanceUrl)}">${escapeHtml(guidanceTitle)}${guidanceSection ? ` — ${escapeHtml(guidanceSection)}` : ""}</a></p>` : ""}
${complianceCode(expectedCode)}
</div></details>
<details class="comparison-details actual-details" open><summary><strong>Actual</strong></summary><div class="comparison-body">
${
  actualCode.length
    ? `<h4>TypeSpec code</h4>${complianceCode(actualCode)}`
    : `<p>${escapeHtml(actual)}</p>`
}
${relatedSemanticIntents.length ? `<p><strong>Related semantic intents:</strong> ${semanticLinks(relatedSemanticIntents)}</p>` : ""}
</div></details>
</div></details>`;
}

function fetchedComplianceDocument(document) {
  return `<li><a href="${escapeHtml(document.canonicalUrl)}">${escapeHtml(document.title)}</a></li>`;
}

function complianceEvidenceAppendix(dimension, semanticItems) {
  const semanticTitles = new Map(
    semanticItems.map((item) => [item.id, item.title]),
  );
  const active = (dimension.intentAssessments ?? [])
    .map((item) => {
      const title =
        semanticTitles.get(item.semanticIntentId) ?? item.semanticIntentId;
      return `<details class="compliance-intent" id="compliance-intent-${anchor(item.semanticIntentId)}"><summary><strong><a href="#intent-${anchor(item.semanticIntentId)}">${escapeHtml(title)}</a></strong></summary>
<div class="compliance-intent-body">
<ul>${(item.documents ?? []).map(fetchedComplianceDocument).join("")}</ul>
</div></details>`;
    })
    .join("");
  const legacyDocuments = (dimension.legacyDocuments ?? [])
    .map(
      (document) =>
        `<li><a href="${escapeHtml(document.url)}">${escapeHtml(document.title)}</a> · ${escapeHtml(document.section ?? "")}<blockquote>${escapeHtml(document.guidanceExcerpt ?? "")}</blockquote></li>`,
    )
    .join("");
  const legacy = legacyDocuments
    ? `<details class="panel"><summary><strong>Official documents</strong></summary><ul>${legacyDocuments}</ul></details>`
    : "";
  return (
    `${active}${legacy}` || '<div class="panel">No guidance fetched.</div>'
  );
}

function intentTitleLinks(ids, semanticItems) {
  const titles = new Map(semanticItems.map((item) => [item.id, item.title]));
  return ids
    .map(
      (id) =>
        `<a href="#intent-${anchor(id)}">${escapeHtml(titles.get(id) ?? "Semantic intent")}</a>`,
    )
    .join(", ");
}

function renderCompliance(dimension, semanticItems) {
  const coverage = dimension.coverage ?? {};
  const activeFindings = (dimension.findings ?? [])
    .map((finding) => {
      const intent = (dimension.intentAssessments ?? []).find(
        (item) => item.semanticIntentId === finding.semanticIntentId,
      );
      const applicable = finding.applicableGuidance?.[0];
      const document = intent?.documents?.find(
        (item) => item.canonicalUrl === applicable?.canonicalDocumentUrl,
      );
      const guidance = document?.guidance?.find(
        (item) => item.section === applicable?.guidanceSection,
      );
      return complianceFindingCard(finding, { document, guidance });
    })
    .join("");
  const legacyFindings = (dimension.legacyFindings ?? [])
    .map((finding) => {
      const document = (dimension.legacyDocuments ?? []).find(
        (item) => item.url === finding.documentationUrl,
      );
      return complianceFindingCard(finding, {
        document: {
          title: document?.title,
          canonicalUrl: document?.url,
        },
        expected: document?.guidanceExcerpt,
        actual: finding.evidence?.join("; "),
        expectedCode: document?.expectedCodeSnippets ?? [],
        guidanceTitle: document?.title,
        guidanceSection: document?.section,
      });
    })
    .join("");
  const uncovered = coverage.unassessedIntentIds ?? [];
  const noApplicableGuidanceIds = (dimension.intentAssessments ?? [])
    .filter((item) => item.decision === "no-applicable-guidance")
    .map((item) => item.semanticIntentId);
  const findings = activeFindings || legacyFindings;
  const empty =
    dimension.status === "not-assessed"
      ? '<p class="empty not-assessed">Azure Guidelines could not be fully assessed.</p>'
      : noApplicableGuidanceIds.length
        ? `<p class="empty good">Azure Guidelines were assessed. ${dimension.intentAssessments.length > noApplicableGuidanceIds.length ? "They passed for the other intents, and no" : "No"} applicable guideline was found for ${noApplicableGuidanceIds.length === 1 ? "intent" : "intents"} ${intentTitleLinks(noApplicableGuidanceIds, semanticItems)}.</p>`
        : '<p class="empty good">No Azure Guidelines findings.</p>';
  return `${findings || empty}
${uncovered.length ? `<div class="panel"><strong>Compliance not assessed for:</strong> ${intentTitleLinks(uncovered, semanticItems)}</div>` : ""}`;
}

function headerComparison(assessment) {
  return `<code>${escapeHtml(assessment.comparison.baseCommit)}</code> → <code>${escapeHtml(assessment.comparison.headCommit)}</code>`;
}

function semanticLinks(ids = []) {
  return ids.length
    ? ids
        .map(
          (id) =>
            `<a href="#intent-${anchor(id)}"><code>${escapeHtml(id)}</code></a>`,
        )
        .join(", ")
    : "None";
}

function findingCards(findings, downstream = false) {
  if (!findings.length)
    return '<p class="empty good">No breaking changes detected.</p>';
  return findings
    .map(
      (
        finding,
      ) => `<details class="finding ${escapeHtml(finding.severity)}" id="finding-${anchor(finding.id)}">
<summary><span class="severity ${escapeHtml(finding.severity)}">${escapeHtml(finding.severity)}</span><strong>${escapeHtml(finding.rule)}</strong></summary>
<div class="finding-body">
${downstream ? `<p><strong>SDK symbol:</strong> <code>${escapeHtml(finding.crossLanguageDefinitionId ?? finding.symbol)}</code></p>` : ""}
<dl><dt>Actual</dt><dd>${escapeHtml(finding.actual)}</dd><dt>Expected</dt><dd>${escapeHtml(finding.expected)}</dd></dl>
<p>${escapeHtml(finding.rationale)}</p>
<p><strong>Related semantic intents:</strong> ${semanticLinks(finding.relatedSemanticIntents)}</p>
<p class="sources"><strong>Changed TypeSpec:</strong> ${sourceLinks(finding.sources)}</p>
</div></details>`,
    )
    .join("\n");
}

function schemaIdentity(schema) {
  const reference = schema?.reference?.split("/").at(-1);
  return schema?.enumMetadata?.name ?? reference;
}

function schemaDisplay(schema) {
  if (!schema) return "removed";
  const identity = schemaIdentity(schema);
  if (schema.kind === "array") return `${schemaDisplay(schema.items)}[]`;
  if (schema.kind === "enum") {
    const values = Array.isArray(schema.values)
      ? schema.values.join(" | ")
      : schema.values;
    return `${identity ?? schema.type ?? "enum"}${values ? ` { ${values} }` : ""}`;
  }
  if (identity) return identity;
  return [schema.type ?? schema.kind, schema.format].filter(Boolean).join(" ");
}

function schemaPath(schema, pathSegments) {
  let current = schema;
  let identity = schemaIdentity(current);
  for (const rawSegment of pathSegments) {
    const isArray = rawSegment.endsWith("[]");
    const name = rawSegment.replace(/\[\]$/, "");
    const property = (current?.properties ?? []).find(
      (item) => item.name === name,
    );
    if (!property) return { schema: undefined, identity };
    current = property.schema;
    identity = schemaIdentity(current) ?? identity;
    if (isArray) {
      current = current?.items;
      identity = schemaIdentity(current) ?? identity;
    }
  }
  return { schema: current, identity };
}

function operationFacts(finding) {
  const facts = finding.evidence ?? [];
  return {
    before:
      facts.find((fact) => fact.comparisonRole === "baseline") ?? facts[0],
    after: facts.find((fact) => fact.comparisonRole === "target") ?? facts[1],
  };
}

function parameterValue(operation, name) {
  const parameter = (operation?.parameters ?? []).find(
    (item) => item.name === name,
  );
  return {
    schema: parameter?.schema,
    identity: schemaIdentity(parameter?.schema),
    display: parameter
      ? `${parameter.in}:${parameter.name}${parameter.required ? " (required)" : ""} · ${schemaDisplay(parameter.schema)}`
      : "removed",
  };
}

function responseHeaderValue(operation, name) {
  for (const response of operation?.responses ?? []) {
    const header = (response.headers ?? []).find(
      (item) => item.name.toLowerCase() === name.toLowerCase(),
    );
    if (header) {
      return {
        schema: header.schema,
        identity: schemaIdentity(header.schema),
        display: `${response.status} · ${schemaDisplay(header.schema)}`,
      };
    }
  }
  return { display: "removed" };
}

function responseSchemaValue(operation, location) {
  const match = location?.match(/^response ([^.]+)(?:\.(.*))?$/);
  if (!match) return { display: "unavailable" };
  const response = (operation?.responses ?? []).find(
    (item) => item.status === match[1],
  );
  if (!response) return { display: "removed" };
  const result = schemaPath(response.schema, match[2]?.split(".") ?? []);
  return {
    ...result,
    display: schemaDisplay(result.schema),
  };
}

function restContractDelta(finding) {
  const change = finding.contractChange ?? {};
  const { before, after } = operationFacts(finding);
  const location = change.location ?? finding.rule;
  let beforeValue;
  let afterValue;
  if (
    finding.rule.startsWith("parameter-") ||
    finding.rule === "required-parameter-added"
  ) {
    beforeValue = parameterValue(before, location);
    afterValue = parameterValue(after, location);
  } else if (finding.rule.startsWith("response-header-")) {
    beforeValue = responseHeaderValue(before, location);
    afterValue = responseHeaderValue(after, location);
  } else if (location.startsWith("response ")) {
    beforeValue = responseSchemaValue(before, location);
    afterValue = responseSchemaValue(after, location);
  } else if (finding.rule === "method-changed") {
    beforeValue = { display: before?.method?.toUpperCase() };
    afterValue = { display: after?.method?.toUpperCase() };
  } else if (finding.rule === "path-changed") {
    beforeValue = { display: before?.path };
    afterValue = { display: after?.path };
  } else if (finding.rule === "operation-removed") {
    beforeValue = {
      display: `${before?.method?.toUpperCase()} ${before?.path}`,
    };
    afterValue = { display: "removed" };
  } else {
    beforeValue = { display: finding.expected };
    afterValue = { display: finding.actual };
  }
  const identity =
    beforeValue.identity ??
    afterValue.identity ??
    finding.operationIds?.[0] ??
    "Unmapped REST contract change";
  return {
    identity,
    area: location,
    before: beforeValue.display ?? "unavailable",
    after: afterValue.display ?? "unavailable",
  };
}

export function restContractCards(findings = []) {
  const cards = new Map();
  const severityOrder = { critical: 4, high: 3, medium: 2, low: 1 };
  for (const finding of findings) {
    const delta = restContractDelta(finding);
    if (!cards.has(delta.identity)) {
      cards.set(delta.identity, {
        identity: delta.identity,
        severity: finding.severity,
        findings: [],
        operations: [],
        relatedSemanticIntents: [],
        sources: [],
      });
    }
    const card = cards.get(delta.identity);
    card.findings.push({ ...finding, contractDelta: delta });
    card.relatedSemanticIntents.push(...(finding.relatedSemanticIntents ?? []));
    card.sources.push(...(finding.sources ?? []));
    const { before, after } = operationFacts(finding);
    for (const operationId of finding.operationIds ?? []) {
      const fact = [after, before].find(
        (item) => item?.operationId === operationId,
      );
      card.operations.push({
        operationId,
        apiVersion: fact?.apiVersion,
        method: fact?.method,
        path: fact?.path,
      });
    }
    if (
      (severityOrder[finding.severity] ?? 0) >
      (severityOrder[card.severity] ?? 0)
    ) {
      card.severity = finding.severity;
    }
  }
  return [...cards.values()]
    .map((card) => {
      card.operations = [
        ...new Map(
          card.operations.map((operation) => [
            `${operation.operationId}:${operation.apiVersion ?? ""}`,
            operation,
          ]),
        ).values(),
      ];
      card.relatedSemanticIntents = [...new Set(card.relatedSemanticIntents)];
      card.sources = [
        ...new Map(
          card.sources.map((source) => [source.id ?? source.path, source]),
        ).values(),
      ];
      return card;
    })
    .sort((left, right) => left.identity.localeCompare(right.identity));
}

function restContractCardId(card) {
  return `rest-contract-${anchor(card.identity)}`;
}

function renderRestContractCards(findings = []) {
  return restContractCards(findings)
    .map((card) => {
      const findingAnchors = card.findings
        .map((finding) => `<span id="finding-${anchor(finding.id)}"></span>`)
        .join("");
      const rows = card.findings
        .map(({ contractDelta }) => {
          const after =
            contractDelta.after === "removed"
              ? '<span class="not-assessed">removed</span>'
              : `<code class="delta-after">${escapeHtml(contractDelta.after)}</code>`;
          return `<tr><td><code>${escapeHtml(contractDelta.area)}</code></td><td><code class="delta-before">${escapeHtml(contractDelta.before)}</code></td><td>${after}</td></tr>`;
        })
        .join("");
      const operations = card.operations
        .map(
          (operation) =>
            `<div class="rest-operation-line"><strong><code>${escapeHtml(operation.operationId)}</code></strong><span>${escapeHtml(operation.apiVersion ?? "unversioned")}</span><code>${escapeHtml((operation.method ?? "").toUpperCase())} ${escapeHtml(operation.path ?? "")}</code></div>`,
        )
        .join("");
      const rationales = [
        ...new Set(
          card.findings.map((finding) => finding.rationale).filter(Boolean),
        ),
      ];
      return `<details class="finding ${escapeHtml(card.severity)}" id="${restContractCardId(card)}"><summary><span class="severity ${escapeHtml(card.severity)}">${escapeHtml(card.severity)}</span><strong>${escapeHtml(card.identity)}</strong><span class="tag rest-breaking-tag">REST contract</span><span class="finding-summary-meta">${card.findings.length} contract changes — ${card.operations.length} affected REST operations</span></summary>
<div class="finding-body">${findingAnchors}
<p><strong>REST contract:</strong> <code>${escapeHtml(card.identity)}</code></p>
<h4>Breaking changes</h4>
<table><thead><tr><th>Contract member</th><th>Before</th><th>After</th></tr></thead><tbody>${rows}</tbody></table>
<p><strong>Why this is breaking:</strong> ${escapeHtml(rationales[0] ?? "The existing wire contract is no longer preserved.")}</p>
<h4>Affected REST operations (${card.operations.length})</h4>
<div class="rest-operation-list">${operations || '<p class="empty">Operation mapping unavailable.</p>'}</div>
<p><strong>Related semantic intents:</strong> ${semanticLinks(card.relatedSemanticIntents)}</p>
<p class="sources"><strong>Changed TypeSpec:</strong> ${sourceLinks(card.sources)}</p>
</div></details>`;
    })
    .join("\n");
}

function legacyEvidence(evidence) {
  const details = {};
  const remaining = [];
  for (const item of evidence) {
    const parameter = item.match(
      /^(.+) changed (\d+) parameter contract\(s\)\.$/,
    );
    if (parameter) {
      details.operation = parameter[1];
      const count = Number(parameter[2]);
      details.impact = `${count} parameter contract${count === 1 ? "" : "s"} changed`;
      continue;
    }
    const response = item.match(
      /^(.+) changed an existing response contract\.$/,
    );
    if (response) {
      details.operation = response[1];
      details.impact = "Existing response contract changed";
      continue;
    }
    const request = item.match(
      /^Compared REST operation:\s*([^:]+):([A-Z]+):(.*)\.$/i,
    );
    if (request) {
      details.apiVersion = request[1];
      details.method = request[2].toUpperCase();
      details.path = request[3].startsWith("?") ? `/${request[3]}` : request[3];
      continue;
    }
    remaining.push(item);
  }
  const operationDetails = Object.keys(details).length
    ? `<dl class="legacy-operation-evidence">
${details.operation ? `<dt>Affected operation</dt><dd><code>${escapeHtml(details.operation)}</code></dd>` : ""}
${details.apiVersion ? `<dt>API version</dt><dd><code>${escapeHtml(details.apiVersion)}</code></dd>` : ""}
${details.method ? `<dt>HTTP request</dt><dd><code>${escapeHtml(details.method)} ${escapeHtml(details.path)}</code></dd>` : ""}
${details.impact ? `<dt>Contract impact</dt><dd>${escapeHtml(details.impact)}</dd>` : ""}
</dl>`
    : "";
  const otherEvidence = remaining.length
    ? `<p><strong>Evidence:</strong></p><ul>${remaining.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}</ul>`
    : "";
  return `${operationDetails}${otherEvidence}`;
}

function legacyFindingCards(findings = [], downstream = false) {
  return findings
    .map((finding) => {
      const omittedRestEvidence =
        downstream &&
        finding.evidence?.some((item) => /^Approved REST finding:/i.test(item));
      const evidence = (finding.evidence ?? []).filter(
        (item) => !downstream || !/^Approved REST finding:/i.test(item),
      );
      const summary = downstream
        ? finding.summary.replace(
            /^The approved REST contract changes/i,
            "The REST breaking changes",
          )
        : finding.summary;
      return `<details class="finding ${escapeHtml(finding.severity)}" id="${downstream ? "downstream" : "finding"}-${anchor(finding.id)}">
<summary><span class="severity ${escapeHtml(finding.severity)}">${escapeHtml(finding.severity)}</span><strong>${escapeHtml(finding.title)}</strong></summary>
<div class="finding-body">
<p>${escapeHtml(summary)}</p>
${legacyEvidence(evidence)}
${omittedRestEvidence ? '<p class="sources">REST finding details are omitted here; see <a href="#rest-breaking">REST breaking changes</a>.</p>' : ""}
<p><strong>Related semantic intents:</strong> ${semanticLinks(finding.relatedSemanticIntents)}</p>
${downstream ? "" : `<p class="sources"><strong>Changed TypeSpec:</strong> ${sourceLinks(finding.sources)}</p>`}
</div></details>`;
    })
    .join("\n");
}

function directLegacyDownstreamFindings(findings = []) {
  return findings.filter(
    (finding) =>
      !finding.evidence?.some((item) => /^Approved REST finding:/i.test(item)),
  );
}

export function visibleSharedTypeImpacts(impacts = []) {
  return impacts.filter(
    (impact) =>
      (impact.findingIds?.length ?? 0) > 0 ||
      (impact.affectedOperationCount ?? 0) > 0 ||
      (impact.affectedMethodCount ?? 0) > 0,
  );
}

export function relatedImpactOperations(impact, semanticItems = []) {
  const relatedIntentIds = new Set(impact.relatedSemanticIntents ?? []);
  const operations = semanticItems
    .filter((item) => relatedIntentIds.has(item.id))
    .flatMap((item) => item.operations ?? []);
  const unique = new Map();
  for (const operation of operations) {
    const key = `${operation.operationId}:${operation.apiVersion ?? ""}`;
    if (!unique.has(key)) unique.set(key, operation);
  }
  return [...unique.values()];
}

export function downstreamTypeCards(dimension = {}) {
  const findingsById = new Map(
    (dimension.findings ?? []).map((finding) => [finding.id, finding]),
  );
  const cards = new Map();
  for (const impact of visibleSharedTypeImpacts(dimension.sharedTypeImpacts)) {
    const types = [...new Set(impact.types ?? [])].sort();
    for (const [index, type] of types.entries()) {
      if (!cards.has(type)) {
        cards.set(type, {
          type,
          findings: [],
          relatedSemanticIntents: [],
          affectedMethods: [],
          rootCauses: [],
          legacyImpactIds: [],
        });
      }
      const card = cards.get(type);
      const matchedFindings = (impact.findingIds ?? [])
        .map((id) => findingsById.get(id))
        .filter((finding) => finding?.crossLanguageDefinitionId === type);
      card.findings.push(...matchedFindings);
      card.relatedSemanticIntents.push(
        ...matchedFindings.flatMap(
          (finding) => finding.relatedSemanticIntents ?? [],
        ),
      );
      if (types.length === 1 && !matchedFindings.length) {
        card.relatedSemanticIntents.push(
          ...(impact.relatedSemanticIntents ?? []),
        );
      }
      card.affectedMethods.push(...(impact.affectedMethods ?? []));
      card.rootCauses.push({
        id: impact.rootCauseId,
        kind: impact.rootCause,
        summary: impact.summary,
      });
      if (index === 0) card.legacyImpactIds.push(impact.id);
    }
  }
  const severityOrder = { critical: 4, high: 3, medium: 2, low: 1 };
  return [...cards.values()]
    .map((card) => {
      card.findings = [
        ...new Map(
          card.findings.map((finding) => [finding.id, finding]),
        ).values(),
      ];
      card.relatedSemanticIntents = [...new Set(card.relatedSemanticIntents)];
      card.affectedMethods = [
        ...new Map(
          card.affectedMethods.map((method) => [method.symbol, method]),
        ).values(),
      ];
      card.rootCauses = [
        ...new Map(
          card.rootCauses.map((rootCause) => [rootCause.id, rootCause]),
        ).values(),
      ];
      card.legacyImpactIds = [...new Set(card.legacyImpactIds)];
      card.severity = card.findings.reduce(
        (highest, finding) =>
          (severityOrder[finding.severity] ?? 0) > (severityOrder[highest] ?? 0)
            ? finding.severity
            : highest,
        "high",
      );
      return card;
    })
    .sort((left, right) => left.type.localeCompare(right.type));
}

function contractSummary(value, field) {
  if (value === undefined) return "—";
  if (field === "parameters") {
    return (
      (value ?? [])
        .map(
          (item) =>
            `${item.in ?? "body"}:${item.name}${item.required ? " (required)" : ""}`,
        )
        .join(", ") || "None"
    );
  }
  if (field === "responses") {
    return (value ?? []).map((item) => item.status).join(", ") || "None";
  }
  if (field === "request") return value ? (value.kind ?? "present") : "None";
  if (field === "paging" || field === "lro") return value ? "present" : "None";
  return String(value);
}

function renderSourceHunks(sources = []) {
  return sources
    .flatMap((source) =>
      (source.hunks ?? []).map(
        (hunk) =>
          `<div class="diff"><div class="diff-path">${escapeHtml(source.path)}</div><pre>${(
            hunk.lines ?? []
          )
            .map((line) => {
              const kind = line.startsWith("+")
                ? "add"
                : line.startsWith("-")
                  ? "remove"
                  : "context";
              return `<span class="${kind}">${escapeHtml(line)}</span>`;
            })
            .join("\n")}</pre></div>`,
      ),
    )
    .join("");
}

function substantiveChange(hunk) {
  return (hunk.lines ?? []).some((line) => {
    if (!line.startsWith("+") && !line.startsWith("-")) return false;
    const code = line.slice(1).trim();
    return code && !/^import\s/.test(code) && !/^using\s/.test(code);
  });
}

function sourceStartLine(source, hunk) {
  const hunkStartLine = hunk.current?.startLine ?? hunk.base?.startLine;
  if (Number.isFinite(hunkStartLine)) return hunkStartLine;
  const declarationLines = (source.declarations ?? [])
    .flatMap((declaration) =>
      declaration.hunkIds?.includes(hunk.id)
        ? [declaration.source?.startLine]
        : [],
    )
    .filter(Number.isFinite);
  return Math.min(...declarationLines, Number.MAX_SAFE_INTEGER);
}

export function representativeSource(item) {
  const operationHunkIds = new Set(
    (item.operations ?? []).flatMap((operation) =>
      (operation.sources ?? []).flatMap((source) =>
        (source.hunks ?? []).map((hunk) => hunk.id),
      ),
    ),
  );
  const candidates = (item.sources ?? []).flatMap((source) =>
    (source.hunks ?? []).map((hunk) => {
      const declaration = (source.declarations ?? []).some((item) =>
        item.hunkIds?.includes(hunk.id),
      );
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
    }),
  );
  candidates.sort((left, right) => {
    for (let index = 0; index < left.score.length; index += 1) {
      if (left.score[index] !== right.score[index]) {
        return right.score[index] - left.score[index];
      }
    }
    return (
      String(left.source.path ?? "").localeCompare(
        String(right.source.path ?? ""),
      ) ||
      left.startLine - right.startLine ||
      String(left.hunk.id ?? "").localeCompare(String(right.hunk.id ?? ""))
    );
  });
  if (!candidates.length) return undefined;
  const selected = candidates[0];
  return {
    ...selected.source,
    hunks: [selected.hunk],
    declarations: (selected.source.declarations ?? []).filter((declaration) =>
      declaration.hunkIds?.includes(selected.hunk.id),
    ),
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
        .map(
          (field) =>
            `<tr><td>${escapeHtml(field)}</td><td>${escapeHtml(contractSummary(operation.before?.[field], field))}</td><td>${escapeHtml(contractSummary(operation.after?.[field], field))}</td></tr>`,
        )
        .join("")}</tbody></table>`
    : `<p class="good"><strong>${escapeHtml(operation.outcome)}</strong></p>`;
  return `<details class="operation"><summary><strong>${escapeHtml(operation.operationId)}</strong> <code>${escapeHtml((operation.method ?? "").toUpperCase())} ${escapeHtml(operation.path)}</code> <span>${escapeHtml(operation.apiVersion)}</span></summary>
<div class="operation-body">${changes}
<p><strong>Change outcome:</strong> ${escapeHtml(operation.outcome)}</p></div></details>`;
}

function semanticFindingReferences(item, compliance) {
  const complianceIds = new Set([
    ...(item.relatedFindings?.compliance ?? []),
    ...(compliance.findings ?? [])
      .filter(
        (finding) =>
          finding.semanticIntentId === item.id ||
          finding.relatedSemanticIntents?.includes(item.id),
      )
      .map((finding) => finding.id),
  ]);
  const downstreamIds = new Set([
    ...(item.relatedFindings?.downstream ?? []),
    ...(item.relatedFindings?.sharedTypeImpact ?? []),
  ]);
  return [
    ...(item.relatedFindings?.rest ?? []).map((id) => ({
      id,
      kind: "rest",
      href: `#finding-${anchor(id)}`,
    })),
    ...[...downstreamIds].map((id) => ({
      id,
      kind: "downstream",
      href: `#downstream-${anchor(id)}`,
    })),
    ...[...complianceIds].map((id) => ({
      id,
      kind: "compliance",
      href: `#compliance-finding-${anchor(id)}`,
    })),
  ];
}

function relatedFindingLinks(references) {
  return references.length
    ? references
        .map(({ href, id }) => `<a href="${href}">${escapeHtml(id)}</a>`)
        .join(", ")
    : "None";
}

function semanticFindingBadge(references) {
  const labels = {
    rest: "REST breaking changes",
    downstream: "Downstream breaking changes",
    compliance: "Azure Guidelines",
  };
  const kinds = [...new Set(references.map(({ kind }) => kind))];
  return kinds.length
    ? `<span class="intent-finding-badges">${kinds
        .map((kind) => {
          const target = references.find(
            (reference) => reference.kind === kind,
          );
          return `<a class="intent-finding-badge ${kind}" href="${target.href}">${labels[kind]}</a>`;
        })
        .join("")}</span>`
    : "";
}

function deterministicCoverageSummary(item) {
  const coverage = item.deterministicCoverage;
  if (!coverage) return "";
  const total =
    coverage.classifications?.length ??
    coverage.coveredHunkIds.length + coverage.uncoveredHunkIds.length;
  const inferred = item.inferenceResults?.length ?? 0;
  const inference = item.inferenceRequired
    ? `AI inference used for ${inferred} request${inferred === 1 ? "" : "s"}.`
    : "AI inference skipped.";
  return `<p><strong>Deterministic coverage:</strong> ${coverage.coveredHunkIds.length} of ${total} changed hunks classified. ${escapeHtml(inference)}</p>`;
}

function semanticCard(item, compliance) {
  const all = item.operations ?? [];
  const shown = all.slice(0, 3);
  const operationContent = all.length
    ? `<p><strong>Operation impact:</strong> ${all.length} REST operations are affected; ${shown.length} representative operation(s) are shown below${all.length > shown.length ? ` and ${all.length - shown.length} are omitted from HTML` : ""}. The complete inventory is retained in assessment.json.</p>
${shown.map((operation) => operationCard(operation)).join("\n")}`
    : '<p class="empty">No directly affected REST operation.</p>';
  const findingReferences = semanticFindingReferences(item, compliance);
  return `<details class="intent" id="intent-${anchor(item.id)}"><summary><strong><span class="action">${escapeHtml(item.action)}</span> ${escapeHtml(item.title)}</strong>${semanticFindingBadge(findingReferences)}</summary><div class="intent-body">
<p>${escapeHtml(item.summary)}</p>
${deterministicCoverageSummary(item)}
${representativeExample(item)}
<h4>Affected REST operations (${all.length})</h4>
${operationContent}
<p id="related-findings-${anchor(item.id)}"><strong>Related findings:</strong> ${relatedFindingLinks(findingReferences)}</p>
</div></details>`;
}

function completeTypeSpecEvidence(items = []) {
  const content = items
    .map(
      (item) =>
        `<section><h4>${escapeHtml(item.title)}</h4>
${renderSourceHunks(item.sources)}
<p class="sources"><strong>Sources:</strong> ${sourceLinks(item.sources)}</p></section>`,
    )
    .join("");
  return `<details id="complete-typespec-evidence"><summary><strong>Complete TypeSpec source evidence</strong></summary>
${content || '<p class="empty">No TypeSpec source evidence.</p>'}</details>`;
}

function downstreamOperationGroups(dimension, semanticItems) {
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
    return (
      parts.map(([label, count]) => `${count} ${label}`).join(", ") ||
      "unchanged"
    );
  };
  const contractCell = (cellValue, detail, kind) =>
    `<code class="contract-value ${kind}">${escapeHtml(cellValue)}</code>${detail ? `<span class="contract-detail">${escapeHtml(detail)}</span>` : ""}`;
  const parameterType = (parameter) =>
    `${parameter.type ?? "unknown"}${parameter.optional ? "?" : ""}`;
  const methodContractRows = (group) =>
    group.deltas.flatMap((delta) => {
      if (delta.field !== "parameters") {
        return [
          {
            member: labels[delta.field] ?? delta.rule,
            before: value(delta.before),
            after: value(delta.after),
          },
        ];
      }
      return [
        ...(delta.changes.removed ?? []).map((item) => ({
          member: item.parameter.name,
          before: parameterType(item.parameter),
          beforeDetail: "Existing method parameter",
          after: "not present",
        })),
        ...(delta.changes.added ?? []).map((item) => ({
          member: item.parameter.name,
          before: "not present",
          beforeDetail: "Existing generated method signature",
          after: parameterType(item.parameter),
          afterDetail: item.parameter.optional
            ? "Optional method parameter"
            : "Required method parameter",
        })),
        ...(delta.changes.modified ?? []).map((item) => ({
          member: item.name,
          before: parameterType(item.before),
          beforeDetail: `Changed: ${item.changedFields.join(", ")}`,
          after: parameterType(item.after),
          afterDetail: `Changed: ${item.changedFields.join(", ")}`,
        })),
        ...(delta.changes.reordered ?? []).map((item) => ({
          member: item.name,
          before: `position ${item.beforeIndex + 1}`,
          after: `position ${item.afterIndex + 1}`,
        })),
      ];
    });
  const severityOrder = { critical: 4, high: 3, medium: 2, low: 1 };
  const groups = (dimension.operationGroups ?? [])
    .map((group) => {
      const rows = methodContractRows(group);
      const severity = group.deltas.reduce(
        (highest, delta) =>
          (severityOrder[delta.severity] ?? 0) > (severityOrder[highest] ?? 0)
            ? delta.severity
            : highest,
        "low",
      );
      const parameterDelta = group.deltas.find(
        (delta) => delta.field === "parameters",
      );
      const addedParameters = parameterDelta?.changes.added ?? [];
      const changeSummary =
        group.deltas.length === 1 &&
        addedParameters.length === 1 &&
        rows.length === 1
          ? `An ${addedParameters[0].parameter.optional ? "optional" : "required"} parameter was added to the generated public method signature`
          : `${rows.length} generated SDK method contract ${rows.length === 1 ? "member changed" : "members changed"}`;
      const rationale = [
        ...new Set(
          group.deltas.map((delta) => delta.rationale).filter(Boolean),
        ),
      ].join(" ");
      const parameterDetail = parameterDelta
        ? parameterSummary(parameterDelta.changes)
        : group.parametersUnchanged
          ? "unchanged"
          : "changed";
      return `<details class="finding sdk-method-card ${escapeHtml(severity)}" id="downstream-${anchor(group.id)}"><summary><span class="severity ${escapeHtml(severity)}">${escapeHtml(severity)}</span><strong>${escapeHtml(group.operationId ?? group.symbol)}</strong><span class="contract-tag">SDK method</span><span class="finding-summary">${rows.length} SDK contract ${rows.length === 1 ? "change" : "changes"}</span></summary>
<div class="finding-body">
<dl class="contract-metadata"><dt>SDK method:</dt><dd><code>${escapeHtml(group.symbol)}</code></dd><dt>HTTP:</dt><dd><span class="http-contract"><span class="http-method">${escapeHtml((group.method ?? "").toUpperCase())}</span><code>${escapeHtml(group.path)}</code></span></dd><dt>Change:</dt><dd>${escapeHtml(changeSummary)}</dd></dl>
<h4>Breaking changes</h4>
<table class="contract-change-table"><thead><tr><th>SDK method member</th><th>Before</th><th>After</th></tr></thead><tbody>${rows
        .map(
          (row) =>
            `<tr><td class="contract-member"><code>${escapeHtml(row.member)}</code></td><td class="contract-before">${contractCell(row.before, row.beforeDetail, "before")}</td><td class="contract-after">${contractCell(row.after, row.afterDetail, "after")}</td></tr>`,
        )
        .join("")}</tbody></table>
${rationale ? `<div class="breaking-rationale"><strong>Why this is breaking:</strong> ${escapeHtml(rationale)}</div>` : ""}
<div class="contract-footer"><span><strong>Parameters:</strong> ${escapeHtml(parameterDetail)}</span><span><strong>Related semantic intents:</strong> ${semanticLinks(group.relatedSemanticIntents)}</span></div>
</div></details>`;
    })
    .join("\n");
  const groupedFindingIds = new Set(
    (dimension.operationGroups ?? []).flatMap((group) =>
      group.deltas.map((delta) => delta.findingId),
    ),
  );
  const shortTypeName = (type) => type.split(".").at(-1) ?? type;
  const enumShape = (fact) => {
    if (!fact) return "unknown";
    if (fact.isFixed) return "fixed enum";
    if (fact.isUnionAsEnum) return "extensible enum";
    return "enum";
  };
  const enumContractRows = (card) => {
    const enumFinding = card.findings.find((finding) =>
      finding.evidence?.some((fact) => fact.factKind === "enum"),
    );
    if (!enumFinding) return [];
    const before = enumFinding.evidence.find(
      (fact) => fact.factKind === "enum" && fact.comparisonRole === "baseline",
    );
    const after = enumFinding.evidence.find(
      (fact) => fact.factKind === "enum" && fact.comparisonRole === "target",
    );
    if (!before || !after) return [];

    const rows = [];
    if (enumShape(before) !== enumShape(after)) {
      rows.push({
        kind: "shape",
        member: shortTypeName(card.type),
        before: enumShape(before),
        beforeDetail: before.isFixed
          ? "Only declared values are represented."
          : undefined,
        after: enumShape(after),
        afterDetail: after.isUnionAsEnum
          ? "Unknown service values are accepted."
          : undefined,
      });
    }
    const beforeByValue = new Map(
      (before.values ?? []).map((item) => [String(item.value), item]),
    );
    const afterByValue = new Map(
      (after.values ?? []).map((item) => [String(item.value), item]),
    );
    for (const [wireValue, previous] of beforeByValue) {
      const current = afterByValue.get(wireValue);
      if (current && current.name !== previous.name) {
        rows.push({
          kind: "member",
          member: `${shortTypeName(card.type)}.${previous.name}`,
          before: previous.name,
          beforeDetail: "Generated public member identity",
          after: current.name,
          afterDetail: `Wire value remains ${JSON.stringify(wireValue)}.`,
        });
      } else if (!current) {
        rows.push({
          kind: "member",
          member: `${shortTypeName(card.type)}.${previous.name}`,
          before: previous.name,
          beforeDetail: `Wire value ${JSON.stringify(wireValue)}.`,
          after: "removed",
        });
      }
    }
    return rows;
  };
  const genericContractRows = (card, findings) =>
    findings.map((finding) => ({
      member: finding.rule,
      before: finding.expected,
      after: finding.actual,
    }));
  const affectedOperations = (operations) =>
    operations.length
      ? `<details class="affected-operations"><summary><strong>Affected REST operations (${operations.length})</strong></summary>
<div class="affected-operation-list">${operations
          .map(
            (operation) =>
              `<div class="affected-operation"><strong>${escapeHtml(operation.operationId)}</strong><span class="http-method">${escapeHtml((operation.method ?? "").toUpperCase())}</span><code>${escapeHtml(operation.path ?? "Path unavailable")}</code></div>`,
          )
          .join("")}</div></details>`
      : '<p class="mapping-unavailable"><strong>Affected REST operations (0):</strong> mapping unavailable</p>';
  const typeCards = downstreamTypeCards(dimension)
    .map((card) => {
      const relatedOperations = relatedImpactOperations(card, semanticItems);
      const directFindings = card.findings.filter(
        (finding) => !groupedFindingIds.has(finding.id),
      );
      const enumRows = enumContractRows(card);
      const contractRows = enumRows.length
        ? enumRows
        : genericContractRows(card, directFindings);
      const shapeRow = enumRows.find((row) => row.kind === "shape");
      const memberChangeCount = enumRows.filter(
        (row) => row.kind === "member",
      ).length;
      const changeSummary = shapeRow
        ? `${shapeRow.before} changed to ${/^[aeiou]/i.test(shapeRow.after) ? "an" : "a"} ${shapeRow.after}${memberChangeCount ? `, with ${memberChangeCount} generated member ${memberChangeCount === 1 ? "renamed" : "changes"}` : ""}`
        : memberChangeCount
          ? `${memberChangeCount} generated member ${memberChangeCount === 1 ? "changed" : "changes"}`
          : directFindings.map((finding) => finding.rule).join(", ");
      const rationale = [
        ...new Set(
          directFindings.map((finding) => finding.rationale).filter(Boolean),
        ),
      ].join(" ");
      const legacyAnchors = card.legacyImpactIds
        .map((id) => `<span id="downstream-${anchor(id)}"></span>`)
        .join("");
      return `<details class="finding sdk-contract-card ${escapeHtml(card.severity)}" id="downstream-type-${anchor(card.type)}"><summary><span class="severity ${escapeHtml(card.severity)}">${escapeHtml(card.severity)}</span><strong>${escapeHtml(shortTypeName(card.type))}</strong><span class="contract-tag">SDK type</span></summary>
<div class="finding-body">${legacyAnchors}
<dl class="contract-metadata"><dt>SDK contract:</dt><dd><code>${escapeHtml(card.type)}</code></dd><dt>Change:</dt><dd>${escapeHtml(changeSummary)}</dd></dl>
<h4>Breaking changes</h4>
<table class="contract-change-table"><thead><tr><th>SDK contract member</th><th>Before</th><th>After</th></tr></thead><tbody>${contractRows
        .map(
          (row) =>
            `<tr><td class="contract-member"><code>${escapeHtml(row.member)}</code></td><td class="contract-before">${contractCell(row.before, row.beforeDetail, "before")}</td><td class="contract-after">${contractCell(row.after, row.afterDetail, "after")}</td></tr>`,
        )
        .join("")}</tbody></table>
${rationale ? `<div class="breaking-rationale"><strong>Why this is breaking:</strong> ${escapeHtml(rationale)}</div>` : ""}
${affectedOperations(relatedOperations)}
<div class="contract-footer"><span><strong>Related semantic intents:</strong> ${semanticLinks(card.relatedSemanticIntents)}</span></div>
</div></details>`;
    })
    .join("\n");
  const legacy = legacyFindingCards(
    directLegacyDownstreamFindings(dimension.legacyFindings),
    true,
  );
  return `${groups}${typeCards}${legacy}`;
}

function downstreamRestItems(rest) {
  const current = restContractCards(rest.findings).map((card) => ({
    title: card.identity,
    target: `#${restContractCardId(card)}`,
  }));
  const legacy = (rest.legacyFindings ?? []).map((finding) => ({
    title: finding.title,
    target: `#finding-${anchor(finding.id)}`,
  }));
  return [...current, ...legacy]
    .map(({ title, target }) => {
      return `<div class="panel downstream-rest-item"><a class="downstream-rest-link" href="${target}"><span class="origin-tag rest-breaking-tag">REST breaking</span><strong>${escapeHtml(title)}</strong></a></div>`;
    })
    .join("\n");
}

function renderCurrent(assessment) {
  const { dimensions } = assessment;
  const summary = headerSummary(assessment);
  const comparisons = new Map(
    (assessment.artifactComparisons ?? []).map((item) => [
      item.projectId,
      item,
    ]),
  );
  const comparisonHeader = headerComparison(assessment);
  const projects = assessment.projects
    .map((project) => {
      const comparison =
        comparisons.get(project.id) ?? project.artifactComparison ?? {};
      const baselineArtifact =
        project.artifacts?.baseline ?? project.artifacts?.base;
      const targetArtifact =
        project.artifacts?.target ?? project.artifacts?.current;
      return `<tr><td><code>${escapeHtml(project.path)}</code></td><td>${escapeHtml(comparison.mode ?? "legacy")}</td><td><code>${escapeHtml(artifactLabel(comparison.baseline))}</code><br><small>${escapeHtml(comparison.baseline?.sourceRevision ?? "base")} · ${escapeHtml(comparison.baseline?.reason ?? "")}</small></td><td><code>${escapeHtml(artifactLabel(comparison.target))}</code><br><small>${escapeHtml(comparison.target?.sourceRevision ?? "current")} · ${escapeHtml(comparison.target?.reason ?? "")}</small></td><td>${escapeHtml(baselineArtifact?.autorest?.status ?? "n/a")} / ${escapeHtml(baselineArtifact?.tcgc?.status ?? "n/a")}</td><td>${escapeHtml(targetArtifact?.autorest?.status ?? "n/a")} / ${escapeHtml(targetArtifact?.tcgc?.status ?? "n/a")}</td></tr>`;
    })
    .join("");
  const semantic = dimensions.semantic.items
    .map((item, index) => ({
      item,
      index,
      hasFindings:
        semanticFindingReferences(item, dimensions.compliance).length > 0,
    }))
    .sort(
      (left, right) =>
        Number(right.hasFindings) - Number(left.hasFindings) ||
        left.index - right.index,
    )
    .map(({ item }) => semanticCard(item, dimensions.compliance))
    .join("\n");
  const rest =
    [
      renderRestContractCards(dimensions.rest.findings),
      legacyFindingCards(dimensions.rest.legacyFindings),
    ]
      .filter((content, index) =>
        index > 0 ? content : dimensions.rest.findings?.length,
      )
      .join("\n") || '<p class="empty good">No breaking changes detected.</p>';
  const downstreamItems =
    [
      downstreamRestItems(dimensions.rest),
      downstreamOperationGroups(
        dimensions.downstream,
        dimensions.semantic.items,
      ),
    ]
      .filter(Boolean)
      .join("\n") ||
    '<p class="empty good">No downstream breaking changes detected.</p>';
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>TypeSpec Assessment</title>
<style>
:root{color-scheme:light dark;--bg:#f5f7fb;--panel:#fff;--text:#172033;--muted:#64748b;--line:#dbe3ef;--accent:#2563eb;--good:#047857;--warn:#b45309;--danger:#b91c1c;--add-bg:#dcfce7;--add-text:#166534;--remove-bg:#fee2e2;--remove-text:#991b1b}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 system-ui,sans-serif}.container{width:min(1100px,calc(100% - 32px));margin:auto}.hero{padding:38px 0 42px;background:linear-gradient(120deg,#172554,#2554d8);color:white}.hero .eyebrow{text-transform:uppercase;letter-spacing:.12em;font-size:13px;font-weight:800;color:#dbeafe}.hero h1{margin:.35em 0 .55em;font-size:clamp(34px,4.2vw,58px);line-height:1.08;letter-spacing:-.025em}.hero-meta{font-size:16px;color:#e0e7ff}.hero-meta strong{color:#86efac;text-transform:capitalize}.hero a{color:#dbeafe}.summary-grid{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:12px;margin-top:28px}.summary-card{display:block;min-height:150px;padding:18px;border:1px solid rgba(255,255,255,.2);border-radius:12px;background:rgba(255,255,255,.09);color:white!important;text-decoration:none;transition:background .15s,border-color .15s,transform .15s}.summary-card:hover,.summary-card:focus-visible{background:rgba(255,255,255,.16);border-color:rgba(255,255,255,.5);transform:translateY(-2px);outline:none}.summary-card:focus-visible{box-shadow:0 0 0 3px #93c5fd}.summary-value{display:flex;gap:10px;align-items:center;font-size:25px;font-weight:800}.summary-value .pass{color:#86efac}.summary-value .fail{color:#fecaca}.summary-label{margin-top:8px;font-size:16px;font-weight:750}.summary-detail{margin-top:7px;color:#dbeafe;font-size:13px}.notice{padding:16px 0;background:#fffbeb;color:#713f12;border-bottom:1px solid #fde68a}main{padding:28px 0}section{margin:0 0 30px}.dimension-details>summary{cursor:pointer;list-style-position:outside}.dimension-details>summary h2{display:inline-block;margin:0 0 12px}.panel,.finding,.intent,.operation,.compliance-intent,.compliance-comparison{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:18px;margin:10px 0}.finding{border-left:4px solid var(--warn)}.finding.high,.compliance-comparison.applicable-fail{border-left:4px solid var(--danger)}.finding.low{border-left-color:var(--muted)}.intent>summary{display:flex;align-items:center;gap:10px;flex-wrap:wrap}.intent>summary .action{margin-left:0}.intent-finding-badges{display:inline-flex;gap:7px;flex-wrap:wrap;margin-left:auto}.intent-finding-badge{border:1px solid #991b1b;border-radius:999px;padding:3px 10px;background:#b91c1c;color:#fff!important;font-size:13px;font-weight:800;text-decoration:none}.intent-finding-badge:hover,.intent-finding-badge:focus-visible{background:#7f1d1d;outline:2px solid #fca5a5;outline-offset:2px}.intent>summary,.representative-example>summary,.operation summary,.compliance-intent>summary,.compliance-comparison>summary,.finding>summary,.comparison-details>summary,.affected-operations>summary,.root-cause-provenance>summary{cursor:pointer}.intent>summary,.compliance-intent>summary{font-size:18px}.intent-body,.compliance-intent-body,.compliance-comparison-body{padding-top:12px}.finding>summary{display:flex;align-items:center;gap:12px;font-size:18px}.sdk-contract-card>summary,.sdk-method-card>summary{flex-wrap:wrap}.finding-summary{margin-left:auto;color:var(--muted);font-size:15px}.severity,.origin-tag,.contract-tag{border-radius:999px;padding:3px 10px;font-size:13px;font-weight:800}.contract-tag{background:#fee2e2;color:#991b1b}.severity{text-transform:lowercase}.severity.high{background:#fee2e2;color:#991b1b}.severity.medium{background:#fef3c7;color:#92400e}.severity.low{background:#dbeafe;color:#1e40af}.origin-tag.rest-breaking-tag{background:#fee2e2;color:#991b1b}.downstream-rest-link{display:flex;align-items:center;gap:12px;color:inherit;text-decoration:none}.downstream-rest-link:hover strong,.downstream-rest-link:focus-visible strong{text-decoration:underline}.finding-body{padding-top:12px}.comparison-details{margin:14px 0;padding:12px 14px;border:1px solid var(--line);border-radius:10px;background:color-mix(in srgb,var(--panel) 94%,var(--accent))}.comparison-details>summary{font-size:16px}.comparison-body{padding-top:12px}.contract-metadata{grid-template-columns:max-content minmax(0,1fr);margin:4px 0 22px}.http-contract{display:inline-flex;align-items:baseline;gap:8px;flex-wrap:wrap}.contract-change-table{border:1px solid var(--line);border-radius:10px;border-collapse:separate;border-spacing:0;overflow:hidden}.contract-change-table th{background:#eaf0f9}.contract-change-table th,.contract-change-table td{padding:13px 15px;border-right:1px solid var(--line)}.contract-change-table th:last-child,.contract-change-table td:last-child{border-right:0}.contract-member{width:31%;background:color-mix(in srgb,#eaf0f9 70%,var(--panel))}.contract-before{width:34.5%;background:#fff1f1}.contract-after{width:34.5%;background:#eefaf3}.contract-value{display:inline-block;padding:3px 7px;border-radius:5px;font-weight:700}.contract-value.before{background:var(--remove-bg);color:var(--remove-text)}.contract-value.after{background:var(--add-bg);color:var(--add-text)}.contract-detail{display:block;margin-top:4px;color:var(--muted);font-size:13px}.breaking-rationale{margin:17px 0 22px;padding:12px 14px;border-left:3px solid #e7a400;background:#fff9e8}.affected-operations{margin:20px 0}.affected-operations>summary{font-size:18px}.affected-operation-list{display:grid;gap:7px;margin-top:10px}.affected-operation{display:grid;grid-template-columns:minmax(230px,.8fr) 65px minmax(300px,1.8fr);align-items:center;gap:12px;padding:9px 12px;border:1px solid var(--line);border-radius:8px}.http-method{color:#075cab;font-family:ui-monospace,monospace;font-weight:800}.contract-footer{display:flex;flex-wrap:wrap;gap:10px 24px;margin-top:18px;color:var(--muted);font-size:13px}.root-cause-provenance{margin-top:14px}.mapping-unavailable{color:var(--muted)}.representative-example{margin:14px 0;padding:12px;border:1px solid var(--line);border-radius:9px}.operation summary{display:flex;gap:12px;align-items:center}.operation summary span,.action{margin-left:auto;border-radius:999px;padding:2px 8px;background:#dbeafe;color:#1e40af}.operation-body{padding-top:12px}.diff{background:#111827;color:#e5e7eb;border-radius:10px;overflow:auto;margin:12px 0}.diff-path{padding:7px 12px;background:#1f2937}.diff-path a{color:#93c5fd}.diff pre{padding:12px;margin:0}.diff pre span{display:block}.diff pre .line-number{display:inline-block;width:42px;color:#94a3b8;user-select:none}.diff .add{background:#123d2a;color:#bbf7d0}.diff .remove{background:#51212a;color:#fecaca}.parameter-diff{margin:12px 0}.parameter-line{display:flex;gap:8px;align-items:baseline;padding:5px 9px;margin:3px 0;border-radius:6px}.parameter-line.add,.delta-after,ins{background:var(--add-bg);color:var(--add-text)}.parameter-line.remove,.delta-before,del{background:var(--remove-bg);color:var(--remove-text)}.parameter-line.modify,.parameter-line.reorder{background:#fef3c7;color:#92400e}.parameter-line>span:first-child{font-weight:800}.parameter-attribute{white-space:nowrap}.delta-before,.delta-after,del,ins{padding:2px 5px;border-radius:4px;text-decoration:none}dl{display:grid;grid-template-columns:max-content 1fr;gap:8px 14px}dt{font-weight:700}dd{margin:0}.not-assessed{color:var(--muted);font-weight:700}.good,.compliance-summary.passed{color:var(--good)}.compliance-summary.failed{color:var(--danger)}.sources{color:var(--muted);font-size:13px}blockquote{margin:8px 0;padding:8px 12px;border-left:3px solid var(--accent);background:color-mix(in srgb,var(--panel) 90%,var(--accent))}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:8px;border-bottom:1px solid var(--line)}code{overflow-wrap:anywhere}@media(max-width:1050px){.summary-grid{grid-template-columns:repeat(3,minmax(0,1fr))}.affected-operation{grid-template-columns:1fr 65px}.affected-operation code{grid-column:1/-1}}@media(max-width:700px){.summary-grid{grid-template-columns:repeat(2,minmax(0,1fr));}.finding-summary{width:100%;margin-left:0}.contract-change-table{display:block;overflow-x:auto}.contract-change-table th,.contract-change-table td{min-width:210px}}@media(max-width:540px){.summary-grid{grid-template-columns:1fr}.hero h1{font-size:32px}}@media(prefers-color-scheme:dark){:root{--bg:#0f172a;--panel:#172033;--text:#e5e7eb;--muted:#9ca3af;--line:#334155;--add-bg:#123d2a;--add-text:#bbf7d0;--remove-bg:#51212a;--remove-text:#fecaca}.notice{background:#422006;color:#fde68a}.parameter-line.modify,.parameter-line.reorder{background:#422006;color:#fde68a}.contract-change-table th{background:#202d45}.contract-member{background:#1d293d}.contract-before{background:#431f29}.contract-after{background:#15382b}.breaking-rationale{background:#3b2f13}.http-method{color:#76b9ff}}
.tag{border-radius:999px;padding:3px 10px;background:#dbeafe;color:#1e40af;font-size:13px;font-weight:800}.finding-summary-meta{margin-left:auto;color:var(--muted);font-size:14px}.rest-operation-list{display:grid;gap:7px}.rest-operation-line{display:grid;grid-template-columns:minmax(220px,1fr) auto minmax(260px,2fr);gap:12px;padding:9px 11px;border:1px solid var(--line);border-radius:8px}.rest-operation-line>span{color:var(--muted)}@media(max-width:700px){.rest-operation-line{grid-template-columns:1fr}.finding-summary-meta{width:100%;margin-left:0}}
</style></head><body>
<header class="hero"><div class="container"><div class="eyebrow">TypeSpec Assessment</div><h1>${escapeHtml(headerTitle(assessment))}</h1>
<p class="hero-meta">TypeSpec source diff: ${comparisonHeader}</p>
<div class="summary-grid">
<a class="summary-card" href="${summary.codeQualityTarget}"><div class="summary-value"><span class="${summary.codeQuality === "passed" ? "pass" : summary.codeQuality === "failed" ? "fail" : ""}">${summary.codeQualityIcon}</span> ${escapeHtml(summary.codeQualityLabel)}</div><div class="summary-label">Overall code quality</div><div class="summary-detail">REST, downstream, and Azure Guidelines</div></a>
<a class="summary-card" href="#azure-compliance"><div class="summary-value"><span class="${complianceStatus(summary.complianceStatus).className}">${complianceStatus(summary.complianceStatus).icon}</span> ${escapeHtml(complianceStatus(summary.complianceStatus).label)}</div><div class="summary-label">Azure Guidelines</div><div class="summary-detail">${summary.complianceFindingCount} findings<br>${escapeHtml(summary.complianceCoverageDetail)}</div></a>
<a class="summary-card" href="#semantic-intents"><div class="summary-value"><span>i</span> ${summary.semanticItems.length}</div><div class="summary-label">Semantic intents</div><div class="summary-detail">${summary.operationCount} operations<br>${summary.actionCounts.add} Added, ${summary.actionCounts.modify} Modified, ${summary.actionCounts.remove} Removed</div></a>
<a class="summary-card" href="#rest-breaking"><div class="summary-value"><span class="${summary.restCount ? "fail" : "pass"}">${summary.restCount ? "×" : "✓"}</span> ${summary.restCount}</div><div class="summary-label">REST breaking changes</div></a>
<a class="summary-card" href="#downstream-breaking"><div class="summary-value"><span class="${summary.downstreamCount ? "fail" : "pass"}">${summary.downstreamCount ? "×" : "✓"}</span> ${summary.downstreamCount}</div><div class="summary-label">Downstream breaking changes</div><div class="summary-detail">${summary.directDownstreamCount} direct · ${summary.restCount} from REST breaking</div></a>
<a class="summary-card" href="#document-quality"><div class="summary-value"><span>i</span> Not assessed</div><div class="summary-label">Document Quality</div><div class="summary-detail">${escapeHtml(dimensions.documentQuality.summary)}</div></a>
</div></div></header>
<aside class="notice"><div class="container"><strong>Preview Notice.</strong> This report is a review aid, not official approval. Overall code quality combines REST/downstream safety with Azure Guidelines. Document Quality is reported separately and is not assessed.</div></aside>
<main class="container">
<section id="rest-breaking"><h2>REST breaking changes (${summary.restCount})</h2>${rest}</section>
<section id="downstream-breaking"><h2>Downstream breaking changes (${summary.downstreamCount})</h2>${downstreamItems}</section>
<section id="azure-compliance"><h2>Azure Guidelines (${summary.complianceFindingCount})</h2>${renderCompliance(dimensions.compliance, dimensions.semantic.items)}</section>
<section id="semantic-intents"><h2>Semantic intents (${dimensions.semantic.items.length})</h2>${semantic || '<div class="panel">No semantic review units.</div>'}</section>
<section id="document-quality"><h2>Document Quality</h2><div class="panel not-assessed"><strong>Not assessed</strong> — ${escapeHtml(dimensions.documentQuality.summary)}</div></section>
<section id="appendix"><details class="dimension-details"><summary><h2>Appendix</h2></summary><div class="panel"><h3 id="potential-limits">Potential limits</h3>${assessment.blockers.length ? `<ul>${assessment.blockers.map((blocker) => `<li>${escapeHtml(blocker.message ?? blocker)}</li>`).join("")}</ul>` : "<p>None</p>"}
<h3 id="projects-and-compiler-status">Projects and compiler status</h3><table><thead><tr><th>Project</th><th>Mode</th><th>Baseline commit@version</th><th>Target commit@version</th><th>Baseline AutoRest / TCGC</th><th>Target AutoRest / TCGC</th></tr></thead><tbody>${projects}</tbody></table>
<p><strong>Pull request:</strong> ${pullRequestLink(assessment.pullRequest)}</p>
${completeTypeSpecEvidence(dimensions.semantic.items)}
<h3 id="compliance-search-evidence">Guidance fetched</h3>
${complianceEvidenceAppendix(dimensions.compliance, dimensions.semantic.items)}
<h3>Changed files</h3><ul>${assessment.changedFiles.map((file) => `<li><code>${escapeHtml(file.path)}</code> (${escapeHtml(file.origins.join(", "))})</li>`).join("")}</ul>
<h3>Timing and model input</h3><pre>${escapeHtml(JSON.stringify({ timings: assessment.timings, inputAccounting: assessment.inputAccounting }, null, 2))}</pre>
<p><strong>Provenance:</strong> ${Object.values(assessment.provenance).map(escapeHtml).join(", ")}</p></div></details></section>
</main>
<script>
function revealHashTarget() {
  const target = document.getElementById(location.hash.slice(1));
  if (!target) return;
  for (let element = target; element; element = element.parentElement) {
    if (element.tagName === "DETAILS") element.open = true;
  }
  target.scrollIntoView({ block: "start" });
}
window.addEventListener("hashchange", revealHashTarget);
document.addEventListener("click", (event) => {
  if (event.target instanceof Element && event.target.closest('a[href^="#"]')) {
    window.setTimeout(revealHashTarget);
  }
});
revealHashTarget();
</script>
</body></html>\n`;
}

function adaptLegacy(assessment) {
  const dimensions = assessment.dimensions;
  const source = assessment.assessmentEvidence?.changedTypeSpec ?? [];
  const semanticItems = dimensions.semanticUnderstanding.items;
  const typeSpecDiffs = semanticItems.flatMap((item) =>
    item.changes.flatMap((change) => change.typeSpecDiffs ?? []),
  );
  const legacyProjects = (assessment.projects ?? []).map((projectPath) => {
    const projectDiffs = typeSpecDiffs.filter(
      (diff) =>
        diff.path === projectPath || diff.path.startsWith(`${projectPath}/`),
    );
    const base = extractApiVersions(
      projectDiffs.map((diff) =>
        (diff.lines ?? [])
          .filter((line) => !line.startsWith("+"))
          .map((line) => (line.startsWith("-") ? line.slice(1) : line))
          .join("\n"),
      ),
    );
    const current = extractApiVersions(
      projectDiffs.map((diff) =>
        (diff.lines ?? [])
          .filter((line) => !line.startsWith("-"))
          .map((line) => (line.startsWith("+") ? line.slice(1) : line))
          .join("\n"),
      ),
    );
    const artifactComparison =
      base.versions.length && current.versions.length
        ? selectApiVersionPair({
            base: { ...base, versioned: true },
            current: { ...current, versioned: true },
            baseCommit: assessment.baseline.commit,
            headCommit: assessment.head.commit,
          })
        : undefined;
    return {
      id: projectPath,
      path: projectPath,
      artifactComparison,
    };
  });
  const hasCodeSafetyFinding =
    dimensions.restBreakingChanges.findings.length > 0 ||
    dimensions.restCompatibleDownstreamBreakingChanges.findings.length > 0;
  const safetyStatus =
    assessment.overallCodeSafety?.toLowerCase?.() ??
    ((assessment.errors ?? []).length
      ? "not-assessed"
      : hasCodeSafetyFinding
        ? "failed"
        : "passed");
  const restFindingIds = new Set(
    dimensions.restBreakingChanges.findings.map(({ id }) => id),
  );
  const downstreamFindingIds = new Set(
    dimensions.restCompatibleDownstreamBreakingChanges.findings.map(
      ({ id }) => id,
    ),
  );
  const action = { added: "add", modified: "modify", removed: "remove" };
  const rangesOverlap = (leftStart, leftEnd, rightStart, rightEnd) =>
    leftStart <= rightEnd && rightStart <= leftEnd;
  const sourceLink = (reference) => {
    if (/^https?:\/\//.test(reference.link ?? "")) return reference.link;
    return (
      source.find(
        (item) =>
          item.path === reference.path &&
          item.revision === reference.revision &&
          rangesOverlap(
            item.startLine,
            item.endLine,
            reference.startLine,
            reference.endLine,
          ),
      )?.link ?? reference.link
    );
  };
  const toSources = (references = [], diffs = [], prefix = "legacy") =>
    references.map((reference, index) => {
      const matchingDiff = diffs.find((diff) => {
        if (diff.path !== reference.path) return false;
        const start =
          reference.revision === "head" ? diff.newStart : diff.oldStart;
        const count =
          reference.revision === "head" ? diff.newCount : diff.oldCount;
        return (
          Number.isFinite(start) &&
          rangesOverlap(
            reference.startLine,
            reference.endLine,
            start,
            start + Math.max((count ?? 1) - 1, 0),
          )
        );
      });
      const id = `${prefix}-source-${index}`;
      const hunkId = `${prefix}-hunk-${index}`;
      return {
        id,
        path: reference.path,
        hunks: [
          {
            id: hunkId,
            lines: matchingDiff?.lines ?? [],
            [reference.revision === "head" ? "current" : "base"]: {
              startLine: reference.startLine,
              lineCount: reference.endLine - reference.startLine + 1,
            },
          },
        ],
        declarations: [
          {
            id: `${prefix}-declaration-${index}`,
            hunkIds: [hunkId],
            source: {
              revision: reference.revision === "head" ? "current" : "base",
              startLine: reference.startLine,
              endLine: reference.endLine,
              link: sourceLink(reference),
            },
          },
        ],
      };
    });
  const relatedSemanticIntents = (findingId) =>
    semanticItems
      .filter((item) =>
        item.changes.some((change) =>
          change.linkedFindingIds?.includes(findingId),
        ),
      )
      .map(({ id }) => id);
  const legacyFindings = (findings) =>
    findings.map((finding) => ({
      ...finding,
      relatedSemanticIntents: relatedSemanticIntents(finding.id),
      sources: toSources(
        finding.sourceReferences,
        [],
        `legacy-finding-${finding.id}`,
      ),
    }));
  const legacyComplianceFindings =
    dimensions.azureCompliance.findings?.map((finding) => ({
      ...finding,
      relatedSemanticIntents: relatedSemanticIntents(finding.id),
      sourceReferences: (finding.sourceReferences ?? []).map((reference) => ({
        ...reference,
        link: sourceLink(reference),
      })),
      codeSnippets: (finding.codeSnippets ?? []).map((snippet) => ({
        ...snippet,
        link: sourceLink({
          path: snippet.path,
          revision: "head",
          startLine: snippet.startLine,
          endLine: snippet.endLine,
        }),
      })),
    })) ?? [];
  return {
    schemaVersion: 1,
    title: assessment.title,
    pullRequest: assessment.url
      ? { number: assessment.pr, url: assessment.url }
      : undefined,
    comparison: {
      baseCommit: assessment.baseline.commit,
      headCommit: assessment.head.commit,
    },
    confidence: assessment.overallConfidence ?? "medium",
    safety: { status: safetyStatus },
    dimensions: {
      semantic: {
        items: semanticItems.map((item) => {
          const change = item.changes[0] ?? {};
          const linkedFindingIds = item.changes.flatMap(
            ({ linkedFindingIds = [] }) => linkedFindingIds,
          );
          return {
            id: item.id,
            action: action[change.kind] ?? "modify",
            title: item.intent,
            summary:
              item.restRepresentation?.summary ?? change.summary ?? item.intent,
            operations: (item.restRepresentation?.operations ?? []).map(
              (operation) => ({
                ...operation,
                apiVersion:
                  operation.apiVersion ??
                  operation.apiVersions?.join(", ") ??
                  "",
                changedAspects: [],
                outcome:
                  change.effect ??
                  item.restRepresentation?.summary ??
                  change.summary,
              }),
            ),
            sources: toSources(
              item.sourceReferences,
              item.changes.flatMap(({ typeSpecDiffs = [] }) => typeSpecDiffs),
              `legacy-${item.id}`,
            ),
            relatedFindings: {
              rest: linkedFindingIds.filter((id) => restFindingIds.has(id)),
              downstream: linkedFindingIds.filter((id) =>
                downstreamFindingIds.has(id),
              ),
              sharedTypeImpact: [],
              compliance: linkedFindingIds.filter((id) =>
                legacyComplianceFindings.some((finding) => finding.id === id),
              ),
            },
          };
        }),
      },
      rest: {
        findings: [],
        legacyFindings: legacyFindings(dimensions.restBreakingChanges.findings),
      },
      downstream: {
        findings: [],
        legacyFindings: legacyFindings(
          dimensions.restCompatibleDownstreamBreakingChanges.findings,
        ),
        operationGroups: [],
        sharedTypeImpacts: [],
        impliedByRest: [],
      },
      compliance: {
        status: dimensions.azureCompliance.status ?? "not-assessed",
        summary:
          dimensions.azureCompliance.reason ??
          `${dimensions.azureCompliance.findings?.length ?? 0} historical compliance finding(s).`,
        coverage: {
          semanticIntentCount: 0,
          assessedIntentCount: 0,
          selectedDocumentCount:
            dimensions.azureCompliance.documents?.length ?? 0,
          unassessedIntentIds: [],
        },
        intentAssessments: [],
        findings: [],
        retrievalFailures: [],
        blockers:
          dimensions.azureCompliance.status === "not-assessed"
            ? [
                dimensions.azureCompliance.reason ??
                  "Historical compliance was not assessed.",
              ]
            : [],
        legacyDocuments: dimensions.azureCompliance.documents ?? [],
        legacyFindings: legacyComplianceFindings,
      },
      documentQuality: {
        status: "not-assessed",
        summary: "Document quality is not assessed.",
      },
    },
    blockers: assessment.errors ?? [],
    projects: legacyProjects,
    changedFiles: source.map((item) => ({
      path: item.path,
      origins: ["historical"],
    })),
    provenance: { source: "legacy assessment adapter" },
  };
}

export function renderAssessmentHtml(assessment) {
  const errors = validateAssessment(assessment);
  if (errors.length) throw new Error(errors.join("\n"));
  return renderCurrent(
    assessment.schemaVersion === 1 ? assessment : adaptLegacy(assessment),
  );
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const [input, output] = process.argv.slice(2);
    if (!input || !output) {
      throw new Error(
        "Usage: render-assessment-html.mjs <assessment.json> <assessment.html>",
      );
    }
    const html = renderAssessmentHtml(readJson(path.resolve(input)));
    fs.mkdirSync(path.dirname(path.resolve(output)), { recursive: true });
    fs.writeFileSync(path.resolve(output), html);
    console.log(path.resolve(output));
  });
}
