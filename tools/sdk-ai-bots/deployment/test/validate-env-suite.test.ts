import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { loadEnvironmentSuite } from "../hooks/lib/env-suite.js";
import { collectEnvironmentSuiteErrors } from "../scripts/validate-env-suite.js";

const configRoot = resolve(dirname(fileURLToPath(import.meta.url)), "../config");

test("validates the checked-in dev environment", () => {
  const errors = collectEnvironmentSuiteErrors({
    suite: loadEnvironmentSuite(),
    environments: ["dev"],
    configRoot,
  });

  assert.deepEqual(errors, []);
});

test("reports malformed identities, placeholders, and route drift", () => {
  const suite = structuredClone(loadEnvironmentSuite());
  suite.environments.dev.subscriptionId = "not-a-guid";
  suite.environments.dev.serverApplicationClientId = "REPLACE_WITH_CLIENT_ID";
  suite.environments.dev.localDeployAllowed = "false" as unknown as boolean;
  suite.environments.dev.teamsChannelIds.push("unmapped-channel");
  suite.components.frontend.healthPath = "";

  const errors = collectEnvironmentSuiteErrors({
    suite,
    environments: ["dev"],
    configRoot,
  });

  assert.ok(errors.some((error) => error.includes("subscriptionId 'not-a-guid'")));
  assert.ok(errors.some((error) => error.includes("REPLACE_WITH_CLIENT_ID")));
  assert.ok(errors.some((error) => error.includes("localDeployAllowed")));
  assert.ok(errors.some((error) => error.includes("unmapped-channel")));
  assert.ok(errors.some((error) => error.includes("components.frontend")));
});

test("reports undefined tenants and Teams group mismatches", () => {
  const root = mkdtempSync(join(tmpdir(), "qabot-env-validation-"));
  const devRoot = join(root, "dev");
  const suite = structuredClone(loadEnvironmentSuite());
  const channelId = suite.environments.dev.teamsChannelIds[0];
  mkdirSync(devRoot, { recursive: true });
  writeFileSync(
    join(devRoot, "channel.yaml"),
    `default:\n  tenant: missing\nchannels:\n  - id: ${channelId}\n    tenant: missing\n`,
  );
  writeFileSync(
    join(devRoot, "tenant.yaml"),
    "tenants:\n  - tenant: defined\n    channel_link: https://teams.example/channel?groupId=wrong\n",
  );

  const errors = collectEnvironmentSuiteErrors({
    suite,
    environments: ["dev"],
    configRoot: root,
  });

  assert.ok(errors.some((error) => error.includes("route tenant 'missing'")));
  assert.ok(errors.some((error) => error.includes("different Teams group")));
});