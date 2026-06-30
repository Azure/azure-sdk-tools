/**
 * predeploy hook (global) — runs before any `azd deploy`.
 *
 * Per-service predeploy hooks are still responsible for building/pushing
 * their own images. This global hook enforces the build-once contract:
 *
 *   - If AZD_SKIP_IMAGE_BUILD=1 (set by CD pipelines), the per-service
 *     predeploy hooks skip docker build.
 *   - If running outside a pipeline and ENV is prod, fail fast.
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
  if (process.env.AZD_SKIP_IMAGE_BUILD === "1") {
    log("AZD_SKIP_IMAGE_BUILD=1 — per-service hooks will skip docker build.");
  }
  log("Predeploy checks passed.");
})().catch((err) => {
  console.error(`[predeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
