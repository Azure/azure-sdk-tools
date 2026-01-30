# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Grant access to the APIView Cosmos DB for the current user.
This script requires the user has elevated access to the Azure SDK Engineering System
subscription. It will assign the "DocumentDB Account Contributor" ARM role and the
"Built-in Data Reader" SQL role to the current user for the production and staging
APIView Cosmos DB accounts.

Note: Uses AZURE_APP_CONFIG_ENDPOINT and ENVIRONMENT_NAME from .env file.
"""

import argparse
import sys
from pathlib import Path
from uuid import uuid4

import requests
from azure.identity import DefaultAzureCredential
from azure.mgmt.authorization import AuthorizationManagementClient
from azure.mgmt.cosmosdb import CosmosDBManagementClient

# Add src to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))
from src._settings import SettingsManager


def get_principal_type(credential, principal_id: str) -> str:
    """Determine if the principal ID is a User or ServicePrincipal using Microsoft Graph."""
    token = credential.get_token("https://graph.microsoft.com/.default")
    headers = {"Authorization": f"Bearer {token.token}"}

    # Try to find as a user first
    resp = requests.get(
        f"https://graph.microsoft.com/v1.0/users/{principal_id}",
        headers=headers,
        timeout=20,
    )
    if resp.status_code == 200:
        return "User"

    # Try as a service principal
    resp = requests.get(
        f"https://graph.microsoft.com/v1.0/servicePrincipals/{principal_id}",
        headers=headers,
        timeout=20,
    )
    if resp.status_code == 200:
        return "ServicePrincipal"

    raise ValueError(f"Could not find principal with ID '{principal_id}' as User or ServicePrincipal.")


def modify_permissions():
    """Grant or revoke APIView Cosmos DB permissions for a specified principal."""
    parser = argparse.ArgumentParser(description="Grant or revoke Cosmos DB permissions for a principal.")
    parser.add_argument(
        "--principal-id",
        type=str,
        required=True,
        help="The principal ID (user or service principal) to grant/revoke permissions for.",
    )
    parser.add_argument("--revoke", action="store_true", help="Revoke permissions instead of granting them.")
    args = parser.parse_args()

    settings = SettingsManager()
    environment = settings.label

    user_principal_id = args.principal_id
    print(f"Principal ID: {user_principal_id}")
    print(f"Environment: {environment}")

    subscription_id = "a18897a6-7e44-457d-9260-f2854c0aca42"

    # Auth
    credential = DefaultAzureCredential()

    # Detect principal type
    principal_type = get_principal_type(credential, user_principal_id)
    print(f"Principal Type: {principal_type}")

    auth_client = AuthorizationManagementClient(credential, subscription_id)
    cosmos_client = CosmosDBManagementClient(credential, subscription_id)

    def process_permissions(resource_group, cosmos_account):
        # Get Cosmos DB account
        account = cosmos_client.database_accounts.get(resource_group, cosmos_account)
        cosmos_resource_id = account.id

        def assign_arm_role():
            role_name = "DocumentDB Account Contributor"
            scope = cosmos_resource_id
            role_defs = list(auth_client.role_definitions.list(scope, filter=f"roleName eq '{role_name}'"))
            if not role_defs:
                raise KeyError(f"Role definition '{role_name}' not found.")
            role_def_id = role_defs[0].id
            existing = list(
                auth_client.role_assignments.list_for_scope(scope, filter=f"principalId eq '{user_principal_id}'")
            )
            if any(ra.role_definition_id == role_def_id for ra in existing):
                print("ℹ️ ARM role already assigned – skipping")
                return
            print(f"Assigning ARM role '{role_name}'")
            assignment = auth_client.role_assignments.create(
                scope,
                str(uuid4()),
                {
                    "principal_id": user_principal_id,
                    "role_definition_id": role_def_id,
                    "principal_type": principal_type,
                },
            )
            print(f"✓ ARM role assigned: {assignment.id}")

        def revoke_arm_role():
            role_name = "DocumentDB Account Contributor"
            scope = cosmos_resource_id
            role_defs = list(auth_client.role_definitions.list(scope, filter=f"roleName eq '{role_name}'"))
            if not role_defs:
                print(f"Role definition '{role_name}' not found.")
                return
            role_def_id = role_defs[0].id
            existing = list(
                auth_client.role_assignments.list_for_scope(scope, filter=f"principalId eq '{user_principal_id}'")
            )
            found = False
            for ra in existing:
                if ra.role_definition_id == role_def_id:
                    print(f"Revoking ARM role assignment: {ra.id}")
                    auth_client.role_assignments.delete_by_id(ra.id)
                    print("✓ ARM role revoked")
                    found = True
            if not found:
                print("ℹ️ No ARM role assignment found to revoke.")

        def assign_sql_role():
            # pylint: disable=line-too-long
            # Use Built-in Data Contributor (not Reader) to allow readMetadata action
            sql_role_id = f"/subscriptions/{subscription_id}/resourceGroups/{resource_group}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmos_account}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
            assignments = list(cosmos_client.sql_resources.list_sql_role_assignments(resource_group, cosmos_account))
            if any(a.principal_id == user_principal_id for a in assignments):
                print("ℹ️ SQL role already assigned – skipping")
                return
            print("Assigning SQL role (Built-in Data Contributor)...")
            poller = cosmos_client.sql_resources.begin_create_update_sql_role_assignment(
                resource_group_name=resource_group,
                account_name=cosmos_account,
                role_assignment_id=str(uuid4()),
                create_update_sql_role_assignment_parameters={
                    "role_definition_id": sql_role_id,
                    "principal_id": user_principal_id,
                    "scope": cosmos_resource_id,
                },
            )
            poller.result()
            print("✓ SQL role assigned")

        def revoke_sql_role():
            # pylint: disable=line-too-long
            sql_role_id = f"/subscriptions/{subscription_id}/resourceGroups/{resource_group}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmos_account}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
            assignments = list(cosmos_client.sql_resources.list_sql_role_assignments(resource_group, cosmos_account))
            found = False
            for a in assignments:
                if a.principal_id == user_principal_id and a.role_definition_id == sql_role_id:
                    print(f"Revoking SQL role assignment: {a.id}")
                    assignment_guid = a.id.split("/")[-1]
                    poller = cosmos_client.sql_resources.begin_delete_sql_role_assignment(
                        role_assignment_id=assignment_guid,
                        resource_group_name=resource_group,
                        account_name=cosmos_account,
                    )
                    poller.result()
                    print("✓ SQL role revoked")
                    found = True
            if not found:
                print("ℹ️ No SQL role assignment found to revoke.")

        if args.revoke:
            revoke_arm_role()
            revoke_sql_role()
        else:
            assign_arm_role()
            assign_sql_role()

    settings_map = {
        "staging": {"resource_group": "apiviewstagingrg", "cosmos_account": "apiviewstaging"},
        "prod": {"resource_group": "apiview", "cosmos_account": "apiview-cosmos"},
    }

    if environment not in settings_map:
        valid_environments = ", ".join(sorted(settings_map.keys()))
        print(f"Error: Unsupported environment '{environment}'. Expected one of: {valid_environments}.")
        sys.exit(1)

    data = settings_map[environment]
    print(f"\n=== Processing {environment} ===")
    resource_group = data["resource_group"]
    cosmos_account = data["cosmos_account"]
    process_permissions(resource_group, cosmos_account)


if __name__ == "__main__":
    modify_permissions()
