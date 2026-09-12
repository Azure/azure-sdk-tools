/**
 * predeploy hook (global) — runs before any `azd deploy`.
 *
 * All services use their azd remote-build lifecycle. Local deployment is
 * allowed only when the selected environment contract permits it.
 *
 */

import { enforceLocalOperationAllowed } from "./lib/provision-guard.js";

const ENV_NAME = process.env.AZURE_ENV_NAME ?? "";

function log(msg: string): void {
  console.log(`[predeploy] ${msg}`);
}

(async () => {
  log(`Starting global predeploy for environment '${ENV_NAME}'`);
  enforceLocalOperationAllowed("deploy");
  log("Predeploy checks passed.");
})().catch((err) => {
  console.error(`[predeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
