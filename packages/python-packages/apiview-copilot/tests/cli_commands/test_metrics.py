# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``metrics`` CLI commands."""

from unittest.mock import patch


class TestMetricsReport:
    """Tests for `metrics report` command."""

    @patch("cli.get_metrics_report")
    def test_metrics_report_calls_function(self, mock_report):
        """Validate metrics report calls get_metrics_report with correct args."""
        from cli import report_metrics

        mock_report.return_value = {"total_reviews": 42}

        result = report_metrics(start_date="2025-01-01", end_date="2025-12-31")

        mock_report.assert_called_once_with("2025-01-01", "2025-12-31", "production", False, False, False, None)
        assert result == {"total_reviews": 42}

    @patch("cli.get_metrics_report")
    def test_metrics_report_with_options(self, mock_report):
        """Validate metrics report with markdown, save, charts, and exclude options."""
        from cli import report_metrics

        mock_report.return_value = {}

        report_metrics(
            start_date="2025-01-01",
            end_date="2025-12-31",
            markdown=True,
            save=True,
            charts=True,
            exclude=["Java"],
        )

        mock_report.assert_called_once_with("2025-01-01", "2025-12-31", "production", True, True, True, ["Java"])
