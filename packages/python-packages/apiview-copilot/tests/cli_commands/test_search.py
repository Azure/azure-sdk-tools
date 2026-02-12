# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``search`` CLI commands (kb, reindex, all-guidelines)."""

import os
from unittest.mock import MagicMock, patch

import pytest

from .conftest import make_temp_file


class TestSearchKb:
    """Tests for `search kb` command."""

    @patch("cli.SearchManager")
    def test_search_kb_with_text(self, mock_search_cls, capsys):
        """Validate search kb with text query."""
        from cli import search_knowledge_base

        mock_search = MagicMock()
        mock_results = MagicMock()
        mock_results.results = []
        mock_search.search_all.return_value = mock_results
        mock_search.build_context.return_value = {"guidelines": [], "examples": []}
        mock_search_cls.return_value = mock_search

        search_knowledge_base(language="python", text="naming conventions")

        mock_search_cls.assert_called_once_with(language="python")
        mock_search.search_all.assert_called_once()

    @patch("cli.SearchManager")
    def test_search_kb_with_ids(self, mock_search_cls, capsys):
        """Validate search kb with IDs."""
        from cli import search_knowledge_base

        mock_search = MagicMock()
        mock_search.search_all_by_id.return_value = []
        mock_search_cls.return_value = mock_search

        search_knowledge_base(ids=["id-1", "id-2"])

        mock_search_cls.assert_called_once_with()
        mock_search.search_all_by_id.assert_called_once_with(["id-1", "id-2"])

    def test_search_kb_ids_with_language_raises(self):
        """Validate error when --ids is used with other params."""
        from cli import search_knowledge_base

        with pytest.raises(ValueError, match="do not provide any other parameters"):
            search_knowledge_base(ids=["id-1"], language="python")

    def test_search_kb_no_language_no_ids_raises(self):
        """Validate error when neither --ids nor --language is provided."""
        from cli import search_knowledge_base

        with pytest.raises(ValueError, match="--language.*required"):
            search_knowledge_base(text="some text")

    def test_search_kb_text_and_path_raises(self):
        """Validate error when both --text and --path are provided."""
        from cli import search_knowledge_base

        with pytest.raises(ValueError, match="Provide one of"):
            search_knowledge_base(language="python", text="query", path="/some/path")

    def test_search_kb_neither_text_nor_path_raises(self):
        """Validate error when neither --text nor --path is provided."""
        from cli import search_knowledge_base

        with pytest.raises(ValueError, match="Provide one of"):
            search_knowledge_base(language="python")

    @patch("cli.SearchManager")
    def test_search_kb_with_path(self, mock_search_cls):
        """Validate search kb with file path."""
        from cli import search_knowledge_base

        query_file = make_temp_file("def my_function():\n    pass\n", suffix=".py")
        mock_search = MagicMock()
        mock_results = MagicMock()
        mock_results.results = []
        mock_search.search_all.return_value = mock_results
        mock_search.build_context.return_value = {"guidelines": []}
        mock_search_cls.return_value = mock_search

        try:
            search_knowledge_base(language="python", path=query_file)
            mock_search.search_all.assert_called_once()
        finally:
            os.unlink(query_file)

    @patch("cli.SearchManager")
    def test_search_kb_markdown_mode(self, mock_search_cls, capsys):
        """Validate markdown output mode."""
        from cli import search_knowledge_base

        mock_search = MagicMock()
        mock_results = MagicMock()
        mock_results.results = []
        mock_search.search_all.return_value = mock_results
        mock_context = MagicMock()
        mock_context.to_markdown.return_value = "# Guidelines\n\nSome markdown."
        mock_search.build_context.return_value = mock_context
        mock_search_cls.return_value = mock_search

        search_knowledge_base(language="python", text="test", markdown=True)

        mock_context.to_markdown.assert_called_once()


class TestSearchReindex:
    """Tests for `search reindex` command."""

    @patch("cli.SearchManager.run_indexers")
    def test_reindex_all(self, mock_run_indexers):
        """Validate reindex without specific containers."""
        from cli import reindex_search

        mock_run_indexers.return_value = None

        reindex_search()

        mock_run_indexers.assert_called_once()

    @patch("cli.SearchManager.run_indexers")
    def test_reindex_specific_containers(self, mock_run_indexers):
        """Validate reindex with specific containers."""
        from cli import reindex_search

        mock_run_indexers.return_value = None

        reindex_search(containers=["guidelines", "examples"])

        mock_run_indexers.assert_called_once_with(container_names=["guidelines", "examples"])


class TestSearchAllGuidelines:
    """Tests for `search all-guidelines` command."""

    @patch("cli.SearchManager")
    def test_all_guidelines_json(self, mock_search_cls, capsys):
        """Validate all-guidelines in JSON mode."""
        from cli import get_all_guidelines

        mock_search = MagicMock()
        mock_search.language_guidelines = MagicMock()
        mock_search.language_guidelines.results = []
        mock_search.build_context.return_value = {"guidelines": []}
        mock_search_cls.return_value = mock_search

        get_all_guidelines(language="python")

        mock_search_cls.assert_called_once_with(language="python")
        mock_search.build_context.assert_called_once()

    @patch("cli.SearchManager")
    def test_all_guidelines_markdown(self, mock_search_cls, capsys):
        """Validate all-guidelines in Markdown mode."""
        from cli import get_all_guidelines

        mock_search = MagicMock()
        mock_search.language_guidelines = MagicMock()
        mock_search.language_guidelines.results = []
        mock_context = MagicMock()
        mock_context.to_markdown.return_value = "# Guidelines"
        mock_search.build_context.return_value = mock_context
        mock_search_cls.return_value = mock_search

        get_all_guidelines(language="python", markdown=True)

        mock_context.to_markdown.assert_called_once()
