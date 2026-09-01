import assert from "node:assert/strict";
import test from "node:test";

import {
  buildSearchResourcePlan,
  reconcileSearchResources,
} from "../hooks/lib/setup-search.js";

const options = {
  subscriptionId: "00000000-0000-0000-0000-000000000000",
  resourceGroup: "test-rg",
  searchServiceName: "test-search",
  storageAccountName: "teststorage",
  aiResourceName: "test-ai",
  env: {},
};

test("builds the production-equivalent Search resources in dependency order", () => {
  const plan = buildSearchResourcePlan(options);

  assert.deepEqual(
    plan.map(({ collection, name }) => `${collection}/${name}`),
    [
      "synonymmaps/pr-approval-synonyms",
      "indexes/azure-sdk-knowledge",
      "datasources/azuresdkqabot-search-datasource",
      "datasources/azure-sdk-knowledge-wiki-datasource",
      "skillsets/azure-sdk-knowledge-skillset",
      "skillsets/azure-sdk-knowledge-wiki-skillset",
      "indexers/azure-sdk-knowledge-indexer",
      "indexers/azure-sdk-knowledge-wiki-indexer",
      "knowledgesources/azure-sdk-knowledge-source",
      "knowledgebases/azure-sdk-knowledgebase",
    ],
  );

  const serialized = JSON.stringify(plan);
  assert.match(serialized, /text-embedding-3-small/);
  assert.doesNotMatch(serialized, /text-embedding-ada-002/);
  assert.doesNotMatch(serialized, /"apiKey"/);
  assert.match(serialized, /https:\/\/test-ai\.openai\.azure\.com/);
  assert.match(
    serialized,
    /ResourceId=\/subscriptions\/00000000-0000-0000-0000-000000000000\/resourceGroups\/test-rg\/providers\/Microsoft\.Storage\/storageAccounts\/teststorage;/,
  );
});

test("dry-run reads every desired resource and never writes", async () => {
  const methods: string[] = [];
  const fetchImpl: typeof fetch = async (_input, init) => {
    methods.push(init?.method ?? "GET");
    return new Response(null, { status: 404 });
  };

  const results = await reconcileSearchResources({
    ...options,
    dryRun: true,
    searchAdminKey: "test-key",
    fetchImpl,
    log: () => undefined,
  });

  assert.equal(results.length, 10);
  assert.ok(results.every(({ action }) => action === "create"));
  assert.deepEqual(methods, Array(10).fill("GET"));
});

test("does not write resources that already match", async () => {
  const plan = buildSearchResourcePlan(options);
  const resourcesByPath = new Map(
    plan.map((resource) => [
      `/${resource.collection}/${encodeURIComponent(resource.name)}`,
      resource.body,
    ]),
  );
  const methods: string[] = [];
  const fetchImpl: typeof fetch = async (input, init) => {
    methods.push(init?.method ?? "GET");
    const resource = resourcesByPath.get(new URL(String(input)).pathname);
    return resource
      ? Response.json(resource)
      : new Response(null, { status: 404 });
  };

  const results = await reconcileSearchResources({
    ...options,
    dryRun: false,
    searchAdminKey: "test-key",
    fetchImpl,
    log: () => undefined,
  });

  assert.ok(results.every(({ action }) => action === "unchanged"));
  assert.deepEqual(methods, Array(10).fill("GET"));
});

test("apply creates missing resources in dependency order", async () => {
  const methods: string[] = [];
  const fetchImpl: typeof fetch = async (_input, init) => {
    const method = init?.method ?? "GET";
    methods.push(method);
    if (method === "GET") return new Response(null, { status: 404 });
    return Response.json(JSON.parse(String(init?.body)));
  };

  const results = await reconcileSearchResources({
    ...options,
    dryRun: false,
    searchAdminKey: "test-key",
    fetchImpl,
    log: () => undefined,
  });

  assert.ok(results.every(({ action }) => action === "create"));
  assert.deepEqual(methods, Array(10).fill(["GET", "PUT"]).flat());
});