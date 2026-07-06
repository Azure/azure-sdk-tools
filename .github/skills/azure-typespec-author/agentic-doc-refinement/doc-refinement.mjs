#!/usr/bin/env node
// @ts-check
//
// Code-based agentic doc-refinement loop for the azure-typespec-author skill,
// driven programmatically by the GitHub Copilot SDK (@github/copilot-sdk).
//
// This is the "run it as code" alternative to the GitHub Actions workflow
// (.github/workflows/typespec-author-doc-refinement.yml). Use it when the
// agentic workflow cannot run the Vally evals itself (Vally needs the internal
// QA-bot KB backend + azsdk-cli MCP server, which only exist on the ADO
// benchmark pool / a properly set-up dev box). Run this script *in that same
// environment* so step 3 (Vally) can execute locally.
//
// Steps (see README.md):
//   1. Agent updates reference documents          (prompts/01-update-reference-docs.md)
//   2. Agent updates the skill markdown if needed (prompts/02-update-skill.md)
//   3. Run the Vally code-quality (forced) evals  (vally CLI, local)
//   4. Agent analyzes results / attributes gaps   (prompts/04-analyze-results.md)
//   5. Agent generates the gap report             (prompts/05-generate-report.md)
//
// Prereqs:
//   - `npm ci` in this folder (installs @github/copilot-sdk, the @github/copilot
//     CLI it spawns, and @microsoft/vally-cli).
//   - GitHub Copilot CLI authenticated (the SDK spawns the bundled copilot CLI), OR
//     pass a token via COPILOT_GITHUB_TOKEN. Override the CLI with COPILOT_CLI_PATH.
//   - For step 3: the eval fixtures set up (../evaluate/scripts/setup-environment.js),
//     the azsdk-cli MCP server buildable, and the KB backend reachable
//     (AZURE_SDK_KB_ENDPOINT / localhost:8088). Skip step 3 with --skip-eval and
//     point --results-dir at an existing run to only (re)generate the report.
//
// Usage:
//   npm run refine                                 # full loop, default forced suites
//   node doc-refinement.mjs --skip-docs            # skip steps 1-2, just eval + report
//   node doc-refinement.mjs --skip-eval \
//        --results-dir ../evaluate/result          # only steps 4-5 over existing jsonl
//   node doc-refinement.mjs --suite warning-forced # limit to one suite
//   node doc-refinement.mjs --model claude-opus-4.6 --idle-timeout 1800

