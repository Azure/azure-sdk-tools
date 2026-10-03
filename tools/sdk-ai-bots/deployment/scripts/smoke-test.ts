import { parseArgs } from "node:util";

import {
  DEFAULT_SUITE_PATH,
  getComponentConfig,
  getEnvironmentConfig,
  loadEnvironmentSuite,
  type EnvironmentName,
} from "../hooks/lib/env-suite.js";
import {
  type CommandRunner,
  isDirectExecution,
  parseEnvironmentName,
  parsePositiveInteger,
  runCli,
  runCommand,
} from "./lib/cli.js";

export const SMOKE_COMPONENTS = ["frontend", "function-app", "agent-server"] as const;
export type SmokeComponent = (typeof SMOKE_COMPONENTS)[number];

const APP_NAME_KEYS: Record<SmokeComponent, "frontendSiteName" | "functionAppName" | "agentServerSiteName"> = {
  frontend: "frontendSiteName",
  "function-app": "functionAppName",
  "agent-server": "agentServerSiteName",
};

export interface SmokeTargetOptions {
  component: SmokeComponent;
  environment: EnvironmentName;
  suitePath?: string;
  appName?: string;
  resourceGroup?: string;
  healthPath?: string;
  slot?: string;
}

export interface SmokeTarget {
  appName: string;
  resourceGroup: string;
  healthPath: string;
  slot: string;
}

export interface SmokeTestOptions extends SmokeTargetOptions {
  easyAuthAudience?: string;
  maxAttempts?: number;
  waitSeconds?: number;
}

interface SmokeDependencies {
  run?: CommandRunner;
  fetch?: typeof fetch;
  delay?: (milliseconds: number) => Promise<void>;
  log?: (message: string) => void;
}

export function parseSmokeComponent(value: string): SmokeComponent {
  if (!SMOKE_COMPONENTS.includes(value as SmokeComponent)) {
    throw new Error(
      `Invalid component '${value}'. Expected one of: ${SMOKE_COMPONENTS.join(", ")}.`,
    );
  }
  return value as SmokeComponent;
}

export function resolveSmokeTarget(options: SmokeTargetOptions): SmokeTarget {
  const suite = loadEnvironmentSuite(options.suitePath ?? DEFAULT_SUITE_PATH);
  const environment = getEnvironmentConfig(suite, options.environment);
  const component = getComponentConfig(suite, options.component);
  const target = {
    appName: options.appName ?? String(environment[APP_NAME_KEYS[options.component]] ?? ""),
    resourceGroup: options.resourceGroup ?? environment.resourceGroupPrefix,
    healthPath: options.healthPath ?? component.healthPath ?? "",
    slot: options.slot ?? "default",
  };
  if (!target.appName || !target.resourceGroup || !target.healthPath) {
    throw new Error(
      `Could not resolve app name, resource group, and health path for ${options.component}/${options.environment}.`,
    );
  }
  return target;
}

export async function runSmokeTest(
  options: SmokeTestOptions,
  dependencies: SmokeDependencies = {},
): Promise<string> {
  const target = resolveSmokeTarget(options);
  const run = dependencies.run ?? runCommand;
  const fetchImpl = dependencies.fetch ?? fetch;
  const delay = dependencies.delay ?? ((milliseconds) =>
    new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds)));
  const log = dependencies.log ?? console.log;
  const showArgs = [
    "webapp",
    "show",
    "--name",
    target.appName,
    "--resource-group",
    target.resourceGroup,
  ];
  if (target.slot !== "default") showArgs.push("--slot", target.slot);
  showArgs.push("--query", "defaultHostName", "--output", "tsv");
  const hostName = run("az", showArgs, { capture: true });
  if (!hostName) {
    throw new Error(
      `Could not resolve hostname for ${target.appName} in ${target.resourceGroup}.`,
    );
  }

  const headers: Record<string, string> = {};
  if (options.easyAuthAudience) {
    const token = run(
      "az",
      [
        "account",
        "get-access-token",
        "--resource",
        options.easyAuthAudience,
        "--query",
        "accessToken",
        "--output",
        "tsv",
      ],
      { capture: true },
    );
    if (!token) {
      throw new Error(`Could not acquire an access token for ${options.easyAuthAudience}.`);
    }
    headers.Authorization = `Bearer ${token}`;
  }

  const url = `https://${hostName}${target.healthPath}`;
  const maxAttempts = options.maxAttempts ?? 12;
  const waitMilliseconds = (options.waitSeconds ?? 10) * 1_000;
  log(`Probing ${url}`);
  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    try {
      const response = await fetchImpl(url, { headers });
      if (response.status === 200) {
        log(`  OK (HTTP 200) on attempt ${attempt}`);
        return url;
      }
      log(`  attempt ${attempt}: HTTP ${response.status}`);
    } catch (error) {
      log(`  attempt ${attempt}: ${error instanceof Error ? error.message : String(error)}`);
    }
    if (attempt < maxAttempts) await delay(waitMilliseconds);
  }
  throw new Error(`Smoke test failed for ${url}.`);
}

function printUsage(): void {
  console.log(
    "Usage: npm run smoke-test -- --component <name> --environment <env> [options]",
  );
}

async function main(): Promise<void> {
  const { values } = parseArgs({
    options: {
      component: { type: "string", short: "c" },
      environment: { type: "string", short: "e" },
      suite: { type: "string" },
      "app-name": { type: "string" },
      "resource-group": { type: "string" },
      slot: { type: "string" },
      "health-path": { type: "string" },
      "easy-auth-audience": { type: "string" },
      "max-attempts": { type: "string" },
      "wait-seconds": { type: "string" },
      "resolve-only": { type: "boolean" },
      help: { type: "boolean", short: "h" },
    },
    strict: true,
  });
  if (values.help) {
    printUsage();
    return;
  }
  if (!values.component || !values.environment) {
    throw new Error("--component and --environment are required.");
  }

  const options: SmokeTestOptions = {
    component: parseSmokeComponent(values.component),
    environment: parseEnvironmentName(values.environment),
    suitePath: values.suite,
    appName: values["app-name"],
    resourceGroup: values["resource-group"],
    slot: values.slot,
    healthPath: values["health-path"],
    easyAuthAudience: values["easy-auth-audience"],
    maxAttempts: values["max-attempts"]
      ? parsePositiveInteger(values["max-attempts"], "--max-attempts")
      : undefined,
    waitSeconds: values["wait-seconds"]
      ? parsePositiveInteger(values["wait-seconds"], "--wait-seconds")
      : undefined,
  };
  if (values["resolve-only"]) {
    console.log(JSON.stringify(resolveSmokeTarget(options)));
    return;
  }
  await runSmokeTest(options);
}

if (isDirectExecution(import.meta.url)) runCli(main);