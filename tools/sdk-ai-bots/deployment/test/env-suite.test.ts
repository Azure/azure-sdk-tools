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

  assert.equal(suite.environments.dev.location, "centralus");
  assert.equal(getComponentConfig(suite, "function-app").healthPath, "/api/health");
  assert.deepEqual(loadServiceImageRepositories("dev"), {
    agent: "sdk-ai-bots/agent-dev",
    "agent-server": "azure-sdk-qa-bot-agent-server",
    frontend: "azure-sdk-qa-bot",
    "function-app": "azure-sdk-qa-bot-function",
  });
  assert.equal(getEnvSuiteValue("dev", "chatbotEvolutionAgentEnabled"), "false");
  assert.deepEqual(getEnvSuiteValues("dev", "teamsChannelIds"), [
    "19:ZK2wSwDjcdWUr_0jR1njUWFbnTCzKJ8QnyyDkNURqUE1@thread.tacv2",
  ]);
});

test("builds the complete local azd environment mapping", () => {
  const suite = loadEnvironmentSuite();
  const values = buildAzdEnvironmentValues(suite, "dev");

  assert.equal(values.AZD_IMAGE_TAG, "dev");
  assert.equal(values.AZURE_SUBSCRIPTION_ID, suite.environments.dev.subscriptionId);
  assert.equal(values.AZURE_LOCATION, "centralus");
  assert.equal(values.AZURE_AI_LOCATION, suite.environments.dev.aiLocation);
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
  assert.equal(values.BOT_SERVICE_NAME, "azsdkqabot-dev-20260826-federated");
  assert.equal(values.AI_RESOURCE_RESTORE, "false");
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