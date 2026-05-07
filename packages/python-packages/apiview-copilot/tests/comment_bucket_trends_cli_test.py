# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Tests for the comment bucket trends CLI integration."""

import builtins
from datetime import date
from types import SimpleNamespace

import cli
import src._comment_bucket_trends as comment_bucket_trends
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
    """The CLI helper should include human comments by default and pass options through."""
    observed = {}

    def fake_build_language_comment_bucket_reports(
        languages,
        months,
        end_date,
        include_human,
        include_neutral,
        environment,
        **_kwargs,
    ):
        if "build" not in observed:
            observed["build"] = {
                "languages": languages,
                "months": months,
                "end_date": end_date,
                "include_human": include_human,
                "include_neutral": include_neutral,
                "environment": environment,
            }
        return {"Python": []}

    def fake_generate_chart(reports, output_path, include_human, include_neutral, raw, environment, **_kwargs):
        if "chart" not in observed:
            observed["chart"] = {
                "reports": reports,
                "output_path": output_path,
                "include_human": include_human,
                "include_neutral": include_neutral,
                "raw": raw,
                "environment": environment,
            }
        return output_path

    def fake_print_report(reports, output_path, include_human, include_neutral, environment):
        observed["print"] = {
            "reports": reports,
            "output_path": output_path,
            "include_human": include_human,
            "include_neutral": include_neutral,
            "environment": environment,
        }

    monkeypatch.setattr(cli, "build_language_comment_bucket_reports", fake_build_language_comment_bucket_reports)
    monkeypatch.setattr(cli, "generate_comment_bucket_chart", fake_generate_chart)
    monkeypatch.setattr(cli, "print_comment_bucket_report", fake_print_report)

    cli.report_comment_bucket_trends(
        months=4,
        end_date="2026-04-01",
        languages=["Python", "Java"],
        exclude_human=False,
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


def test_report_comment_bucket_trends_can_exclude_human_comments(monkeypatch):
    """The CLI helper should exclude human comments only when explicitly requested."""
    observed = {}

    def fake_build_language_comment_bucket_reports(
        languages,
        months,
        end_date,
        include_human,
        include_neutral,
        environment,
        **_kwargs,
    ):
        _ = (languages, months, end_date, include_neutral, environment)
        observed["include_human"] = include_human
        return {"Python": []}

    monkeypatch.setattr(cli, "build_language_comment_bucket_reports", fake_build_language_comment_bucket_reports)
    monkeypatch.setattr(cli, "generate_comment_bucket_chart", lambda *args, **kwargs: cli.DEFAULT_COMMENT_BUCKET_OUTPUT_PATH)
    monkeypatch.setattr(cli, "print_comment_bucket_report", lambda *args, **kwargs: None)

    cli.report_comment_bucket_trends(exclude_human=True)

    assert observed["include_human"] is False


def test_report_comment_bucket_trends_normalizes_language_aliases(monkeypatch):
    """The CLI helper should accept common aliases for multi-language input."""
    observed = {}

    def fake_build_language_comment_bucket_reports(
        languages,
        months,
        end_date,
        include_human,
        include_neutral,
        environment,
        **_kwargs,
    ):
        _ = (months, end_date, include_human, include_neutral, environment)
        observed["languages"] = languages
        return {language: [] for language in languages}

    monkeypatch.setattr(cli, "build_language_comment_bucket_reports", fake_build_language_comment_bucket_reports)
    monkeypatch.setattr(cli, "generate_comment_bucket_chart", lambda *args, **kwargs: cli.DEFAULT_COMMENT_BUCKET_OUTPUT_PATH)
    monkeypatch.setattr(cli, "print_comment_bucket_report", lambda *args, **kwargs: None)

    cli.report_comment_bucket_trends(languages=["csharp", "TypeScript", "go"])

    assert observed["languages"] == ["C#", "JavaScript", "Go"]


def test_print_comment_bucket_report_includes_requested_environment(capsys):
    """The terminal summary should reflect the queried environment."""
    cli.print_comment_bucket_report(
        {"Python": []},
        cli.DEFAULT_COMMENT_BUCKET_OUTPUT_PATH,
        environment="staging",
    )

    captured = capsys.readouterr()

    assert "APIView staging" in captured.out


def test_print_comment_bucket_report_skips_saved_message_when_chart_missing(capsys, tmp_path):
    """The terminal summary should not claim a chart was saved when none exists."""
    output_path = tmp_path / "missing-chart.png"

    cli.print_comment_bucket_report(
        {"Python": []},
        output_path,
    )

    captured = capsys.readouterr()

    assert "Saved chart:" not in captured.out


def test_generate_comment_bucket_chart_handles_missing_matplotlib(monkeypatch, capsys, tmp_path):
    """Chart generation should degrade gracefully when matplotlib is unavailable."""
    real_import = builtins.__import__

    def fake_import(name, globals_=None, locals_=None, fromlist=(), level=0):
        if name == "matplotlib.pyplot":
            raise ImportError("matplotlib missing")
        return real_import(name, globals_, locals_, fromlist, level)

    monkeypatch.setattr(builtins, "__import__", fake_import)

    output_path = tmp_path / "chart.png"
    returned_path = comment_bucket_trends.generate_comment_bucket_chart({"Python": []}, output_path=output_path)
    captured = capsys.readouterr()

    assert returned_path is None
    assert not output_path.exists()
    assert "matplotlib is not installed" in captured.out


def test_build_language_comment_bucket_reports_batches_large_id_queries(monkeypatch):
    """Large review and revision ID sets should be queried in batches."""
    comments = [
        {
            "ReviewId": f"review-{index}",
            "APIRevisionId": f"revision-{index}",
            "CommentSource": "Copilot",
            "CreatedOn": "2026-04-01T12:00:00Z",
        }
        for index in range(205)
    ]
    observed_batches = {"Reviews": [], "APIRevisions": []}

    class FakeContainer:
        def __init__(self, name):
            self.name = name

        def query_items(self, query, parameters, enable_cross_partition_query):
            _ = (query, enable_cross_partition_query)
            observed_batches[self.name].append(len(parameters))
            return []

    monkeypatch.setattr(comment_bucket_trends, "get_comments_in_date_range", lambda *args, **kwargs: comments)
    monkeypatch.setattr(
        comment_bucket_trends,
        "get_apiview_cosmos_client",
        lambda container_name, environment: FakeContainer(container_name),
    )
    monkeypatch.setattr(
        comment_bucket_trends,
        "_build_metrics_segment",
        lambda *args, **kwargs: SimpleNamespace(
            upvoted_ai_comment_count=0,
            implicit_good_ai_comment_count=0,
            neutral_ai_comment_count=0,
            human_comment_count_with_ai=0,
            implicit_bad_ai_comment_count=0,
            downvoted_ai_comment_count=0,
            deleted_ai_comment_count=0,
        ),
    )

    reports = comment_bucket_trends.build_language_comment_bucket_reports(
        languages=["Python"],
        months=1,
        end_date=date(2026, 4, 17),
    )

    assert list(reports.keys()) == ["Python"]
    assert observed_batches["Reviews"] == [100, 100, 5]
    assert observed_batches["APIRevisions"] == [100, 100, 5]
