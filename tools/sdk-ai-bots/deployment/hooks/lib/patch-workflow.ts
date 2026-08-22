/**
 * patch-workflow — apply the real `definition` and `parameters` to a Logic
 * App workflow whose shell was created by `azd provision`.
 *
 * `Microsoft.Logic/workflows` does NOT accept PATCH on any properties field
 * (`PatchWorkflowPropertiesNotSupported`) — only top-level tags can be
 * patched. Instead we GET the current resource, mutate its
 * `properties.definition` and `properties.parameters` in place, and PUT it
 * back. That preserves the identity, integration account, state, tags, etc.
 * set up by main.bicep.
 *
 * Callers (function-postdeploy.ts, scripts/deploy-logic-app.ts) populate
 * process.env with the azd env values before invoking.
 */

import { execSync } from "child_process";
import { mkdtempSync, readFileSync, writeFileSync } from "fs";
import { tmpdir } from "os";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

export const WORKFLOW_API_VERSION = "2019-05-01";

// Resolve relative to this source file (hooks/lib) so callers running from
// different cwds (function-postdeploy from tools/sdk-ai-bots/, the CLI script
// from deployment/) all locate the same on-disk workflow JSON.
const __dirname = dirname(fileURLToPath(import.meta.url));
export const WORKFLOW_DEFINITION_JSON_DEFAULT = resolve(
  __dirname,
  "../../infra/modules/qaBotLogicApp/workflowDefinition.json",
);

