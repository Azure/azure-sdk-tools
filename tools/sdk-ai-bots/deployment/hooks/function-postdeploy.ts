/**
 * function-app — postdeploy hook
 *
 * Runs after `azd deploy function-app`. Confirms the Function App resource
 * is Running, then applies the complete Logic App workflow definition.
 */

import { execSync } from "child_process";
import { patchWorkflow } from "./lib/patch-workflow.js";

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

  await patchWorkflow({ logPrefix: "[function-app:postdeploy]" });
})().catch((err) => {
  console.error(`[function-app:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});
