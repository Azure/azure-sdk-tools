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

import { execSync } from "child_process";

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
export function ensureRoleAssignment(options: EnsureRoleAssignmentOptions): boolean {
  const { principalId, roleDefinitionId, scope, principalType = "ServicePrincipal" } = options;
  const log = options.log ?? (() => {});

  if (!principalId || !roleDefinitionId || !scope) {
    log(`  – skipping role assignment (missing principalId, role, or scope)`);
    return false;
  }

  try {
    execSync(
      `az role assignment create --assignee-object-id "${principalId}" ` +
        `--assignee-principal-type ${principalType} ` +
        `--role "${roleDefinitionId}" --scope "${scope}"`,
      { stdio: "pipe", encoding: "utf8" }
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
