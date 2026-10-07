/**
 * preprovision hook — runs before `azd provision`.
 *
 * Value-adds beyond a stub:
 *
 *   - Reads `infra/environments/environment-suite.yaml` and validates that
 *     `AZURE_ENV_NAME` matches one of the declared environments.
 *   - Enforces the selected environment's `localDeployAllowed` policy.
 *   - Detects local-dev drift between the env-suite and the azd env vars
 *     and tells the developer to run the TypeScript sync command.
 *   - Detects the deploying principal from its ARM access token without
 *     requiring Microsoft Graph access. The active principal is persisted
 *     separately from the optional developer user/group so automation always
 *     receives its own data-plane access.
 */

import { execFileSync, execSync } from "child_process";
import { existsSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

import {
  buildAzdEnvironmentValues,
  getEnvironmentConfig,
  loadEnvironmentSuite,
} from "./lib/env-suite.js";
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

  const declared = Object.keys(loadEnvironmentSuite(SUITE_PATH).environments);
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
 * If they differ, instruct the developer to run the TypeScript sync command.
 *
 * Skipped in pipelines (the pipeline gets values from
 * load-environment-suite.yml directly — no drift possible).
 */
function detectLocalDrift(): void {
  if (RUNNING_IN_PIPELINE || !ENV_NAME) return;

  const suite = loadEnvironmentSuite(SUITE_PATH);
  const environment = getEnvironmentConfig(suite, ENV_NAME);
  const expected = buildAzdEnvironmentValues(suite, ENV_NAME);
  if (
    !environment.serverApplicationClientId ||
    environment.serverApplicationClientId.startsWith("REPLACE_WITH_")
  ) {
    throw new Error(`serverApplicationClientId is missing or still a placeholder in ${SUITE_PATH}`);
  }
  if (
    !environment.serverApplicationIdUri ||
    environment.serverApplicationIdUri.startsWith("REPLACE_WITH_")
  ) {
    throw new Error(`serverApplicationIdUri is missing or still a placeholder in ${SUITE_PATH}`);
  }
  if (!environment.teamsGroupId || environment.teamsGroupId.startsWith("REPLACE_WITH_")) {
    throw new Error(`teamsGroupId is missing or still a placeholder in ${SUITE_PATH}`);
  }
  if (
    environment.teamsChannelIds.length === 0 ||
    environment.teamsChannelIds.some((id) => !id || id.startsWith("REPLACE_WITH_"))
  ) {
    throw new Error(`teamsChannelIds is empty or contains a placeholder in ${SUITE_PATH}`);
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
        `\n\nRun: npm run sync-env-suite -- --environment ${ENV_NAME}`
    );
  }
  log("  ✓ azd env vars match environment-suite.yaml");
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
 * Detects the currently authenticated principal's object ID and type. The
 * deployment identity is refreshed on every provision. The developer identity
 * is configuration-owned and is never inferred from the deployment identity.
 */
function ensureAccessPrincipals(): void {
  log("Detecting the current deployment principal...");
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

    execFileSync("azd", ["env", "set", "DEPLOYMENT_PRINCIPAL_ID", principalId], { stdio: "inherit" });
    execFileSync("azd", ["env", "set", "DEPLOYMENT_PRINCIPAL_TYPE", principalType], { stdio: "inherit" });
    process.env.DEPLOYMENT_PRINCIPAL_ID = principalId;
    process.env.DEPLOYMENT_PRINCIPAL_TYPE = principalType;
    log(`  ✓ DEPLOYMENT_PRINCIPAL_ID=${principalId} (${principalType})`);

    if (process.env.DEVELOPER_PRINCIPAL_ID?.trim()) {
      log(`  ✓ preserving DEVELOPER_PRINCIPAL_ID=${process.env.DEVELOPER_PRINCIPAL_ID.trim()}`);
    } else {
      log("  No developer principal configured; developer role assignments will be skipped.");
    }
    return;
  } catch {
    log("  ⚠ Could not detect principal ID — deployment-principal role assignments will be skipped.");
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
  ensureAccessPrincipals();

  log("Preprovision checks passed.");
})().catch((err) => {
  console.error(`[preprovision] FAILED: ${err.message}`);
  process.exit(1);
});
