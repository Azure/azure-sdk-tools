# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument

"""
Tests for resolve_package function in _apiview.py.
"""

import sys
from unittest.mock import MagicMock, patch

import pytest

# Mock azure.cosmos before importing _apiview
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()

from src._apiview import resolve_package


class MockContainerClient:
    """Mock Cosmos DB container client."""

    def __init__(self, items):
        self.items = items

    def query_items(self, query, parameters, enable_cross_partition_query=True):
        return iter(self.items)


@pytest.fixture
def mock_reviews_data():
    """Sample reviews data."""
    return [
        {"id": "review-1", "PackageName": "azure-storage-blob", "Language": "Python", "packageVersion": "12.0.0"},
        {"id": "review-2", "PackageName": "azure-identity", "Language": "Python", "packageVersion": "1.0.0"},
        {"id": "review-3", "PackageName": "azure-core", "Language": "Python", "packageVersion": "1.0.0"},
        {"id": "review-4", "PackageName": "Azure.Storage.Blobs", "Language": ".NET", "packageVersion": "12.0.0"},
    ]


@pytest.fixture
def mock_revisions_data():
    """Sample revisions data, ordered by _ts DESC (most recent first) as returned by database."""
    return [
        {
            "id": "revision-2",
            "Name": "azure-storage-blob",
            "Label": "Preview",
            "CreatedOn": "2024-02-01",
            "packageVersion": "12.1.0b1",
            "_ts": 1706745600,
        },
        {
            "id": "revision-1",
            "Name": "azure-storage-blob",
            "Label": "GA",
            "CreatedOn": "2024-01-01",
            "packageVersion": "12.0.0",
            "_ts": 1704067200,
        },
    ]


class TestResolvePackageExactMatch:
    """Tests for exact match scenarios."""

    def test_exact_match_case_insensitive(self, mock_reviews_data, mock_revisions_data):
        """Test that exact match works case-insensitively."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            # Setup mocks
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(mock_revisions_data)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            # Test exact match with different casing
            result = resolve_package("AZURE-STORAGE-BLOB", "python")

            assert result is not None
            assert result["package_name"] == "azure-storage-blob"
            assert result["review_id"] == "review-1"
            assert result["language"] == "Python"

    def test_exact_match_returns_latest_revision(self, mock_reviews_data, mock_revisions_data):
        """Test that exact match returns the latest revision."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(mock_revisions_data)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            result = resolve_package("azure-storage-blob", "python")

            assert result is not None
            assert result["revision_id"] == "revision-2"  # Most recent (highest _ts)


class TestResolvePackageLLMFallback:
    """Tests for LLM fallback scenarios."""

    def test_llm_fallback_when_no_exact_match(self, mock_reviews_data, mock_revisions_data):
        """Test that LLM is called when no exact match is found."""
        with (
            patch("src._apiview.get_apiview_cosmos_client") as mock_client,
            patch("src._utils.run_prompty") as mock_prompty,
        ):
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(mock_revisions_data)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container
            mock_prompty.return_value = "azure-storage-blob"

            result = resolve_package("storage blob package", "python")

            assert result is not None
            assert result["package_name"] == "azure-storage-blob"
            mock_prompty.assert_called_once()

    def test_llm_returns_no_match(self, mock_reviews_data):
        """Test that None is returned when LLM returns NO_MATCH."""
        with (
            patch("src._apiview.get_apiview_cosmos_client") as mock_client,
            patch("src._utils.run_prompty") as mock_prompty,
        ):
            reviews_container = MockContainerClient(mock_reviews_data)

            def get_container(container_name, environment, db_name=None):
                return reviews_container

            mock_client.side_effect = get_container
            mock_prompty.return_value = "NO_MATCH"

            result = resolve_package("nonexistent-package", "python")

            assert result is None

    def test_llm_exception_returns_none(self, mock_reviews_data):
        """Test that None is returned when LLM raises an exception."""
        with (
            patch("src._apiview.get_apiview_cosmos_client") as mock_client,
            patch("src._utils.run_prompty") as mock_prompty,
        ):
            reviews_container = MockContainerClient(mock_reviews_data)

            def get_container(container_name, environment, db_name=None):
                return reviews_container

            mock_client.side_effect = get_container
            mock_prompty.side_effect = Exception("LLM error")

            result = resolve_package("some-package", "python")

            assert result is None


