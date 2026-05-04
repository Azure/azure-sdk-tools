# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Tests for cross-language compliance metrics."""

import sys
from datetime import date
from unittest.mock import MagicMock, patch

# Mock azure.cosmos before importing modules
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()

from src._apiview import get_cross_language_compliance
from src._apiview_metrics import build_compliance_reports


def _make_revision(review_id, language, cross_lang_id=None, created_on="2026-04-15T00:00:00Z"):
    """Helper to create a fake revision dict."""
    return {
        "ReviewId": review_id,
        "Language": language,
        "APIRevisionType": "Automatic",
        "CrossLanguagePackageId": cross_lang_id,
        "CreatedOn": created_on,
    }


class TestGetCrossLanguageCompliance:
    """Tests for get_cross_language_compliance."""

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_all_compliant(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", "azure-core"),
            _make_revision("r2", "Java", "azure-core"),
        ]
        mock_cosmos.return_value = container

        result = get_cross_language_compliance("2026-04-01", "2026-04-30")

        assert result["totals"]["compliant"] == 2
        assert result["totals"]["non_compliant"] == 0
        assert result["totals"]["pct"] == 100.0
        assert result["by_language"]["Python"]["compliant"] == 1
        assert result["by_language"]["Java"]["compliant"] == 1

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_mixed_compliance(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", "azure-core"),
            _make_revision("r2", "Python", None),
            _make_revision("r3", "Java", None),
        ]
        mock_cosmos.return_value = container

        result = get_cross_language_compliance("2026-04-01", "2026-04-30")

        assert result["by_language"]["Python"]["compliant"] == 1
        assert result["by_language"]["Python"]["non_compliant"] == 1
        assert result["by_language"]["Python"]["pct"] == 50.0
        assert result["by_language"]["Java"]["non_compliant"] == 1

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_none_compliant(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", None),
            _make_revision("r2", "Python", ""),
        ]
        mock_cosmos.return_value = container

        result = get_cross_language_compliance("2026-04-01", "2026-04-30")

        assert result["by_language"]["Python"]["compliant"] == 0
        assert result["by_language"]["Python"]["non_compliant"] == 2
        assert result["by_language"]["Python"]["pct"] == 0.0

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_empty_results(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = []
        mock_cosmos.return_value = container

        result = get_cross_language_compliance("2026-04-01", "2026-04-30")

        assert result["by_language"] == {}
        assert result["totals"]["total"] == 0

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_excludes_languages(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", "azure-core"),
            _make_revision("r2", "Java", "azure-core"),
        ]
        mock_cosmos.return_value = container

        result = get_cross_language_compliance("2026-04-01", "2026-04-30", exclude_languages=["Java"])

        assert "Java" not in result["by_language"]
        assert result["totals"]["total"] == 1

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_omits_typespec_and_swagger(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", "azure-core"),
            _make_revision("r2", "TypeSpec", "azure-core"),
            _make_revision("r3", "Swagger", None),
        ]
        mock_cosmos.return_value = container

        result = get_cross_language_compliance("2026-04-01", "2026-04-30")

        assert "TypeSpec" not in result["by_language"]
        assert "Swagger" not in result["by_language"]
        assert result["totals"]["total"] == 1

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_latest_revision_per_review(self, mock_cosmos):
        """When a review has multiple revisions, only the latest is counted."""
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", None, created_on="2026-04-10T00:00:00Z"),
            _make_revision("r1", "Python", "azure-core", created_on="2026-04-20T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        result = get_cross_language_compliance("2026-04-01", "2026-04-30")

        # The later revision has CrossLanguagePackageId, so should be compliant
        assert result["by_language"]["Python"]["compliant"] == 1
        assert result["by_language"]["Python"]["non_compliant"] == 0


class TestBuildComplianceReports:
    """Tests for build_compliance_reports."""

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_builds_monthly_reports(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = [
            _make_revision("r1", "Python", "azure-core", created_on="2026-03-15T00:00:00Z"),
            _make_revision("r2", "Python", None, created_on="2026-03-20T00:00:00Z"),
            _make_revision("r3", "Python", "azure-storage", created_on="2026-04-10T00:00:00Z"),
        ]
        mock_cosmos.return_value = container

        reports = build_compliance_reports(
            languages=["Python"],
            months=2,
            end_date=date(2026, 4, 30),
        )

        assert "Python" in reports
        assert len(reports["Python"]) == 2
        assert reports["Python"][0]["label"] == "2026-03"
        assert reports["Python"][1]["label"] == "2026-04"
        # March: r1 compliant, r2 non-compliant
        assert reports["Python"][0]["compliant"] == 1
        assert reports["Python"][0]["non_compliant"] == 1
        assert reports["Python"][0]["total"] == 2
        # April: r3 compliant
        assert reports["Python"][1]["compliant"] == 1
        assert reports["Python"][1]["total"] == 1

    @patch("src._apiview_metrics.get_apiview_cosmos_client")
    def test_missing_language_defaults_to_zero(self, mock_cosmos):
        container = MagicMock()
        container.query_items.return_value = []
        mock_cosmos.return_value = container

        reports = build_compliance_reports(
            languages=["Go"],
            months=1,
            end_date=date(2026, 4, 30),
        )

        assert reports["Go"][0]["compliant"] == 0
        assert reports["Go"][0]["total"] == 0
        assert reports["Go"][0]["pct"] == 0.0
