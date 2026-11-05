import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

import {
  ensureCosmosRoleAssignment,
  ensureRoleAssignment,
  getCurrentPrincipal,
} from "../hooks/lib/ensure-role-assignment.js";
import {
  buildResourceRolePlan,
  reconcileResourceRoles,
  ROLE_IDS,
} from "../hooks/lib/resource-roles.js";
import type { CommandRunner } from "../scripts/lib/cli.js";

const env = {
  AZURE_SUBSCRIPTION_ID: "test-sub",
  AZURE_RESOURCE_GROUP: "test-rg",
  APP_CONFIG_NAME: "test-config",
  CONTAINER_REGISTRY_NAME: "testregistry",
  STORAGE_ACCOUNT_NAME: "teststorage",
  SEARCH_SERVICE_NAME: "test-search",
  COSMOS_DB_ACCOUNT_NAME: "test-cosmos",
  AI_RESOURCE_NAME: "test-ai",
  MANAGED_IDENTITY_PRINCIPAL_ID: "runtime-principal",
  SEARCH_SERVICE_PRINCIPAL_ID: "search-principal",
  FOUNDRY_PROJECT_PRINCIPAL_ID: "project-principal",
  FRONTEND_IDENTITY_PRINCIPAL_ID: "frontend-principal",
  FRONTEND_APPLICATIONINSIGHTS_RESOURCE_ID: "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Insights/components/frontend",
  DEPLOYMENT_PRINCIPAL_ID: "deployment-principal",
  DEPLOYMENT_PRINCIPAL_TYPE: "ServicePrincipal",
  DEVELOPER_PRINCIPAL_ID: "developer-group",
  DEVELOPER_PRINCIPAL_TYPE: "Group",
};
const prefix = "/subscriptions/test-sub/resourceGroups/test-rg/providers/";
const token = `fixture.${Buffer.from(JSON.stringify({ oid: env.DEPLOYMENT_PRINCIPAL_ID, idtyp: "app" })).toString("base64url")}.fixture`;

function fixture() {
  const calls: string[][] = [];
  const grants = new Map<string, object[]>();
  const cosmos: object[] = [];
  const state: { failList: boolean; failCreate: boolean; race: boolean; malformed: boolean } = {
    failList: false, failCreate: false, race: false, malformed: false,
  };
  const run: CommandRunner = (command, args) => {
    assert.equal(command, "az");
    calls.push([...args]);
    const value = (key: string) => args[args.indexOf(key) + 1];
    if (args[0] === "account") {
      return args[1] === "get-access-token" ? token : "servicePrincipal";
    }
    if (args[0] === "role") {
      const key = `${value("--assignee-object-id")}::${value("--scope")}`;
      if (args[2] === "list") {
        assert.ok(args.includes("--include-inherited"));
        assert.equal(value("--fill-principal-name"), "false");
        if (state.failList) throw new Error("AuthorizationFailed: cannot list roles");
        return JSON.stringify(state.malformed ? {} : (grants.get(key) ?? []));
      }
      assert.equal(args[2], "create");
      if (state.failCreate) throw new Error("AuthorizationFailed: roleAssignments/write denied");
      if (state.race) throw new Error("RoleAssignmentExists");
      grants.set(key, [
        ...(grants.get(key) ?? []),
        { roleDefinitionId: value("--role") },
      ]);
      return "{}";
    }
    if (args[0] === "cosmosdb") {
      if (state.failList) throw new Error("AuthorizationFailed: cannot list Cosmos roles");
      if (args[4] === "list") return JSON.stringify(state.malformed ? {} : cosmos);
      assert.equal(args[4], "create");
      if (state.failCreate) throw new Error("AuthorizationFailed: Cosmos role write denied");
      cosmos.push({
        principalId: value("--principal-id"),
        roleDefinitionId: value("--role-definition-id"),
        scope: value("--scope"),
      });
      return "{}";
    }
    assert.fail(`Unexpected Azure call: ${args.join(" ")}`);
  };
  return {
    run, calls, grants, cosmos, state,
    azureCreates: () => calls.filter(args => args[0] === "role" && args[2] === "create"),
    cosmosCreates: () => calls.filter(args => args[0] === "cosmosdb" && args[4] === "create"),
  };
}

