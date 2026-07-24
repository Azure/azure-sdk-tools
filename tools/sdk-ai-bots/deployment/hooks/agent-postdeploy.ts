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
 * 2. Pin the hosted agent to the freshly-built image and inject the
 *    AZURE_APPCONFIG_ENDPOINT environment variable into its container.
 *    app_config.init() reads this on startup and raises RuntimeError (→ container
 *    crash → 424 session_not_ready) if it is missing. azd neither embeds
 *    agent.yaml `environment_variables` into the version definition (its
 *    "Registering agent environment variables" step is a no-op) nor can point
 *    azure.yaml at the freshly-built immutable tag (${VAR} is resolved before the
 *    predeploy build). So we create a follow-up agent version that pins the fresh
 *    image (AGENT_DEPLOYED_IMAGE) and embeds environment_variables — the only
 *    mechanism the Foundry runtime honours. Idempotent.
 * 3. Ensure the server app registration (SERVER_AUDIENCE) is fully authorized
 *    so callers can obtain tokens EasyAuth accepts (service principal,
 *    identifier URI, delegated scope, application app-role, pre-authorized
 *    clients, and the managed-identity app-role assignment).
 * 4. Sanity-check the hosted agent by pinging its /ping endpoint.
 */

import { execSync } from "child_process";
import { ensureServerAppAuthorization } from "./lib/ensure-entra-app.js";

const AGENT_BASE_URL = process.env.AGENT_BASE_URL ?? "";

// Foundry data-plane api-version that returns/accepts environment_variables in
// the hosted agent version definition.
const AGENT_API_VERSION = "2025-11-15-preview";

/** Subset of the hosted agent version `definition` we read/clone. */
interface AgentDefinition {
  container_configuration?: { image?: string };
  environment_variables?: Record<string, string>;
  [key: string]: unknown;
}

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
  // Also pre-authorize the bot's client id for the delegated scope when present.
  // BOT_ID is the bot's MicrosoftAppId (UAMI clientId in UserAssignedMsi mode).
  // BOT_AUDIENCE is a token audience (Bot Framework Service URL) — not a client id.
  const botAppId = process.env.BOT_ID?.trim();
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

/**
 * Resolve the Foundry project endpoint (…/api/projects/<project>) used for the
 * agent versions data-plane API.
 */
