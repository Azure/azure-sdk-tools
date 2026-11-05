import assert from "node:assert/strict";
import test from "node:test";

import { agentVersionMetadata } from "../hooks/lib/agent-version-metadata.js";

test("tags dev agent versions with the complete pipeline source branch", () => {
  const metadata = agentVersionMetadata({
    ENABLE_AI_FOUNDRY_AGENT_ROUTING: "true",
    BUILD_SOURCEBRANCH: "refs/heads/feature/qa-bot-routing",
  }, { existingTag: "retained", branchname: "old-branch" });
  assert.deepEqual(metadata, {
    existingTag: "retained",
    enableVnextExperience: "true",
    branchname: "feature/qa-bot-routing",
  });
});

test("resolves the checked-out branch for local dev deployments", () => {
  const metadata = agentVersionMetadata({
    ENABLE_AI_FOUNDRY_AGENT_ROUTING: "true",
  }, {}, (command, args) => {
    assert.equal(command, "git");
    assert.deepEqual(args, ["symbolic-ref", "--short", "HEAD"]);
    return "local-dev";
  });
  assert.equal(metadata.branchname, "local-dev");
});

test("does not copy a dev branch tag into routing-disabled agent versions", () => {
  const existing = { branchname: "dev-branch", other: "retained" };
  const metadata = agentVersionMetadata({
    ENABLE_AI_FOUNDRY_AGENT_ROUTING: "false",
  }, existing, () => { throw new Error("Must not query Git when routing is disabled."); });
  assert.deepEqual(metadata, { other: "retained", enableVnextExperience: "true" });
  assert.equal(existing.branchname, "dev-branch");
});

test("rejects invalid routing flags and missing or unreadable local branches", () => {
  assert.throws(() => agentVersionMetadata({
    ENABLE_AI_FOUNDRY_AGENT_ROUTING: "enabled",
  }), /must be true or false/);
  assert.throws(() => agentVersionMetadata({
    ENABLE_AI_FOUNDRY_AGENT_ROUTING: "true",
    BUILD_SOURCEBRANCH: "refs/heads/",
  }), /source branch is required/);
  assert.throws(() => agentVersionMetadata({
    ENABLE_AI_FOUNDRY_AGENT_ROUTING: "true",
  }, {}, () => { throw new Error("Detached HEAD"); }), /Detached HEAD/);
});
