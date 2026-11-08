import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const requiredExtensions = [
  "azure.ai.agents",
  "azure.ai.inspector",
  "azure.ai.projects",
  "azure.ai.connections",
  "azure.ai.toolboxes",
];
const installed = requiredExtensions.map(id => ({ id, installedVersion: "1.0.0" }));
const fakeAzd = String.raw`#!/usr/bin/env node
const fs = require("node:fs");
const args = process.argv.slice(2);
fs.appendFileSync(process.env.CALL_LOG, JSON.stringify(args) + "\n");
if (args[0] === "extension" && args[1] === "install") {
  if (process.env.FAIL_INSTALL === "1") {
    console.error("dependency download failed: HTTP 504");
    process.exit(42);
  }
} else if (args[0] === "extension" && args[1] === "list") {
  console.log(process.env.INSTALLED_EXTENSIONS);
} else {
  throw new Error("Unexpected azd operation: " + args.join(" "));
}
`;
const fakeYq = String.raw`#!/usr/bin/env node
const args = process.argv.slice(2);
if (args[0] !== "-r" || args[1] !== '.requiredVersions.extensions."azure.ai.agents"' ||
    args[2] !== process.env.MANIFEST_PATH) throw new Error("Unexpected yq arguments");
console.log(process.env.EXTENSION_CONSTRAINT);
`;

function runInstaller(extraEnv = {}) {
  const temporary = mkdtempSync(join(tmpdir(), "azd-extensions-test-"));
  const log = join(temporary, "calls.jsonl");
  const manifest = join(temporary, "azure.yaml");
  writeFileSync(join(temporary, "azd"), fakeAzd, { mode: 0o700 });
  writeFileSync(join(temporary, "yq"), fakeYq, { mode: 0o700 });
  writeFileSync(log, "");
  try {
    const result = spawnSync("bash", [resolve(root, "scripts/install-azd-extensions.sh"), manifest], {
      encoding: "utf8",
      env: {
        PATH: `${temporary}:${process.env.PATH}`,
        CALL_LOG: log,
        MANIFEST_PATH: manifest,
        EXTENSION_CONSTRAINT: "=1.0.0",
        INSTALLED_EXTENSIONS: JSON.stringify(installed),
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

test("installs the exact GA pin and verifies all direct dependencies", () => {
  const result = runInstaller();
  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(result.calls, [
    ["extension", "install", "azure.ai.agents", "--source", "azd", "--version", "1.0.0", "--no-prompt"],
    ["extension", "list", "--installed", "--output", "json"],
  ]);
});

test("rejects non-exact extension constraints before installation", () => {
  const result = runInstaller({ EXTENSION_CONSTRAINT: ">=1.0.0" });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Expected an exact/);
  assert.deepEqual(result.calls, []);
});

test("installation failure stops before reporting dependency success", () => {
  const result = runInstaller({ FAIL_INSTALL: "1" });
  assert.equal(result.status, 42);
  assert.match(result.stderr, /HTTP 504/);
  assert.equal(result.calls.length, 1);
});

test("does not accept an older agents version after installation", () => {
  const result = runInstaller({
    INSTALLED_EXTENSIONS: JSON.stringify(installed.map(extension =>
      extension.id === "azure.ai.agents" ? { ...extension, installedVersion: "1.0.0-beta.13" } : extension)),
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /azure\.ai\.agents.*unexpected installed version/);
});

for (const id of ["azure.ai.connections", "azure.ai.toolboxes"]) {
  test(`fails when the new GA dependency ${id} is missing`, () => {
    const result = runInstaller({
      INSTALLED_EXTENSIONS: JSON.stringify(installed.filter(extension => extension.id !== id)),
    });
    assert.notEqual(result.status, 0);
    assert.ok(result.stderr.includes(id));
  });
}

test("does not treat a missing installedVersion as an installed dependency", () => {
  const result = runInstaller({
    INSTALLED_EXTENSIONS: JSON.stringify(installed.map(extension =>
      extension.id === "azure.ai.connections" ? { id: extension.id } : extension)),
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /azure\.ai\.connections.*missing/);
});
