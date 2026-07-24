/**
 * function-app — postdeploy hook
 *
 * Runs after `azd deploy function-app`. Confirms the Function App resource
 * is Running.
 *
 * The Logic App workflow update was previously triggered from here, but is
 * now a separate concern owned by `azd deploy logic-app` (see azure.yaml
 * `logic-app` service and hooks/logic-app-deploy.ts).
 */

import { execSync } from "child_process";

const FUNCTION_APP_NAME = process.env.SERVICE_FUNCTION_APP_NAME ?? process.env.FUNCTION_APP_NAME ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";

function log(msg: string): void {
  console.log(`[function-app:postdeploy] ${msg}`);
}

function run(cmd: string): string {
  return execSync(cmd, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

(async () => {
  if (!FUNCTION_APP_NAME || !RESOURCE_GROUP) {
    log("Function app name or RG not set — skipping state check.");
    return;
  }

  const state = run(
    `az functionapp show --name "${FUNCTION_APP_NAME}" --resource-group "${RESOURCE_GROUP}" --query state -o tsv`,
  );
  log(`Function app state: ${state}`);
  if (state !== "Running") {
    throw new Error(`Expected Running, got '${state}'.`);
  }
})().catch((err) => {
  console.error(`[function-app:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});
