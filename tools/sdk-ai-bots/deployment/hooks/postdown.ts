/**
 * postdown hook — runs after `azd down`.
 *
 * Cleanup stub — leave any soft-deleted Key Vaults purged so the next dev
 * provisioning does not collide.
 */

function log(msg: string): void {
  console.log(`[postdown] ${msg}`);
}

(async () => {
  log("Starting postdown");
  // TODO: az keyvault purge --name <env-vault> --location <loc>
  log("Postdown complete.");
})().catch((err) => {
  console.error(`[postdown] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
