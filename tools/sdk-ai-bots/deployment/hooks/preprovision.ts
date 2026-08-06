/**
 * preprovision hook — runs before `azd provision`.
 *
 * Value-adds beyond a stub:
 *
 *   - Reads `infra/environments/environment-suite.yaml` and validates that
 *     `AZURE_ENV_NAME` matches one of the declared environments.
 *   - Enforces `prodDeployOnlyFromPipeline: true` by failing fast when run
 *     outside Azure DevOps for the prod env.
 *   - Detects local-dev drift between the env-suite and the azd env vars
 *     and tells the developer to run scripts/sync-env-suite.ps1.
 *   - Ensures the Entra ID app registration backing `serverAudience` exists
 *     and exports its clientId as SERVER_AUDIENCE so main.bicepparam picks
 *     it up on the same provision run.
 */

import { execFileSync, execSync } from "child_process";
import { readFileSync, existsSync } from "fs";
import { resolve } from "path";

import { ensureEntraApp } from "./lib/ensure-entra-app.js";
import { getEnvSuiteValue, getEnvSuiteValues } from "./lib/env-suite.js";
import { runQuotaCheck } from "./lib/quota-check.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
const LOCATION = process.env.AZURE_LOCATION ?? "westus2";
const RUNNING_IN_PIPELINE = !!process.env.TF_BUILD || !!process.env.GITHUB_ACTIONS;

const SUITE_PATH = resolve(process.cwd(), "deployment/infra/environments/environment-suite.yaml");
const PARAMETERS_PATH = resolve(process.cwd(), `deployment/infra/environments/${ENV_NAME}.parameters.json`);

function log(msg: string): void {
  console.log(`[preprovision] ${msg}`);
}

function checkPrerequisites(): void {
  log("Checking prerequisites...");
  const lookup = process.platform === "win32" ? "where" : "command -v";
  for (const tool of ["az", "azd"]) {
    try {
      execSync(`${lookup} ${tool}`, { stdio: "ignore" });
      log(`  ✓ ${tool} found`);
    } catch {
      throw new Error(`Required tool '${tool}' is not installed or not on PATH.`);
    }
  }
}

function validateEnvironmentSuite(): void {
  log("Validating environment-suite contract...");
  if (!existsSync(SUITE_PATH)) {
    throw new Error(`environment-suite.yaml not found at ${SUITE_PATH}`);
  }

  const text = readFileSync(SUITE_PATH, "utf8");
  const declared = ["dev", "preview", "prod"].filter((e) =>
    new RegExp(`^\\s{2,4}${e}:`, "m").test(text)
  );
  if (!ENV_NAME) {
    log("  AZURE_ENV_NAME not set — assuming developer-local 'dev' run.");
    return;
  }
  if (!declared.includes(ENV_NAME)) {
    throw new Error(
      `AZURE_ENV_NAME='${ENV_NAME}' is not declared in environment-suite.yaml. ` +
        `Declared: ${declared.join(", ")}.`
    );
  }
  log(`  ✓ '${ENV_NAME}' is declared`);
}

/**
 * Local-only drift detection: compares values azd loaded from
 * .azure/<env>/.env against the per-env block in environment-suite.yaml.
 * If they differ, instruct the developer to run sync-env-suite.ps1.
 *
 * Skipped in pipelines (the pipeline gets values from
 * load-environment-suite.yml directly — no drift possible).
 */
