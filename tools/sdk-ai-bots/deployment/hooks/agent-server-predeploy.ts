/**
 * agent-server — predeploy hook
 *
 * Builds the agent SERVER image (FastAPI `server.py`, the container that serves
 * /agent/chat) from the top-level Dockerfile and repoints the App Service
 * `agent` deployment slot at it.
 *
 * Why a hook (and not azd's native App Service deploy): the agent server runs
 * in the `agent` deployment SLOT of the backend web app, and azd does not model
 * named deployment slots — its App Service target only deploys a site's
 * production slot. So the slot image is set here with
 * `az webapp config container set --slot agent`, and SERVICE_AGENT_SERVER_IMAGE_NAME
 * is exported so azd reuses this pre-built image instead of building its own.
 *
 * Two modes:
 *   - Pipeline (TF_BUILD / GITHUB_ACTIONS): CI already built and pushed the
 *     image; this validates the tag exists in ACR.
 *   - Local dev: builds the image cloud-side via `az acr build` — no local
 *     Docker installation required.
 *
 * NOTE: the server Dockerfile COPYs paths like
 * `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/...`, so its build context must be
 * the repository root (not the project directory).
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
// The server runs in the App Service `agent` slot of the backend web app.
const BACKEND_SITE_NAME = process.env.BACKEND_SITE_NAME ?? "";
const AGENT_SLOT_NAME = process.env.AGENT_SLOT_NAME ?? "agent";
const RESOURCE_GROUP = process.env.AZURE_RESOURCE_GROUP ?? "";
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
    // The Dockerfile lives at azure-sdk-qa-bot-agent/Dockerfile but its build
    // context must be the repository ROOT because it COPYs paths like
    // tools/sdk-ai-bots/azure-sdk-qa-bot-agent/... process.cwd() is deployment/
    // because the hook runs: cd ../deployment && npx tsx hooks/agent-server-predeploy.ts
    if (!EXPLICIT_TAG) {
      resolvedTag = getNextVersionTag(REGISTRY_NAME, IMAGE_NAME, ENV_NAME);
      log(`Resolved next version tag '${resolvedTag}'`);
    }
    const repoRoot = path.resolve(process.cwd(), "../../../");
    const dockerfile = path.resolve(process.cwd(), "../azure-sdk-qa-bot-agent/Dockerfile");
    log(`Building image via az acr build → ${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${resolvedTag}`);
    log(`  context:    ${repoRoot}`);
    log(`  dockerfile: ${dockerfile}`);
    run(
      `az acr build --registry "${REGISTRY_NAME}" --image "${IMAGE_NAME}:${resolvedTag}" --file "${dockerfile}" "${repoRoot}"`,
    );
    log("  ✓ build complete");
  }

  // Register the pre-built image with azd so it reuses it instead of building
  // its own container: SERVICE_AGENT_SERVER_IMAGE_NAME is azd's standard
  // per-service image override.
  const fullImage = `${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${resolvedTag}`;
  log(`Setting AGENT_SERVER_DEPLOYED_IMAGE=${fullImage}`);
  run(`azd env set AGENT_SERVER_DEPLOYED_IMAGE "${fullImage}"`);
  log(`Setting SERVICE_AGENT_SERVER_IMAGE_NAME=${fullImage}`);
  run(`azd env set SERVICE_AGENT_SERVER_IMAGE_NAME "${fullImage}"`);

  // Repoint the App Service `agent` slot at the freshly built immutable tag.
  // azd cannot deploy to a named slot, so the container image is set here.
  // Skipped in CI, where provisioning sets the slot image from the
  // AGENT_BASED_IMAGE_REPOSITORY parameter.
  if (!RUNNING_IN_PIPELINE) {
    if (BACKEND_SITE_NAME && RESOURCE_GROUP) {
      log(`Repointing App Service slot '${BACKEND_SITE_NAME}/${AGENT_SLOT_NAME}' → ${fullImage}`);
      run(
        `az webapp config container set --name "${BACKEND_SITE_NAME}" --slot "${AGENT_SLOT_NAME}" ` +
          `--resource-group "${RESOURCE_GROUP}" --container-image-name "${fullImage}"`
      );
      log("  ✓ agent slot image updated");
    } else {
      log(
        "WARNING: BACKEND_SITE_NAME / AZURE_RESOURCE_GROUP not set — cannot repoint " +
          "the agent slot. It will keep serving its previously configured image."
      );
    }
  }

  log("agent-server predeploy complete.");
})().catch((err) => {
  console.error(`[agent-server:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
