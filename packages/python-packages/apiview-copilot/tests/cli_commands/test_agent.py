# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``agent`` CLI commands (mention, resolve-thread)."""

import os
from unittest.mock import MagicMock, patch

from .conftest import make_temp_json


class TestAgentMention:
    """Tests for `agent mention` command."""

    @patch("cli.handle_mention_request")
    def test_mention_local_calls_handler(self, mock_handler, sample_comments_json):
        """Validate local mention reads file and calls handle_mention_request."""
        from cli import handle_agent_mention

        mock_handler.return_value = "Mention handled successfully."

        handle_agent_mention(comments_path=sample_comments_json)

        mock_handler.assert_called_once()
        call_kwargs = mock_handler.call_args.kwargs
        assert call_kwargs["language"] == "Python"
        assert call_kwargs["package_name"] == "azure-test"

    def test_mention_file_not_found(self, capsys):
        """Validate error message for nonexistent file."""
        from cli import handle_agent_mention

        handle_agent_mention(comments_path="/nonexistent/path.json")

        captured = capsys.readouterr()
        assert "does not exist" in captured.out

    def test_mention_unsupported_language(self, capsys):
        """Validate error for unsupported language."""
        from cli import handle_agent_mention

        data = {"language": "brainfuck", "comments": ["test"], "package_name": "test", "code": ""}
        path = make_temp_json(data, suffix=".json")
        try:
            handle_agent_mention(comments_path=path)
            captured = capsys.readouterr()
            assert "Unsupported language" in captured.out
        finally:
            os.unlink(path)

    @patch("cli.requests.post")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_mention_remote_posts_to_api(self, mock_auth, mock_post, mock_settings, sample_comments_json):
        """Validate remote mention POSTs to API."""
        from cli import handle_agent_mention

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = {"response": "Done"}
        mock_post.return_value = mock_resp

        handle_agent_mention(comments_path=sample_comments_json, remote=True)

        mock_post.assert_called_once()


class TestAgentResolveThread:
    """Tests for `agent resolve-thread` command."""

    @patch("cli.handle_thread_resolution_request")
    def test_resolve_thread_local(self, mock_handler, sample_comments_json):
        """Validate local resolve-thread reads file and calls handler."""
        from cli import handle_agent_thread_resolution

        mock_handler.return_value = "Thread resolved."

        handle_agent_thread_resolution(comments_path=sample_comments_json)

        mock_handler.assert_called_once()
        call_kwargs = mock_handler.call_args.kwargs
        assert call_kwargs["language"] == "Python"

    def test_resolve_thread_file_not_found(self, capsys):
        """Validate error for nonexistent file."""
        from cli import handle_agent_thread_resolution

        handle_agent_thread_resolution(comments_path="/nonexistent/path.json")

        captured = capsys.readouterr()
        assert "does not exist" in captured.out

    @patch("cli.requests.post")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_resolve_thread_remote(self, mock_auth, mock_post, mock_settings, sample_comments_json):
        """Validate remote resolve-thread POSTs to API."""
        from cli import handle_agent_thread_resolution

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = {"response": "Resolved"}
        mock_post.return_value = mock_resp

        handle_agent_thread_resolution(comments_path=sample_comments_json, remote=True)

        mock_post.assert_called_once()
