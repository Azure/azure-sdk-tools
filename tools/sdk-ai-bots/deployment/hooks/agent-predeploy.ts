/**
 * agent — predeploy hook
 *
 * Ensures the agent container image is in ACR and registers its full reference
 * as SERVICE_AGENT_IMAGE_NAME in the azd environment so azd uses the
 * pre-built image instead of attempting a local Docker build.
 *
 * Two modes:
 *   - Pipeline (AZD_SKIP_IMAGE_BUILD=1): image was already built by CI;
 *     validates the tag exists in ACR.
 *   - Local dev: builds the image cloud-side via `az acr build` — no local
 *     Docker installation required.
 *
 * In both modes SERVICE_AGENT_IMAGE_NAME is set so azd skips its own
 * container-build step.
 */

import { execSync } from "child_process";
import * as fs from "fs";
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
// When AGENT_IMAGE_TAG is set it is used verbatim. Otherwise the tag is
// resolved dynamically: locally we build/tag with ENV_NAME, and in CI we look
// up the newest tag in ACR that starts with the env prefix (see below).
const EXPLICIT_TAG = process.env.AGENT_IMAGE_TAG;
const TAG = EXPLICIT_TAG ?? ENV_NAME;
const IMAGE_NAME = "azure-sdk-qa-bot-agent-server";
// Only skip the build inside a real CI pipeline (TF_BUILD = Azure DevOps,
// GITHUB_ACTIONS = GitHub Actions). AZD_SKIP_IMAGE_BUILD is intentionally
// ignored here so that local `azd deploy agent` always rebuilds the image.
const RUNNING_IN_PIPELINE = !!process.env.TF_BUILD || !!process.env.GITHUB_ACTIONS;

function log(msg: string): void {
  console.log(`[agent:predeploy] ${msg}`);
}

function run(cmd: string, opts?: { cwd?: string }): void {
  execSync(cmd, { stdio: "inherit", ...opts });
}

