import assert from "node:assert/strict";
import {
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  realpathSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import {
  analysisArguments,
  commonSpecificationRoot,
  parseArgs,
  runNetworkPilot,
} from "./run-network-pilot.mjs";

const evalRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const testRoot = join(evalRoot, ".workspaces");
const requiredPackages = [
  "@typespec/compiler",
  "@typespec/openapi3",
  "@azure-tools/typespec-autorest",
  "@azure-tools/typespec-azure-resource-manager",
  "@azure-tools/typespec-client-generator-core",
];

test("uses the common specification root for multi-root cases", () => {
  assert.equal(
    commonSpecificationRoot([
      "specification/recoveryservices",
      "specification/recoveryservicesbackup",
    ]),
    "specification",
  );
  assert.equal(
    commonSpecificationRoot(["specification/network/resource-manager"]),
    "specification/network/resource-manager",
  );
});

test("preserves each sparse root for multi-root analysis", () => {
  const args = analysisArguments(
    {
      headWorkspace: "head",
      analysisOutput: "analysis",
      documentCache: "documents",
      artifactCache: "artifacts",
      checkoutCache: "checkouts",
    },
    {
      baseCommit: "base",
      sparseRoots: [
        "specification/recoveryservices",
        "specification/recoveryservicesbackup",
      ],
    },
    300 * 1024,
  );

  assert.equal(args[args.indexOf("--specification") + 1], "specification");
  assert.deepEqual(
    args.flatMap((argument, index) =>
      argument === "--sparse-root" ? [args[index + 1]] : [],
    ),
    ["specification/recoveryservices", "specification/recoveryservicesbackup"],
  );
});

function writeFixtureManifests(root) {
  writeFileSync(
    join(root, "package.json"),
    `${JSON.stringify({ private: true, engines: { node: "*" } }, null, 2)}\n`,
  );
  writeFileSync(
    join(root, "package-lock.json"),
    `${JSON.stringify(
      {
        name: "network-pilot-test",
        lockfileVersion: 3,
        packages: Object.fromEntries([
          ["", { name: "network-pilot-test" }],
          ...requiredPackages.map((name) => [
            `node_modules/${name}`,
            { version: "1.0.0" },
          ]),
        ]),
      },
      null,
      2,
    )}\n`,
  );
}

function installFixturePackages(nodeModules, version = "1.0.0") {
  for (const packageName of requiredPackages) {
    const packageRoot = join(nodeModules, ...packageName.split("/"));
    mkdirSync(packageRoot, { recursive: true });
    writeFileSync(
      join(packageRoot, "package.json"),
      `${JSON.stringify({ name: packageName, version })}\n`,
    );
  }
}

test("requires a case and rejects unsupported phases", () => {
  assert.throws(() => parseArgs([]), /--case is required/);
  assert.throws(
    () => parseArgs(["--case", "44988", "--phase", "judge"]),
    /--phase must be one of/,
  );
  assert.throws(
    () =>
      parseArgs(["--case", "44988", "--model-input-budget-bytes", "invalid"]),
    /requires a positive integer/,
  );
});

test("coordinates deterministic analysis and prepares an Agent work item", () => {
  mkdirSync(testRoot, { recursive: true });
  const root = mkdtempSync(join(testRoot, "network-pilot-test-"));
  const output = join(root, "pilot");
  const workspaceOutput = join(root, "workspaces");
  const base = join(root, "base");
  const head = join(root, "head");
  mkdirSync(base);
  mkdirSync(head);
  writeFixtureManifests(base);
  writeFixtureManifests(head);
  const analysisCalls = [];
  const npmCalls = [];
  const materialization = {
    schemaVersion: 1,
    status: "succeeded",
    layout: {
      manifest: join(workspaceOutput, "44988", "materialization-manifest.json"),
      base,
      head,
    },
    storage: {
      sourceBytes: 10,
      baseWorkspaceBytes: 20,
      headWorkspaceBytes: 30,
      totalBytes: 60,
    },
  };

  try {
    const summary = runNetworkPilot(
      {
        case: 44988,
        output,
        workspaceOutput,
        repository: "fixture://repository",
      },
      {
        now: () => 1_000,
        materializeWorkspaces(options) {
          assert.equal("nodeModules" in options, false);
          mkdirSync(dirname(materialization.layout.manifest), {
            recursive: true,
          });
          writeFileSync(
            materialization.layout.manifest,
            `${JSON.stringify(materialization, null, 2)}\n`,
          );
          return materialization;
        },
        runCommand(command, args, options) {
          if (/^npm(?:\.cmd)?$/.test(command)) {
            npmCalls.push({ command, args, options });
            installFixturePackages(join(options.cwd, "node_modules"));
            return { status: 0 };
          }
          analysisCalls.push({ command, args, options });
          const analysisOutput = args[args.indexOf("--output") + 1];
          mkdirSync(analysisOutput, { recursive: true });
          writeFileSync(
            join(analysisOutput, "model-input.json"),
            '{"schemaVersion":1}\n',
          );
          return { status: 0 };
        },
      },
    );

    assert.equal(summary.status, "awaiting-agent-start");
    assert.equal(summary.deterministicPilotComplete, true);
    assert.equal(summary.fullEndToEndComplete, false);
    assert.equal(summary.llmInvoked, false);
    assert.equal(npmCalls.length, 1);
    assert.deepEqual(npmCalls[0].args, ["ci", "--no-audit", "--no-fund"]);
    assert.equal(analysisCalls.length, 1);
    const args = analysisCalls[0].args;
    assert.equal(args.includes("--toolchain-root"), false);
    assert.equal(args[args.indexOf("--repo") + 1], head);
    assert.match(args[0], /run-assessment-analysis\.mjs$/);
    assert.equal(analysisCalls[0].options.cwd, head);
    assert.match(args[args.indexOf("--document-cache") + 1], /documents$/);
    assert.match(args[args.indexOf("--artifact-cache") + 1], /artifacts$/);
    assert.match(args[args.indexOf("--checkout-cache") + 1], /checkouts$/);
    assert.equal(
      args[args.indexOf("--model-input-budget-bytes") + 1],
      String(300 * 1024),
    );
    assert.deepEqual(
      args.flatMap((argument, index) =>
        argument === "--sparse-root" ? [args[index + 1]] : [],
      ),
      ["specification/network/resource-manager/Microsoft.Network/Network"],
    );

    const workItem = JSON.parse(readFileSync(summary.agentWorkItem, "utf8"));
    assert.equal(workItem.status, "ready-for-agent-judgment");
    assert.equal(workItem.boundary.coordinatorInvokesLlm, false);
    assert.equal(workItem.boundary.handoffReadyAt, "1970-01-01T00:00:01.000Z");
    assert.equal(workItem.fullEndToEndComplete, false);
    assert.ok(existsSync(workItem.inputs.sharedNodeModules));
    assert.equal(
      realpathSync.native(join(base, "node_modules")),
      realpathSync.native(workItem.inputs.sharedNodeModules),
    );
    assert.equal(
      realpathSync.native(join(head, "node_modules")),
      realpathSync.native(workItem.inputs.sharedNodeModules),
    );
    assert.equal(summary.dependencySetup.installed, true);
    assert.equal(summary.dependencySetup.override, false);

    const resumed = runNetworkPilot(
      {
        case: 44988,
        output,
        workspaceOutput,
        repository: "fixture://repository",
        resume: true,
      },
      {
        materializeWorkspaces() {
          throw new Error("resume should not rematerialize");
        },
        runCommand() {
          throw new Error("resume should not rerun analysis");
        },
      },
    );
    assert.equal(resumed.status, "awaiting-agent-start");
    assert.equal(npmCalls.length, 1);
    assert.equal(analysisCalls.length, 1);

    const started = runNetworkPilot(
      {
        case: 44988,
        output,
        workspaceOutput,
        repository: "fixture://repository",
        phase: "start-agent",
      },
      {
        now: () => 5_000,
      },
    );
    assert.equal(started.status, "awaiting-agent-judgment");
    assert.equal(started.agentQueueMs, 4_000);

    writeFileSync(
      workItem.expectedOutputs.complianceSearchEvidence,
      '{"schemaVersion":1}\n',
    );
    writeFileSync(
      join(output, "assessment-judgment.json"),
      '{"schemaVersion":1}\n',
    );
    writeFileSync(
      join(output, "materialization-assessment.json"),
      '{"schemaVersion":1}\n',
    );
    const finalCommands = [];
    const completed = runNetworkPilot(
      {
        case: 44988,
        output,
        workspaceOutput,
        repository: "fixture://repository",
        phase: "finalize",
      },
      {
        now: () => 31_000,
        agentCompletedAtEpochMs: () => 31_000,
        runCommand(command, args) {
          finalCommands.push({ command, args });
          if (/assemble-assessment\.mjs$/.test(args[0])) {
            const assessmentJson = args[args.indexOf("--output") + 1];
            const reportOutput = dirname(assessmentJson);
            mkdirSync(reportOutput, { recursive: true });
            writeFileSync(
              assessmentJson,
              '{"schemaVersion":1}\n',
            );
          }
          if (/render-assessment-html\.mjs$/.test(args[0])) {
            writeFileSync(
              args[2],
              "<html></html>\n",
            );
          }
          return { status: 0 };
        },
      },
    );
    assert.equal(completed.status, "succeeded");
    assert.equal(completed.fullEndToEndComplete, true);
    assert.equal(completed.agentJudgmentMs, 26_000);
    assert.equal(completed.agentQueueMs, 4_000);
    assert.equal(completed.timingQuality, "measured");
    assert.ok(completed.skillE2eMs >= 26_000);
    assert.match(
      completed.assessmentJson,
      /reports[\\/]5000[\\/]assessment\.json$/,
    );
    assert.deepEqual(
      JSON.parse(readFileSync(completed.assessmentJson, "utf8")).pullRequest,
      {
        number: 44988,
        url: "https://github.com/Azure/azure-rest-api-specs/pull/44988",
      },
    );
    assert.equal(finalCommands.length, 3);
    const assembleArgs = finalCommands[0].args;
    assert.match(assembleArgs[0], /assemble-assessment\.mjs$/);
    assert.equal(
      assembleArgs[assembleArgs.indexOf("--work") + 1],
      join(output, "deterministic-analysis"),
    );
    assert.match(
      assembleArgs[assembleArgs.indexOf("--output") + 1],
      /assessment\.json$/,
    );
    assert.match(finalCommands[1].args[0], /validate-assessment\.mjs$/);
    assert.match(finalCommands[2].args[0], /render-assessment-html\.mjs$/);
    const completedManifest = JSON.parse(
      readFileSync(join(output, "pilot-manifest.json"), "utf8"),
    );
    assert.equal(completedManifest.fullEndToEndComplete, true);

    const restarted = runNetworkPilot(
      {
        case: 44988,
        output,
        workspaceOutput,
        repository: "fixture://repository",
        phase: "prepare-agent",
      },
      {
        now: () => 41_000,
      },
    );
    assert.equal(restarted.status, "awaiting-agent-start");
    assert.equal(restarted.fullEndToEndComplete, false);
    assert.equal(restarted.agentJudgmentMs, undefined);
    const restartedManifest = JSON.parse(
      readFileSync(join(output, "pilot-manifest.json"), "utf8"),
    );
    assert.equal(restartedManifest.fullEndToEndComplete, false);
    assert.equal(restartedManifest.phases.startAgent, undefined);
    assert.equal(restartedManifest.phases.finalize, undefined);
    assert.equal(
      existsSync(join(output, "assessment-judgment.json")),
      false,
    );
    assert.equal(
      existsSync(join(output, "materialization-assessment.json")),
      false,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("rejects a node_modules override that mismatches the historical lock", () => {
  mkdirSync(testRoot, { recursive: true });
  const root = mkdtempSync(join(testRoot, "network-pilot-override-test-"));
  const output = join(root, "pilot");
  const workspaceOutput = join(root, "workspaces");
  const base = join(root, "base");
  const head = join(root, "head");
  const override = join(root, "override-node_modules");
  mkdirSync(base);
  mkdirSync(head);
  writeFixtureManifests(base);
  writeFixtureManifests(head);
  installFixturePackages(override, "2.0.0");
  const materialization = {
    schemaVersion: 1,
    status: "succeeded",
    layout: {
      manifest: join(workspaceOutput, "44988", "materialization-manifest.json"),
      base,
      head,
    },
    storage: { totalBytes: 1 },
  };

  try {
    assert.throws(
      () =>
        runNetworkPilot(
          {
            case: 44988,
            output,
            workspaceOutput,
            repository: "fixture://repository",
            nodeModules: override,
          },
          {
            materializeWorkspaces() {
              mkdirSync(dirname(materialization.layout.manifest), {
                recursive: true,
              });
              writeFileSync(
                materialization.layout.manifest,
                `${JSON.stringify(materialization, null, 2)}\n`,
              );
              return materialization;
            },
            runCommand() {
              throw new Error("an override must never trigger npm ci");
            },
          },
        ),
      /Provided --node-modules is incompatible.*installed version 2\.0\.0 does not match/s,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
