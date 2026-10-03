import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

const deploymentRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function read(relativePath: string): string {
  return readFileSync(resolve(deploymentRoot, relativePath), "utf8");
}

test("provisions the chatbot evolution and feedback containers", () => {
  const bicep = read("infra/layers/shared-resources/main.bicep");

  assert.match(bicep, /name: 'qa-records'/);
  assert.match(bicep, /name: 'feedback-records'/);
  assert.equal((bicep.match(/paths:\s*\[\s*'\/tenant_id'/g) ?? []).length, 3);
  assert.match(bicep, /paths:\s*\[\s*'\/tenant_id'/);
});

test("adopts the existing dev infrastructure contract without losing live state", () => {
  const suite = parse(read("infra/environments/environment-suite.yaml"));
  const dev = suite.environments.dev.bicepOverrides;
  const prod = suite.environments.prod.bicepOverrides;
  const shared = read("infra/layers/shared-resources/main.bicep");
  const agent = read("infra/layers/agent/main.bicep");
  const frontendParameters = read("infra/layers/frontend/main.bicepparam");
  const agentServerParameters = read("infra/layers/agent-server/main.bicepparam");
  const functionParameters = read("infra/layers/function-app/main.bicepparam");

  assert.equal(dev.SEARCH_KNOWLEDGE_RETRIEVAL, "free");
  assert.deepEqual(JSON.parse(dev.COSMOS_CAPABILITIES), [
    { name: "EnableServerless" },
    { name: "EnableNoSQLVectorSearch" },
  ]);
  assert.equal(JSON.parse(dev.KEY_VAULT_ACCESS_POLICIES).length, 2);
  assert.equal(Object.keys(JSON.parse(dev.CONTAINER_REGISTRY_USER_ASSIGNED_IDENTITIES)).length, 2);
  assert.equal(dev.CONTAINER_REGISTRY_IS_EXISTING, "true");
  assert.equal(Object.keys(JSON.parse(dev.SEARCH_USER_ASSIGNED_IDENTITIES)).length, 1);
  assert.equal(Object.keys(JSON.parse(dev.AI_RESOURCE_USER_ASSIGNED_IDENTITIES)).length, 1);
  assert.equal(dev.GPT_4_1_SKU_NAME, "Standard");
  assert.equal(dev.GPT_4_1_CAPACITY, "400");
  assert.equal(dev.GPT_5_1_CAPACITY, "2000");
  assert.equal(dev.GPT_5_MINI_CAPACITY, "150");
  assert.equal(dev.TEXT_EMBEDDING_3_SMALL_CAPACITY, "120");
  assert.equal(dev.AGENT_LOG_RETENTION_IN_DAYS, "365");
  assert.equal(prod.AGENT_LOG_WORKSPACE_NAME, "azuresdkqabot-log");
  assert.equal(prod.GPT_4_1_CAPACITY, "200");

  assert.match(shared, /accessPolicies: keyVaultAccessPolicies/);
  assert.match(shared, /capabilities: cosmosCapabilities/);
  assert.match(shared, /knowledgeRetrieval: searchKnowledgeRetrieval/);
  assert.match(shared, /type: empty\(searchUserAssignedIdentities\) \? 'SystemAssigned'/);
  assert.match(shared, /type: empty\(containerRegistryUserAssignedIdentities\) \? 'None'/);
  assert.match(shared, /resource existingRegistry .* existing = if \(containerRegistryIsExisting\)/);
  for (const partitionKey of [
    "/mapping_key",
    "/conversation_partition",
    "/tenant_id",
    "/sourceChannelId",
  ]) {
    assert.match(shared, new RegExp(`'${partitionKey}'`));
  }
  assert.match(shared, /vectorEmbeddingPolicy/);
  assert.match(agent, /userAssignedIdentities: aiResourceUserAssignedIdentities/);
  assert.match(agent, /raiPolicyName: modelRaiPolicyName/g);
  assert.match(agent, /capacity: gpt51Capacity/);
  assert.match(agent, /retentionInDays: agentLogRetentionInDays/);
  assert.match(agent, /resource appInsightsConnection[\s\S]*?name: component\.name/);
  for (const parameters of [frontendParameters, agentServerParameters, functionParameters]) {
    assert.match(parameters, /AZURE_CONTAINER_REGISTRY_ENDPOINT/);
  }
  assert.match(frontendParameters, /SERVICE_FRONTEND_IMAGE_NAME/);
  assert.match(agentServerParameters, /SERVICE_AGENT_SERVER_IMAGE_NAME/);
  assert.match(functionParameters, /SERVICE_FUNCTION_APP_IMAGE_NAME/);
  assert.match(
    read("infra/layers/frontend/main.bicep"),
    /Microsoft\.Azure\.Monitor\.WebtestLocationAvailabilityCriteria/,
  );
});

test("deploys the Azure MCP Server agent and its Teams-group workflow", () => {
  const suite = parse(read("infra/environments/environment-suite.yaml"));
  const logicApp = read("infra/layers/logic-app/main.bicep");
  const patchWorkflow = read("hooks/lib/patch-workflow.ts");
  const frontendPostdeploy = read("hooks/frontend-postdeploy.ts");
  const grantAgentAccess = read("scripts/grant-agent-data-access.sh");
  const hostedAgent = read("pipelines/templates/hosted-agent-deploy-steps.yml");
  const fullStack = read("pipelines/templates/deploy-stage.yml");
  const azureMcpStage = read("pipelines/templates/azure-mcp-agent-deploy-stage.yml");
  const targeted = read("pipelines/templates/provision-and-deploy-component.yml");
  const devChannels = read("config/dev/channel.yaml");

  assert.match(hostedAgent, /values: \[chat_agent, azure_mcp_server_agent, chatbot_evolution_agent\]/);
  assert.match(hostedAgent, /AZURE_MCP_SERVER_AGENT_VERSION/);
  assert.match(hostedAgent, /grant-agent-data-access\.sh" primary/);
  assert.doesNotThrow(() => parse(azureMcpStage));
  assert.match(fullStack, /azure-mcp-agent-deploy-stage\.yml/);
  assert.ok(
    fullStack.indexOf("stageName: DeployAgent") <
      fullStack.indexOf("azure-mcp-agent-deploy-stage.yml"),
  );
  assert.match(
    fullStack,
    /stageName: DeployAgentServer[\s\S]*?dependsOn: DeployAzureMcpServerAgent/,
  );
  assert.match(
    targeted,
    /eq\(parameters\.component, 'agent'\)[\s\S]*?azure-mcp-agent-deploy-stage\.yml/,
  );
  assert.match(logicApp, /resource azureMcpWorkflow /);
  assert.match(logicApp, /resource azureMcpMetricAlert /);
  assert.match(logicApp, /azureMcpTeamsGroupId/);
  assert.match(patchWorkflow, /AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME/);
  assert.match(patchWorkflow, /AZURE_MCP_TEAMS_CHANNEL_IDS/);
  assert.match(frontendPostdeploy, /AZURE_MCP_TEAMS_GROUP_ID/);
  assert.match(grantAgentAccess, /properties\.enableRbacAuthorization/);
  assert.match(grantAgentAccess, /permissions\.keys\[\]/);
  assert.equal(
    suite.environments.dev.azureMcpTeamsGroupId,
    "07bb6114-8ffb-4e79-b65a-67f19d23e5bc",
  );
  assert.deepEqual(suite.environments.dev.azureMcpTeamsChannelIds, [
    "19:GZ1GJyuOy0b8PWe6XtNxFdiaiT2M-NdiOXDLR5ySXXc1@thread.tacv2",
  ]);
  assert.match(devChannels, /tenant: azure_mcp_server/);
});

test("deploys application services according to runtime dependencies", () => {
  const orchestrator = read("pipelines/orchestrators/qa-bot-deploy.yml");
  const stages = read("pipelines/templates/deploy-stage.yml");
  assert.doesNotThrow(() => parse(stages));

  const stageInvocation = (stageName: string): string => {
    const start = stages.indexOf(`stageName: ${stageName}`);
    assert.notEqual(start, -1, `${stageName} stage is missing`);
    const next = stages.indexOf("\n    - ", start);
    return stages.slice(start, next === -1 ? undefined : next);
  };

  const functionApp = stageInvocation("DeployFunctionApp");
  const agent = stageInvocation("DeployAgent");
  const agentServer = stageInvocation("DeployAgentServer");
  const frontend = stageInvocation("DeployFrontend");

  assert.match(functionApp, /dependsOn: \$\{\{ parameters\.dependsOn \}\}/);
  assert.match(agent, /dependsOn: \$\{\{ parameters\.dependsOn \}\}/);
  assert.match(agentServer, /dependsOn: DeployAzureMcpServerAgent/);
  assert.match(stages, /stage: VerifyAgentServer[\s\S]*?dependsOn: DeployAgentServer/);
  assert.match(stages, /template: .*\/smoke-test\.yml/);
  assert.match(stages, /appName: '\$\(AGENT_SERVER_SITE_NAME\)'/);
  assert.match(stages, /healthPath: '\$\(AGENT_SERVER_HEALTH_PATH\)'/);
  assert.match(stages, /easyAuthAudience: '\$\(SERVER_APPLICATION_ID_URI\)'/);
  assert.match(stages, /evolution-agent-deploy-stage\.yml[\s\S]*?dependsOn: DeployAgent/);
  assert.match(frontend, /dependsOn: VerifyAgentServer/);
  assert.doesNotMatch(frontend, /dependsOn: DeployEvolutionAgent/);
  assert.doesNotMatch(`${orchestrator}\n${stages}`, /stabilizationSeconds|\bsleep\b/);
});

test("exports the deployment principal for targeted layer provisioning", () => {
  const auth = read("pipelines/templates/azd-auth.yml");

  assert.match(auth, /variable=DEPLOYMENT_PRINCIPAL_ID/);
  assert.match(auth, /variable=DEPLOYMENT_PRINCIPAL_TYPE]ServicePrincipal/);
});

test("keeps dev developer roles separate from the deployment principal", () => {
  const suite = read("infra/environments/environment-suite.yaml");
  const preprovision = read("hooks/preprovision.ts");
  const seedAppConfig = read("hooks/lib/seed-app-config.ts");
  const seedKeyVault = read("hooks/lib/seed-key-vault.ts");
  const shared = read("infra/layers/shared-resources/main.bicep");
  const agent = read("infra/layers/agent/main.bicep");
  const dev = suite.match(/\n    dev:\n([\s\S]*?)\n    preview:/)?.[1] ?? "";

  assert.match(dev, /DEVELOPER_PRINCIPAL_ID: '2efb50ed-0ca9-4cf1-b43b-9b31a87e08f5'/);
  assert.match(dev, /DEVELOPER_PRINCIPAL_TYPE: 'Group'/);
  assert.doesNotMatch(preprovision, /"env", "set", "DEVELOPER_PRINCIPAL_ID"/);
  assert.doesNotMatch(seedAppConfig, /DEPLOYMENT_PRINCIPAL_ID.*\|\|.*DEVELOPER_PRINCIPAL_ID/s);
  assert.doesNotMatch(seedKeyVault, /DEPLOYMENT_PRINCIPAL_ID.*\|\|.*DEVELOPER_PRINCIPAL_ID/s);
  for (const resourceName of [
    "deploymentStorageBlobContributor",
    "deploymentAppConfigDataReader",
    "deploymentKeyVaultSecretsUser",
    "deploymentAcrContributor",
    "deploymentSearchServiceContributor",
    "deploymentCosmosDataContributor",
  ]) {
    assert.match(shared, new RegExp(`resource ${resourceName} `));
  }
  assert.match(agent, /resource deploymentOpenAiUserRoleAssignment /);
  assert.match(agent, /resource deploymentFoundryProjectManagerRoleAssignment /);
});

test("targets the existing shared dev environment", () => {
  const suite = parse(read("infra/environments/environment-suite.yaml"));
  const dev = suite.environments.dev;
  const serviceConnections = read("pipelines/templates/service-connection.yml");

  assert.equal(dev.subscriptionId, "a18897a6-7e44-457d-9260-f2854c0aca42");
  assert.equal(dev.resourceGroupPrefix, "azure-sdk-qa-bot-dev");
  assert.equal(dev.location, "westus2");
  assert.equal(dev.aiLocation, "swedencentral");
  assert.equal(dev.keyVaultName, "azuresdkqabot-dev-kv");
  assert.equal(dev.appConfigName, "azuresdkqabot-dev-config");
  assert.equal(dev.containerRegistryName, "azuresdkqabotdevcontainer");
  assert.equal(dev.frontendSiteName, "azsdkqabotdev");
  assert.equal(dev.agentServerSiteName, "azuresdkqabot-dev-server");
  assert.equal(dev.functionAppName, "azuresdkqabot-dev-function");
  assert.equal(dev.bicepOverrides.MANAGED_IDENTITY_NAME, "azuresdkqabot-dev-identity");
  assert.equal(dev.bicepOverrides.AI_RESOURCE_NAME, "azuresdkqabot-dev-ai-resource");
  assert.equal(dev.bicepOverrides.AI_PROJECT_NAME, "azuresdkqabot-ai");
  assert.match(
    serviceConnections,
    /eq\(parameters\.environment, 'dev'\)\s*\}\}:\s*\n\s*value: 'Azure SDK Engineering System'/,
  );
});

