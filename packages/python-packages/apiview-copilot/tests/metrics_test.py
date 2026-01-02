# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Test for metrics functionality.
"""

from unittest.mock import patch

import src._metrics as metrics
from src._apiview import ActiveReviewMetadata


class TestMetrics:
    """Test class for metrics functions."""

    @staticmethod
    def _make_active_reviews():
        return [
            ActiveReviewMetadata(review_id="review1", name="azure-core", language="Python"),
            ActiveReviewMetadata(review_id="review2", name="azure-storage-blob", language="Python"),
            ActiveReviewMetadata(review_id="review3", name="azure-core", language="Java"),
        ]

    @staticmethod
    def _make_comments():
        # return raw dicts similar to what get_comments_in_date_range returns
        created_on = "2025-08-16T00:00:00Z"
        return [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "ElementId": "e1",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentType": "ai",
                "Upvotes": ["1"],
                "Downvotes": [],
                "CommentSource": "AIGenerated",
            },
            {
                "id": "c2",
                "ReviewId": "review2",
                "APIRevisionId": "rev2",
                "ElementId": "e2",
                "CommentText": "Human comment",
                "CreatedBy": "human",
                "CreatedOn": created_on,
                "CommentType": "human",
                "Upvotes": [],
                "Downvotes": [],
                "CommentSource": "UserGenerated",
            },
            {
                "id": "c3",
                "ReviewId": "review3",
                "APIRevisionId": "rev3",
                "ElementId": "e3",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentType": "ai",
                "Upvotes": [],
                "Downvotes": ["1"],
                "CommentSource": "AIGenerated",
            },
        ]

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_metrics_report_basic(self, mock_get_comments, mock_get_reviews):
        """Test metrics report generation and formatting."""
        mock_get_reviews.return_value = self._make_active_reviews()
        # Return raw dicts, as get_comments_in_date_range does
        mock_get_comments.return_value = self._make_comments()

        report = metrics.get_metrics_report("2025-08-15", "2025-09-12", environment="test")
        assert "metrics" in report
        # Check overall metrics
        overall = report["metrics"]["overall"]
        assert "adoption" in overall
        assert "comment_quality" in overall
        assert "comment_makeup" in overall
        # Check formatting (should be rounded to two decimals)
        assert isinstance(overall["adoption"]["adoption_rate"], float)
        assert round(overall["adoption"]["adoption_rate"], 2) == overall["adoption"]["adoption_rate"]

        # Validate actual values in comment_quality
        cq = overall["comment_quality"]
        # There are 2 AI comments: one upvoted, one downvoted, and one neutral (none)
        # From _make_comments: c1 (upvoted), c3 (downvoted), no neutral (since all have votes)
        # But c1: upvotes ["1"], c3: downvotes ["1"], c2: human
        assert cq == {
            "ai_comment_count": 2,
            "good": 0.5,
            "bad": 0.5,
            "neutral": 0.0,
        }

        cm = overall["comment_makeup"]
        # There is 1 human comment in a review with AI (review2 has no AI, so human_comment_count_without_copilot = 1)
        # But review1 and review3 have AI comments, review2 is human only
        assert cm == {
            "human_comment_count_without_copilot": 1,
            "human_comment_count_with_ai": 0,
            "ai_comment_count": 2,
            "ai_comment_rate": 1.0,
        }

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_metrics_report_empty(self, mock_get_comments, mock_get_reviews):
        """Test metrics report with no reviews or comments."""
        mock_get_reviews.return_value = []
        mock_get_comments.return_value = []
        report = metrics.get_metrics_report("2025-08-15", "2025-09-12", environment="test")
        overall = report["metrics"]["overall"]
        assert overall["adoption"]["active_review_count"] == 0
        assert overall["adoption"]["adoption_rate"] == 0.0

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_metrics_report_missing_element_id(self, mock_get_comments, mock_get_reviews):
        """Metrics report should not fail when comment dicts omit ElementId."""
        mock_get_reviews.return_value = self._make_active_reviews()
        comments = self._make_comments()
        # Simulate Cosmos query results where the property doesn't exist and is omitted.
        comments[0].pop("ElementId", None)
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2025-08-15", "2025-09-12", environment="test")
        assert report["metrics"]["overall"]["comment_quality"]["ai_comment_count"] == 2

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_metrics_report_language_split(self, mock_get_comments, mock_get_reviews):
        """Test metrics report with multiple languages."""
        reviews = [
            ActiveReviewMetadata(review_id="review1", name="azure-core", language="Python"),
            ActiveReviewMetadata(review_id="review2", name="azure-core", language="Java"),
        ]
        # use raw dicts for comments (as returned by get_comments_in_date_range)
        created_on = "2025-08-16T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "ElementId": "e1",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentType": "ai",
                "CommentSource": "AIGenerated",
                "Upvotes": ["1"],
                "Downvotes": [],
            },
            {
                "id": "c2",
                "ReviewId": "review2",
                "APIRevisionId": "rev2",
                "ElementId": "e2",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentType": "ai",
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": ["1"],
            },
        ]
        mock_get_reviews.return_value = reviews
        mock_get_comments.return_value = comments
        report = metrics.get_metrics_report("2025-08-15", "2025-09-12", environment="test")
        # language keys may preserve case from ActiveReviewMetadata; check case-insensitively
        keys = {k.lower(): k for k in report["metrics"].keys()}
        assert "python" in keys
        assert "java" in keys
        py_adoption = report["metrics"]["Python"]["adoption"]
        java_adoption = report["metrics"]["Java"]["adoption"]

        assert py_adoption == {
            "active_review_count": 1,
            "copilot_review_count": 1,
            "adoption_rate": 1.0,
        }
        assert java_adoption == {
            "active_review_count": 1,
            "copilot_review_count": 1,
            "adoption_rate": 1.0,
        }

        # Also check comment_quality and comment_makeup for both languages
        py_cq = report["metrics"]["Python"]["comment_quality"]
        py_cm = report["metrics"]["Python"]["comment_makeup"]
        java_cq = report["metrics"]["Java"]["comment_quality"]
        java_cm = report["metrics"]["Java"]["comment_makeup"]

        # Both comments are AI, one upvoted, one downvoted
        assert py_cq == {
            "ai_comment_count": 1,
            "good": 1.0,
            "bad": 0.0,
            "neutral": 0.0,
        }
        assert java_cq == {
            "ai_comment_count": 1,
            "good": 0.0,
            "bad": 1.0,
            "neutral": 0.0,
        }
        assert py_cm == {
            "human_comment_count_without_copilot": 0,
            "human_comment_count_with_ai": 0,
            "ai_comment_count": 1,
            "ai_comment_rate": 1.0,
        }
        assert java_cm == {
            "human_comment_count_without_copilot": 0,
            "human_comment_count_with_ai": 0,
            "ai_comment_count": 1,
            "ai_comment_rate": 1.0,
        }

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_language_adoption_basic(self, mock_get_comments, mock_get_reviews):
        reviews = [
            ActiveReviewMetadata(review_id="review1", name="azure-core", language="Python"),
            ActiveReviewMetadata(review_id="review2", name="azure-storage-blob", language="Python"),
            ActiveReviewMetadata(review_id="review3", name="azure-core", language="C#"),
            ActiveReviewMetadata(review_id="review4", name="azure-core", language="Java"),
        ]
        created_on = "2024-01-05T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "ElementId": "e1",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentType": "ai",
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
            {
                "id": "c2",
                "ReviewId": "review3",
                "APIRevisionId": "rev3",
                "ElementId": "e3",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "CommentType": "ai",
                "Upvotes": [],
                "Downvotes": [],
            },
        ]
        mock_get_reviews.return_value = reviews
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2024-01-01", "2024-01-31", environment="test")
        keys = {k.lower(): k for k in report["metrics"].keys()}
        # Python should have 2 active reviews and 1 copilot review
        assert "python" in keys
        py_key = keys["python"]
        py_adoption = report["metrics"][py_key]["adoption"]
        assert py_adoption == {
            "active_review_count": 2,
            "copilot_review_count": 1,
            "adoption_rate": 0.5,
        }

        # Also check comment_quality and comment_makeup for Python
        py_cq = report["metrics"][py_key]["comment_quality"]
        py_cm = report["metrics"][py_key]["comment_makeup"]
        # Only one AI comment in Python reviews, no upvotes/downvotes
        assert py_cq == {
            "ai_comment_count": 1,
            "good": 0.0,
            "bad": 0.0,
            "neutral": 1.0,
        }
        assert py_cm == {
            "human_comment_count_without_copilot": 0,
            "human_comment_count_with_ai": 0,
            "ai_comment_count": 1,
            "ai_comment_rate": 1.0,
        }

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_language_adoption_no_ai_comments(self, mock_get_comments, mock_get_reviews):
        reviews = [
            ActiveReviewMetadata(review_id="review1", name="pkg", language="Python"),
            ActiveReviewMetadata(review_id="review2", name="pkg2", language="Python"),
        ]
        mock_get_reviews.return_value = reviews
        mock_get_comments.return_value = []

        report = metrics.get_metrics_report("2024-01-01", "2024-01-31", environment="test")
        keys = {k.lower(): k for k in report["metrics"].keys()}
        py_key = keys["python"]
        py_adoption = report["metrics"][py_key]["adoption"]
        assert py_adoption == {
            "active_review_count": 2,
            "copilot_review_count": 0,
            "adoption_rate": 0.0,
        }
