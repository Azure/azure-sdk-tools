import assert from "node:assert/strict";
import test from "node:test";

import { loadEnvironmentSuite } from "../hooks/lib/env-suite.js";
import { enforceLocalOperationAllowed, enforceProvisionGuard } from "../hooks/lib/provision-guard.js";

const suite = loadEnvironmentSuite();

test("allows local operations for dev", () => {
  assert.doesNotThrow(() =>
    enforceLocalOperationAllowed("deploy", { AZURE_ENV_NAME: "dev" }, suite));
});

test("rejects local operations when the environment disables them", () => {
  assert.throws(
    () => enforceLocalOperationAllowed("provision", { AZURE_ENV_NAME: "preview" }, suite),
    /Refusing to provision 'preview'/,
  );
  assert.throws(
    () => enforceLocalOperationAllowed("deploy", { AZURE_ENV_NAME: "prod" }, suite),
    /Refusing to deploy 'prod'/,
  );
});

test("allows pipeline operations for restricted environments", () => {
  assert.doesNotThrow(() =>
    enforceLocalOperationAllowed(
      "deploy",
      { AZURE_ENV_NAME: "prod", TF_BUILD: "True" },
      suite,
    ));
});

const groupEnv = {
  AZURE_ENV_NAME: "dev",
  AZURE_SUBSCRIPTION_ID: "test-sub",
  AZURE_RESOURCE_GROUP: "existing-rg",
};

test("provisioning validates the exact existing resource group using only a read", () => {
  const calls: Array<{ command: string; args: readonly string[] }> = [];
  enforceProvisionGuard(groupEnv, (command, args) => {
    calls.push({ command, args });
    return "/subscriptions/TEST-SUB/resourcegroups/EXISTING-RG";
  }, suite);
  assert.deepEqual(calls, [{
    command: "az",
    args: ["group", "show", "--subscription", "test-sub", "--name", "existing-rg", "--query", "id", "--output", "tsv"],
  }]);
});

test("missing or unreadable resource groups stop provisioning without a creation fallback", () => {
  for (const message of ["ResourceGroupNotFound", "AuthorizationFailed"]) {
    let calls = 0;
    assert.throws(() => enforceProvisionGuard(groupEnv, () => {
      calls++;
      throw new Error(message);
    }, suite), /must already exist and be readable/);
    assert.equal(calls, 1);
  }
});

test("a missing group selection or unexpected returned ID is an explicit error", () => {
  assert.throws(() => enforceProvisionGuard({}, () => assert.fail("No Azure call expected"), suite), /must select an existing resource group/);
  assert.throws(() => enforceProvisionGuard(groupEnv, () => "", suite), /Cannot verify the selected/);
  assert.throws(() => enforceProvisionGuard(groupEnv, () => "/subscriptions/other/resourceGroups/other", suite), /Cannot verify the selected/);
});

test("local-operation restrictions are checked before accessing the group", () => {
  assert.throws(() => enforceProvisionGuard({
    ...groupEnv, AZURE_ENV_NAME: "prod",
  }, () => assert.fail("No Azure call expected"), suite), /Refusing to provision/);
});