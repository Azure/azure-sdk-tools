from uuid import uuid4
import requests
from azure.identity import DefaultAzureCredential
from azure.mgmt.authorization import AuthorizationManagementClient
from azure.mgmt.cosmosdb import CosmosDBManagementClient


def get_current_user_object_id():
    credential = DefaultAzureCredential()
    token = credential.get_token("https://graph.microsoft.com/.default")
    headers = {"Authorization": f"Bearer {token.token}"}
    resp = requests.get("https://graph.microsoft.com/v1.0/me", headers=headers)
    resp.raise_for_status()
    return resp.json()["id"]


def main():
    user_principal_id = get_current_user_object_id()
    print("Current user object ID:", user_principal_id)

    subscription_id = "a18897a6-7e44-457d-9260-f2854c0aca42"
    resource_group = "apiviewstagingrg"
    cosmos_account = "apiviewstaging"

    # Auth
    credential = DefaultAzureCredential()
    auth_client = AuthorizationManagementClient(credential, subscription_id)
    cosmos_client = CosmosDBManagementClient(credential, subscription_id)

    # Get Cosmos DB account
    account = cosmos_client.database_accounts.get(resource_group, cosmos_account)
    cosmos_resource_id = account.id

    def assign_arm_role():
        role_name = "DocumentDB Account Contributor"
        scope = cosmos_resource_id

        # Get the role definition ID for the role name
        role_defs = list(auth_client.role_definitions.list(scope, filter=f"roleName eq '{role_name}'"))
        if not role_defs:
            raise Exception(f"Role definition '{role_name}' not found.")
        role_def_id = role_defs[0].id

        # Check if assignment already exists
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
            {"principal_id": user_principal_id, "role_definition_id": role_def_id, "principal_type": "User"},
        )
        print(f"✓ ARM role assigned: {assignment.id}")

    def assign_sql_role():
        sql_role_id = f"/subscriptions/{subscription_id}/resourceGroups/{resource_group}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmos_account}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
        assignments = list(cosmos_client.sql_resources.list_sql_role_assignments(resource_group, cosmos_account))
        if any(a.principal_id == user_principal_id for a in assignments):
            print("ℹ️ SQL role already assigned – skipping")
            return
        print("Assigning SQL role (Built-in Data Reader)...")
        cosmos_client.sql_resources.create_update_sql_role_assignment(
            resource_group,
            cosmos_account,
            str(uuid4()),
            {"role_definition_id": sql_role_id, "principal_id": user_principal_id, "scope": "/"},
        )
        print("✓ SQL role assigned")

    assign_arm_role()
    assign_sql_role()


if __name__ == "__main__":
    main()
