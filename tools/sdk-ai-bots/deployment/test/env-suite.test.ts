import assert from "node:assert/strict";
import test from "node:test";

import {
  buildAzdEnvironmentValues,
  getComponentConfig,
  getEnvSuiteValue,
  getEnvSuiteValues,
  loadEnvironmentSuite,
  loadServiceImageRepositories,
} from "../hooks/lib/env-suite.js";

test("loads typed environment and hyphenated component names", () => {
  const suite = loadEnvironmentSuite();

  assert.equal(suite.environments.dev.location, "westus2");
  assert.equal(getComponentConfig(suite, "agent-server").healthPath, "/ping");
  assert.deepEqual(loadServiceImageRepositories("dev"), {
    agent: "sdk-ai-bots/agent-dev",
    "agent-server": "azure-sdk-qa-bot-agent-server",
    frontend: "azure-sdk-qa-bot",
  });
  assert.equal(getEnvSuiteValue("dev", "chatbotEvolutionAgentEnabled"), "false");
  const devChannelIds = getEnvSuiteValues("dev", "teamsChannelIds");
  assert.equal(devChannelIds.length, 13);
  assert.ok(
    devChannelIds.includes(
      "19:3iefzURPmxhDZJJTtwePbdO1EdI5T0hfK9UFK_59Sbk1@thread.tacv2",
    ),
  );
});

test("does not require or export application-vault settings", () => {
  const suite = loadEnvironmentSuite();
  for (const environment of ["dev", "preview", "prod"]) {
    const values = buildAzdEnvironmentValues(suite, environment);
    assert.equal(suite.environments[environment].keyVaultName, undefined);
    assert.equal(values.KEY_VAULT_NAME, undefined);
    assert.equal(values.KEY_VAULT_NAME_OVERRIDE, undefined);
    assert.equal(values.KEY_VAULT_ACCESS_POLICIES, undefined);
  }
});

test("builds the complete local azd environment mapping", () => {
  const suite = loadEnvironmentSuite();
  const values = buildAzdEnvironmentValues(suite, "dev");

  assert.equal(values.AZD_IMAGE_TAG, "dev");
  assert.equal(values.AZURE_SUBSCRIPTION_ID, suite.environments.dev.subscriptionId);
  assert.equal(values.AZURE_LOCATION, "westus2");
  assert.equal(values.AZURE_AI_LOCATION, suite.environments.dev.aiLocation);
  assert.equal(values.MANAGE_AUTHORIZATION_RESOURCES, undefined);
  assert.equal(values.AGENT_IMAGE_REPOSITORY, "sdk-ai-bots/agent-dev");
  assert.equal(values.AZURE_AI_DEPLOYMENTS_LOCATION, undefined);
  assert.equal(values.ACR_NAME, "azuresdkqabotdevcontainer");
  assert.equal(
    values.SERVER_APPLICATION_CLIENT_ID,
    suite.environments.dev.serverApplicationClientId,
  );
  assert.equal(values.SERVER_AUDIENCE, undefined);
  assert.equal(values.RAG_SERVICE_SCOPE, undefined);
  assert.equal(values.FRONTEND_IMAGE_REPOSITORY, "azure-sdk-qa-bot:dev");
  assert.equal(values.AGENT_SERVER_IMAGE_REPOSITORY, "azure-sdk-qa-bot-agent-server:dev");
  assert.equal(
    values.AZURE_MCP_TEAMS_GROUP_ID,
    suite.environments.dev.azureMcpTeamsGroupId,
  );
  assert.equal(
    values.AZURE_MCP_TEAMS_CHANNEL_IDS,
    suite.environments.dev.azureMcpTeamsChannelIds.join(","),
  );
  assert.equal(
    values.AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME_OVERRIDE,
    "azuremcpserver-qabot-dev-logicapp",
  );
  assert.equal(
    values.AZURE_MCP_SERVER_LOGIC_APP_ALERT_NAME_OVERRIDE,
    "azuremcpserver-qabot-dev-logicapp-alert",
  );
  assert.equal(values.BOT_SERVICE_NAME, "azsdkqabotdev");
  assert.equal(
    values.AGENT_SERVER_LOG_WORKSPACE_RESOURCE_ID,
    "/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/DefaultResourceGroup-WUS2/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace-a18897a6-7e44-457d-9260-f2854c0aca42-WUS2",
  );
  assert.equal(values.AI_RESOURCE_RESTORE, "false");
  assert.deepEqual(JSON.parse(values.AGENT_SERVER_ALLOWED_CLIENT_IDS), [
    "2a0eb32e-233f-4ccf-8977-0588c3d554b5",
  ]);
  assert.equal(
    values.MANAGED_IDENTITY_RESOURCE_ID,
    "/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/azuresdkqabot-dev-identity",
  );
  assert.equal(
    values.AZURE_CONTAINER_REGISTRY_ENDPOINT,
    "azuresdkqabotdevcontainer.azurecr.io",
  );
  assert.equal(values.GPT_5_1_CAPACITY, "2000");
  assert.equal(values.SEARCH_KNOWLEDGE_RETRIEVAL, "free");
  assert.equal(values.AZD_DEPLOY_AGENT_SERVER_SLOT_NAME, "production");
  assert.equal(
    buildAzdEnvironmentValues(suite, "prod").MANAGE_AUTHORIZATION_RESOURCES,
    undefined,
  );
});