export interface PatchWorkflowOptions {
  /** Log prefix used to distinguish this call in mixed output. */
  logPrefix?: string;
  /**
   * Override the workflow definition JSON path. Defaults to the on-disk
   * template shipped with the module.
   */
  workflowDefinitionPath?: string;
  /**
   * How long to wait for the Function App host to become healthy before
   * giving up (ms). The workflow's `function.id` reference is validated
   * against this host at write time, so we must confirm it is Running AND
   * serving /admin/host/status = 200 before attempting the PUT. Defaults
   * to 5 minutes. Set to 0 to skip the readiness wait entirely (only safe
   * from function-postdeploy where the deploy already succeeded).
   */
  functionHostReadyTimeoutMs?: number;
  /**
   * When true, skip the Function App readiness gate. Only set this from
   * contexts that have already confirmed the container is live (e.g. the
   * function-postdeploy hook that runs immediately after `azd deploy`).
   */
  skipFunctionHostCheck?: boolean;
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

function tryRun(cmd: string): string | undefined {
  try {
    return execSync(cmd, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
  } catch {
    return undefined;
  }
}

async function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/**
 * Enforce the Function-App-first dependency: ARM validates the workflow's
 * `function.id` reference (which points to convertActivity inside the
 * container) against the Function App host at write time. The host only
 * serves `/admin/host/status = 200` once the container image is pulled and
 * the functions runtime is loaded — which happens on the first successful
 * `azd deploy function-app`. Callers should run this preflight before
 * PUTting the workflow so we fail with an actionable hint rather than an
 * opaque `ServiceUnavailable from host runtime` from the PUT response.
 */
async function waitForFunctionAppReady(
  functionAppName: string,
  resourceGroup: string,
  log: (msg: string) => void,
  timeoutMs: number,
): Promise<void> {
  // State check first — cheap, catches "app doesn't exist / not provisioned"
  // right away.
  const state = tryRun(
    `az functionapp show --name "${functionAppName}" --resource-group "${resourceGroup}" --query state -o tsv`,
  );
  if (!state) {
    throw new Error(
      `Function App '${functionAppName}' in RG '${resourceGroup}' not found. ` +
        `Run \`azd provision\` first to create it, then \`azd deploy function-app\`.`,
    );
  }
  if (state !== "Running") {
    throw new Error(
      `Function App '${functionAppName}' state is '${state}' (expected Running). ` +
        `Fix the app in the portal or re-run \`azd deploy function-app\`.`,
    );
  }

  const defaultHostname = tryRun(
    `az functionapp show --name "${functionAppName}" --resource-group "${resourceGroup}" --query defaultHostName -o tsv`,
  );
  if (!defaultHostname) {
    throw new Error(`Could not read defaultHostName for Function App '${functionAppName}'.`);
  }

  const url = `https://${defaultHostname}/admin/host/status`;
  log(`Waiting for function host at ${url} (Logic App PUT depends on convertActivity being loaded)...`);

  const deadline = Date.now() + timeoutMs;
  let attempt = 0;
  let lastCode = "0";
  while (Date.now() < deadline) {
    attempt++;
    lastCode = tryRun(`curl -s -o /dev/null -w "%{http_code}" --max-time 15 "${url}"`) ?? "0";
    // 200 = healthy with admin access. 401/403 also mean the Functions host is
    // up and loaded — /admin/host/status requires the master key, so an
    // unauthenticated probe is rejected with 401/403 only once the runtime is
    // serving requests. A host that hasn't pulled its container yet returns 503
    // (or the connection fails), so treat 401/403 as ready too.
    if (lastCode === "200" || lastCode === "401" || lastCode === "403") {
      log(`  ✓ Function host healthy after ${attempt} attempt(s) (status ${lastCode}).`);
      return;
    }
    log(`  attempt ${attempt}: /admin/host/status returned ${lastCode} — retrying in 15s`);
    await sleep(15_000);
  }
  throw new Error(
    `Function host at ${url} did not return 200/401/403 within ${Math.round(timeoutMs / 1000)}s ` +
      `(last status: ${lastCode}). The container image is probably not deployed yet — ` +
      `run \`azd deploy function-app -e <env>\` first, then retry this step.`,
  );
}

export async function patchWorkflow(opts: PatchWorkflowOptions = {}): Promise<void> {
  const prefix = opts.logPrefix ?? "[patch-workflow]";
  const log = (msg: string): void => console.log(`${prefix} ${msg}`);

  // Global azd env context.
  const subscriptionId = requireEnv("AZURE_SUBSCRIPTION_ID");
  const resourceGroup = requireEnv("AZURE_RESOURCE_GROUP");
  const location = requireEnv("AZURE_LOCATION");

  // Resource / identity names emitted by main.bicep outputs.
  const workflowName = requireEnv("LOGIC_APP_WORKFLOW_NAME");
  const teamsConnName = requireEnv("TEAMS_CONNECTION_NAME");
  const blobConnName = requireEnv("AZURE_BLOB_CONNECTION_NAME");
  const docDbConnName = requireEnv("DOCUMENT_DB_CONNECTION_NAME");

  const serverIdentityName = requireEnv("MANAGED_IDENTITY_NAME");
  const botIdentityName = requireEnv("BOT_IDENTITY_NAME");
  const functionAppName = requireEnv("FUNCTION_APP_NAME");

  // Enforce the function-app-first dependency before touching the workflow.
  if (!opts.skipFunctionHostCheck) {
    await waitForFunctionAppReady(
      functionAppName,
      resourceGroup,
      log,
      opts.functionHostReadyTimeoutMs ?? 5 * 60 * 1000,
    );
  }

  // Workflow parameter values.
  const teamsGroupId = requireEnv("TEAMS_GROUP_ID");
  const teamsChannelIds = requireEnv("TEAMS_CHANNEL_IDS")
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
  const serverBaseUrl = requireEnv("SERVER_BASE_URL");
  const serverAudience = requireEnv("SERVER_AUDIENCE");
  const botBaseUrl = requireEnv("BOT_BASE_URL");
  const botAudience = requireEnv("BOT_AUDIENCE");
  const blobStorageAccountName = requireEnv("STORAGE_ACCOUNT_NAME");

  // Derived resource IDs.
  const serverIdentityResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.ManagedIdentity", "userAssignedIdentities", serverIdentityName);
  const botIdentityResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.ManagedIdentity", "userAssignedIdentities", botIdentityName);
  const functionAppResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.Web", "sites", functionAppName);
  const teamsConnResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.Web", "connections", teamsConnName);
  const blobConnResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.Web", "connections", blobConnName);
  const docDbConnResourceId = armResourceId(subscriptionId, resourceGroup, "Microsoft.Web", "connections", docDbConnName);

  // Load and templatize the workflow definition. `function.id` is resolved at
  // PATCH time (ARM rejects `@{parameters(...)}` there); the rest of the
  // definition continues to reference workflow-runtime parameters that we
  // populate below.
  const definitionPath = opts.workflowDefinitionPath ?? WORKFLOW_DEFINITION_JSON_DEFAULT;
  const rawDefinition = readFileSync(definitionPath, "utf8").replace(
    /@\{parameters\('functionAppResourceId'\)\}/g,
    functionAppResourceId,
  );
  const definition = JSON.parse(rawDefinition);

  const parameters = {
    $connections: {
      // Keyed by the connector token (teams / azureblob / documentdb) — the
      // static, designer-friendly form the portal Logic App designer expects.
      // The connectionId / id still point at the real connection resources, so
      // runtime behavior is identical to keying by resource name.
      value: {
        azureblob: {
          connectionId: blobConnResourceId,
          connectionName: blobConnName,
          connectionProperties: {
            authentication: {
              identity: serverIdentityResourceId,
              type: "ManagedServiceIdentity",
            },
          },
          id: managedApiId(subscriptionId, location, "azureblob"),
        },
        documentdb: {
          connectionId: docDbConnResourceId,
          connectionName: docDbConnName,
          connectionProperties: {
            authentication: {
              identity: serverIdentityResourceId,
              type: "ManagedServiceIdentity",
            },
          },
          id: managedApiId(subscriptionId, location, "documentdb"),
        },
        teams: {
          connectionId: teamsConnResourceId,
          connectionName: teamsConnName,
          connectionProperties: {},
          id: managedApiId(subscriptionId, location, "teams"),
        },
      },
    },
    teamsGroupId: { value: teamsGroupId },
    teamsChannelIds: { value: teamsChannelIds },
    serverBaseUrl: { value: serverBaseUrl },
    serverAudience: { value: serverAudience },
    serverIdentityResourceId: { value: serverIdentityResourceId },
    botBaseUrl: { value: botBaseUrl },
    botAudience: { value: botAudience },
    botIdentityResourceId: { value: botIdentityResourceId },
    functionAppResourceId: { value: functionAppResourceId },
    blobStorageAccountName: { value: blobStorageAccountName },
  };

  const workflowUrl =
    `https://management.azure.com/subscriptions/${subscriptionId}` +
    `/resourceGroups/${resourceGroup}/providers/Microsoft.Logic/workflows/${workflowName}` +
    `?api-version=${WORKFLOW_API_VERSION}`;

  // Logic Apps reject PATCH on any properties field (only tags can be
  // patched). GET the current workflow, mutate definition + parameters, then
  // PUT it back so the identity / integration account / state / tags set up
  // by main.bicep survive.
  log(`GET workflow ${workflowName}...`);
  const currentRaw = execSync(`az rest --method GET --url "${workflowUrl}"`, { encoding: "utf8" });
  const current = JSON.parse(currentRaw);

  current.properties = current.properties ?? {};
  current.properties.definition = definition;
  current.properties.parameters = parameters;

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

  log(`PUT workflow ${workflowName} (definition + parameters)...`);
  execSync(
    `az rest --method PUT --url "${workflowUrl}" --body @"${bodyPath}" --headers "Content-Type=application/json"`,
    { stdio: "inherit" },
  );
  log("  ✓ Workflow updated.");
}
