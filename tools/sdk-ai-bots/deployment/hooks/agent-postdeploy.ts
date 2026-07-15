/**
 * agent — postdeploy hook
 *
 * 1. Ensure the server app registration (SERVER_AUDIENCE) is fully authorized
 *    so callers can obtain tokens EasyAuth accepts (service principal,
 *    identifier URI, delegated scope, application app-role, pre-authorized
 *    clients, and the managed-identity app-role assignment).
 * 2. Sanity-check the hosted agent by pinging its /ping endpoint.
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

(async () => {
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
