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
 *   - Detects the deploying principal from its ARM access token without
 *     requiring Microsoft Graph access.
 */

import { execFileSync, execSync } from "child_process";
import { readFileSync, existsSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

import { getEnvSuiteValue, getEnvSuiteValues } from "./lib/env-suite.js";
import { enforceProvisionGuard } from "./lib/provision-guard.js";
import { runQuotaCheck } from "./lib/quota-check.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const LOCATION = process.env.AZURE_LOCATION ?? "westus2";
const RUNNING_IN_PIPELINE = !!process.env.TF_BUILD || !!process.env.GITHUB_ACTIONS;

const SUITE_PATH = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../infra/environments/environment-suite.yaml",
);

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
    expected.AZURE_TENANT_ID = read(`.environments.${ENV_NAME}.tenantId`);
    expected.AZURE_RESOURCE_GROUP = read(`.environments.${ENV_NAME}.resourceGroupPrefix`);
    expected.AZURE_LOCATION = read(`.environments.${ENV_NAME}.regions[0].name`);
    expected.AZURE_AI_LOCATION = read(`.environments.${ENV_NAME}.aiLocation`);
    expected.AZURE_AI_DEPLOYMENTS_LOCATION = expected.AZURE_AI_LOCATION;
    expected.COSMOS_DB_LOCATION = read(`.environments.${ENV_NAME}.cosmosDbLocation`);
    expected.CHATBOT_EVOLUTION_AGENT_ENABLED = read(
      `.environments.${ENV_NAME}.chatbotEvolutionAgentEnabled`,
    );
    expected.FRONTEND_SITE_NAME = read(`.environments.${ENV_NAME}.frontendSiteName`);
    expected.AGENT_SERVER_SITE_NAME = read(`.environments.${ENV_NAME}.agentServerSiteName`);
    expected.FUNCTION_APP_NAME = read(`.environments.${ENV_NAME}.functionAppName`);
    expected.ACR_NAME = read(`.environments.${ENV_NAME}.containerRegistryName`);
    expected.CONTAINER_REGISTRY_NAME = expected.ACR_NAME;
    expected.KEY_VAULT_NAME = read(`.environments.${ENV_NAME}.keyVaultName`);
    expected.APP_CONFIG_NAME = read(`.environments.${ENV_NAME}.appConfigName`);
    expected.FRONTEND_IMAGE_REPOSITORY = `${read('.components.frontend.imageName')}:${ENV_NAME}`;
    expected.AGENT_SERVER_IMAGE_REPOSITORY = `${read('.components."agent-server".imageName')}:${ENV_NAME}`;
    expected.FUNCTION_IMAGE_REPOSITORY = `${read('.components."function-app".imageName')}:${ENV_NAME}`;

    const overrides = JSON.parse(
      execFileSync(
        "yq",
        ["-o=json", `.environments.${ENV_NAME}.bicepOverrides // {}`, SUITE_PATH],
        { encoding: "utf8" },
      ),
    );
    Object.assign(expected, overrides);
  }

  const serverApplicationClientId =
    getEnvSuiteValue(ENV_NAME, "serverApplicationClientId", SUITE_PATH) ?? "";
  const serverApplicationIdUri =
    getEnvSuiteValue(ENV_NAME, "serverApplicationIdUri", SUITE_PATH) ?? "";
  if (!serverApplicationClientId || serverApplicationClientId.startsWith("REPLACE_WITH_")) {
    throw new Error(`serverApplicationClientId is missing or still a placeholder in ${SUITE_PATH}`);
  }
  if (!serverApplicationIdUri || serverApplicationIdUri.startsWith("REPLACE_WITH_")) {
    throw new Error(`serverApplicationIdUri is missing or still a placeholder in ${SUITE_PATH}`);
  }
  expected.SERVER_AUDIENCE = serverApplicationClientId;
  expected.SERVER_APPLICATION_ID_URI = serverApplicationIdUri;
  expected.RAG_SERVICE_SCOPE = `${serverApplicationIdUri}/.default`;

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
  log(
    yqAvailable
      ? "  ✓ azd env vars match environment-suite.yaml"
      : "  ✓ fixed identity and Teams routing match environment-suite.yaml; full drift check skipped without yq",
  );
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
    const token = execFileSync(
      "az",
      [
        "account",
        "get-access-token",
        "--resource",
        "https://management.azure.com/",
        "--query",
        "accessToken",
        "--output",
        "tsv",
      ],
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
    ).trim();
    const payloadSegment = token.split(".")[1];
    if (!payloadSegment) throw new Error("ARM access token is not a JWT");

    const payload = JSON.parse(
      Buffer.from(payloadSegment.replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8"),
    ) as { oid?: string; idtyp?: string };
    const principalId = payload.oid?.trim();
    if (!principalId) throw new Error("ARM access token has no oid claim");

    const accountType = execFileSync(
      "az",
      ["account", "show", "--query", "user.type", "--output", "tsv"],
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
    ).trim().toLowerCase();
    const principalType = accountType === "serviceprincipal" || payload.idtyp === "app"
      ? "ServicePrincipal"
      : "User";

    execFileSync("azd", ["env", "set", "DEVELOPER_PRINCIPAL_ID", principalId], { stdio: "inherit" });
    execFileSync("azd", ["env", "set", "DEVELOPER_PRINCIPAL_TYPE", principalType], { stdio: "inherit" });
    process.env.DEVELOPER_PRINCIPAL_ID = principalId;
    process.env.DEVELOPER_PRINCIPAL_TYPE = principalType;
    log(`  ✓ DEVELOPER_PRINCIPAL_ID=${principalId} (${principalType})`);
    return;
  } catch {
    log("  ⚠ Could not detect principal ID — developer role assignments will be skipped.");
  }
}

(async () => {
  log(`Starting preprovision for environment '${ENV_NAME}' in '${LOCATION}'`);

  checkPrerequisites();
  validateEnvironmentSuite();
  detectLocalDrift();
  enforceProvisionGuard();
  validateAuth();
  checkResourceQuotas();
  ensureDeveloperPrincipal();

  log("Preprovision checks passed.");
})().catch((err) => {
  console.error(`[preprovision] FAILED: ${err.message}`);
  process.exit(1);
});
