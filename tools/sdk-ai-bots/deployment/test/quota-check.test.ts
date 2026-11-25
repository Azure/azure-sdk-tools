import assert from "node:assert/strict";
import test from "node:test";

import {
  type QuotaCheckOptions,
  runQuotaCheck,
} from "../hooks/lib/quota-check.js";
import { checkQuotas } from "../scripts/check-quotas.js";
import type { CommandRunner } from "../scripts/lib/cli.js";

const env: NodeJS.ProcessEnv = {
  AZURE_RESOURCE_GROUP: "test-rg",
  AZURE_AI_LOCATION: "swedencentral",
  STORAGE_ACCOUNT_NAME_OVERRIDE: "teststorage",
  SEARCH_SERVICE_NAME_OVERRIDE: "test-search",
  AI_RESOURCE_NAME_OVERRIDE: "test-ai",
  GPT_4_1_SKU_NAME: "Standard",
  GPT_4_1_CAPACITY: "400",
  GPT_5_6_SOL_CAPACITY: "500",
  GPT_5_1_CAPACITY: "2000",
  GPT_5_MINI_CAPACITY: "150",
  TEXT_EMBEDDING_3_SMALL_CAPACITY: "120",
};
const options: QuotaCheckOptions = { subscriptionId: "test-sub", location: "westus2", env };
const modelCapacities = [
  ["gpt-4.1", "Standard", 400],
  ["gpt-5.6-sol", "GlobalStandard", 500],
  ["gpt-5.1", "GlobalStandard", 2000],
  ["gpt-5-mini", "GlobalStandard", 150],
  ["text-embedding-3-small", "GlobalStandard", 120],
] as const;

function deployment(name: string, sku: string, amount: number) {
  return { name, sku: { name: sku, capacity: amount }, properties: { model: { name } } };
}

function usage(name: string, currentValue: number, limit: number) {
  return { name: { value: name, localizedValue: name }, currentValue, limit };
}

function fixture() {
  const state = {
    storage: { location: "West US 2" } as object | null,
    search: { location: "westus2", sku: { name: "standard" } } as object | null,
    account: { location: "swedencentral", kind: "AIServices", sku: { name: "S0" } } as object | null,
    deployments: modelCapacities.map(([name, sku, amount]) => deployment(name, sku, amount)),
    usages: new Map<string, object[]>([
      ["Microsoft.Storage/westus2", [usage("StorageAccounts", 0, 250)]],
      ["Microsoft.Search/westus2", [usage("standard", 0, 16)]],
      ["Microsoft.CognitiveServices/swedencentral", [
        usage("AIServices.S0.AccountCount", 0, 64),
        ...modelCapacities.map(([name, sku]) => usage(`OpenAI.${sku}.${name}`, 0, 10000)),
      ]],
      ["Microsoft.CognitiveServices/westus2", [
        usage("OpenAI.GlobalStandard.gpt-5.6-sol", 1000, 250),
      ]],
    ]),
    inventory: [] as object[],
  };
  const calls: URL[] = [];
  const run: CommandRunner = (command, args) => {
    assert.equal(command, "az");
    assert.equal(args[args.indexOf("--method") + 1], "GET");
    const url = new URL(args[args.indexOf("--url") + 1]);
    calls.push(url);
    let response: unknown;
    if (url.pathname.endsWith("/usages")) {
      const match = /\/providers\/([^/]+)\/locations\/([^/]+)\/usages$/.exec(url.pathname);
      assert.ok(match);
      const values = state.usages.get(`${match[1]}/${match[2]}`);
      assert.ok(values, `Unexpected quota region: ${url}`);
      response = { value: values };
    } else if (url.pathname.endsWith("/deployments")) {
      response = { value: state.deployments };
    } else if (url.pathname.endsWith("/resources")) {
      response = { value: state.inventory };
    } else if (url.pathname.includes("/Microsoft.Storage/storageAccounts/")) {
      response = state.storage;
    } else if (url.pathname.includes("/Microsoft.Search/searchServices/")) {
      response = state.search;
    } else if (url.pathname.includes("/Microsoft.CognitiveServices/accounts/")) {
      response = state.account;
    } else {
      assert.fail(`Unexpected ARM request: ${url}`);
    }
    if (response === null) throw new Error("(ResourceNotFound) Target does not exist.");
    return JSON.stringify(response);
  };
  return { state, calls, run };
}