function getProjectEndpoint(): string | undefined {
  const direct = process.env.FOUNDRY_PROJECT_ENDPOINT?.trim();
  if (direct) return direct.replace(/\/+$/, "");
  // Derive from the full agent-version endpoint if only that is set.
  const agentEndpoint = process.env.AGENT_AGENT_ENDPOINT?.trim();
  const m = agentEndpoint?.match(/^(.*\/api\/projects\/[^/]+)\/agents\//);
  return m ? m[1] : undefined;
}

/** Acquire a Foundry data-plane bearer token via the Azure CLI. */
function getFoundryToken(): string {
  return execSync(
    "az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv",
    { encoding: "utf8" }
  ).trim();
}

/**
 * Ensure the hosted agent's LATEST version runs the freshly-built image AND
 * carries the AZURE_APPCONFIG_ENDPOINT environment variable in its container
 * definition.
 *
 * Two azd limitations force this hook to own the final version:
 *   1. azd's "Registering agent environment variables" step does NOT embed
 *      agent.yaml `environment_variables` into the version definition, so the
 *      container boots without AZURE_APPCONFIG_ENDPOINT and crashes in
 *      app_config.init() (→ 424 session_not_ready).
 *   2. azd resolves azure.yaml `${VAR}` at project-load time (before the
 *      predeploy image build), so azure.yaml can only reference a static image
 *      tag — it cannot point at the freshly-built immutable tag.
 *
 * So azd deploys a placeholder version from the static tag, and here we create a
 * follow-up version that pins the freshly-built image (AGENT_DEPLOYED_IMAGE) and
 * embeds environment_variables — the mechanism the Foundry hosted runtime
 * honours — making it @latest.
 *
 * Idempotent: if the latest version already runs the target image with the
 * correct AZURE_APPCONFIG_ENDPOINT we do nothing, so re-running postdeploy
 * without a fresh `azd deploy` is a no-op.
 */
async function ensureAgentAppConfigEnv(): Promise<void> {
  loadAzdEnv();

  const appConfigEndpoint = process.env.AZURE_APPCONFIG_ENDPOINT?.trim();
  if (!appConfigEndpoint) {
    log("AZURE_APPCONFIG_ENDPOINT not set — skipping hosted agent env injection.");
    return;
  }
  const projectEndpoint = getProjectEndpoint();
  const agentName = process.env.AGENT_AGENT_NAME?.trim();
  if (!projectEndpoint || !agentName) {
    log("FOUNDRY_PROJECT_ENDPOINT / AGENT_AGENT_NAME not set — skipping hosted agent env injection.");
    return;
  }

  const token = getFoundryToken();
  const authHeader = { Authorization: `Bearer ${token}` };
  const versionsUrl = `${projectEndpoint}/agents/${agentName}/versions?api-version=${AGENT_API_VERSION}`;

  // Resolve the latest version (versions list is returned newest-first).
  const listRes = await fetch(versionsUrl, { headers: authHeader });
  if (!listRes.ok) {
    log(`Failed to list agent versions (HTTP ${listRes.status}) — skipping env injection.`);
    return;
  }
  const list = (await listRes.json()) as {
    data?: Array<{ version?: string; definition?: AgentDefinition }>;
  };
  const versions = (list.data ?? [])
    .map((v) => ({ n: Number(v.version), def: v.definition }))
    .filter((v) => Number.isFinite(v.n))
    .sort((a, b) => b.n - a.n);
  if (versions.length === 0) {
    log("No agent versions found — skipping env injection.");
    return;
  }
  const latest = versions[0];
  const def = latest.def;
  const latestImage = def?.container_configuration?.image;
  if (!def || !latestImage) {
    log(`Latest agent version ${latest.n} has no container image — skipping env injection.`);
    return;
  }

  // Prefer the freshly-built immutable tag from agent-predeploy.ts; fall back to
  // whatever image the latest version already runs (e.g. env-only re-runs).
  const targetImage = process.env.AGENT_DEPLOYED_IMAGE?.trim() || latestImage;

  if (
    latestImage === targetImage &&
    def.environment_variables?.AZURE_APPCONFIG_ENDPOINT === appConfigEndpoint
  ) {
    log(
      `Latest agent version ${latest.n} already runs ${targetImage} with ` +
        `AZURE_APPCONFIG_ENDPOINT — no new version needed.`
    );
    return;
  }

  log(
    `Creating hosted agent version: image ${targetImage} + AZURE_APPCONFIG_ENDPOINT ` +
      `(latest v${latest.n} runs ${latestImage}, env=${def.environment_variables?.AZURE_APPCONFIG_ENDPOINT ?? "unset"}).`
  );

  const newDefinition: AgentDefinition = {
    ...def,
    container_configuration: { ...def.container_configuration, image: targetImage },
    environment_variables: {
      ...(def.environment_variables ?? {}),
      AZURE_APPCONFIG_ENDPOINT: appConfigEndpoint,
    },
  };
  const body = {
    definition: newDefinition,
    metadata: {
      enableVnextExperience: "true",
      appconfigEnvInjected: targetImage,
    },
  };

  const createUrl = `${projectEndpoint}/agents/${agentName}/versions?api-version=${AGENT_API_VERSION}`;
  const createRes = await fetch(createUrl, {
    method: "POST",
    headers: { ...authHeader, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!createRes.ok) {
    const detail = await createRes.text().catch(() => "");
    throw new Error(
      `Failed to create hosted agent version with AZURE_APPCONFIG_ENDPOINT ` +
        `(HTTP ${createRes.status}): ${detail.slice(0, 500)}`
    );
  }
  const created = (await createRes.json()) as {
    version?: string;
    definition?: AgentDefinition;
  };
  const injected = created.definition?.environment_variables?.AZURE_APPCONFIG_ENDPOINT;
  if (injected !== appConfigEndpoint) {
    throw new Error(
      `Created agent version ${created.version} but AZURE_APPCONFIG_ENDPOINT was not persisted.`
    );
  }
  log(`  ✓ created agent version ${created.version} (image ${targetImage}, AZURE_APPCONFIG_ENDPOINT set, @latest).`);
}

(async () => {
  await ensureAgentIdentityRbac();

  await ensureAgentAppConfigEnv();

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
