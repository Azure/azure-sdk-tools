import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

import {
  callerHasDataAction,
  ensureCallerPermission,
  ensureRoleAssignment,
  getCurrentPrincipal,
} from "../hooks/lib/ensure-role-assignment.js";
import {
  ensureAgentDeployAccess,
  resolveAgentProjectId,
} from "../scripts/ensure-agent-deploy-access.js";
import type { CommandRunner } from "../scripts/lib/cli.js";

const scope = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/ai/projects/project";
const action = "Microsoft.CognitiveServices/accounts/AIServices/agents/write";
const role = "53ca6127-db72-4b80-b1b0-d745d6d5456d";
const principalId = "f0552653-da7c-4e30-9ddd-d7270d56ead3";
const options = { scope, dataAction: action, roleDefinitionId: role };
const missing = { value: [{ actions: ["*"], dataActions: [] }] };
const allowed = {
  value: [{
    dataActions: ["Microsoft.CognitiveServices/*"],
    notDataActions: ["Microsoft.CognitiveServices/accounts/AIServices/agents/endpoints/UserIdentityImpersonation/action"],
  }],
};

function token(payload: object): string {
  return `fixture.${Buffer.from(JSON.stringify(payload)).toString("base64url")}.fixture`;
}

function fixture() {
  const state: {
    permissions: unknown[];
    assignments: unknown;
    accountType: string;
    token: string;
    createError?: string;
    listError?: string;
    requestError?: string;
    rawPermissions?: string;
  } = {
    permissions: [missing, allowed],
    assignments: [],
    accountType: "servicePrincipal",
    token: token({ oid: principalId, idtyp: "app" }),
  };
  const recorded: string[][] = [];
  const delays: number[] = [];
  const logs: string[] = [];
  let permissionReads = 0;
  const run: CommandRunner = (command, args) => {
    assert.equal(command, "az");
    recorded.push([...args]);
    if (args[0] === "rest") {
      assert.equal(args[args.indexOf("--method") + 1], "GET");
      if (state.requestError) throw new Error(state.requestError);
      if (state.rawPermissions !== undefined) return state.rawPermissions;
      return JSON.stringify(state.permissions[Math.min(permissionReads++, state.permissions.length - 1)]);
    }
    if (args[0] === "account" && args[1] === "get-access-token") return state.token;
    if (args[0] === "account" && args[1] === "show") return state.accountType;
    if (args[0] === "role" && args[1] === "assignment" && args[2] === "list") {
      if (state.listError) throw new Error(state.listError);
      return JSON.stringify(state.assignments);
    }
    if (args[0] === "role" && args[1] === "assignment" && args[2] === "create") {
      if (state.createError) throw new Error(state.createError);
      return "{}";
    }
    assert.fail(`Unexpected Azure operation: ${args.join(" ")}`);
  };
  return {
    state, run, logs, delays,
    calls: recorded,
    wait: async (milliseconds: number) => { delays.push(milliseconds); },
    log: (message: string) => { logs.push(message); },
    creates: () => recorded.filter(args => args[0] === "role" && args[2] === "create"),
  };
}

test("existing effective Foundry permission skips all role-assignment operations", async () => {
  const azure = fixture();
  azure.state.permissions = [allowed];
  await ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait);

  assert.equal(azure.calls.length, 1);
  assert.equal(azure.calls[0][0], "rest");
  assert.match(azure.logs.join("\n"), /skipping role assignment/);
  assert.deepEqual(azure.delays, []);
});

test("accepts exact and case-insensitive data actions without requiring a particular role", () => {
  for (const permission of [
    { value: [{ dataActions: [action] }] },
    { value: [{ dataActions: [action.toUpperCase()] }] },
    { value: [{ dataActions: ["*"] }] },
  ]) {
    const azure = fixture();
    azure.state.permissions = [permission];
    assert.equal(callerHasDataAction(scope, action, azure.run), true);
    assert.deepEqual(azure.creates(), []);
  }
});

test("Contributor control-plane actions alone do not authorize Foundry data actions", async () => {
  const azure = fixture();
  await ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait);
  assert.equal(azure.creates().length, 1);
  const args = azure.creates()[0];
  assert.equal(args[args.indexOf("--assignee-object-id") + 1], principalId);
  assert.equal(args[args.indexOf("--assignee-principal-type") + 1], "ServicePrincipal");
  assert.equal(args[args.indexOf("--role") + 1], role);
  assert.equal(args[args.indexOf("--scope") + 1], scope);
  assert.match(azure.logs.join("\n"), /Required permission.*is available/);
});