test("unchanged existing allocations pass without asking for free quota", () => {
  const azure = fixture();
  const result = runQuotaCheck(options, azure.run);

  assert.equal(result.ok, true);
  assert.deepEqual(result.exhausted, []);
  assert.equal(azure.calls.filter((url) => url.pathname.endsWith("/usages")).length, 0);
});

test("model growth uses the AI region and only the incremental capacity", () => {
  const azure = fixture();
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("OpenAI.GlobalStandard.gpt-5.6-sol", 1000, 1100),
  ]);
  const result = runQuotaCheck({
    ...options, env: { ...env, GPT_5_6_SOL_CAPACITY: "600" },
  }, azure.run);

  assert.equal(result.ok, true);
  assert.match(result.warnings.join("\n"), /additional allocation 100/);
  assert.deepEqual(
    azure.calls.filter((url) => url.pathname.endsWith("/usages")).map((url) => url.pathname),
    ["/subscriptions/test-sub/providers/Microsoft.CognitiveServices/locations/swedencentral/usages"],
  );
});

test("growth fails when the available quota is smaller than the required delta", () => {
  const azure = fixture();
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("OpenAI.GlobalStandard.gpt-5.6-sol", 1000, 1099),
  ]);
  const result = runQuotaCheck({
    ...options, env: { ...env, GPT_5_6_SOL_CAPACITY: "600" },
  }, azure.run);

  assert.equal(result.ok, false);
  assert.equal(result.exhausted[0].required, 100);
  assert.equal(result.exhausted[0].region, "swedencentral");
  assert.match(result.message, /additional allocation 100; available 99/);
});

test("capacity reductions do not require free quota", () => {
  const azure = fixture();
  const result = runQuotaCheck({
    ...options, env: { ...env, GPT_5_6_SOL_CAPACITY: "400" },
  }, azure.run);

  assert.equal(result.ok, true);
  assert.equal(azure.calls.some((url) => url.pathname.endsWith("/usages")), false);
});

test("a new model requires its full allocation and a zero limit is not unlimited", () => {
  const azure = fixture();
  azure.state.deployments = azure.state.deployments.filter((item) => item.name !== "gpt-5.6-sol");
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("OpenAI.GlobalStandard.gpt-5.6-sol", 0, 0),
  ]);
  const result = runQuotaCheck(options, azure.run);

  assert.equal(result.ok, false);
  assert.equal(result.exhausted[0].required, 500);
  assert.equal(result.exhausted[0].limit, 0);
});

test("changing a model SKU does not reuse allocation from a different quota", () => {
  const azure = fixture();
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("OpenAI.GlobalStandard.gpt-4.1", 0, 399),
  ]);
  const result = runQuotaCheck({
    ...options, env: { ...env, GPT_4_1_SKU_NAME: "GlobalStandard" },
  }, azure.run);

  assert.equal(result.ok, false);
  assert.equal(result.exhausted[0].nameValue, "OpenAI.GlobalStandard.gpt-4.1");
  assert.equal(result.exhausted[0].required, 400);
});

test("other deployment names do not provide credit for a new model deployment", () => {
  const azure = fixture();
  const existing = azure.state.deployments.find((item) => item.name === "gpt-5.6-sol");
  assert.ok(existing);
  existing.name = "another-deployment";
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("OpenAI.GlobalStandard.gpt-5.6-sol", 0, 499),
  ]);

  assert.equal(runQuotaCheck(options, azure.run).exhausted[0].required, 500);
});

