/**
 * agent — postdeploy hook
 *
 * 1. Grant the hosted Foundry agent's runtime managed identity (the auto-created
 *    "…-AgentIdentity") the data-plane roles it needs. In a hosted container the
 *    agent runs as this identity (not the shared qabot-identity), and its very
 *    first startup step — `await app_config.init()` — reads Azure App
 *    Configuration via that identity. Without at least "App Configuration Data
 *    Reader" the container crashes on boot, its /readiness never returns 200, and
 *    every session fails with 424 session_not_ready → /agent/chat returns 500.
 * 2. Ensure the server app registration (SERVER_AUDIENCE) is fully authorized
 *    so callers can obtain tokens EasyAuth accepts (service principal,
 *    identifier URI, delegated scope, application app-role, pre-authorized
 *    clients, and the managed-identity app-role assignment).
 * 3. Sanity-check the hosted agent by pinging its /ping endpoint.
 */

import { execSync } from "child_process";
import { ensureServerAppAuthorization } from "./lib/ensure-entra-app.js";

const AGENT_BASE_URL = process.env.AGENT_BASE_URL ?? "";

function log(msg: string): void {
  console.log(`[agent:postdeploy] ${msg}`);
}

/** Load azd env values into process.env if not already present. */
function loadAzdEnv(): void {
  try {
    const raw = execSync("azd env get-values", { encoding: "utf8" });
    for (const line of raw.split(/\r?\n/)) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith("#")) continue;
      const eq = trimmed.indexOf("=");
      if (eq === -1) continue;
      const key = trimmed.slice(0, eq).trim();
      let val = trimmed.slice(eq + 1).trim();
      if (val.startsWith('"') && val.endsWith('"')) {
        val = val.slice(1, -1).replace(/\\"/g, '"').replace(/\\\\/g, "\\").replace(/\\n/g, "\n");
      }
      if (!process.env[key]) process.env[key] = val;
    }
  } catch {
    // azd env get-values may fail if no environment is selected; ignore.
  }
}

/**
 * Ensure the deployed agent server's Entra app registration is authorized.
 * No-op-friendly: skips when SERVER_AUDIENCE is unset (e.g. audience supplied
 * out-of-band), and each underlying step is idempotent.
 */
function ensureAgentServerAuthorization(): void {
  loadAzdEnv();
  const appId = process.env.SERVER_AUDIENCE?.trim();
  if (!appId) {
    log("SERVER_AUDIENCE not set — skipping server app authorization.");
    return;
  }
  // The frontend/bot calls the server via its user-assigned managed identity
  // (DefaultAzureCredential → app-only token), so grant it the app-role.
  const managedIdentityPrincipalId = process.env.MANAGED_IDENTITY_PRINCIPAL_ID?.trim() || undefined;
  // Also pre-authorize the bot app for the delegated scope when present.
  const botAppId = process.env.BOT_AUDIENCE?.trim();
  ensureServerAppAuthorization({
    appId,
    managedIdentityPrincipalId,
    preAuthorizeClientIds: botAppId ? [botAppId] : [],
  });
}

/**
 * Resolve the hosted agent's runtime managed-identity principal (object) ID by
 * reading the deployed agent version from the Foundry data-plane API. The
 * container runs as `instance_identity` (the auto-created "…-AgentIdentity"
 * ServiceIdentity), which is what needs the data-plane role grants.
 */
async function getAgentIdentityPrincipalId(): Promise<string | undefined> {
  let endpoint = process.env.AGENT_AGENT_ENDPOINT?.trim();
  if (!endpoint) {
    const projectEndpoint = process.env.FOUNDRY_PROJECT_ENDPOINT?.trim();
    const agentName = process.env.AGENT_AGENT_NAME?.trim();
    const agentVersion = process.env.AGENT_AGENT_VERSION?.trim();
    if (projectEndpoint && agentName && agentVersion) {
      endpoint = `${projectEndpoint}/agents/${agentName}/versions/${agentVersion}`;
    }
  }
  if (!endpoint) {
    log("AGENT_AGENT_ENDPOINT not set and cannot be derived — skipping agent identity RBAC.");
    return undefined;
  }

  const token = execSync(
    "az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv",
    { encoding: "utf8" }
  ).trim();

  const url = `${endpoint}${endpoint.includes("?") ? "&" : "?"}api-version=2025-05-15-preview`;
  const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
  if (!res.ok) {
    log(`Failed to read agent version (HTTP ${res.status}) — skipping agent identity RBAC.`);
    return undefined;
  }
  const body = (await res.json()) as {
    instance_identity?: { principal_id?: string };
  };
  const principalId = body.instance_identity?.principal_id?.trim();
  if (!principalId) {
    log("Agent version has no instance_identity.principal_id — skipping agent identity RBAC.");
    return undefined;
  }
  return principalId;
}

