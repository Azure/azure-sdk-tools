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
  const frontend = read("hooks/frontend-predeploy.ts");
  const functionApp = read("hooks/function-predeploy.ts");

  assert.equal((project.match(/tag: \$\{AZD_IMAGE_TAG\}/g) ?? []).length, 3);
  assert.match(frontend, /process\.env\.AZD_IMAGE_TAG/);
  assert.match(functionApp, /process\.env\.AZD_IMAGE_TAG/);
});

test("runs Search indexers after both data producers", () => {
  const knowledge = read("../azure-sdk-qa-bot-knowledge-sync/src/DailySyncKnowledge.ts");
  const wiki = read("../azure-sdk-qa-bot-wiki-index/build_wiki.yml");

  assert.match(knowledge, /searchService\.runIndexer\(\)/);
  assert.match(wiki, /run-search-indexer\.sh" AI_SEARCH_WIKI_INDEXER/);
});