test("reports the selected image version before each azd deployment", () => {
  const orchestrator = read("pipelines/orchestrators/qa-bot-deploy.yml");
  const fullStack = read("pipelines/templates/deploy-stage.yml");
  const targeted = read("pipelines/templates/provision-and-deploy-component.yml");
  const component = read("pipelines/templates/component-deploy-stage.yml");
  const deploy = read("pipelines/templates/azd-deploy.yml");
  const evolution = read("pipelines/templates/evolution-agent-deploy-stage.yml");
  const hostedAgent = read("pipelines/templates/hosted-agent-deploy-steps.yml");
  const showVersionPosition = deploy.indexOf("displayName: 'Show image version'");
  const deployPosition = deploy.indexOf("displayName: 'azd deploy");

  assert.notEqual(showVersionPosition, -1);
  assert.notEqual(deployPosition, -1);
  assert.ok(showVersionPosition < deployPosition);
  assert.match(orchestrator, /- name: imageTag/);
  assert.match(orchestrator, /displayName: Image tag override \(optional\)/);
  assert.match(orchestrator, /default: auto/);
  for (const template of [orchestrator, fullStack, targeted, component, evolution, deploy, hostedAgent]) {
    assert.match(template, /- name: imageTag[\s\S]*?default: auto/);
  }
  for (const template of [orchestrator, fullStack, targeted, component, evolution]) {
    assert.match(template, /imageTag: \$\{\{ parameters\.imageTag \}\}/);
  }
  assert.match(deploy, /REQUESTED_IMAGE_TAG/);
  assert.match(deploy, /REQUESTED_IMAGE_TAG" = "auto"/);
  assert.match(deploy, /variable=AZD_IMAGE_TAG/);
  assert.match(deploy, /azd env set AZD_IMAGE_TAG/);
  assert.match(hostedAgent, /REQUESTED_IMAGE_TAG" = "auto"/);
  assert.match(hostedAgent, /--tag '\$\(AZD_IMAGE_TAG\)'/);
});

test("deploys the image tag selected by the pipeline", () => {
  const project = read("../azure.yaml");
  const localSync = read("scripts/sync-env-suite.ts");
  const frontend = project.match(/\n    frontend:\n([\s\S]*?)\n    function-app:/)?.[1] ?? "";
  const functionApp = project.match(/\n    function-app:\n([\s\S]*?)\ninfra:/)?.[1] ?? "";
  const agentServer = project.match(/\n    agent-server:\n([\s\S]*?)\n    frontend:/)?.[1] ?? "";

  assert.equal((project.match(/tag: \$\{AZD_IMAGE_TAG\}/g) ?? []).length, 4);
  assert.doesNotMatch(agentServer, /resourceName:/);
  assert.match(frontend, /language: docker/);
  assert.match(frontend, /image: azure-sdk-qa-bot/);
  assert.match(frontend, /remoteBuild: true/);
  assert.doesNotMatch(frontend, /predeploy:/);
  assert.match(functionApp, /host: function/);
  assert.match(functionApp, /image: azure-sdk-qa-bot-function/);
  assert.match(functionApp, /remoteBuild: true/);
  assert.doesNotMatch(functionApp, /predeploy:/);
  assert.match(localSync, /buildAzdEnvironmentValues/);
});

test("exports the Foundry project ID required by the agent extension", () => {
  const agent = read("infra/layers/agent/main.bicep");

  assert.match(agent, /output AZURE_AI_PROJECT_ID string = project\.id/);
});

test("runs hooks with the package-local TypeScript runtime", () => {
  const project = read("../azure.yaml");

  assert.doesNotMatch(project, /npx(?: --yes)? tsx/);
  assert.equal(
    (project.match(/\.\/node_modules\/\.bin\/tsx/g) ?? []).length,
    16,
  );
});

test("builds frontend TypeScript inside the native container context", () => {
  const dockerfile = read("../azure-sdk-qa-bot/Dockerfile");
  const dockerignore = read("../azure-sdk-qa-bot/.dockerignore");

  assert.match(dockerfile, /RUN npm run build && npm prune --omit=dev/);
  assert.doesNotMatch(dockerignore, /^src$/m);
  assert.doesNotMatch(dockerignore, /^\*\.ts$/m);
  assert.doesNotMatch(dockerignore, /^tsconfig\.json$/m);
});

test("keeps rollout configuration limited to active behavior", () => {
  const suite = read("infra/environments/environment-suite.yaml");
  const loader = read("pipelines/templates/load-environment-suite.yml");
  const smokeTest = read("scripts/smoke-test.ts");

  assert.doesNotMatch(suite, /rolloutStrategy|slot-swap|slot: 'staging'/);
  assert.doesNotMatch(suite, /minSuccessRate|latencyP95Ms|stabilizationWindowMinutes/);
  assert.doesNotMatch(loader, /ROLLOUT_STRATEGY/);
  assert.match(loader, /AGENT_SERVER_HEALTH_PATH\s+'.components\."agent-server"\.healthPath'/);
  assert.match(smokeTest, /frontendSiteName/);
  assert.match(smokeTest, /resourceGroupPrefix/);
  assert.match(smokeTest, /healthPath/);
});

test("declares principal-aware frontend role assignments in Bicep", () => {
  const bicep = read("infra/layers/frontend/main.bicep");
  const hook = read("hooks/frontend-postprovision.ts");

  assert.equal((bicep.match(/Microsoft\.Authorization\/roleAssignments/g) ?? []).length, 3);
  assert.match(
    bicep,
    /guid\(component\.id, userAssignedIdentity\.id, monitoringMetricsPublisherRoleDefinitionId\)/,
  );
  assert.match(
    bicep,
    /guid\(sharedRegistry\.id, userAssignedIdentity\.id, acrPullRoleDefinitionId\)/,
  );
  assert.match(
    bicep,
    /guid\(storageAccount\.id, userAssignedIdentity\.id, roleDefinitionId\)/,
  );
  assert.doesNotMatch(bicep, /17d1049b-9a84-46fb-8f53-869881c3d3ab/);
  assert.match(bicep, /enabled: true\s+emailReceivers:/);
  assert.doesNotMatch(hook, /ensureFrontendRoleAssignments/);
});

test("grants Search access to Foundry embedding deployments", () => {
  const bicep = read("infra/layers/agent/main.bicep");

  assert.match(
    bicep,
    /resource searchOpenAiUserRoleAssignment[\s\S]*?roleDefinitionId: subscriptionResourceId\('Microsoft\.Authorization\/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'\)[\s\S]*?principalId: searchServicePrincipalId/,
  );
});

test("can reuse pre-authorized environments without privileged ARM writes", () => {
  const suite = read("infra/environments/environment-suite.yaml");
  const postprovision = read("hooks/postprovision.ts");

  assert.match(suite, /dev:[\s\S]*manageAuthorizationResources: false[\s\S]*localDeployAllowed: true/);
  for (const layer of ["shared-resources", "agent", "frontend"]) {
    const bicep = read(`infra/layers/${layer}/main.bicep`);
    assert.match(bicep, /param manageAuthorizationResources bool = true/);
    assert.match(bicep, /if \(manageAuthorizationResources/);
  }
  assert.match(postprovision, /skipping privileged data-plane reconciliation/);
});

test("uses BOT_ID as the single frontend identity client ID", () => {
  const frontend = read("infra/layers/frontend/main.bicep");
  const agentServerParameters = read("infra/layers/agent-server/main.bicepparam");
  const frontendConfig = read("../azure-sdk-qa-bot/src/config/config.ts");

  assert.doesNotMatch(frontend, /BOT_MANAGED_IDENTITY_CLIENT_ID/);
  assert.match(agentServerParameters, /frontendIdentityClientId = readEnvironmentVariable\('BOT_ID'/);
  assert.doesNotMatch(agentServerParameters, /BOT_MANAGED_IDENTITY_CLIENT_ID/);
  assert.match(frontendConfig, /userManagedIdentityClientID: process\.env\.BOT_ID/);
});

test("uses suite-owned frontend identity name and tenant values", () => {
  const frontend = read("infra/layers/frontend/main.bicep");
  const agentServerParameters = read("infra/layers/agent-server/main.bicepparam");
  const logicAppParameters = read("infra/layers/logic-app/main.bicepparam");
  const teamsSync = read("hooks/lib/sync-teams-env.ts");

  assert.doesNotMatch(frontend, /output BOT_IDENTITY_NAME|output BOT_TENANT_ID/);
  assert.match(agentServerParameters, /frontendIdentityName = readEnvironmentVariable\('FRONTEND_SITE_NAME'/);
  assert.match(logicAppParameters, /botIdentityName = readEnvironmentVariable\('FRONTEND_SITE_NAME'/);
  assert.match(teamsSync, /target: "BOT_TENANT_ID", source: "AZURE_TENANT_ID"/);
});

test("aligns runtime app settings with application consumers", () => {
  const agentServer = read("infra/layers/agent-server/main.bicep");
  const functionApp = read("infra/layers/function-app/main.bicep");
  const storageService = read("../azure-sdk-qa-bot-function/src/services/StorageService.ts");

  assert.match(agentServer, /name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'/);
  assert.doesNotMatch(agentServer, /APP_INSIGHTS_CONNECTION_STRING/);
  for (const unusedSetting of [
    "AI_PROJECT_NAME",
    "AZURE_AI_RESOURCE_NAME",
    "COSMOS_DB_ACCOUNT_NAME",
    "STORAGE_ACCOUNT_NAME",
  ]) {
    assert.doesNotMatch(agentServer, new RegExp(`name: '${unusedSetting}'`));
  }
  assert.doesNotMatch(functionApp, /name: 'APP_CONFIG_NAME'/);
  assert.match(storageService, /process\.env\.STORAGE_ACCOUNT_NAME/g);
  assert.doesNotMatch(storageService, /AZURE_STORAGE_ACCOUNT_NAME/);
});

test("runs Search indexers after both data producers", () => {
  const knowledge = read("../azure-sdk-qa-bot-knowledge-sync/src/DailySyncKnowledge.ts");
  const wiki = read("../azure-sdk-qa-bot-wiki-index/build_wiki.yml");

  assert.match(knowledge, /searchService\.runIndexer\(\)/);
  assert.match(wiki, /run-search-indexer\.sh" AI_SEARCH_WIKI_INDEXER/);
});

test("loads Logic App channel configuration through the authenticated backend", () => {
  const workflowText = read("infra/layers/logic-app/workflowDefinition.json");
  const workflow = JSON.parse(workflowText);
  const bicep = read("infra/layers/logic-app/main.bicep");
  const parameters = read("infra/layers/logic-app/main.bicepparam");
  const patchWorkflow = read("hooks/lib/patch-workflow.ts");
  const server = read("../azure-sdk-qa-bot-agent/server.py");
  const threadActions =
    workflow.actions.For_each.actions.Thread_Message_From_User.actions;
  const channelLookup = threadActions.Get_Channel_Config;

  assert.equal(channelLookup.type, "Http");
  assert.equal(channelLookup.inputs.authentication.type, "ManagedServiceIdentity");
  assert.equal(channelLookup.inputs.authentication.audience, "@parameters('serverApplicationIdUri')");
  assert.match(channelLookup.inputs.uri, /\/config\/channel\?channel_id=/);
  assert.equal(
    threadActions.Build_Conversation_Save_Request_Body.inputs.tenant_id,
    "@body('Get_Channel_Config')?['tenant_id']",
  );
  assert.equal(threadActions.Load_Channel_Config, undefined);
  assert.equal(threadActions.Decode_Channel_Config, undefined);
  assert.equal(threadActions.Get_Tenant_ID, undefined);
  assert.doesNotMatch(workflowText, /azureblob|blobStorageAccountName/);
  assert.doesNotMatch(bicep, /blobConnection|azureBlobConnection|documentdb/i);
  assert.doesNotMatch(
    parameters,
    /AZURE_BLOB_CONNECTION_NAME|blobStorageAccountName|DOCUMENT_DB_CONNECTION_NAME/,
  );
  assert.doesNotMatch(
    patchWorkflow,
    /AZURE_BLOB_CONNECTION_NAME|blobConn|DOCUMENT_DB_CONNECTION_NAME|documentdb/i,
  );
  assert.match(server, /@app\.get\("\/config\/channel"/);
  assert.match(server, /_bot_config_service\.get_channel_config\(channel_id\)/);
});

test("resolves forwarded Teams messages before processing them", () => {
  const workflow = JSON.parse(
    read("infra/layers/logic-app/workflowDefinition.json"),
  );
  const actions = workflow.actions.For_each.actions;
  const resolution = actions.Get_and_resolve_message_details;
  const threadActions = actions.Thread_Message_From_User.actions;

  assert.equal(resolution.type, "Scope");
  assert.match(
    resolution.actions.Resolve_forwarded_message_content.inputs.code,
    /forwardedMessageReference/,
  );
  assert.match(
    resolution.actions.Include_post_title_in_message_content.inputs.code,
    /message\.subject/,
  );
  assert.equal(
    resolution.actions.Resolved_message_details.inputs,
    "@body('Include_post_title_in_message_content')",
  );
  assert.equal(
    actions.Filter_messages_mentioned_bot.inputs.from,
    "@outputs('Resolved_message_details')?['mentions']",
  );
  assert.equal(
    threadActions.Build_Conversation_Save_Request_Body.inputs.content,
    "@outputs('Resolved_message_details')?['body']?['content']",
  );
  assert.equal(
    threadActions.Need_Reply.actions.Should_Reply.actions.convertActivity.inputs.body,
    "@outputs('Resolved_message_details')",
  );
});