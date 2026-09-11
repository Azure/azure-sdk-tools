import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const deploymentRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function read(relativePath: string): string {
  return readFileSync(resolve(deploymentRoot, relativePath), "utf8");
}

test("provisions the chatbot evolution status container", () => {
  const bicep = read("infra/layers/shared-resources/main.bicep");

  assert.match(bicep, /name: 'qa-records'/);
  assert.match(bicep, /paths:\s*\[\s*'\/tenant_id'/);
});

test("deploys the evolution agent in the production full-stack chain", () => {
  const stages = read("pipelines/templates/deploy-stage.yml");

  assert.match(stages, /evolution-agent-deploy-stage\.yml/);
  assert.match(stages, /dependsOn: DeployEvolutionAgent/);
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
  for (const template of [orchestrator, fullStack, targeted, component, evolution]) {
    assert.match(template, /imageTag: \$\{\{ parameters\.imageTag \}\}/);
  }
  assert.match(deploy, /REQUESTED_IMAGE_TAG/);
  assert.match(deploy, /variable=AZD_IMAGE_TAG/);
  assert.match(deploy, /azd env set AZD_IMAGE_TAG/);
  assert.match(hostedAgent, /--tag '\$\(AZD_IMAGE_TAG\)'/);
});

test("deploys the image tag selected by the pipeline", () => {
  const project = read("../azure.yaml");
  const localSync = read("scripts/sync-env-suite.ts");
  const frontend = project.match(/\n    frontend:\n([\s\S]*?)\n    function-app:/)?.[1] ?? "";
  const functionApp = project.match(/\n    function-app:\n([\s\S]*?)\ninfra:/)?.[1] ?? "";

  assert.equal((project.match(/tag: \$\{AZD_IMAGE_TAG\}/g) ?? []).length, 4);
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
  assert.doesNotMatch(bicep, /blobConnection|azureBlobConnection/);
  assert.doesNotMatch(parameters, /AZURE_BLOB_CONNECTION_NAME|blobStorageAccountName/);
  assert.doesNotMatch(patchWorkflow, /AZURE_BLOB_CONNECTION_NAME|blobConn/);
  assert.match(server, /@app\.get\("\/config\/channel"/);
  assert.match(server, /_bot_config_service\.get_channel_config\(channel_id\)/);
});