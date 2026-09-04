/**
 * predeploy hook (global) — runs before any `azd deploy`.
 *
 * Per-service predeploy hooks are responsible for image preparation. All
 * services use their azd remote-build lifecycle. If running outside a pipeline
 * and ENV is prod, this hook fails fast.
 *
 */

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

(async () => {
  log(`Starting global predeploy for environment '${ENV_NAME}'`);
  enforceProdGuardrail();
  log("Predeploy checks passed.");
})().catch((err) => {
  console.error(`[predeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
