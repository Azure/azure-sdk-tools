/**
 * frontend — predeploy hook
 *
 * The frontend image ships only the pre-compiled `lib/` output (its
 * .dockerignore excludes src/*.ts/tsconfig.json), so the TypeScript must be
 * built before the image is assembled. This hook:
 *   1. Runs `npm install` + `npm run build` locally (plain tsc — no Docker), and
 *   2. Builds/pushes the image via `az acr build` (cloud-side — no Docker),
 *      which ships the freshly-built `lib/`.
 * Skipped in CI pipelines, which build the image in a dedicated stage and set
 * AZD_SKIP_IMAGE_BUILD=1.
 */

import { execSync } from "child_process";
import * as path from "path";
import { getNextVersionTag } from "./lib/acr-tags";

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const ENV_NAME = process.env.AZURE_ENV_NAME ?? "dev";
// When FRONTEND_IMAGE_TAG is pinned it is used verbatim; otherwise the tag is
// resolved dynamically as the next auto-incrementing version (dev-1.0.0,
// dev-2.0.0, ...) against ACR (see getNextVersionTag).
const EXPLICIT_TAG = process.env.FRONTEND_IMAGE_TAG;
const IMAGE_NAME = "azure-sdk-qa-bot";
// App Service that runs the frontend container; needed to repoint the site at
// the freshly built immutable tag (the site is provisioned pinned to ':dev').
const SITE_NAME = process.env.FRONTEND_SITE_NAME ?? process.env.SERVICE_FRONTEND_NAME ?? "";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";

function log(msg: string): void {
  console.log(`[frontend:predeploy] ${msg}`);
}

(async () => {
  if (process.env.AZD_SKIP_IMAGE_BUILD === "1") {
    log("AZD_SKIP_IMAGE_BUILD=1 — skipping image build/push.");
    return;
  }
  if (!REGISTRY_NAME) {
    throw new Error("CONTAINER_REGISTRY_NAME not set (should come from `azd env` outputs).");
  }

  // Resolve the image tag. When FRONTEND_IMAGE_TAG is pinned it is used
  // verbatim; otherwise auto-increment the major version against ACR so a fresh
  // build gets 'dev-1.0.0' and subsequent builds get 'dev-2.0.0', ...
  const tag = EXPLICIT_TAG ?? getNextVersionTag(REGISTRY_NAME, IMAGE_NAME, ENV_NAME);
  if (!EXPLICIT_TAG) log(`Resolved next version tag '${tag}'`);

  // The frontend project root (with the Dockerfile) is two levels up from
  // deployment/hooks/: ../../azure-sdk-qa-bot. process.cwd() is the deployment/
  // folder when azd runs the hook.
  const frontendRoot = path.resolve(process.cwd(), "../azure-sdk-qa-bot");

  // 1. Compile lib/ locally (plain tsc — the image only ships compiled output).
  log("Compiling frontend (npm install + npm run build)...");
  execSync("npm install", { stdio: "inherit", cwd: frontendRoot });
  execSync("npm run build", { stdio: "inherit", cwd: frontendRoot });

  // 2. Build + push the image cloud-side (ships the freshly-built lib/).
  const fullImage = `${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${tag}`;
  log(`Building ${fullImage} via az acr build (no local Docker)...`);
  execSync(
    `az acr build --registry "${REGISTRY_NAME}" --image "${IMAGE_NAME}:${tag}" "${frontendRoot}"`,
    { stdio: "inherit" }
  );
  log("  ✓ image built and pushed to ACR");

  // 3. Repoint the App Service at the freshly built immutable tag. Provisioning
  //    pins the site to ':dev', and `azd deploy` does not re-provision, so an
  //    immutable version tag would otherwise never be served.
  if (SITE_NAME && RESOURCE_GROUP) {
    log(`Repointing App Service '${SITE_NAME}' → ${fullImage}`);
    execSync(
      `az webapp config container set --name "${SITE_NAME}" --resource-group "${RESOURCE_GROUP}" --container-image-name "${fullImage}"`,
      { stdio: "inherit" }
    );
    log("  ✓ App Service image updated");
  } else {
    log(
      "WARNING: FRONTEND_SITE_NAME / AZURE_RESOURCE_GROUP not set — cannot repoint " +
        "the App Service. It will keep serving its previously configured image."
    );
  }
})().catch((err) => {
  console.error(`[frontend:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
