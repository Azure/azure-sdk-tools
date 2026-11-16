import assert from "node:assert/strict";
import test from "node:test";

import {
  buildSearchResourcePlan,
  reconcileSearchResources,
} from "../hooks/lib/setup-search.js";
import { getSearchIndexerIdentityResourceId } from "../hooks/lib/search-identity.js";

const options = {
  subscriptionId: "00000000-0000-0000-0000-000000000000",
  resourceGroup: "test-rg",
  searchServiceName: "test-search",
  storageAccountName: "teststorage",
  aiResourceName: "test-ai",
  env: {},
};
const identityId = `/subscriptions/${options.subscriptionId}/resourceGroups/${options.resourceGroup}` +
  "/providers/Microsoft.ManagedIdentity/userAssignedIdentities/runtime";
const identityEnv = {
  MANAGED_IDENTITY_RESOURCE_ID: identityId,
  SEARCH_USER_ASSIGNED_IDENTITIES: JSON.stringify({ [identityId]: {} }),
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

test("configured shared Search identity is used by both new datasources", () => {
  const sources = buildSearchResourcePlan({ ...options, env: identityEnv })
    .filter(resource => resource.collection === "datasources");

  assert.equal(sources.length, 2);
  for (const source of sources) {
    assert.deepEqual(source.body.identity, {
      "@odata.type": "#Microsoft.Azure.Search.DataUserAssignedIdentity",
      userAssignedIdentity: identityId,
    });
  }
  assert.equal(getSearchIndexerIdentityResourceId({
    ...identityEnv,
    MANAGED_IDENTITY_RESOURCE_ID: identityId.toUpperCase(),
  }), identityId);
  assert.equal(getSearchIndexerIdentityResourceId({}), undefined);
  assert.equal(getSearchIndexerIdentityResourceId({
    ...identityEnv,
    SEARCH_USER_ASSIGNED_IDENTITIES: JSON.stringify({ [`${identityId}-other`]: {} }),
  }), undefined);
  assert.throws(() => getSearchIndexerIdentityResourceId({
    ...identityEnv,
    SEARCH_USER_ASSIGNED_IDENTITIES: JSON.stringify({ [identityId]: null }),
  }), /map resource IDs to objects/);
  assert.throws(() => getSearchIndexerIdentityResourceId({
    SEARCH_USER_ASSIGNED_IDENTITIES: "[]",
  }), /JSON object/);
  assert.throws(() => getSearchIndexerIdentityResourceId({
    SEARCH_USER_ASSIGNED_IDENTITIES: "{",
  }), /valid JSON object/);
  assert.throws(() => getSearchIndexerIdentityResourceId({
    SEARCH_USER_ASSIGNED_IDENTITIES: identityEnv.SEARCH_USER_ASSIGNED_IDENTITIES,
  }), /MANAGED_IDENTITY_RESOURCE_ID is required/);
});

test("datasource updates preserve their existing explicit identity", async () => {
  const plan = buildSearchResourcePlan({ ...options, env: identityEnv });
  const existingIdentity = {
    "@odata.type": "#Microsoft.Azure.Search.DataUserAssignedIdentity",
    userAssignedIdentity: `${identityId}-adopted`,
  };
  const byPath = new Map(plan.map(resource => [
    `/${resource.collection}/${resource.name}`,
    resource,
  ]));
  const updatedIdentities: unknown[] = [];
  const fetchImpl: typeof fetch = async (input, init) => {
    const resource = byPath.get(new URL(String(input)).pathname);
    assert.ok(resource);
    if (init?.method === "PUT") {
      const body = JSON.parse(String(init.body));
      updatedIdentities.push(body.identity);
      return Response.json(body);
    }
    return Response.json(resource.collection === "datasources"
      ? { ...resource.body, description: "Existing datasource", identity: existingIdentity }
      : resource.body);
  };

  const results = await reconcileSearchResources({
    ...options,
    env: identityEnv,
    dryRun: false,
    searchAdminKey: "test-key",
    fetchImpl,
    log: () => undefined,
  });

  assert.equal(results.filter(resource => resource.action === "update").length, 2);
  assert.deepEqual(updatedIdentities, [existingIdentity, existingIdentity]);
});

test("matching adopted datasource identities do not cause redundant updates", async () => {
  const plan = buildSearchResourcePlan({ ...options, env: identityEnv });
  const existingIdentity = {
    "@odata.type": "#Microsoft.Azure.Search.DataUserAssignedIdentity",
    userAssignedIdentity: `${identityId}-adopted`,
  };
  const byPath = new Map(plan.map(resource => [
    `/${resource.collection}/${resource.name}`,
    resource,
  ]));
  const methods: string[] = [];
  const fetchImpl: typeof fetch = async (input, init) => {
    methods.push(init?.method ?? "GET");
    const resource = byPath.get(new URL(String(input)).pathname);
    assert.ok(resource);
    return Response.json(resource.collection === "datasources"
      ? { ...resource.body, identity: existingIdentity }
      : resource.body);
  };

  const results = await reconcileSearchResources({
    ...options,
    env: identityEnv,
    dryRun: false,
    searchAdminKey: "test-key",
    fetchImpl,
    log: () => undefined,
  });

  assert.ok(results.every(resource => resource.action === "unchanged"));
  assert.deepEqual(methods, Array(10).fill("GET"));
});

test("malformed existing datasource identities fail without writes", async () => {
  const plan = buildSearchResourcePlan(options);
  const byPath = new Map(plan.map(resource => [
    `/${resource.collection}/${resource.name}`,
    resource,
  ]));
  const methods: string[] = [];
  const fetchImpl: typeof fetch = async (input, init) => {
    methods.push(init?.method ?? "GET");
    const resource = byPath.get(new URL(String(input)).pathname);
    assert.ok(resource);
    return Response.json(resource.collection === "datasources"
      ? { ...resource.body, identity: "invalid" }
      : resource.body);
  };

  await assert.rejects(() => reconcileSearchResources({
    ...options,
    dryRun: false,
    searchAdminKey: "test-key",
    fetchImpl,
    log: () => undefined,
  }), /returned an invalid identity/);
  assert.ok(methods.every(method => method === "GET"));
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