# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Test for metrics functionality.
"""

from unittest.mock import Mock, patch

import src._metrics as metrics


class TestMetrics:
    """Test class for metrics functions."""

    # Helper methods for mock setup
    @staticmethod
    def _make_mock_comments(ai_comments):
        mock_comments = Mock()
        mock_comments.query_items.return_value = ai_comments
        return mock_comments

    @staticmethod
    def _make_mock_reviews(reviews_data):
        mock_reviews = Mock()
        mock_reviews.query_items.return_value = iter(reviews_data)
        return mock_reviews

    def _set_cosmos_client_side_effect(self, mock_cosmos_client, mock_comments, mock_reviews):
        def cosmos_client_side_effect(*_, **kwargs):
            if kwargs.get("container_name") == "Comments":
                return mock_comments
            elif kwargs.get("container_name") == "Reviews":
                return mock_reviews
            raise ValueError(f"Unexpected container_name: {kwargs.get('container_name')}")

        mock_cosmos_client.side_effect = cosmos_client_side_effect

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_calculate_language_adoption_basic(self, mock_cosmos_client):
        """Test basic language adoption calculation."""
        reviews_data = [
            {"id": "review1", "Language": "Python", "PackageName": "azure-core"},
            {"id": "review2", "Language": "Python", "PackageName": "azure-storage-blob"},
            {"id": "review3", "Language": "C#", "PackageName": "azure-core"},
            {"id": "review4", "Language": "Java", "PackageName": "azure-core"},
        ]
        ai_comments = [
            {"ReviewId": "review1", "CreatedBy": "azure-sdk"},
            {"ReviewId": "review3", "CreatedBy": "azure-sdk"},
        ]
        mock_comments = self._make_mock_comments(ai_comments)
        mock_reviews = self._make_mock_reviews(reviews_data)
        self._set_cosmos_client_side_effect(mock_cosmos_client, mock_comments, mock_reviews)

        result = metrics._calculate_language_adoption("2024-01-01", "2024-01-31")  # pylint: disable=protected-access
        expected = {
            "python": {
                "adoption_rate": "0.50",
                "active_reviews": 2,
                "active_copilot_reviews": 1,
            },
            "c#": {
                "adoption_rate": "1.00",
                "active_reviews": 1,
                "active_copilot_reviews": 1,
            },
            "java": {
                "adoption_rate": "0.00",
                "active_reviews": 1,
                "active_copilot_reviews": 0,
            },
        }
        assert result == expected

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_calculate_language_adoption_empty(self, mock_cosmos_client):
        """Test language adoption calculation with no data."""
        mock_comments = self._make_mock_comments([])
        mock_reviews = self._make_mock_reviews([])
        self._set_cosmos_client_side_effect(mock_cosmos_client, mock_comments, mock_reviews)

        result = metrics._calculate_language_adoption("2024-01-01", "2024-01-31")  # pylint: disable=protected-access
        assert not result

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_calculate_language_adoption_no_ai_comments(self, mock_cosmos_client):
        """Test language adoption with revisions but no AI comments."""
        reviews_data = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
        ]
        mock_comments = self._make_mock_comments([])
        mock_reviews = self._make_mock_reviews(reviews_data)
        self._set_cosmos_client_side_effect(mock_cosmos_client, mock_comments, mock_reviews)

        result = metrics._calculate_language_adoption("2024-01-01", "2024-01-31")  # pylint: disable=protected-access
        expected = {
            "python": {
                "adoption_rate": "0.00",
                "active_reviews": 2,
                "active_copilot_reviews": 0,
            }
        }
        assert result == expected

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_calculate_language_adoption_multiple_revisions_per_review(self, mock_cosmos_client):
        """Test language adoption with multiple revisions per review."""
        reviews_data = [
            {"id": "review1", "Language": "Python", "PackageName": "azure-core"},
            {"id": "review2", "Language": "Python", "PackageName": "azure-storage-blob"},
            {"id": "review3", "Language": "Java", "PackageName": "azure-core"},
        ]
        ai_comments = [
            {"ReviewId": "review1", "CreatedBy": "azure-sdk"},
            {"ReviewId": "review3", "CreatedBy": "azure-sdk"},
        ]
        mock_comments = self._make_mock_comments(ai_comments)
        mock_reviews = self._make_mock_reviews(reviews_data)
        self._set_cosmos_client_side_effect(mock_cosmos_client, mock_comments, mock_reviews)

        result = metrics._calculate_language_adoption("2024-01-01", "2024-01-31")  # pylint: disable=protected-access
        expected = {
            "python": {
                "adoption_rate": "0.50",
                "active_reviews": 2,
                "active_copilot_reviews": 1,
            },
            "java": {
                "adoption_rate": "1.00",
                "active_reviews": 1,
                "active_copilot_reviews": 1,
            },
        }
        assert result == expected

    @patch("src._apiview.get_apiview_cosmos_client")
    def test_datetime_parsing_in_language_adoption(self, mock_cosmos_client):
        """Test that datetime parsing works correctly in language adoption."""
        mock_comments = self._make_mock_comments([])
        mock_reviews = self._make_mock_reviews([])
        self._set_cosmos_client_side_effect(mock_cosmos_client, mock_comments, mock_reviews)

        result = metrics._calculate_language_adoption("2024-01-01", "2024-12-31")  # pylint: disable=protected-access
        assert not result

    @patch("src._metrics._calculate_language_adoption")
    @patch("src._apiview.get_apiview_cosmos_client")
    def test_report_metrics_includes_language_adoption(self, mock_cosmos_client, mock_language_adoption):
        """Test that report_metrics includes language adoption in output."""
        mock_comments = self._make_mock_comments([])
        mock_cosmos_client.return_value = mock_comments
        mock_language_adoption.return_value = {
            "python": {
                "adoption_rate": "0.25",
                "active_reviews": 4,
                "active_copilot_reviews": 1,
            },
            "java": {
                "adoption_rate": "0.50",
                "active_reviews": 2,
                "active_copilot_reviews": 1,
            },
        }

        result = metrics.get_metrics_report("2024-01-01", "2024-01-31", environment="test")
        assert "language_adoption" in result["metrics"]
        expected_language_adoption = {
            "python": {
                "adoption_rate": "0.25",
                "active_reviews": 4,
                "active_copilot_reviews": 1,
            },
            "java": {
                "adoption_rate": "0.50",
                "active_reviews": 2,
                "active_copilot_reviews": 1,
            },
        }
        assert result["metrics"]["language_adoption"] == expected_language_adoption
