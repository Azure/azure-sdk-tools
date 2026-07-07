/**
 * postprovision hook — runs after `azd provision`.
 *
 * Ported from ../../azd-experiments/hooks/postprovision.ts.
 *
 * Drives the infra-only layer pipeline (shared-resources → agent-platform →
 * backend → logic-app) via hooks/lib/deploy-layer.ts. Application services
 * (frontend, function-app, agent) are azd services and are handled by
 * `azd deploy` with their own per-service hooks.
 *
 *   DEPLOY_LAYER=agent-platform azd provision   — deploy only one layer
 */

import { INFRA_LAYERS } from "./lib/layers.js";
import { runLayerPipeline } from "./lib/deploy-layer.js";
import { uploadBotConfigs } from "./lib/upload-bot-configs.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
const LOCATION = process.env.AZURE_LOCATION ?? "westus2";
const STORAGE_ACCOUNT_NAME = process.env.STORAGE_ACCOUNT_NAME ?? "";

function log(msg: string): void {
  console.log(`[postprovision] ${msg}`);
}

async function runInfraLayers(): Promise<void> {
  log("Starting infra layer pipeline...");
  await runLayerPipeline(INFRA_LAYERS, {
    resourceGroup: RESOURCE_GROUP,
    subscriptionId: SUBSCRIPTION_ID,
    location: LOCATION,
  });
}

function uploadPerEnvBotConfigs(): void {
  uploadBotConfigs({
    envName: ENV_NAME,
    storageAccountName: STORAGE_ACCOUNT_NAME,
    log,
  });
}

function seedKeyVaultSecrets(): void {
  log("Seeding Key Vault secrets...");
  // TODO: populate per-env Key Vault. See docs/runbook-deploy.md §"Seed secrets".
}

function updateAppConfiguration(): void {
  log("Updating App Configuration store...");
  // TODO: write Deployment:* and BotSettings:* keys consumed by the backend.
}

function printSummary(): void {
  log("─────────────────────────────────────────────────────");
  log(`Environment : ${ENV_NAME}`);
  log(`Subscription: ${SUBSCRIPTION_ID}`);
  log(`Resource grp: ${RESOURCE_GROUP}`);
  log("");
  log("Next: run `azd deploy` to push the frontend, function-app, and agent images.");
  log("─────────────────────────────────────────────────────");
}

(async () => {
  log(`Starting postprovision for environment '${ENV_NAME}'`);
  await runInfraLayers();
  uploadPerEnvBotConfigs();
  seedKeyVaultSecrets();
  updateAppConfiguration();
  printSummary();
  log("Postprovision complete.");
})().catch((err) => {
  console.error(`[postprovision] FAILED: ${err.message}`);
  process.exit(1);
});