test("shared layer grants caller configuration access and all runtime data roles", () => {
  const plan = buildResourceRolePlan("shared-resources", env);
  assert.ok(plan.some(grant =>
    grant.principalId === env.DEPLOYMENT_PRINCIPAL_ID &&
    grant.roleDefinitionId === ROLE_IDS.configOwner));
  for (const role of [
    ROLE_IDS.blobOwner, ROLE_IDS.tableContributor, ROLE_IDS.queueContributor,
    ROLE_IDS.configReader, ROLE_IDS.searchIndexContributor,
    ROLE_IDS.searchServiceContributor, ROLE_IDS.acrPull,
  ]) {
    assert.ok(plan.some(grant =>
      grant.principalId === env.MANAGED_IDENTITY_PRINCIPAL_ID && grant.roleDefinitionId === role), role);
  }
  assert.ok(plan.some(grant =>
    grant.principalId === env.SEARCH_SERVICE_PRINCIPAL_ID &&
    grant.roleDefinitionId === ROLE_IDS.blobReader));
});

test("fresh provisioning creates missing roles; repeating it skips every assignment", () => {
  const azure = fixture();
  for (const layer of ["shared-resources", "agent", "frontend"] as const) {
    reconcileResourceRoles(layer, env, azure.run, () => {});
  }
  const created = azure.azureCreates().length;
  assert.ok(created > 20);
  assert.equal(azure.cosmosCreates().length, 3);
  for (const layer of ["shared-resources", "agent", "frontend"] as const) {
    reconcileResourceRoles(layer, env, azure.run, () => {});
  }
  assert.equal(azure.azureCreates().length, created);
  assert.equal(azure.cosmosCreates().length, 3);
});

test("existing differently named and inherited assignments are reused without writes", () => {
  const azure = fixture();
  const scope = `${prefix}Microsoft.AppConfiguration/configurationStores/test-config`;
  azure.grants.set(`${env.DEPLOYMENT_PRINCIPAL_ID}::${scope}`, [{
    id: "manually-created-guid",
    roleDefinitionId: `/subscriptions/test-sub/providers/Microsoft.Authorization/roleDefinitions/${ROLE_IDS.configOwner}`,
    scope: "/subscriptions/test-sub",
  }]);
  assert.equal(ensureRoleAssignment({
    principalId: env.DEPLOYMENT_PRINCIPAL_ID, scope, roleDefinitionId: ROLE_IDS.configOwner,
  }, azure.run), true);
  assert.deepEqual(azure.azureCreates(), []);
});

test("known broader role grants satisfy narrower requirements without redundant creation", () => {
  const azure = fixture();
  const scope = `${prefix}Microsoft.Search/searchServices/test-search`;
  azure.grants.set(`${env.DEPLOYMENT_PRINCIPAL_ID}::${scope}`, [{
    roleDefinitionId: ROLE_IDS.contributor,
    scope: "/subscriptions/test-sub/resourceGroups/test-rg",
  }]);
  const grant = buildResourceRolePlan("shared-resources", env)
    .find(item => item.principalId === env.DEPLOYMENT_PRINCIPAL_ID && item.scope === scope);
  assert.ok(grant);
  assert.equal(ensureRoleAssignment(grant, azure.run), true);
  assert.deepEqual(azure.azureCreates(), []);
});

test("a management Contributor grant does not substitute for data-plane access", () => {
  const azure = fixture();
  const scope = `${prefix}Microsoft.AppConfiguration/configurationStores/test-config`;
  azure.grants.set(`${env.DEPLOYMENT_PRINCIPAL_ID}::${scope}`, [{ roleDefinitionId: ROLE_IDS.contributor }]);
  assert.equal(ensureRoleAssignment({
    principalId: env.DEPLOYMENT_PRINCIPAL_ID, scope, roleDefinitionId: ROLE_IDS.configOwner,
  }, azure.run), true);
  assert.equal(azure.azureCreates().length, 1);
});

test("missing privilege and unreadable assignment lists fail explicitly", () => {
  const azure = fixture();
  azure.state.failList = true;
  assert.throws(() => reconcileResourceRoles("shared-resources", env, azure.run, () => {}), /permission to read and create role assignments/);
  assert.deepEqual(azure.azureCreates(), []);
  azure.state.failList = false;
  azure.state.failCreate = true;
  assert.throws(() => reconcileResourceRoles("shared-resources", env, azure.run, () => {}), /Cannot ensure role/);
  assert.equal(azure.azureCreates().length, 1);
});

