import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
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

  assert.equal(dev.SEARCH_KNOWLEDGE_RETRIEVAL, "free");
  assert.deepEqual(JSON.parse(dev.COSMOS_CAPABILITIES), [
    { name: "EnableServerless" },
    { name: "EnableNoSQLVectorSearch" },
  ]);
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
  for (const parameters of [frontendParameters, agentServerParameters]) {
    assert.match(parameters, /AZURE_CONTAINER_REGISTRY_ENDPOINT/);
  }
  assert.match(frontendParameters, /SERVICE_FRONTEND_IMAGE_NAME/);
  assert.match(agentServerParameters, /SERVICE_AGENT_SERVER_IMAGE_NAME/);
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

  const agent = stageInvocation("DeployAgent");
  const agentServer = stageInvocation("DeployAgentServer");
  const frontend = stageInvocation("DeployFrontend");

  assert.match(agent, /dependsOn: \$\{\{ parameters\.dependsOn \}\}/);
  assert.match(agentServer, /dependsOn: DeployAzureMcpServerAgent/);
  assert.match(stages, /stage: VerifyAgentServer[\s\S]*?dependsOn: DeployAgentServer/);
  assert.match(stages, /template: .*\/smoke-test\.yml/);
  assert.match(stages, /appName: '\$\(AGENT_SERVER_SITE_NAME\)'/);
  assert.match(stages, /healthPath: '\$\(AGENT_SERVER_HEALTH_PATH\)'/);
  assert.match(stages, /easyAuthAudience: '\$\(SERVER_APPLICATION_ID_URI\)'/);
  assert.match(stages, /evolution-agent-deploy-stage\.yml[\s\S]*?dependsOn: DeployAgent/);
  assert.match(stages, /knowledge-sync-deploy-stage\.yml[\s\S]*?dependsOn: VerifyAgentServer/);
  assert.match(frontend, /dependsOn: VerifyAgentServer/);
  assert.doesNotMatch(frontend, /dependsOn: DeployEvolutionAgent/);
  assert.doesNotMatch(`${orchestrator}\n${stages}`, /function-app|DeployFunctionApp/);
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
  const dev = suite.match(/\n    dev:\n([\s\S]*?)\n    preview:/)?.[1] ?? "";

  assert.match(dev, /DEVELOPER_PRINCIPAL_ID: '2efb50ed-0ca9-4cf1-b43b-9b31a87e08f5'/);
  assert.match(dev, /DEVELOPER_PRINCIPAL_TYPE: 'Group'/);
  assert.doesNotMatch(preprovision, /"env", "set", "DEVELOPER_PRINCIPAL_ID"/);
  assert.doesNotMatch(seedAppConfig, /DEPLOYMENT_PRINCIPAL_ID.*\|\|.*DEVELOPER_PRINCIPAL_ID/s);
  assert.doesNotMatch(read("hooks/lib/resource-roles.ts"), /DEPLOYMENT_PRINCIPAL_ID[^\n]*\|\|[^\n]*DEVELOPER_PRINCIPAL_ID/);
});

test("targets the existing shared dev environment", () => {
  const suite = parse(read("infra/environments/environment-suite.yaml"));
  const dev = suite.environments.dev;
  const serviceConnections = read("pipelines/templates/service-connection.yml");

  assert.equal(dev.subscriptionId, "a18897a6-7e44-457d-9260-f2854c0aca42");
  assert.equal(dev.resourceGroupPrefix, "azure-sdk-qa-bot-dev");
  assert.equal(dev.location, "westus2");
  assert.equal(dev.aiLocation, "swedencentral");
  assert.equal(dev.appConfigName, "azuresdkqabot-dev-config");
  assert.equal(dev.containerRegistryName, "azuresdkqabotdevcontainer");
  assert.equal(dev.frontendSiteName, "azsdkqabotdev");
  assert.equal(dev.agentServerSiteName, "azuresdkqabot-dev-server");
  assert.equal(dev.bicepOverrides.MANAGED_IDENTITY_NAME, "azuresdkqabot-dev-identity");
  assert.equal(dev.bicepOverrides.AI_RESOURCE_NAME, "azuresdkqabot-dev-ai-resource");
  assert.equal(dev.bicepOverrides.AI_PROJECT_NAME, "azuresdkqabot-ai");
  assert.match(
    serviceConnections,
    /eq\(parameters\.environment, 'dev'\)\s*\}\}:\s*\n\s*value: qa-bot-dev/,
  );
});

