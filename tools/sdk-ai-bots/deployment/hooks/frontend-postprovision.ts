import { getEnvSuiteValue } from "./lib/env-suite.js";
import { ensureManagedIdentityFederatedCredential } from "./lib/ensure-entra-app.js";
import { syncTeamsEnv } from "./lib/sync-teams-env.js";

const environmentName = process.env.AZURE_ENV_NAME ?? "";

function log(message: string): void {
  console.log(`[frontend:postprovision] ${message}`);
}

try {
  const botAppId = process.env.BOT_APP_ID?.trim() || process.env.BOT_ID?.trim();
  const managedIdentityPrincipalId = process.env.BOT_MANAGED_IDENTITY_PRINCIPAL_ID?.trim();
  const tenantId = process.env.BOT_TENANT_ID?.trim() || process.env.AZURE_TENANT_ID?.trim();
  if (!botAppId || !managedIdentityPrincipalId || !tenantId) {
    throw new Error(
      "BOT_APP_ID/BOT_ID, BOT_MANAGED_IDENTITY_PRINCIPAL_ID, and BOT_TENANT_ID " +
        "are required to configure federated bot authentication.",
    );
  }
  ensureManagedIdentityFederatedCredential({
    appId: botAppId,
    managedIdentityPrincipalId,
    tenantId,
  });
  log("Reconciled the Teams bot application's federated credential.");

  syncTeamsEnv({
    azdEnvName: environmentName,
    env: process.env,
    teamsAppId: getEnvSuiteValue(environmentName, "teamsAppId"),
    teamsAppTenantId: getEnvSuiteValue(environmentName, "teamsAppTenantId"),
    log,
  });
  log("Generated the azd-owned Teams environment file.");
} catch (error) {
  console.error(`[frontend:postprovision] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}