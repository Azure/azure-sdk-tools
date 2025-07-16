import pytest
from unittest.mock import Mock, patch
from datetime import datetime
import json

# Import the module under test
import cli


class TestMetrics:
    """Test class for metrics functions."""

    @patch("cli._get_apiview_revisions_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_basic(self, mock_comments_client, mock_revisions_client):
        """Test basic language adoption calculation."""

        # Mock revisions data (now includes ReviewId)
        mock_revisions = [
            {"ReviewId": "review1", "Language": "Python"},
            {"ReviewId": "review2", "Language": "Python"},
            {"ReviewId": "review3", "Language": "C#"},
            {"ReviewId": "review4", "Language": "Java"},
        ]

        # Mock AI comments data (now uses ReviewId)
        mock_ai_comments = [
            {"ReviewId": "review1"},  # Python review with AI comment
            {"ReviewId": "review3"},  # C# review with AI comment
        ]

        # Setup mock clients
        mock_revisions_client.return_value.query_items.return_value = mock_revisions
        mock_comments_client.return_value.query_items.return_value = mock_ai_comments

        # Call the function
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")

        # Verify results
        expected = {
            "python": "0.50",  # 1/2 Python reviews have AI comments
            "c#": "1.00",  # 1/1 C# review has AI comments
            "java": "0.00",  # 0/1 Java review has AI comments
        }
        assert result == expected

    @patch("cli._get_apiview_revisions_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_empty(self, mock_comments_client, mock_revisions_client):
        """Test language adoption calculation with no data."""

        # Mock empty data
        mock_revisions_client.return_value.query_items.return_value = []
        mock_comments_client.return_value.query_items.return_value = []

        # Call the function
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")

        # Verify empty result
        assert result == {}

    @patch("cli._get_apiview_revisions_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_no_ai_comments(self, mock_comments_client, mock_revisions_client):
        """Test language adoption with revisions but no AI comments."""

        # Mock revisions but no AI comments (now includes ReviewId)
        mock_revisions = [
            {"ReviewId": "review1", "Language": "Python"},
            {"ReviewId": "review2", "Language": "Python"},
        ]

        mock_revisions_client.return_value.query_items.return_value = mock_revisions
        mock_comments_client.return_value.query_items.return_value = []

        # Call the function
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")

        # Verify 0% adoption
        expected = {"python": "0.00"}
        assert result == expected

    @patch("cli._get_apiview_revisions_client")
    @patch("cli._get_apiview_cosmos_client")
    def test_calculate_language_adoption_multiple_revisions_per_review(self, mock_comments_client, mock_revisions_client):
        """Test language adoption with multiple revisions per review."""

        # Mock revisions data where the same ReviewId appears multiple times (multiple revisions)
        mock_revisions = [
            {"ReviewId": "review1", "Language": "Python"},  # First revision of review1
            {"ReviewId": "review1", "Language": "Python"},  # Second revision of review1
            {"ReviewId": "review2", "Language": "Python"},  # Only revision of review2
            {"ReviewId": "review3", "Language": "Java"},    # First revision of review3
            {"ReviewId": "review3", "Language": "Java"},    # Second revision of review3
        ]

        # Mock AI comments - only review1 and review3 have AI comments
        mock_ai_comments = [
            {"ReviewId": "review1"},  # Python review with AI comment
            {"ReviewId": "review3"},  # Java review with AI comment
        ]

        # Setup mock clients
        mock_revisions_client.return_value.query_items.return_value = mock_revisions
        mock_comments_client.return_value.query_items.return_value = mock_ai_comments

        # Call the function
        result = cli._calculate_language_adoption("2024-01-01", "2024-01-31")

        # Verify results - should count distinct ReviewIds, not individual revisions
        expected = {
            "python": "0.50",  # 1/2 Python reviews have AI comments (review1 yes, review2 no)
            "java": "1.00",    # 1/1 Java review has AI comments (review3 yes)
        }
        assert result == expected

    def test_datetime_parsing_in_language_adoption(self):
        """Test that datetime parsing works correctly in language adoption."""
        with patch("cli._get_apiview_revisions_client") as mock_revisions_client, patch(
            "cli._get_apiview_cosmos_client"
        ) as mock_comments_client:

            mock_revisions_client.return_value.query_items.return_value = []
            mock_comments_client.return_value.query_items.return_value = []

            # This should not raise an exception
            result = cli._calculate_language_adoption("2024-01-01", "2024-12-31")
            assert result == {}

    @patch("cli._calculate_language_adoption")
    @patch("cli._get_apiview_cosmos_client")
    def test_report_metrics_includes_language_adoption(self, mock_comments_client, mock_language_adoption):
        """Test that report_metrics includes language adoption in output."""

        # Mock existing functions
        mock_comments_client.return_value.query_items.return_value = []
        mock_language_adoption.return_value = {"python": "0.25", "java": "0.50"}

        # Call report_metrics and capture the return value
        with patch("builtins.print") as mock_print:
            result = cli.report_metrics("2024-01-01", "2024-01-31")

            # Verify language_adoption is in the metrics
            assert "language_adoption" in result["metrics"]
            assert result["metrics"]["language_adoption"] == {"python": "0.25", "java": "0.50"}

            # Verify print was called with JSON output
            mock_print.assert_called_once()
            printed_output = mock_print.call_args[0][0]
            parsed_output = json.loads(printed_output)
            assert "language_adoption" in parsed_output["metrics"]
