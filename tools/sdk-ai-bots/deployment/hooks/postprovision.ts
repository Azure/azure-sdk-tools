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

import { execSync } from "child_process";
import { INFRA_LAYERS } from "./lib/layers.js";
import { runLayerPipeline } from "./lib/deploy-layer.js";
import { uploadBotConfigs } from "./lib/upload-bot-configs.js";
import { seedAppConfiguration } from "./lib/seed-app-config.js";
import { seedKeyVaultSecrets } from "./lib/seed-key-vault.js";
import { ensureRoleAssignment } from "./lib/ensure-role-assignment.js";
import { syncTeamsEnv } from "./lib/sync-teams-env.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
const LOCATION = process.env.AZURE_LOCATION ?? "westus2";
const STORAGE_ACCOUNT_NAME = process.env.STORAGE_ACCOUNT_NAME ?? "";

function log(msg: string): void {
  console.log(`[postprovision] ${msg}`);
}

/**
 * Reads the subscription-scoped deployment outputs produced by main.bicep and
 * writes each one into the azd environment with `azd env set KEY VALUE`.
 *
 * azd names the subscription-level deployment `<ENV_NAME>-<timestamp>` (e.g.
 * `dev-1783668832`), so we resolve the most recent succeeded deployment whose
 * name starts with `<ENV_NAME>-` rather than assuming an exact `<ENV_NAME>`.
 *
 * Only runs when triggered by a full `azd provision` (i.e. DEPLOY_LAYER is not
 * set). When DEPLOY_LAYER is set the caller is doing a targeted per-layer
 * re-deploy and the top-level main.bicep was not re-run, so its outputs have
 * not changed and there is nothing new to persist.
 */
