# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for assigning Azure RBAC roles and permissions.
"""
import sys
from typing import List, Literal, Optional
from uuid import uuid4

from azure.core.exceptions import ResourceExistsError
from azure.mgmt.authorization import AuthorizationManagementClient
from azure.mgmt.authorization.models import (
    PrincipalType,
    RoleAssignmentCreateParameters,
)
from azure.mgmt.cosmosdb import CosmosDBManagementClient
from azure.mgmt.cosmosdb.models import SqlRoleAssignmentCreateUpdateParameters
from src._credential import get_credential


def get_current_user_object_id():
    """Retrieve the current user's object ID from Microsoft Graph API."""
    import requests

    credential = get_credential()
    token = credential.get_token("https://graph.microsoft.com/.default")
    headers = {"Authorization": f"Bearer {token.token}"}
    resp = requests.get("https://graph.microsoft.com/v1.0/me", headers=headers, timeout=20)
    resp.raise_for_status()
    return resp.json()["id"]


def assign_cosmosdb_roles(
    *,
    principal_id: str,
    principal_type: PrincipalType,
    subscription_id: str,
    rg_name: str,
    role_kind: Literal["readOnly", "readWrite"],
    cosmos_account_name: str,
):
    """Assigns the necessary control and data plane roles."""
    assign_rbac_roles(
        roles=["Cosmos DB Operator"],
        principal_id=principal_id,
        principal_type=principal_type,
        subscription_id=subscription_id,
        rg_name=rg_name,
    )
    _assign_cosmosdb_builtin_roles(
        kind=role_kind,
        principal_id=principal_id,
        subscription_id=subscription_id,
        rg_name=rg_name,
        cosmos_account_name=cosmos_account_name,
    )


def assign_rbac_roles(
    *,
    roles: List[str],
    principal_id: str,
    principal_type: PrincipalType,
    subscription_id: str,
    rg_name: str,
    scope: Optional[str] = None,
):
    """Assigns arbitrary RBAC roles to the logged-in user."""
    credential = get_credential()
    client = AuthorizationManagementClient(credential, subscription_id)
    for role_name in roles:
        try:
            role_definitions = list(
                client.role_definitions.list(
                    f"/subscriptions/{subscription_id}",
                    filter=f"roleName eq '{role_name}'",
                )
            )

            if not role_definitions:
                raise KeyError(f"Role '{role_name}' not found!")

            role_definition_id = role_definitions[0].id
            role_scope = scope or f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}"

            # Assign role
            assignment_id = str(uuid4())
            params = RoleAssignmentCreateParameters(
                principal_id=principal_id,
                role_definition_id=role_definition_id,
                principal_type=principal_type,
            )
            client.role_assignments.create(scope=role_scope, role_assignment_name=assignment_id, parameters=params)
            print(f"✅ Assigned '{role_name}' to {principal_id}.")
        except ResourceExistsError:
            print(f"✅ RBAC role '{role_name}' already assigned to {principal_id}.")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


COSMOS_DATA_READER_ROLE_ID = "00000000-0000-0000-0000-000000000001"
COSMOS_DATA_CONTRIBUTOR_ROLE_ID = "00000000-0000-0000-0000-000000000002"


def _assign_cosmosdb_builtin_roles(
    *,
    kind: Literal["readOnly", "readWrite"],
    principal_id: str,
    subscription_id: str,
    rg_name: str,
    cosmos_account_name: str,
):
    """Assigns special built-in roles for Cosmos DB."""
    credential = get_credential()
    cosmos_mgmt_client = CosmosDBManagementClient(credential, subscription_id)
    cosmos_account = cosmos_mgmt_client.database_accounts.get(rg_name, cosmos_account_name)
    role_name = None
    role_id = None
    # Assign well-known Role ID for "Cosmos DB Built-in Data Contributor"
    if kind == "readWrite":
        role_id = f"{cosmos_account.id}/sqlRoleDefinitions/{COSMOS_DATA_CONTRIBUTOR_ROLE_ID}"
        role_name = "Data Contributor (built-in)"
    elif kind == "readOnly":
        role_id = f"{cosmos_account.id}/sqlRoleDefinitions/{COSMOS_DATA_READER_ROLE_ID}"
        role_name = "Data Reader (built-in)"
    if not role_name or not role_id:
        return
    try:
        results = list(cosmos_mgmt_client.sql_resources.list_sql_role_assignments(rg_name, cosmos_account_name))
        for result in results:
            if result.role_definition_id == role_id and result.principal_id == principal_id:
                print(f"✅ Cosmos role '{role_name}' already assigned to {principal_id}.")
                return
        # Otherwise we must assign the role
        role_assignment_params = SqlRoleAssignmentCreateUpdateParameters(
            role_definition_id=role_id,
            principal_id=principal_id,
            scope=cosmos_account.id,  # Assign at account level
        )
        role_assignment_id = str(uuid4())
        cosmos_mgmt_client.sql_resources.begin_create_update_sql_role_assignment(
            role_assignment_id,
            rg_name,
            cosmos_account_name,
            role_assignment_params,
        ).result()
        print(f"✅ Assigned Cosmos role '{role_name}' to {principal_id}.")
    except ResourceExistsError:
        print(f"✅ 'Cosmos role {role_name}' already assigned to {principal_id}.")
    except Exception as e:
        print(f"❌ An error occurred: {e}")
        sys.exit(1)


