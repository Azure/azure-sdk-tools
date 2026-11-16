import { reconcileResourceRoles } from "./lib/resource-roles.js";

try {
  reconcileResourceRoles("agent");
} catch (error) {
  console.error(`[agent:postprovision] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}
