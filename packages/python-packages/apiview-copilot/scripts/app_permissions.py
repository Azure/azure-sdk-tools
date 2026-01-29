# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Manage AVC App Role assignments for users and service principals.

This script assigns/revokes Azure AD App Roles on the AVC App Registration.
- Users get: Read, Write roles
- Service Principals get: App.Read, App.Write roles

Requires: Microsoft.Graph permissions (AppRoleAssignment.ReadWrite.All or similar)

Usage:
    python scripts/app_permissions.py --principal-id <id> --permission reader
    python scripts/app_permissions.py --principal-id <id> --permission writer
    python scripts/app_permissions.py --principal-id <id> --permission both
    python scripts/app_permissions.py --principal-id <id> --permission reader --revoke

Note: Uses AZURE_APP_CONFIG_ENDPOINT and ENVIRONMENT_NAME from .env file.
"""

import argparse
import sys
from pathlib import Path
from typing import Literal

import requests
from azure.identity import DefaultAzureCredential

# Add src to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))
from src._settings import SettingsManager


def get_graph_token(credential) -> str:
    """Get a token for Microsoft Graph API."""
    token = credential.get_token("https://graph.microsoft.com/.default")
    return token.token


def get_principal_info(credential, principal_id: str) -> tuple[Literal["User", "ServicePrincipal"], dict]:
    """
    Determine if the principal ID is a User or ServicePrincipal using Microsoft Graph.
    Returns (principal_type, principal_data).
    """
    token = get_graph_token(credential)
    headers = {"Authorization": f"Bearer {token}"}

    # Try to find as a user first
    resp = requests.get(
        f"https://graph.microsoft.com/v1.0/users/{principal_id}",
        headers=headers,
        timeout=20,
    )
    if resp.status_code == 200:
        return "User", resp.json()
    if resp.status_code != 404:
        resp.raise_for_status()

    # Try as a service principal
    resp = requests.get(
        f"https://graph.microsoft.com/v1.0/servicePrincipals/{principal_id}",
        headers=headers,
        timeout=20,
    )
    if resp.status_code == 200:
        return "ServicePrincipal", resp.json()
    if resp.status_code != 404:
        resp.raise_for_status()

    raise ValueError(f"Could not find principal with ID '{principal_id}' as User or ServicePrincipal.")


def get_service_principal_for_app(credential, app_id: str) -> dict:
    """Get the service principal (enterprise app) for an app registration."""
    token = get_graph_token(credential)
    headers = {"Authorization": f"Bearer {token}"}

    resp = requests.get(
        f"https://graph.microsoft.com/v1.0/servicePrincipals?$filter=appId eq '{app_id}'",
        headers=headers,
        timeout=20,
    )
    resp.raise_for_status()
    data = resp.json()
    if not data.get("value"):
        raise ValueError(f"No service principal found for app ID '{app_id}'")
    return data["value"][0]


def get_app_roles(sp: dict) -> dict[str, str]:
    """
    Extract app roles from service principal.
    Returns dict mapping role value (e.g., "Read") to role ID.
    """
    return {role["value"]: role["id"] for role in sp.get("appRoles", []) if role.get("isEnabled")}


def get_existing_role_assignments(credential, resource_sp_id: str, principal_id: str) -> list[dict]:
    """Get existing app role assignments for a principal on a resource."""
    token = get_graph_token(credential)
    headers = {"Authorization": f"Bearer {token}"}

    # Query assignments where principal is the assignee
    resp = requests.get(
        f"https://graph.microsoft.com/v1.0/servicePrincipals/{resource_sp_id}/appRoleAssignedTo"
        f"?$filter=principalId eq '{principal_id}'",
        headers=headers,
        timeout=20,
    )
    resp.raise_for_status()
    return resp.json().get("value", [])


def assign_app_role(
    credential,
    resource_sp_id: str,
    principal_id: str,
    principal_type: str,
    app_role_id: str,
    role_name: str,
):
    """Assign an app role to a principal."""
    token = get_graph_token(credential)
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
    }

    body = {
        "principalId": principal_id,
        "resourceId": resource_sp_id,
        "appRoleId": app_role_id,
    }

    # Different endpoint for users vs service principals
    if principal_type == "User":
        url = f"https://graph.microsoft.com/v1.0/users/{principal_id}/appRoleAssignments"
    else:
        url = f"https://graph.microsoft.com/v1.0/servicePrincipals/{principal_id}/appRoleAssignments"

    resp = requests.post(url, headers=headers, json=body, timeout=20)

    if resp.status_code == 201:
        print(f"✅ Assigned role '{role_name}' to {principal_id}")
    elif resp.status_code == 409 or "already exists" in resp.text.lower():
        print(f"✅ Role '{role_name}' already assigned to {principal_id}")
    else:
        print(f"❌ Failed to assign role '{role_name}': {resp.status_code} - {resp.text}")
        return False
    return True


def revoke_app_role(
    credential,
    principal_id: str,
    principal_type: str,
    assignment_id: str,
    role_name: str,
):
    """Revoke an app role assignment."""
    token = get_graph_token(credential)
    headers = {"Authorization": f"Bearer {token}"}

    # Different endpoint for users vs service principals
    if principal_type == "User":
        url = f"https://graph.microsoft.com/v1.0/users/{principal_id}/appRoleAssignments/{assignment_id}"
    else:
        url = f"https://graph.microsoft.com/v1.0/servicePrincipals/{principal_id}/appRoleAssignments/{assignment_id}"

    resp = requests.delete(url, headers=headers, timeout=20)

    if resp.status_code == 204:
        print(f"✅ Revoked role '{role_name}' from {principal_id}")
    elif resp.status_code == 404:
        print(f"ℹ️ Role '{role_name}' not found for {principal_id}")
    else:
        print(f"❌ Failed to revoke role '{role_name}': {resp.status_code} - {resp.text}")
        return False
    return True


def main():
    parser = argparse.ArgumentParser(description="Manage AVC App Role assignments for users and service principals.")
    parser.add_argument(
        "--principal-id",
        type=str,
        required=True,
        help="The principal ID (user or service principal) to grant/revoke permissions for.",
    )
    parser.add_argument(
        "--permission",
        "-p",
        type=str,
        choices=["reader", "writer", "both"],
        required=True,
        help="The permission level to grant/revoke: reader, writer, or both.",
    )
    parser.add_argument(
        "--revoke",
        action="store_true",
        help="Revoke permissions instead of granting them.",
    )
    args = parser.parse_args()

    settings = SettingsManager()
    credential = DefaultAzureCredential()

    # Get app ID from AppConfig
    print(f"Looking up app_id from AppConfig ({settings.label})...")
    app_id = settings.get("app_id")
    print(f"App ID: {app_id}")

    print(f"Principal ID: {args.principal_id}")
    print(f"Environment: {settings.label}")
    print(f"Permission: {args.permission}")
    print(f"Action: {'Revoke' if args.revoke else 'Grant'}")
    print()

    # Detect principal type
    try:
        principal_type, principal_data = get_principal_info(credential, args.principal_id)
    except ValueError as e:
        print(f"❌ {e}")
        sys.exit(1)

    display_name = principal_data.get("displayName", principal_data.get("appDisplayName", "Unknown"))
    print(f"Principal Type: {principal_type}")
    print(f"Display Name: {display_name}")
    print()

    # Get the AVC service principal (enterprise app)
    try:
        avc_sp = get_service_principal_for_app(credential, app_id)
    except ValueError as e:
        print(f"❌ {e}")
        sys.exit(1)

    avc_sp_id = avc_sp["id"]
    app_roles = get_app_roles(avc_sp)
    print(f"Available App Roles: {list(app_roles.keys())}")
    print()

    # Determine which roles to assign based on principal type and permission
    roles_to_process = []
    if principal_type == "User":
        if args.permission in ("reader", "both"):
            roles_to_process.append("Read")
        if args.permission in ("writer", "both"):
            roles_to_process.append("Write")
    else:  # ServicePrincipal
        if args.permission in ("reader", "both"):
            roles_to_process.append("App.Read")
        if args.permission in ("writer", "both"):
            roles_to_process.append("App.Write")

    # Validate roles exist
    for role in roles_to_process:
        if role not in app_roles:
            print(f"❌ Role '{role}' not found in app registration")
            sys.exit(1)

    # Track failures
    has_failures = False

    if args.revoke:
        # Get existing assignments to find the ones to revoke
        existing = get_existing_role_assignments(credential, avc_sp_id, args.principal_id)

        for role_name in roles_to_process:
            role_id = app_roles[role_name]
            # Find assignment with this role
            assignment = None
            for a in existing:
                if a.get("appRoleId") == role_id:
                    assignment = a
                    break

            if assignment:
                success = revoke_app_role(
                    credential,
                    args.principal_id,
                    principal_type,
                    assignment["id"],
                    role_name,
                )
                if not success:
                    has_failures = True
            else:
                print(f"ℹ️ Role '{role_name}' not currently assigned to {args.principal_id}")
    else:
        # Grant roles
        for role_name in roles_to_process:
            role_id = app_roles[role_name]
            success = assign_app_role(
                credential,
                avc_sp_id,
                args.principal_id,
                principal_type,
                role_id,
                role_name,
            )
            if not success:
                has_failures = True

    print()
    if has_failures:
        print("Completed with errors.")
        sys.exit(1)
    else:
        print("Done!")


if __name__ == "__main__":
    main()
