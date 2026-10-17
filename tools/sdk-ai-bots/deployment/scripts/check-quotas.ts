import { parseArgs } from "node:util";

import {
  buildAzdEnvironmentValues,
  loadEnvironmentSuite,
} from "../hooks/lib/env-suite.js";
import { runQuotaCheck } from "../hooks/lib/quota-check.js";
import {
  type CommandRunner,
  isDirectExecution,
  runCli,
  runCommand,
} from "./lib/cli.js";

export function checkQuotas(
  argv: string[],
  env: NodeJS.ProcessEnv = process.env,
  run: CommandRunner = runCommand,
  log: (message: string) => void = console.log,
) {
  const { values } = parseArgs({
    args: argv,
    options: {
      subscription: { type: "string", short: "s" },
      location: { type: "string", short: "l" },
      env: { type: "string", short: "e" },
      help: { type: "boolean", short: "h" },
    },
  });
  if (values.help) {
    log(
      "Usage: npm run check-quotas -- [--env <name>] [--subscription <id>] [--location <region>]\n" +
      "Checks additional allocations against existing resources. AI uses AZURE_AI_LOCATION.\n" +
      "Defaults come from process values, saved azd values, and the selected environment suite.\n" +
      "Exit codes: 0 = sufficient quota; 1 = insufficient quota or verification failed.",
    );
    return;
  }
  const requestedEnvironment = values.env ?? env.AZURE_ENV_NAME;
  if (values.env && env.AZURE_ENV_NAME && values.env !== env.AZURE_ENV_NAME) {
    throw new Error("--env conflicts with AZURE_ENV_NAME; select the intended environment first.");
  }
  const saved: Record<string, string> = {};
  let raw: string | undefined;
  try {
    raw = run(
      "azd",
      ["env", "get-values", ...(requestedEnvironment ? ["--environment", requestedEnvironment] : [])],
      { capture: true },
    );
  } catch (error) {
    log(
      "[check-quotas] Saved azd values are unavailable; using configured deployment values. " +
      (error instanceof Error ? error.message : String(error)),
    );
  }
  for (const line of raw?.split(/\r?\n/) ?? []) {
    const match = /^([A-Z0-9_]+)="?(.*?)"?$/.exec(line.trim());
    if (match) saved[match[1]] = match[2];
  }
  if (
    requestedEnvironment && saved.AZURE_ENV_NAME &&
    requestedEnvironment !== saved.AZURE_ENV_NAME
  ) {
    throw new Error("Saved azd values belong to a different environment.");
  }
  const environmentName = requestedEnvironment ?? saved.AZURE_ENV_NAME;
  const defaults = environmentName
    ? buildAzdEnvironmentValues(loadEnvironmentSuite(), environmentName)
    : {};
  const deploymentEnv = { ...defaults, ...saved, ...env };
  const subscriptionId = values.subscription ?? deploymentEnv.AZURE_SUBSCRIPTION_ID ?? "";
  const location = values.location ?? deploymentEnv.AZURE_LOCATION ?? "";
  log(
    `[check-quotas] subscription=${subscriptionId} location=${location} ` +
    `AI=${deploymentEnv.AZURE_AI_LOCATION || location} env=${environmentName || "(none)"}`,
  );
  const result = runQuotaCheck({
    subscriptionId,
    location,
    envName: environmentName,
    env: deploymentEnv,
  }, run);
  for (const warning of result.warnings) log(`[check-quotas] ${warning}`);
  log(result.ok ? "[check-quotas] Additional quota requirements satisfied." : result.message);
  return result;
}

if (isDirectExecution(import.meta.url)) {
  runCli(() => {
    const result = checkQuotas(process.argv.slice(2));
    if (result && !result.ok) process.exitCode = 1;
  });
}
