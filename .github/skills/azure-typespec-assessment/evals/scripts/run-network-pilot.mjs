#!/usr/bin/env node

import {
  copyFileSync,
  existsSync,
  lstatSync,
  mkdirSync,
  readFileSync,
  realpathSync,
  rmSync,
  statSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { loadCase, materializeWorkspaces } from "./materialize-workspaces.mjs";
import { preflightToolchain } from "../../scripts/prepare-assessment.mjs";

const evidenceRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultRepository = "https://github.com/Azure/azure-rest-api-specs.git";
const defaultModelInputBudgetBytes = 300 * 1024;
const analysisScript = resolve(
  evidenceRoot,
  "..",
  "scripts",
  "run-assessment-analysis.mjs",
);
const assembleScript = resolve(
  evidenceRoot,
  "..",
  "scripts",
  "assemble-assessment.mjs",
);
const validateScript = resolve(
  evidenceRoot,
  "..",
  "scripts",
  "validate-assessment.mjs",
);
const renderScript = resolve(
  evidenceRoot,
  "..",
  "scripts",
  "render-assessment-html.mjs",
);

function requiredValue(argv, index, argument) {
  const value = argv[index + 1];
  if (!value || value.startsWith("--")) {
    throw new Error(`${argument} requires a value`);
  }
  return value;
}

export function parseArgs(argv) {
  const options = {
    repository: defaultRepository,
    phase: "all",
  };
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--resume") {
      options.resume = true;
    } else if (
      [
        "--case",
        "--repository",
        "--phase",
        "--output",
        "--workspace-output",
        "--analysis-output",
        "--document-cache",
        "--artifact-cache",
        "--checkout-cache",
        "--node-modules",
        "--model-input-budget-bytes",
      ].includes(argument)
    ) {
      const value = requiredValue(argv, index, argument);
      const key =
        {
          "--workspace-output": "workspaceOutput",
          "--analysis-output": "analysisOutput",
          "--document-cache": "documentCache",
          "--artifact-cache": "artifactCache",
          "--checkout-cache": "checkoutCache",
          "--node-modules": "nodeModules",
          "--model-input-budget-bytes": "modelInputBudgetBytes",
        }[argument] ?? argument.slice(2);
      options[key] = key === "modelInputBudgetBytes" ? Number(value) : value;
      index += 1;
    } else {
      throw new Error(`Unknown argument: ${argument}`);
    }
  }
  if (!options.case) {
    throw new Error("--case is required during the sparse workspace pilot");
  }
  if (
    ![
      "all",
      "materialize",
      "dependency-setup",
      "analyze",
      "prepare-agent",
      "start-agent",
      "finalize",
    ].includes(options.phase)
  ) {
    throw new Error(
      "--phase must be one of: all, materialize, dependency-setup, analyze, prepare-agent, start-agent, finalize",
    );
  }
  if (
    options.modelInputBudgetBytes !== undefined &&
    (!Number.isInteger(options.modelInputBudgetBytes) ||
      options.modelInputBudgetBytes <= 0)
  ) {
    throw new Error("--model-input-budget-bytes requires a positive integer");
  }
  return options;
}

