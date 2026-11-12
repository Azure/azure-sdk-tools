import { getEnvironmentConfig, loadEnvironmentSuite } from "./lib/env-suite.js";
import { syncTeamsEnv } from "./lib/sync-teams-env.js";
import { reconcileResourceRoles } from "./lib/resource-roles.js";

const environmentName = process.env.AZURE_ENV_NAME ?? "";

function log(message: string): void {
  console.log(`[frontend:postprovision] ${message}`);
}

try {
  const environment = getEnvironmentConfig(loadEnvironmentSuite(), environmentName);
  reconcileResourceRoles("frontend", process.env, undefined, log);
  syncTeamsEnv({
    azdEnvName: environmentName,
    env: process.env,
    teamsAppId: environment.teamsAppId,
    tenantId: environment.tenantId,
    log,
  });
  log("Generated the azd-owned Teams environment file.");
} catch (error) {
  console.error(`[frontend:postprovision] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}