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
  const { displayName, signInAudience = "AzureADMyOrg" } = opts;

  log(`Looking up existing app registration '${displayName}'`);
  const existing = run(
    `az ad app list --display-name "${displayName}" --query "[0].appId" --output tsv`,
  );
  if (existing) {
    log(`  ✓ found existing appId=${existing}`);
    return existing;
  }

  log(`Creating Entra app registration '${displayName}' (sign-in audience: ${signInAudience})`);
  const appId = run(
    `az ad app create --display-name "${displayName}" --sign-in-audience ${signInAudience} --query appId --output tsv`,
  );
  if (!appId) {
    throw new Error(`az ad app create returned no appId for '${displayName}'`);
  }
  log(`  ✓ created appId=${appId}`);
  return appId;
}