class TestResolvePackageVersionFiltering:
    """Tests for version filtering scenarios."""

    def test_version_filter_returns_matching_version(self, mock_reviews_data):
        """Test that specifying a version returns the matching revision."""
        versioned_revisions = [
            {
                "id": "revision-v12",
                "Name": "azure-storage-blob",
                "Label": "GA",
                "packageVersion": "12.0.0",
                "_ts": 1704067200,
            },
        ]

        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(versioned_revisions)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            result = resolve_package("azure-storage-blob", "python", version="12.0.0")

            assert result is not None
            assert result["version"] == "12.0.0"
            assert result["revision_id"] == "revision-v12"

    def test_multiple_revisions_same_version_returns_most_recent(self, mock_reviews_data):
        """Test that when multiple revisions have the same version, the most recent (highest _ts) is returned."""
        # Multiple revisions with same version but different timestamps, ordered by _ts DESC
        versioned_revisions = [
            {
                "id": "revision-v12-newer",
                "Name": "azure-storage-blob",
                "Label": "GA Hotfix",
                "packageVersion": "12.0.0",
                "_ts": 1707350400,  # Feb 8, 2024 - most recent
            },
            {
                "id": "revision-v12-older",
                "Name": "azure-storage-blob",
                "Label": "GA",
                "packageVersion": "12.0.0",
                "_ts": 1704067200,  # Jan 1, 2024 - older
            },
        ]

        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(versioned_revisions)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            result = resolve_package("azure-storage-blob", "python", version="12.0.0")

            assert result is not None
            assert result["version"] == "12.0.0"
            assert result["revision_id"] == "revision-v12-newer"
            assert result["revision_label"] == "GA Hotfix"

    def test_version_not_found_falls_back_to_latest(self, mock_reviews_data, mock_revisions_data):
        """Test that when version is not found, latest revision is returned."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            # First call for versioned query returns empty, second returns latest
            call_count = [0]

            def mock_query_items(query, parameters, enable_cross_partition_query=True):
                call_count[0] += 1
                if call_count[0] == 1:
                    # Versioned query - no match
                    return iter([])
                # Latest query - return pre-sorted revisions
                return iter(mock_revisions_data)

            revisions_container = MagicMock()
            revisions_container.query_items = mock_query_items

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            result = resolve_package("azure-storage-blob", "python", version="99.0.0")

            assert result is not None
            # Should fall back to latest revision (most recent by _ts)
            assert result["revision_id"] == "revision-2"


class TestResolvePackageEdgeCases:
    """Tests for edge cases and error scenarios."""

    def test_no_packages_for_language_returns_none(self):
        """Test that None is returned when no packages exist for the language."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient([])

            def get_container(container_name, environment, db_name=None):
                return reviews_container

            mock_client.side_effect = get_container

            result = resolve_package("some-package", "rust")

            assert result is None

    def test_no_revisions_returns_partial_result(self, mock_reviews_data):
        """Test that partial result is returned when no revisions are found."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient([])

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            result = resolve_package("azure-storage-blob", "python")

            assert result is not None
            assert result["package_name"] == "azure-storage-blob"
            assert result["review_id"] == "review-1"
            assert result["revision_id"] is None
            assert result["version"] is None
            assert result["revision_label"] is None

    def test_database_exception_returns_none(self):
        """Test that None is returned when database raises an exception."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            mock_client.side_effect = Exception("Database connection error")

            result = resolve_package("azure-storage-blob", "python")

            assert result is None

    def test_language_normalization(self, mock_reviews_data, mock_revisions_data):
        """Test that language is normalized to pretty name."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(mock_revisions_data)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            # Use lowercase 'python' - should still match Python packages
            result = resolve_package("azure-storage-blob", "python")

            assert result is not None
            assert result["language"] == "Python"

    def test_llm_match_not_found_in_results(self, mock_reviews_data):
        """Test that None is returned when LLM returns a package not in results."""
        with (
            patch("src._apiview.get_apiview_cosmos_client") as mock_client,
            patch("src._utils.run_prompty") as mock_prompty,
        ):
            reviews_container = MockContainerClient(mock_reviews_data)

            def get_container(container_name, environment, db_name=None):
                return reviews_container

            mock_client.side_effect = get_container
            # LLM returns a package name that doesn't exist in our data
            mock_prompty.return_value = "nonexistent-package"

            result = resolve_package("some description", "python")

            assert result is None

    def test_environment_parameter_passed_correctly(self, mock_reviews_data, mock_revisions_data):
        """Test that environment parameter is passed to cosmos client."""
        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(mock_revisions_data)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            resolve_package("azure-storage-blob", "python", environment="staging")

            # Verify cosmos client was called with staging environment
            calls = mock_client.call_args_list
            assert any(call.kwargs.get("environment") == "staging" or "staging" in call.args for call in calls)


class TestResolvePackageRevisionLabel:
    """Tests for revision label handling."""

    def test_revision_label_included_in_result(self, mock_reviews_data):
        """Test that revision label is included in the result."""
        revisions_with_label = [
            {
                "id": "revision-1",
                "Name": "azure-storage-blob",
                "Label": "GA Release",
                "packageVersion": "12.0.0",
                "_ts": 1704067200,
            },
        ]

        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(revisions_with_label)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            result = resolve_package("azure-storage-blob", "python")

            assert result is not None
            assert result["revision_label"] == "GA Release"

    def test_missing_label_returns_empty_string(self, mock_reviews_data):
        """Test that missing label returns empty string."""
        revisions_no_label = [
            {
                "id": "revision-1",
                "Name": "azure-storage-blob",
                "packageVersion": "12.0.0",
                "_ts": 1704067200,
            },
        ]

        with patch("src._apiview.get_apiview_cosmos_client") as mock_client:
            reviews_container = MockContainerClient(mock_reviews_data)
            revisions_container = MockContainerClient(revisions_no_label)

            def get_container(container_name, environment, db_name=None):
                if container_name == "Reviews":
                    return reviews_container
                return revisions_container

            mock_client.side_effect = get_container

            result = resolve_package("azure-storage-blob", "python")

            assert result is not None
            assert result["revision_label"] == ""
