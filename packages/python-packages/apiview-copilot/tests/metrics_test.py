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

    @patch("src._metrics.get_apiview_cosmos_client")
    def test_calculate_language_adoption_basic(self, mock_cosmos_client):
        """Test basic language adoption calculation."""
        # Mock reviews data (should use 'id' not 'ReviewId')
        mock_reviews = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
            {"id": "review3", "Language": "C#"},
            {"id": "review4", "Language": "Java"},
        ]
        mock_ai_comments = [
            {"ReviewId": "review1", "CreatedBy": "azure-sdk"},
            {"ReviewId": "review3", "CreatedBy": "azure-sdk"},
        ]
        mock_comments = Mock()
        mock_comments.query_items.return_value = mock_ai_comments
        mock_reviews_data = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
            {"id": "review3", "Language": "C#"},
            {"id": "review4", "Language": "Java"},
        ]
        mock_reviews = Mock()
        mock_reviews.query_items.return_value = iter(mock_reviews_data)
        mock_cosmos_client.side_effect = [mock_comments, mock_reviews]
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

    @patch("src._metrics.get_apiview_cosmos_client")
    def test_calculate_language_adoption_empty(self, mock_cosmos_client):
        """Test language adoption calculation with no data."""
        mock_comments = Mock()
        mock_comments.query_items.return_value = []
        mock_reviews = Mock()
        mock_reviews.query_items.return_value = iter([])
        mock_cosmos_client.side_effect = [mock_comments, mock_reviews]
        result = metrics._calculate_language_adoption("2024-01-01", "2024-01-31")  # pylint: disable=protected-access
        assert not result

    @patch("src._metrics.get_apiview_cosmos_client")
    def test_calculate_language_adoption_no_ai_comments(self, mock_cosmos_client):
        """Test language adoption with revisions but no AI comments."""
        mock_reviews = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
        ]
        mock_comments = Mock()
        mock_comments.query_items.return_value = []
        mock_reviews_data = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
        ]
        mock_reviews = Mock()
        mock_reviews.query_items.return_value = iter(mock_reviews_data)
        mock_cosmos_client.side_effect = [mock_comments, mock_reviews]
        result = metrics._calculate_language_adoption("2024-01-01", "2024-01-31")  # pylint: disable=protected-access
        expected = {
            "python": {
                "adoption_rate": "0.00",
                "active_reviews": 2,
                "active_copilot_reviews": 0,
            }
        }
        assert result == expected

    @patch("src._metrics.get_apiview_cosmos_client")
    def test_calculate_language_adoption_multiple_revisions_per_review(self, mock_cosmos_client):
        """Test language adoption with multiple revisions per review."""
        mock_reviews = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
            {"id": "review3", "Language": "Java"},
        ]
        mock_ai_comments = [
            {"ReviewId": "review1", "CreatedBy": "azure-sdk"},
            {"ReviewId": "review3", "CreatedBy": "azure-sdk"},
        ]
        mock_comments = Mock()
        mock_comments.query_items.return_value = mock_ai_comments
        mock_reviews_data = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
            {"id": "review3", "Language": "Java"},
        ]
        mock_reviews = Mock()
        mock_reviews.query_items.return_value = iter(mock_reviews_data)
        mock_cosmos_client.side_effect = [mock_comments, mock_reviews]
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

    @patch("src._metrics.get_apiview_cosmos_client")
    def test_datetime_parsing_in_language_adoption(self, mock_cosmos_client):
        """Test that datetime parsing works correctly in language adoption."""
        mock_comments = Mock()
        mock_comments.query_items.return_value = []
        mock_reviews = Mock()
        mock_reviews.query_items.return_value = iter([])
        mock_cosmos_client.side_effect = [mock_comments, mock_reviews]
        result = metrics._calculate_language_adoption("2024-01-01", "2024-12-31")  # pylint: disable=protected-access
        assert not result

    @patch("src._metrics._calculate_language_adoption")
    @patch("src._metrics.get_apiview_cosmos_client")
    def test_report_metrics_includes_language_adoption(self, mock_cosmos_client, mock_language_adoption):
        """Test that report_metrics includes language adoption in output."""

        # Mock existing functions
        mock_comments = Mock()
        mock_comments.query_items.return_value = []
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

        # Call report_metrics and capture the return value
        result = metrics.get_metrics_report("2024-01-01", "2024-01-31", environment="test")

        # Verify language_adoption is in the metrics
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
