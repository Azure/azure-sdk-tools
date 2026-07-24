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
import { randomUUID } from "crypto";
import * as fs from "fs";
import * as os from "os";
import * as path from "path";

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

/** Microsoft Azure CLI public client — pre-authorized so `az account
 *  get-access-token` / local dev can call the server without a consent prompt. */
const AZURE_CLI_CLIENT_ID = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

/**
 * Returns true if an Application registration with this appId can be resolved
 * in the signed-in tenant. Microsoft Graph rejects the entire
 * `preAuthorizedApplications` PATCH with `InvalidAppId` if any referenced
 * client appId is not a resolvable *Application* — a bare service principal
 * (e.g. a ManagedIdentity, or an enterprise app whose registration lives in
 * another tenant) is NOT valid here. So we deliberately check for the
 * Application object only and do not fall back to `az ad sp show`; callers must
 * filter unresolvable ids out before writing.
 */
function clientAppResolvable(clientId: string): boolean {
  try {
    return !!run(`az ad app show --id ${clientId} --query appId --output tsv`);
  } catch {
    return false;
  }
}

interface OAuth2Scope {
  id: string;
  value: string;
  isEnabled: boolean;
  type?: string;
  adminConsentDisplayName?: string;
  adminConsentDescription?: string;
  userConsentDisplayName?: string;
  userConsentDescription?: string;
}
interface PreAuthorizedApp {
  appId: string;
  delegatedPermissionIds: string[];
}
interface ApplicationApi {
  requestedAccessTokenVersion?: number | null;
  oauth2PermissionScopes?: OAuth2Scope[];
  preAuthorizedApplications?: PreAuthorizedApp[];
  [k: string]: unknown;
}
interface AppRole {
  id: string;
  value: string;
  isEnabled: boolean;
  allowedMemberTypes: string[];
  displayName: string;
  description: string;
}

export interface EnsureServerAppAuthorizationOptions {
  /** The server app registration's appId (a.k.a. SERVER_AUDIENCE). */
  appId: string;
  /** Identifier URI to expose. Defaults to `api://<appId>` (matches EasyAuth audience). */
  identifierUri?: string;
  /** Delegated (user) scope to expose. Default 'access_as_user'. */
  delegatedScopeValue?: string;
  /** Application app-role to expose for daemon / managed-identity callers. Default 'access_as_application'. */
  appRoleValue?: string;
  /** Client appIds to pre-authorize on the delegated scope. Azure CLI is always included. */
  preAuthorizeClientIds?: string[];
  /** Managed-identity service-principal objectId to grant the application app-role. */
  managedIdentityPrincipalId?: string;
}

/** Run an `az rest` call against Microsoft Graph, passing any body via a temp file. */
function azRest(method: string, url: string, body?: unknown): string {
  if (body === undefined) {
    return run(`az rest --method ${method} --url "${url}"`);
  }
  const tmp = path.join(os.tmpdir(), `graph-${randomUUID()}.json`);
  fs.writeFileSync(tmp, JSON.stringify(body));
  try {
    return run(
      `az rest --method ${method} --url "${url}" ` +
        `--headers "Content-Type=application/json" --body @${tmp}`,
    );
  } finally {
    try {
      fs.unlinkSync(tmp);
    } catch {
      /* best-effort cleanup */
    }
  }
}

/**
 * Idempotently configure the server app registration so callers can obtain
 * tokens that App Service EasyAuth accepts. Each step is a no-op when already
 * applied, so this is safe to run on every deploy:
 *
 *   1. service principal exists — required before Entra will issue any token
 *      whose audience is this app (without it: AADSTS9002313 / 500011);
 *   2. identifier URI set — the token audience EasyAuth validates;
 *   3. delegated scope exposed — for user / Azure-CLI (local) callers;
 *   4. application app-role exposed — for daemon / managed-identity callers;
 *   5. selected client apps pre-authorized on the delegated scope;
 *   6. the managed identity granted the application app-role.
 *
 * Requires the caller to be logged in via `az login` with permission to manage
 * the Application / ServicePrincipal objects and app-role assignments.
 */