function writeJson(path, value) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`);
}

function loadJson(path, description) {
  if (!existsSync(path))
    throw new Error(`${description} does not exist: ${path}`);
  return JSON.parse(readFileSync(path, "utf8"));
}

function defaultRunCommand(command, args, options) {
  execFileSync(command, args, {
    cwd: options.cwd,
    encoding: "utf8",
    stdio: "inherit",
    shell: process.platform === "win32" && command.endsWith(".cmd"),
  });
  return { status: 0 };
}

function elapsedMs(startedAt) {
  return Math.round(Number(process.hrtime.bigint() - startedAt) / 1_000_000);
}

function writePilotManifest(path, manifest) {
  manifest.updatedAt = new Date().toISOString();
  writeJson(path, manifest);
}

function verifyPilotManifest(manifest, identity) {
  if (!manifest) return;
  for (const [key, value] of Object.entries(identity)) {
    if (key === "modelInputBudgetBytes" && manifest[key] === undefined) {
      continue;
    }
    if (key === "paths") {
      for (const [pathKey, existingPath] of Object.entries(
        manifest.paths ?? {},
      )) {
        if (
          value[pathKey] === undefined &&
          ["assessmentJson", "assessmentHtml"].includes(pathKey)
        ) {
          continue;
        }
        if (value[pathKey] !== existingPath) {
          throw new Error(
            `Existing pilot manifest has unexpected paths.${pathKey}: ${JSON.stringify(existingPath)}`,
          );
        }
      }
      continue;
    }
    if (JSON.stringify(manifest[key]) !== JSON.stringify(value)) {
      throw new Error(
        `Existing pilot manifest has unexpected ${key}: ${JSON.stringify(manifest[key])}`,
      );
    }
  }
}

function runPhase(manifest, manifestPath, name, action) {
  const startedAt = process.hrtime.bigint();
  manifest.phases[name] = { status: "running" };
  writePilotManifest(manifestPath, manifest);
  try {
    const details = action();
    manifest.phases[name] = {
      ...details,
      status: "succeeded",
      elapsedMs: elapsedMs(startedAt),
    };
    writePilotManifest(manifestPath, manifest);
    return details;
  } catch (error) {
    manifest.phases[name] = {
      status: "failed",
      elapsedMs: elapsedMs(startedAt),
      error: error.message,
    };
    manifest.status = "failed";
    writePilotManifest(manifestPath, manifest);
    throw error;
  }
}

function selectedPhases(phase) {
  if (phase === "all") {
    return ["materialize", "dependency-setup", "analyze", "prepare-agent"];
  }
  return [phase];
}

export function commonSpecificationRoot(sparseRoots) {
  const segments = sparseRoots.map((root) => root.split("/"));
  const common = segments[0].filter((segment, index) =>
    segments.every((parts) => parts[index] === segment));
  return common.join("/") || ".";
}

function analysisArguments(paths, testCase, modelInputBudgetBytes) {
  return [
    analysisScript,
    "--repo",
    paths.headWorkspace,
    "--base",
    testCase.baseCommit,
    "--specification",
    commonSpecificationRoot(testCase.sparseRoots),
    "--output",
    paths.analysisOutput,
    "--document-cache",
    paths.documentCache,
    "--artifact-cache",
    paths.artifactCache,
    "--checkout-cache",
    paths.checkoutCache,
    "--model-input-budget-bytes",
    String(modelInputBudgetBytes),
  ];
}

function requireDirectory(path, description) {
  if (!existsSync(path) || !statSync(path).isDirectory()) {
    throw new Error(`${description} is not a directory: ${path}`);
  }
}

function ensureDirectoryLink(target, source) {
  const expected = realpathSync.native(source);
  if (existsSync(target)) {
    const actual = realpathSync.native(target);
    if (actual !== expected) {
      throw new Error(`${target} resolves to ${actual}; expected ${expected}`);
    }
    return true;
  }
  symlinkSync(
    expected,
    target,
    process.platform === "win32" ? "junction" : "dir",
  );
  return false;
}

function preflightOrThrow(preflight, root, description) {
  try {
    preflight(root);
  } catch (error) {
    throw new Error(`${description}:\n${error.message}`);
  }
}

function setupDependencies(
  paths,
  materialization,
  nodeModulesOverride,
  runCommand,
  preflight,
) {
  mkdirSync(paths.dependencyRoot, { recursive: true });
  for (const file of ["package.json", "package-lock.json"]) {
    copyFileSync(
      join(materialization.layout.head, file),
      join(paths.dependencyRoot, file),
    );
  }

  const managedNodeModules = join(paths.dependencyRoot, "node_modules");
  let nodeModules;
  let installed = false;
  let installReason;
  if (nodeModulesOverride) {
    requireDirectory(nodeModulesOverride, "Provided --node-modules");
    ensureDirectoryLink(managedNodeModules, nodeModulesOverride);
    nodeModules = realpathSync.native(nodeModulesOverride);
    preflightOrThrow(
      preflight,
      paths.dependencyRoot,
      "Provided --node-modules is incompatible with the historical package-lock.json",
    );
  } else {
    nodeModules = managedNodeModules;
    if (!existsSync(managedNodeModules)) {
      installReason = "node_modules is absent";
    } else {
      try {
        preflight(paths.dependencyRoot);
      } catch (error) {
        if (lstatSync(managedNodeModules).isSymbolicLink()) {
          throw new Error(
            `Managed dependency cache contains an unexpected node_modules link: ${managedNodeModules}`,
          );
        }
        installReason = error.message;
      }
    }
    if (installReason) {
      const npm = process.platform === "win32" ? "npm.cmd" : "npm";
      const result = runCommand(npm, ["ci", "--no-audit", "--no-fund"], {
        cwd: paths.dependencyRoot,
      });
      if (result?.status !== undefined && result.status !== 0) {
        throw new Error(`npm ci exited with ${result.status}`);
      }
      installed = true;
    }
    requireDirectory(managedNodeModules, "Installed node_modules");
    preflightOrThrow(
      preflight,
      paths.dependencyRoot,
      "Installed dependency cache failed the unchanged core preflight",
    );
    nodeModules = realpathSync.native(managedNodeModules);
  }

  const workspaceLinks = [
    materialization.layout.base,
    materialization.layout.head,
  ].map((workspace) => ({
    workspace,
    reused: ensureDirectoryLink(join(workspace, "node_modules"), nodeModules),
  }));
  preflightOrThrow(
    preflight,
    materialization.layout.base,
    "Base workspace failed the unchanged core preflight",
  );
  preflightOrThrow(
    preflight,
    materialization.layout.head,
    "Head workspace failed the unchanged core preflight",
  );
  return {
    dependencyRoot: paths.dependencyRoot,
    nodeModules,
    override: Boolean(nodeModulesOverride),
    installed,
    installReason,
    workspaceLinks,
  };
}

function verifyDependencySetup(materialization, phase, preflight) {
  if (!phase?.nodeModules) {
    throw new Error(
      "Analyze requires a completed dependencySetup phase; run --phase dependency-setup first",
    );
  }
  requireDirectory(phase.nodeModules, "Shared node_modules");
  for (const workspace of [
    materialization.layout.base,
    materialization.layout.head,
  ]) {
    const linked = join(workspace, "node_modules");
    if (
      !existsSync(linked) ||
      realpathSync.native(linked) !== realpathSync.native(phase.nodeModules)
    ) {
      throw new Error(
        `Dependency setup is stale for ${workspace}; rerun --phase dependency-setup`,
      );
    }
    preflightOrThrow(
      preflight,
      workspace,
      `${workspace} failed the unchanged core preflight`,
    );
  }
  return phase;
}

function prepareAgentWorkItem(
  paths,
  testCase,
  materialization,
  dependencySetup,
  handoffReadyAtEpochMs,
) {
  if (!existsSync(paths.modelInput)) {
    throw new Error(
      `Deterministic analysis did not produce model input: ${paths.modelInput}`,
    );
  }
  const workItem = {
    schemaVersion: 1,
    kind: "azure-typespec-assessment-agent-judgment",
    pr: testCase.pr,
    status: "ready-for-agent-judgment",
    deterministicPilotComplete: true,
    fullEndToEndComplete: false,
    boundary: {
      coordinatorInvokesLlm: false,
      llmInvoked: false,
      owner: "parent Agent",
      handoffReadyAt: new Date(handoffReadyAtEpochMs).toISOString(),
      timingInstruction:
        "Run the start-agent phase as the Agent's first action so queue time is not counted as judgment.",
    },
    inputs: {
      modelInput: paths.modelInput,
      materializationManifest: materialization.layout.manifest,
      baseWorkspace: materialization.layout.base,
      headWorkspace: materialization.layout.head,
      sharedNodeModules: dependencySetup.nodeModules,
    },
    expectedOutputs: {
      complianceSearchEvidence: paths.complianceSearchEvidence,
      assessmentJudgment: join(paths.output, "assessment-judgment.json"),
      materializationAssessment: join(
        paths.output,
        "materialization-assessment.json",
      ),
    },
    instructions: [
      "Review every bounded item in model-input.json using the azure-typespec-assessment classification rules.",
      "As the first Agent action, run the start-agent phase for this pilot.",
      "Run the bounded Compliance search and write compliance-search-evidence.json in the deterministic-analysis directory.",
      "Write assessment-judgment.json with exact Compliance tuple coverage and materialization-assessment.json using the skill schemas and output contract.",
      "Assemble and validate the final assessment only after Agent judgment; this coordinator does not invoke an LLM.",
    ],
  };
  writeJson(paths.agentWorkItem, workItem);
  return {
    workItem: paths.agentWorkItem,
    modelInput: paths.modelInput,
    modelInputBytes: statSync(paths.modelInput).size,
    handoffReadyAtEpochMs,
    handoffReadyAt: new Date(handoffReadyAtEpochMs).toISOString(),
    status: workItem.status,
  };
}

function startAgentJudgment(paths, manifest, agentStartedAtEpochMs) {
  const handoffReadyAtEpochMs =
    manifest.phases.prepareAgent?.handoffReadyAtEpochMs ??
    manifest.phases.prepareAgent?.agentStartedAtEpochMs;
  if (!Number.isInteger(handoffReadyAtEpochMs)) {
    throw new Error(
      "Agent start requires a successful prepareAgent phase with a handoff timestamp",
    );
  }
  rmSync(paths.assessmentJudgment, { force: true });
  rmSync(paths.materializationAssessment, { force: true });
  const workItem = loadJson(paths.agentWorkItem, "Agent work item");
  workItem.status = "agent-judgment-running";
  workItem.boundary = {
    ...workItem.boundary,
    agentStartedAt: new Date(agentStartedAtEpochMs).toISOString(),
  };
  writeJson(paths.agentWorkItem, workItem);
  return {
    agentStartedAtEpochMs,
    agentStartedAt: new Date(agentStartedAtEpochMs).toISOString(),
    handoffReadyAtEpochMs,
    queueMs: Math.max(0, agentStartedAtEpochMs - handoffReadyAtEpochMs),
  };
}

function finalizeAssessment(
  paths,
  manifest,
  testCase,
  runCommand,
  agentCompletedAtEpochMs,
) {
  const agentStartedAtEpochMs =
    manifest.phases.startAgent?.agentStartedAtEpochMs ??
    manifest.phases.prepareAgent?.agentStartedAtEpochMs;
  const explicitAgentStart = manifest.phases.startAgent?.status === "succeeded";
  if (!Number.isInteger(agentStartedAtEpochMs)) {
    throw new Error(
      "Finalize requires an Agent start timestamp; run --phase start-agent before Agent judgment",
    );
  }
  for (const [path, description] of [
    [paths.complianceSearchEvidence, "Agent Compliance search evidence"],
    [paths.assessmentJudgment, "Agent assessment judgment"],
    [paths.materializationAssessment, "Agent materialization assessment"],
  ]) {
    if (!existsSync(path)) {
      throw new Error(`${description} does not exist: ${path}`);
    }
    if (statSync(path).mtimeMs < agentStartedAtEpochMs) {
      throw new Error(
        `${description} predates the current Agent run: ${path}`,
      );
    }
  }
  const judgmentElapsedMs = Math.max(
    0,
    agentCompletedAtEpochMs - agentStartedAtEpochMs,
  );
  const reportBase = join(paths.reportRoot, String(agentStartedAtEpochMs));
  let reportOutput = reportBase;
  for (let suffix = 2; existsSync(reportOutput); suffix += 1) {
    reportOutput = `${reportBase}-${suffix}`;
  }
  const assessmentJson = join(reportOutput, "assessment.json");
  const assessmentHtml = join(reportOutput, "assessment.html");
  const assembleArgs = [
    assembleScript,
    "--work",
    paths.analysisOutput,
    "--judgment",
    paths.assessmentJudgment,
    "--output",
    assessmentJson,
  ];
  runCommand(process.execPath, assembleArgs, { cwd: paths.output });
  const assessment = loadJson(assessmentJson, "Final assessment JSON");
  assessment.pr = testCase.pr;
  assessment.pullRequest = {
    number: testCase.pr,
    url: `https://github.com/Azure/azure-rest-api-specs/pull/${testCase.pr}`,
  };
  writeJson(assessmentJson, assessment);
  runCommand(process.execPath, [validateScript, assessmentJson], {
    cwd: paths.output,
  });
  runCommand(process.execPath, [renderScript, assessmentJson, assessmentHtml], {
    cwd: paths.output,
  });
  for (const [path, description] of [
    [assessmentJson, "Final assessment JSON"],
    [assessmentHtml, "Final assessment HTML"],
  ]) {
    if (!existsSync(path)) {
      throw new Error(`${description} does not exist: ${path}`);
    }
  }
  return {
    agentStartedAtEpochMs,
    agentCompletedAtEpochMs,
    agentStartedAt: new Date(agentStartedAtEpochMs).toISOString(),
    agentCompletedAt: new Date(agentCompletedAtEpochMs).toISOString(),
    judgmentElapsedMs,
    judgmentTimingQuality: explicitAgentStart
      ? "measured"
      : "queue-inclusive-legacy",
    queueMs: explicitAgentStart ? manifest.phases.startAgent.queueMs : null,
    assembleArgs,
    reportOutput,
    assessmentJson,
    assessmentHtml,
  };
}