test("notDataActions exclusions apply within each permission set", () => {
  const azure = fixture();
  azure.state.permissions = [{
    value: [{ dataActions: ["Microsoft.CognitiveServices/*"], notDataActions: [action] }],
  }];
  assert.equal(callerHasDataAction(scope, action, azure.run), false);
  azure.state.permissions = [{
    value: [
      { dataActions: ["*"], notDataActions: [action] },
      { dataActions: [action] },
    ],
  }];
  assert.equal(callerHasDataAction(scope, action, azure.run), true);
});

test("permission pagination finds an existing grant without creating another assignment", async () => {
  const azure = fixture();
  azure.state.permissions = [
    { value: [{ actions: ["*"] }], nextLink: "https://management.azure.com/permissions-page-two" },
    allowed,
  ];
  await ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait);
  assert.equal(azure.calls.length, 2);
  assert.equal(azure.calls[1][azure.calls[1].indexOf("--url") + 1], "https://management.azure.com/permissions-page-two");
  assert.deepEqual(azure.creates(), []);
});

test("unreadable or malformed permission data never triggers role creation", async () => {
  for (const permissions of [
    { unexpected: [] },
    { value: [null] },
    { value: [{ dataActions: "*" }] },
    { value: [], nextLink: 42 },
    { value: [], nextLink: "https://example.com/permissions" },
  ]) {
    const azure = fixture();
    azure.state.permissions = [permissions];
    await assert.rejects(ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait));
    assert.deepEqual(azure.creates(), []);
  }
  const azure = fixture();
  azure.state.requestError = "AuthorizationFailed: HTTP 403";
  await assert.rejects(
    ensureCallerPermission(options, azure.run, azure.wait), /AuthorizationFailed/,
  );
  assert.deepEqual(azure.creates(), []);
});

test("malformed JSON permissions do not fall through to creation", async () => {
  const azure = fixture();
  azure.state.rawPermissions = "not-json";
  await assert.rejects(ensureCallerPermission(options, azure.run, azure.wait));
  assert.deepEqual(azure.creates(), []);
});

test("an existing differently named or inherited role is reused while permissions propagate", async () => {
  const azure = fixture();
  azure.state.assignments = [{
    id: "/subscriptions/sub/providers/Microsoft.Authorization/roleAssignments/external-guid",
    scope: "/subscriptions/sub",
    roleDefinitionId: `/subscriptions/sub/providers/Microsoft.Authorization/roleDefinitions/${role}`,
  }];
  await ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait);
  assert.deepEqual(azure.creates(), []);
  const lookup = azure.calls.find(args => args[0] === "role" && args[2] === "list");
  assert.ok(lookup);
  assert.ok(lookup.includes("--include-inherited"));
  assert.equal(lookup[lookup.indexOf("--fill-principal-name") + 1], "false");
});

test("permission propagation is verified rather than assuming creation is immediately effective", async () => {
  const azure = fixture();
  azure.state.permissions = [missing, missing, allowed];
  await ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait);
  assert.equal(azure.creates().length, 1);
  assert.deepEqual(azure.delays, [10_000]);
});

test("a duplicate-assignment race is accepted only when the permission becomes effective", async () => {
  const azure = fixture();
  azure.state.createError = "(RoleAssignmentExists) The role assignment already exists.";
  await ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait);
  assert.equal(azure.creates().length, 1);
  assert.match(azure.logs.join("\n"), /Required permission.*is available/);
});

test("denied role creation reports the bootstrap requirement and fails deployment", async () => {
  const azure = fixture();
  azure.state.createError = "AuthorizationFailed: roleAssignments/write is forbidden.";
  await assert.rejects(
    ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait),
    /administrator must grant the required role/,
  );
  assert.equal(azure.creates().length, 1);
  assert.deepEqual(azure.delays, []);
});

test("unreadable existing assignments do not cause blind creation", async () => {
  const azure = fixture();
  azure.state.listError = "HTTP 403 listing role assignments";
  await assert.rejects(
    ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait),
    /Cannot ensure deployment access/,
  );
  assert.deepEqual(azure.creates(), []);
  assert.match(azure.logs.join("\n"), /HTTP 403/);
});

test("a role without effective permission eventually fails rather than reporting success", async () => {
  const azure = fixture();
  azure.state.permissions = [missing];
  await assert.rejects(
    ensureCallerPermission({ ...options, log: azure.log }, azure.run, azure.wait),
    /not effective.*after waiting for propagation/,
  );
  assert.equal(azure.creates().length, 1);
  assert.equal(azure.delays.length, 11);
});

