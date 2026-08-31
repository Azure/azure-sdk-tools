import assert from "node:assert/strict";
import test from "node:test";

import {
  derivedAppConfigValues,
  fixedAppConfigValues,
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

test("injects the chat agent Application Insights resource ID", () => {
  const resourceId =
    "/subscriptions/sub/resourceGroups/rg/providers/microsoft.insights/components/agent-insights";
  const values = derivedAppConfigValues({
    AGENT_APPLICATIONINSIGHTS_RESOURCE_ID: resourceId,
    AI_RESOURCE_NAME: "ai-resource",
    APP_CONFIG_NAME: "app-config",
    CONTAINER_REGISTRY_NAME: "registry",
    COSMOSDB_ACCOUNT_NAME: "cosmos",
    FOUNDRY_PROJECT_ENDPOINT: "https://example.services.ai.azure.com/api/projects/project/",
    KEY_VAULT_NAME: "vault",
    SEARCH_SERVICE_NAME: "search",
    STORAGE_ACCOUNT_NAME: "storage",
  });

  assert.equal(values.AGENT_APPLICATIONINSIGHTS_RESOURCE_ID, resourceId);
});