/**
 * patch-workflow — apply the real `definition` and `parameters` to a Logic
 * App workflow whose shell was created by `azd provision`.
 *
 * `Microsoft.Logic/workflows` does NOT accept PATCH on any properties field
 * (`PatchWorkflowPropertiesNotSupported`) — only top-level tags can be
 * patched. Instead we GET the current resource, mutate its
 * `properties.definition` and `properties.parameters` in place, and PUT it
 * back. That preserves the identity, integration account, tags, etc. set up
 * by main.bicep while setting state from the Teams OAuth status: Enabled when
 * connected, Disabled otherwise.
 *
 * Callers populate process.env with the azd env values before invoking.
 */

import { execSync } from "child_process";
import { mkdtempSync, readFileSync, writeFileSync } from "fs";
import { tmpdir } from "os";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

const BOT_FRAMEWORK_AUDIENCE = "https://api.botframework.com";

export const WORKFLOW_API_VERSION = "2019-05-01";

// Resolve relative to this source file (hooks/lib) so callers running from
// different cwds (function-postdeploy from tools/sdk-ai-bots/, the CLI script
// from deployment/) all locate the same on-disk workflow JSON.
const __dirname = dirname(fileURLToPath(import.meta.url));
export const WORKFLOW_DEFINITION_JSON_DEFAULT = resolve(
  __dirname,
  "../../infra/layers/logic-app/workflowDefinition.json",
);

export interface PatchWorkflowOptions {
  /** Log prefix used to distinguish this call in mixed output. */
  logPrefix?: string;
  /**
   * Override the workflow definition JSON path. Defaults to the on-disk
    * template shipped with the Logic App layer.
   */
  workflowDefinitionPath?: string;
}

interface WorkflowTarget {
  name: string;
  teamsGroupId: string;
  teamsChannelIds: string[];
}

function requireEnv(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`Required env var '${name}' is not set — cannot PATCH Logic App workflow.`);
  }
  return value;
}

function armResourceId(subscriptionId: string, resourceGroup: string, provider: string, type: string, name: string): string {
  return `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/${provider}/${type}/${name}`;
}

function managedApiId(subscriptionId: string, location: string, connector: string): string {
  return `/subscriptions/${subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/${connector}`;
}

function readTeamsConnectionStatus(
  subscriptionId: string,
  resourceGroup: string,
  connectionName: string,
): string | undefined {
  try {
    return execSync(
      `az resource show --subscription "${subscriptionId}" --resource-group "${resourceGroup}" ` +
        `--resource-type Microsoft.Web/connections --name "${connectionName}" ` +
        `--query "properties.statuses[0].status" -o tsv`,
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
    ).trim();
  } catch (error) {
    const message = error instanceof Error && "stderr" in error
      ? String((error as Error & { stderr?: unknown }).stderr ?? error.message)
      : String(error);
    if (/ResourceNotFound|could not be found|was not found/i.test(message)) return undefined;
    throw new Error(`Failed to read Teams connection '${connectionName}': ${message.split("\n")[0]}`);
  }
}