test("malformed assignment responses do not cause blind role creation", () => {
  const azure = fixture();
  azure.state.malformed = true;
  assert.throws(() => reconcileResourceRoles("shared-resources", env, azure.run, () => {}), /Cannot ensure role/);
  assert.deepEqual(azure.azureCreates(), []);
});

test("role creation handles a concurrent existing-assignment response", () => {
  const azure = fixture();
  azure.state.race = true;
  assert.equal(ensureRoleAssignment({
    principalId: env.DEPLOYMENT_PRINCIPAL_ID, scope: "/subscriptions/test-sub", roleDefinitionId: ROLE_IDS.configOwner,
  }, azure.run), true);
  assert.equal(azure.azureCreates().length, 1);
});

test("agent layer grants Foundry access to the caller and runtime and image pull to the project identity", () => {
  const plan = buildResourceRolePlan("agent", env);
  for (const principal of [env.DEPLOYMENT_PRINCIPAL_ID, env.MANAGED_IDENTITY_PRINCIPAL_ID]) {
    assert.ok(plan.some(grant => grant.principalId === principal && grant.roleDefinitionId === ROLE_IDS.foundryUser));
  }
  assert.ok(plan.some(grant =>
    grant.principalId === env.FOUNDRY_PROJECT_PRINCIPAL_ID &&
    grant.roleDefinitionId === ROLE_IDS.acrPull));
  assert.ok(plan.some(grant =>
    grant.principalId === env.SEARCH_SERVICE_PRINCIPAL_ID &&
    grant.roleDefinitionId === ROLE_IDS.openAiUser));
});

test("frontend grants use the provisioned principal, not the client ID", () => {
  const plan = buildResourceRolePlan("frontend", { ...env, BOT_ID: "client-id" });
  assert.equal(plan.length, 4);
  assert.ok(plan.every(grant => grant.principalId === env.FRONTEND_IDENTITY_PRINCIPAL_ID));
  assert.deepEqual(plan.map(grant => grant.roleDefinitionId), [
    ROLE_IDS.metricsPublisher, ROLE_IDS.acrPull, ROLE_IDS.blobContributor, ROLE_IDS.tableContributor,
  ]);
});

test("externally managed registries still get required image-pull grants", () => {
  const plan = buildResourceRolePlan("shared-resources", { ...env, CONTAINER_REGISTRY_IS_EXISTING: "true" });
  const registryGrants = plan.filter(grant => grant.scope.includes("/registries/"));
  assert.ok(registryGrants.some(grant => grant.roleDefinitionId === ROLE_IDS.acrPull));
  assert.ok(registryGrants.every(grant => grant.roleDefinitionId !== ROLE_IDS.contributor));
});

test("optional developer roles do not change deployment identity selection", () => {
  const values: NodeJS.ProcessEnv = { ...env };
  delete values.DEVELOPER_PRINCIPAL_ID;
  delete values.DEVELOPER_PRINCIPAL_TYPE;
  const plan = buildResourceRolePlan("shared-resources", values);
  assert.ok(plan.every(grant => grant.principalId !== "developer-group"));
  assert.equal(plan[0].principalId, "deployment-principal");
  assert.equal(plan[0].roleDefinitionId, ROLE_IDS.configOwner);
});

test("missing required outputs and malformed principal types fail before any role writes", () => {
  const azure = fixture();
  assert.throws(() => reconcileResourceRoles("agent", {
    ...env, FOUNDRY_PROJECT_PRINCIPAL_ID: undefined,
  }, azure.run, () => {}), /FOUNDRY_PROJECT_PRINCIPAL_ID is required/);
  assert.throws(() => reconcileResourceRoles("shared-resources", {
    ...env, COSMOS_DB_ACCOUNT_NAME: undefined,
  }, azure.run, () => {}), /COSMOS_DB_ACCOUNT_NAME is required/);
  assert.throws(() => reconcileResourceRoles("frontend", {
    ...env, DEPLOYMENT_PRINCIPAL_TYPE: "unexpected",
  }, azure.run, () => {}), /Invalid principal type/);
  assert.deepEqual(azure.azureCreates(), []);
});

