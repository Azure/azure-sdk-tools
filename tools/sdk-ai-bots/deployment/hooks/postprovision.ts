/**
 * postprovision hook — runs after `azd provision`.
 *
 * Ported from ../../azd-experiments/hooks/postprovision.ts.
 *
 * Runs after the final Logic App layer. Seeds Key Vault / App Configuration
 * and uploads bot configuration. Layer outputs are persisted by azd;
 * role-assignment adoption and Teams environment generation belong to their
 * owning layers.
 */

import { uploadBotConfigs } from "./lib/upload-bot-configs.js";
import { seedAppConfiguration } from "./lib/seed-app-config.js";
import { seedKeyVaultSecrets } from "./lib/seed-key-vault.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
const STORAGE_ACCOUNT_NAME = process.env.STORAGE_ACCOUNT_NAME ?? "";

function log(msg: string): void {
  console.log(`[postprovision] ${msg}`);
}

function uploadPerEnvBotConfigs(): void {
  uploadBotConfigs({
    envName: ENV_NAME,
    storageAccountName: STORAGE_ACCOUNT_NAME,
    log,
  });
}

function seedKeyVaultSecretsStep(): void {
  log("Seeding Key Vault secrets...");
  seedKeyVaultSecrets({
    keyVaultName: process.env.KEY_VAULT_NAME ?? "",
    resourceGroup: RESOURCE_GROUP,
    searchServiceName: process.env.SEARCH_SERVICE_NAME ?? "",
    aiResourceName: process.env.AI_RESOURCE_NAME ?? "",
    env: process.env,
    log: (m) => log(m),
  });
}

function updateAppConfiguration(): void {
  log("Updating App Configuration store...");
  seedAppConfiguration({
    appConfigName: process.env.APP_CONFIG_NAME ?? "",
    env: process.env,
    log: (m) => log(m),
  });
}

function printSummary(): void {
  log("─────────────────────────────────────────────────────");
  log(`Environment : ${ENV_NAME}`);
  log(`Subscription: ${SUBSCRIPTION_ID}`);
  log(`Resource grp: ${RESOURCE_GROUP}`);
  log("");
  log("Next: run `azd deploy` to push the frontend, agent-server, function-app, and agent images.");
  log("`azd deploy frontend` also runs `teamsapp provision --env azd` internally,");
  log("using the env/.env.azd file generated from this azd environment.");
  log("─────────────────────────────────────────────────────");
}

(async () => {
  log(`Starting postprovision for environment '${ENV_NAME}'`);
  uploadPerEnvBotConfigs();
  seedKeyVaultSecretsStep();
  updateAppConfiguration();
  printSummary();
  log("Postprovision complete.");
})().catch((err) => {
  console.error(`[postprovision] FAILED: ${err.message}`);
  process.exit(1);
});
