import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const prefix = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Web/connections/";
const apiId = "/subscriptions/test-sub/providers/Microsoft.Web/locations/westus2/managedApis/teams";

function fixture() {
  return {
    connections: {
      teams: { api: apiId, statuses: ["Connected"] },
      "teams-1": { api: apiId, statuses: ["Connected"] },
      "teams-2": { api: apiId, statuses: ["Error"] },
    },
    workflows: {
      "qa-workflow": { connectionId: `${prefix}teams`, id: apiId },
      "mcp-workflow": { connectionId: `${prefix}teams`, id: apiId },
    },
  };
}

const fakeAz = String.raw`#!/usr/bin/env node
const fs = require("node:fs");
const args = process.argv.slice(2);
const log = process.env.AZURE_CALL_LOG;
const data = JSON.parse(process.env.AZURE_FIXTURE);
const option = (name) => args[args.indexOf(name) + 1];
const name = option("--name");
const query = option("--query");
fs.appendFileSync(log, JSON.stringify(args) + "\n");
function fail(message) {
  console.error(message);
  process.exit(1);
}
if (option("--subscription") !== "test-sub" || option("--resource-group") !== "test-rg") {
  fail("Unexpected Azure scope");
}
if (args[0] !== "resource" || !["show", "list"].includes(args[1])) {
  fail("Unexpected Azure operation");
}
if (data.fail === name) fail("AuthorizationFailed: " + name);
const type = option("--resource-type");
if (args[1] === "list") {
  console.log(Object.keys(data.connections).join("\n"));
} else if (type === "Microsoft.Logic/workflows") {
  if (query !== 'properties.parameters."$connections".value.teams') fail("Unexpected workflow query");
  if (!Object.hasOwn(data.workflows, name)) fail("ResourceNotFound: " + name);
  console.log(JSON.stringify(data.workflows[name]));
} else if (type === "Microsoft.Web/connections") {
  const key = Object.keys(data.connections).find(key => key.toLowerCase() === name.toLowerCase());
  if (!key) fail("ResourceNotFound: " + name);
  const connection = data.connections[key];
  if (query === "properties.api.id") {
    console.log(connection.api);
  } else if (query === "properties.statuses[0].status") {
    const statusCalls = fs.readFileSync(log, "utf8").trim().split("\n")
      .map(line => JSON.parse(line))
      .filter(call => call[call.indexOf("--name") + 1] === name &&
        call[call.indexOf("--query") + 1] === query).length;
    console.log(connection.statuses[Math.min(statusCalls - 1, connection.statuses.length - 1)] ?? "");
  } else {
    fail("Unexpected connection query");
  }
} else {
  fail("Unexpected resource type");
}
`;

function runConsent({ args = ["check"], env = {}, data = fixture() } = {}) {
  const temporary = mkdtempSync(join(tmpdir(), "teams-consent-test-"));
  const callsPath = join(temporary, "calls.jsonl");
  writeFileSync(join(temporary, "az"), fakeAz, { mode: 0o700 });
  writeFileSync(callsPath, "");
  try {
    const result = spawnSync("bash", [resolve(root, "scripts/teams-connection-consent.sh"), ...args], {
      encoding: "utf8",
      env: {
        PATH: `${temporary}:${process.env.PATH}`,
        AZURE_SUBSCRIPTION_ID: "test-sub",
        AZURE_RESOURCE_GROUP: "test-rg",
        TEAMS_CONNECTION_NAME: "teams",
        LOGIC_APP_WORKFLOW_NAME: "qa-workflow",
        AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME: "mcp-workflow",
        AZURE_MCP_TEAMS_GROUP_ID: "mcp-team",
        AZURE_MCP_TEAMS_CHANNEL_IDS: "mcp-channel",
        CONSENT_VERIFY_ATTEMPTS: "2",
        CONSENT_VERIFY_INTERVAL_SECONDS: "0",
        AZURE_FIXTURE: JSON.stringify(data),
        AZURE_CALL_LOG: callsPath,
        ...env,
      },
    });
    if (result.error) throw result.error;
    const calls = readFileSync(callsPath, "utf8").trim();
    return {
      ...result,
      calls: calls ? calls.split("\n").map(line => JSON.parse(line)) : [],
    };
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

test("checks the configured workflow connection even when three Teams connections exist", () => {
  const result = runConsent();
  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), {
    connectionName: "teams",
    status: "Connected",
    consentRequired: false,
    portalUrl: `https://portal.azure.com/#resource${prefix}teams`,
  });
  assert.ok(result.calls.every(call => call[1] === "show"));
  assert.equal(result.calls.filter(call =>
    call[call.indexOf("--resource-type") + 1] === "Microsoft.Logic/workflows").length, 2);
  assert.ok(result.calls.every(call => !call.includes("teams-1") && !call.includes("teams-2")));
});

