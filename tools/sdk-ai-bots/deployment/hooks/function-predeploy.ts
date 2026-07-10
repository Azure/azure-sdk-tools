/**
 * function-app — predeploy hook
 *
 * Builds the function container image in ACR via `az acr build` (cloud-side,
 * no local Docker required — the Dockerfile does the full npm install + build
 * inside the container). Skipped in CI pipelines, which build the image in a
 * dedicated container-build stage and set AZD_SKIP_IMAGE_BUILD=1.
 */

import { execSync } from "child_process";
import * as path from "path";

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const ENV_NAME = process.env.AZURE_ENV_NAME ?? "dev";
const TAG = process.env.FUNCTION_IMAGE_TAG ?? ENV_NAME;
const IMAGE_NAME = "azure-sdk-qa-bot-function";

function log(msg: string): void {
  console.log(`[function-app:predeploy] ${msg}`);
}

(async () => {
  if (process.env.AZD_SKIP_IMAGE_BUILD === "1") {
    log("AZD_SKIP_IMAGE_BUILD=1 — skipping image build/push.");
    return;
  }
  if (!REGISTRY_NAME) throw new Error("CONTAINER_REGISTRY_NAME not set.");

  // The function project root (with the Dockerfile) is two levels up from
  // deployment/hooks/: ../../azure-sdk-qa-bot-function. process.cwd() is the
  // deployment/ folder when azd runs the hook.
  const functionRoot = path.resolve(process.cwd(), "../azure-sdk-qa-bot-function");
  log(`Building ${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${TAG} via az acr build (no local Docker)...`);
  log(`  context: ${functionRoot}`);
  execSync(
    `az acr build --registry "${REGISTRY_NAME}" --image "${IMAGE_NAME}:${TAG}" "${functionRoot}"`,
    { stdio: "inherit" }
  );
  log("  ✓ image built and pushed to ACR");
})().catch((err) => {
  console.error(`[function-app:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
