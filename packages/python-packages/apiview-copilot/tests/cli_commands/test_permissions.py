# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``permissions`` CLI commands (grant, revoke)."""

from unittest.mock import MagicMock, patch

import pytest


class TestPermissionsGrant:
    """Tests for `permissions grant` command."""

    @patch("src._permissions.assign_keyvault_access")
    @patch("src._permissions.assign_cosmosdb_roles")
    @patch("src._permissions.assign_rbac_roles")
    @patch("src._permissions.get_current_user_object_id")
    def test_grant_permissions_calls_rbac(self, mock_get_user, mock_rbac, mock_cosmos, mock_kv, capsys):
        """Validate grant_permissions calls RBAC assignment functions."""
        from cli import grant_permissions

        mock_get_user.return_value = "fake-user-oid"

        grant_permissions()

        assert mock_rbac.call_count >= 2
        assert mock_cosmos.call_count >= 2
        assert mock_kv.call_count >= 2

    def test_grant_permissions_no_user_raises(self):
        """Validate error when user ID cannot be determined."""
        from cli import grant_permissions

        with patch("src._permissions.get_current_user_object_id", return_value=None):
            with pytest.raises(ValueError, match="Could not determine"):
                grant_permissions()


class TestPermissionsRevoke:
    """Tests for `permissions revoke` command."""

    @patch("src._permissions.revoke_keyvault_access")
    @patch("src._permissions.revoke_cosmosdb_roles")
    @patch("src._permissions.revoke_rbac_roles")
    @patch("src._permissions.get_current_user_object_id")
    @patch("src._credential.get_credential")
    def test_revoke_permissions_calls_revoke_functions(
        self, mock_get_cred, mock_get_user, mock_rbac, mock_cosmos, mock_kv
    ):
        """Validate revoke_permissions calls revocation functions."""
        from cli import revoke_permissions

        mock_get_user.return_value = "fake-user-oid"

        with patch("azure.mgmt.resource.ManagementLockClient") as mock_lock_cls:
            mock_lock_client = MagicMock()
            mock_lock_cls.return_value = mock_lock_client
            mock_lock_client.management_locks.list_at_resource_group_level.return_value = []

            revoke_permissions()

        assert mock_rbac.call_count >= 2
        assert mock_cosmos.call_count >= 2
        assert mock_kv.call_count >= 2