export function ensureServerAppAuthorization(opts: EnsureServerAppAuthorizationOptions): void {
  const {
    appId,
    identifierUri = `api://${appId}`,
    delegatedScopeValue = "access_as_user",
    appRoleValue = "access_as_application",
    preAuthorizeClientIds = [],
    managedIdentityPrincipalId,
  } = opts;

  const graphApp = `https://graph.microsoft.com/v1.0/applications(appId='${appId}')`;
  log(`Ensuring authorization for server app ${appId}`);

  // 1. Service principal ----------------------------------------------------
  let spId = "";
  try {
    spId = run(`az ad sp show --id ${appId} --query id --output tsv`);
    log(`  ✓ service principal exists (${spId})`);
  } catch {
    log(`  creating service principal for ${appId}`);
    run(`az ad sp create --id ${appId}`);
    spId = run(`az ad sp show --id ${appId} --query id --output tsv`);
    log(`  ✓ service principal created (${spId})`);
  }

  // Snapshot the current app registration once, then read-modify-write.
  const app = JSON.parse(
    run(
      `az ad app show --id ${appId} ` +
        `--query "{identifierUris:identifierUris, api:api, appRoles:appRoles}" --output json`,
    ),
  ) as { identifierUris: string[] | null; api: ApplicationApi | null; appRoles: AppRole[] | null };

  const identifierUris = app.identifierUris ?? [];
  const api: ApplicationApi = app.api ?? {};
  api.oauth2PermissionScopes = api.oauth2PermissionScopes ?? [];
  api.preAuthorizedApplications = api.preAuthorizedApplications ?? [];
  const appRoles = app.appRoles ?? [];

  // 2. Identifier URI -------------------------------------------------------
  if (!identifierUris.includes(identifierUri)) {
    log(`  setting identifier URI ${identifierUri}`);
    run(`az ad app update --id ${appId} --identifier-uris "${identifierUri}"`);
  } else {
    log(`  ✓ identifier URI already set (${identifierUri})`);
  }

  // 3 + 5. Delegated scope and pre-authorized clients (both live under `api`,
  // so mutate the whole object and PATCH once to avoid clobbering siblings).
  let apiChanged = false;
  let scope = api.oauth2PermissionScopes.find((s) => s.value === delegatedScopeValue);
  if (!scope) {
    scope = {
      id: randomUUID(),
      value: delegatedScopeValue,
      type: "User",
      isEnabled: true,
      adminConsentDisplayName: "Access Azure SDK QA agent as the signed-in user",
      adminConsentDescription:
        "Allows the app to call the Azure SDK QA agent server on behalf of the signed-in user.",
      userConsentDisplayName: "Access Azure SDK QA agent on your behalf",
      userConsentDescription:
        "Allows the app to call the Azure SDK QA agent server on your behalf.",
    };
    api.oauth2PermissionScopes.push(scope);
    apiChanged = true;
    log(`  exposing delegated scope '${delegatedScopeValue}' (${scope.id})`);
  } else {
    log(`  ✓ delegated scope '${delegatedScopeValue}' already exposed (${scope.id})`);
  }

  const clientsToAuthorize = Array.from(
    new Set([AZURE_CLI_CLIENT_ID, ...preAuthorizeClientIds]),
  ).filter(Boolean).filter((clientId) => {
    // Azure CLI is a well-known Microsoft first-party client present in every
    // tenant; never probe or skip it. For any other client, skip (with a
    // warning) when no app/service principal resolves in this tenant — a
    // dangling reference would make Graph reject the whole api PATCH with
    // InvalidAppId and fail the deploy.
    if (clientId === AZURE_CLI_CLIENT_ID) return true;
    if (clientAppResolvable(clientId)) return true;
    log(`  ⚠ skipping pre-authorization of ${clientId} — no app/service principal found in this tenant`);
    return false;
  });
  for (const clientId of clientsToAuthorize) {
    const entry = api.preAuthorizedApplications.find((p) => p.appId === clientId);
    if (!entry) {
      api.preAuthorizedApplications.push({ appId: clientId, delegatedPermissionIds: [scope.id] });
      apiChanged = true;
      log(`  pre-authorizing client ${clientId}`);
    } else if (!entry.delegatedPermissionIds.includes(scope.id)) {
      entry.delegatedPermissionIds.push(scope.id);
      apiChanged = true;
      log(`  pre-authorizing client ${clientId} (adding scope)`);
    }
  }

  // Prune any pre-authorized client that no longer resolves in the directory.
  // The PATCH below resends the ENTIRE preAuthorizedApplications array, and
  // Graph rejects the whole api PATCH with InvalidAppId if any referenced appId
  // is dangling — including entries that were valid when first added but have
  // since been deleted from the tenant. Azure CLI is a well-known first-party
  // client and is never pruned.
  const preAuthBefore = api.preAuthorizedApplications.length;
  api.preAuthorizedApplications = api.preAuthorizedApplications.filter((p) => {
    if (p.appId === AZURE_CLI_CLIENT_ID) return true;
    if (clientAppResolvable(p.appId)) return true;
    log(`  ⚠ pruning stale pre-authorized app ${p.appId} — not found in this tenant`);
    return false;
  });
  if (api.preAuthorizedApplications.length !== preAuthBefore) {
    apiChanged = true;
  }

  if (apiChanged) {
    azRest("PATCH", graphApp, { api });
    log(`  ✓ api (scopes + pre-authorized apps) updated`);
  }

  // 4. Application app-role -------------------------------------------------
  let role = appRoles.find((r) => r.value === appRoleValue);
  if (!role) {
    role = {
      id: randomUUID(),
      value: appRoleValue,
      isEnabled: true,
      allowedMemberTypes: ["Application"],
      displayName: "Access Azure SDK QA agent as an application",
      description: "Allows a daemon / managed-identity app to call the Azure SDK QA agent server.",
    };
    appRoles.push(role);
    azRest("PATCH", graphApp, { appRoles });
    log(`  exposing application app-role '${appRoleValue}' (${role.id})`);
  } else {
    log(`  ✓ application app-role '${appRoleValue}' already exposed (${role.id})`);
  }

  // 6. Grant the managed identity the application app-role ------------------
  if (managedIdentityPrincipalId) {
    let assigned: string[] = [];
    try {
      assigned = JSON.parse(
        run(
          `az rest --method GET ` +
            `--url "https://graph.microsoft.com/v1.0/servicePrincipals/${managedIdentityPrincipalId}/appRoleAssignments" ` +
            `--query "value[].appRoleId" --output json`,
        ) || "[]",
      );
    } catch {
      assigned = [];
    }
    if (!assigned.includes(role.id)) {
      log(`  granting managed identity ${managedIdentityPrincipalId} app-role '${appRoleValue}'`);
      azRest(
        "POST",
        `https://graph.microsoft.com/v1.0/servicePrincipals/${managedIdentityPrincipalId}/appRoleAssignments`,
        { principalId: managedIdentityPrincipalId, resourceId: spId, appRoleId: role.id },
      );
      log(`  ✓ app-role granted (may take a few minutes to propagate)`);
    } else {
      log(`  ✓ managed identity already has app-role '${appRoleValue}'`);
    }
  }

  log(`  ✓ server app authorization ensured for ${appId}`);
}
