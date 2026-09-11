import assert from "node:assert/strict";
import test from "node:test";

import type { CommandRunner } from "../scripts/lib/cli.js";
import { resolveSmokeTarget, runSmokeTest } from "../scripts/smoke-test.js";

test("resolves smoke targets from the environment suite", () => {
  assert.deepEqual(
    resolveSmokeTarget({ component: "frontend", environment: "dev" }),
    {
      appName: "azsdkqabot-dev-20260826",
      resourceGroup: "azure-sdk-qa-bot-dev-20260826",
      healthPath: "/health",
      slot: "default",
    },
  );
});

test("explicit smoke target values override the suite", () => {
  assert.deepEqual(
    resolveSmokeTarget({
      component: "function-app",
      environment: "dev",
      appName: "custom-app",
      resourceGroup: "custom-rg",
      healthPath: "/ready",
      slot: "staging",
    }),
    {
      appName: "custom-app",
      resourceGroup: "custom-rg",
      healthPath: "/ready",
      slot: "staging",
    },
  );
});

test("retries the endpoint with an Easy Auth token", async () => {
  const commandCalls: string[][] = [];
  const requests: Array<{ input: string; authorization?: string }> = [];
  const run: CommandRunner = (_command, args) => {
    commandCalls.push([...args]);
    return args[0] === "webapp" ? "app.example" : "token-value";
  };
  let attempt = 0;

  const url = await runSmokeTest(
    {
      component: "agent-server",
      environment: "dev",
      easyAuthAudience: "api://server",
      maxAttempts: 2,
      waitSeconds: 1,
    },
    {
      run,
      fetch: (async (input, init) => {
        requests.push({
          input: String(input),
          authorization: (init?.headers as Record<string, string>)?.Authorization,
        });
        attempt += 1;
        return new Response(null, { status: attempt === 1 ? 503 : 200 });
      }) as typeof fetch,
      delay: async () => {},
      log: () => {},
    },
  );

  assert.equal(url, "https://app.example/ping");
  assert.equal(requests.length, 2);
  assert.equal(requests[0].authorization, "Bearer token-value");
  assert.ok(commandCalls[0].includes("--resource-group"));
});