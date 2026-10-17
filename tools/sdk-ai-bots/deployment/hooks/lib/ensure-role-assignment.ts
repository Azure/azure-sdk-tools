/**
 * ensureRoleAssignment — idempotent "create the role assignment if it does not
 * already exist" helper.
 *
 * A native bicep `Microsoft.Authorization/roleAssignments` resource uses a
 * deterministic name (`guid(scope, principal, roleDef)`) and fails the whole
 * deployment with `RoleAssignmentExists` when the same
 * (principalId, roleDefinitionId, scope) tuple already exists under a *different*
 * name — for example one created out-of-band via `az role assignment create`,
 * which Azure names with a random GUID. In a lock-protected resource group that
 * duplicate cannot even be deleted, so bicep can never win.
 *
 * `az role assignment create` does not have that limitation: if the assignment
 * already exists it is a no-op, and we additionally treat `RoleAssignmentExists`
 * as success. That makes this safe to run on every provision and correct on both
 * fresh environments (creates it) and dirty ones (reuses the existing grant).
 */

import { type CommandRunner, runCommand } from "../../scripts/lib/cli.js";

export interface EnsureRoleAssignmentOptions {
  /** Object (principal) ID of the assignee. */
  principalId: string;
  /** Built-in role definition GUID (or role name) to assign. */
  roleDefinitionId: string;
  /** Full ARM resource ID scope the assignment is created at. */
  scope: string;
  /**
   * Assignee principal type. Defaults to ServicePrincipal (managed identities),
   * which also skips a Microsoft Graph lookup that can fail for MSIs.
   */
  principalType?: "ServicePrincipal" | "User" | "Group";
  log?: (msg: string) => void;
}

/**
 * Create the role assignment if it does not already exist. Returns true when the
 * grant is in place afterwards (created now or pre-existing), false only on an
 * unexpected failure.
 */