test("derives the connection from deployed workflow bindings when no name is configured", () => {
  const result = runConsent({ args: ["check", ""], env: { TEAMS_CONNECTION_NAME: "" } });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).connectionName, "teams");
  assert.ok(result.calls.every(call => call[1] !== "list"));
});

test("supports the generated primary-workflow case through the configured MCP workflow", () => {
  const result = runConsent({
    env: { TEAMS_CONNECTION_NAME: "", LOGIC_APP_WORKFLOW_NAME: "" },
  });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).connectionName, "teams");
  assert.ok(result.calls.some(call => call.includes("mcp-workflow")));
});

test("explicit connection arguments take precedence while still matching both workflows", () => {
  const data = fixture();
  for (const binding of Object.values(data.workflows)) binding.connectionId = `${prefix}teams-1`;
  const result = runConsent({ args: ["check", "teams-1"], data });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).connectionName, "teams-1");
});

test("fails when a workflow references a different connection", () => {
  const data = fixture();
  data.workflows["mcp-workflow"].connectionId = `${prefix}teams-1`;
  const result = runConsent({ data });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /mcp-workflow.*teams-1.*not the selected Teams connection 'teams'/);
  assert.ok(result.calls.every(call => !call.includes("properties.statuses[0].status")));
});

test("does not guess when workflows disagree and no connection is configured", () => {
  const data = fixture();
  data.workflows["mcp-workflow"].connectionId = `${prefix}teams-2`;
  const result = runConsent({ data, env: { TEAMS_CONNECTION_NAME: "" } });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /not the selected Teams connection/);
});

test("rejects a workflow binding outside the deployment's resource group", () => {
  const data = fixture();
  data.workflows["qa-workflow"].connectionId = `${prefix.replace("test-rg", "other-rg")}teams`;
  const result = runConsent({ data });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /outside the target subscription\/resource group/);
});

test("compares ARM resource IDs without case sensitivity", () => {
  const data = fixture();
  for (const binding of Object.values(data.workflows)) {
    binding.connectionId = binding.connectionId.toUpperCase();
    binding.id = binding.id.toUpperCase();
  }
  const result = runConsent({ data });
  assert.equal(result.status, 0, result.stderr);
});

test("rejects non-Teams connectors even when their name is teams", () => {
  const data = fixture();
  data.connections.teams.api = apiId.replace("/teams", "/azureblob");
  const result = runConsent({ data });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /not a Teams managed API connection/);
});

test("rejects managed-API IDs that differ between the workflow and connection", () => {
  const data = fixture();
  data.workflows["qa-workflow"].id = apiId.replace("westus2", "eastus");
  const result = runConsent({ data });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /managed API.*does not match connection/);
});

test("requests consent only for the selected unauthenticated connection", () => {
  const data = fixture();
  data.connections.teams.statuses = ["Error"];
  const result = runConsent({ data });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).consentRequired, true);
});

test("verification polls the same selected connection until consent propagates", () => {
  const data = fixture();
  data.connections.teams.statuses = ["Error", "Connected"];
  const result = runConsent({ args: ["verify"], data });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /Teams API connection 'teams' is Connected/);
  assert.equal(result.calls.filter(call => call.includes("properties.statuses[0].status")).length, 2);
});

