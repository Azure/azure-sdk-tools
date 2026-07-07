/**
 * ensureEntraApp — idempotently create (or look up) an Entra ID app
 * registration and return its clientId (appId).
 *
 * Used to derive `serverAudience` (the token audience the agent server
 * validates against) before `azd provision` runs the main Bicep template.
 *
 * Requires the caller to be logged in via `az login` with permission to read
 * and create Application objects in the target tenant.
 */

import { execSync } from "child_process";

export type SignInAudience =
  | "AzureADMyOrg"
  | "AzureADMultipleOrgs"
  | "AzureADandPersonalMicrosoftAccount";

export interface EnsureEntraAppOptions {
  displayName: string;
  signInAudience?: SignInAudience;
  /**
   * Service management reference required by some tenants (e.g. Microsoft
   * internal tenants enforce this via Conditional Access policy).
   * Maps to `az ad app create --service-management-reference`.
   * Read from the SERVICE_MANAGEMENT_REFERENCE environment variable by
   * the preprovision hook; set it to bypass the tenant policy error:
   *   export SERVICE_MANAGEMENT_REFERENCE=<your-smr-value>
   */
  serviceManagementReference?: string;
}

function log(msg: string): void {
  console.log(`[entra-app] ${msg}`);
}

function run(cmd: string): string {
  return execSync(cmd, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

/**
 * Returns the appId of an Entra app registration with the given display name,
 * creating one if none exists. Idempotent — safe to call on every provision.
 */
export function ensureEntraApp(opts: EnsureEntraAppOptions): string {
  const { displayName, signInAudience = "AzureADMyOrg", serviceManagementReference } = opts;

  log(`Looking up existing app registration '${displayName}'`);
  const existing = run(
    `az ad app list --display-name "${displayName}" --query "[0].appId" --output tsv`,
  );
  if (existing) {
    log(`  ✓ found existing appId=${existing}`);
    return existing;
  }

  const smrFlag = serviceManagementReference
    ? ` --service-management-reference "${serviceManagementReference}"`
    : "";
  log(`Creating Entra app registration '${displayName}' (sign-in audience: ${signInAudience}${serviceManagementReference ? `, smr: ${serviceManagementReference}` : ""})`);
  let appId: string;
  try {
    appId = run(
      `az ad app create --display-name "${displayName}" --sign-in-audience ${signInAudience}${smrFlag} --query appId --output tsv`,
    );
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : String(err);
    if (msg.includes("ServiceManagementReference") && !serviceManagementReference) {
      throw new Error(
        `Entra app creation failed: this tenant requires a ServiceManagementReference.\n` +
        `Set the SERVICE_MANAGEMENT_REFERENCE environment variable and re-run:\n` +
        `  export SERVICE_MANAGEMENT_REFERENCE=<your-smr-value>\n` +
        `  azd provision\n\n` +
        `See: https://aka.ms/service-management-reference-error`,
      );
    }
    throw err;
  }
  if (!appId) {
    throw new Error(`az ad app create returned no appId for '${displayName}'`);
  }
  log(`  ✓ created appId=${appId}`);
  return appId;
}
