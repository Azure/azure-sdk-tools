/**
 * seedAppConfiguration — populate the App Configuration store consumed by the
 * agent / backend / function-app with the two classes of values described in
 * the design notes (Azure/azure-sdk-pr#2628):
 *
 *   1. FIXED       — chosen by the project, not derived from provisioning
 *                    (model names, tuning params, feature flags, data-plane
 *                    search names, GitHub App references, blob container names).
 *                    Values come from the design-note table; per-environment
 *                    overrides are honored via matching environment variables.
 *
 *   2. AUTO-INJECTED — derived from the provision / deployment outputs that the
 *                      postprovision hook already persists into the azd
 *                      environment (resource names / endpoints).
 *
 * Every key is written with `az appconfig kv set`, which creates-or-updates, so
 * this is safe to run on every provision.
 *
 * Requires the caller to be logged in via `az login` with data-plane access
 * (App Configuration Data Owner) on the target store; uses `--auth-mode login`.
 */

import { execSync } from "child_process";

export interface SeedAppConfigurationOptions {
  /** App Configuration store name (APP_CONFIG_NAME output). */
  appConfigName: string;
  /** Environment values — normally `process.env` after azd env is loaded. */
  env: NodeJS.ProcessEnv;
  log?: (msg: string) => void;
}

/** Azure DevOps global resource application ID — stable across every tenant. */
const ADO_RESOURCE_APP_ID = "499b84ac-1321-427f-aa17-267ca6975798";

