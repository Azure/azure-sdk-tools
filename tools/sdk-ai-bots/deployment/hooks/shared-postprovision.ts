import { reconcileResourceRoles } from "./lib/resource-roles.js";

try {
  reconcileResourceRoles("shared-resources");
} catch (error) {
  console.error(`[shared:postprovision] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}
