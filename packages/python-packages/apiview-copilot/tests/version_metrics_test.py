# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=no-member

"""Tests for version-coverage metrics (build_version_reports)."""

import sys
from datetime import date
from unittest.mock import MagicMock, patch

# Mock azure.cosmos before importing modules
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()

from src._apiview_metrics import build_version_reports


def _make_revision(language, revision_type, package_version=None, created_on="2026-04-15T00:00:00Z"):
    """Helper to create a fake revision dict."""
    rev = {
        "Language": language,
        "APIRevisionType": revision_type,
        "CreatedOn": created_on,
    }
    if package_version is not None:
        rev["packageVersion"] = package_version
    return rev


class TestBuildVersionReports:
    """Tests for build_version_reports."""

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_buckets_by_month(self, mock_cosmos):
        """Revisions are correctly bucketed into their respective months."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("Python", "Automatic", "1.0.0", created_on="2026-03-10T00:00:00Z"),
            _make_revision("Python", "Manual", None, created_on="2026-03-20T00:00:00Z"),
            _make_revision("Python", "PullRequest", "2.0.0", created_on="2026-04-05T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_version_reports(
            languages=["Python"],
            months=2,
            end_date=date(2026, 4, 30),
        )

        assert "Python" in reports
        assert len(reports["Python"]) == 2
        assert reports["Python"][0]["label"] == "2026-03"
        assert reports["Python"][1]["label"] == "2026-04"

        # March: 1 versioned Automatic + 1 unversioned Manual = 50% overall
        march = reports["Python"][0]
        assert march["total"] == 2
        assert march["versioned"] == 1
        assert march["unversioned"] == 1
        assert march["versioned_pct"] == 50.0
        assert march["Automatic"]["total"] == 1
        assert march["Automatic"]["versioned"] == 1
        assert march["Automatic"]["versioned_pct"] == 100.0
        assert march["Manual"]["total"] == 1
        assert march["Manual"]["versioned"] == 0

        # April: 1 versioned PullRequest = 100%
        april = reports["Python"][1]
        assert april["total"] == 1
        assert april["versioned"] == 1
        assert april["PullRequest"]["total"] == 1
        assert april["PullRequest"]["versioned"] == 1
        assert april["PullRequest"]["versioned_pct"] == 100.0

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_empty_results(self, mock_cosmos):
        """Empty query results produce zero-filled data points."""
        container = MagicMock()
        container.query_items.return_value = []
        mock_cosmos.return_value = container

        reports = build_version_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        assert reports["Python"][0]["total"] == 0
        assert reports["Python"][0]["versioned"] == 0
        assert reports["Python"][0]["versioned_pct"] == 0.0

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_omits_excluded_languages(self, mock_cosmos):
        """Languages in OMIT_LANGUAGES are filtered out and don't appear in results."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("TypeSpec", "Automatic", "1.0.0", created_on="2026-04-10T00:00:00Z"),
            _make_revision("Python", "Automatic", "1.0.0", created_on="2026-04-10T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_version_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        # Only the Python revision should be counted
        assert reports["Python"][0]["total"] == 1

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_unknown_revision_type_skipped(self, mock_cosmos):
        """Revisions with unrecognized APIRevisionType are not counted."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("Python", "Unknown", "1.0.0", created_on="2026-04-10T00:00:00Z"),
            _make_revision("Python", "Automatic", "1.0.0", created_on="2026-04-10T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_version_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        assert reports["Python"][0]["total"] == 1

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_multiple_languages(self, mock_cosmos):
        """Multiple languages are reported independently."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("Python", "Automatic", "1.0.0", created_on="2026-04-10T00:00:00Z"),
            _make_revision("Java", "Automatic", None, created_on="2026-04-10T00:00:00Z"),
            _make_revision("Java", "Manual", "3.0.0", created_on="2026-04-15T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_version_reports(
            languages=["Python", "Java"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        assert reports["Python"][0]["total"] == 1
        assert reports["Python"][0]["versioned_pct"] == 100.0
        assert reports["Java"][0]["total"] == 2
        assert reports["Java"][0]["versioned"] == 1
        assert reports["Java"][0]["versioned_pct"] == 50.0

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_json_shape(self, mock_cosmos):
        """Emitted dict has the expected top-level keys and nested structure."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("Python", "Automatic", "1.0.0", created_on="2026-04-10T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_version_reports(
            languages=["Python"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        point = reports["Python"][0]
        # Top-level keys
        assert "label" in point
        assert "start_date" in point
        assert "end_date" in point
        assert "total" in point
        assert "versioned" in point
        assert "unversioned" in point
        assert "versioned_pct" in point
        # Per-type buckets
        for type_name in ("Automatic", "Manual", "PullRequest"):
            assert type_name in point
            bucket = point[type_name]
            assert "total" in bucket
            assert "versioned" in bucket
            assert "unversioned" in bucket
            assert "versioned_pct" in bucket

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_single_query_called(self, mock_cosmos):
        """Only one Cosmos query is issued regardless of month count."""
        container = MagicMock()
        container.query_items.return_value = []
        mock_cosmos.return_value = container

        build_version_reports(
            languages=["Python"],
            months=3,
            end_date=date(2026, 4, 30),
        )

        # get_apiview_cosmos_client called once, query_items called once
        assert mock_cosmos.call_count == 1
        assert container.query_items.call_count == 1
