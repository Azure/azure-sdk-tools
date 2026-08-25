#!/usr/bin/env node

import { readFileSync, writeFileSync } from "node:fs";
import { basename, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import {
  deriveCodeSafety,
  displayedHunkLines,
  displayedTypeSpecExcerpts,
} from "./render-assessment.mjs";

const CHANGE_LABELS = {
  added: ["➕", "Added"],
  modified: ["✏️", "Modified"],
  removed: ["➖", "Removed"],
};

const METRIC_ICONS = {
  good: ["✓", "Passed"],
  info: ["ℹ", "Information"],
  warn: ["⚠", "Review"],
  danger: ["✕", "Failed"],
};

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function metricIcon(status) {
  const [symbol, label] = METRIC_ICONS[status];
  return `<span class="metric-icon metric-${status}" role="img" aria-label="${label}" title="${label}">${symbol}</span>`;
}

function riskStatus(level) {
  if (level === "High" || level === "high") return "good";
  if (level === "Medium" || level === "medium") return "warn";
  return "danger";
}

function titleCase(value) {
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}

function lowerFirst(value) {
  return `${value.charAt(0).toLowerCase()}${value.slice(1)}`;
}

function slug(value) {
  return String(value)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function sourceLinks(references) {
  return (references ?? [])
    .map(
      (reference) =>
        `<a href="${escapeHtml(reference.link)}">${escapeHtml(basename(reference.path))}:L${reference.startLine}-L${reference.endLine}</a>`,
    )
    .join(", ");
}

function findingGroups(assessment) {
  return [
    [
      "REST breaking changes",
      assessment.dimensions.restBreakingChanges.findings,
    ],
    [
      "Downstream breaking changes",
      assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings,
    ],
    ["Compliance findings", assessment.dimensions.azureCompliance.findings],
  ];
}

function findingsById(assessment) {
  return new Map(
    findingGroups(assessment)
      .flatMap(([, findings]) => findings)
      .map((finding) => [finding.id, finding]),
  );
}

function operationCount(assessment) {
  return assessment.dimensions.semanticUnderstanding.items.reduce(
    (total, item) => total + item.restRepresentation.operations.length,
    0,
  );
}

function formatDuration(milliseconds) {
  const seconds = Math.round(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes > 0 ? `${minutes}m ${seconds % 60}s` : `${seconds}s`;
}

function renderExecutionTime(assessment) {
  const duration = assessment.assessmentDuration;
  const breakdown = duration?.breakdown;
  if (!duration) {
    return '<div class="panel"><p class="empty-state">No execution timing was recorded.</p></div>';
  }
  if (!breakdown) {
    return `<div class="panel"><p><strong>Total:</strong> ${escapeHtml(formatDuration(duration.totalMs))}</p>${duration.note ? `<p class="sources">${escapeHtml(duration.note)}</p>` : ""}</div>`;
  }
  const rows = [
    [
      "Semantic understanding",
      breakdown.semanticUnderstandingMs,
      breakdown.semanticUnderstandingQuality,
    ],
    ["REST breaking", breakdown.restBreakingMs, breakdown.restBreakingQuality],
    [
      "Downstream breaking",
      breakdown.downstreamBreakingMs,
      breakdown.downstreamBreakingQuality,
    ],
    ["Compliance", breakdown.complianceMs, breakdown.complianceQuality],
    ["Overhead", breakdown.overheadMs, breakdown.overheadQuality],
    ["Total", breakdown.totalMs, breakdown.totalQuality],
  ];
  return `<div class="panel timing-panel">
<div class="appendix-table-scroll"><table class="appendix-table"><thead><tr><th>Phase</th><th>Duration</th><th>Timing quality</th></tr></thead><tbody>${rows
    .map(
      ([phase, milliseconds, quality]) =>
        `<tr><td>${escapeHtml(phase)}</td><td>${escapeHtml(formatDuration(milliseconds))}</td><td>${escapeHtml(quality)}</td></tr>`,
    )
    .join("")}</tbody></table></div>
<p><strong>Documentation search:</strong> ${escapeHtml(breakdown.searchRoute)}</p>
${duration.note ? `<p class="sources">${escapeHtml(duration.note)}</p>` : ""}
</div>`;
}

function changeCounts(assessment) {
  const counts = { added: 0, modified: 0, removed: 0 };
  for (const item of assessment.dimensions.semanticUnderstanding.items) {
    for (const change of item.changes) {
      counts[change.kind] += change.operationIds.length;
    }
  }
  return counts;
}

function renderDiff(hunk, change, focusIndex) {
  const oldPath = hunk.oldCount === 0 ? "/dev/null" : `a/${hunk.path}`;
  const newPath = hunk.newCount === 0 ? "/dev/null" : `b/${hunk.path}`;
  const lines = displayedHunkLines(
    hunk,
    change.sourceReferences,
    change,
    focusIndex,
  ).map((line) => {
    const className = line.startsWith("+")
      ? "add"
      : line.startsWith("-")
        ? "remove"
        : line.startsWith(" ...")
          ? "omitted"
          : "context";
    const decoratorClass =
      /@(added|removed|renamedFrom|madeOptional|typeChangedFrom|returnTypeChangedFrom)\(/.test(
        line,
      )
        ? " version-decorator"
        : "";
    return `<span class="${className}${decoratorClass}">${escapeHtml(line === " " ? "" : line)}</span>`;
  });
  return `<div class="diff">
<div class="diff-file">${escapeHtml(hunk.path)}</div>
<pre><span class="meta">--- ${escapeHtml(oldPath)}</span>
<span class="meta">+++ ${escapeHtml(newPath)}</span>
<span class="meta">@@ -${hunk.oldStart},${hunk.oldCount} +${hunk.newStart},${hunk.newCount} @@${hunk.context ? ` ${escapeHtml(hunk.context)}` : ""}</span>
${lines.join("\n")}</pre>
</div>`;
}

function operationFocuses(change, operation) {
  const [group = "", method = ""] = operation.operationId.split("_");
  const methodName = method
    ? `${method[0].toLowerCase()}${method.slice(1)}`
    : "";
  const focuses = [];
  const addFocus = (hunk, focusIndex) => {
    if (
      focusIndex >= 0 &&
      !focuses.some(
        (focus) =>
          focus.hunk === hunk && Math.abs(focus.focusIndex - focusIndex) <= 6,
      )
    ) {
      focuses.push({ hunk, focusIndex });
    }
  };
  for (const hunk of change.typeSpecDiffs) {
    const interfaceIndex = hunk.lines.findIndex(
      (line) =>
        /^\+?\s*interface\s+/.test(line) &&
        line.toLowerCase().includes(group.toLowerCase()),
    );
    if (change.kind === "added" && interfaceIndex >= 0) {
      const decoratorOffset = hunk.lines
        .slice(Math.max(0, interfaceIndex - 10), interfaceIndex)
        .findLastIndex((line) => /^\+\s*@added\(/.test(line));
      addFocus(
        hunk,
        decoratorOffset >= 0
          ? Math.max(0, interfaceIndex - 10) + decoratorOffset
          : interfaceIndex,
      );
    }
    const exactPatterns = [
      operation.operationId.toLowerCase(),
      `${group}.${methodName}`.toLowerCase(),
      `${methodName} is `.toLowerCase(),
      `op ${methodName}(`.toLowerCase(),
    ].filter((value) => value.length > 3);
    addFocus(
      hunk,
      hunk.lines.findIndex((line) =>
        exactPatterns.some((pattern) => line.toLowerCase().includes(pattern)),
      ),
    );
  }
  if (focuses.length === 0) {
    return displayedTypeSpecExcerpts(change.typeSpecDiffs, change).excerpts;
  }
  return focuses.slice(0, 2);
}

function restContract(operation) {
  const lro = operation.lro?.isLongRunning
    ? [
        "long-running",
        operation.lro.pattern && `pattern=${operation.lro.pattern}`,
        operation.lro.finalStateVia &&
          `final-state-via=${operation.lro.finalStateVia}`,
      ]
        .filter(Boolean)
        .join("; ")
    : "synchronous";
  const paging = operation.paging?.isPaged
    ? [
        "paged",
        operation.paging.itemType && `item=${operation.paging.itemType}`,
        operation.paging.itemsProperty &&
          `items-property=${operation.paging.itemsProperty}`,
        operation.paging.nextLinkName &&
          `next-link=${operation.paging.nextLinkName}`,
      ]
        .filter(Boolean)
        .join("; ")
    : "not paged";
  return {
    "method/path":
      operation.signature ?? `${operation.method} ${operation.path}`,
    parameters: (operation.parameters ?? []).join("; ") || "none",
    request: operation.requestPayload ?? "none",
    responses: (operation.responsePayloads ?? []).join("; ") || "none",
    LRO: lro,
    paging,
  };
}

function restContractDiff(change, operation, variants) {
  if (variants.length > 1) {
    const beforeOperation = variants[0];
    const afterOperation = variants.at(-1);
    const before = restContract(beforeOperation);
    const after = restContract(afterOperation);
    const fields = Object.keys(before).filter(
      (field) => before[field] !== after[field],
    );
    if (fields.length > 0) {
      const beforeVersions = beforeOperation.apiVersions.join(", ");
      const afterVersions = afterOperation.apiVersions.join(", ");
      const changedFields = fields.filter((field) => field !== "method/path");
      return [
        {
          kind: "remove",
          signature: before["method/path"],
          text: `[${beforeVersions}] ${changedFields
            .map((field) => `${field} = ${before[field]}`)
            .join("; ")}`,
        },
        {
          kind: "add",
          signature: after["method/path"],
          text: `[${afterVersions}] ${changedFields
            .map((field) => `${field} = ${after[field]}`)
            .join("; ")}`,
        },
      ];
    }
  }

  return [];
}

function renderOperationDetails(change, operations) {
  const affectedOperations = operations.filter((operation) =>
    change.operationIds.includes(operation.operationId),
  );
  const variantsByOperation = new Map();
  for (const operation of affectedOperations) {
    const variants = variantsByOperation.get(operation.operationId) ?? [];
    variants.push(operation);
    variantsByOperation.set(operation.operationId, variants);
  }
  return [...variantsByOperation.values()]
    .map((variants) => {
      const operation = variants.at(-1);
      const focuses = operationFocuses(change, operation);
      const operationSignature =
        operation.signature ?? `${operation.method} ${operation.path}`;
      const contractDiff = restContractDiff(change, operation, variants);
      const declaredContractDiff = change.aspects.filter(
        (aspect) =>
          /(parameter|request|response|header|status|payload|body|schema)/i.test(
            aspect.field,
          ) &&
          !/(lro|paging|api version availability)/i.test(aspect.field) &&
          ![aspect.before, aspect.after].some(
            (value) =>
              typeof value === "string" &&
              /^(baseline|head) operation contract\.$/i.test(value),
          ),
      );
      const apiVersions = [
        ...new Set(variants.flatMap((variant) => variant.apiVersions)),
      ];
      const sourceReferences = [
        ...new Map(
          variants
            .flatMap((variant) => variant.sourceReferences ?? [])
            .map((reference) => [
              `${reference.path}:${reference.revision}:${reference.startLine}:${reference.endLine}`,
              reference,
            ]),
        ).values(),
      ];
      const hasRepresentedContractChange = change.aspects.some((aspect) =>
        /method|path|parameter|request|response|header|status/i.test(
          aspect.field,
        ),
      );
      const restSignature =
        change.kind === "modified"
          ? contractDiff.length > 0
            ? `<div class="rest-signature rest-signature-change"><strong>REST API signature changes</strong><pre>${contractDiff
                .map(
                  ({ kind, signature, text }) =>
                    `<span class="${kind}">${kind === "remove" ? "-" : "+"} ${escapeHtml(signature ?? operationSignature)} | ${escapeHtml(text)}</span>`,
                )
                .join("\n")}</pre></div>`
            : declaredContractDiff.length > 0
              ? `<div class="rest-signature rest-signature-change"><strong>REST contract changes</strong><pre>${declaredContractDiff
                  .flatMap((aspect) => [
                    ...(aspect.before === null
                      ? []
                      : [
                          `<span class="remove">- ${escapeHtml(aspect.field)} | ${escapeHtml(aspect.before)}</span>`,
                        ]),
                    ...(aspect.after === null
                      ? []
                      : [
                          `<span class="add">+ ${escapeHtml(aspect.field)} | ${escapeHtml(aspect.after)}</span>`,
                        ]),
                  ])
                  .join("\n")}</pre><p>HTTP method and path remain unchanged.</p></div>`
            : hasRepresentedContractChange
              ? `<div class="rest-signature rest-signature-change"><strong>HTTP method and path unchanged; represented contract changes are summarized above</strong><code>${escapeHtml(operationSignature)}</code></div>`
              : `<div class="rest-signature rest-signature-unchanged"><strong>HTTP signature and represented payload contract unchanged</strong><code>${escapeHtml(operationSignature)}</code></div>`
          : `<div class="rest-signature"><strong>REST API signature</strong><code>${escapeHtml(operationSignature)}</code></div>`;
      const serviceBehavior = renderServiceBehavior(change, operation);
      return `<details class="operation-details">
<summary><code>${escapeHtml(operation.operationId)}</code><span class="operation-path tooltip" data-tooltip="${escapeHtml(`${operation.method} ${operation.path}`)}">${escapeHtml(operation.method)} ${escapeHtml(operation.path)}</span><span class="operation-version tooltip" data-tooltip="${escapeHtml(apiVersions.join(", "))}">${escapeHtml(apiVersions.join(", "))}</span></summary>
<div class="operation-body">
${restSignature}
${serviceBehavior}
${focuses.map(({ hunk, focusIndex }) => renderDiff(hunk, change, focusIndex)).join("")}
<p class="sources"><strong>TypeSpec source:</strong> ${sourceLinks(sourceReferences)}</p>
</div>
</details>`;
    })
    .join("");
}

function renderServiceBehavior(change, operation) {
  if (change.kind !== "modified") return "";

  const clause = (value) => String(value).trim().replace(/[.;:,]+$/, "");
  const outcomes = [];
  const aspectFields = change.aspects.map((aspect) =>
    aspect.field.toLowerCase(),
  );
  const lroRelated = aspectFields.some(
    (field) =>
      field.includes("lro") ||
      /initial operation|polling operation|final result|final operation|service behavior/.test(
        field,
      ),
  );
  if (operation.lro?.isLongRunning === true && lroRelated) {
    const metadataChanged = aspectFields.some(
      (field) => field.includes("lro") && field.includes("metadata"),
    );
    const serviceChanged = aspectFields.some((field) =>
      /initial operation|polling operation|final result|final operation|service behavior/.test(
        field,
      ),
    );
    const initialResponse =
      operation.responsePayloads.find((response) =>
        /^(201|202):/.test(response),
      ) ?? operation.responsePayloads[0];
    outcomes.push([
      "LRO",
      metadataChanged && !serviceChanged
        ? `${clause(change.typeSpecCause)}. Service behavior is unchanged: the operation still starts with ${clause(initialResponse)}, ${clause(lowerFirst(operation.lro.polling))}, and returns ${clause(operation.lro.finalResult)}.`
        : `${change.typeSpecCause} ${change.effect}`,
    ]);
  }
  const pagingAspect = change.aspects.find((aspect) =>
    aspect.field.toLowerCase().includes("paging"),
  );
  const pagingChanged = pagingAspect !== undefined;
  if (pagingChanged) {
    outcomes.push([
      "Paging",
      operation.paging?.isPaged === true
        ? `${clause(change.typeSpecCause)}. The REST response is unchanged; generated clients now expose ${operation.paging.itemType} values as pageable items and ${clause(lowerFirst(operation.paging.continuation))}.`
        : `${change.typeSpecCause} The operation result is no longer exposed as pageable.`,
    ]);
  }
  if (outcomes.length === 0) {
    return `<div class="lro-service-behavior"><strong>Change outcomes</strong><p>${escapeHtml(change.effect)}</p></div>`;
  }
  return `<div class="lro-service-behavior"><strong>Change outcomes</strong>${outcomes
    .map(
      ([title, summary]) =>
        `<p><span class="behavior-title">${escapeHtml(title)}:</span> ${escapeHtml(summary)}</p>`,
    )
    .join("")}</div>`;
}

function renderChange(
  change,
  impactMap,
  operations,
  { suppressOperationDetails = false } = {},
) {
  const [icon, label] = CHANGE_LABELS[change.kind];
  const impacts = change.linkedFindingIds
    .map((id) => impactMap.get(id))
    .filter(Boolean)
    .map(
      (finding) =>
        `<a class="impact severity-${finding.severity}" href="#finding-${slug(finding.id)}">${escapeHtml(finding.title)}</a>`,
    )
    .join("");
  const rows = change.aspects
    .map(
      (aspect) => `<tr>
<td><span class="change-badge ${change.kind}">${icon} ${label}</span></td>
<td>${escapeHtml(aspect.field)}</td>
<td>${aspect.before === null ? '<span class="empty">—</span>' : escapeHtml(aspect.before)}</td>
<td>${aspect.after === null ? '<span class="empty">—</span>' : escapeHtml(aspect.after)}</td>
<td><div class="table-impacts">${impacts || '<span class="empty">—</span>'}</div></td>
</tr>`,
    )
    .join("");
  const hasMergedOutcome = operations.some((operation) => {
    if (!change.operationIds.includes(operation.operationId)) return false;
    const fields = change.aspects.map((aspect) => aspect.field.toLowerCase());
    const lroChanged =
      operation.lro?.isLongRunning === true &&
      fields.some(
        (field) =>
          field.includes("lro") ||
          /initial operation|polling operation|final result|final operation|service behavior/.test(
            field,
          ),
      );
    const pagingChanged = fields.some((field) => field.includes("paging"));
    return change.kind === "modified" && (lroChanged || pagingChanged);
  });
  return `<table class="change-table">
<thead><tr><th>Change</th><th>Aspect</th><th>Before</th><th>After</th><th>Impact</th></tr></thead>
<tbody>${rows}</tbody>
</table>
${hasMergedOutcome ? "" : `<p class="typespec-summary"><strong>TypeSpec change:</strong> ${escapeHtml(change.typeSpecCause)}</p>`}
<p class="sources"><strong>Source:</strong> ${sourceLinks(change.sourceReferences)}</p>
${suppressOperationDetails ? `<div class="operation-changes"><p class="empty-state"><strong>Operation impact:</strong> operations using the changed models expose the additive fields in their payload schemas. The broader ${change.operationIds.length}-operation API-version lineage is retained in assessment.json, but it is not a list of ${change.operationIds.length} direct behavioral changes.</p></div>` : `<div class="operation-changes">
<h4>All affected operations <span class="count">${change.operationIds.length}</span></h4>
${renderOperationDetails(change, operations)}
</div>`}`;
}

function renderSemanticUnderstanding(assessment) {
  const impactMap = findingsById(assessment);
  const sortedItems = assessment.dimensions.semanticUnderstanding.items
    .map((item, originalIndex) => ({
      item,
      originalIndex,
      hasImpact: item.changes.some(
        (change) => change.linkedFindingIds.length > 0,
      ),
    }))
    .sort(
      (left, right) =>
        Number(right.hasImpact) - Number(left.hasImpact) ||
        left.originalIndex - right.originalIndex,
    );
  const intents = sortedItems.map(({ item, hasImpact }, index) => {
    const uniqueOperationCount = new Set(
      item.restRepresentation.operations.map(
        (operation) => operation.operationId,
      ),
    ).size;
    const kindCounts = Object.fromEntries(
      ["added", "modified", "removed"].map((kind) => [
        kind,
        new Set(
          item.changes
            .filter((change) => change.kind === kind)
            .flatMap((change) => change.operationIds),
        ).size,
      ]),
    );
    const isVersionLineage =
      /version-lineage/.test(item.id) ||
      /introduce the .* api version/i.test(item.intent);
    const operationSummary = isVersionLineage
      ? `Three model properties are added. Operations using those models expose the new fields, but the ${uniqueOperationCount}-operation version-lineage inventory is not a direct-impact count.`
      : [
          kindCounts.added > 0
            ? `${kindCounts.added} new operation${kindCounts.added === 1 ? "" : "s"}`
            : "",
          kindCounts.modified > 0
            ? `${kindCounts.modified} existing operation${kindCounts.modified === 1 ? "" : "s"} affected`
            : "",
          kindCounts.removed > 0
            ? `${kindCounts.removed} removed operation${kindCounts.removed === 1 ? "" : "s"}`
            : "",
        ]
          .filter(Boolean)
          .join("; ");
    const kindIcons = ["added", "modified", "removed"]
      .filter((kind) => kindCounts[kind] > 0)
      .map(
        (kind) =>
          `<span class="intent-kind-icon" title="${CHANGE_LABELS[kind][1]}">${CHANGE_LABELS[kind][0]}</span>`,
      )
      .join("");
    const countLabel = isVersionLineage
      ? `${item.changes.flatMap((change) => change.aspects).length} model properties`
      : `${uniqueOperationCount} operation${uniqueOperationCount === 1 ? "" : "s"}`;
    return `<details class="intent-details" id="change-${index + 1}">
<summary><span class="change-number">${index + 1}</span><span>${isVersionLineage ? "" : kindIcons}${hasImpact ? '<span class="intent-impact-icon" title="Has linked impact">⚠️</span> ' : ""}${escapeHtml(item.intent)} <span class="count">${countLabel}</span></span></summary>
<article class="change-card">
<div class="change-content">
<p class="intent-summary">${escapeHtml(item.restRepresentation.summary)}</p>
<p class="intent-api-surface"><strong>API surface:</strong> ${escapeHtml(operationSummary)}${item.changes.length > 1 ? " The groups below are disjoint parts of the same user intent." : ""}</p>
${item.changes.map((change) =>
  renderChange(
    change,
    impactMap,
    item.restRepresentation.operations,
    { suppressOperationDetails: isVersionLineage && uniqueOperationCount > 25 },
  ),
).join("")}
</div>
</article>
</details>`;
  });
  const visibleIntentCount = 10;
  if (intents.length <= visibleIntentCount) {
    return intents.join("");
  }
  return `${intents.slice(0, visibleIntentCount).join("")}
<details class="more-intents">
<summary>Show ${intents.length - visibleIntentCount} more semantic intent${intents.length - visibleIntentCount === 1 ? "" : "s"}</summary>
<div class="more-intents-body">${intents.slice(visibleIntentCount).join("")}</div>
</details>`;
}

function renderFindingSourceDiff(assessment, finding) {
  const sourcePaths = new Set(
    (finding.sourceReferences ?? []).map((reference) => reference.path),
  );
  const rendered = [];
  const seen = new Set();
  for (const change of complianceChanges(assessment, finding)) {
    const findingChange = {
      ...change,
      sourceReferences: finding.sourceReferences,
    };
    for (const hunk of change.typeSpecDiffs ?? []) {
      const key = `${hunk.path}:${hunk.oldStart}:${hunk.newStart}`;
      if (
        !sourcePaths.has(hunk.path) ||
        seen.has(key) ||
        rendered.length >= 4
      ) {
        continue;
      }
      seen.add(key);
      const focusIndex = Math.max(
        0,
        hunk.lines.findIndex(
          (line) =>
            (line.startsWith("+") && !line.startsWith("+++")) ||
            (line.startsWith("-") && !line.startsWith("---")),
        ),
      );
      rendered.push(renderDiff(hunk, findingChange, focusIndex));
    }
  }
  if (rendered.length === 0) return "";
  return `<details class="finding-source-diff">
<summary>TypeSpec source change <span>expand to inspect why this breaks clients</span></summary>
${rendered.join("")}
</details>`;
}

function renderFinding(finding, sourceDiff = "") {
  return `<article class="finding severity-border-${finding.severity}" id="finding-${slug(finding.id)}">
<div class="finding-heading"><span class="severity severity-${finding.severity}">${escapeHtml(finding.severity)}</span><h3>${escapeHtml(finding.title)}</h3></div>
<p>${escapeHtml(finding.summary)}</p>
<p><strong>Evidence:</strong> ${escapeHtml(Array.isArray(finding.evidence) ? finding.evidence.join("; ") : finding.evidence)}</p>
<p class="sources"><strong>TypeSpec source:</strong> ${sourceLinks(finding.sourceReferences)}</p>
${sourceDiff}
${finding.documentationUrl ? `<p><strong>Guidance:</strong> <a href="${escapeHtml(finding.documentationUrl)}">${escapeHtml(finding.documentationUrl)}</a></p>` : ""}
</article>`;
}

function complianceChanges(assessment, finding) {
  const findingPaths = new Set(
    (finding.sourceReferences ?? []).map((reference) => reference.path),
  );
  const changes = assessment.dimensions.semanticUnderstanding.items.flatMap(
    (item) => item.changes,
  );
  const linked = changes.filter((change) =>
    change.linkedFindingIds.includes(finding.id),
  );
  if (linked.length > 0) return linked;
  return changes.filter((change) =>
    (change.sourceReferences ?? []).some((reference) =>
      findingPaths.has(reference.path),
    ),
  );
}

function renderCodeSnippet(snippet, finding) {
  const source = finding.sourceReferences.find(
    (reference) =>
      reference.path === snippet.path &&
      reference.startLine <= snippet.startLine &&
      reference.endLine >= snippet.endLine,
  );
  const link = source?.link.replace(
    /#L\d+-L\d+$/,
    `#L${snippet.startLine}-L${snippet.endLine}`,
  );
  const lines = snippet.lines
    .map(
      (line, index) =>
        `<span class="context"><span class="line-number">${snippet.startLine + index}</span>${escapeHtml(line)}</span>`,
    )
    .join("\n");
  return `<div class="diff source-code">
<div class="diff-file"><a href="${escapeHtml(link)}">${escapeHtml(snippet.path)}:L${snippet.startLine}-L${snippet.endLine}</a></div>
<pre>${lines}</pre>
</div>`;
}

function renderExpectedCodeSnippet(snippet) {
  return `<div class="diff expected-code">
<div class="diff-file"><a href="${escapeHtml(snippet.url)}">${escapeHtml(snippet.caption)}</a></div>
<pre>${snippet.lines.map((line) => `<span class="context">${escapeHtml(line)}</span>`).join("\n")}</pre>
</div>`;
}

function renderComplianceFinding(assessment, finding) {
  const document = assessment.dimensions.azureCompliance.documents.find(
    (candidate) => candidate.url === finding.documentationUrl,
  ) ?? {
    title: "Referenced guidance",
    section: "Unavailable",
    url: finding.documentationUrl,
    applicableGuidance: "Matching fetched guidance is unavailable.",
    evidence: Array.isArray(finding.evidence)
      ? finding.evidence.join("; ")
      : finding.evidence,
  };
  const exactSnippets = finding.codeSnippets ?? [];
  const excerpts =
    exactSnippets.length > 0
      ? []
      : complianceChanges(assessment, finding)
          .flatMap((change) =>
            displayedTypeSpecExcerpts(
              change.typeSpecDiffs,
              change,
            ).excerpts.map((excerpt) => ({ ...excerpt, change })),
          )
          .filter(
            (excerpt, index, all) =>
              all.findIndex(
                (candidate) =>
                  candidate.hunk.path === excerpt.hunk.path &&
                  candidate.focusIndex === excerpt.focusIndex,
              ) === index,
          )
          .slice(0, 2);
  const code =
    exactSnippets.length > 0 || excerpts.length > 0
      ? `<div class="compliance-code"><h4>TypeSpec code</h4>${
          exactSnippets.length > 0
            ? exactSnippets
                .map((snippet) => renderCodeSnippet(snippet, finding))
                .join("")
            : excerpts
                .map(({ hunk, focusIndex, change }) =>
                  renderDiff(hunk, change, focusIndex),
                )
                .join("")
        }</div>`
      : "";
  const expectedCode =
    document.expectedCodeStatus === "available"
      ? document.expectedCodeSnippets.map(renderExpectedCodeSnippet).join("")
      : `<p class="empty-state">${escapeHtml(document.expectedCodeReason ?? "The fetched guidance does not contain an applicable code example.")}</p>`;
  const actualEvidence = Array.isArray(finding.evidence)
    ? finding.evidence.join("; ")
    : finding.evidence;
  return `<details class="finding compliance-finding severity-border-${finding.severity}" id="finding-${slug(finding.id)}">
<summary><span class="severity severity-${finding.severity}">${escapeHtml(finding.severity)}</span><h3>${escapeHtml(finding.title)}</h3></summary>
<div class="finding-body">
<p><strong>Gap:</strong> ${escapeHtml(finding.summary)}</p>
<details class="comparison-details expected-details">
<summary>Expected</summary>
<div class="comparison-body">
<p>${escapeHtml(document.guidanceExcerpt)}</p>
<p><strong>Guidance:</strong> <a href="${escapeHtml(document.url)}">${escapeHtml(document.title)} — ${escapeHtml(document.section)}</a></p>
${expectedCode}
</div>
</details>
<details class="comparison-details actual-details">
<summary>Actual</summary>
<div class="comparison-body">
<p>${escapeHtml(actualEvidence)}</p>
${code}
</div>
</details>
</div>
</details>`;
}

function renderFindingCategory(findings, emptyMessage, render = renderFinding) {
  return findings.length > 0
    ? findings.map(render).join("")
    : `<div class="panel"><p class="empty-state good">${escapeHtml(emptyMessage)}</p></div>`;
}

function renderAppendix(assessment) {
  const errors = assessment.errors ?? [];
  const documents = assessment.dimensions.azureCompliance.documents ?? [];
  const complianceFindings =
    assessment.dimensions.azureCompliance.findings ?? [];
  const emitterRuns = assessment.assessmentEvidence?.emitterRuns ?? [];
  const tooling = [
    ...new Set(emitterRuns.map((run) => run.emitter).filter(Boolean)),
  ];
  const artifactEvidence = assessment.artifactEvidence
    ? Object.entries(assessment.artifactEvidence)
    : [];
  const guidanceRows = documents
    .map((document) => {
      const result = complianceFindings.some(
        (finding) => finding.documentationUrl === document.url,
      )
        ? "Mismatch"
        : "Matched";
      return `<tr><td>${result}</td><td><a href="${escapeHtml(document.url)}">${escapeHtml(document.title)} — ${escapeHtml(document.section)}</a></td><td>${escapeHtml(document.guidanceExcerpt)}</td><td>${escapeHtml(document.evidence)}</td><td>${sourceLinks(document.sourceReferences)}</td></tr>`;
    })
    .join("");
  return `<details class="appendix-details">
<summary>Appendix</summary>
<div class="appendix-body">
<h3>Assessment Errors</h3>
${errors.length > 0 ? `<ul>${errors.map((error) => `<li>${escapeHtml(error)}</li>`).join("")}</ul>` : '<p class="empty-state">None.</p>'}
<h3>Code-to-Guidance Evidence</h3>
${guidanceRows ? `<div class="appendix-table-scroll"><table class="appendix-table"><thead><tr><th>Result</th><th>Document section</th><th>Fetched guidance</th><th>Observed TypeSpec</th><th>Evidence</th></tr></thead><tbody>${guidanceRows}</tbody></table></div>` : '<p class="empty-state">No authoritative document evidence was available.</p>'}
<h3>Tooling Used</h3>
${tooling.length > 0 ? `<ul>${tooling.map((name) => `<li><code>${escapeHtml(name)}</code></li>`).join("")}</ul>` : '<p class="empty-state">No emitter or library usage recorded.</p>'}
<h3>Artifact Evidence</h3>
${artifactEvidence.length > 0 ? `<dl>${artifactEvidence.map(([name, evidence]) => `<dt>${escapeHtml(name)}</dt><dd>${escapeHtml(evidence)}</dd>`).join("")}</dl>` : '<p class="empty-state">No aggregate artifact evidence recorded.</p>'}
<h3>Execution time breakdown</h3>
${renderExecutionTime(assessment)}
</div>
</details>`;
}

export function renderAssessmentHtml(assessment) {
  const counts = changeCounts(assessment);
  const safety = deriveCodeSafety(assessment);
  const restFindings = assessment.dimensions.restBreakingChanges.findings;
  const downstreamFindings =
    assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings;
  const compliance = assessment.dimensions.azureCompliance;
  const confidence = assessment.overallConfidence;
  const complianceMetricStatus =
    compliance.status === "passed"
      ? "good"
      : compliance.status === "failed"
        ? "danger"
        : "warn";
  const title = assessment.pr
    ? `PR #${assessment.pr} — ${assessment.title}`
    : (assessment.title ?? "TypeSpec working-tree assessment");
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escapeHtml(title)} | TypeSpec Assessment</title>
<style>
:root{color-scheme:light dark;--bg:#f5f7fb;--panel:#fff;--text:#172033;--muted:#64748b;--line:#dbe3ef;--accent:#2563eb;--accent-soft:#eff6ff;--good:#047857;--warn:#b45309;--danger:#b91c1c;--code:#111827;--code-text:#e5e7eb;--add-bg:#163d2b;--add:#9ae6b4;--remove-bg:#4a2028;--remove:#feb2b2}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}a{color:var(--accent);text-decoration:none}a:hover{text-decoration:underline}.container{width:min(1180px,calc(100% - 32px));margin:auto}.hero{background:linear-gradient(125deg,#172554,#1d4ed8);color:#fff;padding:44px 0 34px}.eyebrow{text-transform:uppercase;letter-spacing:.12em;font-size:12px;font-weight:700;opacity:.8}.hero h1{font-size:clamp(26px,4vw,42px);line-height:1.15;margin:8px 0 12px}.hero-meta{opacity:.82}.metrics{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:12px;margin-top:28px}.metric{display:block;background:#ffffff16;border:1px solid #ffffff2b;border-radius:12px;padding:14px;color:inherit;text-decoration:none}.metric:hover{background:#ffffff24;text-decoration:none}.metric strong{display:block;font-size:24px}.metric span{font-size:12px;opacity:.8}nav{position:sticky;top:0;z-index:5;background:color-mix(in srgb,var(--panel) 92%,transparent);backdrop-filter:blur(12px);border-bottom:1px solid var(--line)}nav .container{display:flex;gap:22px;padding:12px 0;overflow:auto}main{padding:30px 0 60px}section{scroll-margin-top:64px;margin-bottom:34px}h2{font-size:25px;margin:0 0 16px}h3{line-height:1.3}.panel,.change-card,.finding{background:var(--panel);border:1px solid var(--line);border-radius:14px;box-shadow:0 4px 18px #0f172a0a}.panel{padding:20px}.intent-details{margin-bottom:12px}.intent-details>summary{display:flex;align-items:center;gap:14px;cursor:pointer;background:var(--panel);border:1px solid var(--line);border-radius:14px;padding:0 18px 0 0;font-size:18px;font-weight:750;box-shadow:0 4px 18px #0f172a0a}.intent-details>summary .change-number{align-self:stretch;display:flex;align-items:center;justify-content:center;min-width:48px;padding:14px 10px}.intent-kind-icon,.intent-impact-icon{font-size:15px}.intent-details[open]>summary{border-radius:14px 14px 0 0}.intent-details[open]>.change-card{border-top:0;border-radius:0 0 14px 14px}.more-intents{margin-top:12px}.more-intents>summary{cursor:pointer;width:max-content;color:var(--accent);font-weight:700;padding:8px 2px}.more-intents-body{margin-top:8px}.change-card{display:grid;grid-template-columns:1fr;margin-bottom:18px;overflow:visible}.change-number{background:var(--accent-soft);color:var(--accent);font-size:18px;font-weight:800;text-align:center;padding-top:22px}.change-content{min-width:0;padding:20px 22px}.change-content>h3{margin:0 0 18px;font-size:18px}.change-table,.impact-table{width:100%;border-collapse:collapse}.change-table th,.change-table td,.impact-table th,.impact-table td{text-align:left;vertical-align:top;padding:10px;border-bottom:1px solid var(--line)}th{color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.04em}.change-badge,.severity,.impact{display:inline-flex;align-items:center;border-radius:999px;padding:3px 9px;font-size:12px;font-weight:700;white-space:nowrap}.added{background:#dcfce7;color:#166534}.modified{background:#fef3c7;color:#92400e}.removed{background:#fee2e2;color:#991b1b}.typespec-summary{margin:20px 0 6px}.versioning-summary{margin:6px 0 12px;color:var(--muted)}.diff{border:1px solid #334155;border-radius:10px;overflow:hidden;margin:12px 0;background:var(--code)}.diff-file{padding:8px 12px;color:#cbd5e1;background:#1e293b;font:12px ui-monospace,SFMono-Regular,Consolas,monospace;word-break:break-all}.diff pre{margin:0;padding:12px;overflow:auto;color:var(--code-text);font:13px/1.5 ui-monospace,SFMono-Regular,Consolas,monospace}.diff span{display:block;min-height:1.5em}.diff .add{background:var(--add-bg);color:var(--add)}.diff .remove{background:var(--remove-bg);color:var(--remove)}.diff .meta{color:#93c5fd}.diff .omitted{color:#94a3b8;font-style:italic}.diff .version-decorator{background:#713f12;color:#fef08a;font-weight:800;border-left:3px solid #facc15;padding-left:8px}.table-impacts{display:flex;flex-wrap:wrap;gap:6px;min-width:180px}.impact{border:1px solid currentColor}.severity-high{color:var(--danger);background:#fee2e2}.severity-medium{color:var(--warn);background:#fef3c7}.severity-low{color:#475569;background:#e2e8f0}.sources,.omitted-note,.empty{color:var(--muted);font-size:13px}.operation-changes{margin-top:20px;padding-top:14px;border-top:1px solid var(--line)}.operation-changes h4{margin:0 0 10px}.operation-details{border:1px solid var(--line);border-radius:9px;margin:8px 0;background:var(--bg)}.operation-details>summary{display:flex;align-items:center;gap:12px;cursor:pointer;padding:10px 12px;font-weight:650}.operation-details>summary span{color:var(--muted);font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.operation-details>summary .operation-version{margin-left:auto;background:var(--line);border-radius:999px;padding:2px 8px}.tooltip{position:relative}.tooltip::before,.tooltip::after{position:absolute;z-index:20;pointer-events:none;visibility:hidden;opacity:0;transition:opacity .12s ease,transform .12s ease}.tooltip::before{content:"";left:18px;top:calc(100% + 3px);border:5px solid transparent;border-bottom-color:var(--line);transform:translateY(-3px)}.tooltip::after{content:attr(data-tooltip);left:0;top:calc(100% + 13px);width:max-content;max-width:min(620px,72vw);padding:7px 10px;border:1px solid var(--line);border-radius:6px;background:var(--panel);color:var(--text);box-shadow:0 6px 18px #0f172a1f;white-space:normal;overflow-wrap:anywhere;font:12px/1.4 -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;transform:translateY(-3px)}.tooltip:hover{overflow:visible}.tooltip:hover::before,.tooltip:hover::after{visibility:visible;opacity:1;transform:translateY(0)}.operation-version.tooltip::before{left:auto;right:18px}.operation-version.tooltip::after{left:auto;right:0}.operation-body{padding:0 12px 12px}.rest-signature{display:flex;align-items:flex-start;gap:10px;margin:4px 0 12px;padding:10px 12px;border-radius:8px;background:var(--panel);border:1px solid var(--line)}.rest-signature strong{white-space:nowrap}.rest-signature code{overflow-wrap:anywhere}.rest-signature-change{display:block;min-width:0;max-width:100%;overflow:hidden}.rest-signature-change pre{max-width:100%;margin:8px 0 0;white-space:pre-wrap;overflow-wrap:anywhere;word-break:break-word;font:13px/1.55 ui-monospace,SFMono-Regular,Consolas,monospace}.rest-signature-change span{display:block;max-width:100%;padding:2px 7px;overflow-wrap:anywhere;word-break:break-word}.rest-signature-change .add{background:#dcfce7;color:#166534}.rest-signature-change .remove{background:#fee2e2;color:#991b1b}.lro-service-behavior{margin:0 0 12px;padding:10px 12px;border:1px solid var(--line);border-radius:8px;background:var(--panel)}.lro-service-behavior>strong{margin-right:8px}.behavior-status{display:inline-flex;border-radius:999px;padding:2px 8px;font-size:12px;font-weight:700}.behavior-status.unchanged{background:#dcfce7;color:#166534}.behavior-status.changed{background:#fef3c7;color:#92400e}.lro-service-behavior dl{display:grid;grid-template-columns:max-content 1fr;gap:5px 12px;margin:10px 0 0}.lro-service-behavior dt{font-weight:700}.lro-service-behavior dd{margin:0;color:var(--muted)}.operation-body .diff{margin-top:4px}.finding-group>h3{margin:20px 0 10px}.count{display:inline-block;background:var(--line);border-radius:999px;padding:1px 8px;font-size:12px}.finding{padding:16px 18px;margin:10px 0}.compliance-status{display:inline-block;margin-left:8px;border-radius:999px;padding:3px 9px;font-size:12px;text-transform:uppercase}.compliance-status.passed{background:#dcfce7;color:#166534}.compliance-status.failed{background:#fee2e2;color:#991b1b}.compliance-status.not-assessed{background:#fef3c7;color:#92400e}.finding-heading{display:flex;gap:10px;align-items:center}.finding-heading h3{margin:0}.severity-border-high{border-left:4px solid var(--danger)}.severity-border-medium{border-left:4px solid var(--warn)}.severity-border-low{border-left:4px solid #64748b}.empty-state{color:var(--muted)}.empty-state.good{color:var(--good);font-weight:600}.appendix-details{background:var(--panel);border:1px solid var(--line);border-radius:14px}.appendix-details>summary{cursor:pointer;padding:16px 20px;font-size:22px;font-weight:750}.appendix-body{padding:0 20px 20px}.appendix-body h3{margin:24px 0 10px}.appendix-table-scroll{overflow:auto}.appendix-table{width:100%;border-collapse:collapse}.appendix-table th,.appendix-table td{text-align:left;vertical-align:top;padding:9px;border-bottom:1px solid var(--line)}.appendix-body dl{display:grid;grid-template-columns:max-content 1fr;gap:8px 16px}.appendix-body dt{font-weight:700}.appendix-body dd{margin:0}.status-high{color:#86efac}.status-medium{color:#fde68a}.status-low{color:#fecaca}.metric-icon{display:inline-flex;align-items:center;justify-content:center;width:1.25em;margin-right:.3em;font-size:.85em}.metric-good{color:#86efac}.metric-info{color:#bfdbfe}.metric-warn{color:#fde68a}.metric-danger{color:#fecaca}@media(max-width:760px){.metrics{grid-template-columns:repeat(2,1fr)}.change-card{grid-template-columns:1fr}.change-number{text-align:left;padding:10px 20px}.change-table{display:block;overflow:auto}.hero{padding-top:28px}}@media(prefers-color-scheme:dark){:root{--bg:#0f172a;--panel:#172033;--text:#e5e7eb;--muted:#9ca3af;--line:#334155;--accent:#93c5fd;--accent-soft:#1e3a5f}.added{background:#163d2b;color:#9ae6b4}.modified{background:#473b16;color:#fde68a}.removed{background:#4a2028;color:#fecaca}.severity-high{background:#4a2028}.severity-medium{background:#473b16}.severity-low{background:#334155;color:#cbd5e1}}
.behavior-section{margin-top:10px;padding-top:10px;border-top:1px solid var(--line)}.behavior-section:first-of-type{border-top:0}.behavior-title{margin-right:8px;font-weight:700}.compliance-finding>summary{display:flex;align-items:center;gap:10px;cursor:pointer;list-style:none}.compliance-finding>summary::-webkit-details-marker{display:none}.compliance-finding>summary::after{content:"▸";margin-left:auto;color:var(--muted);font-size:18px}.compliance-finding[open]>summary::after{content:"▾"}.compliance-finding>summary h3{margin:0}.finding-body{padding-top:12px}.comparison-details{margin:10px 0;border:1px solid var(--line);border-radius:10px;background:var(--bg)}.comparison-details>summary{cursor:pointer;padding:11px 13px;font-weight:750}.comparison-body{padding:0 13px 13px}.compliance-code{margin-top:12px}.compliance-code h4{margin:14px 0 6px}.source-code .line-number{display:inline-block;min-width:3.5em;margin-right:12px;color:#64748b;text-align:right;user-select:none}.source-code pre>.context{white-space:pre}
.metric strong{display:flex;align-items:center}.metric strong .metric-icon{width:1.15em;margin-right:.4em;font-size:1.05em;opacity:1}.metric>span.metric-title{display:block;font-size:15px;font-weight:650;opacity:.9}.metric>span.metric-detail{display:block;margin-top:4px;font-size:12px;opacity:.75}
.finding-source-diff{margin-top:14px;border:1px solid var(--line);border-radius:10px;background:var(--bg);overflow:hidden}.finding-source-diff>summary{cursor:pointer;padding:11px 13px;font-weight:750}.finding-source-diff>summary span{margin-left:6px;color:var(--muted);font-size:12px;font-weight:400}.finding-source-diff>.diff{margin:0;border-width:1px 0 0;border-radius:0}.finding-source-diff .diff pre{white-space:pre-wrap;overflow-wrap:anywhere;word-break:break-word}
</style>
</head>
<body>
<header class="hero"><div class="container">
<div class="eyebrow">TypeSpec Assessment</div>
<h1>${assessment.url ? `<a href="${escapeHtml(assessment.url)}" style="color:inherit">${escapeHtml(title)}</a>` : escapeHtml(title)}</h1>
<div class="hero-meta">Overall confidence: <strong class="status-${confidence}">${escapeHtml(titleCase(confidence))}</strong> · Baseline <code>${escapeHtml(assessment.baseline?.ref ?? assessment.baseline?.commit)}</code> → Head <code>${escapeHtml(assessment.head?.commit)}</code></div>
<div class="metrics">
<div class="metric safety-metric"><strong class="status-${safety.toLowerCase()}">${metricIcon(riskStatus(safety))}${escapeHtml(safety)}</strong><span class="metric-title">Overall code safety</span></div>
<a class="metric" href="#semantic-intents"><strong>${metricIcon("info")}${assessment.dimensions.semanticUnderstanding.items.length}</strong><span class="metric-title">Semantic intents</span><span class="metric-detail">${operationCount(assessment)} operations<br>${counts.added} Added, ${counts.modified} Modified, ${counts.removed} Removed</span></a>
<a class="metric" href="#rest-breaking"><strong>${metricIcon(restFindings.length === 0 ? "good" : "danger")}${restFindings.length}</strong><span class="metric-title">REST breaking changes</span></a>
<a class="metric" href="#downstream-breaking"><strong>${metricIcon(downstreamFindings.length === 0 ? "good" : "danger")}${downstreamFindings.length}</strong><span class="metric-title">Downstream breaking changes</span></a>
<a class="metric" href="#azure-compliance"><strong>${metricIcon(complianceMetricStatus)}${compliance.findings.length}</strong><span class="metric-title">Azure compliance</span><span class="metric-detail">${compliance.findings.length} finding${compliance.findings.length === 1 ? "" : "s"}</span></a>
</div></div></header>
<nav><div class="container"><a href="#semantic-intents">Semantic intents</a><a href="#rest-breaking">REST breaking changes</a><a href="#downstream-breaking">Downstream breaking changes</a><a href="#azure-compliance">Azure compliance</a><a href="#appendix">Appendix</a></div></nav>
<main class="container">
<section id="semantic-intents"><h2>Semantic intents <span class="count">${assessment.dimensions.semanticUnderstanding.items.length}</span></h2>${renderSemanticUnderstanding(assessment)}</section>
<section id="rest-breaking"><h2>REST breaking changes <span class="count">${restFindings.length}</span></h2>${renderFindingCategory(restFindings, "No REST breaking changes detected.")}</section>
<section id="downstream-breaking"><h2>Downstream breaking changes <span class="count">${downstreamFindings.length}</span></h2>${renderFindingCategory(downstreamFindings, "No downstream breaking changes detected.", (finding) => renderFinding(finding, renderFindingSourceDiff(assessment, finding)))}</section>
<section id="azure-compliance"><h2>Azure compliance <span class="compliance-status ${escapeHtml(compliance.status)}">${escapeHtml(compliance.status)}</span></h2>${renderFindingCategory(compliance.findings, compliance.status === "passed" ? "Azure compliance passed with no mismatches." : (compliance.reason ?? "No compliance findings recorded."), (finding) => renderComplianceFinding(assessment, finding))}</section>
<section id="appendix">${renderAppendix(assessment)}</section>
</main>
</body>
</html>
`;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const [, , jsonPath, htmlPath] = process.argv;
  if (!jsonPath || !htmlPath) {
    process.stderr.write(
      "Usage: render-assessment-html.mjs <assessment.json> <assessment.html>\n",
    );
    process.exitCode = 1;
  } else {
    const assessment = JSON.parse(readFileSync(resolve(jsonPath), "utf8"));
    writeFileSync(resolve(htmlPath), renderAssessmentHtml(assessment));
  }
}