function detectLocalDrift(): void {
  if (RUNNING_IN_PIPELINE || !ENV_NAME) return;

  const expected: Record<string, string> = {};
  const lookup = process.platform === "win32" ? "where" : "command -v";
  let yqAvailable = false;
  try {
    execSync(`${lookup} yq`, { stdio: "ignore" });
    yqAvailable = true;
  } catch {
    log("  yq not on PATH — skipping env-suite portion of drift check.");
  }

  if (yqAvailable) {
    const read = (path: string): string =>
      execFileSync("yq", ["-r", path, SUITE_PATH], { encoding: "utf8" }).trim();

    expected.AZURE_SUBSCRIPTION_ID = read(`.environments.${ENV_NAME}.subscriptionId`);
    expected.AZURE_RESOURCE_GROUP = read(`.environments.${ENV_NAME}.resourceGroupPrefix`);
    expected.AZURE_LOCATION = read(`.environments.${ENV_NAME}.regions[0].name`);
  }

  if (!existsSync(PARAMETERS_PATH)) {
    throw new Error(`Environment parameters file not found: ${PARAMETERS_PATH}`);
  }
  const parameters = JSON.parse(readFileSync(PARAMETERS_PATH, "utf8")).parameters ?? {};
  const teamsGroupId = getEnvSuiteValue(ENV_NAME, "teamsGroupId", SUITE_PATH) ?? "";
  const teamsChannelIds = getEnvSuiteValues(ENV_NAME, "teamsChannelIds", SUITE_PATH);
  if (!teamsGroupId || teamsGroupId.startsWith("REPLACE_WITH_")) {
    throw new Error(`teamsGroupId is missing or still a placeholder in ${SUITE_PATH}`);
  }
  if (
    teamsChannelIds.length === 0 ||
    teamsChannelIds.some((id: string) => !id || id.startsWith("REPLACE_WITH_"))
  ) {
    throw new Error(`teamsChannelIds is empty or contains a placeholder in ${SUITE_PATH}`);
  }
  expected.TEAMS_GROUP_ID = teamsGroupId;
  expected.TEAMS_CHANNEL_IDS = teamsChannelIds.join(",");
  const serverAudience = String(parameters.serverAudience?.value ?? "");
  if (serverAudience && !serverAudience.startsWith("REPLACE_WITH_")) {
    expected.SERVER_AUDIENCE = serverAudience;
  }

  const drift: string[] = [];
  for (const [key, want] of Object.entries(expected)) {
    if (!want || want === "null" || want.startsWith("REPLACE_WITH_")) continue;
    const have = process.env[key] ?? "";
    if (have !== want) drift.push(`  ${key}: azd='${have}'  expected='${want}'`);
  }

  if (drift.length > 0) {
    throw new Error(
      "azd environment is out of sync with its configuration sources:\n" +
        drift.join("\n") +
        `\n\nRun:  pwsh ./scripts/sync-env-suite.ps1 -Environment ${ENV_NAME}`
    );
  }
  log("  ✓ azd env vars match environment-suite.yaml and the environment parameters file");
}

function enforceProdGuardrail(): void {
  if (ENV_NAME === "prod" && !RUNNING_IN_PIPELINE) {
    throw new Error(
      "Refusing to provision prod from a non-pipeline context. " +
        "Set TF_BUILD or GITHUB_ACTIONS if this really is a pipeline run."
    );
  }
}

function validateAuth(): void {
  log("Validating Azure authentication...");
  if (SUBSCRIPTION_ID) log(`  Target subscription: ${SUBSCRIPTION_ID}`);
  else log("  AZURE_SUBSCRIPTION_ID not set — azd will use the default subscription.");
}

/**
 * Pre-flight quota check for the providers this deployment consumes.
 * Delegates to hooks/lib/quota-check.ts so scripts/check-quotas.ts can share
 * the same logic. Opt out with SKIP_QUOTA_CHECK=1.
 */
function checkResourceQuotas(): void {
  if (process.env.SKIP_QUOTA_CHECK === "1") {
    log("SKIP_QUOTA_CHECK=1 — skipping quota verification.");
    return;
  }
  log(`Checking resource quotas in '${LOCATION}'...`);
  if (!SUBSCRIPTION_ID) {
    log("  AZURE_SUBSCRIPTION_ID not set — skipping quota check.");
    return;
  }

  const result = runQuotaCheck({
    subscriptionId: SUBSCRIPTION_ID,
    location: LOCATION,
    envName: ENV_NAME,
  });

  for (const p of result.unreachable) {
    log(`  ${p} @ ${LOCATION}: unable to query usages (skipping)`);
  }
  for (const w of result.warnings) log(`  ⚠ ${w}`);

  if (result.ok) {
    log("  ✓ quota check passed");
    return;
  }

  throw new Error(result.message + "\n\nOr set SKIP_QUOTA_CHECK=1 to bypass this gate.");
}

/**
 * Ensures the Entra ID app registration that backs `serverAudience` exists,
 * then persists its clientId (appId) as SERVER_AUDIENCE so main.bicepparam
 * picks it up in this same provision run.
 *
 * No-op when SERVER_AUDIENCE is already set (e.g. pipelines pass it in
 * explicitly via environments/<env>.parameters.json).
 */
