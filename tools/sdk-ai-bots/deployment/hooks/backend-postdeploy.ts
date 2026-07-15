/**
 * backend — postdeploy hook
 *
 * Health-check the backend main site after `azd deploy backend`. The main site
 * is the production App Service (BACKEND_SITE_NAME) — note SERVER_BASE_URL points
 * at the `agent` slot (where the agent-server runs), which is a different app, so
 * we build the URL from BACKEND_SITE_NAME here. azd may also pass
 * SERVICE_BACKEND_URI for the deployed target.
 *
 * Before the health check we re-pin the ACR pull identity: `azd deploy` for an
 * appservice/docker service overwrites the site's container config and clears
 * `acrUserManagedIdentityID`, so the platform falls back to the (nonexistent)
 * system-assigned identity and the image pull fails with 503. main.bicep sets
 * this at provision time, but every `azd deploy backend` wipes it, so it must be
 * re-applied here (idempotent).
 */

import { execSync } from "child_process";
import { seedKeyVaultSecrets } from "./lib/seed-key-vault.js";

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

loadAzdEnv();

const SITE_NAME = process.env.BACKEND_SITE_NAME ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const MI_CLIENT_ID = process.env.MANAGED_IDENTITY_CLIENT_ID ?? "";
const KEY_VAULT_NAME = process.env.KEY_VAULT_NAME ?? "";
const BACKEND_URL =
  process.env.SERVICE_BACKEND_URI ??
  (SITE_NAME ? `https://${SITE_NAME}.azurewebsites.net` : "");

function log(msg: string): void {
  console.log(`[backend:postdeploy] ${msg}`);
}

/**
 * Re-apply the user-assigned identity used for ACR image pulls and restart the
 * site so the fresh container is pulled with valid credentials. No-op when the
 * required env values are missing.
 */
function repinAcrPullIdentity(): void {
  if (!SITE_NAME || !RESOURCE_GROUP || !SUBSCRIPTION_ID || !MI_CLIENT_ID) {
    log("Site/RG/subscription/identity not all set — skipping ACR pull-identity re-pin.");
    return;
  }
  const configId =
    `/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}` +
    `/providers/Microsoft.Web/sites/${SITE_NAME}/config/web`;
  log(`Re-pinning ACR pull identity (acrUserManagedIdentityID=${MI_CLIENT_ID})...`);
  execSync(
    `az resource update --ids "${configId}" ` +
      `--set properties.acrUseManagedIdentityCreds=true ` +
      `properties.acrUserManagedIdentityID=${MI_CLIENT_ID} -o none`,
    { stdio: "inherit" }
  );
  log("Restarting site to pull the image with the pinned identity...");
  execSync(
    `az webapp restart --name "${SITE_NAME}" --resource-group "${RESOURCE_GROUP}" -o none`,
    { stdio: "inherit" }
  );
  log("  ✓ identity re-pinned and site restarted");
}

/**
 * The backend crashes on startup unless its Key Vault is reachable by the
 * managed identity. Two things must hold, both of which are ensured here so a
 * standalone `azd deploy backend` needs no manual setup:
 *
 *   1. The vault's tenantId matches the subscription tenant. A mismatch makes
 *      every data-plane token fail (AggregatedAuthenticationFailure / S2S17001)
 *      — the identities live in the subscription tenant. main.bicep now sets
 *      this correctly, but this repairs any already-drifted vault.
 *   2. The two secrets the Go backend reads at startup exist
 *      (AI-SEARCH-APIKEY, AOAI-CHAT-COMPLETIONS-API-KEY). Normally seeded at
 *      provision time; re-seeded here so a backend-only deploy is self-healing.
 */