test("new resources check account counts and model capacities in the appropriate regions", () => {
  const azure = fixture();
  azure.state.storage = null;
  azure.state.search = null;
  azure.state.account = null;
  azure.state.usages.set("Microsoft.Storage/westus2", [usage("StorageAccounts", 250, 250)]);
  azure.state.usages.set("Microsoft.Search/westus2", [usage("standard", 16, 16)]);
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("AIServices.S0.AccountCount", 64, 64),
    ...modelCapacities.map(([name, sku, amount]) => usage(`OpenAI.${sku}.${name}`, 0, amount)),
  ]);
  const result = runQuotaCheck(options, azure.run);

  assert.equal(result.ok, false);
  assert.deepEqual(result.exhausted.map((item) => [item.provider, item.region, item.required]), [
    ["Microsoft.Storage", "westus2", 1],
    ["Microsoft.Search", "westus2", 1],
    ["Microsoft.CognitiveServices", "swedencentral", 1],
  ]);
  assert.equal(azure.calls.some((url) => url.pathname.endsWith("/deployments")), false);
  assert.equal(azure.calls.filter((url) => url.pathname.endsWith("/usages")).length, 3);
});

test("allocations in a different region cannot satisfy the target region", () => {
  const azure = fixture();
  azure.state.account = { location: "westus2", kind: "AIServices", sku: { name: "S0" } };
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("AIServices.S0.AccountCount", 0, 64),
    ...modelCapacities.map(([name, sku, amount]) =>
      usage(`OpenAI.${sku}.${name}`, 0, name === "gpt-5.6-sol" ? amount - 1 : amount)),
  ]);
  const result = runQuotaCheck(options, azure.run);

  assert.equal(result.exhausted[0].required, 500);
  assert.equal(azure.calls.some((url) => url.pathname.endsWith("/deployments")), false);
});

test("resource absence is distinguished from permission and query failures", () => {
  const azure = fixture();
  const notFound: CommandRunner = (command, args, settings) => {
    if (args[args.indexOf("--url") + 1].includes("/storageAccounts/")) {
      throw new Error("(ResourceNotFound) Not found");
    }
    return azure.run(command, args, settings);
  };
  azure.state.usages.set("Microsoft.Storage/westus2", [usage("StorageAccounts", 250, 250)]);
  assert.equal(runQuotaCheck(options, notFound).exhausted[0].required, 1);

  assert.throws(() => runQuotaCheck(options, () => {
    throw new Error("AuthorizationFailed: HTTP 403");
  }), /Cannot verify quota.*AuthorizationFailed/);
});

test("unavailable required quota information blocks instead of reporting success", () => {
  const azure = fixture();
  azure.state.deployments = [];
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", []);
  assert.throws(() => runQuotaCheck(options, azure.run), /did not return OpenAI.Standard.gpt-4.1/);

  const denied: CommandRunner = (command, args, settings) => {
    if (args[args.indexOf("--url") + 1].includes("/usages?")) throw new Error("HTTP 403");
    return azure.run(command, args, settings);
  };
  assert.throws(() => runQuotaCheck(options, denied), /Cannot verify quota.*HTTP 403/);
});

test("invalid JSON, usage limits, and model capacities fail explicitly", () => {
  const azure = fixture();
  assert.throws(() => runQuotaCheck(options, () => "not-json"), /returned invalid JSON/);
  azure.state.deployments = [];
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    { name: { value: "OpenAI.Standard.gpt-4.1" }, currentValue: 0 },
  ]);
  assert.throws(() => runQuotaCheck(options, azure.run), /quota limit must be a non-negative number/);
  assert.throws(() => runQuotaCheck({
    ...options, env: { ...env, GPT_5_6_SOL_CAPACITY: "-1" },
  }, fixture().run), /GPT_5_6_SOL_CAPACITY must be a non-negative integer/);
});

