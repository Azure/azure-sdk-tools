/**
 * postdeploy hook (global) — runs after any `azd deploy`.
 *
 * azd fires this once, AFTER every service's per-service postdeploy hook in
 * a given deploy invocation. That ordering lets us update the Logic App
 * workflow definition here regardless of which service was targeted — the
 * update is safe once the Function App container image is live, because
 * patchWorkflow gates on /admin/host/status = 200 before issuing the PUT.
 *
 * It also reconciles the hosted agent's RBAC + Entra authorization here. The
 * `agent` service cannot use a per-service postdeploy hook: azd's
 * `azure.ai.agent` host rewrites the agent service block on deploy and strips
 * the `hooks` field (see https://github.com/Azure/azure-dev/issues/9152). The
 * agent-postdeploy step is idempotent and self-skips when the agent isn't
 * deployed, so we run it on every deploy — this makes `azd deploy agent` grant
 * the agent identity its roles without any special command flag, and lets other
 * deploys self-heal the grants.
 *
 * To skip the Logic App update (e.g. iterating on frontend only):
 *   POSTDEPLOY_SKIP_LOGIC_APP=1 azd deploy frontend
 */

import { execSync } from "child_process";
import * as path from "path";
import { patchWorkflow } from "./lib/patch-workflow.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";
const SKIP_LOGIC_APP = process.env.POSTDEPLOY_SKIP_LOGIC_APP === "1";

function log(msg: string): void {
  console.log(`[postdeploy] ${msg}`);
}

function recordLastKnownGood(): void {
  log("Recording LastKnownGoodTag in App Configuration (TODO)...");
  // TODO: when AZD_DEPLOY_SUCCESS=1, write
  //   Deployment:<service>:LastKnownGoodTag = <image tag>
  // to the env's App Configuration store. CD pipelines call this hook with
  // the service+tag in AZD_DEPLOY_SERVICE / AZD_DEPLOY_TAG env vars.
}

/**
 * Reconcile the hosted agent's identity RBAC + server Entra authorization by
 * delegating to the existing agent-postdeploy.ts (run from the deployment/ dir,
 * matching the cwd it expects). Idempotent and self-skipping — no-op when the
 * agent hasn't been deployed in this environment yet.
 */
function runAgentPostdeploy(): void {
  log("Reconciling hosted agent RBAC + Entra authorization...");
  execSync("npx tsx hooks/agent-postdeploy.ts", {
    stdio: "inherit",
    cwd: path.join(process.cwd(), "deployment"),
  });
}

async function updateLogicAppWorkflow(): Promise<void> {
  if (SKIP_LOGIC_APP) {
    log("POSTDEPLOY_SKIP_LOGIC_APP=1 — skipping Logic App workflow update.");
    return;
  }
  await patchWorkflow({ logPrefix: "[postdeploy]" });
}

(async () => {
  log(`Starting global postdeploy for environment '${ENV_NAME}'`);
  recordLastKnownGood();
  runAgentPostdeploy();
  await updateLogicAppWorkflow();
  log("Postdeploy complete.");
})().catch((err) => {
  console.error(`[postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
