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
import { getNextVersionTag } from "./lib/acr-tags";

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const ENV_NAME = process.env.AZURE_ENV_NAME ?? "dev";
// When FUNCTION_IMAGE_TAG is pinned it is used verbatim; otherwise the tag is
// resolved dynamically as the next auto-incrementing version (dev-1.0.0,
// dev-2.0.0, ...) against ACR (see getNextVersionTag).
const EXPLICIT_TAG = process.env.FUNCTION_IMAGE_TAG;
const IMAGE_NAME = "azure-sdk-qa-bot-function";
// Function App that runs the container; needed to repoint it at the freshly
// built immutable tag (the app is provisioned pinned to ':dev').
const FUNCTION_APP_NAME =
  process.env.SERVICE_FUNCTION_APP_NAME ?? process.env.FUNCTION_APP_NAME ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";

function log(msg: string): void {
  console.log(`[function-app:predeploy] ${msg}`);
}

(async () => {
  if (process.env.AZD_SKIP_IMAGE_BUILD === "1") {
    log("AZD_SKIP_IMAGE_BUILD=1 — skipping image build/push.");
    return;
  }
  if (!REGISTRY_NAME) throw new Error("CONTAINER_REGISTRY_NAME not set.");

  // Resolve the image tag. When FUNCTION_IMAGE_TAG is pinned it is used
  // verbatim; otherwise auto-increment the major version against ACR so a fresh
  // build gets 'dev-1.0.0' and subsequent builds get 'dev-2.0.0', ...
  const tag = EXPLICIT_TAG ?? getNextVersionTag(REGISTRY_NAME, IMAGE_NAME, ENV_NAME);
  if (!EXPLICIT_TAG) log(`Resolved next version tag '${tag}'`);

  // The function project root (with the Dockerfile) is two levels up from
  // deployment/hooks/: ../../azure-sdk-qa-bot-function. process.cwd() is the
  // deployment/ folder when azd runs the hook.
  const functionRoot = path.resolve(process.cwd(), "../azure-sdk-qa-bot-function");
  const fullImage = `${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${tag}`;
  log(`Building ${fullImage} via az acr build (no local Docker)...`);
  log(`  context: ${functionRoot}`);
  execSync(
    `az acr build --registry "${REGISTRY_NAME}" --image "${IMAGE_NAME}:${tag}" "${functionRoot}"`,
    { stdio: "inherit" }
  );
  log("  ✓ image built and pushed to ACR");

  // Register the pre-built image with azd so its App Service container deploy
  // (azure.yaml `function-app` host: appservice) reuses this exact immutable tag
  // instead of remote-building a fresh 'azd-deploy-<timestamp>' image. This is
  // azd's standard per-service image override.
  log(`Setting SERVICE_FUNCTION_APP_IMAGE_NAME=${fullImage}`);
  execSync(`azd env set SERVICE_FUNCTION_APP_IMAGE_NAME "${fullImage}"`, { stdio: "inherit" });

  // Repoint the Function App at the freshly built immutable tag. Provisioning
  // pins the app to ':dev', and `azd deploy` does not re-provision, so an
  // immutable version tag would otherwise never be served.
  if (FUNCTION_APP_NAME && RESOURCE_GROUP) {
    log(`Repointing Function App '${FUNCTION_APP_NAME}' → ${fullImage}`);
    execSync(
      `az functionapp config container set --name "${FUNCTION_APP_NAME}" --resource-group "${RESOURCE_GROUP}" --image "${fullImage}"`,
      { stdio: "inherit" }
    );
    log("  ✓ Function App image updated");
  } else {
    log(
      "WARNING: FUNCTION_APP_NAME / AZURE_RESOURCE_GROUP not set — cannot repoint " +
        "the Function App. It will keep serving its previously configured image."
    );
  }
})().catch((err) => {
  console.error(`[function-app:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