test("applies production overrides and candidate configuration", () => {
  const suite = loadEnvironmentSuite();
  const values = buildAzdEnvironmentValues(suite, "prod");

  assert.equal(values.FRONTEND_IMAGE_REPOSITORY, "azure-sdk-qa-bot:latest");
  assert.equal(values.MANAGED_IDENTITY_NAME, "azuresdkqabot-identity");
  assert.equal(values.MANAGED_IDENTITY_NAME_OVERRIDE, "azuresdkqabot-identity");
  assert.equal(values.AGENT_LOG_WORKSPACE_NAME, "azuresdkqabot-log");
  assert.equal(values.GPT_4_1_CAPACITY, "200");
  assert.equal(
    values.CANDIDATE_APPCONFIG_ENDPOINT,
    `https://${suite.environments.dev.appConfigName}.azconfig.io`,
  );
});

test("reuses the existing test resource group for preview without cross-environment resource IDs", () => {
  const suite = loadEnvironmentSuite();
  const preview = suite.environments.preview;
  const values = buildAzdEnvironmentValues(suite, "preview");
  const prefix = "/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-test/";

  assert.equal(preview.subscriptionId, "a18897a6-7e44-457d-9260-f2854c0aca42");
  assert.equal(preview.resourceGroupPrefix, "azure-sdk-qa-bot-test");
  assert.equal(preview.serverApplicationClientId, "674414e1-152b-41ac-b0d2-763535edea6c");
  assert.equal(preview.serverApplicationIdUri, "api://azure-sdk-qa-bot-test");
  assert.equal(preview.teamsAppId, "9ded3f10-3bcd-4d89-9f32-8ed170e003c2");
  assert.equal(values.AZURE_APPCONFIG_ENDPOINT, "https://azuresdkqabot-preview-config.azconfig.io");
  assert.equal(values.AZURE_CONTAINER_REGISTRY_ENDPOINT, "azsdkqabottest.azurecr.io");
  assert.equal(values.FRONTEND_SITE_NAME, "azsdkqabottest");
  assert.equal(values.BOT_SERVICE_NAME, "azsdkqabottest");
  assert.equal(values.BOT_ID, "d05d0376-47f5-4326-b8ca-91deabbe3b5a");
  assert.equal(values.FRONTEND_DELETE_LOCK_NAME, "delete");
  assert.equal(values.FRONTEND_DIAGNOSTIC_SETTING_NAME, "azsdkqabottest-diagnostic");
  assert.equal(values.AGENT_SERVER_SITE_NAME_OVERRIDE, "azuresdkqabot-test-server");
  assert.equal(values.AGENT_SERVER_APP_INSIGHTS_NAME, "azuresdkqabot-test-server");
  assert.equal(
    values.AGENT_SERVER_LOG_WORKSPACE_RESOURCE_ID,
    `${prefix}providers/Microsoft.OperationalInsights/workspaces/azsdkqabottest`,
  );
  assert.equal(values.AGENT_LOG_WORKSPACE_NAME, "azuresdkqabot-test-log");
  assert.equal(values.CONTAINER_REGISTRY_IS_EXISTING, "true");
  assert.equal(values.AZURE_TABLE_NAME_FOR_CONVERSATION, "TeamsChannelConversationsPreview");
  assert.equal(preview.localDeployAllowed, false);

  for (const [key, value] of Object.entries(values)) {
    assert.equal(value.startsWith("REPLACE_WITH_"), false, key);
    if (value.startsWith("/subscriptions/")) assert.ok(value.startsWith(prefix), key);
  }
  for (const [key, expected] of Object.entries({
    MANAGED_IDENTITY_NAME: "azuresdkqabot-test-identity",
    STORAGE_ACCOUNT_NAME: "azuresdkqabotteststorage",
    SEARCH_SERVICE_NAME: "azuresdkqabot-test-search",
    COSMOS_DB_ACCOUNT_NAME: "azuresdkqabot-test-db",
    AI_RESOURCE_NAME: "azuresdkqabot-test-ai-resource",
    INTEGRATION_ACCOUNT_NAME: "azuresdkqabot-test-ia",
    TEAMS_CONNECTION_NAME: "teams-preview",
    LOGIC_APP_WORKFLOW_NAME: "azuresdkqabot-test-logicapp",
  })) {
    assert.equal(values[key], expected, key);
    assert.equal(values[`${key}_OVERRIDE`], expected, `${key}_OVERRIDE`);
  }
});