/**
 * agent — predeploy hook
 *
 * The hosted Foundry agent picks up its container from ACR; this hook ensures
 * the image tag exists before azd attempts to deploy it.
 */

import { execSync } from "child_process";

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const TAG = process.env.AGENT_IMAGE_TAG ?? process.env.AZURE_ENV_NAME ?? "dev";

function log(msg: string): void {
  console.log(`[agent:predeploy] ${msg}`);
}

(async () => {
  if (!REGISTRY_NAME) throw new Error("CONTAINER_REGISTRY_NAME not set.");
  log(`Validating image azure-sdk-qa-bot-agent-server:${TAG} in ACR ${REGISTRY_NAME}`);
  const tags = execSync(
    `az acr repository show-tags --name "${REGISTRY_NAME}" --repository "azure-sdk-qa-bot-agent-server" --output tsv`,
    { encoding: "utf8" }
  );
  if (!tags.split(/\r?\n/).map((s) => s.trim()).includes(TAG)) {
    throw new Error(
      `Image tag '${TAG}' not found in ACR. Build it in the agent CI pipeline first.`
    );
  }
  log("  ✓ tag found");
})().catch((err) => {
  console.error(`[agent:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
