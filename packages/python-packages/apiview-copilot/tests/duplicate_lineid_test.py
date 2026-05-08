# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=no-member,redefined-outer-name

"""Tests for duplicate line ID compliance metrics (build_duplicate_lineid_reports)."""

import sys
from datetime import date
from io import StringIO
from unittest.mock import MagicMock, patch

# Mock azure.cosmos before importing modules
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()

from src._apiview_metrics import (  # pylint: disable=wrong-import-position
    build_duplicate_lineid_reports,
    print_duplicate_lineid_report,
)


def _make_revision(review_id, language, has_duplicate_line_ids=None, created_on="2026-04-15T00:00:00Z"):
    """Helper to create a fake revision dict."""
    rev = {
        "ReviewId": review_id,
        "Language": language,
        "CreatedOn": created_on,
    }
    if has_duplicate_line_ids is not None:
        rev["HasDuplicateLineIds"] = has_duplicate_line_ids
    return rev


class TestBuildDuplicateLineidReports:
    """Tests for build_duplicate_lineid_reports."""

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_latest_revision_per_review(self, mock_cosmos):
        """When a review has multiple revisions, only the latest is counted."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", True, created_on="2026-04-10T00:00:00Z"),
            _make_revision("r1", "Python", False, created_on="2026-04-20T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_duplicate_lineid_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        # The later revision has HasDuplicateLineIds=False, so should be clean
        assert reports["Python"][0]["clean"] == 1
        assert reports["Python"][0]["has_duplicates"] == 0
        assert reports["Python"][0]["unknown"] == 0

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_missing_field_tracked_as_unknown(self, mock_cosmos):
        """Revisions without HasDuplicateLineIds are tracked as unknown."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", False, created_on="2026-04-10T00:00:00Z"),
            _make_revision("r2", "Python", None, created_on="2026-04-15T00:00:00Z"),  # field absent
            _make_revision("r3", "Python", True, created_on="2026-04-20T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_duplicate_lineid_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        point = reports["Python"][0]
        assert point["clean"] == 1
        assert point["has_duplicates"] == 1
        assert point["unknown"] == 1
        assert point["total"] == 3

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_unknown_excluded_from_percentage(self, mock_cosmos):
        """Clean percentage is calculated only from evaluated (clean + has_duplicates) revisions."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", False, created_on="2026-04-05T00:00:00Z"),
            _make_revision("r2", "Python", True, created_on="2026-04-10T00:00:00Z"),
            _make_revision("r3", "Python", None, created_on="2026-04-15T00:00:00Z"),  # absent
            _make_revision("r4", "Python", None, created_on="2026-04-20T00:00:00Z"),  # absent
        ]
        mock_cosmos.return_value = container

        reports = build_duplicate_lineid_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        point = reports["Python"][0]
        # 1 clean out of 2 evaluated = 50%
        assert point["clean_pct"] == 50.0

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_all_unknown_yields_zero_pct(self, mock_cosmos):
        """If all revisions are unknown, clean_pct is 0."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", None, created_on="2026-04-10T00:00:00Z"),
            _make_revision("r2", "Python", None, created_on="2026-04-15T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_duplicate_lineid_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        point = reports["Python"][0]
        assert point["unknown"] == 2
        assert point["clean_pct"] == 0.0

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_omits_excluded_languages(self, mock_cosmos):
        """Languages in OMIT_LANGUAGES (e.g., TypeSpec) are not counted."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", False, created_on="2026-04-10T00:00:00Z"),
            _make_revision("r2", "TypeSpec", False, created_on="2026-04-12T00:00:00Z"),
            _make_revision("r3", "Swagger", True, created_on="2026-04-14T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_duplicate_lineid_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        # Only Python should be in the report
        assert reports["Python"][0]["total"] == 1
        assert reports["Python"][0]["clean"] == 1

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_buckets_by_month(self, mock_cosmos):
        """Revisions are correctly bucketed into their respective months."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", False, created_on="2026-03-15T00:00:00Z"),
            _make_revision("r2", "Python", True, created_on="2026-04-10T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_duplicate_lineid_reports(
            languages=["Python"],
            months=2,
            end_date=date(2026, 4, 30),
        )

        assert len(reports["Python"]) == 2
        assert reports["Python"][0]["label"] == "2026-03"
        assert reports["Python"][0]["clean"] == 1
        assert reports["Python"][0]["has_duplicates"] == 0
        assert reports["Python"][1]["label"] == "2026-04"
        assert reports["Python"][1]["clean"] == 0
        assert reports["Python"][1]["has_duplicates"] == 1

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_empty_results(self, mock_cosmos):
        """Empty query results produce zeroed reports."""
        container = MagicMock()
        container.query_items.return_value = []
        mock_cosmos.return_value = container

        reports = build_duplicate_lineid_reports(
            languages=["Go"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        assert reports["Go"][0]["clean"] == 0
        assert reports["Go"][0]["has_duplicates"] == 0
        assert reports["Go"][0]["unknown"] == 0
        assert reports["Go"][0]["total"] == 0
        assert reports["Go"][0]["clean_pct"] == 0.0


class TestPrintDuplicateLineidReport:
    """Tests for print_duplicate_lineid_report."""

    def test_prints_unknown_column(self):
        """The printed table includes the Unknown column."""
        reports = {
            "Python": [
                {
                    "label": "2026-04",
                    "start_date": "2026-04-01",
                    "end_date": "2026-04-30",
                    "clean": 5,
                    "has_duplicates": 2,
                    "unknown": 3,
                    "total": 10,
                    "clean_pct": 71.43,
                }
            ]
        }

        output = StringIO()
        print_duplicate_lineid_report(reports, None, file=output)
        text = output.getvalue()

        assert "Unknown" in text
        assert "Python" in text
        assert "2026-04" in text