test("verification fails instead of accepting a different connected connection", () => {
  const data = fixture();
  data.connections.teams.statuses = ["Error"];
  const result = runConsent({ args: ["verify", "teams"], data });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /'teams' is 'Error', not Connected/);
  assert.ok(result.calls.every(call => !call.includes("teams-1")));
});

test("missing or unreadable workflow bindings fail explicitly", () => {
  const data = fixture();
  data.workflows["qa-workflow"] = null;
  const missing = runConsent({ data });
  assert.notEqual(missing.status, 0);
  assert.match(missing.stderr, /qa-workflow.*no valid Teams connection binding/);
  const denied = runConsent({ data: { ...fixture(), fail: "qa-workflow" } });
  assert.notEqual(denied.status, 0);
  assert.match(denied.stderr, /AuthorizationFailed/);
});

test("does not validate an optional workflow when its Teams routing is disabled", () => {
  const data = fixture();
  delete data.workflows["mcp-workflow"];
  const result = runConsent({
    data, env: { AZURE_MCP_TEAMS_GROUP_ID: "", AZURE_MCP_TEAMS_CHANNEL_IDS: "" },
  });
  assert.equal(result.status, 0, result.stderr);
  assert.ok(result.calls.every(call => !call.includes("mcp-workflow")));
});

test("requires an explicit selection rather than enumerating arbitrary connections", () => {
  const result = runConsent({
    env: {
      TEAMS_CONNECTION_NAME: "",
      LOGIC_APP_WORKFLOW_NAME: "",
      AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME: "",
      AZURE_MCP_TEAMS_GROUP_ID: "",
      AZURE_MCP_TEAMS_CHANNEL_IDS: "",
    },
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Specify a connection name/);
  assert.deepEqual(result.calls, []);
});

test("supports an explicitly named standalone check without workflow discovery", () => {
  const result = runConsent({
    args: ["check", "teams"],
    env: {
      LOGIC_APP_WORKFLOW_NAME: "",
      AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME: "",
      AZURE_MCP_TEAMS_GROUP_ID: "",
      AZURE_MCP_TEAMS_CHANNEL_IDS: "",
    },
  });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).connectionName, "teams");
  assert.equal(result.calls.length, 2);
});

test("an enabled MCP route requires its workflow name for binding verification", () => {
  const result = runConsent({ env: { AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME: "" } });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /enabled Azure MCP Server workflow name is required/);
  assert.deepEqual(result.calls, []);
});

test("missing connection status and invalid polling parameters are errors", () => {
  const data = fixture();
  data.connections.teams.statuses = [];
  const missing = runConsent({ data });
  assert.notEqual(missing.status, 0);
  assert.match(missing.stderr, /'teams' has no status/);
  const invalid = runConsent({ args: ["verify"], env: { CONSENT_VERIFY_ATTEMPTS: "0" } });
  assert.notEqual(invalid.status, 0);
  assert.match(invalid.stderr, /attempts must be positive/);
});

test("pipeline check and verify preserve the selected connection name", () => {
  const template = parse(readFileSync(resolve(root, "pipelines/templates/logic-app-consent-jobs.yml"), "utf8"));
  const prepare = template.jobs.find(job => job.job === "CheckTeamsConsent")
    .steps.find(step => step.name === "CheckConsent");
  const verify = template.jobs.find(job => job.job === "VerifyTeamsConsent")
    .steps.find(step => step.displayName === "Verify Teams consent");

  assert.match(prepare.inputs.inlineScript, /check "\$\{TEAMS_CONNECTION_NAME:-\}"/);
  assert.match(prepare.inputs.inlineScript, /variable=connectionName;isOutput=true/);
  assert.equal(verify.env.TEAMS_CONNECTION_NAME, "$(teamsConnectionName)");
  assert.match(verify.inputs.inlineScript, /verify "\$TEAMS_CONNECTION_NAME"/);
});
