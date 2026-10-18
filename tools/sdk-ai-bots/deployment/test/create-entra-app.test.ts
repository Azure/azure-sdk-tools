import assert from "node:assert/strict";
import test from "node:test";

import {
  APPLICATION_ROLE_VALUE,
  AZURE_CLI_CLIENT_ID,
  buildApplicationPatch,
  DELEGATED_SCOPE_VALUE,
  parseArgs,
  withoutPreAuthorizedApplications,
} from "../scripts/create-entra-app.js";

test("parses an Application ID URI independently from the client ID", () => {
  const args = parseArgs([
    "--display-name",
    "agent-server-dev",
    "--application-id-uri",
    "api://00000000-0000-0000-0000-000000000000/azure-sdk-qa-bot-dev/",
    "--application-client-id",
    "11111111-1111-1111-1111-111111111111",
    "--dry-run",
  ]);

  assert.equal(
    args.applicationIdUri,
    "api://00000000-0000-0000-0000-000000000000/azure-sdk-qa-bot-dev",
  );
  assert.equal(args.applicationClientId, "11111111-1111-1111-1111-111111111111");
  assert.equal(args.dryRun, true);
});

test("creates the delegated scope, application role, and Azure CLI preauthorization", () => {
  const { patch, scopeId, appRoleId } = buildApplicationPatch(
    {},
    "api://00000000-0000-0000-0000-000000000000/azure-sdk-qa-bot-dev",
  );

  assert.equal(patch.signInAudience, "AzureADMyOrg");
  assert.deepEqual(patch.identifierUris, [
    "api://00000000-0000-0000-0000-000000000000/azure-sdk-qa-bot-dev",
  ]);
  assert.equal(patch.api.requestedAccessTokenVersion, 2);
  assert.equal(
    patch.api.oauth2PermissionScopes?.find((scope) => scope.id === scopeId)?.value,
    DELEGATED_SCOPE_VALUE,
  );
  assert.deepEqual(
    patch.api.preAuthorizedApplications?.find(
      (application) => application.appId === AZURE_CLI_CLIENT_ID,
    )?.delegatedPermissionIds,
    [scopeId],
  );
  assert.equal(
    patch.appRoles.find((role) => role.id === appRoleId)?.value,
    APPLICATION_ROLE_VALUE,
  );

  const initialPatch = withoutPreAuthorizedApplications(patch);
  assert.equal(initialPatch.api.preAuthorizedApplications, undefined);
  assert.equal(patch.api.preAuthorizedApplications?.length, 1);
});

test("reuses existing scope and role IDs without duplicating CLI authorization", () => {
  const scopeId = "22222222-2222-2222-2222-222222222222";
  const appRoleId = "33333333-3333-3333-3333-333333333333";
  const { patch } = buildApplicationPatch(
    {
      api: {
        oauth2PermissionScopes: [{
          id: scopeId,
          value: DELEGATED_SCOPE_VALUE,
          isEnabled: false,
        }],
        preAuthorizedApplications: [{
          appId: AZURE_CLI_CLIENT_ID,
          delegatedPermissionIds: [scopeId],
        }],
      },
      appRoles: [{
        id: appRoleId,
        value: APPLICATION_ROLE_VALUE,
        isEnabled: false,
        allowedMemberTypes: ["User"],
        displayName: "Access agent",
        description: "Access agent",
      }],
    },
    "api://00000000-0000-0000-0000-000000000000/azure-sdk-qa-bot-dev",
  );

  assert.equal(patch.api.oauth2PermissionScopes?.length, 1);
  assert.equal(patch.api.oauth2PermissionScopes?.[0].isEnabled, true);
  assert.equal(patch.api.preAuthorizedApplications?.length, 1);
  assert.equal(patch.appRoles.length, 1);
  assert.equal(patch.appRoles[0].isEnabled, true);
  assert.deepEqual(patch.appRoles[0].allowedMemberTypes, ["Application"]);
});