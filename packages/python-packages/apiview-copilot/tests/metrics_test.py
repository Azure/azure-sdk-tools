import pytest
from unittest.mock import Mock, patch
from datetime import datetime
import json

# Import the module under test
import cli


class TestMetrics:
    """Test class for metrics functions."""

    @patch("cli._get_apiview_reviews_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_basic(self, mock_comments_client, mock_reviews_client):
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
        mock_reviews_client.return_value.query_items.return_value = mock_reviews
        mock_comments_client.return_value.query_items.return_value = mock_ai_comments
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")
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

    @patch("cli._get_apiview_reviews_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_empty(self, mock_comments_client, mock_reviews_client):
        """Test language adoption calculation with no data."""
        mock_reviews_client.return_value.query_items.return_value = []
        mock_comments_client.return_value.query_items.return_value = []
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")
        assert result == {}

    @patch("cli._get_apiview_reviews_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_no_ai_comments(self, mock_comments_client, mock_reviews_client):
        """Test language adoption with revisions but no AI comments."""
        mock_reviews = [
            {"id": "review1", "Language": "Python"},
            {"id": "review2", "Language": "Python"},
        ]
        mock_reviews_client.return_value.query_items.return_value = mock_reviews
        mock_comments_client.return_value.query_items.return_value = []
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")
        expected = {
            "python": {
                "adoption_rate": "0.00",
                "active_reviews": 2,
                "active_copilot_reviews": 0,
            }
        }
        assert result == expected

    @patch("cli._get_apiview_reviews_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_multiple_revisions_per_review(self, mock_comments_client, mock_reviews_client):
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
        mock_reviews_client.return_value.query_items.return_value = mock_reviews
        mock_comments_client.return_value.query_items.return_value = mock_ai_comments
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")
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

    def test_datetime_parsing_in_language_adoption(self):
        """Test that datetime parsing works correctly in language adoption."""
        with patch("cli._get_apiview_reviews_client") as mock_reviews_client, patch(
            "cli._get_apiview_cosmos_client"
        ) as mock_comments_client:
            mock_reviews_client.return_value.query_items.return_value = []
            mock_comments_client.return_value.query_items.return_value = []
            result = cli._calculate_language_adoption("2024-01-01", "2024-12-31")
            assert result == {}

    @patch("cli._calculate_language_adoption")
    @patch("cli._get_apiview_cosmos_client")
    def test_report_metrics_includes_language_adoption(self, mock_comments_client, mock_language_adoption):
        """Test that report_metrics includes language adoption in output."""

        # Mock existing functions
        mock_comments_client.return_value.query_items.return_value = []
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
        result = cli.report_metrics("2024-01-01", "2024-01-31")

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
