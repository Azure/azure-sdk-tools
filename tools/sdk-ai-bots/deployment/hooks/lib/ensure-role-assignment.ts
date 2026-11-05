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
  equivalentRoleDefinitionIds?: readonly string[];
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
    if (!Array.isArray(assignments)) throw new Error("Azure returned no role assignment list.");
    const expectedRoles = [roleDefinitionId, ...(options.equivalentRoleDefinitionIds ?? [])]
      .map(role => role.split("/").pop()?.toLowerCase());
    const exists = assignments.some((assignment) => {
      if (!isRecord(assignment)) throw new Error("Azure returned an invalid role assignment.");
      const id = typeof assignment.roleDefinitionId === "string"
        ? assignment.roleDefinitionId.split("/").pop()?.toLowerCase() : undefined;
      const name = typeof assignment.roleDefinitionName === "string"
        ? assignment.roleDefinitionName.toLowerCase() : undefined;
      if (!id && !name) throw new Error("Role assignment has no role definition.");
      return (!!id && expectedRoles.includes(id)) || (!!name && expectedRoles.includes(name));
    });
    if (exists) {
      log(`  ✓ role assignment already exists (${roleDefinitionId})`);
      return true;
    }
  } catch (error) {
    log(`  ✗ cannot inspect existing role assignments: ${error instanceof Error ? error.message : error}`);
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
    throw new Error("ARM token has no principal oid claim.");
  }
  const accountType = run("az", [
    "account", "show", "--query", "user.type", "--output", "tsv",
  ], { capture: true }).trim().toLowerCase();
  if (accountType !== "user" && accountType !== "serviceprincipal") {
    throw new Error(`Cannot determine the Azure caller type: '${accountType}'.`);
  }
  return {
    principalId: payload.oid,
    principalType: accountType === "serviceprincipal" || payload.idtyp === "app" ? "ServicePrincipal" : "User",
  };
}

export function ensureCosmosRoleAssignment(
  options: { subscriptionId: string; resourceGroup: string; accountName: string; principalId: string },
  run: CommandRunner = runCommand,
  log: (message: string) => void = console.log,
): void {
  const { subscriptionId, resourceGroup, accountName, principalId } = options;
  const scope = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.DocumentDB/databaseAccounts/${accountName}`;
  const roleId = `${scope}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002`;
  const target = ["--subscription", subscriptionId, "--resource-group", resourceGroup, "--account-name", accountName];
  const assignments: unknown = JSON.parse(run(
    "az", ["cosmosdb", "sql", "role", "assignment", "list", ...target, "--output", "json"],
    { capture: true },
  ));
  if (!Array.isArray(assignments)) throw new Error("Azure returned no Cosmos role assignment list.");
  const exists = assignments.some(assignment => {
    if (
      !isRecord(assignment) || typeof assignment.principalId !== "string" ||
      typeof assignment.roleDefinitionId !== "string" || typeof assignment.scope !== "string"
    ) {
      throw new Error("Azure returned an invalid Cosmos role assignment.");
    }
    return assignment.principalId.toLowerCase() === principalId.toLowerCase() &&
      assignment.roleDefinitionId.toLowerCase() === roleId.toLowerCase() &&
      (assignment.scope === "/" || assignment.scope.toLowerCase() === scope.toLowerCase());
  });
  if (exists) {
    log(`Cosmos data contributor already assigned to ${principalId}; skipping creation.`);
    return;
  }
  run("az", [
    "cosmosdb", "sql", "role", "assignment", "create", ...target,
    "--principal-id", principalId, "--role-definition-id", roleId, "--scope", scope,
    "--output", "none",
  ], { capture: true });
  log(`Created Cosmos data contributor for ${principalId}.`);
}