export function runNetworkPilot(options, dependencies = {}) {
  const testCase = loadCase(options.case, options.casesPath);

  const output = resolve(
    options.output ??
      join(evidenceRoot, "outputs", "network-pilot", String(testCase.pr)),
  );
  const paths = {
    output,
    workspaceOutput: resolve(
      options.workspaceOutput ?? join(output, "workspaces"),
    ),
    analysisOutput: resolve(
      options.analysisOutput ?? join(output, "deterministic-analysis"),
    ),
    documentCache: resolve(
      options.documentCache ?? join(output, "cache", "documents"),
    ),
    artifactCache: resolve(
      options.artifactCache ?? join(output, "cache", "artifacts"),
    ),
    checkoutCache: resolve(
      options.checkoutCache ?? join(output, "cache", "checkouts"),
    ),
    dependencyRoot: resolve(join(output, "cache", "toolchain")),
    nodeModulesOverride: options.nodeModules
      ? resolve(options.nodeModules)
      : undefined,
  };
  paths.modelInput = join(paths.analysisOutput, "model-input.json");
  paths.complianceSearchEvidence = join(
    paths.analysisOutput,
    "compliance-search-evidence.json",
  );
  paths.manifest = join(output, "pilot-manifest.json");
  paths.summary = join(output, "pilot-summary.json");
  paths.agentWorkItem = join(output, "agent-work-item.json");
  paths.assessmentJudgment = join(output, "assessment-judgment.json");
  paths.materializationAssessment = join(
    output,
    "materialization-assessment.json",
  );
  paths.reportRoot = join(output, "reports");
  const identityPaths = { ...paths };
  const materializationManifestPath = join(
    paths.workspaceOutput,
    String(testCase.pr),
    "materialization-manifest.json",
  );
  const identity = {
    pr: testCase.pr,
    repository: options.repository ?? defaultRepository,
    baseCommit: testCase.baseCommit,
    headCommit: testCase.headCommit,
    sparseRoots: testCase.sparseRoots,
    modelInputBudgetBytes:
      options.modelInputBudgetBytes ?? defaultModelInputBudgetBytes,
    paths: identityPaths,
  };
  const previousManifest = existsSync(paths.manifest)
    ? loadJson(paths.manifest, "Pilot manifest")
    : undefined;
  verifyPilotManifest(previousManifest, identity);
  const manifest = {
    schemaVersion: 1,
    ...identity,
    resumable: true,
    fullEndToEndComplete: false,
    phases: previousManifest?.phases ?? {},
    status: "running",
  };
  mkdirSync(output, { recursive: true });
  writePilotManifest(paths.manifest, manifest);

  const materialize =
    dependencies.materializeWorkspaces ?? materializeWorkspaces;
  const runCommand = dependencies.runCommand ?? defaultRunCommand;
  const preflight = dependencies.preflightToolchain ?? preflightToolchain;
  const now = dependencies.now ?? Date.now;
  let materialization;
  const phases = selectedPhases(options.phase ?? "all");

  if (phases.includes("materialize")) {
    if (
      options.resume &&
      manifest.phases.materialize?.status === "succeeded" &&
      existsSync(materializationManifestPath)
    ) {
      materialization = loadJson(
        materializationManifestPath,
        "Materialization manifest",
      );
    } else {
      materialization = runPhase(
        manifest,
        paths.manifest,
        "materialize",
        () => {
          const result = materialize({
            case: testCase.pr,
            repository: options.repository ?? defaultRepository,
            output: paths.workspaceOutput,
            casesPath: options.casesPath,
          });
          return {
            manifest: result.layout.manifest,
            storage: result.storage,
            materialization: result,
          };
        },
      ).materialization;
    }
  } else {
    materialization = loadJson(
      materializationManifestPath,
      "Materialization manifest required by this phase",
    );
  }

  paths.baseWorkspace = materialization.layout.base;
  paths.headWorkspace = materialization.layout.head;

  let dependencySetup;
  if (phases.includes("dependency-setup")) {
    if (
      options.resume &&
      manifest.phases.dependencySetup?.status === "succeeded"
    ) {
      dependencySetup = verifyDependencySetup(
        materialization,
        manifest.phases.dependencySetup,
        preflight,
      );
    } else {
      dependencySetup = runPhase(
        manifest,
        paths.manifest,
        "dependencySetup",
        () =>
          setupDependencies(
            paths,
            materialization,
            paths.nodeModulesOverride,
            runCommand,
            preflight,
          ),
      );
    }
  } else if (phases.some((phase) => phase !== "materialize")) {
    dependencySetup = verifyDependencySetup(
      materialization,
      manifest.phases.dependencySetup,
      preflight,
    );
  }

  if (phases.includes("analyze")) {
    if (manifest.phases.dependencySetup?.status !== "succeeded") {
      throw new Error(
        "Analyze requires a successful dependencySetup phase; run --phase dependency-setup first",
      );
    }
    if (!(
      options.resume &&
      manifest.phases.analyze?.status === "succeeded" &&
      existsSync(paths.modelInput)
    )) {
      delete manifest.phases.prepareAgent;
      delete manifest.phases.startAgent;
      delete manifest.phases.finalize;
      runPhase(manifest, paths.manifest, "analyze", () => {
        const args = analysisArguments(
          paths,
          testCase,
          identity.modelInputBudgetBytes,
        );
        const result = runCommand(process.execPath, args, {
          cwd: paths.headWorkspace,
        });
        if (result?.status !== undefined && result.status !== 0) {
          throw new Error(
            `Deterministic analysis exited with ${result.status}`,
          );
        }
        if (!existsSync(paths.modelInput)) {
          throw new Error(
            `Deterministic analysis did not produce ${paths.modelInput}`,
          );
        }
        return {
          command: process.execPath,
          args,
          modelInput: paths.modelInput,
          modelInputBytes: statSync(paths.modelInput).size,
        };
      });
    }
  }

  let agentPreparation;
  if (phases.includes("prepare-agent")) {
    if (manifest.phases.analyze?.status !== "succeeded") {
      throw new Error(
        "Agent preparation requires a successful analyze phase; run --phase analyze first",
      );
    }
    if (
      options.resume &&
      manifest.phases.prepareAgent?.status === "succeeded" &&
      existsSync(paths.agentWorkItem)
    ) {
      agentPreparation = manifest.phases.prepareAgent;
    } else {
      delete manifest.phases.startAgent;
      delete manifest.phases.finalize;
      rmSync(paths.assessmentJudgment, { force: true });
      rmSync(paths.materializationAssessment, { force: true });
      agentPreparation = runPhase(
        manifest,
        paths.manifest,
        "prepareAgent",
        () => {
          const handoffReadyAtEpochMs = now();
          return {
            ...prepareAgentWorkItem(
              paths,
              testCase,
              materialization,
              dependencySetup,
              handoffReadyAtEpochMs,
            ),
          };
        },
      );
    }
  }

  if (phases.includes("start-agent")) {
    if (manifest.phases.prepareAgent?.status !== "succeeded") {
      throw new Error(
        "Agent start requires a successful prepareAgent phase; run --phase prepare-agent first",
      );
    }
    delete manifest.phases.finalize;
    runPhase(manifest, paths.manifest, "startAgent", () =>
      startAgentJudgment(paths, manifest, now()),
    );
  }

  if (phases.includes("finalize")) {
    if (
      manifest.phases.analyze?.status !== "succeeded" ||
      manifest.phases.prepareAgent?.status !== "succeeded"
    ) {
      throw new Error(
        "Finalize requires successful analyze and prepareAgent phases",
      );
    }
    if (!(
      options.resume &&
      manifest.phases.finalize?.status === "succeeded" &&
      existsSync(manifest.phases.finalize.assessmentJson) &&
      existsSync(manifest.phases.finalize.assessmentHtml)
    )) {
      runPhase(manifest, paths.manifest, "finalize", () =>
        finalizeAssessment(
          paths,
          manifest,
          testCase,
          runCommand,
          dependencies.agentCompletedAtEpochMs?.() ??
            Math.ceil(
              Math.max(
                statSync(paths.complianceSearchEvidence).mtimeMs,
                statSync(paths.assessmentJudgment).mtimeMs,
                statSync(paths.materializationAssessment).mtimeMs,
              ),
            ),
        ),
      );
    }
  }

  const requestedPhasesComplete = phases.every((phase) => {
    const manifestName =
      {
        "dependency-setup": "dependencySetup",
        "prepare-agent": "prepareAgent",
        "start-agent": "startAgent",
      }[phase] ?? phase;
    return manifest.phases[manifestName]?.status === "succeeded";
  });
  const deterministicPilotComplete =
    manifest.phases.analyze?.status === "succeeded" &&
    manifest.phases.prepareAgent?.status === "succeeded";
  const fullEndToEndComplete =
    deterministicPilotComplete &&
    manifest.phases.finalize?.status === "succeeded";
  manifest.fullEndToEndComplete = fullEndToEndComplete;
  manifest.status = requestedPhasesComplete
    ? fullEndToEndComplete
      ? "succeeded"
      : manifest.phases.startAgent?.status === "succeeded"
        ? "awaiting-agent-judgment"
        : deterministicPilotComplete
          ? "awaiting-agent-start"
          : "phase-complete"
    : "incomplete";
  manifest.agentBoundary = {
    coordinatorInvokesLlm: false,
    fullEndToEndComplete,
    workItem: agentPreparation?.workItem,
  };
  writePilotManifest(paths.manifest, manifest);

  const summary = {
    schemaVersion: 1,
    pr: testCase.pr,
    status: manifest.status,
    deterministicPilotComplete,
    fullEndToEndComplete,
    llmInvoked: fullEndToEndComplete,
    baseCommit: testCase.baseCommit,
    headCommit: testCase.headCommit,
    sparseRoots: testCase.sparseRoots,
    storage: materialization.storage,
    dependencySetup: manifest.phases.dependencySetup
      ? {
          nodeModules: manifest.phases.dependencySetup.nodeModules,
          installed: manifest.phases.dependencySetup.installed,
          override: manifest.phases.dependencySetup.override,
        }
      : undefined,
    timings: Object.fromEntries(
      Object.entries(manifest.phases)
        .filter(([, phase]) => phase.elapsedMs !== undefined)
        .map(([name, phase]) => [name, phase.elapsedMs]),
    ),
    skillE2eMs: fullEndToEndComplete
      ? manifest.phases.analyze.elapsedMs +
        manifest.phases.finalize.judgmentElapsedMs +
        manifest.phases.finalize.elapsedMs
      : undefined,
    agentJudgmentMs: fullEndToEndComplete
      ? manifest.phases.finalize.judgmentElapsedMs
      : undefined,
    agentQueueMs:
      manifest.phases.startAgent?.status === "succeeded"
        ? manifest.phases.startAgent.queueMs
        : undefined,
    timingQuality: fullEndToEndComplete
      ? manifest.phases.finalize.judgmentTimingQuality
      : "incomplete",
    manifest: paths.manifest,
    modelInput: existsSync(paths.modelInput) ? paths.modelInput : undefined,
    agentWorkItem: existsSync(paths.agentWorkItem)
      ? paths.agentWorkItem
      : undefined,
    assessmentJson: fullEndToEndComplete
      ? manifest.phases.finalize.assessmentJson
      : undefined,
    assessmentHtml: fullEndToEndComplete
      ? manifest.phases.finalize.assessmentHtml
      : undefined,
    nextStage:
      manifest.status === "succeeded"
        ? "The measured full skill E2E assessment is complete."
        : manifest.status === "awaiting-agent-judgment"
          ? "Parent Agent reviews model-input.json and completes judgment and assembly."
          : manifest.status === "awaiting-agent-start"
            ? "The assigned Agent runs --phase start-agent as its first action."
            : "Resume the remaining deterministic pilot phases.",
  };
  writeJson(paths.summary, summary);
  return summary;
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  try {
    const summary = runNetworkPilot(parseArgs(process.argv.slice(2)));
    process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