test("invalid caller identity never creates a role for a guessed principal", async () => {
  for (const accessToken of ["not-a-jwt", token({ idtyp: "app" })]) {
    const azure = fixture();
    azure.state.token = accessToken;
    await assert.rejects(ensureCallerPermission(options, azure.run, azure.wait));
    assert.deepEqual(azure.creates(), []);
  }
});

test("current-principal resolution supports interactive users as well as pipeline service principals", () => {
  const azure = fixture();
  azure.state.accountType = "user";
  azure.state.token = token({ oid: "interactive-user", idtyp: "user" });
  assert.deepEqual(getCurrentPrincipal(azure.run), { principalId: "interactive-user", principalType: "User" });
});

test("the generic role helper remains idempotent for role names and rejects malformed lookup data", () => {
  const azure = fixture();
  azure.state.assignments = [{ roleDefinitionName: "Foundry User" }];
  assert.equal(ensureRoleAssignment({
    principalId, scope, roleDefinitionId: "Foundry User", log: azure.log,
  }, azure.run), true);
  assert.deepEqual(azure.creates(), []);
  azure.state.assignments = {};
  assert.equal(ensureRoleAssignment({
    principalId, scope, roleDefinitionId: role, log: azure.log,
  }, azure.run), false);
  assert.deepEqual(azure.creates(), []);
});

test("deployment permission reconciliation runs even when broader dev authorization management is disabled", async () => {
  const azure = fixture();
  await ensureAgentDeployAccess({
    AZURE_AI_PROJECT_ID: scope,
    MANAGE_AUTHORIZATION_RESOURCES: "false",
    DEPLOYMENT_PRINCIPAL_ID: "stale-other-principal",
  }, azure.run, azure.wait, azure.log);
  assert.equal(azure.creates().length, 1);
  assert.equal(azure.creates()[0][azure.creates()[0].indexOf("--assignee-object-id") + 1], principalId);
});

test("project resolution accepts provisioned IDs or explicit resource names and rejects conflicts", () => {
  assert.equal(resolveAgentProjectId({ AZURE_AI_PROJECT_ID: `${scope}/` }), scope);
  const names = {
    AZURE_SUBSCRIPTION_ID: "sub",
    AZURE_RESOURCE_GROUP: "rg",
    AI_RESOURCE_NAME: "ai",
    AI_PROJECT_NAME: "project",
  };
  assert.equal(resolveAgentProjectId(names), scope);
  assert.throws(() => resolveAgentProjectId({}), /Agent deployment requires/);
  assert.throws(() => resolveAgentProjectId({
    ...names, AZURE_AI_PROJECT_ID: scope.replace(/\/project$/, "/other"),
  }), /conflicts/);
});

test("generated resource names resolve from the same endpoint used by hosted-agent deployment", () => {
  const endpoint = {
    AZURE_SUBSCRIPTION_ID: "sub",
    AZURE_RESOURCE_GROUP: "rg",
    FOUNDRY_PROJECT_ENDPOINT: "https://ai.services.ai.azure.com/api/projects/project/",
  };
  assert.equal(resolveAgentProjectId(endpoint), scope);
  assert.throws(() => resolveAgentProjectId({
    ...endpoint, AZURE_AI_PROJECT_ID: scope.replace(/\/project$/, "/different-project"),
  }), /endpoint conflicts/);
  assert.throws(() => resolveAgentProjectId({
    ...endpoint, FOUNDRY_PROJECT_ENDPOINT: "https://example.com/other",
  }), /Cannot resolve the deployed Foundry endpoint/);
});

test("both native and Python hosted-agent deployment paths check access before creating versions", () => {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  const project = parse(readFileSync(resolve(root, "../azure.yaml"), "utf8"));
  const hosted = readFileSync(resolve(root, "pipelines/templates/hosted-agent-deploy-steps.yml"), "utf8");
  const bicep = readFileSync(resolve(root, "infra/layers/agent/main.bicep"), "utf8");
  assert.match(project.services.agent.hooks.predeploy.run, /ensure-agent-deploy-access\.ts/);
  assert.ok(hosted.indexOf("Install deployment permission-check dependencies") < hosted.indexOf("ensure-agent-deploy-access.ts"));
  assert.ok(hosted.indexOf("--key AI_FOUNDRY_PROJECT_ENDPOINT") < hosted.indexOf("ensure-agent-deploy-access.ts"));
  assert.ok(hosted.indexOf("ensure-agent-deploy-access.ts") < hosted.indexOf("python scripts/deploy_hosted_agent.py"));
  assert.doesNotMatch(bicep, /resource deploymentFoundryProjectManagerRoleAssignment /);
});
