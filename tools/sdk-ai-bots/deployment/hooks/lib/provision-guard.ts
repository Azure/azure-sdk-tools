import {
  getEnvironmentConfig,
  loadEnvironmentSuite,
  type EnvironmentSuite,
} from "./env-suite.js";

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

export function enforceProvisionGuard(): void {
  enforceLocalOperationAllowed("provision");
}