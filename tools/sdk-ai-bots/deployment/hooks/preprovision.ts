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

import { execSync } from "child_process";
import { readFileSync, existsSync } from "fs";
import { resolve } from "path";

import { ensureEntraApp } from "./lib/ensure-entra-app.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
const LOCATION = process.env.AZURE_LOCATION ?? "westus2";
const RUNNING_IN_PIPELINE = !!process.env.TF_BUILD || !!process.env.GITHUB_ACTIONS;

const SUITE_PATH = resolve(process.cwd(), "infra/environments/environment-suite.yaml");

function log(msg: string): void {
  console.log(`[preprovision] ${msg}`);
}

function checkPrerequisites(): void {
  log("Checking prerequisites...");
  for (const tool of ["az", "azd"]) {
    try {
      execSync(`command -v ${tool}`, { stdio: "ignore" });
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

  let yqAvailable = false;
  try {
    execSync("command -v yq", { stdio: "ignore" });
    yqAvailable = true;
  } catch {
    log("  yq not on PATH — skipping env-suite drift check.");
    return;
  }
  if (!yqAvailable) return;

  const read = (path: string): string =>
    execSync(`yq -r '${path}' "${SUITE_PATH}"`, { encoding: "utf8" }).trim();

  const expected: Record<string, string> = {
    AZURE_SUBSCRIPTION_ID: read(`.environments.${ENV_NAME}.subscriptionId`),
    AZURE_RESOURCE_GROUP: read(`.environments.${ENV_NAME}.resourceGroupPrefix`),
    AZURE_LOCATION: read(`.environments.${ENV_NAME}.regions[0].name`),
  };

  const drift: string[] = [];
  for (const [key, want] of Object.entries(expected)) {
    if (!want || want === "null" || want.startsWith("REPLACE_WITH_")) continue;
    const have = process.env[key] ?? "";
    if (have !== want) drift.push(`  ${key}: azd='${have}'  suite='${want}'`);
  }

  if (drift.length > 0) {
    throw new Error(
      "azd environment is out of sync with environment-suite.yaml:\n" +
        drift.join("\n") +
        `\n\nRun:  pwsh ./scripts/sync-env-suite.ps1 -Environment ${ENV_NAME}`
    );
  }
  log("  ✓ azd env vars match environment-suite.yaml");
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
  const appId = ensureEntraApp({ displayName });

  // Persist for subsequent azd runs (.azure/<env>/.env).
  execSync(`azd env set SERVER_AUDIENCE ${appId}`, { stdio: "inherit" });
  // Export into the current process so main.bicepparam's readEnvironmentVariable
  // sees the value on the same `azd provision` invocation.
  process.env.SERVER_AUDIENCE = appId;
  log(`  ✓ SERVER_AUDIENCE=${appId} persisted via azd env set`);
}

(async () => {
  log(`Starting preprovision for environment '${ENV_NAME}' in '${LOCATION}'`);
  log(`  Resource group: ${RESOURCE_GROUP || "(not set; will be created by main.bicep)"}`);

  checkPrerequisites();
  validateEnvironmentSuite();
  detectLocalDrift();
  enforceProdGuardrail();
  validateAuth();
  ensureServerAudience();

  log("Preprovision checks passed.");
})().catch((err) => {
  console.error(`[preprovision] FAILED: ${err.message}`);
  process.exit(1);
});
