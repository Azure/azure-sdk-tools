import {
  buildAzdEnvironmentValues,
  loadEnvironmentSuite,
} from "../hooks/lib/env-suite.js";
import { ensureCallerPermission } from "../hooks/lib/ensure-role-assignment.js";
import {
  type CommandRunner,
  isDirectExecution,
  runCli,
  runCommand,
} from "./lib/cli.js";

export function resolveAgentProjectId(env: NodeJS.ProcessEnv): string {
  const defaults = env.AZURE_ENV_NAME
    ? buildAzdEnvironmentValues(loadEnvironmentSuite(), env.AZURE_ENV_NAME)
    : {};
  const values = { ...defaults, ...env };
  const subscription = values.AZURE_SUBSCRIPTION_ID?.trim();
  const resourceGroup = values.AZURE_RESOURCE_GROUP?.trim();
  const account = (values.AI_RESOURCE_NAME_OVERRIDE ?? values.AI_RESOURCE_NAME)?.trim();
  const project = (values.AI_PROJECT_NAME_OVERRIDE ?? values.AI_PROJECT_NAME)?.trim();
  const derived = subscription && resourceGroup && account && project
    ? `/subscriptions/${subscription}/resourceGroups/${resourceGroup}/providers/Microsoft.CognitiveServices/accounts/${account}/projects/${project}`
    : undefined;
  let endpointProjectId: string | undefined;
  if (values.FOUNDRY_PROJECT_ENDPOINT) {
    const endpoint = new URL(values.FOUNDRY_PROJECT_ENDPOINT);
    const match = /^\/api\/projects\/([^/]+)\/?$/.exec(endpoint.pathname);
    if (
      endpoint.protocol !== "https:" ||
      !endpoint.hostname.endsWith(".services.ai.azure.com") ||
      !match || !subscription || !resourceGroup
    ) {
      throw new Error("Cannot resolve the deployed Foundry endpoint to its project ARM scope.");
    }
    const endpointAccount = endpoint.hostname.split(".")[0];
    const endpointProject = decodeURIComponent(match[1]);
    endpointProjectId =
      `/subscriptions/${subscription}/resourceGroups/${resourceGroup}/providers/Microsoft.CognitiveServices/accounts/${endpointAccount}/projects/${endpointProject}`;
  }
  const projectId = values.AZURE_AI_PROJECT_ID?.trim().replace(/\/+$/, "") || derived || endpointProjectId;
  if (!projectId || !/^\/subscriptions\/[^/]+\/resourceGroups\/[^/]+\/providers\/Microsoft\.CognitiveServices\/accounts\/[^/]+\/projects\/[^/]+$/i.test(projectId)) {
    throw new Error("Agent deployment requires AZURE_AI_PROJECT_ID or the provisioned subscription, resource group, AI account, and project names.");
  }
  if (derived && projectId.toLowerCase() !== derived.toLowerCase()) {
    throw new Error("AZURE_AI_PROJECT_ID conflicts with the selected environment's AI resource/project names.");
  }
  if (endpointProjectId && projectId.toLowerCase() !== endpointProjectId.toLowerCase()) {
    throw new Error("The Foundry project endpoint conflicts with the deployment's project ARM scope.");
  }
  return projectId;
}

export async function ensureAgentDeployAccess(
  env: NodeJS.ProcessEnv = process.env,
  run: CommandRunner = runCommand,
  wait?: (milliseconds: number) => Promise<void>,
  log: (message: string) => void = console.log,
): Promise<void> {
  await ensureCallerPermission({
    scope: resolveAgentProjectId(env),
    dataAction: "Microsoft.CognitiveServices/accounts/AIServices/agents/write",
    roleDefinitionId: "53ca6127-db72-4b80-b1b0-d745d6d5456d",
    log,
  }, run, wait);
}

if (isDirectExecution(import.meta.url)) {
  runCli(() => ensureAgentDeployAccess());
}
