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
  assert.equal(getComponentConfig(suite, "function-app").healthPath, "/api/health");
  assert.deepEqual(loadServiceImageRepositories("dev"), {
    agent: "sdk-ai-bots/agent-dev",
    "agent-server": "azure-sdk-qa-bot-agent-server",
    frontend: "azure-sdk-qa-bot",
    "function-app": "azure-sdk-qa-bot-function",
  });
  assert.equal(getEnvSuiteValue("dev", "chatbotEvolutionAgentEnabled"), "false");
  const devChannelIds = getEnvSuiteValues("dev", "teamsChannelIds");
  assert.equal(devChannelIds.length, 12);
  assert.ok(
    devChannelIds.includes(
      "19:3iefzURPmxhDZJJTtwePbdO1EdI5T0hfK9UFK_59Sbk1@thread.tacv2",
    ),
  );
});

test("builds the complete local azd environment mapping", () => {
  const suite = loadEnvironmentSuite();
  const values = buildAzdEnvironmentValues(suite, "dev");

  assert.equal(values.AZD_IMAGE_TAG, "dev");
  assert.equal(values.AZURE_SUBSCRIPTION_ID, suite.environments.dev.subscriptionId);
  assert.equal(values.AZURE_LOCATION, "westus2");
  assert.equal(values.AZURE_AI_LOCATION, suite.environments.dev.aiLocation);
  assert.equal(values.MANAGE_AUTHORIZATION_RESOURCES, "false");
  assert.equal(values.AGENT_IMAGE_REPOSITORY, "sdk-ai-bots/agent-dev");
  assert.equal(values.AZURE_AI_DEPLOYMENTS_LOCATION, undefined);
  assert.equal(values.ACR_NAME, undefined);
  assert.equal(
    values.SERVER_APPLICATION_CLIENT_ID,
    suite.environments.dev.serverApplicationClientId,
  );
  assert.equal(values.SERVER_AUDIENCE, undefined);
  assert.equal(values.RAG_SERVICE_SCOPE, undefined);
  assert.equal(values.FRONTEND_IMAGE_REPOSITORY, "azure-sdk-qa-bot:dev");
  assert.equal(values.AGENT_SERVER_IMAGE_REPOSITORY, "azure-sdk-qa-bot-agent-server:dev");
  assert.equal(values.FUNCTION_IMAGE_REPOSITORY, "azure-sdk-qa-bot-function:dev");
  assert.equal(values.BOT_SERVICE_NAME, "azsdkqabotdev");
  assert.equal(
    values.AGENT_SERVER_LOG_WORKSPACE_RESOURCE_ID,
    "/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/DefaultResourceGroup-WUS2/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace-a18897a6-7e44-457d-9260-f2854c0aca42-WUS2",
  );
  assert.equal(values.AI_RESOURCE_RESTORE, "false");
  assert.equal(
    buildAzdEnvironmentValues(suite, "prod").MANAGE_AUTHORIZATION_RESOURCES,
    "true",
  );
});

test("applies production overrides and candidate configuration", () => {
  const suite = loadEnvironmentSuite();
  const values = buildAzdEnvironmentValues(suite, "prod");

  assert.equal(values.FRONTEND_IMAGE_REPOSITORY, "azure-sdk-qa-bot:latest");
  assert.equal(values.MANAGED_IDENTITY_NAME, "azuresdkqabot-identity");
  assert.equal(values.MANAGED_IDENTITY_NAME_OVERRIDE, "azuresdkqabot-identity");
  assert.equal(
    values.CANDIDATE_APPCONFIG_ENDPOINT,
    `https://${suite.environments.dev.appConfigName}.azconfig.io`,
  );
});