import { type CommandRunner, runCommand } from "../../scripts/lib/cli.js";
import {
  ensureCosmosRoleAssignment,
  ensureRoleAssignment,
  getCurrentPrincipal,
  type EnsureRoleAssignmentOptions,
} from "./ensure-role-assignment.js";

export const ROLE_IDS = {
  owner: "8e3af657-a8ff-443c-a75c-2fe8c4bcb635",
  contributor: "b24988ac-6180-42a0-ab88-20f7382dd24c",
  blobOwner: "b7e6dc6d-f1e8-4753-8033-0f276bb0955b",
  blobContributor: "ba92f5b4-2d11-453d-a403-e96b0029c9fe",
  blobReader: "2a2b9908-6ea1-4ae2-8e65-a410df84e7d1",
  queueContributor: "974c5e8b-45b9-4653-ba55-5f855dd0fb88",
  tableContributor: "0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3",
  configReader: "516239f1-63e1-4d78-a4de-a74fb236a071",
  configOwner: "5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b",
  searchIndexContributor: "8ebe5a00-799e-43f5-93ac-243d3dce84a7",
  searchServiceContributor: "7ca78c08-252a-4471-8644-bb5ff32d4ba0",
  acrPull: "7f951dda-4ed3-4680-a7ca-43fe172d538d",
  acrPush: "8311e382-0749-4cb8-b61a-304f252e45ec",
  openAiUser: "5e0bd9bd-7b93-4f28-af87-19fc36ad61bd",
  cognitiveUser: "a97b65f3-24c7-4388-baec-2e87135dc908",
  foundryUser: "53ca6127-db72-4b80-b1b0-d745d6d5456d",
  foundryManager: "eadc314b-1a2d-4efa-be10-5d325db5065e",
  metricsPublisher: "3913510d-42f4-4e42-8a64-420c390055eb",
};

export type ResourceRoleLayer = "shared-resources" | "agent" | "frontend" | "deployment";

function required(env: NodeJS.ProcessEnv, key: string): string {
  const value = env[key]?.trim();
  if (!value) throw new Error(`Cannot reconcile resource roles: ${key} is required.`);
  return value;
}

function principalType(value: string | undefined): "User" | "Group" | "ServicePrincipal" {
  if (value === "User" || value === "Group" || value === "ServicePrincipal") return value;
  throw new Error(`Invalid principal type '${value}'.`);
}

function resourceScope(env: NodeJS.ProcessEnv, type: string, nameKey: string): string {
  return `/subscriptions/${required(env, "AZURE_SUBSCRIPTION_ID")}` +
    `/resourceGroups/${required(env, "AZURE_RESOURCE_GROUP")}/providers/${type}/${required(env, nameKey)}`;
}

const EQUIVALENT_ROLES: Record<string, readonly string[]> = {
  [ROLE_IDS.contributor]: [ROLE_IDS.owner],
  [ROLE_IDS.blobContributor]: [ROLE_IDS.blobOwner],
  [ROLE_IDS.blobReader]: [ROLE_IDS.blobContributor, ROLE_IDS.blobOwner],
  [ROLE_IDS.configReader]: [ROLE_IDS.configOwner],
  [ROLE_IDS.searchServiceContributor]: [ROLE_IDS.contributor, ROLE_IDS.owner],
  [ROLE_IDS.acrPull]: [ROLE_IDS.acrPush],
  [ROLE_IDS.openAiUser]: [ROLE_IDS.foundryUser, ROLE_IDS.foundryManager],
  [ROLE_IDS.foundryUser]: [ROLE_IDS.foundryManager],
};

