import { readFileSync, writeFileSync } from "node:fs";
import { basename, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SEVERITY_ORDER = { high: 0, medium: 1, low: 2 };

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
  return `# 📋 TypeSpec Assessment

${pr}**Overall confidence:** ${formatConfidence(assessment.overallConfidence)}<br>
**Overall code safety:** ${formatCodeSafety(deriveCodeSafety(assessment))}

**Baseline:** ${code(baseline)}<br>
**Head:** ${code(assessment.head?.commit)}${workingTree}<br>
**Total assessment time:** ${formatTotalAssessmentTime(assessment.assessmentDuration)}
`;
}

function renderExecutiveSummary(assessment) {
  const dimensions = assessment.dimensions;
  const findings = allFindings(assessment);
  return `## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ${(assessment.errors ?? []).length > 0 ? "❌ Incomplete" : "✅ Assessed"} — ${dimensions.semanticUnderstanding.items.length} intent(s), ${operationCount(assessment)} operation(s) | n/a |
| REST compatibility | ${dimensions.restBreakingChanges.findings.length > 0 ? "❌ Issues found" : "✅ No breaks detected"} | ${dimensions.restBreakingChanges.findings.length} |
| Downstream compatibility | ${dimensions.restCompatibleDownstreamBreakingChanges.findings.length > 0 ? "❌ Issues found" : "✅ No breaks detected"} | ${dimensions.restCompatibleDownstreamBreakingChanges.findings.length} |
| Azure compliance | ${dimensions.azureCompliance.status === "passed" ? "✅ passed" : dimensions.azureCompliance.status === "failed" ? "❌ failed" : "⚠️ not-assessed"} | ${dimensions.azureCompliance.findings.length} |

**Scope:** ${dimensions.semanticUnderstanding.items.length} intent(s), ${operationCount(assessment)} affected operation(s), ${projectCount(assessment)} project(s).<br>
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

function renderChangeOverview(assessment) {
  const rows = assessment.dimensions.semanticUnderstanding.items
    .map((item, index) => {
      const operations = item.restRepresentation.operations;
      const versions = [
        ...new Set(operations.flatMap((operation) => operation.apiVersions)),
      ];
      const anchor = `intent-${index + 1}-${slug(item.intent).slice(0, 48)}`;
      return `| ${index + 1} | ${tableText(item.intent)} | ${operations.length} | ${tableText(versions.join(", "))} | [details](#${anchor}) |`;
    })
    .join("\n");
  return `### Change Overview

| # | Intent | Operations | API versions | Details |
| ---: | --- | ---: | --- | --- |
${rows}
`;
}

function renderFindingDetails(findings, emptyMessage) {
  if (findings.length === 0) return emptyMessage;
  return findings
    .map(
      (finding) => `### ${finding.title}

- **Severity:** ${finding.severity}
${finding.confidence ? `- **Confidence:** ${finding.confidence}\n` : ""}- **Summary:** ${finding.summary}
- **Evidence:** ${joinValues(finding.evidence)}
- **TypeSpec source:** ${sourceLinks(finding.sourceReferences)}
${finding.documentationUrl ? `- **Guidance:** ${finding.documentationUrl}` : ""}`,
    )
    .join("\n\n");
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
  const findings = renderFindingDetails(
    compliance.findings,
    "No compliance mismatches found.",
  );
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

function describeLro(lro) {
  if (!lro.isLongRunning) return "No";
  return [lro.pattern, lro.finalStateVia ? `via ${lro.finalStateVia}` : null]
    .filter(Boolean)
    .join("; ");
}

function describePaging(paging) {
  if (!paging.isPaged) return "No";
  return [paging.itemType, paging.nextLinkName].filter(Boolean).join("; ");
}

function renderSemanticUnderstanding(assessment) {
  return assessment.dimensions.semanticUnderstanding.items
    .map((item, index) => {
      const anchor = `intent-${index + 1}-${slug(item.intent).slice(0, 48)}`;
      const operations = item.restRepresentation.operations
        .map(
          (operation) => `#### ${code(operation.operationId)}

- **HTTP path:** ${code(operation.signature)}
- **API versions:** ${operation.apiVersions.map(code).join(", ")}
- **Parameters:** ${operation.parameters.join("; ") || "None."}
- **Request payload:** ${operation.requestPayload}
- **Response payloads:** ${operation.responsePayloads.join("; ")}
- **Service behavior:** ${operation.serviceBehavior}
- **LRO:** ${describeLro(operation.lro)}${operation.lro.isLongRunning ? `; ${text(operation.lro.polling)}; final result: ${text(operation.lro.finalResult)}` : "."}
- **Paging:** ${describePaging(operation.paging)}${operation.paging.isPaged ? `; ${text(operation.paging.continuation)}` : "."}
- **TypeSpec source:** ${sourceLinks(operation.sourceReferences)}`,
        )
        .join("\n\n");
      return `<a id="${anchor}"></a>
### ${index + 1}. ${item.intent}

**Confidence:** ${item.confidence}<br>
**REST summary:** ${item.restRepresentation.summary}

${operations}`;
    })
    .join("\n\n");
}

function renderToolingUsed(assessment) {
  const runs = assessment.assessmentEvidence?.emitterRuns ?? [];
  const tooling = [...new Set(runs.map((run) => run.emitter).filter(Boolean))];
  if (tooling.length === 0) return "No emitter or library usage recorded.";
  return tooling.map((name) => `- ${code(name)}`).join("\n");
}

function renderRepositoryValidation(assessment) {
  const validations = assessment.assessmentEvidence?.repositoryValidation ?? [];
  if (validations.length === 0) {
    return "No repository-validation evidence recorded.";
  }
  return `| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
${validations
  .map(
    (validation) =>
      `| ${code(validation.project)} | ${code(validation.tool)} | ${validation.status} | ${formatDuration(validation.durationMs)} | ${code(validation.log)} |`,
  )
  .join("\n")}`;
}

function renderChangedSources(assessment) {
  const references = assessment.assessmentEvidence?.changedTypeSpec ?? [];
  const byPath = new Map();
  for (const reference of references) {
    const group = byPath.get(reference.path) ?? [];
    group.push(reference);
    byPath.set(reference.path, group);
  }
  return [...byPath.entries()]
    .map(
      ([path, pathReferences]) =>
        `- ${code(path)}: ${sourceLinks(pathReferences)}`,
    )
    .join("\n");
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

### Repository Validation

${renderRepositoryValidation(assessment)}

### Artifact Evidence

${artifactEvidence}

### Changed TypeSpec

${renderChangedSources(assessment)}
`;
}

export function renderAssessment(assessment) {
  return [
    renderHeader(assessment),
    renderExecutiveSummary(assessment),
    renderActionRequired(assessment),
    "## 🧠 Semantic Understanding\n",
    renderChangeOverview(assessment),
    "### Operation Details\n",
    renderSemanticUnderstanding(assessment),
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