import { CopilotClient, approveAll } from "@github/copilot-sdk";
import { spawn } from "node:child_process";
import { readFile, mkdir, readdir, stat } from "node:fs/promises";
import { readFileSync, existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
// This folder: .github/skills/azure-typespec-author/agentic-doc-refinement → repo root is 4 up.
const REPO_ROOT = path.resolve(__dirname, "..", "..", "..", "..");
const SKILL_DIR = path.resolve(__dirname, ".."); // azure-typespec-author
const SKILLS_ROOT = path.resolve(__dirname, "..", ".."); // .github/skills
const EVAL_DIR = path.join(SKILL_DIR, "evaluate");
const PROMPTS_DIR = path.join(__dirname, "prompts");
const RESULT_DIR = path.join(EVAL_DIR, "result");
const WORKSPACE_DIR = path.join(EVAL_DIR, "debug");
const REPORT_PATH = path.join(RESULT_DIR, "document-gaps.md");

// Default code-quality suite: the consolidated `forced` suite (all forced-mode cases).
const DEFAULT_SUITES = ["forced"];

/**
 * Resolve the Copilot CLI entry point from the installed @github/copilot package's
 * `bin` field. Returns undefined if it can't be found (SDK falls back to its own
 * resolver / COPILOT_CLI_PATH).
 * @returns {string | undefined}
 */
function resolveCopilotCliPath() {
  try {
    const require = createRequire(import.meta.url);
    const pkgJsonPath = require.resolve("@github/copilot/package.json");
    const pkgDir = path.dirname(pkgJsonPath);
    const pkg = JSON.parse(readFileSync(pkgJsonPath, "utf8"));
    const binRel =
      typeof pkg.bin === "string" ? pkg.bin : pkg.bin && pkg.bin.copilot;
    if (!binRel) return undefined;
    const binPath = path.join(pkgDir, binRel);
    return existsSync(binPath) ? binPath : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Environment for the Vally subprocess. Vally embeds its own copy of the Copilot
 * SDK to run the agent-under-test, and that SDK has the same broken bundled-CLI
 * path resolver. Export COPILOT_CLI_PATH so Vally's SDK uses the correct CLI entry
 * (the SDK checks COPILOT_CLI_PATH before its own resolver) instead of the wrong
 * `node_modules/@github/index.js`.
 * @returns {NodeJS.ProcessEnv}
 */
function vallyEnv() {
  const env = { ...process.env };
  if (!env.COPILOT_CLI_PATH) {
    const cli = resolveCopilotCliPath();
    if (cli) env.COPILOT_CLI_PATH = cli;
  }
  return env;
}

// -------------------------- arg parsing --------------------------
function parseArgs(argv) {
  const opts = {
    skipDocs: false,
    skipEval: false,
    suites: null,
    resultsDir: RESULT_DIR,
    model: process.env.AGENT_MODEL || "claude-opus-4.6",
    idleTimeoutMs: Number(process.env.AGENT_IDLE_TIMEOUT_SEC || 1800) * 1000,
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--skip-docs") opts.skipDocs = true;
    else if (a === "--skip-eval") opts.skipEval = true;
    else if (a === "--suite") (opts.suites ||= []).push(argv[++i]);
    else if (a === "--results-dir") opts.resultsDir = path.resolve(argv[++i]);
    else if (a === "--model") opts.model = argv[++i];
    else if (a === "--idle-timeout") opts.idleTimeoutMs = Number(argv[++i]) * 1000;
    else throw new Error(`Unknown argument: ${a}`);
  }
  if (!opts.suites) opts.suites = DEFAULT_SUITES;
  return opts;
}

// -------------------------- helpers --------------------------
function log(msg) {
  process.stderr.write(`\n\u001b[36m==> ${msg}\u001b[0m\n`);
}

async function readPrompt(name) {
  return readFile(path.join(PROMPTS_DIR, name), "utf8");
}

/**
 * Run a single autonomous agent turn via the Copilot SDK.
 * @param {CopilotClient} client
 * @param {string} prompt
 * @param {{ model: string, idleTimeoutMs: number }} cfg
 */
async function runAgent(client, prompt, cfg) {
  const session = await client.createSession({
    model: cfg.model,
    workingDirectory: REPO_ROOT,
    skillDirectories: [SKILLS_ROOT],
    // Autonomous run: auto-approve file/shell/tool permission requests.
    onPermissionRequest: approveAll,
    systemMessage: {
      mode: "append",
      content:
        "You are running fully autonomously in a script; never ask for confirmation " +
        "or further input. Make the edits directly, then stop.",
    },
  });
  try {
    const result = await session.sendAndWait({ prompt }, cfg.idleTimeoutMs);
    return result?.data?.content ?? "";
  } finally {
    await session.disconnect();
  }
}

/** Resolve the local vally binary installed by `npm ci` in this folder. */
function vallyBin() {
  const bin = process.platform === "win32" ? "vally.cmd" : "vally";
  return path.join(__dirname, "node_modules", ".bin", bin);
}

/** Run one Vally suite; resolves on completion, rejects on non-zero exit. */
function runVallySuite(suite, outputDir) {
  return new Promise((resolve, reject) => {
    log(`Vally suite: ${suite}`);
    // Run from evaluate/ so `.vally.yaml`, --skill-dir .. (the skill under test),
    // ./result and ./debug all resolve as they do for a manual `vally eval`.
    const child = spawn(
      vallyBin(),
      [
        "eval",
        "--suite", suite,
        "--skill-dir", "..",
        "--output-dir", outputDir,
        "--workspace", WORKSPACE_DIR,
        "--verbose",
      ],
      { cwd: EVAL_DIR, stdio: "inherit", shell: process.platform === "win32", env: vallyEnv() },
    );
    child.on("error", reject);
    child.on("close", (code) =>
      code === 0
        ? resolve()
        : reject(new Error(`vally suite '${suite}' exited with code ${code}`)),
    );
  });
}

/** Recursively collect every results.jsonl produced by Vally under a dir. */
async function findResultFiles(dir) {
  const out = [];
  async function walk(d) {
    let entries;
    try {
      entries = await readdir(d, { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entries) {
      const full = path.join(d, e.name);
      if (e.isDirectory()) await walk(full);
      else if (e.name.endsWith(".jsonl")) out.push(full);
    }
  }
  await walk(dir);
  return out;
}

// -------------------------- main --------------------------
async function main() {
  const opts = parseArgs(process.argv.slice(2));
  await mkdir(RESULT_DIR, { recursive: true });

  log(
    `Doc-refinement loop | model=${opts.model} | skipDocs=${opts.skipDocs} | ` +
      `skipEval=${opts.skipEval} | suites=${opts.suites.join(",")}`,
  );

  // The SDK spawns and connects to the Copilot CLI. gitHubToken is optional if the
  // CLI is already authenticated locally (copilot login).
  // NOTE: the SDK's bundled-CLI path resolver assumes @github/copilot exposes an
  // `index.js`, but newer CLI versions ship a `bin` (e.g. npm-loader.js) with no
  // index.js, so it mis-resolves. Compute the real entry point from the installed
  // package's `bin` and pass it explicitly (COPILOT_CLI_PATH still overrides).
  const cliPath = process.env.COPILOT_CLI_PATH || resolveCopilotCliPath();
  const client = new CopilotClient({
    ...(cliPath ? { cliPath } : {}),
    ...(process.env.COPILOT_GITHUB_TOKEN
      ? { gitHubToken: process.env.COPILOT_GITHUB_TOKEN }
      : {}),
  });

  try {
    await client.start();
    const auth = await client.getAuthStatus();
    if (!auth.isAuthenticated) {
      throw new Error(
        "GitHub Copilot is not authenticated. Run `copilot login`, or set COPILOT_GITHUB_TOKEN.",
      );
    }

    // ---- Step 1: update reference documents ----
    if (!opts.skipDocs) {
      log("Step 1 — update reference documents");
      await runAgent(client, await readPrompt("01-update-reference-docs.md"), opts);

      // ---- Step 2: update skill markdown if needed ----
      log("Step 2 — update skill markdown if needed");
      await runAgent(client, await readPrompt("02-update-skill.md"), opts);
    } else {
      log("Skipping steps 1-2 (--skip-docs)");
    }

    // ---- Step 3: run Vally code-quality evals ----
    if (!opts.skipEval) {
      log("Step 3 — run Vally code-quality evals");
      for (const suite of opts.suites) {
        // continueOnError semantics: a failing suite still leaves jsonl to analyze.
        try {
          await runVallySuite(suite, opts.resultsDir);
        } catch (err) {
          process.stderr.write(`\u001b[33mWARN: ${err.message}\u001b[0m\n`);
        }
      }
    } else {
      log("Skipping step 3 (--skip-eval)");
    }

    // ---- Step 4: analyze results ----
    const resultFiles = await findResultFiles(opts.resultsDir);
    if (resultFiles.length === 0) {
      throw new Error(
        `No results.jsonl found under ${opts.resultsDir}. Run step 3 first or pass --results-dir.`,
      );
    }
    log(`Step 4 — analyze ${resultFiles.length} result file(s)`);
    const analysisContext =
      `\n\nThe Vally results.jsonl files to analyze are:\n` +
      resultFiles.map((f) => `- ${f}`).join("\n") +
      `\n\nWrite your structured analysis to ${path.join(opts.resultsDir, "analysis.md")}.`;
    await runAgent(
      client,
      (await readPrompt("04-analyze-results.md")) + analysisContext,
      opts,
    );

    // ---- Step 5: generate the gap report ----
    log("Step 5 — generate documentation-gap report");
    const reportContext =
      `\n\nUse ${path.join(opts.resultsDir, "analysis.md")} as the step-4 analysis input. ` +
      `Write the final report to ${REPORT_PATH}.`;
    await runAgent(
      client,
      (await readPrompt("05-generate-report.md")) + reportContext,
      opts,
    );

    // Confirm the report exists.
    await stat(REPORT_PATH).catch(() => {
      throw new Error(`Expected report was not written: ${REPORT_PATH}`);
    });
    log(`Done. Report: ${REPORT_PATH}`);
  } finally {
    const errs = await client.stop().catch(() => []);
    if (errs && errs.length) {
      process.stderr.write(`Cleanup warnings: ${errs.map((e) => e.message).join("; ")}\n`);
    }
  }
}

main().catch((err) => {
  process.stderr.write(`\u001b[31mFAILED: ${err?.stack || err}\u001b[0m\n`);
  process.exit(1);
});
