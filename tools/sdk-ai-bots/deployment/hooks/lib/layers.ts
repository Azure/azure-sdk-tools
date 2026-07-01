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
  /** Optional hook that runs before `az deployment group create`. */
  pre?: (ctx: LayerContext) => Promise<void>;
  /** Optional hook that runs after `az deployment group create` succeeds. */
  post?: (ctx: LayerContext) => Promise<void>;
}

const BICEP_BASE = "infra/modules";

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
    pre: async (_ctx) => {
      // TODO: Confirm prerequisite resources (ACR, Storage, Vault) already exist.
    },
    post: async (_ctx) => {
      // TODO: Validate that role assignments were applied correctly.
    },
  },

  // ── Layer 4: Logic App ────────────────────────────────────────────────────
  // frontend (Layer 3) and function-app (Layer 5 in the diagram) are azd
  // services and are deployed via `azd deploy`, not this pipeline.
  {
    name: "logic-app",
    bicepFile: `${BICEP_BASE}/qaBotLogicApp/logicAppResources.bicep`,
    pre: async (_ctx) => {
      // TODO: Verify the storage account connection string is available
      // for the 'azureblob' managed API connection.
    },
    post: async (_ctx) => {
      // TODO: Authorise the Teams and Azure Blob managed API connections.
      // These require an OAuth consent flow that cannot be automated in Bicep.
      // Print the consent URLs so an operator can complete them once.
    },
  },
];
