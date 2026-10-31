import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const fakeAz = String.raw`#!/usr/bin/env node
const fs = require("node:fs");
const args = process.argv.slice(2);
fs.appendFileSync(process.env.AZURE_CALL_LOG, JSON.stringify(args) + "\n");
if (args[0] === "role" && args[2] === "list") {
  if (process.env.DENY_ROLE_LOOKUP === "1") {
    console.error("AuthorizationFailed: role lookup");
    process.exit(1);
  }
  console.log(process.env.EXISTING_ROLES === "1" ? "inherited-assignment-id" : "");
} else if (args[0] === "role" && args[2] === "create") {
  console.log("");
} else if (args[0] === "appconfig") {
  const key = args[args.indexOf("--key") + 1];
  const values = {
    STORAGE_ACCOUNT_RESOURCE_ID: "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/storage",
    AI_SEARCH_SERVICE_NAME: "search",
    AZURE_OPENAI_ENDPOINT: "https://ai.openai.azure.com"
  };
  if (!values[key]) throw new Error("Unexpected setting: " + key);
  console.log(values[key]);
} else {
  throw new Error("Unexpected Azure operation: " + args.join(" "));
}
`;

function runGrant(extraEnv) {
  const temporary = mkdtempSync(join(tmpdir(), "agent-role-test-"));
  const log = join(temporary, "calls.jsonl");
  writeFileSync(join(temporary, "az"), fakeAz, { mode: 0o700 });
  writeFileSync(log, "");
  try {
    const result = spawnSync("bash", [resolve(root, "scripts/grant-agent-data-access.sh"), "candidate"], {
      encoding: "utf8",
      env: {
        PATH: `${temporary}:${process.env.PATH}`,
        AZURE_CALL_LOG: log,
        AGENT_PRINCIPAL_ID: "agent",
        AZURE_SUBSCRIPTION_ID: "sub",
        AZURE_RESOURCE_GROUP: "rg",
        APP_CONFIG_NAME: "config",
        ...extraEnv,
      },
    });
    if (result.error) throw result.error;
    const calls = readFileSync(log, "utf8").trim();
    return { ...result, calls: calls ? calls.split("\n").map(line => JSON.parse(line)) : [] };
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

test("hosted-agent grants skip every existing candidate-role assignment", () => {
  const result = runGrant({ EXISTING_ROLES: "1" });
  assert.equal(result.status, 0, result.stderr);
  const lookups = result.calls.filter(call => call[0] === "role" && call[2] === "list");
  assert.equal(lookups.length, 6);
  assert.ok(lookups.every(call => call.includes("--include-inherited")));
  assert.ok(result.calls.every(call => call[2] !== "create"));
});

test("hosted-agent grants create only missing roles after checking existing assignments", () => {
  const result = runGrant({ EXISTING_ROLES: "0" });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.calls.filter(call => call[0] === "role" && call[2] === "create").length, 6);
  assert.equal(result.calls.filter(call => call[0] === "role" && call[2] === "list").length, 6);
});

test("hosted-agent role lookup failure stops rather than attempting unverified creation", () => {
  const result = runGrant({ DENY_ROLE_LOOKUP: "1" });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /AuthorizationFailed/);
  assert.ok(result.calls.every(call => call[2] !== "create"));
});
