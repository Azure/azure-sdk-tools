import assert from "node:assert/strict";
import test from "node:test";

import {
  derivedAppConfigValues,
  fixedAppConfigValues,
  isRetryableAppConfigWriteError,
} from "../hooks/lib/seed-app-config.js";

test("disables the chatbot evolution agent by default", () => {
  assert.equal(
    fixedAppConfigValues({}).CHATBOT_EVOLUTION_AGENT_ENABLED,
    "false",
  );
});

test("honors the environment-specific chatbot evolution agent setting", () => {
  assert.equal(
    fixedAppConfigValues({ CHATBOT_EVOLUTION_AGENT_ENABLED: " true " })
      .CHATBOT_EVOLUTION_AGENT_ENABLED,
    "true",
  );
});

test("seeds hosted-agent deployment settings", () => {
  const values = fixedAppConfigValues({});

  assert.equal(
    values.AI_FOUNDRY_CHATBOT_EVOLUTION_AGENT_NAME,
    "azure-sdk-chatbot-evolution-agent",
  );
  assert.equal(values.AI_FOUNDRY_RAI_POLICY_ID, "Microsoft.DefaultV2");
});

test("points the legacy Search agent key at the configured knowledge base", () => {
  const values = fixedAppConfigValues({});

  assert.equal(values.AI_SEARCH_AGENT, values.AI_SEARCH_KNOWLEDGE_BASE);
});

test("seeds the generated wiki defaults", () => {
  const values = fixedAppConfigValues({});

  assert.equal(values.AI_SEARCH_WIKI_INDEXER, "azure-sdk-knowledge-wiki-indexer");
  assert.equal(values.WIKI_SYNTHESIS_DEPLOYMENT, "gpt-5.6-sol");
  assert.equal(values.STORAGE_WIKI_OUTPUT_CONTAINER, "wiki");
});

test("injects the chat agent Application Insights resource ID", () => {
  const resourceId =
    "/subscriptions/sub/resourceGroups/rg/providers/microsoft.insights/components/agent-insights";
  const values = derivedAppConfigValues({
    AGENT_APPLICATIONINSIGHTS_RESOURCE_ID: resourceId,
    AI_RESOURCE_NAME: "ai-resource",
    APP_CONFIG_NAME: "app-config",
    AZURE_RESOURCE_GROUP: "resource-group",
    AZURE_SUBSCRIPTION_ID: "00000000-0000-0000-0000-000000000000",
    CONTAINER_REGISTRY_NAME: "registry",
    COSMOSDB_ACCOUNT_NAME: "cosmos",
    FOUNDRY_PROJECT_ENDPOINT: "https://example.services.ai.azure.com/api/projects/project/",
    KEY_VAULT_NAME: "vault",
    SEARCH_SERVICE_NAME: "search",
    STORAGE_ACCOUNT_NAME: "storage",
  });

  assert.equal(values.AGENT_APPLICATIONINSIGHTS_RESOURCE_ID, resourceId);
  assert.equal(values.AZURE_OPENAI_ENDPOINT, "https://ai-resource.openai.azure.com");
  assert.equal(
    values.STORAGE_ACCOUNT_RESOURCE_ID,
    "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/resource-group/providers/Microsoft.Storage/storageAccounts/storage",
  );
});

test("retries the opaque App Configuration RBAC propagation error", () => {
  assert.equal(
    isRetryableAppConfigWriteError(
      new Error("Failed to set the key-value due to an exception: Expecting value: line 1 column 1 (char 0)"),
    ),
    true,
  );
  assert.equal(isRetryableAppConfigWriteError(new Error("Invalid key")), false);
});