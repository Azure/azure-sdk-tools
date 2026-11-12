/**
 * predown hook — runs before `azd down`.
 *
 * `azd down` is allowed only for dev. Anything else fails fast.
 */

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";

function log(msg: string): void {
  console.log(`[predown] ${msg}`);
}

(async () => {
  log(`Starting predown for environment '${ENV_NAME}'`);
  if (ENV_NAME !== "dev") {
    throw new Error(
      `Refusing to run 'azd down' for environment '${ENV_NAME}'. ` +
        "Only 'dev' may be torn down via azd. To delete preview/prod, ask the DRI."
    );
  }
  log("Predown checks passed.");
})().catch((err) => {
  console.error(`[predown] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
