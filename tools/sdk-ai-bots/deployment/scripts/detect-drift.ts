import {
  existsSync,
  mkdtempSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { parseArgs } from "node:util";
import { fileURLToPath } from "node:url";

import type { EnvironmentName } from "../hooks/lib/env-suite.js";
import {
  type CommandRunner,
  isDirectExecution,
  parseAzdEnvironmentNames,
  parseEnvironmentName,
  runCli,
  runCommand,
} from "./lib/cli.js";

export const PROVISION_LAYERS = [
  "resource-group",
  "shared-resources",
  "agent",
  "frontend",
  "agent-server",
  "function-app",
  "logic-app",
] as const;

export interface DetectDriftOptions {
  environment: EnvironmentName;
  projectDirectory?: string;
}

export function detectDrift(
  options: DetectDriftOptions,
  run: CommandRunner = runCommand,
  log: (message: string) => void = console.log,
): string[] {
  const projectDirectory = resolve(
    options.projectDirectory ??
      resolve(dirname(fileURLToPath(import.meta.url)), "../.."),
  );
  if (!existsSync(join(projectDirectory, "azure.yaml"))) {
    throw new Error(`azure.yaml not found under ${projectDirectory}.`);
  }

  const environmentNames = parseAzdEnvironmentNames(
    run("azd", ["env", "list", "--output", "json"], {
      cwd: projectDirectory,
      capture: true,
    }),
  );
  if (!environmentNames.includes(options.environment)) {
    throw new Error(
      `azd environment '${options.environment}' does not exist. Run npm run sync-env-suite -- --environment ${options.environment} first.`,
    );
  }

  run("azd", ["env", "select", options.environment, "--no-prompt"], {
    cwd: projectDirectory,
  });
  const tempDirectory = mkdtempSync(join(tmpdir(), "qabot-drift-"));
  const parserPath = resolve(
    dirname(fileURLToPath(import.meta.url)),
    "list-risky-preview-operations.mjs",
  );
  const riskyOperations: string[] = [];
  const previewEnv = { AZD_PROVISION_PREVIEW: "true" };

  try {
    for (const layer of PROVISION_LAYERS) {
      log(`Previewing provisioning layer '${layer}'...`);
      run(
        "azd",
        ["env", "refresh", options.environment, "--layer", layer, "--no-prompt"],
        { cwd: projectDirectory, env: previewEnv },
      );
      run(
        "azd",
        [
          "hooks",
          "run",
          "preprovision",
          "--layer",
          layer,
          "--environment",
          options.environment,
          "--no-prompt",
        ],
        { cwd: projectDirectory, env: previewEnv },
      );
      const previewOutput = run(
        "azd",
        [
          "provision",
          layer,
          "--preview",
          "--environment",
          options.environment,
          "--no-prompt",
          "--output",
          "json",
        ],
        { cwd: projectDirectory, env: previewEnv, capture: true },
      );
      log(previewOutput);
      const previewPath = join(tempDirectory, `${layer}.json`);
      writeFileSync(previewPath, previewOutput);
      const riskyOutput = run("node", [parserPath, previewPath], { capture: true });
      for (const operation of riskyOutput.split(/\r?\n/).filter(Boolean)) {
        riskyOperations.push(`${layer}: ${operation}`);
      }
    }
  } finally {
    rmSync(tempDirectory, { recursive: true, force: true });
  }

  if (riskyOperations.length > 0) {
    throw new Error(
      "DRIFT DETECTED - azd preview reports Modify or Delete operations:\n" +
        riskyOperations.map((operation) => `  ${operation}`).join("\n"),
    );
  }
  log("No drift detected.");
  return riskyOperations;
}

function printUsage(): void {
  console.log(
    "Usage: npm run detect-drift -- --environment <dev|preview|prod> [--project-directory <path>]",
  );
}

function main(): void {
  const { values } = parseArgs({
    options: {
      environment: { type: "string", short: "e" },
      "project-directory": { type: "string" },
      help: { type: "boolean", short: "h" },
    },
    strict: true,
  });
  if (values.help) {
    printUsage();
    return;
  }
  if (!values.environment) throw new Error("--environment is required.");

  console.log(`Running drift detection for '${values.environment}'...`);
  detectDrift({
    environment: parseEnvironmentName(values.environment),
    projectDirectory: values["project-directory"],
  });
}

if (isDirectExecution(import.meta.url)) runCli(main);