async function persistBicepOutputs(): Promise<void> {
  if (process.env.DEPLOY_LAYER) {
    log(`DEPLOY_LAYER=${process.env.DEPLOY_LAYER} — skipping bicep output persistence (main.bicep was not re-run).`);
    return;
  }

  log("Persisting bicep outputs into azd environment...");

  // Resolve azd's actual deployment name: the most recent succeeded
  // subscription deployment whose name starts with `<ENV_NAME>-`.
  const deploymentName = execSync(
    `az deployment sub list --subscription "${SUBSCRIPTION_ID}" ` +
      `--query "sort_by([?starts_with(name, '${ENV_NAME}-') && properties.provisioningState=='Succeeded'], &properties.timestamp)[-1].name" ` +
      `-o tsv`,
    { encoding: "utf8" }
  ).trim();

  if (!deploymentName) {
    throw new Error(
      `No succeeded subscription deployment found matching '${ENV_NAME}-*'. ` +
        `Did 'azd provision' complete the infra deployment?`
    );
  }
  log(`Resolved deployment name: ${deploymentName}`);

  const raw = execSync(
    `az deployment sub show --name "${deploymentName}" --subscription "${SUBSCRIPTION_ID}" --query "properties.outputs" -o json`,
    { encoding: "utf8" }
  );

  const outputs: Record<string, { value: string }> = JSON.parse(raw);

  // ARM returns `properties.outputs` keys transformed by .NET's camelCase JSON
  // naming policy (e.g. AI_PROJECT_NAME → aI_PROJECT_NAME, ACTION_GROUP_NAME →
  // actioN_GROUP_NAME). All main.bicep outputs are SCREAMING_SNAKE_CASE, so
  // upper-casing restores the original name and avoids `azd env set` creating a
  // case-mismatched duplicate of the var azd already persisted.
  for (const [key, entry] of Object.entries(outputs)) {
    const name = key.toUpperCase();
    const value = String(entry.value ?? "");
    execSync(`azd env set "${name}" "${value.replace(/"/g, '\\"')}"`, {
      stdio: "inherit",
    });
    // Also expose the value to the current process so later steps in this hook
    // (e.g. ensureAgentAccountRoleAssignments) can read the freshly persisted
    // outputs without re-loading the azd environment.
    process.env[name] = value;
    log(`  set ${name}`);
  }

  log(`Persisted ${Object.keys(outputs).length} outputs.`);
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

/**
 * Generate the azd-owned Teams Toolkit env file (azure-sdk-qa-bot/env/.env.azd)
 * from the frontend bicep outputs (bot site resource id, domain, id, tenant)
 * that azd just persisted. azd decides the logical environment (AZURE_ENV_NAME =
 * dev / preview / prod) and stamps its outputs onto the matching committed base
 * env; `teamsapp <cmd> --env azd` then consumes the single generated file. This
 * replaces the frontend teamsapp.yml's former `arm/deploy` step: azd provisions
 * the resources and Teams only owns the app manifest + bot registration + publish.
 *
 * Skipped for targeted per-layer re-deploys (DEPLOY_LAYER set), which do not
 * re-run main.bicep and therefore have no fresh outputs to propagate.
 */
function syncTeamsEnvStep(): void {
  if (process.env.DEPLOY_LAYER) {
    log(`DEPLOY_LAYER=${process.env.DEPLOY_LAYER} — skipping Teams env sync (main.bicep was not re-run).`);
    return;
  }
  log("Generating the azd-owned Teams env file (.env.azd)...");
  syncTeamsEnv({
    azdEnvName: ENV_NAME,
    env: process.env,
    log: (m) => log(m),
  });
}

/** "Azure AI User" (Foundry User) built-in role definition ID. */
const FOUNDRY_USER_ROLE_ID = "53ca6127-db72-4b80-b1b0-d745d6d5456d";

/**
 * Ensure qabot-identity has the "Azure AI User" (Foundry User) role on the AI
 * account, creating it only if it does not already exist. This grant lives here
 * rather than in bicep because a native roleAssignments resource fails the whole
 * deployment with RoleAssignmentExists when the same (principal, role, scope)
 * already exists under a different name — e.g. one created out-of-band that
 * cannot be deleted in the lock-protected dev RG. qabot-identity needs this role
 * so the agent server (server.py) can read the hosted Foundry agent via
 * `project_client.agents.get(...)` (data action AIServices/agents/read). See the
 * note in infra/modules/qaBotAgent/component.bicep.
 */
function ensureAgentAccountRoleAssignments(): void {
  const principalId = process.env.MANAGED_IDENTITY_PRINCIPAL_ID ?? "";
  const aiResourceName = process.env.AI_RESOURCE_NAME ?? "";
  if (!principalId || !aiResourceName || !SUBSCRIPTION_ID || !RESOURCE_GROUP) {
    log(
      "Skipping AI account role assignment (missing identity principal, AI account name, subscription, or resource group)."
    );
    return;
  }
  log("Ensuring qabot-identity has 'Azure AI User' (Foundry User) on the AI account...");
  ensureRoleAssignment({
    principalId,
    roleDefinitionId: FOUNDRY_USER_ROLE_ID,
    scope: `/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.CognitiveServices/accounts/${aiResourceName}`,
    principalType: "ServicePrincipal",
    log: (m) => log(m),
  });
}

function printSummary(): void {
  log("─────────────────────────────────────────────────────");
  log(`Environment : ${ENV_NAME}`);
  log(`Subscription: ${SUBSCRIPTION_ID}`);
  log(`Resource grp: ${RESOURCE_GROUP}`);
  log("");
  log("Next: run `azd deploy` to push the frontend, function-app, and agent images.");
  log("`azd deploy frontend` also runs `teamsapp provision --env azd` internally,");
  log("using the env/.env.azd file generated from this azd environment.");
  log("─────────────────────────────────────────────────────");
}

(async () => {
  log(`Starting postprovision for environment '${ENV_NAME}'`);
  await persistBicepOutputs();
  ensureAgentAccountRoleAssignments();
  await runInfraLayers();
  uploadPerEnvBotConfigs();
  seedKeyVaultSecretsStep();
  updateAppConfiguration();
  syncTeamsEnvStep();
  printSummary();
  log("Postprovision complete.");
})().catch((err) => {
  console.error(`[postprovision] FAILED: ${err.message}`);
  process.exit(1);
});
