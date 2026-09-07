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

test("runs Search indexers after both data producers", () => {
  const knowledge = read("pipelines/orchestrators/knowledge-sync/knowledge-sync.yml");
  const wiki = read("../azure-sdk-qa-bot-wiki-index/build_wiki.yml");

  assert.match(knowledge, /run-search-indexer\.sh" AI_SEARCH_INDEXER/);
  assert.match(
    knowledge,
    /Agent\.BuildDirectory\)\/s\/azure-sdk-tools\/tools\/sdk-ai-bots\/deployment/,
  );
  assert.match(wiki, /run-search-indexer\.sh" AI_SEARCH_WIKI_INDEXER/);
});