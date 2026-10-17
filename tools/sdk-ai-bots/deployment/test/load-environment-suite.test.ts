import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

import { loadEnvironmentSuite } from "../hooks/lib/env-suite.js";

const deploymentRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const suite = loadEnvironmentSuite();
const template = parse(
  readFileSync(
    resolve(deploymentRoot, "pipelines/templates/load-environment-suite.yml"),
    "utf8",
  ),
);

function runLoader(environment: string, overrides: NodeJS.ProcessEnv = {}) {
  const script: string = template.steps[0].bash;
  const result = spawnSync(
    "bash",
    [
      "-c",
      script
        .replaceAll("${{ parameters.environment }}", environment)
        .replaceAll(
          "${{ parameters.suitePath }}",
          resolve(deploymentRoot, "infra/environments/environment-suite.yaml"),
        )
        .replaceAll(
          "${{ parameters.projectPath }}",
          resolve(deploymentRoot, "../azure.yaml"),
        ),
    ],
    {
      encoding: "utf8",
      env: { PATH: process.env.PATH, HOME: process.env.HOME, ...overrides },
    },
  );
  if (result.error) throw result.error;

  const escapes: Record<string, string> = {
    "%AZP25": "%",
    "%0A": "\n",
    "%0D": "\r",
  };
  const values = Object.fromEntries(
    [...result.stdout.matchAll(/^##vso\[task\.setvariable variable=([A-Z_0-9]+)\](.*)$/gm)]
      .map((match) => [
        match[1],
        match[2].replace(/%AZP25|%0A|%0D/g, (escape) => escapes[escape]),
      ]),
  );
  return { ...result, values };
}

test("preserves every JSON-valued dev Bicep override through pipeline logging", () => {
  const result = runLoader("dev");
  assert.equal(result.status, 0, result.stderr || result.stdout);
  const overrides = suite.environments.dev.bicepOverrides;
  assert.ok(overrides);

  for (const [name, value] of Object.entries(overrides)) {
    if (value.startsWith("{") || value.startsWith("[")) {
      assert.equal(result.values[name], value, name);
      assert.deepEqual(JSON.parse(result.values[name]), JSON.parse(value), name);
    }
  }
});

test("preserves scalar configuration in production", () => {
  const result = runLoader("prod");
  assert.equal(result.status, 0, result.stderr || result.stdout);
  assert.equal(result.values.MANAGE_AUTHORIZATION_RESOURCES, "true");
  assert.equal(result.values.GPT_4_1_CAPACITY, "200");
});

test("preserves multiline JSON and literal logging escape sequences in pipeline overrides", () => {
  const overrides = suite.environments.dev.bicepOverrides;
  assert.ok(overrides);
  const identities = JSON.stringify(
    JSON.parse(overrides.AI_RESOURCE_USER_ASSIGNED_IDENTITIES),
    null,
    "\t",
  ).replaceAll("\n", "\r\n");
  const policy = "policy%0A%0D%AZP25";
  const result = runLoader("dev", {
    AI_RESOURCE_USER_ASSIGNED_IDENTITIES: identities,
    MODEL_RAI_POLICY_NAME: policy,
  });

  assert.equal(result.status, 0, result.stderr || result.stdout);
  assert.equal(result.values.AI_RESOURCE_USER_ASSIGNED_IDENTITIES, identities);
  assert.equal(result.values.MODEL_RAI_POLICY_NAME, policy);
  assert.deepEqual(JSON.parse(result.values.AI_RESOURCE_USER_ASSIGNED_IDENTITIES), JSON.parse(identities));
});

test("still rejects conflicting canonical and alias pipeline overrides", () => {
  const result = runLoader("dev", {
    AI_RESOURCE_NAME: "first-resource",
    AI_RESOURCE_NAME_OVERRIDE: "second-resource",
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stdout, /Conflicting pipeline overrides/);
});
