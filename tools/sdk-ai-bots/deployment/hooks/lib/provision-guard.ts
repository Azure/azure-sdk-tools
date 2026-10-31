import {
  getEnvironmentConfig,
  loadEnvironmentSuite,
  type EnvironmentSuite,
} from "./env-suite.js";
import { type CommandRunner, runCommand } from "../../scripts/lib/cli.js";

export function enforceLocalOperationAllowed(
  operation: "provision" | "deploy",
  env: NodeJS.ProcessEnv = process.env,
  suite: EnvironmentSuite = loadEnvironmentSuite(),
): void {
  const environmentName = env.AZURE_ENV_NAME?.trim();
  const runningInPipeline = !!env.TF_BUILD || !!env.GITHUB_ACTIONS;
  if (!environmentName || runningInPipeline) return;

  const environment = getEnvironmentConfig(suite, environmentName);
  if (!environment.localDeployAllowed) {
    throw new Error(
      `Refusing to ${operation} '${environmentName}' from a non-pipeline context. ` +
        "Use the environment's deployment pipeline.",
    );
  }
}

export function enforceProvisionGuard(
  env: NodeJS.ProcessEnv = process.env,
  run: CommandRunner = runCommand,
  suite: EnvironmentSuite = loadEnvironmentSuite(),
): void {
  enforceLocalOperationAllowed("provision", env, suite);
  const subscription = env.AZURE_SUBSCRIPTION_ID?.trim();
  const group = env.AZURE_RESOURCE_GROUP?.trim();
  if (!subscription || !group) {
    throw new Error("AZURE_SUBSCRIPTION_ID and AZURE_RESOURCE_GROUP must select an existing resource group.");
  }
  let id: string;
  try {
    id = run("az", [
      "group", "show", "--subscription", subscription, "--name", group,
      "--query", "id", "--output", "tsv",
    ], { capture: true });
  } catch (error) {
    throw new Error(
      `Resource group '${group}' must already exist and be readable in subscription '${subscription}': ` +
      (error instanceof Error ? error.message : String(error)),
      { cause: error },
    );
  }
  const expected = `/subscriptions/${subscription}/resourceGroups/${group}`;
  if (id.trim().toLowerCase() !== expected.toLowerCase()) {
    throw new Error(`Cannot verify the selected existing resource group '${expected}'.`);
  }
}