function run(cmd: string): string {
  return execSync(cmd, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

/** App Configuration Data Owner built-in role definition ID. */
const APP_CONFIG_DATA_OWNER_ROLE_ID = "5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b";

/**
 * Ensure the deploying principal has the `App Configuration Data Owner` role on
 * the store. The store has local auth disabled, so data-plane writes require an
 * AAD RBAC assignment; a fresh store (or a fresh deployer) has none, which
 * surfaces as `Forbidden` on the first `az appconfig kv set`. This grants it
 * idempotently so seeding is self-healing.
 */
function ensureDataOwnerRole(
  appConfigName: string,
  env: NodeJS.ProcessEnv,
  log: (msg: string) => void,
): void {
  const subscriptionId = env.AZURE_SUBSCRIPTION_ID?.trim();
  const resourceGroup = env.AZURE_RESOURCE_GROUP?.trim();
  if (!subscriptionId || !resourceGroup) {
    log("  AZURE_SUBSCRIPTION_ID / AZURE_RESOURCE_GROUP not set — cannot ensure RBAC; relying on existing access.");
    return;
  }

  // Prefer the deployer object id detected by the preprovision hook; fall back
  // to the currently signed-in user (local dev).
  let principalId = env.DEVELOPER_PRINCIPAL_ID?.trim();
  let principalType = env.DEVELOPER_PRINCIPAL_TYPE?.trim() || "User";
  if (!principalId) {
    try {
      principalId = run(`az ad signed-in-user show --query id --output tsv`);
      principalType = "User";
    } catch {
      log("  Could not resolve deployer principal id — relying on existing access.");
      return;
    }
  }
  if (!principalId) {
    log("  No principal id available — relying on existing access.");
    return;
  }

  const scope =
    `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}` +
    `/providers/Microsoft.AppConfiguration/configurationStores/${appConfigName}`;

  try {
    run(
      `az role assignment create --assignee-object-id "${principalId}" ` +
        `--assignee-principal-type "${principalType}" ` +
        `--role "${APP_CONFIG_DATA_OWNER_ROLE_ID}" --scope "${scope}" --output none`,
    );
    log(`  ✓ granted App Configuration Data Owner to ${principalId} (${principalType})`);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    if (/RoleAssignmentExists|already exists/i.test(msg)) {
      log(`  ✓ App Configuration Data Owner already assigned to ${principalId}`);
      return;
    }
    // Don't hard-fail: the deployer may already have access via another
    // assignment (e.g. group/subscription scope). Log and let the writes try.
    log(`  ⚠ could not create role assignment (${msg.split("\n")[0]}); continuing.`);
  }
}

/**
 * FIXED App Configuration values. Literals are taken from the design-note
 * table; where a value can legitimately differ per environment (search
 * data-plane names, GitHub App identifiers, the knowledge-base API template)
 * a same-named environment variable overrides the documented default.
 */
export function fixedAppConfigValues(env: NodeJS.ProcessEnv): Record<string, string> {
  const or = (key: string, fallback: string): string => env[key]?.trim() || fallback;
  return {
    // ── Azure DevOps ─────────────────────────────────────────────────────
    ADO_RESOURCE_SCOPE: `${ADO_RESOURCE_APP_ID}/.default`,

    // ── AI Foundry agent ─────────────────────────────────────────────────
    AI_FOUNDRY_AGENT_COMPLETION_MODEL: "gpt-5.4",
    AI_FOUNDRY_AGENT_REASONING_EFFORT: "medium",

    // ── Azure OpenAI chat (model selection + tuning) ─────────────────────
    AOAI_CHAT_COMPLETIONS_MODEL: "gpt-5.1",
    AOAI_CHAT_COMPLETIONS_MODEL_REASONING_EFFORT: "medium",
    AOAI_CHAT_COMPLETIONS_TEMPERATURE: "0.1",
    AOAI_CHAT_COMPLETIONS_TOP_P: "0.8",
    AOAI_CHAT_CONTEXT_MAX_TOKENS: "100000",
    AOAI_CHAT_MAX_TOKENS: "10000",
    AOAI_CHAT_REASONING_MODEL: "gpt-5.1",
    AOAI_CHAT_REASONING_MODEL_REASONING_EFFORT: "high",
    AOAI_CHAT_REASONING_MODEL_TEMPERATURE: "0.1",

    // ── AI Search data-plane names (created by the indexing setup, not ARM;
    //    values taken from the reference store, override per env if needed) ──
    AI_SEARCH_AGENT: or("AI_SEARCH_AGENT", "azure-sdk-knowledge-agent"),
    AI_SEARCH_INDEX: or("AI_SEARCH_INDEX", "azure-sdk-knowledge"),
    AI_SEARCH_INDEXER: or("AI_SEARCH_INDEXER", "azure-sdk-knowledge-indexer"),
    AI_SEARCH_KNOWLEDGE_BASE: or("AI_SEARCH_KNOWLEDGE_BASE", "azure-sdk-knowledgebase"),
    AI_SEARCH_KNOWLEDGE_SOURCE: or("AI_SEARCH_KNOWLEDGE_SOURCE", "azure-sdk-knowledge-source"),
    // Template resolved at runtime by the backend: {AI_SEARCH_BASE_URL} and
    // {AI_SEARCH_AGENT} placeholders are substituted before the request.
    AI_SEARCH_KNOWLEDGE_BASE_API: or(
      "AI_SEARCH_KNOWLEDGE_BASE_API",
      "{AI_SEARCH_BASE_URL}/knowledgebases/{AI_SEARCH_AGENT}/retrieve?api-version=2025-11-01-preview",
    ),
    AI_SEARCH_TOPK: "10",

    // ── Tenant / user memory ─────────────────────────────────────────────
    ENABLE_TENANT_MEMORY_SEARCH: "true",
    MEMORY_STORE_EMBEDDING_MODEL: "text-embedding-ada-002",
    MEMORY_USER_STORE_NAME: "azure-sdk-qa-bot-user-memory-store",

    // ── GitHub App (external; not created by azd — override per env if the
    //    app registration / shared vault differ) ───────────────────────────
    GITHUB_APP_ID: or("GITHUB_APP_ID", "1086291"),
    GITHUB_APP_INSTALLATION_OWNER: or("GITHUB_APP_INSTALLATION_OWNER", "Azure"),
    GITHUB_APP_KEY_NAME: or("GITHUB_APP_KEY_NAME", "azure-sdk-automation"),
    GITHUB_APP_KEYVAULT_URL: or(
      "GITHUB_APP_KEYVAULT_URL",
      "https://azuresdkengkeyvault.vault.azure.net",
    ),

    // ── Blob container names ─────────────────────────────────────────────
    STORAGE_FEEDBACK_CONTAINER: "feedback",
    STORAGE_KNOWLEDGE_CONTAINER: "knowledge",
    STORAGE_RECORDS_CONTAINER: "records",
  };
}

/**
 * AUTO-INJECTED App Configuration values, derived from the provision /
 * deployment outputs the postprovision hook persists into the azd environment.
 * All endpoints are built from the current environment's resource names so they
 * always point at this environment's resources (never the reference store's).
 * Formats mirror the reference store (no trailing slashes; AOAI uses the
 * `openai.azure.com` custom-subdomain host; Cosmos omits the `:443` port).
 */
export function derivedAppConfigValues(env: NodeJS.ProcessEnv): Record<string, string> {
  const required = (key: string): string => {
    const v = env[key]?.trim();
    if (!v) throw new Error(`Cannot seed App Configuration: env ${key} is not set (expected from bicep outputs).`);
    return v;
  };

  const searchServiceName = required("SEARCH_SERVICE_NAME");
  const storageAccountName = required("STORAGE_ACCOUNT_NAME");
  const keyVaultName = required("KEY_VAULT_NAME");
  const aiResourceName = required("AI_RESOURCE_NAME");
  const cosmosAccountName = required("COSMOSDB_ACCOUNT_NAME");

  return {
    ACR_LOGIN_SERVER:
      env.CONTAINER_REGISTRY_LOGIN_SERVER?.trim() ||
      `${required("CONTAINER_REGISTRY_NAME")}.azurecr.io`,
    AI_FOUNDRY_PROJECT_ENDPOINT: required("FOUNDRY_PROJECT_ENDPOINT").replace(/\/+$/, ""),
    AI_SEARCH_SERVICE_NAME: searchServiceName,
    AI_SEARCH_BASE_URL: `https://${searchServiceName}.search.windows.net`,
    // Backend appends '/openai/v1/' to this, so no trailing slash.
    AOAI_CHAT_COMPLETIONS_ENDPOINT: `https://${aiResourceName}.openai.azure.com`,
    AZURE_COSMOSDB_ENDPOINT: `https://${cosmosAccountName}.documents.azure.com`,
    KEYVAULT_ENDPOINT: `https://${keyVaultName}.vault.azure.net`,
    STORAGE_ACCOUNT_NAME: storageAccountName,
    STORAGE_BASE_URL: `https://${storageAccountName}.blob.core.windows.net`,
  };
}

/**
 * Seed the store with both the fixed and the auto-injected value sets.
 */
export function seedAppConfiguration(opts: SeedAppConfigurationOptions): void {
  const { appConfigName, env } = opts;
  const log = opts.log ?? ((m: string) => console.log(`[seed-app-config] ${m}`));

  if (!appConfigName) {
    log("APP_CONFIG_NAME not set — skipping App Configuration seeding.");
    return;
  }

  // The store has local auth disabled, so writes use the deployer's AAD
  // identity. Ensure it holds the `App Configuration Data Owner` role before
  // writing (self-heals a fresh store / fresh deployer).
  ensureDataOwnerRole(appConfigName, env, log);

  // The role assignment can take a short while to propagate, so the first
  // write is retried on a Forbidden/authorization error.
  const setKvOnce = (key: string, value: string): void => {
    run(
      `az appconfig kv set --name "${appConfigName}" --auth-mode login ` +
        `--key "${key}" --value "${value}" --yes --output none`,
    );
  };

  const sleep = (seconds: number): void => {
    execSync(`sleep ${seconds}`, { stdio: "ignore" });
  };

  const setKvWithRetry = (key: string, value: string): void => {
    // App Configuration data-plane RBAC can take several minutes to propagate
    // after the role assignment is created, so allow a generous total window
    // (~8 min) before giving up.
    const delaysSeconds = [0, 15, 30, 30, 45, 60, 60, 60, 60, 60, 60];
    for (let attempt = 0; attempt < delaysSeconds.length; attempt++) {
      try {
        setKvOnce(key, value);
        return;
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        const isAuthDelay = /Forbidden|AuthorizationfailedException|not authorized|403/i.test(msg);
        const isLast = attempt === delaysSeconds.length - 1;
        if (!isAuthDelay || isLast) throw err;
        const wait = delaysSeconds[attempt + 1];
        log(`  … write forbidden (RBAC still propagating); retrying in ${wait}s`);
        sleep(wait);
      }
    }
  };

  const fixed = fixedAppConfigValues(env);
  const derived = derivedAppConfigValues(env);

  // Route every write through the retry wrapper: it absorbs both the initial
  // RBAC propagation delay and the intermittent Forbidden responses the App
  // Configuration data plane occasionally returns mid-batch. The retry backoff
  // only kicks in on a Forbidden/authorization error, so once access is stable
  // each write returns on the first attempt.
  const write = (key: string, value: string): void => {
    setKvWithRetry(key, value);
  };

  log(`Writing ${Object.keys(fixed).length} fixed value(s)...`);
  for (const [key, value] of Object.entries(fixed)) {
    write(key, value);
    log(`  ✓ ${key}`);
  }

  log(`Writing ${Object.keys(derived).length} auto-injected value(s)...`);
  for (const [key, value] of Object.entries(derived)) {
    write(key, value);
    log(`  ✓ ${key} = ${value}`);
  }

  log(`App Configuration '${appConfigName}' seeded (${Object.keys(fixed).length + Object.keys(derived).length} keys).`);
}
