# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``app`` CLI commands (check, deploy)."""

from unittest.mock import MagicMock, patch


class TestAppCheck:
    """Tests for `app check` command."""

    @patch("cli.requests.get")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_check_health_no_auth(self, mock_auth, mock_get, mock_settings, capsys):
        """Validate health check without auth."""
        from cli import check_health

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_get.return_value = mock_resp

        check_health(include_auth=False)

        mock_get.assert_called_once()
        call_args = mock_get.call_args
        assert "health-test" in str(call_args)
        captured = capsys.readouterr()
        assert "healthy" in captured.out

    @patch("cli.requests.get")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_check_health_with_auth(self, mock_auth, mock_get, mock_settings, capsys):
        """Validate health check with auth."""
        from cli import check_health

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_get.return_value = mock_resp

        check_health(include_auth=True)

        mock_get.assert_called_once()
        call_args = mock_get.call_args
        assert "auth-test" in str(call_args)

    @patch("cli.requests.get")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_check_health_failure(self, mock_auth, mock_get, mock_settings, capsys):
        """Validate output on health check failure."""
        from cli import check_health

        mock_resp = MagicMock()
        mock_resp.status_code = 500
        mock_resp.text = "Internal Server Error"
        mock_get.return_value = mock_resp

        check_health()

        captured = capsys.readouterr()
        assert "failed" in captured.out.lower() or "500" in captured.out


class TestAppDeploy:
    """Tests for `app deploy` command."""

    @patch("scripts.deploy_app.deploy_app_to_azure")
    def test_deploy_calls_deploy_function(self, mock_deploy):
        """Validate deploy_flask_app calls the deploy script."""
        from cli import deploy_flask_app

        deploy_flask_app()

        mock_deploy.assert_called_once()
