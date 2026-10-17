import assert from "node:assert/strict";
import test from "node:test";

import { loadEnvironmentSuite } from "../hooks/lib/env-suite.js";
import { enforceLocalOperationAllowed } from "../hooks/lib/provision-guard.js";

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