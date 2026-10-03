import assert from "node:assert/strict";
import test from "node:test";

import type { CommandRunner } from "../scripts/lib/cli.js";
import { syncEnvironment } from "../scripts/sync-env-suite.js";

test("syncs every derived value into an existing azd environment", () => {
  const calls: Array<{ command: string; args: readonly string[] }> = [];
  const run: CommandRunner = (command, args) => {
    calls.push({ command, args });
    return args[0] === "env" && args[1] === "list"
      ? JSON.stringify([{ Name: "dev" }])
      : "";
  };

  const values = syncEnvironment({ environment: "dev" }, run, () => {});
  const setCalls = calls.filter(({ args }) => args[0] === "env" && args[1] === "set");

  assert.equal(calls.some(({ args }) => args[1] === "new"), false);
  assert.equal(setCalls.length, Object.keys(values).length);
  assert.ok(setCalls.some(({ args }) =>
    args[2] === "AZD_IMAGE_TAG" && args[3] === "dev"));
  assert.ok(setCalls.some(({ args }) =>
    args[2] === "SERVER_APPLICATION_ID_URI" &&
    args[3] === values.SERVER_APPLICATION_ID_URI));
});

test("creates a missing azd environment before setting values", () => {
  const calls: string[][] = [];
  const run: CommandRunner = (_command, args) => {
    calls.push([...args]);
    return args[0] === "env" && args[1] === "list" ? "[]" : "";
  };

  syncEnvironment({ environment: "dev" }, run, () => {});

  assert.ok(calls.some((args) => args.join(" ") === "env new dev --no-prompt"));
});