(async () => {
  if (!REGISTRY_NAME) throw new Error("CONTAINER_REGISTRY_NAME not set.");

  let resolvedTag = TAG;

  if (RUNNING_IN_PIPELINE) {
    // CI already built and pushed the image. When a specific tag was pinned via
    // AGENT_IMAGE_TAG, validate that exact tag exists. Otherwise resolve the
    // newest tag in ACR that starts with the env prefix (e.g. 'dev-<build>').
    if (EXPLICIT_TAG) {
      log(`CI pipeline detected — validating pinned image tag '${TAG}'`);
      const latest = getLatestTagWithPrefix(REGISTRY_NAME, IMAGE_NAME, TAG);
      if (latest !== TAG) {
        throw new Error(
          `Image tag '${TAG}' not found in ACR '${REGISTRY_NAME}'. ` +
            `Ensure the agent CI pipeline ran successfully for this tag.`
        );
      }
    } else {
      log(`CI pipeline detected — resolving latest tag with env prefix '${ENV_NAME}'`);
      const latest = getLatestTagWithPrefix(REGISTRY_NAME, IMAGE_NAME, ENV_NAME);
      if (!latest) {
        throw new Error(
          `No image tag matching env prefix '${ENV_NAME}' found in ACR '${REGISTRY_NAME}'. ` +
            `Ensure the agent CI pipeline ran successfully.`
        );
      }
      resolvedTag = latest;
    }
    log(`  ✓ using tag '${resolvedTag}'`);
  } else {
    // Local dev (or any non-CI context): build image in ACR cloud-side.
    // az acr build sends the context to ACR and builds there — no local
    // Docker installation required.
    // The Dockerfile lives at agents/chat_agent/Dockerfile but the build
    // context must be the azure-sdk-qa-bot-agent/ parent (requirements.txt,
    // config/, models/, prompts/, skills/, tools/, utils/ are all there).
    // process.cwd() is deployment/ because the hook runs: cd ../../../deployment && npx tsx ...

    // Resolve the image tag. When AGENT_IMAGE_TAG is pinned it is used verbatim;
    // otherwise auto-increment the major version against ACR so a fresh build
    // gets 'dev-1.0.0' and subsequent builds get 'dev-2.0.0', 'dev-3.0.0', ...
    if (!EXPLICIT_TAG) {
      resolvedTag = getNextVersionTag(REGISTRY_NAME, IMAGE_NAME, ENV_NAME);
      log(`Resolved next version tag '${resolvedTag}'`);
    }
    const agentRoot = path.resolve(process.cwd(), "../azure-sdk-qa-bot-agent");
    const dockerfile = path.join(agentRoot, "agents/chat_agent/Dockerfile");
    log(`Building image via az acr build → ${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${resolvedTag}`);
    log(`  context:    ${agentRoot}`);
    log(`  dockerfile: ${dockerfile}`);
    run(
      `az acr build --registry "${REGISTRY_NAME}" --image "${IMAGE_NAME}:${resolvedTag}" --file "${dockerfile}" "${agentRoot}"`,
    );
    log("  ✓ build complete");
  }

  // Register the pre-built image with azd via two env vars:
  //   AGENT_DEPLOYED_IMAGE — read by azure.yaml container.image (${AGENT_DEPLOYED_IMAGE})
  //   SERVICE_AGENT_IMAGE_NAME — azd standard override for container image
  const fullImage = `${REGISTRY_NAME}.azurecr.io/${IMAGE_NAME}:${resolvedTag}`;
  log(`Setting AGENT_DEPLOYED_IMAGE=${fullImage}`);
  run(`azd env set AGENT_DEPLOYED_IMAGE "${fullImage}"`);
  log(`Setting SERVICE_AGENT_IMAGE_NAME=${fullImage}`);
  run(`azd env set SERVICE_AGENT_IMAGE_NAME "${fullImage}"`);

  // NOTE: the App Service 'agent' slot (the /agent/chat server) is deployed
  // separately by the `agent-server` service's predeploy hook
  // (hooks/agent-server-predeploy.ts), which builds the server image from the
  // top-level Dockerfile and repoints the slot. This hook only deploys the
  // hosted Foundry agent (agents/chat_agent).

  // Install a docker shim so azd's own package step (which runs after this
  // prepackage hook) finds "docker" and succeeds without a real Docker daemon.
  // The shim intercepts build/push/tag and returns success since the real image
  // was already pushed to ACR by az acr build above.
  //
  // We must install into a STABLE user directory on PATH (~/.local/bin), NOT
  // the first writable dir on PATH — azd prepends its own ephemeral hook temp
  // directory to PATH while a hook runs, and that dir is deleted before the
  // package step, so a shim written there would vanish.
  if (!RUNNING_IN_PIPELINE) {
    const shimScript = `#!/bin/sh
# Docker shim installed by azd deploy agent prepackage hook.
# The real image is already in ACR via az acr build.
CMD="$1"
case "$CMD" in
  build)
    IIDFILE=""
    shift
    while [ "$#" -gt 0 ]; do
      case "$1" in --iidfile) shift; IIDFILE="$1" ;; esac
      shift
    done
    FAKE_ID="sha256:aabbccddeeff001122334455667788990011223344556677889900aabbccddeeff"
    [ -n "$IIDFILE" ] && echo "$FAKE_ID" > "$IIDFILE"
    echo "Successfully built aabbccddeeff0011"
    exit 0
    ;;
  push|tag|login|logout|inspect|image)
    exit 0 ;;
  *)
    exit 0 ;;
esac
`;
    const home = process.env.HOME ?? process.env.USERPROFILE ?? "";
    if (!home) throw new Error("HOME not set; cannot install Docker shim.");
    const shimDir = path.join(home, ".local", "bin");
    fs.mkdirSync(shimDir, { recursive: true });
    const shimPath = path.join(shimDir, "docker");
    fs.writeFileSync(shimPath, shimScript, { mode: 0o755 });
    log(`Installed Docker shim at ${shimPath}`);

    // Warn if ~/.local/bin isn't on PATH — azd's package step won't find the shim.
    const onPath = (process.env.PATH ?? "")
      .split(path.delimiter)
      .some((d) => path.resolve(d) === path.resolve(shimDir));
    if (!onPath) {
      log(`WARNING: ${shimDir} is not on PATH. Add it so azd can find the shim:`);
      log(`  export PATH="${shimDir}:$PATH"`);
    }
  }

  log("Agent predeploy complete.");
})().catch((err) => {
  console.error(`[agent:predeploy] FAILED: ${err.message}`);
  process.exit(1);
});
