import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import test from "node:test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const fakeCli = String.raw`#!/usr/bin/env node
const fs = require("node:fs");
const args = process.argv.slice(2);
const command = require("node:path").basename(process.argv[1]);
fs.appendFileSync(process.env.AUDIT_CALLS, JSON.stringify([command, ...args]) + "\n");
if (command === "azd" && args[0] === "env") {
  if (args[1] !== "get-values" && args[1] !== "set") throw new Error("Unexpected azd operation");
  if (args[1] === "get-values" && process.env.AUDIT_MODE === "denied-env") {
    console.error("Cannot read selected azd environment");
    process.exit(1);
  }
} else if (command === "az" && args[0] === "account" && args[1] === "get-access-token") {
  console.log("fake-token");
} else if (command === "az" && args[0] === "role" && args[1] === "assignment") {
  if (args[2] === "list") console.log("[]");
  else if (args[2] !== "create") throw new Error("Unexpected role operation");
} else {
  throw new Error("Unexpected CLI call");
}
`;

function runHook(mode) {
  const temporary = mkdtempSync(join(tmpdir(), "qabot-agent-postdeploy-"));
  const calls = join(temporary, "calls.jsonl");
  const payload = join(temporary, "payload.json");
  writeFileSync(calls, "");
  for (const command of ["az", "azd"]) {
    writeFileSync(join(temporary, command), fakeCli, { mode: 0o700 });
  }
  const script = `
    import { writeFileSync } from "node:fs";
    globalThis.fetch = async (url, options = {}) => {
      if (options.method === "POST") {
        const body = JSON.parse(options.body);
        writeFileSync(process.env.AUDIT_PAYLOAD, JSON.stringify(body));
        return Response.json({
          version: "2", definition: body.definition,
          metadata: process.env.AUDIT_MODE === "lost-metadata" ? {} : body.metadata,
        });
      }
      if (url.includes("/versions?")) {
        if (process.env.AUDIT_MODE === "denied-list") return new Response("", { status: 403 });
        return Response.json({ data: [{
          version: "1",
          metadata: { branchname: process.env.AUDIT_MODE === "matching" ? "feature/new" : "old" },
          definition: {
            container_configuration: { image: "registry.azurecr.io/agent:reviewed" },
            environment_variables: { AZURE_APPCONFIG_ENDPOINT: "https://config.azconfig.io" },
          },
        }] });
      }
      if (/\\/versions\\/[12]\\?/.test(url)) {
        return Response.json({
          instance_identity: { principal_id: url.includes("/versions/2?") ? "new-principal" : "old-principal" },
        });
      }
      throw new Error("Unexpected fetch: " + url);
    };
    await import(${JSON.stringify(pathToFileURL(resolve(root, "hooks/agent-postdeploy.ts")).href)});
  `;
  try {
    const result = spawnSync(process.execPath, ["--import", "tsx", "--input-type=module", "--eval", script], {
      cwd: root,
      encoding: "utf8",
      env: {
        PATH: `${temporary}:${process.env.PATH}`,
        HOME: process.env.HOME,
        AUDIT_CALLS: calls, AUDIT_PAYLOAD: payload, AUDIT_MODE: mode,
        AZURE_ENV_NAME: "dev", AZURE_SUBSCRIPTION_ID: "sub", AZURE_RESOURCE_GROUP: "rg",
        APP_CONFIG_NAME: "config", AI_RESOURCE_NAME: "ai",
        STORAGE_ACCOUNT_NAME: "storage", SEARCH_SERVICE_NAME: "search",
        AZURE_APPCONFIG_ENDPOINT: "https://config.azconfig.io",
        FOUNDRY_PROJECT_ENDPOINT: "https://foundry.example/api/projects/test",
        AGENT_AGENT_NAME: "chat", AGENT_AGENT_VERSION: "1",
        AGENT_AGENT_ENDPOINT: "https://foundry.example/api/projects/test/agents/chat/versions/1",
        AZURE_CONTAINER_REGISTRY_ENDPOINT: "registry.azurecr.io",
        AGENT_IMAGE_REPOSITORY: "agent", AZD_IMAGE_TAG: "reviewed",
        ENABLE_AI_FOUNDRY_AGENT_ROUTING: "true", BUILD_SOURCEBRANCH: "refs/heads/feature/new",
      },
    });
    if (result.error) throw result.error;
    return {
      ...result,
      calls: readFileSync(calls, "utf8").trim().split("\n").filter(Boolean).map(line => JSON.parse(line)),
      payload: ["matching", "denied-list", "denied-env"].includes(mode) ? undefined : JSON.parse(readFileSync(payload, "utf8")),
    };
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

test("native postdeploy tags the selected version and reconciles its final runtime identity", () => {
  const result = runHook("changed");
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.payload.metadata.branchname, "feature/new");
  assert.equal(result.payload.definition.container_configuration.image, "registry.azurecr.io/agent:reviewed");
  assert.equal(result.calls.filter(call => call[0] === "azd" && call[2] === "get-values").length, 1);
  assert.ok(result.calls.some(call => call[0] === "azd" && call[2] === "set" &&
    call[3] === "AGENT_AGENT_VERSION" && call[4] === "2"));
  for (const principal of ["old-principal", "new-principal"]) {
    const grants = result.calls.filter(call => call[0] === "az" && call[3] === "create" &&
      call[call.indexOf("--assignee-object-id") + 1] === principal);
    assert.equal(grants.length, 7, principal);
  }
});

test("native postdeploy reuses an image with matching configuration and branch metadata", () => {
  const result = runHook("matching");
  assert.equal(result.status, 0, result.stderr);
  assert.ok(result.calls.every(call => !(call[0] === "azd" && call[2] === "set")));
});

test("native postdeploy rejects a version response that lost its branch metadata", () => {
  const result = runHook("lost-metadata");
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /source branch metadata was not persisted/);
});

test("native postdeploy fails when deployed versions cannot be inspected", () => {
  const result = runHook("denied-list");
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Failed to list hosted agent versions \(HTTP 403\)/);
});

test("native postdeploy stops before Azure calls when azd state cannot be read", () => {
  const result = runHook("denied-env");
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Cannot read selected azd environment/);
  assert.equal(result.calls.length, 1);
  assert.equal(result.calls[0][0], "azd");
});