function ensureServerAudience(): void {
  const existing = process.env.SERVER_AUDIENCE?.trim();
  if (existing) {
    log(`  ✓ SERVER_AUDIENCE already set (${existing}); skipping Entra app creation`);
    return;
  }
  if (!ENV_NAME) {
    log("  AZURE_ENV_NAME not set — skipping Entra app registration.");
    return;
  }

  const displayName = `azuresdkqabot-server-${ENV_NAME}`;
  log(`SERVER_AUDIENCE not set — ensuring Entra app registration '${displayName}'`);
  const appId = ensureEntraApp({
    displayName,
    ownedDisplayNameContains: "qabot",
    serviceManagementReference: process.env.SERVICE_MANAGEMENT_REFERENCE?.trim() || undefined,
  });

  // Persist for subsequent azd runs (.azure/<env>/.env).
  execSync(`azd env set SERVER_AUDIENCE ${appId}`, { stdio: "inherit" });
  // Export into the current process so main.bicepparam's readEnvironmentVariable
  // sees the value on the same `azd provision` invocation.
  process.env.SERVER_AUDIENCE = appId;
  log(`  ✓ SERVER_AUDIENCE=${appId} persisted via azd env set`);
}

/**
 * Detects the currently authenticated principal's object ID and type, then
 * persists them as DEVELOPER_PRINCIPAL_ID / DEVELOPER_PRINCIPAL_TYPE so Bicep
 * grants the deployer direct access to Azure resources (Key Vault, ACR, etc.).
 *
 * Only runs when DEVELOPER_PRINCIPAL_ID is not already set.
 */
function ensureDeveloperPrincipal(): void {
  if (process.env.DEVELOPER_PRINCIPAL_ID?.trim()) {
    log(`  ✓ DEVELOPER_PRINCIPAL_ID already set (${process.env.DEVELOPER_PRINCIPAL_ID.trim()})`);
    return;
  }
  log("DEVELOPER_PRINCIPAL_ID not set — detecting current auth principal...");
  try {
    // Try signed-in user first (interactive / user login).
    const userId = execSync(
      "az ad signed-in-user show --query id -o tsv 2>/dev/null",
      { encoding: "utf8" }
    ).trim();
    if (userId) {
      execSync(`azd env set DEVELOPER_PRINCIPAL_ID ${userId}`, { stdio: "inherit" });
      execSync(`azd env set DEVELOPER_PRINCIPAL_TYPE User`, { stdio: "inherit" });
      process.env.DEVELOPER_PRINCIPAL_ID = userId;
      process.env.DEVELOPER_PRINCIPAL_TYPE = "User";
      log(`  ✓ DEVELOPER_PRINCIPAL_ID=${userId} (User)`);
      return;
    }
  } catch {
    // Not a user login — fall through to service principal.
  }
  try {
    // Service principal (pipeline / federated login).
    const spId = execSync(
      "az ad sp show --id $(az account show --query user.name -o tsv) --query id -o tsv 2>/dev/null",
      { encoding: "utf8" }
    ).trim();
    if (spId) {
      execSync(`azd env set DEVELOPER_PRINCIPAL_ID ${spId}`, { stdio: "inherit" });
      execSync(`azd env set DEVELOPER_PRINCIPAL_TYPE ServicePrincipal`, { stdio: "inherit" });
      process.env.DEVELOPER_PRINCIPAL_ID = spId;
      process.env.DEVELOPER_PRINCIPAL_TYPE = "ServicePrincipal";
      log(`  ✓ DEVELOPER_PRINCIPAL_ID=${spId} (ServicePrincipal)`);
      return;
    }
  } catch {
    // ignore
  }
  log("  ⚠ Could not detect principal ID — developer role assignments will be skipped.");
}

/**
 * Probe the Teams `Microsoft.Web/connections` resource. When it exists AND its
 * status is "Connected" (OAuth consent already completed), set
 * CREATE_TEAMS_CONNECTION=false so bicep leaves it untouched — re-issuing the
 * PUT on an authorized OAuth connection can invalidate the bound token.
 * Otherwise (missing, not yet Connected, or unknown), set true so the
 * connection shell is created / repaired on this provision.
 *
 * The connection name is either persisted from a previous provision
 * (TEAMS_CONNECTION_NAME output of main.bicep) or, on a very first provision,
 * absent — in which case we default to creating (true).
 */
