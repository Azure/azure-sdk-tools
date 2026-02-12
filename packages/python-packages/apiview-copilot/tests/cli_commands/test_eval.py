# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``eval`` CLI commands."""

from unittest.mock import MagicMock, patch

import pytest


class TestEvalExtractSection:
    """Tests for `eval extract-section` command."""

    def test_extract_section_valid_file(self, sample_apiview_file, capsys):
        """Validate section extraction on a valid file (no network call needed)."""
        from cli import extract_document_section

        extract_document_section(apiview_path=sample_apiview_file, size=500, index=1)

        captured = capsys.readouterr()
        assert "Azure" in captured.out or "namespace" in captured.out or len(captured.out) > 0

    def test_extract_section_invalid_file(self):
        """Validate error for nonexistent file."""
        from cli import extract_document_section

        with pytest.raises(ValueError, match="does not exist"):
            extract_document_section(apiview_path="/nonexistent/file.txt", size=500)

    def test_extract_section_out_of_range(self, sample_apiview_file, capsys):
        """Validate error for out-of-range index."""
        from cli import extract_document_section

        extract_document_section(apiview_path=sample_apiview_file, size=500, index=999)

        captured = capsys.readouterr()
        assert "out of range" in captured.out


class TestEvalRun:
    """Tests for `eval run` command."""

    @patch("cli.EvaluationRunner", create=True)
    @patch("cli.discover_targets", create=True)
    def test_eval_run_discovers_and_runs(self, mock_discover, mock_runner_cls):
        """Validate eval run discovers targets and runs them."""
        with patch("evals._discovery.discover_targets", return_value=[]) as mock_disc, patch(
            "evals._runner.EvaluationRunner"
        ) as mock_runner_cls2:
            from cli import run_evals

            mock_runner = MagicMock()
            mock_runner.run.return_value = []
            mock_runner_cls2.return_value = mock_runner

            run_evals(test_paths=[], num_runs=1)

            mock_disc.assert_called_once()
            mock_runner.run.assert_called_once()
            mock_runner.show_results.assert_called_once()
            mock_runner.cleanup.assert_called_once()