export function ensureRoleAssignment(
  options: EnsureRoleAssignmentOptions,
  run: CommandRunner = runCommand,
): boolean {
  const { principalId, roleDefinitionId, scope, principalType = "ServicePrincipal" } = options;
  const log = options.log ?? (() => {});

  if (!principalId || !roleDefinitionId || !scope) {
    log(`  – skipping role assignment (missing principalId, role, or scope)`);
    return false;
  }

  try {
    const assignments: unknown = JSON.parse(
      run(
        "az",
        [
          "role",
          "assignment",
          "list",
          "--assignee-object-id",
          principalId,
          "--scope",
          scope,
          "--include-inherited",
          "--fill-principal-name",
          "false",
          "--output",
          "json",
        ],
        { capture: true },
      ),
    );
    if (!Array.isArray(assignments)) throw new Error("Azure returned no role-assignment list.");
    const expectedRole = roleDefinitionId.toLowerCase();
    const exists = assignments.some((assignment) => {
      if (!isRecord(assignment)) throw new Error("Azure returned an invalid role assignment.");
      const assignedRoleId = typeof assignment.roleDefinitionId === "string"
        ? assignment.roleDefinitionId.split("/").pop()?.toLowerCase() : undefined;
      const assignedRoleName = typeof assignment.roleDefinitionName === "string"
        ? assignment.roleDefinitionName.toLowerCase() : undefined;
      if (!assignedRoleId && !assignedRoleName) throw new Error("Role assignment has no role definition.");
      return assignedRoleId === expectedRole || assignedRoleName === expectedRole;
    });
    if (exists) {
      log(`  ✓ role assignment already exists (${roleDefinitionId})`);
      return true;
    }
  } catch (error) {
    log(
      "  – could not query existing role assignments; refusing unverified role creation: " +
      (error instanceof Error ? error.message : String(error)),
    );
    return false;
  }

  try {
    run(
      "az",
      [
        "role",
        "assignment",
        "create",
        "--assignee-object-id",
        principalId,
        "--assignee-principal-type",
        principalType,
        "--role",
        roleDefinitionId,
        "--scope",
        scope,
      ],
      { capture: true },
    );
    log(`  ✓ created role assignment (${roleDefinitionId})`);
    return true;
  } catch (err) {
    const msg =
      err instanceof Error && "stderr" in err
        ? String((err as { stderr?: unknown }).stderr ?? err.message)
        : String(err);
    if (/RoleAssignmentExists|already exists/i.test(msg)) {
      log(`  ✓ role assignment already exists (${roleDefinitionId})`);
      return true;
    }
    log(`  ✗ failed to create role assignment (${roleDefinitionId}): ${msg.split("\n")[0]}`);
    return false;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

export function getCurrentPrincipal(run: CommandRunner = runCommand): {
  principalId: string;
  principalType: "User" | "ServicePrincipal";
} {
  const token = run("az", [
    "account", "get-access-token", "--resource", "https://management.azure.com/",
    "--query", "accessToken", "--output", "tsv",
  ], { capture: true });
  const payloadSegment = token.split(".")[1];
  if (!payloadSegment) throw new Error("ARM access token is not a JWT.");
  const payload: unknown = JSON.parse(Buffer.from(payloadSegment, "base64url").toString("utf8"));
  if (!isRecord(payload) || typeof payload.oid !== "string" || !payload.oid.trim()) {
    throw new Error("ARM access token has no principal oid claim.");
  }
  const accountType = run(
    "az", ["account", "show", "--query", "user.type", "--output", "tsv"], { capture: true },
  ).trim().toLowerCase();
  if (accountType !== "user" && accountType !== "serviceprincipal") {
    throw new Error(`Cannot determine the Azure caller type: '${accountType}'.`);
  }
  return {
    principalId: payload.oid.trim(),
    principalType: accountType === "serviceprincipal" || payload.idtyp === "app" ? "ServicePrincipal" : "User",
  };
}

export interface EnsureCallerPermissionOptions {
  scope: string;
  dataAction: string;
  roleDefinitionId: string;
  log?: (message: string) => void;
}

function actionMatches(pattern: string, action: string): boolean {
  const expression = pattern.split("*")
    .map(part => part.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"))
    .join(".*");
  return new RegExp(`^${expression}$`, "i").test(action);
}

function permissionActions(permission: Record<string, unknown>, key: string): string[] {
  const value = permission[key] ?? [];
  if (!Array.isArray(value) || !value.every((item): item is string => typeof item === "string")) {
    throw new Error(`Cannot verify caller permissions: invalid ${key}.`);
  }
  return value;
}

export function callerHasDataAction(
  scope: string,
  dataAction: string,
  run: CommandRunner = runCommand,
): boolean {
  let next: string | undefined =
    `https://management.azure.com${scope}/providers/Microsoft.Authorization/permissions?api-version=2022-04-01`;
  while (next) {
    if (new URL(next).origin !== "https://management.azure.com") {
      throw new Error("Caller permission pagination left the ARM endpoint.");
    }
    const response: unknown = JSON.parse(
      run("az", ["rest", "--method", "GET", "--url", next, "--output", "json"], { capture: true }),
    );
    if (!isRecord(response) || !Array.isArray(response.value)) {
      throw new Error("Cannot verify caller permissions: Azure returned no permission list.");
    }
    for (const permission of response.value) {
      if (!isRecord(permission)) throw new Error("Azure returned an invalid permission entry.");
      const allowed = permissionActions(permission, "dataActions");
      const excluded = permissionActions(permission, "notDataActions");
      if (
        allowed.some(pattern => actionMatches(pattern, dataAction)) &&
        !excluded.some(pattern => actionMatches(pattern, dataAction))
      ) {
        return true;
      }
    }
    if (response.nextLink !== undefined && response.nextLink !== null && typeof response.nextLink !== "string") {
      throw new Error("Azure returned invalid caller permission pagination.");
    }
    next = typeof response.nextLink === "string" ? response.nextLink : undefined;
  }
  return false;
}

export async function ensureCallerPermission(
  options: EnsureCallerPermissionOptions,
  run: CommandRunner = runCommand,
  wait: (milliseconds: number) => Promise<void> =
    milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds)),
): Promise<void> {
  const { scope, dataAction, roleDefinitionId } = options;
  const log = options.log ?? console.log;
  if (!scope || !dataAction || !roleDefinitionId) {
    throw new Error("Caller permission verification requires a scope, data action, and role.");
  }
  if (callerHasDataAction(scope, dataAction, run)) {
    log(`Existing permission covers '${dataAction}' at '${scope}'; skipping role assignment.`);
    return;
  }

  const principal = getCurrentPrincipal(run);
  log(`Permission '${dataAction}' is missing; ensuring role '${roleDefinitionId}' for ${principal.principalId}.`);
  if (!ensureRoleAssignment({ ...principal, scope, roleDefinitionId, log }, run)) {
    throw new Error(
      `Cannot ensure deployment access for ${principal.principalId} at '${scope}'. ` +
      "An administrator must grant the required role or authorize this caller to create role assignments.",
    );
  }

  for (let attempt = 0; attempt < 12; attempt++) {
    if (callerHasDataAction(scope, dataAction, run)) {
      log(`Required permission '${dataAction}' is available.`);
      return;
    }
    if (attempt < 11) {
      log("Waiting for role-assignment propagation...");
      await wait(10_000);
    }
  }
  throw new Error(`Role assignment exists, but '${dataAction}' is not effective at '${scope}' after waiting for propagation.`);
}
