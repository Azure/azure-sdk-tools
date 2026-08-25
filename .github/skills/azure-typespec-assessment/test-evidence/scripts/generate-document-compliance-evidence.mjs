#!/usr/bin/env node

import { readFileSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { renderAssessmentHtml } from "../../scripts/render-assessment-html.mjs";
import { linkImpactFindings } from "./operation-changes.mjs";

const fixture = JSON.parse(
  readFileSync(
    new URL("../fixtures/recent-pr-compliance.json", import.meta.url),
    "utf8",
  ),
);

function parseEvidenceInputs(values) {
  return new Map(
    values.map((value) => {
      const separator = value.indexOf("=");
      if (separator < 1) {
        throw new Error(`Expected <pr>=<evidence.json>, received: ${value}`);
      }
      return [value.slice(0, separator), resolve(value.slice(separator + 1))];
    }),
  );
}

function selectedSources(assessment, entry, label) {
  if (entry.sourceReferences?.length > 0) return entry.sourceReferences;
  if (entry.sourcePathIncludes?.length > 0) {
    const sources = [
      ...assessment.dimensions.semanticUnderstanding.items.flatMap(
        (item) => item.sourceReferences,
      ),
      ...(assessment.assessmentEvidence?.changedTypeSpec ?? []),
    ].filter((source) =>
      entry.sourcePathIncludes.some((value) => source.path.includes(value)),
    );
    if (sources.length === 0) {
      throw new Error(
        `${label} sourcePathIncludes matched no source evidence.`,
      );
    }
    return [
      ...new Map(
        sources.map((source) => [
          `${source.path}:${source.revision}:${source.startLine}:${source.endLine}`,
          source,
        ]),
      ).values(),
    ];
  }
  if (
    !Array.isArray(entry.semanticItemIds) ||
    entry.semanticItemIds.length === 0
  ) {
    throw new Error(
      `${label} requires sourceReferences or explicit semanticItemIds.`,
    );
  }
  const items = assessment.dimensions.semanticUnderstanding.items;
  const selected = entry.semanticItemIds.map((id) => {
    const item = items.find((candidate) => candidate.id === id);
    if (!item) {
      throw new Error(`${label} references unknown semantic item: ${id}`);
    }
    return item;
  });
  const sources = selected.flatMap((item) => item.sourceReferences);
  return [
    ...new Map(
      sources.map((source) => [
        `${source.path}:${source.revision}:${source.startLine}:${source.endLine}`,
        source,
      ]),
    ).values(),
  ];
}

export function buildCompliance(assessment, specification) {
  const documents = specification.documents.map((document, index) => {
    const { semanticItemIds, sourcePathIncludes, ...content } = document;
    return {
      ...content,
      sourceReferences: selectedSources(
        assessment,
        document,
        `Compliance document ${index + 1}`,
      ),
    };
  });
  return {
    status: specification.status,
    ...(specification.reason ? { reason: specification.reason } : {}),
    ...(specification.status === "not-assessed"
      ? {}
      : {
          summary: {
            patternsAssessed: documents.length,
            findingCount: specification.findings?.length ?? 0,
          },
        }),
    documents,
    findings: (specification.findings ?? []).map((finding, index) => {
      const { semanticItemIds, sourcePathIncludes, ...content } = finding;
      const supportingDocument = documents.find(
        (document) => document.url === finding.documentationUrl,
      );
      const codeSnippets =
        content.codeSnippets ?? supportingDocument?.codeSnippets;
      const sourceReferences =
        codeSnippets === supportingDocument?.codeSnippets
          ? supportingDocument.sourceReferences
          : selectedSources(
              assessment,
              finding,
              `Compliance finding ${index + 1}`,
            );
      for (const snippet of codeSnippets ?? []) {
        const reference = sourceReferences.find(
          (candidate) => candidate.path === snippet.path,
        );
        if (!reference) continue;
        reference.startLine = Math.min(reference.startLine, snippet.startLine);
        reference.endLine = Math.max(reference.endLine, snippet.endLine);
        reference.link = reference.link.replace(
          /#L\d+-L\d+$/,
          `#L${reference.startLine}-L${reference.endLine}`,
        );
      }
      return {
        ...content,
        ...(codeSnippets ? { codeSnippets } : {}),
        sourceReferences,
      };
    }),
  };
}

function preparationDuration(evidence) {
  if (Number.isInteger(evidence.durationMs)) return evidence.durationMs;
  return evidence.projects.reduce((total, project) => {
    const bySide = new Map();
    for (const compilation of project.compilations ?? []) {
      bySide.set(
        compilation.side,
        (compilation.emitters ?? []).reduce(
          (sum, emitter) => sum + (emitter.durationMs ?? 0),
          0,
        ),
      );
    }
    return total + Math.max(0, ...bySide.values());
  }, 0);
}

function formatDuration(milliseconds) {
  const seconds = Math.round(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes > 0 ? `${minutes}m ${seconds % 60}s` : `${seconds}s`;
}

function updateSummary(path, assessments) {
  const existing = readFileSync(path, "utf8");
  const intro = existing.slice(0, existing.indexOf("| PR |"));
  const rows = assessments.map((assessment) => {
    const operations = assessment.dimensions.semanticUnderstanding.items.reduce(
      (count, item) => count + item.restRepresentation.operations.length,
      0,
    );
    const compliance = assessment.dimensions.azureCompliance;
    return `| [${assessment.pr}](assessments/${assessment.pr}/assessment.html) | ${assessment.overallConfidence} | ${operations} | ${assessment.dimensions.restBreakingChanges.findings.length} | ${assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings.length} | ${compliance.status} | ${compliance.documents.length} | ${formatDuration(assessment.assessmentDuration.preparationMs)} | ${assessment.errors.length} |`;
  });
  writeFileSync(
    path,
    `${intro}| PR | Confidence | Operations | REST findings | Downstream findings | Compliance | Documents | Measured preparation time | Errors |\n| --- | --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |\n${rows.join("\n")}\n`,
  );
}

function main() {
  const [rootValue, ...inputValues] = process.argv.slice(2);
  if (!rootValue) {
    throw new Error(
      "Usage: generate-document-compliance-evidence.mjs <test-evidence-root> <pr>=<evidence.json> ...",
    );
  }
  const root = resolve(rootValue);
  const inputs = parseEvidenceInputs(inputValues);
  const aggregatePath = join(root, "assessments.json");
  const aggregate = JSON.parse(readFileSync(aggregatePath, "utf8"));
  const outputs = [];
  for (const assessment of aggregate.assessments) {
    const pr = String(assessment.pr);
    const specification = fixture[pr];
    const evidencePath = inputs.get(pr);
    if (!specification || !evidencePath) {
      throw new Error(
        `Missing compliance specification or evidence for PR ${pr}.`,
      );
    }
    const compliance = buildCompliance(assessment, specification);
    const preparationMs = preparationDuration(
      JSON.parse(readFileSync(evidencePath, "utf8")),
    );
    const duration = {
      preparationMs,
      documentationReviewMs: null,
      totalMs: preparationMs,
      note: "Documentation assessment was performed as one shared 11-PR batch, so per-PR assessment time is unavailable and was not estimated.",
    };
    assessment.dimensions.azureCompliance = compliance;
    assessment.assessmentDuration = duration;
    const directory = join(root, "assessments", pr);
    const jsonPath = join(directory, "assessment.json");
    const htmlPath = join(directory, "assessment.html");
    const standalone = JSON.parse(readFileSync(jsonPath, "utf8"));
    standalone.dimensions.azureCompliance = compliance;
    standalone.assessmentDuration = duration;
    linkImpactFindings(standalone);
    outputs.push({
      jsonPath,
      json: `${JSON.stringify(standalone, null, 2)}\n`,
      htmlPath,
      html: renderAssessmentHtml(standalone),
    });
  }
  for (const output of outputs) {
    writeFileSync(output.jsonPath, output.json);
    writeFileSync(output.htmlPath, output.html);
  }
  aggregate.generatedAt = new Date().toISOString();
  writeFileSync(aggregatePath, `${JSON.stringify(aggregate, null, 2)}\n`);
  updateSummary(join(root, "assessment-summary.md"), aggregate.assessments);
  process.stdout.write(
    `Generated documentation-grounded compliance evidence for ${aggregate.assessments.length} PRs.\n`,
  );
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  main();
}