test("unreadable resource metadata blocks instead of treating an allocation as reusable", () => {
  const azure = fixture();
  azure.state.search = { location: "westus2", sku: {} };
  assert.throws(() => runQuotaCheck(options, azure.run), /has no SKU name/);
  assert.throws(() => runQuotaCheck(options, () => "null"), /returned invalid JSON/);
  azure.state.search = { location: "westus2", sku: { name: "standard" } };
  azure.state.account = { location: "swedencentral", sku: { name: "S0" } };
  assert.throws(() => runQuotaCheck(options, azure.run), /has no account kind/);
});

test("changing the Search SKU requires quota for the target SKU", () => {
  const azure = fixture();
  azure.state.search = { location: "westus2", sku: { name: "basic" } };
  azure.state.usages.set("Microsoft.Search/westus2", [usage("standard", 16, 16)]);

  assert.equal(runQuotaCheck(options, azure.run).exhausted[0].required, 1);
});

test("unresolved resource names are allowed only when no existing allocation is ambiguous", () => {
  const azure = fixture();
  const unnamed = {
    ...options, env: { ...env, STORAGE_ACCOUNT_NAME_OVERRIDE: undefined },
  };
  assert.equal(runQuotaCheck(unnamed, azure.run).ok, true);
  azure.state.inventory = [{ type: "Microsoft.Storage/storageAccounts", name: "generated-storage" }];
  assert.throws(() => runQuotaCheck(unnamed, azure.run), /STORAGE_ACCOUNT_NAME_OVERRIDE is unresolved/);
});

test("model inventory follows pagination before calculating additional allocations", () => {
  const azure = fixture();
  const nextLink = "https://management.azure.com/model-page-two";
  const paged: CommandRunner = (command, args, settings) => {
    const url = args[args.indexOf("--url") + 1];
    if (url === nextLink) return JSON.stringify({ value: azure.state.deployments });
    if (new URL(url).pathname.endsWith("/deployments")) {
      return JSON.stringify({ value: [], nextLink });
    }
    return azure.run(command, args, settings);
  };

  assert.equal(runQuotaCheck(options, paged).ok, true);
  assert.equal(azure.calls.some((url) => url.pathname.endsWith("/usages")), false);
});

test("the CLI uses selected-environment configuration and process overrides consistently", () => {
  const azure = fixture();
  azure.state.usages.set("Microsoft.CognitiveServices/swedencentral", [
    usage("OpenAI.GlobalStandard.gpt-5.6-sol", 1000, 1049),
  ]);
  const run: CommandRunner = (command, args, settings) => {
    if (command === "azd") {
      assert.deepEqual(args, ["env", "get-values", "--environment", "dev"]);
      return 'AZURE_ENV_NAME="dev"\nGPT_5_6_SOL_CAPACITY="600"';
    }
    return azure.run(command, args, settings);
  };
  const result = checkQuotas(
    ["--env", "dev", "--subscription", "test-sub", "--location", "westus2"],
    { ...env, GPT_5_6_SOL_CAPACITY: "550" },
    run,
    () => {},
  );
  assert.ok(result);
  assert.equal(result.ok, false);
  assert.equal(result.exhausted[0].required, 50);
  assert.equal(result.exhausted[0].region, "swedencentral");
});

test("the CLI rejects conflicting environments and supports help without Azure queries", () => {
  assert.throws(() => checkQuotas(
    ["--env", "prod"], { AZURE_ENV_NAME: "dev" }, () => assert.fail("Must not query Azure"),
  ), /--env conflicts with AZURE_ENV_NAME/);
  const lines: string[] = [];
  assert.equal(checkQuotas(["--help"], {}, () => assert.fail("Must not query Azure"), (line) => lines.push(line)), undefined);
  assert.match(lines.join("\n"), /additional allocations/);
});
