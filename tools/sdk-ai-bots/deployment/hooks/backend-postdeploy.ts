/**
 * backend — postdeploy hook
 *
 * Health-check the backend main site after `azd deploy backend`. The main site
 * is the production App Service (BACKEND_SITE_NAME) — note SERVER_BASE_URL points
 * at the `agent` slot (where the agent-server runs), which is a different app, so
 * we build the URL from BACKEND_SITE_NAME here. azd may also pass
 * SERVICE_BACKEND_URI for the deployed target.
 */

const SITE_NAME = process.env.BACKEND_SITE_NAME ?? "";
const BACKEND_URL =
  process.env.SERVICE_BACKEND_URI ??
  (SITE_NAME ? `https://${SITE_NAME}.azurewebsites.net` : "");

function log(msg: string): void {
  console.log(`[backend:postdeploy] ${msg}`);
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
  await healthCheck();
  log("Done.");
})().catch((err) => {
  console.error(`[backend:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
