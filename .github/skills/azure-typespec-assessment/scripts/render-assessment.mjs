import { readFileSync, writeFileSync } from "node:fs";
import { basename, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SEVERITY_ORDER = { high: 0, medium: 1, low: 2 };
const CHANGE_KIND_LABELS = {
  added: "➕ Added",
  modified: "✏️ Modified",
  removed: "➖ Removed",
};

function text(value, fallback = "None.") {
  if (value === undefined || value === null || value === "") return fallback;
  return String(value).replace(/\s+/g, " ").trim();
}

function tableText(value) {
  return text(value).replaceAll("|", "\\|").replaceAll("\n", " ");
}

function code(value) {
  return `\`${text(value, "unknown").replaceAll("`", "\\`")}\``;
}

function joinValues(value, separator = "; ") {
  if (Array.isArray(value))
    return value.map((item) => text(item)).join(separator);
  return text(value);
}

function slug(value) {
  return text(value, "item")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function sourceLabel(reference) {
  return `${basename(reference.path)}:L${reference.startLine}-L${reference.endLine}`;
}

function sourceLinks(references, limit = Number.POSITIVE_INFINITY) {
  const visible = (references ?? []).slice(0, limit);
  const links = visible.map(
    (reference) => `[${sourceLabel(reference)}](${reference.link})`,
  );
  const remaining = (references?.length ?? 0) - visible.length;
  if (remaining > 0) links.push(`+${remaining} more`);
  return links.join(", ") || "None.";
}

function allFindings(assessment) {
  const dimensions = assessment.dimensions;
  return [
    ...dimensions.restBreakingChanges.findings.map((finding) => ({
      ...finding,
      dimension: "REST",
    })),
    ...dimensions.restCompatibleDownstreamBreakingChanges.findings.map(
      (finding) => ({ ...finding, dimension: "Downstream" }),
    ),
    ...dimensions.azureCompliance.findings.map((finding) => ({
      ...finding,
      dimension: "Compliance",
    })),
  ].sort(
    (left, right) =>
      (SEVERITY_ORDER[left.severity] ?? 99) -
        (SEVERITY_ORDER[right.severity] ?? 99) ||
      left.dimension.localeCompare(right.dimension) ||
      left.title.localeCompare(right.title),
  );
}

export function deriveCodeSafety(assessment) {
  const findings = allFindings(assessment);
  if (
    (assessment.errors ?? []).length > 0 ||
    findings.some((finding) => finding.severity === "high")
  ) {
    return "Low";
  }
  if (
    findings.length > 0 ||
    assessment.dimensions.azureCompliance.status === "not-assessed"
  ) {
    return "Medium";
  }
  return "High";
}

export function formatCodeSafety(safety) {
  const icons = { High: "🟢", Medium: "🟡", Low: "🔴" };
  return `${icons[safety]} ${safety}`;
}

export function formatConfidence(confidence) {
  const icons = { high: "🟢", medium: "🟡", low: "🔴" };
  return `${icons[confidence]} ${confidence}`;
}

function operationCount(assessment) {
  return assessment.dimensions.semanticUnderstanding.items.reduce(
    (total, item) => total + item.restRepresentation.operations.length,
    0,
  );
}

function changeCounts(assessment) {
  const counts = {
    added: 0,
    modified: 0,
    removed: 0,
  };
  for (const change of assessment.dimensions.semanticUnderstanding.items.flatMap(
    (item) => item.changes,
  )) {
    counts[change.kind] += change.operationIds.length;
  }
  return counts;
}

function projectCount(assessment) {
  if (Array.isArray(assessment.projects)) return assessment.projects.length;
  return new Set(
    (assessment.assessmentEvidence?.emitterRuns ?? []).map(
      (run) => run.project,
    ),
  ).size;
}

function formatDuration(milliseconds) {
  if (!Number.isFinite(milliseconds)) return "Unavailable";
  const seconds = Math.round(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes > 0 ? `${minutes}m ${seconds % 60}s` : `${seconds}s`;
}

function formatTotalAssessmentTime(duration) {
  if (!duration) return "Unavailable";
  if (duration.documentationReviewMs === null) {
    return "Unavailable; compliance time was not attributable per PR";
  }
  const time = formatDuration(duration.totalMs);
  const note = duration.note?.toLowerCase() ?? "";
  if (!note.includes("approximate")) return time;
  return `~${time}; includes approximate timing`;
}

function renderHeader(assessment) {
  const pr = assessment.pr
    ? `**PR:** [#${assessment.pr} - ${assessment.title}](${assessment.url})\n\n`
    : "";
  const baseline =
    assessment.baseline?.ref &&
    assessment.baseline.ref !== assessment.baseline.commit
      ? `${assessment.baseline.ref} (${assessment.baseline.commit})`
      : assessment.baseline?.commit;
  const workingTree =
    assessment.head?.hasWorkingTreeChanges === undefined
      ? ""
      : `; working-tree changes: ${assessment.head.hasWorkingTreeChanges}`;
  const scope = assessment.head?.changeScope;
  const includedScope =
    assessment.head?.hasWorkingTreeChanges && scope
      ? ` (${Object.entries(scope)
          .filter(([, included]) => included)
          .map(([name]) => name)
          .join(", ")})`
      : "";
  return `# 📋 TypeSpec Assessment

${pr}**Overall confidence:** ${formatConfidence(assessment.overallConfidence)}<br>
**Overall code safety:** ${formatCodeSafety(deriveCodeSafety(assessment))}

**Baseline:** ${code(baseline)}<br>
**Head:** ${code(assessment.head?.commit)}${workingTree}${includedScope}<br>
**Total assessment time:** ${formatTotalAssessmentTime(assessment.assessmentDuration)}
`;
}

function renderExecutiveSummary(assessment) {
  const dimensions = assessment.dimensions;
  const findings = allFindings(assessment);
  const changes = changeCounts(assessment);
  return `## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ${(assessment.errors ?? []).length > 0 ? "❌ Incomplete" : "✅ Assessed"} — ${dimensions.semanticUnderstanding.items.length} intent(s), ${operationCount(assessment)} operation(s) | n/a |
| REST compatibility | ${dimensions.restBreakingChanges.findings.length > 0 ? "❌ Issues found" : "✅ No breaks detected"} | ${dimensions.restBreakingChanges.findings.length} |
| Downstream compatibility | ${dimensions.restCompatibleDownstreamBreakingChanges.findings.length > 0 ? "❌ Issues found" : "✅ No breaks detected"} | ${dimensions.restCompatibleDownstreamBreakingChanges.findings.length} |
| Azure compliance | ${dimensions.azureCompliance.status === "passed" ? "✅ passed" : dimensions.azureCompliance.status === "failed" ? "❌ failed" : "⚠️ not-assessed"} | ${dimensions.azureCompliance.findings.length} |

**Scope:** ${dimensions.semanticUnderstanding.items.length} intent(s), ${operationCount(assessment)} affected operation(s), ${projectCount(assessment)} project(s).<br>
**Changes:** ${changes.added} added, ${changes.modified} modified, ${changes.removed} removed.<br>
**Highest severity:** ${findings[0]?.severity ?? "none"}.
`;
}

function renderActionRequired(assessment) {
  const findings = allFindings(assessment);
  const blockers =
    (assessment.errors ?? []).length > 0
      ? `Resolve the assessment blockers and rerun the assessment:

${assessment.errors.map((error) => `- ${text(error)}`).join("\n")}`
      : "";
  if (findings.length === 0) {
    if (blockers) {
      return `## 🎯 Action Required

${blockers}
`;
    }
    return `## 🎯 Action Required

No action required from the assessed dimensions.
`;
  }
  const rows = findings
    .map((finding) => {
      const document = assessment.dimensions.azureCompliance.documents.find(
        (candidate) => candidate.url === finding.documentationUrl,
      );
      const guidance = document
        ? `[${tableText(document.applicableGuidance)}](${document.url})`
        : finding.documentationUrl
          ? `[guidance](${finding.documentationUrl})`
          : "n/a";
      return `| ${finding.severity} | ${finding.dimension} | ${tableText(finding.title)} | ${tableText(finding.summary)} | ${sourceLinks(finding.sourceReferences, 2)} | ${guidance} |`;
    })
    .join("\n");
  return `## 🎯 Action Required

${blockers ? `${blockers}\n\n` : ""}| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
${rows}
`;
}

function renderFindingDetails(findings, emptyMessage) {
  if (findings.length === 0) return emptyMessage;
  return findings
    .map(
      (finding) => `<a id="finding-${slug(finding.id)}"></a>
### ${finding.title}

- **Severity:** ${finding.severity}
${finding.confidence ? `- **Confidence:** ${finding.confidence}\n` : ""}- **Summary:** ${finding.summary}
- **Evidence:** ${joinValues(finding.evidence)}
- **TypeSpec source:** ${sourceLinks(finding.sourceReferences)}
${finding.documentationUrl ? `- **Guidance:** ${finding.documentationUrl}` : ""}`,
    )
    .join("\n\n");
}

function renderComplianceFinding(finding, documents) {
  const document = documents.find(
    (candidate) => candidate.url === finding.documentationUrl,
  ) ?? {
    title: "Referenced guidance",
    section: "Unavailable",
    url: finding.documentationUrl,
    applicableGuidance: "Matching fetched guidance is unavailable.",
    evidence: joinValues(finding.evidence),
  };
  const actualSnippets = (finding.codeSnippets ?? [])
    .map((snippet) => {
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
      return `**[${sourceLabel(snippet)}](${link})**

\`\`\`tsp
${snippet.lines.join("\n")}
\`\`\``;
    })
    .join("\n\n");
  const expectedSnippets =
    document.expectedCodeStatus === "available"
      ? document.expectedCodeSnippets
          .map(
            (snippet) => `**${snippet.caption}**

\`\`\`${snippet.language}
${snippet.lines.join("\n")}
\`\`\``,
          )
          .join("\n\n")
      : `_${document.expectedCodeReason ?? "The fetched guidance does not contain an applicable code example."}_`;
  return `<a id="finding-${slug(finding.id)}"></a>
### ${finding.title}

**Severity:** ${finding.severity}

**Gap:** ${finding.summary}

<details>
<summary><strong>Expected</strong></summary>

${document.applicableGuidance}

**Guidance:** [${document.title} — ${document.section}](${document.url})

${expectedSnippets}

</details>

<details>
<summary><strong>Actual</strong></summary>

${document.evidence}

${actualSnippets}

</details>`;
}

function renderCompatibility(assessment) {
  const dimensions = assessment.dimensions;
  const blocked =
    (assessment.errors ?? []).length > 0
      ? "Not fully assessed because compilation did not complete."
      : "None detected.";
  return `## 🛡️ Compatibility Assessment

### REST Breaking Changes

${renderFindingDetails(dimensions.restBreakingChanges.findings, blocked)}

### Downstream Breaking Changes

${renderFindingDetails(
  dimensions.restCompatibleDownstreamBreakingChanges.findings,
  blocked,
)}
`;
}

function documentResult(document, findings) {
  return findings.some((finding) => finding.documentationUrl === document.url)
    ? "Mismatch"
    : "Matched";
}

function renderCompliance(assessment) {
  const compliance = assessment.dimensions.azureCompliance;
  const reason = compliance.reason ? `\n${compliance.reason}\n` : "";
  const findings =
    compliance.findings.length > 0
      ? compliance.findings
          .map((finding) =>
            renderComplianceFinding(finding, compliance.documents),
          )
          .join("\n\n")
      : "No compliance mismatches found.";
  return `## ☁️ Azure Compliance

**Status:** \`${compliance.status}\`
${reason}
### Compliance Findings

${findings}
`;
}

function renderGuidanceEvidence(assessment) {
  const compliance = assessment.dimensions.azureCompliance;
  const documents =
    compliance.documents.length === 0
      ? "No authoritative document evidence was available."
      : `| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
${compliance.documents
  .map(
    (document) =>
      `| ${documentResult(document, compliance.findings)} | [${tableText(document.title)} - ${tableText(document.section)}](${document.url}) | ${tableText(document.guidanceExcerpt)} | ${tableText(document.evidence)} | ${sourceLinks(document.sourceReferences, 3)} |`,
  )
  .join("\n")}`;
  return documents;
}

function renderChangeSummary(changes) {
  return `| Change | Aspect | Before | After |
| --- | --- | --- | --- |
${changes
  .flatMap((change) =>
    change.aspects.map(
      (aspect) =>
        `| ${CHANGE_KIND_LABELS[change.kind]} | ${tableText(aspect.field)} | ${aspect.before === null ? "—" : tableText(aspect.before)} | ${aspect.after === null ? "—" : tableText(aspect.after)} |`,
    ),
  )
  .join("\n")}`;
}

function diffRange(start, count) {
  return count === 1 ? `${start}` : `${start},${count}`;
}

function lineIndexForReference(hunk, sourceReferences) {
  const reference = sourceReferences.find(
    (candidate) => candidate.path === hunk.path,
  );
  if (!reference) return 0;
  let oldLine = hunk.oldStart;
  let newLine = hunk.newStart;
  for (const [index, line] of hunk.lines.entries()) {
    const appliesToReference =
      reference.revision === "base"
        ? !line.startsWith("+") && oldLine >= reference.startLine
        : !line.startsWith("-") && newLine >= reference.startLine;
    if (appliesToReference) return index;
    if (!line.startsWith("+")) oldLine += 1;
    if (!line.startsWith("-")) newLine += 1;
  }
  return 0;
}

function relevantDecoratorMarkers(change) {
  const cause = change.typeSpecCause.toLowerCase();
  const markers = explicitDecoratorMarkers(change);
  if (change.kind === "added" || /\badd(?:ed)?\b/.test(cause)) {
    markers.push("@added");
  }
  if (change.kind === "removed" || /\bremove[ds]?\b/.test(cause)) {
    markers.push("@removed");
  }
  return [...new Set(markers)];
}

function explicitDecoratorMarkers(change) {
  return [...change.typeSpecCause.matchAll(/`(@[a-zA-Z][\w.]*)/g)].map(
    (match) => match[1].toLowerCase(),
  );
}

function relevantTypeSpecSymbols(change) {
  const ignored = new Set([
    "Add",
    "After",
    "Before",
    "Mark",
    "Remove",
    "Removed",
    "TypeSpec",
    "Versions",
  ]);
  return [
    ...new Set(
      [
        ...change.typeSpecCause.matchAll(
          /\b[A-Z][A-Za-z0-9]*(?:[A-Z][A-Za-z0-9]*)+\b/g,
        ),
        ...change.typeSpecCause.matchAll(/`([A-Za-z_][A-Za-z0-9_]*)`/g),
      ]
        .map((match) => match[1] ?? match[0])
        .filter((value) => !ignored.has(value)),
    ),
  ];
}

function relevantLineIndex(hunk, change) {
  const symbols = relevantTypeSpecSymbols(change);
  const declarationIndex = hunk.lines.findIndex(
    (line) =>
      /^\+?\s*(?:model|interface|enum|union|scalar|alias|op)\s/.test(line) &&
      symbols.some((symbol) => line.includes(symbol)),
  );
  if (declarationIndex >= 0) return declarationIndex;
  const markers = relevantDecoratorMarkers(change);
  const markerIndex = hunk.lines.findIndex((line) =>
    markers.some((marker) => line.toLowerCase().includes(marker)),
  );
  if (markerIndex >= 0) return markerIndex;
  return hunk.lines.findIndex((line) =>
    symbols.some((symbol) => line.includes(symbol)),
  );
}

export function displayedHunkLines(
  hunk,
  sourceReferences,
  change,
  focusIndex = undefined,
) {
  const maximumLines = 12;
  if (hunk.lines.length <= maximumLines) return hunk.lines;
  const relevantIndex = relevantLineIndex(hunk, change);
  const targetIndex = Number.isInteger(focusIndex)
    ? focusIndex
    : relevantIndex >= 0
      ? relevantIndex
      : lineIndexForReference(hunk, sourceReferences);
  const start = Math.max(
    0,
    Math.min(targetIndex - 2, hunk.lines.length - maximumLines),
  );
  const end = start + maximumLines;
  const result = hunk.lines.slice(start, end);
  if (start > 0) {
    result.unshift(
      ` ... ${start} earlier diff lines omitted; full hunk is in assessment.json ...`,
    );
  }
  if (end < hunk.lines.length) {
    result.push(
      ` ... ${hunk.lines.length - end} later diff lines omitted; full hunk is in assessment.json ...`,
    );
  }
  return result;
}

export function displayedTypeSpecHunks(hunks, change) {
  if (hunks.length <= 2) return { hunks, omittedCount: 0 };
  const markers = relevantDecoratorMarkers(change);
  const selected = markers
    .map((marker) =>
      hunks.find((hunk) =>
        hunk.lines.some((line) => line.toLowerCase().includes(marker)),
      ),
    )
    .filter((hunk, index, matches) => hunk && matches.indexOf(hunk) === index)
    .slice(0, 2);
  for (const hunk of hunks) {
    if (selected.length === 2) break;
    if (!selected.includes(hunk)) selected.push(hunk);
  }
  return {
    hunks: selected,
    omittedCount: hunks.length - 2,
  };
}

function candidateFocuses(hunks, change) {
  const candidates = [];
  const addCandidate = (hunk, focusIndex, priority) => {
    if (focusIndex < 0) return;
    if (
      candidates.some(
        (candidate) =>
          candidate.hunk === hunk &&
          Math.abs(candidate.focusIndex - focusIndex) <= 6,
      )
    ) {
      return;
    }
    candidates.push({ hunk, focusIndex, priority });
  };
  for (const marker of explicitDecoratorMarkers(change)) {
    for (const hunk of hunks) {
      addCandidate(
        hunk,
        hunk.lines.findIndex((line) => line.toLowerCase().includes(marker)),
        0,
      );
    }
  }
  for (const symbol of relevantTypeSpecSymbols(change)) {
    for (const hunk of hunks) {
      for (const [index, line] of hunk.lines.entries()) {
        if (!line.includes(symbol)) continue;
        const declaration = line.match(
          /^\+?\s*(?:(model|interface|enum|union|scalar|alias|op)\s+[A-Za-z_][A-Za-z0-9_]*|([A-Za-z_][A-Za-z0-9_]*)\s+is\b)/,
        );
        if (!declaration) continue;
        const kind = declaration[1] ?? "operation";
        const priority = ["model", "interface", "op", "operation"].includes(
          kind,
        )
          ? 1
          : 2;
        addCandidate(hunk, index, priority);
      }
    }
  }
  for (const marker of relevantDecoratorMarkers(change)) {
    for (const hunk of hunks) {
      addCandidate(
        hunk,
        hunk.lines.findIndex((line) => line.toLowerCase().includes(marker)),
        2,
      );
    }
  }
  return candidates.sort((left, right) => left.priority - right.priority);
}

export function displayedTypeSpecExcerpts(hunks, change) {
  const excerpts = candidateFocuses(hunks, change).slice(0, 2);
  for (const hunk of hunks) {
    if (excerpts.length === 2) break;
    if (!excerpts.some((excerpt) => excerpt.hunk === hunk)) {
      excerpts.push({
        hunk,
        focusIndex: relevantLineIndex(hunk, change),
        priority: 3,
      });
    }
  }
  const displayedHunkCount = new Set(excerpts.map((excerpt) => excerpt.hunk))
    .size;
  return {
    excerpts,
    omittedCount: Math.max(0, hunks.length - displayedHunkCount),
  };
}

function renderTypeSpecHunk(hunk, sourceReferences, change, focusIndex) {
  const oldPath = hunk.oldCount === 0 ? "/dev/null" : `a/${hunk.path}`;
  const newPath = hunk.newCount === 0 ? "/dev/null" : `b/${hunk.path}`;
  const context = hunk.context ? ` ${hunk.context}` : "";
  return `\`\`\`diff
--- ${oldPath}
+++ ${newPath}
@@ -${diffRange(hunk.oldStart, hunk.oldCount)} +${diffRange(hunk.newStart, hunk.newCount)} @@${context}
${displayedHunkLines(hunk, sourceReferences, change, focusIndex)
  .map((line) => (line === " " ? "" : line))
  .join("\n")}
\`\`\``;
}

function impactFindingsById(assessment) {
  return new Map(
    [
      ...assessment.dimensions.restBreakingChanges.findings,
      ...assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings,
      ...assessment.dimensions.azureCompliance.findings,
    ].map((finding) => [finding.id, finding]),
  );
}

function renderKeyChanges(assessment) {
  const findings = impactFindingsById(assessment);
  return assessment.dimensions.semanticUnderstanding.items
    .map((item, index) => {
      const anchor = `intent-${index + 1}-${slug(item.intent).slice(0, 48)}`;
      const changeDetails = item.changes
        .map((change) => {
          const displayedDiffs = displayedTypeSpecExcerpts(
            change.typeSpecDiffs,
            change,
          );
          const impacts = change.linkedFindingIds
            .map((id) => findings.get(id))
            .filter(Boolean)
            .map(
              (finding) => `[${finding.title}](#finding-${slug(finding.id)})`,
            );
          return `**TypeSpec change:** ${change.typeSpecCause}

${displayedDiffs.excerpts
  .map(({ hunk, focusIndex }) =>
    renderTypeSpecHunk(hunk, change.sourceReferences, change, focusIndex),
  )
  .join("\n\n")}

${displayedDiffs.omittedCount > 0 ? `${displayedDiffs.omittedCount} additional TypeSpec hunk${displayedDiffs.omittedCount === 1 ? "" : "s"} omitted; complete diffs are in \`assessment.json\`.\n\n` : ""}${impacts.length > 0 ? `**Impact:** ${impacts.join(", ")}<br>\n` : ""}**Source:** ${sourceLinks(change.sourceReferences)}`;
        })
        .join("\n\n");
      return `<a id="${anchor}"></a>
### ${index + 1}. ${item.intent}

${renderChangeSummary(item.changes)}

${changeDetails}`;
    })
    .join("\n\n");
}

function comparisonPromptContext(assessment) {
  if (assessment.pr) return `PR #${assessment.pr}`;
  const baseline =
    assessment.baseline?.ref ?? assessment.baseline?.commit ?? "<baseline>";
  if (assessment.head?.hasWorkingTreeChanges) {
    return `changes from ${baseline} to the current working tree`;
  }
  return `changes from ${baseline} to ${assessment.head?.commit ?? "<head>"}`;
}

function renderRestRepresentationPrompt(assessment) {
  const context = comparisonPromptContext(assessment);
  return `Need the complete REST representation for every affected operation? Use this prompt:

\`Using assessment.json for ${context}, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.\`
`;
}

function renderToolingUsed(assessment) {
  const runs = assessment.assessmentEvidence?.emitterRuns ?? [];
  const tooling = [...new Set(runs.map((run) => run.emitter).filter(Boolean))];
  if (tooling.length === 0) return "No emitter or library usage recorded.";
  return tooling.map((name) => `- ${code(name)}`).join("\n");
}

function renderAppendix(assessment) {
  const errors =
    (assessment.errors ?? []).length === 0
      ? "None."
      : assessment.errors.map((error) => `- ${error}`).join("\n");
  const artifactEvidence = assessment.artifactEvidence
    ? Object.entries(assessment.artifactEvidence)
        .map(([name, evidence]) => `- **${name}:** ${evidence}`)
        .join("\n")
    : "No aggregate artifact evidence recorded.";
  return `## 📎 Appendix

### Assessment Errors

${errors}

### Code-to-Guidance Evidence

${renderGuidanceEvidence(assessment)}

### Tooling Used

${renderToolingUsed(assessment)}

### Artifact Evidence

${artifactEvidence}
`;
}

export function renderAssessment(assessment) {
  return [
    renderHeader(assessment),
    renderExecutiveSummary(assessment),
    renderActionRequired(assessment),
    "## 🧠 Semantic Understanding\n",
    renderKeyChanges(assessment),
    "",
    renderRestRepresentationPrompt(assessment),
    renderCompatibility(assessment),
    renderCompliance(assessment),
    renderAppendix(assessment),
  ].join("\n");
}

function main() {
  const [jsonPath, markdownPath] = process.argv.slice(2);
  if (!jsonPath || !markdownPath) {
    process.stderr.write(
      "Usage: render-assessment.mjs <assessment.json> <assessment.md>\n",
    );
    process.exitCode = 1;
    return;
  }
  const assessment = JSON.parse(readFileSync(resolve(jsonPath), "utf8"));
  writeFileSync(resolve(markdownPath), renderAssessment(assessment));
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  main();
}
