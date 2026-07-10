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

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const ENV_NAME = process.env.AZURE_ENV_NAME ?? "dev";
const TAG = process.env.FRONTEND_IMAGE_TAG ?? ENV_NAME;
const IMAGE_NAME = "azure-sdk-qa-bot";

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

  // The frontend project root (with the Dockerfile) is two levels up from
  // deployment/hooks/: ../../azure-sdk-qa-bot. process.cwd() is the deployment/
  // folder when azd runs the hook.
  const frontendRoot = path.resolve(process.cwd(), "../azure-sdk-qa-bot");

  // 1. Compile lib/ locally (plain tsc — the image only ships compiled output).
  log("Compiling frontend (npm install + npm run build)...");
  execSync("npm install", { stdio: "inherit", cwd: frontendRoot });
  execSync("npm run build", { stdio: "inherit", cwd: frontendRoot });

  // 2. Build + push the image cloud-side (ships the freshly-built lib/).
  log(`Building ${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${TAG} via az acr build (no local Docker)...`);
  execSync(
    `az acr build --registry "${REGISTRY_NAME}" --image "${IMAGE_NAME}:${TAG}" "${frontendRoot}"`,
    { stdio: "inherit" }
  );
  log("  ✓ image built and pushed to ACR");
})().catch((err) => {
  console.error(`[frontend:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