function ensureKeyVaultReady(): void {
  if (!KEY_VAULT_NAME || !SUBSCRIPTION_ID) {
    log("KEY_VAULT_NAME / subscription not set — skipping Key Vault readiness check.");
    return;
  }

  // 1. Correct the vault tenant if it drifted from the subscription tenant.
  try {
    const subTenant =
      process.env.AZURE_TENANT_ID?.trim() ||
      execSync(`az account show --query tenantId -o tsv`, {
        encoding: "utf8",
        stdio: ["ignore", "pipe", "pipe"],
      }).trim();
    const vaultTenant = execSync(
      `az keyvault show --name "${KEY_VAULT_NAME}" --query properties.tenantId -o tsv`,
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
    ).trim();
    if (subTenant && vaultTenant && subTenant !== vaultTenant) {
      log(`Correcting Key Vault tenant ${vaultTenant} → ${subTenant}...`);
      execSync(
        `az keyvault update --name "${KEY_VAULT_NAME}" --set properties.tenantId="${subTenant}" -o none`,
        { stdio: "inherit" },
      );
      log("  ✓ vault tenant corrected");
    } else {
      log(`  ✓ Key Vault tenant already correct (${vaultTenant})`);
    }
  } catch (err) {
    log(`  ⚠ could not verify/correct vault tenant: ${(err as Error).message.split("\n")[0]}`);
  }

  // 2. Ensure the backend's required secrets exist.
  seedKeyVaultSecrets({
    keyVaultName: KEY_VAULT_NAME,
    resourceGroup: RESOURCE_GROUP,
    searchServiceName: process.env.SEARCH_SERVICE_NAME ?? "",
    aiResourceName: process.env.AI_RESOURCE_NAME ?? "",
    env: process.env,
    log: (m) => log(m),
  });
}

async function healthCheck(): Promise<void> {
  if (!BACKEND_URL) {
    log("BACKEND_SITE_NAME / SERVICE_BACKEND_URI not set — skipping health check.");
    return;
  }
  const url = `${BACKEND_URL.replace(/\/$/, "")}/ping`;

  // The site is fronted by App Service Easy Auth, so an unauthenticated /ping
  // returns 401/403 even when the container is perfectly healthy. Acquire a
  // bearer token for the server audience so the probe reaches the container.
  let authHeader: Record<string, string> = {};
  const serverAudience = process.env.SERVER_AUDIENCE?.trim();
  if (serverAudience) {
    try {
      const token = execSync(
        `az account get-access-token --scope "${serverAudience}/.default" --query accessToken -o tsv`,
        { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
      ).trim();
      if (token) authHeader = { Authorization: `Bearer ${token}` };
    } catch {
      log("  could not acquire token for /ping; will treat 401/403 as 'site up'.");
    }
  }

  log(`Probing ${url}`);
  // Container cold-start (image pull + Go server boot) can take a few minutes,
  // so poll for up to ~5 minutes before giving up.
  for (let i = 0; i < 30; i++) {
    try {
      const res = await fetch(url, { headers: authHeader });
      if (res.ok) {
        log(`  ✓ healthy (HTTP ${res.status}) after ${i * 10}s`);
        return;
      }
      // 401/403 means Easy Auth rejected the request but the platform is
      // routing — i.e. the container is up. Only accept this once we could not
      // authenticate (otherwise a real auth misconfig would be masked).
      if ((res.status === 401 || res.status === 403) && !authHeader.Authorization) {
        log(`  ✓ site is up (HTTP ${res.status} from Easy Auth) after ${i * 10}s`);
        return;
      }
      log(`  attempt ${i + 1}: HTTP ${res.status}`);
    } catch (err) {
      log(`  attempt ${i + 1}: ${(err as Error).message}`);
    }
    await new Promise((r) => setTimeout(r, 10_000));
  }
  throw new Error("Backend health check did not succeed within 5 minutes.");
}

(async () => {
  log("Starting backend postdeploy");
  ensureKeyVaultReady();
  repinAcrPullIdentity();
  await healthCheck();
  log("Done.");
})().catch((err) => {
  console.error(`[backend:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
