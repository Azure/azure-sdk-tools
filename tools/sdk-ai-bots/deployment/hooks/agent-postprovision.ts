import { ensureRoleAssignment } from "./lib/ensure-role-assignment.js";

const FOUNDRY_USER_ROLE_ID = "53ca6127-db72-4b80-b1b0-d745d6d5456d";

function log(message: string): void {
  console.log(`[agent:postprovision] ${message}`);
}

function ensureAgentAccountRoleAssignment(): void {
  const principalId = process.env.MANAGED_IDENTITY_PRINCIPAL_ID ?? "";
  const aiResourceName = process.env.AI_RESOURCE_NAME ?? "";
  const subscriptionId = process.env.AZURE_SUBSCRIPTION_ID ?? "";
  const resourceGroup = process.env.AZURE_RESOURCE_GROUP ?? "";

  if (!principalId || !aiResourceName || !subscriptionId || !resourceGroup) {
    throw new Error(
      "Cannot adopt the Foundry role assignment because an identity, AI account, subscription, or resource group output is missing.",
    );
  }

  const assigned = ensureRoleAssignment({
    principalId,
    roleDefinitionId: FOUNDRY_USER_ROLE_ID,
    scope: `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.CognitiveServices/accounts/${aiResourceName}`,
    principalType: "ServicePrincipal",
    log,
  });

  if (!assigned) {
    throw new Error("Failed to create or adopt the Foundry User role assignment.");
  }
}

try {
  log("Creating or adopting the Foundry User role assignment...");
  ensureAgentAccountRoleAssignment();
  log("Foundry role assignment is ready.");
} catch (error) {
  console.error(`[agent:postprovision] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}