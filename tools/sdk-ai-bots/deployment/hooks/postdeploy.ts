/**
 * postdeploy hook (global) — runs after any `azd deploy`.
 *
 * Ported from ../../azd-experiments/hooks/postdeploy.ts. Stub form for now;
 * per-service postdeploy hooks own their own health checks. This global hook
 * exists for cross-service summary + LastKnownGoodTag bookkeeping.
 */

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";

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

(async () => {
  log(`Starting global postdeploy for environment '${ENV_NAME}'`);
  recordLastKnownGood();
  log("Postdeploy complete.");
})().catch((err) => {
  console.error(`[postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