function ensureTeamsConnectionFlag(): void {
  const connectionName = process.env.TEAMS_CONNECTION_NAME?.trim();
  const rg = RESOURCE_GROUP;
  if (!connectionName || !rg || !SUBSCRIPTION_ID) {
    log(
      "  Teams connection name / RG / subscription not yet known — leaving " +
        "CREATE_TEAMS_CONNECTION at its default (true; first provision).",
    );
    return;
  }
  log(`Checking Teams API connection '${connectionName}' status in '${rg}'...`);
  let status = "";
  try {
    status = execSync(
      `az resource show --resource-type Microsoft.Web/connections ` +
        `--subscription "${SUBSCRIPTION_ID}" -g "${rg}" -n "${connectionName}" ` +
        `--query "properties.statuses[0].status" -o tsv 2>/dev/null`,
      { encoding: "utf8" },
    ).trim();
  } catch {
    // Not found or transient error — fall through to default (create).
  }
  if (status === "Connected") {
    execSync(`azd env set CREATE_TEAMS_CONNECTION false`, { stdio: "inherit" });
    process.env.CREATE_TEAMS_CONNECTION = "false";
    log(`  ✓ '${connectionName}' is Connected — CREATE_TEAMS_CONNECTION=false (skip PUT)`);
    return;
  }
  execSync(`azd env set CREATE_TEAMS_CONNECTION true`, { stdio: "inherit" });
  process.env.CREATE_TEAMS_CONNECTION = "true";
  log(
    `  Teams connection status='${status || "not-found"}' — ` +
      `CREATE_TEAMS_CONNECTION=true (bicep will PUT the shell).`,
  );
}

/**
 * Preserve the full Logic App definition on subsequent provisions. The first
 * provision still creates an empty workflow shell because the Function App
 * runtime is not available for ARM validation until after `azd deploy`.
 */
function ensureLogicAppWorkflowFlag(): void {
  const includeDefinitionEnv = "INCLUDE_LOGIC_APP_WORKFLOW_DEFINITION";
  if (!RESOURCE_GROUP || !SUBSCRIPTION_ID) {
    log(
      "  Resource group / subscription not yet known — " +
        `${includeDefinitionEnv}=false (first provision deploys the workflow shell).`,
    );
    process.env[includeDefinitionEnv] = "false";
    return;
  }

  const configuredWorkflowName = process.env.LOGIC_APP_WORKFLOW_NAME?.trim();
  let workflowNames: string[] = [];
  try {
    const raw = execSync(
      `az resource list --resource-group "${RESOURCE_GROUP}" ` +
        `--subscription "${SUBSCRIPTION_ID}" ` +
        `--resource-type Microsoft.Logic/workflows ` +
        `--query "[?${configuredWorkflowName
          ? `name=='${configuredWorkflowName}'`
          : "starts_with(name, 'azuresdkqabot-logicapp-')"}].name" -o json`,
      { encoding: "utf8" },
    );
    workflowNames = JSON.parse(raw);
  } catch {
    // A missing resource group or transient lookup failure is equivalent to a
    // first provision; Bicep will create the shell and postdeploy fills it.
  }

  const includeDefinition = workflowNames.length > 0;
  const value = String(includeDefinition);
  execSync(`azd env set ${includeDefinitionEnv} ${value}`, { stdio: "inherit" });
  process.env[includeDefinitionEnv] = value;

  if (includeDefinition) {
    log(
      `  ✓ Existing Logic App workflow found (${workflowNames.join(", ")}) — ` +
        `${includeDefinitionEnv}=true (provision full definition).`,
    );
  } else {
    log(
      `  No Logic App workflow found in '${RESOURCE_GROUP}' — ` +
        `${includeDefinitionEnv}=false (provision workflow shell).`,
    );
  }
}

(async () => {
  log(`Starting preprovision for environment '${ENV_NAME}' in '${LOCATION}'`);
  log(`  Resource group: ${RESOURCE_GROUP || "(not set; will be created by main.bicep)"}`);

  checkPrerequisites();
  validateEnvironmentSuite();
  detectLocalDrift();
  enforceProdGuardrail();
  validateAuth();
  checkResourceQuotas();
  ensureServerAudience();
  ensureDeveloperPrincipal();
  ensureTeamsConnectionFlag();
  ensureLogicAppWorkflowFlag();

  log("Preprovision checks passed.");
})().catch((err) => {
  console.error(`[preprovision] FAILED: ${err.message}`);
  process.exit(1);
});
