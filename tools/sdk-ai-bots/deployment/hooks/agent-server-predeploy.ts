/**
 * agent-server — predeploy hook
 *
 * Builds the agent SERVER image (FastAPI `server.py`, the container that serves
 * /agent/chat) from the top-level Dockerfile and registers it with azd
 * (SERVICE_AGENT_SERVER_IMAGE_NAME) so azd can deploy it to the App Service
 * `agent` slot.
 *
 * Why a hook: this repo builds every service image cloud-side with `az acr build`
 * (immutable version tags, no local Docker) rather than azd's native remote
 * build, so this hook builds the agent SERVER image and exports
 * SERVICE_AGENT_SERVER_IMAGE_NAME so azd reuses it. azd then deploys that image
 * to the backend web app's `agent` deployment SLOT natively — the service is
 * resolved to the backend site via `resourceName: ${BACKEND_SITE_NAME}` in
 * azure.yaml and targeted at the slot via AZD_DEPLOY_AGENT_SERVER_SLOT_NAME=agent
 * (see https://github.com/Azure/azure-dev/issues/9246). The previous
 * `az webapp config container set --slot agent` workaround is no longer needed.
 *
 * Two modes:
 *   - Pipeline (TF_BUILD / GITHUB_ACTIONS): CI already built and pushed the
 *     image; this validates the tag exists in ACR.
 *   - Local dev: builds the image cloud-side via `az acr build` — no local
 *     Docker installation required.
 *
 * NOTE: the server Dockerfile COPYs project-relative paths (e.g. `requirements.txt`,
 * `config/`, `server.py`), so its build context is the project directory
 * (azure-sdk-qa-bot-agent) — not the repository root. Keeping the context scoped
 * to the project keeps the `az acr build` upload small and fast.
 */

import { execSync } from "child_process";
import * as path from "path";
import { getLatestTagWithPrefix, getNextVersionTag } from "./lib/acr-tags";

/** Load azd env values into process.env if not already set. */
function loadAzdEnv(): void {
  try {
    const raw = execSync("azd env get-values", { encoding: "utf8" });
    for (const line of raw.split(/\r?\n/)) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith("#")) continue;
      const eq = trimmed.indexOf("=");
      if (eq === -1) continue;
      const key = trimmed.slice(0, eq).trim();
      let val = trimmed.slice(eq + 1).trim();
      if (val.startsWith('"') && val.endsWith('"')) {
        val = val.slice(1, -1).replace(/\\"/g, '"').replace(/\\\\/g, "\\").replace(/\\n/g, "\n");
      }
      if (!process.env[key]) process.env[key] = val;
    }
  } catch {
    // azd env get-values may fail if no environment is selected; ignore.
  }
}

loadAzdEnv();

const REGISTRY_NAME = process.env.CONTAINER_REGISTRY_NAME ?? "";
const ENV_NAME = process.env.AZURE_ENV_NAME ?? "dev";
// When AGENT_SERVER_IMAGE_TAG is set it is used verbatim. Otherwise the tag is
// resolved dynamically: locally we auto-increment against ACR, and in CI we
// look up the newest tag that starts with the env prefix (see below).
const EXPLICIT_TAG = process.env.AGENT_SERVER_IMAGE_TAG;
const IMAGE_NAME = "azure-sdk-qa-bot-agent-server";
// Only skip the build inside a real CI pipeline (TF_BUILD = Azure DevOps,
// GITHUB_ACTIONS = GitHub Actions). Locally we always rebuild.
const RUNNING_IN_PIPELINE = !!process.env.TF_BUILD || !!process.env.GITHUB_ACTIONS;

function log(msg: string): void {
  console.log(`[agent-server:predeploy] ${msg}`);
}

function run(cmd: string, opts?: { cwd?: string }): void {
  execSync(cmd, { stdio: "inherit", ...opts });
}

(async () => {
  if (!REGISTRY_NAME) throw new Error("CONTAINER_REGISTRY_NAME not set.");

  let resolvedTag = EXPLICIT_TAG ?? ENV_NAME;

  if (RUNNING_IN_PIPELINE) {
    // CI already built and pushed the image. When a specific tag was pinned via
    // AGENT_SERVER_IMAGE_TAG, validate that exact tag exists. Otherwise resolve
    // the newest tag that starts with the env prefix (e.g. 'dev-<build>').
    if (EXPLICIT_TAG) {
      log(`CI pipeline detected — validating pinned image tag '${EXPLICIT_TAG}'`);
      const latest = getLatestTagWithPrefix(REGISTRY_NAME, IMAGE_NAME, EXPLICIT_TAG);
      if (latest !== EXPLICIT_TAG) {
        throw new Error(
          `Image tag '${EXPLICIT_TAG}' not found in ACR '${REGISTRY_NAME}'. ` +
            `Ensure the agent-server CI pipeline ran successfully for this tag.`
        );
      }
    } else {
      log(`CI pipeline detected — resolving latest tag with env prefix '${ENV_NAME}'`);
      const latest = getLatestTagWithPrefix(REGISTRY_NAME, IMAGE_NAME, ENV_NAME);
      if (!latest) {
        throw new Error(
          `No image tag matching env prefix '${ENV_NAME}' found in ACR '${REGISTRY_NAME}'. ` +
            `Ensure the agent-server CI pipeline ran successfully.`
        );
      }
      resolvedTag = latest;
    }
    log(`  ✓ using tag '${resolvedTag}'`);
  } else {
    // Local dev (or any non-CI context): build the image in ACR cloud-side.
    // az acr build sends the context to ACR and builds there — no local Docker
    // installation required.
    //
    // The Dockerfile lives at azure-sdk-qa-bot-agent/Dockerfile and COPYs
    // project-relative paths, so the build context is the project directory.
    // process.cwd() is deployment/ because the hook runs:
    //   cd ../deployment && npx tsx hooks/agent-server-predeploy.ts
    if (!EXPLICIT_TAG) {
      resolvedTag = getNextVersionTag(REGISTRY_NAME, IMAGE_NAME, ENV_NAME);
      log(`Resolved next version tag '${resolvedTag}'`);
    }
    const contextDir = path.resolve(process.cwd(), "../azure-sdk-qa-bot-agent");
    const dockerfile = path.resolve(contextDir, "Dockerfile");
    log(`Building image via az acr build → ${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${resolvedTag}`);
    log(`  context:    ${contextDir}`);
    log(`  dockerfile: ${dockerfile}`);
    run(
      `az acr build --registry "${REGISTRY_NAME}" --image "${IMAGE_NAME}:${resolvedTag}" --file "${dockerfile}" "${contextDir}"`,
    );
    log("  ✓ build complete");
  }

  // Register the pre-built image with azd so its App Service container deploy
  // reuses this exact immutable tag instead of remote-building its own. azd then
  // applies it to the backend site's `agent` slot (the service is resolved to
  // the backend site via `resourceName: ${BACKEND_SITE_NAME}` in azure.yaml and
  // targeted at the slot via AZD_DEPLOY_AGENT_SERVER_SLOT_NAME=agent).
  const fullImage = `${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${resolvedTag}`;
  log(`Setting SERVICE_AGENT_SERVER_IMAGE_NAME=${fullImage}`);
  run(`azd env set SERVICE_AGENT_SERVER_IMAGE_NAME "${fullImage}"`);

  log("agent-server predeploy complete.");
})().catch((err) => {
  console.error(`[agent-server:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