test("current-principal fallback uses the actual CLI identity when no cached caller is supplied", () => {
  const azure = fixture();
  assert.deepEqual(getCurrentPrincipal(azure.run), {
    principalId: "deployment-principal", principalType: "ServicePrincipal",
  });
  reconcileResourceRoles("deployment", {
    ...env, DEPLOYMENT_PRINCIPAL_ID: undefined, DEPLOYMENT_PRINCIPAL_TYPE: undefined,
  }, azure.run, () => {});
  assert.ok(azure.azureCreates().every(args =>
    args[args.indexOf("--assignee-object-id") + 1] === env.DEPLOYMENT_PRINCIPAL_ID));
});

test("standalone hosted deployments only ensure their two prerequisite data roles", () => {
  const plan = buildResourceRolePlan("deployment", env);
  assert.deepEqual(plan.map(grant => grant.roleDefinitionId), [ROLE_IDS.configOwner, ROLE_IDS.foundryUser]);
  assert.ok(plan.every(grant => grant.principalId === env.DEPLOYMENT_PRINCIPAL_ID));
});

test("Cosmos reuses account-root data grants and adds missing grants without duplicate GUIDs", () => {
  const azure = fixture();
  const options = { subscriptionId: "test-sub", resourceGroup: "test-rg", accountName: "test-cosmos", principalId: "deployment-principal" };
  ensureCosmosRoleAssignment(options, azure.run, () => {});
  ensureCosmosRoleAssignment(options, azure.run, () => {});
  assert.equal(azure.cosmosCreates().length, 1);
  azure.cosmos.length = 0;
  azure.cosmos.push({
    principalId: options.principalId,
    roleDefinitionId: `${prefix}Microsoft.DocumentDB/databaseAccounts/test-cosmos/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002`,
    scope: "/",
  });
  ensureCosmosRoleAssignment(options, azure.run, () => {});
  assert.equal(azure.cosmosCreates().length, 1);
});

test("a Cosmos grant scoped to one database does not substitute for account-root access", () => {
  const azure = fixture();
  azure.cosmos.push({
    principalId: "deployment-principal",
    roleDefinitionId: `${prefix}Microsoft.DocumentDB/databaseAccounts/test-cosmos/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002`,
    scope: "/dbs/one-database",
  });
  ensureCosmosRoleAssignment({
    subscriptionId: "test-sub", resourceGroup: "test-rg", accountName: "test-cosmos", principalId: "deployment-principal",
  }, azure.run, () => {});
  assert.equal(azure.cosmosCreates().length, 1);
});

test("Cosmos query failures and malformed records fail rather than returning success", () => {
  const azure = fixture();
  const options = { subscriptionId: "test-sub", resourceGroup: "test-rg", accountName: "test-cosmos", principalId: "deployment-principal" };
  azure.state.failList = true;
  assert.throws(() => ensureCosmosRoleAssignment(options, azure.run, () => {}), /cannot list Cosmos roles/);
  assert.deepEqual(azure.cosmosCreates(), []);
  azure.state.failList = false;
  azure.cosmos.push({ invalid: true });
  assert.throws(() => ensureCosmosRoleAssignment(options, azure.run, () => {}), /invalid Cosmos role assignment/);
  assert.deepEqual(azure.cosmosCreates(), []);
});

test("deployment hooks own roles while Bicep owns resource shape and the lock", () => {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  const project = parse(readFileSync(resolve(root, "../azure.yaml"), "utf8"));
  for (const [layer, hook] of [
    ["shared-resources", "shared-postprovision.ts"],
    ["agent", "agent-postprovision.ts"],
    ["frontend", "frontend-postprovision.ts"],
  ]) {
    assert.match(project.infra.layers.find((item: { name: string }) => item.name === layer).hooks.postprovision.run, new RegExp(hook));
    const bicep = readFileSync(resolve(root, `infra/layers/${layer}/main.bicep`), "utf8");
    assert.doesNotMatch(bicep, /manageAuthorizationResources|Microsoft\.Authorization\/roleAssignments|Microsoft\.DocumentDB\/databaseAccounts\/sqlRoleAssignments/);
  }
  const hosted = readFileSync(resolve(root, "pipelines/templates/hosted-agent-deploy-steps.yml"), "utf8");
  assert.ok(hosted.indexOf("reconcile-resource-roles.ts") < hosted.indexOf("python scripts/deploy_hosted_agent.py"));
  assert.match(readFileSync(resolve(root, "infra/layers/frontend/main.bicep"), "utf8"), /resource lock 'Microsoft\.Authorization\/locks@2020-05-01' = \{/);
});
