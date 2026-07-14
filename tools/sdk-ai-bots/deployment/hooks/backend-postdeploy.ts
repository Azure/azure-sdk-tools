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

const SITE_NAME = process.env.BACKEND_SITE_NAME ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
const SUBSCRIPTION_ID = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const MI_CLIENT_ID = process.env.MANAGED_IDENTITY_CLIENT_ID ?? "";
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

async function healthCheck(): Promise<void> {
  if (!BACKEND_URL) {
    log("BACKEND_SITE_NAME / SERVICE_BACKEND_URI not set — skipping health check.");
    return;
  }
  const url = `${BACKEND_URL.replace(/\/$/, "")}/ping`;
  log(`Probing ${url}`);
  // Container cold-start (image pull + Go server boot) can take a few minutes,
  // so poll for up to ~5 minutes before giving up.
  for (let i = 0; i < 30; i++) {
    try {
      const res = await fetch(url);
      if (res.ok) {
        log(`  ✓ healthy (HTTP ${res.status}) after ${i * 10}s`);
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
  repinAcrPullIdentity();
  await healthCheck();
  log("Done.");
})().catch((err) => {
  console.error(`[backend:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
