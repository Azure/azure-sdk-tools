import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { parse } from "yaml";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const template = parse(readFileSync(resolve(root, "pipelines/templates/smoke-test.yml"), "utf8"));
const fakeCommand = String.raw`#!/usr/bin/env node
const fs = require("node:fs");
const name = require("node:path").basename(process.argv[1]);
const args = process.argv.slice(2);
const previous = fs.readFileSync(process.env.SMOKE_CALLS, "utf8").trim().split("\n").filter(Boolean).map(JSON.parse);
fs.appendFileSync(process.env.SMOKE_CALLS, JSON.stringify([name, ...args]) + "\n");
if (name === "az") {
  if (process.env.SMOKE_FAIL_AZ === "1") {
    console.error("AuthorizationFailed");
    process.exit(1);
  }
  if (args[0] === "webapp" && args[1] === "show") console.log("backend.example");
  else if (args[0] === "account" && args[1] === "get-access-token") console.log("fake token");
  else throw new Error("Unexpected Azure command");
} else if (name === "curl") {
  const attempt = previous.filter(call => call[0] === "curl").length;
  const status = process.env.SMOKE_STATUSES.split(",")[attempt];
  if (!status) throw new Error("Unexpected extra HTTP request");
  if (status === "network-error") {
    console.error("Connection failed");
    process.exit(7);
  }
  process.stdout.write(status);
} else throw new Error("Unexpected command");
`;

function runSmoke({ audience = "api://backend", statuses = "503,200", failAz = false } = {}) {
  const temporary = mkdtempSync(join(tmpdir(), "qabot-pipeline-smoke-"));
  const log = join(temporary, "calls.jsonl");
  writeFileSync(log, "");
  for (const command of ["az", "curl"]) {
    writeFileSync(join(temporary, command), fakeCommand, { mode: 0o700 });
  }
  const parameters = {
    appName: "backend", healthPath: "/ping", easyAuthAudience: audience,
    maxAttempts: 2, waitSeconds: 0,
  };
  const script = template.steps[0].inputs.inlineScript.replace(
    /\$\{\{ parameters\.(\w+) \}\}/g,
    (_match, name) => String(parameters[name]),
  );
  try {
    const result = spawnSync("bash", ["-c", script], {
      encoding: "utf8",
      env: {
        PATH: `${temporary}:${process.env.PATH}`,
        AZURE_RESOURCE_GROUP: "target-rg",
        SMOKE_CALLS: log, SMOKE_STATUSES: statuses, SMOKE_FAIL_AZ: failAz ? "1" : "0",
      },
    });
    if (result.error) throw result.error;
    return {
      ...result,
      calls: readFileSync(log, "utf8").trim().split("\n").filter(Boolean).map(line => JSON.parse(line)),
    };
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

test("pipeline readiness probes the main site and retries with one bearer-header argument", () => {
  const result = runSmoke();
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.calls[0].includes("--slot"), false);
  const requests = result.calls.filter(call => call[0] === "curl");
  assert.equal(requests.length, 2);
  assert.ok(requests.every(call => call[call.indexOf("--header") + 1] === "Authorization: Bearer fake token"));
  assert.ok(requests.every(call => call.at(-1) === "https://backend.example/ping"));
});

test("pipeline readiness fails after non-200 responses exhaust retries", () => {
  const result = runSmoke({ statuses: "403,403" });
  assert.notEqual(result.status, 0);
  assert.match(result.stdout, /Smoke test failed/);
  assert.equal(result.calls.filter(call => call[0] === "curl").length, 2);
});

test("pipeline readiness reports network failures and retries without authentication when not requested", () => {
  const result = runSmoke({ audience: "", statuses: "network-error,200" });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /HTTP probe failed/);
  assert.equal(result.calls.some(call => call.includes("get-access-token")), false);
  assert.ok(result.calls.filter(call => call[0] === "curl").every(call => !call.includes("--header")));
});

test("pipeline readiness stops before HTTP calls when Azure lookup is denied", () => {
  const result = runSmoke({ failAz: true });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /AuthorizationFailed/);
  assert.equal(result.calls.length, 1);
});
