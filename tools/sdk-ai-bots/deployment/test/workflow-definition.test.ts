import assert from "node:assert/strict";
import test from "node:test";

import { hasWorkflowActions } from "../hooks/lib/workflow-definition.js";

test("does not treat an empty workflow shell as a deployed definition", () => {
  assert.equal(hasWorkflowActions(null), false);
  assert.equal(hasWorkflowActions({}), false);
});

test("detects a deployed workflow definition", () => {
  assert.equal(hasWorkflowActions({ ConvertActivity: { type: "Function" } }), true);
});