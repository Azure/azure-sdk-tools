import assert from "node:assert/strict";
import test from "node:test";

import { PROVISION_LAYERS, detectDrift } from "../scripts/detect-drift.js";
import type { CommandRunner } from "../scripts/lib/cli.js";

test("previews every layer in dependency order", () => {
  const calls: Array<{ command: string; args: readonly string[]; previewMode?: string }> = [];
  const run: CommandRunner = (command, args, options) => {
    calls.push({
      command,
      args,
      previewMode: options?.env?.AZD_PROVISION_PREVIEW,
    });
    if (args[0] === "env" && args[1] === "list") {
      return JSON.stringify([{ Name: "dev" }]);
    }
    if (command === "node") return "";
    if (args[0] === "provision") return "{}";
    return "";
  };

  detectDrift({ environment: "dev" }, run, () => {});

  const previewLayers = calls
    .filter(({ args }) => args[0] === "provision")
    .map(({ args }) => args[1]);
  assert.deepEqual(previewLayers, [...PROVISION_LAYERS]);
  assert.ok(
    calls
      .filter(({ args }) => args[0] === "provision")
      .every(({ previewMode }) => previewMode === "true"),
  );
});

test("fails with layer-qualified risky operations", () => {
  const run: CommandRunner = (command, args) => {
    if (args[0] === "env" && args[1] === "list") {
      return JSON.stringify([{ Name: "dev" }]);
    }
    if (command === "node" && String(args[1]).endsWith("frontend.json")) {
      return "Modify: Microsoft.Web/sites/frontend";
    }
    if (command === "node") return "";
    if (args[0] === "provision") return "{}";
    return "";
  };

  assert.throws(
    () => detectDrift({ environment: "dev" }, run, () => {}),
    /frontend: Modify: Microsoft\.Web\/sites\/frontend/,
  );
});