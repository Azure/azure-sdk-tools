# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for CLI utility helpers, argument parsing, and CustomJSONEncoder."""

import json
from unittest.mock import MagicMock, patch

import pytest


# =====================================================================
# normalize_language
# =====================================================================


class TestNormalizeLanguage:
    """Tests for the normalize_language helper function."""

    def test_normalize_known_languages(self):
        from cli import normalize_language

        assert normalize_language("python") == "Python"
        assert normalize_language("Python") == "Python"
        assert normalize_language("PYTHON") == "Python"
        assert normalize_language("csharp") == "C#"
        assert normalize_language("dotnet") == "C#"
        assert normalize_language("golang") == "Go"
        assert normalize_language("cpp") == "C++"
        assert normalize_language("typescript") == "JavaScript"
        assert normalize_language("ios") == "Swift"
        assert normalize_language("clang") == "C"

    def test_normalize_unknown_language(self):
        from cli import normalize_language

        result = normalize_language("ruby")
        assert result == "ruby"

    def test_normalize_empty(self):
        from cli import normalize_language

        assert normalize_language("") == ""
        assert normalize_language(None) is None


# =====================================================================
# _build_auth_header
# =====================================================================


class TestBuildAuthHeader:
    """Tests for the _build_auth_header helper."""

    def test_build_auth_header_returns_bearer(self, mock_settings):
        """Validate auth header returns a Bearer token."""
        from cli import _build_auth_header

        fake_cred = MagicMock()
        fake_token = MagicMock()
        fake_token.token = "test-token-123"
        fake_cred.get_token.return_value = fake_token

        with patch("src._credential.get_credential", return_value=fake_cred):
            header = _build_auth_header()

        assert header == {"Authorization": "Bearer test-token-123"}

    def test_build_auth_header_auth_failure(self, mock_settings):
        """Validate auth header handles authentication failure."""
        from azure.core.exceptions import ClientAuthenticationError
        from cli import _build_auth_header

        fake_cred = MagicMock()
        fake_cred.get_token.side_effect = ClientAuthenticationError("Not logged in")

        with patch("src._credential.get_credential", return_value=fake_cred):
            with pytest.raises(SystemExit):
                _build_auth_header()


# =====================================================================
# _claims_is_writer
# =====================================================================


class TestClaimsIsWriter:
    """Tests for the _claims_is_writer helper."""

    def test_writer_with_write_role(self):
        from cli import _claims_is_writer

        assert _claims_is_writer({"roles": ["Write"]}) is True
        assert _claims_is_writer({"roles": ["App.Write"]}) is True

    def test_writer_without_write_role(self):
        from cli import _claims_is_writer

        assert _claims_is_writer({"roles": ["Read"]}) is False
        assert _claims_is_writer({}) is False
        assert _claims_is_writer({"roles": []}) is False


# =====================================================================
# Knack CLI argument parsing tests
# =====================================================================


class TestCliArgumentParsing:
    """
    Tests that verify Knack CLI argument parsing and validation works correctly.
    These create a CLI instance and invoke it with specific args to verify
    that arguments are parsed and validated before reaching command functions.
    """

    def _make_cli(self):
        """Create a test CLI instance."""
        from cli import CliCommandsLoader
        from knack import CLI

        return CLI(cli_name="avc", commands_loader_cls=CliCommandsLoader)

    @patch("cli._local_review")
    def test_cli_review_generate_requires_language(self, mock_review):
        """Validate that review generate requires --language."""
        cli = self._make_cli()
        exit_code = cli.invoke(["review", "generate"])
        assert exit_code != 0

    @patch("cli._local_review")
    def test_cli_review_generate_validates_language_choices(self, mock_review):
        """Validate that review generate rejects invalid language values."""
        cli = self._make_cli()
        exit_code = cli.invoke(["review", "generate", "-l", "invalid_lang", "-t", "somefile.txt"])
        assert exit_code != 0

    @patch("cli.review_job_get")
    def test_cli_review_get_job_requires_job_id(self, mock_get):
        """Validate review get-job requires --job-id."""
        cli = self._make_cli()
        exit_code = cli.invoke(["review", "get-job"])
        assert exit_code != 0

    @patch("cli.db_get")
    def test_cli_db_get_requires_args(self, mock_db_get):
        """Validate db get requires --container-name and --id."""
        cli = self._make_cli()
        exit_code = cli.invoke(["db", "get"])
        assert exit_code != 0

    @patch("cli.db_get")
    def test_cli_db_get_validates_container_choices(self, mock_db_get):
        """Validate db get rejects invalid container names."""
        cli = self._make_cli()
        exit_code = cli.invoke(["db", "get", "-c", "invalid_container", "-i", "some-id"])
        assert exit_code != 0

    @patch("cli.search_knowledge_base")
    def test_cli_search_kb_parses_args(self, mock_search):
        """Validate search kb rejects missing required args."""
        cli = self._make_cli()
        mock_search.return_value = None
        exit_code = cli.invoke(["search", "kb"])
        assert exit_code != 0

    @patch("cli.check_health")
    def test_cli_app_check_is_registered(self, mock_check):
        """Validate app check command is registered in the CLI."""
        from cli import CliCommandsLoader

        cli = self._make_cli()
        loader = CliCommandsLoader(cli_ctx=cli)
        command_table = loader.load_command_table(None)
        assert "app check" in command_table

    @patch("cli.extract_document_section")
    def test_cli_eval_extract_section_validates_args(self, mock_extract):
        """Validate eval extract-section requires apiview_path and size."""
        cli = self._make_cli()
        exit_code = cli.invoke(["eval", "extract-section"])
        assert exit_code != 0


# =====================================================================
# CustomJSONEncoder
# =====================================================================


class TestCustomJSONEncoder:
    """Tests for the CustomJSONEncoder class."""

    def test_encodes_object_with_to_dict(self):
        from cli import CustomJSONEncoder

        class Obj:
            def to_dict(self):
                return {"key": "value"}

        result = json.dumps(Obj(), cls=CustomJSONEncoder)
        assert '"key"' in result
        assert '"value"' in result

    def test_encodes_object_with_dict(self):
        from cli import CustomJSONEncoder

        class Obj:
            def __init__(self):
                self.key = "value"

        result = json.dumps(Obj(), cls=CustomJSONEncoder)
        assert '"key"' in result

    def test_encodes_regular_types(self):
        from cli import CustomJSONEncoder

        result = json.dumps({"a": 1, "b": [2, 3]}, cls=CustomJSONEncoder)
        assert '"a"' in result
