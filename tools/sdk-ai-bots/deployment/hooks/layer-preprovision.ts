import { enforceProvisionGuard } from "./lib/provision-guard.js";

try {
  enforceProvisionGuard();
} catch (error) {
  console.error(`[layer:preprovision] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}