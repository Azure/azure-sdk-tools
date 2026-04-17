# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Tests for the comment bucket trends CLI integration."""

from datetime import date

import cli
from src._comment_bucket_trends import get_last_n_month_ranges


def test_get_last_n_month_ranges_includes_partial_end_month():
    """The month containing the end date should count even when only partially included."""
    assert get_last_n_month_ranges(months=6, end_date=date(2026, 4, 1)) == [
        (date(2025, 11, 1), date(2025, 11, 30)),
        (date(2025, 12, 1), date(2025, 12, 31)),
        (date(2026, 1, 1), date(2026, 1, 31)),
        (date(2026, 2, 1), date(2026, 2, 28)),
        (date(2026, 3, 1), date(2026, 3, 31)),
        (date(2026, 4, 1), date(2026, 4, 1)),
    ]


def test_report_comment_bucket_trends_generates_chart(monkeypatch):
    """The CLI helper should pass the selected options through to the chart generator."""
    observed = {}

    def fake_build_language_comment_bucket_reports(
        languages,
        months,
        end_date,
        include_human,
        include_neutral,
        environment,
    ):
        observed["build"] = {
            "languages": languages,
            "months": months,
            "end_date": end_date,
            "include_human": include_human,
            "include_neutral": include_neutral,
            "environment": environment,
        }
        return {"Python": []}

    def fake_generate_chart(reports, output_path, include_human, include_neutral, raw):
        observed["chart"] = {
            "reports": reports,
            "output_path": output_path,
            "include_human": include_human,
            "include_neutral": include_neutral,
            "raw": raw,
        }
        return output_path

    def fake_print_report(reports, output_path, include_human, include_neutral):
        observed["print"] = {
            "reports": reports,
            "output_path": output_path,
            "include_human": include_human,
            "include_neutral": include_neutral,
        }

    monkeypatch.setattr(cli, "build_language_comment_bucket_reports", fake_build_language_comment_bucket_reports)
    monkeypatch.setattr(cli, "generate_comment_bucket_chart", fake_generate_chart)
    monkeypatch.setattr(cli, "print_comment_bucket_report", fake_print_report)

    cli.report_comment_bucket_trends(
        months=4,
        end_date="2026-04-01",
        languages=["Python", "Java"],
        human=True,
        neutral=True,
    )

    assert observed["build"] == {
        "languages": ["Python", "Java"],
        "months": 4,
        "end_date": date(2026, 4, 1),
        "include_human": True,
        "include_neutral": True,
        "environment": "production",
    }
    assert observed["chart"]["reports"] == {"Python": []}
    assert observed["chart"]["output_path"] == cli.DEFAULT_COMMENT_BUCKET_OUTPUT_PATH
    assert observed["chart"]["include_human"] is True
    assert observed["chart"]["include_neutral"] is True
    assert observed["chart"]["raw"] is False
    assert observed["print"]["output_path"] == cli.DEFAULT_COMMENT_BUCKET_OUTPUT_PATH
