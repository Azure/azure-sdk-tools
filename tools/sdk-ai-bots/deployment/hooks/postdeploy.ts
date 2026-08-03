/**
 * postdeploy hook (global) — runs after any `azd deploy`.
 *
 * azd fires this once, AFTER every service's per-service postdeploy hook in
 * a given deploy invocation. That ordering lets us update the Logic App
 * workflow definition here regardless of which service was targeted — the
 * update is safe once the Function App container image is live, because
 * patchWorkflow gates on /admin/host/status = 200 before issuing the PUT.
 *
 * To skip the Logic App update (e.g. iterating on frontend only):
 *   POSTDEPLOY_SKIP_LOGIC_APP=1 azd deploy frontend
 */

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
  await updateLogicAppWorkflow();
  log("Postdeploy complete.");
})().catch((err) => {
  console.error(`[postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
