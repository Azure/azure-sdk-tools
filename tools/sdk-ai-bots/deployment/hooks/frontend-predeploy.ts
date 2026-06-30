/**
 * frontend — predeploy hook
 *
 * Skipped in pipelines (which set AZD_SKIP_IMAGE_BUILD=1 because CI built the
 * image already). For developer-local `azd deploy frontend`, runs the
 * existing `scripts/setup-docker-image.ps1` against the dev ACR.
 */

import { execSync } from "child_process";

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const ENV_NAME = process.env.AZURE_ENV_NAME ?? "dev";

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
  log(`Building + pushing image to ${REGISTRY_NAME} with tag ${ENV_NAME}`);
  execSync(
    `pwsh -c "./scripts/setup-docker-image.ps1 -Tag ${ENV_NAME} -AcrName ${REGISTRY_NAME} -Push"`,
    { stdio: "inherit" }
  );
})().catch((err) => {
  console.error(`[frontend:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
