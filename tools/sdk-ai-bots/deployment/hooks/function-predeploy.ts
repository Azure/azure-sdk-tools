/**
 * function-app — predeploy hook
 *
 * Calls scripts/setup-docker-image.ts in the function-app source folder when
 * AZD_SKIP_IMAGE_BUILD is unset (local dev). Pipelines build in CI.
 */

import { execSync } from "child_process";

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const ENV_NAME = process.env.AZURE_ENV_NAME ?? "dev";

function log(msg: string): void {
  console.log(`[function-app:predeploy] ${msg}`);
}

(async () => {
  if (process.env.AZD_SKIP_IMAGE_BUILD === "1") {
    log("AZD_SKIP_IMAGE_BUILD=1 — skipping image build/push.");
    return;
  }
  if (!REGISTRY_NAME) throw new Error("CONTAINER_REGISTRY_NAME not set.");
  log(`Building + pushing image to ${REGISTRY_NAME} with tag ${ENV_NAME}`);
  execSync(
    `npx tsx scripts/setup-docker-image.ts --tag "${ENV_NAME}" --acr-name "${REGISTRY_NAME}" --push`,
    { stdio: "inherit" }
  );
})().catch((err) => {
  console.error(`[function-app:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