export async function patchWorkflow(opts: PatchWorkflowOptions = {}): Promise<void> {
  const prefix = opts.logPrefix ?? "[patch-workflow]";
  const log = (msg: string): void => console.log(`${prefix} ${msg}`);

  // Global azd env context.
  const subscriptionId = requireEnv("AZURE_SUBSCRIPTION_ID");
  const resourceGroup = requireEnv("AZURE_RESOURCE_GROUP");
  const location = requireEnv("AZURE_LOCATION");

  // Resource / identity names emitted by main.bicep outputs.
  const teamsConnName = requireEnv("TEAMS_CONNECTION_NAME");

  const serverIdentityName = requireEnv("MANAGED_IDENTITY_NAME");
  const botIdentityName = requireEnv("FRONTEND_SITE_NAME");

  const teamsConnectionStatus = readTeamsConnectionStatus(
    subscriptionId,
    resourceGroup,
    teamsConnName,
  );
  const teamsConnected = teamsConnectionStatus === "Connected";
  if (!teamsConnected) {
    log(
      `Teams connection '${teamsConnName}' is '${teamsConnectionStatus || "not-found"}'. ` +
        "Deploying the full workflow in Disabled state until OAuth consent is complete.",
    );
  }

  const parseChannelIds = (name: string): string[] => requireEnv(name)
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
  const workflowTargets: WorkflowTarget[] = [
    {
      name: requireEnv("LOGIC_APP_WORKFLOW_NAME"),
      teamsGroupId: requireEnv("TEAMS_GROUP_ID"),
      teamsChannelIds: parseChannelIds("TEAMS_CHANNEL_IDS"),
    },
  ];
  const azureMcpTargetValues = [
    process.env.AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME?.trim(),
    process.env.AZURE_MCP_TEAMS_GROUP_ID?.trim(),
    process.env.AZURE_MCP_TEAMS_CHANNEL_IDS?.trim(),
  ];
  if (azureMcpTargetValues.some(Boolean)) {
    if (!azureMcpTargetValues.every(Boolean)) {
      throw new Error(
        "AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME, AZURE_MCP_TEAMS_GROUP_ID, and " +
          "AZURE_MCP_TEAMS_CHANNEL_IDS must be set together.",
      );
    }
    workflowTargets.push({
      name: requireEnv("AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME"),
      teamsGroupId: requireEnv("AZURE_MCP_TEAMS_GROUP_ID"),
      teamsChannelIds: parseChannelIds("AZURE_MCP_TEAMS_CHANNEL_IDS"),
    });
  }

  // Shared workflow parameter values.
  const serverBaseUrl = requireEnv("SERVER_BASE_URL");
  const serverApplicationIdUri = requireEnv("SERVER_APPLICATION_ID_URI");
  const botBaseUrl = `https://${requireEnv("BOT_DOMAIN")}`;
  const botAudience = BOT_FRAMEWORK_AUDIENCE;

  // Derived resource IDs.
  const serverIdentityResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.ManagedIdentity", "userAssignedIdentities", serverIdentityName);
  const botIdentityResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.ManagedIdentity", "userAssignedIdentities", botIdentityName);
  const teamsConnResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.Web", "connections", teamsConnName);

  const definitionPath = opts.workflowDefinitionPath ?? WORKFLOW_DEFINITION_JSON_DEFAULT;
  const definition = JSON.parse(readFileSync(definitionPath, "utf8"));

  for (const target of workflowTargets) {
    const parameters = {
      $connections: {
        value: {
          teams: {
            connectionId: teamsConnResourceId,
            connectionName: teamsConnName,
            connectionProperties: {},
            id: managedApiId(subscriptionId, location, "teams"),
          },
        },
      },
      teamsGroupId: { value: target.teamsGroupId },
      teamsChannelIds: { value: target.teamsChannelIds },
      serverBaseUrl: { value: serverBaseUrl },
      serverApplicationIdUri: { value: serverApplicationIdUri },
      serverIdentityResourceId: { value: serverIdentityResourceId },
      botBaseUrl: { value: botBaseUrl },
      botAudience: { value: botAudience },
      botIdentityResourceId: { value: botIdentityResourceId },
    };

    const workflowUrl =
      `https://management.azure.com/subscriptions/${subscriptionId}` +
      `/resourceGroups/${resourceGroup}/providers/Microsoft.Logic/workflows/${target.name}` +
      `?api-version=${WORKFLOW_API_VERSION}`;

    // Logic Apps reject PATCH on any properties field (only tags can be
    // patched). GET the current workflow, mutate definition + parameters, then
    // PUT it back so the identity / integration account / tags set up by
    // main.bicep survive. State is set explicitly from Teams OAuth readiness.
    log(`GET workflow ${target.name}...`);
    const currentRaw = execSync(`az rest --method GET --url "${workflowUrl}"`, { encoding: "utf8" });
    const current = JSON.parse(currentRaw);

    current.properties = current.properties ?? {};
    current.properties.definition = definition;
    current.properties.parameters = parameters;
    current.properties.state = teamsConnected ? "Enabled" : "Disabled";

    // Fields ARM does not accept on PUT — strip them.
    delete current.properties.provisioningState;
    delete current.properties.createdTime;
    delete current.properties.changedTime;
    delete current.properties.version;
    delete current.properties.accessEndpoint;
    delete current.properties.endpointsConfiguration;

    // Route the body through a temp file — the workflow JSON is several KB
    // and would blow past cmd.exe's inline argument limit on Windows.
    const tmpDir = mkdtempSync(join(tmpdir(), "logicapp-put-"));
    const bodyPath = join(tmpDir, "workflow.json");
    writeFileSync(bodyPath, JSON.stringify(current));

    log(`PUT workflow ${target.name} (definition + parameters)...`);
    execSync(
      `az rest --method PUT --url "${workflowUrl}" --body @"${bodyPath}" --headers "Content-Type=application/json"`,
      { stdio: "inherit" },
    );
    log(`  ✓ Workflow updated in ${current.properties.state} state.`);
  }
}