test("selects environment service connections at template expansion time", () => {
  const template = parse(read("pipelines/templates/service-connection.yml"));

  assert.deepEqual(template.parameters[0].values, ["dev", "preview", "prod"]);
  assert.deepEqual(template.variables, [
    {
      name: "AZURE_SERVICE_CONNECTION",
      "${{ if eq(parameters.environment, 'dev') }}": { value: "qa-bot-dev" },
      "${{ elseif eq(parameters.environment, 'prod') }}": { value: "azuresdkqabot" },
      "${{ else }}": { value: "azuresdkqabot-preview" },
    },
    {
      "${{ if eq(parameters.environment, 'prod') }}": [
        { name: "CANDIDATE_AZURE_SERVICE_CONNECTION", value: "azuresdkqabot-dev" },
      ],
    },
  ]);
});

test("configures qa-bot-dev backend authentication during pipeline provisioning", () => {
  const suite = parse(read("infra/environments/environment-suite.yaml"));
  const backend = read("infra/layers/agent-server/main.bicep");
  const adapter = read("infra/layers/agent-server/main.bicepparam");

  assert.deepEqual(JSON.parse(suite.environments.dev.bicepOverrides.AGENT_SERVER_ALLOWED_CLIENT_IDS), [
    "2a0eb32e-233f-4ccf-8977-0588c3d554b5",
  ]);
  assert.equal(suite.environments.prod.bicepOverrides.AGENT_SERVER_ALLOWED_CLIENT_IDS, undefined);
  assert.equal(suite.environments.preview.bicepOverrides.AGENT_SERVER_ALLOWED_CLIENT_IDS, undefined);
  assert.match(backend, /allowedApplications: union\(\[[\s\S]*?managedIdentityClientId[\s\S]*?frontendIdentityClientId[\s\S]*?azureCliClientId[\s\S]*?\], allowedCallerApplicationIds\)/);
  assert.match(adapter, /json\(readEnvironmentVariable\('AGENT_SERVER_ALLOWED_CLIENT_IDS', '\[\]'\)\)/);
  assert.doesNotMatch(read("scripts/qa-bot-dev-bootstrap.sh"), /authsettingsV2|allowedApplications/);
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
  const frontend = project.match(/\n    frontend:\n([\s\S]*?)\ninfra:/)?.[1] ?? "";
  const agentServer = project.match(/\n    agent-server:\n([\s\S]*?)\n    frontend:/)?.[1] ?? "";

  assert.equal((project.match(/tag: \$\{AZD_IMAGE_TAG\}/g) ?? []).length, 3);
  assert.doesNotMatch(agentServer, /resourceName:/);
  assert.match(frontend, /language: docker/);
  assert.match(frontend, /image: azure-sdk-qa-bot/);
  assert.match(frontend, /remoteBuild: true/);
  assert.doesNotMatch(frontend, /predeploy:/);
  assert.doesNotMatch(project, /function-app|azure-sdk-qa-bot-function/);
  for (const path of ["Dockerfile", "src/services/StorageService.ts", "tsconfig.json", "scripts/tsconfig.json"]) {
    assert.equal(existsSync(resolve(deploymentRoot, "../azure-sdk-qa-bot-function", path)), false);
  }
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
    13,
  );
});

test("requires existing resource groups and deploys only resource-group-scoped layers", () => {
  const project = parse(read("../azure.yaml"));
  const names = ["shared-resources", "agent", "frontend", "agent-server", "logic-app"];
  assert.deepEqual(project.infra.layers.map((layer: { name: string }) => layer.name), names);
  assert.match(project.infra.layers[0].hooks.preprovision.run, /hooks\/preprovision\.ts/);
  for (const layer of project.infra.layers) {
    assert.ok((layer.dependsOn ?? []).every((name: string) => names.includes(name)));
    assert.match(read(`infra/layers/${layer.name}/main.bicep`), /targetScope = 'resourceGroup'/);
  }
  assert.equal(existsSync(resolve(deploymentRoot, "infra/layers/resource-group/main.bicep")), false);
  const pipeline = read("pipelines/templates/azd-provision.yml");
  const orchestrator = read("pipelines/orchestrators/qa-bot-deploy.yml");
  const singleLayer = read("pipelines/templates/layer-provision-stage.yml");
  assert.doesNotMatch(`${pipeline}\n${orchestrator}\n${singleLayer}`, /resource-group|provisionResourceGroup/);
  const guard = pipeline.indexOf("./node_modules/.bin/tsx hooks/layer-preprovision.ts");
  assert.notEqual(guard, -1);
  assert.ok(guard < pipeline.indexOf("azd env select"));
  assert.ok(guard < pipeline.indexOf('azd provision "$TARGET_LAYER"'));
  const bootstrap = read("scripts/qa-bot-dev-bootstrap.sh");
  assert.doesNotMatch(bootstrap, /QA Bot Subscription Deployment|role definition|Microsoft\.Resources\/deployments\/\*|resourceGroups\/write/);
});