export function buildResourceRolePlan(
  layer: ResourceRoleLayer,
  env: NodeJS.ProcessEnv,
): EnsureRoleAssignmentOptions[] {
  const plan: EnsureRoleAssignmentOptions[] = [];
  const add = (
    principalId: string,
    roleDefinitionId: string,
    scope: string,
    type: "User" | "Group" | "ServicePrincipal" = "ServicePrincipal",
  ): void => {
    plan.push({
      principalId, roleDefinitionId, scope, principalType: type,
      equivalentRoleDefinitionIds: EQUIVALENT_ROLES[roleDefinitionId],
    });
  };
  const deployment = required(env, "DEPLOYMENT_PRINCIPAL_ID");
  const deploymentType = principalType(env.DEPLOYMENT_PRINCIPAL_TYPE);
  const developer = env.DEVELOPER_PRINCIPAL_ID?.trim();
  const developerType = developer ? principalType(env.DEVELOPER_PRINCIPAL_TYPE) : undefined;
  const registry = resourceScope(env, "Microsoft.ContainerRegistry/registries", "CONTAINER_REGISTRY_NAME");
  const externallyManagedRegistry = env.CONTAINER_REGISTRY_IS_EXISTING === "true";

  if (layer === "shared-resources" || layer === "deployment") {
    const configuration = resourceScope(env, "Microsoft.AppConfiguration/configurationStores", "APP_CONFIG_NAME");
    add(deployment, ROLE_IDS.configOwner, configuration, deploymentType);
  }
  if (layer === "shared-resources") {
    const identity = required(env, "MANAGED_IDENTITY_PRINCIPAL_ID");
    const storage = resourceScope(env, "Microsoft.Storage/storageAccounts", "STORAGE_ACCOUNT_NAME");
    const search = resourceScope(env, "Microsoft.Search/searchServices", "SEARCH_SERVICE_NAME");
    const configuration = resourceScope(env, "Microsoft.AppConfiguration/configurationStores", "APP_CONFIG_NAME");
    add(identity, ROLE_IDS.blobOwner, storage);
    add(identity, ROLE_IDS.queueContributor, storage);
    add(identity, ROLE_IDS.tableContributor, storage);
    add(identity, ROLE_IDS.configReader, configuration);
    add(identity, ROLE_IDS.searchIndexContributor, search);
    add(identity, ROLE_IDS.searchServiceContributor, search);
    add(identity, ROLE_IDS.acrPull, registry);
    add(required(env, "SEARCH_SERVICE_PRINCIPAL_ID"), ROLE_IDS.blobReader, storage);
    add(deployment, ROLE_IDS.blobContributor, storage, deploymentType);
    add(deployment, ROLE_IDS.searchServiceContributor, search, deploymentType);
    if (!externallyManagedRegistry) {
      add(identity, ROLE_IDS.contributor, registry);
      add(deployment, ROLE_IDS.contributor, registry, deploymentType);
    }
    if (developer && developerType) {
      add(developer, ROLE_IDS.blobContributor, storage, developerType);
      add(developer, ROLE_IDS.configReader, configuration, developerType);
      add(developer, ROLE_IDS.searchServiceContributor, search, developerType);
      if (!externallyManagedRegistry) add(developer, ROLE_IDS.contributor, registry, developerType);
    }
  }
  if (layer === "agent" || layer === "deployment") {
    const account = resourceScope(env, "Microsoft.CognitiveServices/accounts", "AI_RESOURCE_NAME");
    add(deployment, ROLE_IDS.foundryUser, account, deploymentType);
    if (layer === "agent") {
      const identity = required(env, "MANAGED_IDENTITY_PRINCIPAL_ID");
      add(identity, ROLE_IDS.foundryUser, account);
      add(identity, ROLE_IDS.openAiUser, account);
      add(deployment, ROLE_IDS.openAiUser, account, deploymentType);
      const searchIdentity = required(env, "SEARCH_SERVICE_PRINCIPAL_ID");
      add(searchIdentity, ROLE_IDS.cognitiveUser, account);
      add(searchIdentity, ROLE_IDS.openAiUser, account);
      add(required(env, "FOUNDRY_PROJECT_PRINCIPAL_ID"), ROLE_IDS.acrPull, registry);
      if (developer && developerType) {
        add(developer, ROLE_IDS.foundryManager, account, developerType);
        add(developer, ROLE_IDS.openAiUser, account, developerType);
      }
    }
  }
  if (layer === "frontend") {
    const identity = required(env, "FRONTEND_IDENTITY_PRINCIPAL_ID");
    add(identity, ROLE_IDS.metricsPublisher, required(env, "FRONTEND_APPLICATIONINSIGHTS_RESOURCE_ID"));
    add(identity, ROLE_IDS.acrPull, registry);
    const storage = resourceScope(env, "Microsoft.Storage/storageAccounts", "STORAGE_ACCOUNT_NAME");
    add(identity, ROLE_IDS.blobContributor, storage);
    add(identity, ROLE_IDS.tableContributor, storage);
  }
  return plan;
}

export function reconcileResourceRoles(
  layer: ResourceRoleLayer,
  env: NodeJS.ProcessEnv = process.env,
  run: CommandRunner = runCommand,
  log: (message: string) => void = console.log,
): void {
  const principal = env.DEPLOYMENT_PRINCIPAL_ID
    ? { principalId: env.DEPLOYMENT_PRINCIPAL_ID, principalType: env.DEPLOYMENT_PRINCIPAL_TYPE }
    : getCurrentPrincipal(run);
  const values: NodeJS.ProcessEnv = {
    ...env, DEPLOYMENT_PRINCIPAL_ID: principal.principalId,
    DEPLOYMENT_PRINCIPAL_TYPE: principal.principalType,
  };
  const plan = buildResourceRolePlan(layer, values);
  if (layer === "shared-resources") required(values, "COSMOS_DB_ACCOUNT_NAME");
  for (const grant of plan) {
    if (!ensureRoleAssignment({ ...grant, log }, run)) {
      throw new Error(
        `Cannot ensure role ${grant.roleDefinitionId} for ${grant.principalId} at ${grant.scope}. ` +
        "The provisioning identity needs permission to read and create role assignments at this scope.",
      );
    }
  }
  if (layer === "shared-resources") {
    const principals = new Set([
      required(values, "MANAGED_IDENTITY_PRINCIPAL_ID"),
      required(values, "DEPLOYMENT_PRINCIPAL_ID"),
      values.DEVELOPER_PRINCIPAL_ID?.trim(),
    ]);
    for (const principalId of principals) {
      if (!principalId) continue;
      ensureCosmosRoleAssignment({
        subscriptionId: required(values, "AZURE_SUBSCRIPTION_ID"),
        resourceGroup: required(values, "AZURE_RESOURCE_GROUP"),
        accountName: required(values, "COSMOS_DB_ACCOUNT_NAME"),
        principalId,
      }, run, log);
    }
  }
}