def revoke_rbac_roles(
    *,
    roles: List[str],
    principal_id: str,
    subscription_id: str,
    rg_name: str,
    scope: Optional[str] = None,
):
    credential = get_credential()
    auth_client = AuthorizationManagementClient(credential, subscription_id)

    for role_name in roles:
        rg_scope = f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}"

        role_defs = list(auth_client.role_definitions.list(scope or rg_scope, filter=f"roleName eq '{role_name}'"))
        if not role_defs:
            print(f"Role definition '{role_name}' not found.")
            return
        role_def_id = role_defs[0].id
        if scope:
            existing = list(
                auth_client.role_assignments.list_for_scope(scope, filter=f"principalId eq '{principal_id}'")
            )
        else:
            existing = list(
                auth_client.role_assignments.list_for_resource_group(rg_name, filter=f"principalId eq '{principal_id}'")
            )
        found = False
        for ra in existing:
            if ra.role_definition_id == role_def_id:
                auth_client.role_assignments.delete_by_id(ra.id)
                print(f"✅ ARM role '{role_name}' revoked")
                found = True
        if not found:
            print(f"ℹ️ No ARM role assignment '{role_name}' found to revoke.")


def revoke_cosmosdb_roles(
    *,
    principal_id: str,
    subscription_id: str,
    rg_name: str,
    cosmos_account_name: str,
):
    credential = get_credential()
    cosmos_client = CosmosDBManagementClient(credential, subscription_id)

    # pylint: disable=line-too-long
    sql_role_id = f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmos_account_name}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
    assignments = list(cosmos_client.sql_resources.list_sql_role_assignments(rg_name, cosmos_account_name))
    found = False
    for a in assignments:
        if a.principal_id == principal_id and a.role_definition_id == sql_role_id:
            print(f"Revoking SQL role assignment: {a.id}")
            assignment_guid = a.id.split("/")[-1]
            poller = cosmos_client.sql_resources.begin_delete_sql_role_assignment(
                role_assignment_id=assignment_guid,
                resource_group_name=rg_name,
                account_name=cosmos_account_name,
            )
            poller.result()
            print("✅ SQL role revoked")
            found = True
    if not found:
        print("ℹ️ No SQL role assignment found to revoke.")
    revoke_rbac_roles(
        roles=["Cosmos DB Operator"],
        principal_id=principal_id,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmos_account_name}",
    )


def assign_keyvault_access(*, principal_id: str, subscription_id: str, rg_name: str, vault_name: str, tenant_id: str):
    """
    Grants KeyVault access by creating an AccessPolicyEntry
    """
    from azure.mgmt.keyvault import KeyVaultManagementClient
    from azure.mgmt.keyvault.models import AccessPolicyEntry, Permissions

    credential = get_credential()
    client = KeyVaultManagementClient(credential, subscription_id)

    policy = AccessPolicyEntry(
        tenant_id=tenant_id,
        object_id=principal_id,
        permissions=Permissions(keys=["get", "list"], secrets=["get", "list", "set"], certificates=["get", "list"]),
    )
    vault = client.vaults.get(rg_name, vault_name)
    # if the policy already exists for the principal_id we shouldn't add another
    for p in vault.properties.access_policies:
        if p.object_id == principal_id:
            print(f"✅ KeyVault access policy already exists for {principal_id}")
            return
    vault.properties.access_policies.append(policy)
    return_val = client.vaults.begin_create_or_update(rg_name, vault_name, vault).result()
    print(f"✅ KeyVault access policy created for {principal_id}")
    return return_val


def revoke_keyvault_access(*, principal_id: str, subscription_id: str, rg_name: str, vault_name: str):
    """
    Revokes KeyVault access by deleting an AccessPolicyEntry
    """
    from azure.mgmt.keyvault import KeyVaultManagementClient

    credential = get_credential()
    client = KeyVaultManagementClient(credential, subscription_id)
    vault = client.vaults.get(rg_name, vault_name)
    original_len = len(vault.properties.access_policies)
    vault.properties.access_policies = [p for p in vault.properties.access_policies if p.object_id != principal_id]
    return_val = client.vaults.begin_create_or_update(rg_name, vault_name, vault).result()
    if len(vault.properties.access_policies) < original_len:
        print(f"✅ KeyVault access policy revoked for {principal_id}")
    else:
        print(f"ℹ️ No KeyVault access policy found to revoke for {principal_id}")
    return return_val