test("builds frontend TypeScript inside the native container context", () => {
  const dockerfile = read("../azure-sdk-qa-bot/Dockerfile");
  const dockerignore = read("../azure-sdk-qa-bot/.dockerignore");

  const stages = dockerfile.split(/^FROM /m).slice(1);
  assert.equal(stages.length, 2);
  assert.match(stages[0], /nodejs:20 AS build/);
  assert.match(stages[0], /RUN npm ci\s/);
  assert.match(stages[0], /RUN npm run build/);
  assert.match(stages[1], /nodejs:20/);
  assert.match(stages[1], /COPY --from=build \/app\/lib \.\/lib/);
  assert.match(stages[1], /RUN npm ci --omit=dev/);
  assert.doesNotMatch(stages[1], /COPY --from=build .*node_modules/);
  assert.doesNotMatch(dockerignore, /^src$/m);
  assert.doesNotMatch(dockerignore, /^\*\.ts$/m);
  assert.doesNotMatch(dockerignore, /^tsconfig\.json$/m);
});

test("does not provision or access an application secret vault", () => {
  const shared = read("infra/layers/shared-resources/main.bicep");
  const project = read("../azure.yaml");
  const postprovision = read("hooks/postprovision.ts");
  const hostedAccess = read("scripts/grant-agent-data-access.sh");
  const agentPostdeploy = read("hooks/agent-postdeploy.ts");
  const stage = read("pipelines/templates/component-deploy-stage.yml");

  assert.doesNotMatch(shared, /Microsoft\.KeyVault|keyVault|KEY_VAULT/);
  assert.doesNotMatch(project, /postdown|notify/);
  assert.doesNotMatch(postprovision, /seedKeyVault|seed-key-vault/);
  assert.doesNotMatch(`${hostedAccess}\n${agentPostdeploy}`, /KEYVAULT_ENDPOINT|KEY_VAULT_NAME|Key Vault Secrets/);
  assert.doesNotMatch(stage, /notify\.yml|TeamsWebhookUrl/);
  assert.match(hostedAccess, /GITHUB_APP_KEYVAULT_URL/);
  assert.match(hostedAccess, /permissions\.keys\[\]/);
  for (const path of [
    "hooks/lib/seed-key-vault.ts",
    "hooks/postdown.ts",
    "pipelines/templates/notify.yml",
    "../azure-sdk-qa-bot-agent/utils/azure_keyvault.py",
  ]) {
    assert.equal(existsSync(resolve(deploymentRoot, path)), false, path);
  }
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

test("reconciles frontend roles before generating Teams environment values", () => {
  const bicep = read("infra/layers/frontend/main.bicep");
  const hook = read("hooks/frontend-postprovision.ts");

  assert.doesNotMatch(bicep, /Microsoft\.Authorization\/roleAssignments/);
  assert.match(bicep, /output FRONTEND_IDENTITY_PRINCIPAL_ID string = userAssignedIdentity\.properties\.principalId/);
  assert.match(bicep, /output FRONTEND_APPLICATIONINSIGHTS_RESOURCE_ID string = component\.id/);
  assert.doesNotMatch(bicep, /17d1049b-9a84-46fb-8f53-869881c3d3ab/);
  assert.match(bicep, /enabled: true\s+emailReceivers:/);
  assert.ok(hook.indexOf('reconcileResourceRoles("frontend"') < hook.indexOf("syncTeamsEnv({"));
});

test("exposes the Foundry project identity for image-pull role reconciliation", () => {
  const bicep = read("infra/layers/agent/main.bicep");

  assert.match(
    bicep,
    /output FOUNDRY_PROJECT_PRINCIPAL_ID string = project\.identity\.principalId/,
  );
});

test("always reconciles resource roles and configuration without an authorization flag", () => {
  const suite = read("infra/environments/environment-suite.yaml");
  const postprovision = read("hooks/postprovision.ts");

  assert.doesNotMatch(suite, /manageAuthorizationResources/);
  for (const layer of ["shared-resources", "agent", "frontend"]) {
    const bicep = read(`infra/layers/${layer}/main.bicep`);
    assert.doesNotMatch(bicep, /manageAuthorizationResources|Microsoft\.Authorization\/roleAssignments/);
  }
  assert.doesNotMatch(postprovision, /MANAGE_AUTHORIZATION_RESOURCES|skipping privileged/);
  assert.match(postprovision, /uploadPerEnvBotConfigs\(\);[\s\S]*await setupSearchResources\(\);[\s\S]*updateAppConfiguration\(\);/);
  const project = parse(read("../azure.yaml"));
  assert.match(
    project.infra.layers.find((layer: { name: string }) => layer.name === "shared-resources").hooks.postprovision.run,
    /shared-postprovision\.ts/,
  );
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

  assert.match(agentServer, /name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'/);
  assert.match(agentServer, /name: 'BOT_CLIENT_ID'\s+value: frontendIdentityClientId/);
  assert.doesNotMatch(agentServer, /APP_INSIGHTS_CONNECTION_STRING/);
  for (const unusedSetting of [
    "AI_PROJECT_NAME",
    "AZURE_AI_RESOURCE_NAME",
    "COSMOS_DB_ACCOUNT_NAME",
    "STORAGE_ACCOUNT_NAME",
  ]) {
    assert.doesNotMatch(agentServer, new RegExp(`name: '${unusedSetting}'`));
  }
  assert.doesNotMatch(agentServer, /KEY_VAULT_URL|ADO_RESOURCE_SCOPE/);
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
  const convertActivity =
    threadActions.Need_Reply.actions.Should_Reply.actions.convertActivity;

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
  assert.equal(convertActivity.type, "Http");
  assert.equal(convertActivity.inputs.method, "POST");
  assert.equal(
    convertActivity.inputs.authentication.audience,
    "@parameters('serverApplicationIdUri')",
  );
  assert.equal(
    convertActivity.inputs.uri,
    "@{parameters('serverBaseUrl')}/activity/convert",
  );
  assert.doesNotMatch(
    `${workflowText}\n${bicep}\n${parameters}\n${patchWorkflow}`,
    /functionAppResourceId|FUNCTION_APP/,
  );
});

test("deploys scheduled knowledge sync with the agent server", () => {
  const agentServer = read("infra/layers/agent-server/main.bicep");
  const fullStack = read("pipelines/templates/deploy-stage.yml");
  const targeted = read("pipelines/templates/provision-and-deploy-component.yml");
  const stage = read("pipelines/templates/knowledge-sync-deploy-stage.yml");

  assert.doesNotThrow(() => parse(stage));
  assert.match(agentServer, /name: 'BOT_CLIENT_ID'\s+value: frontendIdentityClientId/);
  assert.match(fullStack, /knowledge-sync-deploy-stage\.yml/);
  assert.match(
    targeted,
    /eq\(parameters\.component, 'agent-server'\)[\s\S]*?knowledge-sync-deploy-stage\.yml/,
  );
  assert.match(stage, /values: \[dev, prod\]/);
  assert.match(stage, /npm test -- --no-file-parallelism/);
  assert.match(stage, /npm run build:webjob/);
  assert.match(stage, /configure-knowledge-sync-ssh\.mjs/);
  assert.match(stage, /deploy-webjob\.mjs/);
  assert.match(stage, /WEBJOB_NAME: knowledge-sync/);
});

test("keeps WebJob tools without reinstalling Node in the backend image", () => {
  const dockerfile = read("../azure-sdk-qa-bot-agent/Dockerfile");
  const launcher = read("../azure-sdk-qa-bot-knowledge-sync/webjob/run.sh");

  assert.match(dockerfile, /tdnf install -y .*git openssh-clients/);
  assert.doesNotMatch(dockerfile, /nodejs\.org|npm install|tdnf install[^\n]*nodejs/);
  assert.match(launcher, /NODE="\$JOB_ROOT\/runtime\/bin\/node"/);
  assert.match(launcher, /exec "\$NODE"/);
});

test("subscribes the additional dev .NET channel", () => {
  const suite = parse(read("infra/environments/environment-suite.yaml"));
  const channels = read("config/dev/channel.yaml");
  const channelId =
    "19:de16cb01a8a140678f02c3c1c8999756@thread.tacv2";

  assert.ok(suite.environments.dev.teamsChannelIds.includes(channelId));
  assert.match(
    channels,
    new RegExp(channelId.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")),
  );
  assert.match(
    channels,
    /name: Code Generation - \.NET[\s\S]*?tenant: dotnet_channel_qa_bot/,
  );
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