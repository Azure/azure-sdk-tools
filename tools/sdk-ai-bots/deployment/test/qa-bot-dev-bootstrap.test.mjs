import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const subscription = "/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42";
const group = `${subscription}/resourceGroups/azure-sdk-qa-bot-dev`;
const principal = "f177a89e-2025-447c-ab31-a4b8dba3751a";
const fakeAz = String.raw`#!/usr/bin/env node
const fs = require("node:fs");
const args = process.argv.slice(2);
fs.appendFileSync(process.env.AZURE_CALL_LOG, JSON.stringify(args) + "\n");
if (args[0] === "role" && args[1] === "assignment") {
  if (args[2] === "list") {
    if (process.env.DENY_LOOKUP === "1") {
      console.error("AuthorizationFailed: cannot list assignments");
      process.exit(1);
    }
    console.log(process.env.EXISTING_ROLES === "1" ? "existing-assignment" : "");
  } else if (args[2] === "create") {
    console.log("");
  } else {
    throw new Error("Unexpected assignment operation");
  }
} else if (args[0] === "rest") {
  const method = args[args.indexOf("--method") + 1];
  const url = args[args.indexOf("--url") + 1];
  if (method !== "PATCH" || !url.startsWith("https://dev.azure.com/azure-sdk/internal/_apis/pipelines/pipelinepermissions/endpoint/")) {
    throw new Error("Unexpected REST operation");
  }
  console.log("");
} else {
  throw new Error("Unexpected command: " + args.join(" "));
}
`;

function runBootstrap(extra = {}) {
  const temporary = mkdtempSync(join(tmpdir(), "qabot-bootstrap-test-"));
  const log = join(temporary, "calls.jsonl");
  writeFileSync(join(temporary, "az"), fakeAz, { mode: 0o700 });
  writeFileSync(log, "");
  try {
    const result = spawnSync("bash", [resolve(root, "scripts/qa-bot-dev-bootstrap.sh")], {
      encoding: "utf8",
      env: { PATH: `${temporary}:${process.env.PATH}`, AZURE_CALL_LOG: log, ...extra },
    });
    if (result.error) throw result.error;
    const calls = readFileSync(log, "utf8").trim();
    return { ...result, calls: calls ? calls.split("\n").map(line => JSON.parse(line)) : [] };
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

test("bootstrap grants only the specified identity's missing authority at intended scopes", () => {
  const result = runBootstrap();
  assert.equal(result.status, 0, result.stderr);
  const assignments = result.calls.filter(call => call[0] === "role" && call[1] === "assignment" && call[2] === "create");
  assert.equal(assignments.length, 2);
  assert.ok(assignments.every(call => call[call.indexOf("--assignee-object-id") + 1] === principal));
  assert.deepEqual(assignments.map(call => [
    call[call.indexOf("--role") + 1], call[call.indexOf("--scope") + 1],
  ]), [
    ["18d7d88d-d35e-4fb5-a5c3-7773c20a72d9", group],
    ["acdd72a7-3385-48ef-bd42-f606fba81ae7", subscription],
  ]);
  assert.ok(result.calls.every(call => !(call[0] === "role" && call[1] === "definition")));
});

test("bootstrap reuses existing role assignments without granting subscription deployment access", () => {
  const result = runBootstrap({ EXISTING_ROLES: "1" });
  assert.equal(result.status, 0, result.stderr);
  assert.ok(result.calls.every(call => !(call[0] === "role" && call[2] === "create")));
});

test("bootstrap authorizes only pipeline 8265 without accessing backend configuration", () => {
  const result = runBootstrap();
  assert.equal(result.status, 0, result.stderr);
  const authorization = result.calls.find(call => call[0] === "rest" && call[call.indexOf("--method") + 1] === "PATCH");
  assert.ok(authorization);
  assert.match(authorization[authorization.indexOf("--url") + 1], /endpoint\/5e79584d-12de-4b1a-9637-7897b6d8b2cd\?/);
  assert.deepEqual(JSON.parse(authorization[authorization.indexOf("--body") + 1]), {
    pipelines: [{ id: 8265, authorized: true }],
  });
  assert.equal(result.calls.filter(call => call[0] === "rest").length, 1);
  assert.ok(result.calls.every(call =>
    (call[0] === "role" && call[1] === "assignment") || call === authorization,
  ));
});

test("bootstrap stops on lookup failure without attempting writes", () => {
  const result = runBootstrap({ DENY_LOOKUP: "1" });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /AuthorizationFailed/);
  assert.equal(result.calls.length, 1);
  assert.equal(result.calls[0][2], "list");
});
