import { dirname, resolve } from "node:path";
import { parseArgs } from "node:util";

import {
  buildAzdEnvironmentValues,
  DEFAULT_SUITE_PATH,
  loadEnvironmentSuite,
  type EnvironmentName,
} from "../hooks/lib/env-suite.js";
import {
  type CommandRunner,
  isDirectExecution,
  parseAzdEnvironmentNames,
  parseEnvironmentName,
  runCli,
  runCommand,
} from "./lib/cli.js";

export interface SyncEnvironmentOptions {
  environment: EnvironmentName;
  suitePath?: string;
  projectDirectory?: string;
}

export function syncEnvironment(
  options: SyncEnvironmentOptions,
  run: CommandRunner = runCommand,
  log: (message: string) => void = console.log,
): Record<string, string> {
  const suitePath = resolve(options.suitePath ?? DEFAULT_SUITE_PATH);
  const projectDirectory = resolve(
    options.projectDirectory ?? resolve(dirname(suitePath), "../../.."),
  );
  const values = buildAzdEnvironmentValues(
    loadEnvironmentSuite(suitePath),
    options.environment,
  );
  const invalidKeys = Object.entries(values)
    .filter(([, value]) => !value || value.includes("REPLACE_WITH_"))
    .map(([key]) => key);
  if (invalidKeys.length > 0) {
    throw new Error(
      `Environment '${options.environment}' contains missing values or placeholders: ${invalidKeys.join(", ")}.`,
    );
  }

  const existingNames = parseAzdEnvironmentNames(
    run("azd", ["env", "list", "--output", "json"], {
      cwd: projectDirectory,
      capture: true,
    }),
  );
  if (!existingNames.includes(options.environment)) {
    log(`Creating azd environment '${options.environment}'...`);
    run("azd", ["env", "new", options.environment, "--no-prompt"], {
      cwd: projectDirectory,
    });
  }
  run("azd", ["env", "select", options.environment, "--no-prompt"], {
    cwd: projectDirectory,
  });

  log(`Syncing environment-suite.yaml to azd env '${options.environment}'...`);
  for (const [key, value] of Object.entries(values).sort(([left], [right]) =>
    left.localeCompare(right))) {
    run("azd", ["env", "set", key, value], { cwd: projectDirectory });
    log(`  ${key} = ${value}`);
  }
  log(`azd env '${options.environment}' is in sync with environment-suite.yaml.`);
  return values;
}

function printUsage(): void {
  console.log(
    "Usage: npm run sync-env-suite -- --environment <dev|preview|prod> [--suite <path>]",
  );
}

function main(): void {
  const { values } = parseArgs({
    options: {
      environment: { type: "string", short: "e" },
      suite: { type: "string" },
      help: { type: "boolean", short: "h" },
    },
    strict: true,
  });
  if (values.help) {
    printUsage();
    return;
  }
  if (!values.environment) throw new Error("--environment is required.");

  syncEnvironment({
    environment: parseEnvironmentName(values.environment),
    suitePath: values.suite,
  });
}

if (isDirectExecution(import.meta.url)) runCli(main);