/**
 * Idempotently grant a built-in role to a service principal at a scope.
 * Returns true on success (including when the assignment already exists).
 */
function grantRole(principalId: string, role: string, scope: string): boolean {
  try {
    execSync(
      `az role assignment create --assignee-object-id "${principalId}" ` +
        `--assignee-principal-type ServicePrincipal --role "${role}" --scope "${scope}"`,
      { stdio: "pipe", encoding: "utf8" }
    );
    log(`  ✓ granted "${role}"`);
    return true;
  } catch (err) {
    const msg = err instanceof Error && "stderr" in err ? String((err as { stderr?: unknown }).stderr ?? err.message) : String(err);
    if (/RoleAssignmentExists|already exists/i.test(msg)) {
      log(`  ✓ "${role}" already assigned`);
      return true;
    }
    log(`  ✗ failed to grant "${role}": ${msg.split("\n")[0]}`);
    return false;
  }
}

/**
 * Grant the hosted agent's runtime identity every data-plane role it needs.
 * Mirrors the role set held by the shared qabot-identity, since the hosted
 * container authenticates as the agent's own identity (AZURE_CLIENT_ID is unset
 * in Foundry hosted containers, so ManagedIdentityCredential() resolves to the
 * agent identity rather than the shared one).
 *
 * "App Configuration Data Reader" is the critical, boot-blocking grant: without
 * it the container crashes in app_config.init() before it can serve /readiness.
 */
async function ensureAgentIdentityRbac(): Promise<void> {
  loadAzdEnv();

  const sub = process.env.AZURE_SUBSCRIPTION_ID?.trim();
  const rg = process.env.AZURE_RESOURCE_GROUP?.trim();
  if (!sub || !rg) {
    log("AZURE_SUBSCRIPTION_ID / AZURE_RESOURCE_GROUP not set — skipping agent identity RBAC.");
    return;
  }

  const principalId = await getAgentIdentityPrincipalId();
  if (!principalId) return;

  const providers = `/subscriptions/${sub}/resourceGroups/${rg}/providers`;
  const scopeFor = (type: string, name: string | undefined) =>
    name ? `${providers}/${type}/${name}` : undefined;

  const appConfigScope = scopeFor("Microsoft.AppConfiguration/configurationStores", process.env.APP_CONFIG_NAME?.trim());
  const aiScope = scopeFor("Microsoft.CognitiveServices/accounts", process.env.AI_RESOURCE_NAME?.trim());
  const storageScope = scopeFor("Microsoft.Storage/storageAccounts", process.env.STORAGE_ACCOUNT_NAME?.trim());
  const searchScope = scopeFor("Microsoft.Search/searchServices", process.env.SEARCH_SERVICE_NAME?.trim());
  const kvScope = scopeFor("Microsoft.KeyVault/vaults", process.env.KEY_VAULT_NAME?.trim());

  // [role, scope, critical] — critical grants abort the hook on failure.
  const grants: Array<[string, string | undefined, boolean]> = [
    ["App Configuration Data Reader", appConfigScope, true],
    ["Cognitive Services OpenAI User", aiScope, false],
    ["Foundry User", aiScope, false],
    ["Storage Blob Data Owner", storageScope, false],
    ["Storage Table Data Contributor", storageScope, false],
    ["Storage Queue Data Contributor", storageScope, false],
    ["Search Index Data Contributor", searchScope, false],
    ["Key Vault Secrets User", kvScope, false],
  ];

  log(`Granting data-plane roles to hosted agent identity ${principalId}`);
  let criticalFailed = false;
  for (const [role, scope, critical] of grants) {
    if (!scope) {
      log(`  – skipping "${role}" (target resource name not set)`);
      if (critical) criticalFailed = true;
      continue;
    }
    const ok = grantRole(principalId, role, scope);
    if (!ok && critical) criticalFailed = true;
  }

  if (criticalFailed) {
    throw new Error(
      "Failed to grant the critical 'App Configuration Data Reader' role to the hosted " +
        "agent identity; the agent container will crash on startup until this is resolved."
    );
  }
  log("Agent identity RBAC complete.");
}

(async () => {
  await ensureAgentIdentityRbac();

  ensureAgentServerAuthorization();

  if (!AGENT_BASE_URL) {
    log("AGENT_BASE_URL not set — skipping ping.");
    return;
  }
  log(`Pinging ${AGENT_BASE_URL}/ping`);
  const res = await fetch(`${AGENT_BASE_URL}/ping`);
  log(`  HTTP ${res.status}`);
  if (!res.ok) throw new Error(`Agent ping failed: HTTP ${res.status}`);
})().catch((err) => {
  console.error(`[agent:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
