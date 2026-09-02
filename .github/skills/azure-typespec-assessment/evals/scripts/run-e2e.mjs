#!/usr/bin/env node

import {
  existsSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { renderAssessmentHtml } from "../../scripts/render-assessment-html.mjs";
import { validateAssessment } from "../../scripts/validate-assessment.mjs";

const evidenceRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function parseArgs(argv) {
  const options = {
    case: "all",
    root: evidenceRoot,
    output: join(evidenceRoot, "outputs"),
    checkCanonicalHtml: true,
  };
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--case") {
      options.case = argv[++index];
    } else if (argument === "--root") {
      options.root = resolve(argv[++index]);
    } else if (argument === "--output") {
      options.output = resolve(argv[++index]);
    } else if (argument === "--skip-canonical-html-check") {
      options.checkCanonicalHtml = false;
    } else {
      throw new Error(`Unknown argument: ${argument}`);
    }
  }
  if (!options.case) throw new Error("--case requires a PR number or 'all'");
  return options;
}

function findingCounts(assessment) {
  if (assessment.schemaVersion === 1) {
    return {
      semanticIntents: assessment.dimensions.semantic.items.length,
      restBreakingFindings: assessment.dimensions.rest.findings.length,
      downstreamFindings: assessment.dimensions.downstream.findings.length,
      complianceFindings: assessment.dimensions.compliance.findings.length,
    };
  }
  return {
    semanticIntents: assessment.dimensions.semanticUnderstanding.items.length,
    restBreakingFindings:
      assessment.dimensions.restBreakingChanges.findings.length,
    downstreamFindings:
      assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings
        .length,
    complianceFindings: assessment.dimensions.azureCompliance.findings.length,
  };
}

function assertEqual(actual, expected, description) {
  if (actual !== expected) {
    throw new Error(`${description}: expected ${expected}, received ${actual}`);
  }
}

function assertMajorReportPoints(html, assessment, description) {
  const required = [
    "REST breaking changes",
    "Downstream breaking changes",
    "Semantic intents",
    "Azure Guidelines",
    "Document Quality",
    "Not assessed",
    "Appendix",
  ];
  for (const value of required) {
    if (value && !html.toLowerCase().includes(value.toLowerCase())) {
      throw new Error(`${description}: missing ${value}`);
    }
  }
}

export function loadCases(root = evidenceRoot) {
  const cases = JSON.parse(readFileSync(join(root, "cases.json"), "utf8"));
  if (!Array.isArray(cases) || cases.length !== 11) {
    throw new Error(`cases.json must define exactly 11 cases`);
  }
  const prs = new Set(cases.map(({ pr }) => pr));
  if (prs.size !== cases.length) {
    throw new Error("cases.json contains duplicate PR numbers");
  }
  return cases;
}

export function replayCase(testCase, options = {}) {
  const root = resolve(options.root ?? evidenceRoot);
  const outputRoot = resolve(options.output ?? join(root, "outputs"));
  const canonicalRoot = join(root, "assessments", String(testCase.pr));
  const jsonPath = join(canonicalRoot, "assessment.json");
  const htmlPath = join(canonicalRoot, "assessment.html");
  const assessment = JSON.parse(readFileSync(jsonPath, "utf8"));
  const startedAt = process.hrtime.bigint();

  const validationErrors = validateAssessment(assessment);
  if (validationErrors.length > 0) {
    throw new Error(
      `PR ${testCase.pr} assessment is invalid:\n${validationErrors.join("\n")}`,
    );
  }
  assertEqual(assessment.pr, testCase.pr, `PR ${testCase.pr} identity`);
  assertEqual(
    assessment.baseline?.commit ?? assessment.comparison?.baseCommit,
    testCase.baseCommit,
    `PR ${testCase.pr} baseline`,
  );
  assertEqual(
    assessment.head?.commit ?? assessment.comparison?.headCommit,
    testCase.headCommit,
    `PR ${testCase.pr} head`,
  );
  const actualCounts = findingCounts(assessment);
  for (const [name, expected] of Object.entries(testCase.expected)) {
    assertEqual(actualCounts[name], expected, `PR ${testCase.pr} ${name}`);
  }

  const html = renderAssessmentHtml(assessment);
  assertMajorReportPoints(html, assessment, `PR ${testCase.pr} HTML`);
  if (options.checkCanonicalHtml !== false && existsSync(htmlPath)) {
    const acceptedHtml = readFileSync(htmlPath, "utf8");
    for (const section of ["REST breaking changes", "Semantic intents", "Appendix"]) {
      if (!acceptedHtml.includes(section)) {
        throw new Error(`PR ${testCase.pr} accepted HTML is missing ${section}`);
      }
    }
  }

  const caseOutput = join(outputRoot, String(testCase.pr));
  mkdirSync(caseOutput, { recursive: true });
  writeFileSync(
    join(caseOutput, "assessment.json"),
    `${JSON.stringify(assessment, null, 2)}\n`,
  );
  writeFileSync(join(caseOutput, "assessment.html"), html);
  const elapsedMs = Math.round(
    Number(process.hrtime.bigint() - startedAt) / 1_000_000,
  );
  const result = {
    pr: testCase.pr,
    baseCommit: testCase.baseCommit,
    headCommit: testCase.headCommit,
    projects: testCase.projects,
    expected: testCase.expected,
    elapsedMs,
    output: caseOutput,
  };
  writeFileSync(
    join(caseOutput, "result.json"),
    `${JSON.stringify(result, null, 2)}\n`,
  );
  return result;
}

export function runE2e(options = {}) {
  const root = resolve(options.root ?? evidenceRoot);
  const cases = loadCases(root);
  const selected =
    options.case === undefined || options.case === "all"
      ? cases
      : cases.filter(({ pr }) => String(pr) === String(options.case));
  if (selected.length === 0) {
    throw new Error(`Unknown assessment case: ${options.case}`);
  }
  const results = selected.map((testCase) =>
    replayCase(testCase, { ...options, root }),
  );
  const outputRoot = resolve(options.output ?? join(root, "outputs"));
  mkdirSync(outputRoot, { recursive: true });
  const summary = {
    caseCount: results.length,
    prs: results.map(({ pr }) => pr),
    totalElapsedMs: results.reduce(
      (total, { elapsedMs }) => total + elapsedMs,
      0,
    ),
    results: results.map(({ pr, elapsedMs, output }) => ({
      pr,
      elapsedMs,
      output,
    })),
  };
  writeFileSync(
    join(outputRoot, "summary.json"),
    `${JSON.stringify(summary, null, 2)}\n`,
  );
  return summary;
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  try {
    const summary = runE2e(parseArgs(process.argv.slice(2)));
    process.stdout.write(
      `Replayed ${summary.caseCount} assessment case(s): ${summary.prs.join(", ")}.\n`,
    );
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
