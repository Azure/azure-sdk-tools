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
  /**
   * If set, before falling back to creation the function first searches app
   * registrations owned by the currently signed-in user (`az ad app list
   * --show-mine`) for one whose displayName contains this substring
   * (case-insensitive). If exactly one match is found, its appId is reused.
   * Lets developers reuse a pre-existing app registration when the tenant
   * blocks new app creation (e.g. missing ServiceManagementReference).
   */
  ownedDisplayNameContains?: string;
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
  const {
    displayName,
    signInAudience = "AzureADMyOrg",
    serviceManagementReference,
    ownedDisplayNameContains,
  } = opts;

  log(`Looking up existing app registration '${displayName}'`);
  const existing = run(
    `az ad app list --display-name "${displayName}" --query "[0].appId" --output tsv`,
  );
  if (existing) {
    log(`  ✓ found existing appId=${existing}`);
    return existing;
  }

  if (ownedDisplayNameContains) {
    const needle = ownedDisplayNameContains.toLowerCase();
    log(`  no exact match — searching apps you own for displayName containing '${needle}'`);
    const ownedJson = run(
      `az ad app list --show-mine --query "[].{appId:appId, displayName:displayName}" --output json`,
    );
    let owned: Array<{ appId: string; displayName: string }> = [];
    try {
      owned = JSON.parse(ownedJson || "[]");
    } catch {
      owned = [];
    }
    const matches = owned.filter((a) =>
      (a.displayName ?? "").toLowerCase().includes(needle),
    );
    if (matches.length === 1) {
      const m = matches[0];
      log(`  ✓ reusing owned appId=${m.appId} (displayName='${m.displayName}')`);
      return m.appId;
    }
    if (matches.length > 1) {
      const list = matches.map((m) => `    - ${m.displayName} (${m.appId})`).join("\n");
      throw new Error(
        `Multiple app registrations you own match '${needle}':\n${list}\n` +
        `Refusing to guess. Set SERVER_AUDIENCE=<appId> and re-run, ` +
        `or narrow the filter passed to ensureEntraApp.`,
      );
    }
    log(`  no owned apps matched '${needle}' — will attempt to create '${displayName}'`);
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
