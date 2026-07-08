/**
 * Infra-only layer pipeline for the sdk-ai-bots deployment.
 *
 * Ported from ../../../azd-experiments/hooks/lib/layers.ts. The only change is
 * the BICEP_BASE path, which now points at this repo's `infra/modules/`.
 *
 * These layers contain no application code and are managed through azd's
 * `postprovision` hook (azd services are handled separately by `azd deploy`).
 *
 * Partial deployment:
 *   DEPLOY_LAYER=agent-platform azd provision   — only that one layer
 */

export interface LayerContext {
  resourceGroup: string;
  subscriptionId: string;
  location: string;
}

export interface Layer {
  /** Unique identifier — also the az deployment name. */
  name: string;
  /** Path to the .bicep file, relative to the deployment/ folder. */
  bicepFile: string;
  /**
   * Parameters passed to `az deployment group create --parameters key=value`.
   * Values are sourced from process.env, populated by azd from main.bicep
   * outputs (see main.bicep). A missing key throws before the deployment runs.
   */
  params?: () => Record<string, string>;
  /** Optional hook that runs before `az deployment group create`. */
  pre?: (ctx: LayerContext) => Promise<void>;
  /** Optional hook that runs after `az deployment group create` succeeds. */
  post?: (ctx: LayerContext) => Promise<void>;
}

const BICEP_BASE = "deployment/infra/modules";

/** Reads a required env var; throws with the layer name if missing. */
function req(layer: string, name: string): string {
  const v = process.env[name];
  if (!v) {
    throw new Error(
      `[${layer}] required env var '${name}' is not set. ` +
        `Run \`azd provision\` (full graph) once so main.bicep outputs are captured into .azure/<env>/.env.`
    );
  }
  return v;
}

export const INFRA_LAYERS: Layer[] = [
  // ── Layer 1: Shared resources ──────────────────────────────────────────────
  {
    name: "shared-resources",
    bicepFile: `${BICEP_BASE}/qaBotSharedResources/sharedResources.bicep`,
    pre: async (_ctx) => {
      // TODO: Check Key Vault name (per environment-suite) is not soft-deleted.
      //   az keyvault list-deleted --query "[?name=='<vault>']"
    },
    post: async (_ctx) => {
      // TODO: Capture and store the managed identity principalId for downstream layers.
    },
  },

  // ── Layer 2: Agent platform / AI services ─────────────────────────────────
  {
    name: "agent-platform",
    bicepFile: `${BICEP_BASE}/qaBotAgent/component.bicep`,
    params: () => ({
      managedIdentityPrincipalId: req("agent-platform", "MANAGED_IDENTITY_PRINCIPAL_ID"),
      storageAccountName:         req("agent-platform", "STORAGE_ACCOUNT_NAME"),
      storageBlobEndpoint:        req("agent-platform", "STORAGE_BLOB_ENDPOINT"),
    }),
    pre: async (_ctx) => {
      // TODO: Verify Cognitive Services quota in the target region is sufficient.
    },
    post: async (_ctx) => {
      // TODO: Poll until all model deployments reach 'Running' state.
    },
  },

  // ── Layer 3: Backend role assignments + slot ──────────────────────────────
  {
    name: "backend",
    bicepFile: `${BICEP_BASE}/qaBotBackend/serverfarm.bicep`,
    params: () => ({
      location:               req("backend", "AZURE_LOCATION"),
      ragBasedBackendImage:   req("backend", "RAG_BASED_BACKEND_IMAGE"),
      agentBasedBackendImage: req("backend", "AGENT_BASED_BACKEND_IMAGE"),
      managedIdentityClientId: req("backend", "MANAGED_IDENTITY_CLIENT_ID"),
      serverAudience:         req("backend", "SERVER_AUDIENCE"),
      sharedIdentityName:     req("backend", "MANAGED_IDENTITY_NAME"),
      frontendIdentityName:   req("backend", "BOT_IDENTITY_NAME"),
      aiResourceName:         req("backend", "AI_RESOURCE_NAME"),
      aiProjectName:          req("backend", "AI_PROJECT_NAME"),
      searchServiceName:      req("backend", "SEARCH_SERVICE_NAME"),
      cosmosDbAccountName:    req("backend", "COSMOSDB_ACCOUNT_NAME"),
      storageAccountName:     req("backend", "STORAGE_ACCOUNT_NAME"),
      keyVaultName:           req("backend", "KEY_VAULT_NAME"),
      appConfigName:          req("backend", "APP_CONFIG_NAME"),
      actionGroupName:        req("backend", "ACTION_GROUP_NAME"),
    }),
    pre: async (_ctx) => {
      // TODO: Confirm prerequisite resources (ACR, Storage, Vault) already exist.
    },
    post: async (_ctx) => {
      // TODO: Validate that role assignments were applied correctly.
    },
  },

  // ── Layer 4: (Logic App removed) ──────────────────────────────────────────
  // The Logic App workflow references a function inside the Function App
  // container and ARM validates that at deploy time. On the first provision
  // the container isn't pushed yet, so the workflow create fails with
  // "ServiceUnavailable from host runtime". main.bicep creates the workflow
  // shell with an empty definition; hooks/function-postdeploy.ts PATCHes the
  // real definition after the container image is live.

  // ── Frontend (azd service — infra module deployed standalone here) ─────────
  {
    name: "frontend",
    bicepFile: `${BICEP_BASE}/qaBotFrontend/userAssignedIdentity.bicep`,
  },

  // ── Function App (azd service — infra module deployed standalone here) ────
  {
    name: "function-app",
    bicepFile: `${BICEP_BASE}/qaBotFunctionApp/serverfarm.bicep`,
    params: () => ({
      location:                  req("function-app", "AZURE_LOCATION"),
      containerImage:            req("function-app", "FUNCTION_CONTAINER_IMAGE"),
      managedIdentityClientId:   req("function-app", "MANAGED_IDENTITY_CLIENT_ID"),
      storageAccountName:        req("function-app", "STORAGE_ACCOUNT_NAME"),
      managedIdentityResourceId: req("function-app", "MANAGED_IDENTITY_RESOURCE_ID"),
    }),
  },
];
