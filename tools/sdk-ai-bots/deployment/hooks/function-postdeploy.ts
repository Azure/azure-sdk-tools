/**
 * function-app — postdeploy hook
 *
 * Lightweight health check; the function app does not expose a standard
 * /health route, so we just confirm the Function App resource is in a
 * Running state.
 */

import { execSync } from "child_process";

const FUNCTION_APP_NAME = process.env.SERVICE_FUNCTION_APP_NAME ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";

function log(msg: string): void {
  console.log(`[function-app:postdeploy] ${msg}`);
}

(async () => {
  if (!FUNCTION_APP_NAME || !RESOURCE_GROUP) {
    log("Function app name or RG not set — skipping state check.");
    return;
  }
  const state = execSync(
    `az functionapp show --name "${FUNCTION_APP_NAME}" --resource-group "${RESOURCE_GROUP}" --query state -o tsv`,
    { encoding: "utf8" }
  ).trim();
  log(`Function app state: ${state}`);
  if (state !== "Running") {
    throw new Error(`Expected Running, got '${state}'.`);
  }
})().catch((err) => {
  console.error(`[function-app:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});
