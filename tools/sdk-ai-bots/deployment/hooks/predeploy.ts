/**
 * predeploy hook (global) — runs before any `azd deploy`.
 *
 * Per-service predeploy hooks are still responsible for building/pushing
 * their own images. This global hook enforces the build-once contract:
 *
 *   - If AZD_SKIP_IMAGE_BUILD=1 (set by CD pipelines), the per-service
 *     predeploy hooks skip docker build.
 *   - If running outside a pipeline and ENV is prod, fail fast.
 *
 * The `agent` service cannot use a per-service predeploy hook: azd's
 * `azure.ai.agent` host rewrites the agent service block on deploy and strips
 * the `hooks` field (see https://github.com/Azure/azure-dev/issues/9152). So
 * the agent image build lives here instead, gated on the deploy targeting the
 * agent (AZD_DEPLOY_SERVICE=agent — set by CD pipelines, or by the developer
 * for a local agent deploy).
 */

import { execSync } from "child_process";
import * as path from "path";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const RUNNING_IN_PIPELINE = !!process.env.TF_BUILD || !!process.env.GITHUB_ACTIONS;

function log(msg: string): void {
  console.log(`[predeploy] ${msg}`);
}

function enforceProdGuardrail(): void {
  if (ENV_NAME === "prod" && !RUNNING_IN_PIPELINE) {
    throw new Error(
      "Refusing to deploy to prod from a non-pipeline context. " +
        "Use the prod CD pipeline."
    );
  }
}

/**
 * Build the hosted agent image when this deploy targets the agent. Delegates to
 * the existing agent-predeploy.ts (run from the deployment/ dir, matching the
 * cwd it expects). Skipped for non-agent deploys so we don't trigger a ~3-min
 * `az acr build` on every frontend/backend/function deploy.
 */
function maybeBuildAgentImage(): void {
  if (process.env.AZD_DEPLOY_SERVICE !== "agent") {
    log("Deploy target is not 'agent' (AZD_DEPLOY_SERVICE) — skipping agent image build.");
    return;
  }
  log("Agent deploy detected — building agent image via agent-predeploy.ts");
  execSync("npx tsx hooks/agent-predeploy.ts", {
    stdio: "inherit",
    cwd: path.join(process.cwd(), "deployment"),
  });
}

(async () => {
  log(`Starting global predeploy for environment '${ENV_NAME}'`);
  enforceProdGuardrail();
  if (process.env.AZD_SKIP_IMAGE_BUILD === "1") {
    log("AZD_SKIP_IMAGE_BUILD=1 — per-service hooks will skip docker build.");
  }
  maybeBuildAgentImage();
  log("Predeploy checks passed.");
})().catch((err) => {
  console.error(`[predeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
