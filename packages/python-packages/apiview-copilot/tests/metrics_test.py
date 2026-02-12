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
from src._apiview import ActiveReviewMetadata, ActiveRevisionMetadata


class TestMetrics:
    """Test class for metrics functions."""

    @staticmethod
    def _make_active_reviews():
        return [
            ActiveReviewMetadata(
                review_id="review1",
                name="azure-core",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev1"],
                        package_version="1.0.0",
                        approval="2025-08-16T00:00:00Z",
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review2",
                name="azure-storage-blob",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev2"],
                        package_version="2.0.0",
                        approval="2025-08-16T00:00:00Z",
                        has_copilot_review=False,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review3",
                name="azure-core",
                language="Java",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev3"],
                        package_version="1.0.0",
                        approval="2025-08-16T00:00:00Z",
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
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
        # There are 2 AI comments: c1 (upvoted, from rev1), c3 (downvoted, from rev3)
        assert cq == {
            "ai_comment_count": 2,
            "good": 0.5,
            "bad": 0.5,
            "neutral": 0.0,
            "good_count": 1,
            "bad_count": 1,
            "deleted_count": 0,
            "deleted": 0.0,
            "implicit_good_count": 0,
            "implicit_good": 0.0,
            "implicit_bad_count": 0,
            "implicit_bad": 0.0,
            "neutral_count": 0,
        }

        cm = overall["comment_makeup"]
        # c1 and c3 are AI comments from revisions with copilot (rev1, rev3)
        # c2 is a human comment from a revision without copilot (rev2)
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
            ActiveReviewMetadata(
                review_id="review1",
                name="azure-core",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev1"],
                        package_version="1.0.0",
                        approval="2025-08-16T00:00:00Z",
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review2",
                name="azure-core",
                language="Java",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev2"],
                        package_version="1.0.0",
                        approval="2025-08-16T00:00:00Z",
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
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
            "good_count": 1,
            "bad_count": 0,
            "deleted_count": 0,
            "deleted": 0.0,
            "implicit_good_count": 0,
            "implicit_good": 0.0,
            "implicit_bad_count": 0,
            "implicit_bad": 0.0,
            "neutral_count": 0,
        }
        assert java_cq == {
            "ai_comment_count": 1,
            "good": 0.0,
            "bad": 1.0,
            "neutral": 0.0,
            "good_count": 0,
            "bad_count": 1,
            "deleted_count": 0,
            "deleted": 0.0,
            "implicit_good_count": 0,
            "implicit_good": 0.0,
            "implicit_bad_count": 0,
            "implicit_bad": 0.0,
            "neutral_count": 0,
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
            ActiveReviewMetadata(
                review_id="review1",
                name="azure-core",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev1"],
                        package_version="1.0.0",
                        approval="2024-01-05T00:00:00Z",
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review2",
                name="azure-storage-blob",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev2"],
                        package_version="1.0.0",
                        approval="2024-01-05T00:00:00Z",
                        has_copilot_review=False,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review3",
                name="azure-core",
                language="C#",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev3"],
                        package_version="1.0.0",
                        approval="2024-01-05T00:00:00Z",
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review4",
                name="azure-core",
                language="Java",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev4"],
                        package_version="1.0.0",
                        approval="2024-01-05T00:00:00Z",
                        has_copilot_review=False,
                        version_type="GA",
                    )
                ],
            ),
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
        # Only one AI comment in Python reviews (rev1, approved), no upvotes/downvotes/resolved = implicit_bad
        assert py_cq == {
            "ai_comment_count": 1,
            "good": 0.0,
            "bad": 0.0,
            "neutral": 0.0,
            "good_count": 0,
            "bad_count": 0,
            "deleted_count": 0,
            "deleted": 0.0,
            "implicit_good_count": 0,
            "implicit_good": 0.0,
            "implicit_bad_count": 1,
            "implicit_bad": 1.0,
            "neutral_count": 0,
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
            ActiveReviewMetadata(
                review_id="review1",
                name="pkg",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev1"],
                        package_version="1.0.0",
                        approval="2024-01-05T00:00:00Z",
                        has_copilot_review=False,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review2",
                name="pkg2",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev2"],
                        package_version="1.0.0",
                        approval="2024-01-05T00:00:00Z",
                        has_copilot_review=False,
                        version_type="GA",
                    )
                ],
            ),
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

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_multiple_revisions_same_package_version_counts_as_one(self, mock_get_comments, mock_get_reviews):
        """Test that multiple revision IDs for the same package version count as ONE active package."""
        # Create a review with one package version that has multiple revision IDs
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1a", "rev1b"],  # Two revisions for same package version
                    package_version="12.28.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = []

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        py_adoption = report["metrics"]["Python"]["adoption"]

        # Should count as 1 active package, not 2
        assert py_adoption["active_review_count"] == 1
        assert py_adoption["copilot_review_count"] == 1
        assert py_adoption["adoption_rate"] == 1.0

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comments_summed_across_multiple_revisions_same_package(self, mock_get_comments, mock_get_reviews):
        """Test that comments from all revision IDs of the same package version are summed together."""
        # Create a review with one package version that has two revision IDs
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1a", "rev1b"],  # Two revisions for same package version
                    package_version="12.28.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        # Comments spread across both revision IDs
        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1a",  # Comment on first revision
                "CommentText": "AI comment 1",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": ["user1"],
                "Downvotes": [],
            },
            {
                "id": "c2",
                "ReviewId": "review1",
                "APIRevisionId": "rev1b",  # Comment on second revision
                "CommentText": "AI comment 2",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
            {
                "id": "c3",
                "ReviewId": "review1",
                "APIRevisionId": "rev1a",  # Another comment on first revision
                "CommentText": "Human comment",
                "CreatedBy": "user1",
                "CreatedOn": created_on,
                "CommentSource": "UserGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        py_metrics = report["metrics"]["Python"]

        # Should count all comments from both revisions
        assert py_metrics["comment_quality"]["ai_comment_count"] == 2
        assert py_metrics["comment_makeup"]["human_comment_count_with_ai"] == 1
        assert py_metrics["comment_makeup"]["ai_comment_count"] == 2

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_copilot_status_shared_across_revision_ids(self, mock_get_comments, mock_get_reviews):
        """Test that all revision IDs from the same package version share the same copilot status."""
        # Package with copilot
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1a", "rev1b"],
                    package_version="12.28.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,  # Has copilot
                    version_type="GA",
                )
            ],
        )

        # Package without copilot
        review2 = ActiveReviewMetadata(
            review_id="review2",
            name="azure-appconfiguration",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev2a", "rev2b"],
                    package_version="1.8.0",
                    approval="2026-01-27T00:00:00Z",
                    has_copilot_review=False,  # No copilot
                    version_type="GA",
                )
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            # Comments on rev1a and rev1b (copilot package)
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1a",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
            {
                "id": "c2",
                "ReviewId": "review1",
                "APIRevisionId": "rev1b",
                "CommentText": "Human comment",
                "CreatedBy": "user1",
                "CreatedOn": created_on,
                "CommentSource": "UserGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
            # Comments on rev2a and rev2b (non-copilot package)
            {
                "id": "c3",
                "ReviewId": "review2",
                "APIRevisionId": "rev2a",
                "CommentText": "Human comment 1",
                "CreatedBy": "user2",
                "CreatedOn": created_on,
                "CommentSource": "UserGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
            {
                "id": "c4",
                "ReviewId": "review2",
                "APIRevisionId": "rev2b",
                "CommentText": "Human comment 2",
                "CreatedBy": "user3",
                "CreatedOn": created_on,
                "CommentSource": "UserGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
        ]

        mock_get_reviews.return_value = [review1, review2]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        py_metrics = report["metrics"]["Python"]

        # Verify adoption counts
        assert py_metrics["adoption"]["active_review_count"] == 2
        assert py_metrics["adoption"]["copilot_review_count"] == 1

        # Verify comment categorization
        # Comments on rev1a and rev1b should be categorized as "with copilot"
        assert py_metrics["comment_makeup"]["human_comment_count_with_ai"] == 1  # c2 on rev1b
        assert py_metrics["comment_makeup"]["ai_comment_count"] == 1  # c1 on rev1a
        # Comments on rev2a and rev2b should be categorized as "without copilot"
        assert py_metrics["comment_makeup"]["human_comment_count_without_copilot"] == 2  # c3, c4

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_diagnostic_comments_excluded(self, mock_get_comments, mock_get_reviews):
        """Test that comments with CommentSource='Diagnostic' are excluded from all counts."""
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1"],
                    package_version="12.28.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
            {
                "id": "c2",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Diagnostic comment",
                "CreatedBy": "system",
                "CreatedOn": created_on,
                "CommentSource": "Diagnostic",  # Should be excluded
                "Upvotes": [],
                "Downvotes": [],
            },
            {
                "id": "c3",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Human comment",
                "CreatedBy": "user1",
                "CreatedOn": created_on,
                "CommentSource": "UserGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        py_metrics = report["metrics"]["Python"]

        # Diagnostic comment should not be counted
        assert py_metrics["comment_quality"]["ai_comment_count"] == 1  # Only c1
        assert py_metrics["comment_makeup"]["human_comment_count_with_ai"] == 1  # Only c3
        assert py_metrics["comment_makeup"]["ai_comment_count"] == 1  # Only c1

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comment_quality_deleted_comments(self, mock_get_comments, mock_get_reviews):
        """Test that deleted AI comments only count in deleted_count, not other categories."""
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1"],
                    package_version="1.0.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Deleted AI comment with upvotes",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": ["user1"],
                "Downvotes": [],
                "IsDeleted": True,  # Deleted - should only count as deleted, not good
            },
            {
                "id": "c2",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Deleted AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
                "IsDeleted": True,  # Deleted
            },
            {
                "id": "c3",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Non-deleted AI comment with upvotes",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": ["user2"],
                "Downvotes": [],
                "IsDeleted": False,
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        cq = report["metrics"]["Python"]["comment_quality"]

        assert cq["ai_comment_count"] == 3  # All 3 AI comments counted
        assert cq["deleted_count"] == 2  # c1 and c2
        assert cq["deleted"] == round(2 / 3, 2)
        assert cq["good_count"] == 1  # Only c3 (non-deleted with upvotes)
        assert cq["bad_count"] == 0
        assert cq["implicit_good_count"] == 0
        assert cq["implicit_bad_count"] == 0
        assert cq["neutral_count"] == 0

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comment_quality_implicit_good(self, mock_get_comments, mock_get_reviews):
        """Test that resolved AI comments with no votes count as implicit_good."""
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1"],
                    package_version="1.0.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Resolved AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
                "IsResolved": True,  # Resolved, no votes = implicit good
            },
            {
                "id": "c2",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Unresolved AI comment",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
                "IsResolved": False,  # Unresolved, no votes, approved = implicit bad
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        cq = report["metrics"]["Python"]["comment_quality"]

        assert cq["ai_comment_count"] == 2
        assert cq["implicit_good_count"] == 1  # c1
        assert cq["implicit_good"] == 0.5
        assert cq["implicit_bad_count"] == 1  # c2
        assert cq["implicit_bad"] == 0.5

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comment_quality_implicit_bad(self, mock_get_comments, mock_get_reviews):
        """Test that AI comments in approved revisions with no action count as implicit_bad."""
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1"],
                    package_version="1.0.0",
                    approval="2026-01-06T00:00:00Z",  # Approved
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "AI comment with no action",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
                "IsResolved": False,
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        cq = report["metrics"]["Python"]["comment_quality"]

        assert cq["ai_comment_count"] == 1
        assert cq["implicit_bad_count"] == 1
        assert cq["implicit_bad"] == 1.0
        assert cq["good_count"] == 0
        assert cq["bad_count"] == 0
        assert cq["neutral_count"] == 0

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comment_quality_neutral_unapproved(self, mock_get_comments, mock_get_reviews):
        """Test that AI comments in unapproved revisions with no action count as neutral."""
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1"],
                    package_version="1.0.0",
                    approval=None,  # NOT approved
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "AI comment in unapproved revision",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
                "IsResolved": False,
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        cq = report["metrics"]["Python"]["comment_quality"]

        assert cq["ai_comment_count"] == 1
        assert cq["neutral_count"] == 1
        assert cq["neutral"] == 1.0
        assert cq["implicit_bad_count"] == 0
        assert cq["good_count"] == 0
        assert cq["bad_count"] == 0

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comment_quality_downvotes_trump_upvotes(self, mock_get_comments, mock_get_reviews):
        """Test that any downvote trumps upvotes - comment counts as bad, not good."""
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1"],
                    package_version="1.0.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,
                    version_type="GA",
                )
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "AI comment with both up and downvotes",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": ["user1", "user2"],  # Multiple upvotes
                "Downvotes": ["user3"],  # One downvote - should trump upvotes
            },
            {
                "id": "c2",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "AI comment with only upvotes",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": ["user1"],
                "Downvotes": [],  # No downvotes = good
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        cq = report["metrics"]["Python"]["comment_quality"]

        assert cq["ai_comment_count"] == 2
        assert cq["bad_count"] == 1  # c1 (downvote trumps upvotes)
        assert cq["good_count"] == 1  # c2
        assert cq["neutral_count"] == 0
        assert cq["implicit_good_count"] == 0
        assert cq["implicit_bad_count"] == 0

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comment_quality_ai_count_includes_unapproved(self, mock_get_comments, mock_get_reviews):
        """Test that ai_comment_count includes comments from BOTH approved and unapproved revisions."""
        reviews = [
            ActiveReviewMetadata(
                review_id="review1",
                name="azure-storage-blob",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev1"],
                        package_version="1.0.0",
                        approval="2026-01-06T00:00:00Z",  # Approved
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
            ActiveReviewMetadata(
                review_id="review2",
                name="azure-core",
                language="Python",
                revisions=[
                    ActiveRevisionMetadata(
                        revision_ids=["rev2"],
                        package_version="2.0.0",
                        approval=None,  # NOT approved
                        has_copilot_review=True,
                        version_type="GA",
                    )
                ],
            ),
        ]

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",  # Approved revision
                "CommentText": "AI comment in approved",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": ["user1"],
                "Downvotes": [],
            },
            {
                "id": "c2",
                "ReviewId": "review2",
                "APIRevisionId": "rev2",  # Unapproved revision
                "CommentText": "AI comment in unapproved",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": [],
                "Downvotes": [],
            },
        ]

        mock_get_reviews.return_value = reviews
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        cq = report["metrics"]["Python"]["comment_quality"]
        cm = report["metrics"]["Python"]["comment_makeup"]

        # comment_quality.ai_comment_count includes ALL active revisions
        assert cq["ai_comment_count"] == 2
        assert cq["good_count"] == 1  # c1
        assert cq["neutral_count"] == 1  # c2 (unapproved, no action)

        # comment_makeup.ai_comment_count only includes approved revisions
        assert cm["ai_comment_count"] == 1  # Only c1

    @patch("src._metrics.get_active_reviews")
    @patch("src._metrics.get_comments_in_date_range")
    def test_comment_quality_categories_sum_to_total(self, mock_get_comments, mock_get_reviews):
        """Test that all category counts sum exactly to ai_comment_count."""
        review1 = ActiveReviewMetadata(
            review_id="review1",
            name="azure-storage-blob",
            language="Python",
            revisions=[
                ActiveRevisionMetadata(
                    revision_ids=["rev1"],
                    package_version="1.0.0",
                    approval="2026-01-06T00:00:00Z",
                    has_copilot_review=True,
                    version_type="GA",
                ),
                ActiveRevisionMetadata(
                    revision_ids=["rev2"],
                    package_version="2.0.0",
                    approval=None,  # Unapproved
                    has_copilot_review=True,
                    version_type="GA",
                ),
            ],
        )

        created_on = "2026-01-07T00:00:00Z"
        comments = [
            # Deleted
            {
                "id": "c1",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Deleted",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "IsDeleted": True,
            },
            # Bad (downvoted)
            {
                "id": "c2",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Bad",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Downvotes": ["user1"],
            },
            # Good (upvoted, no downvotes)
            {
                "id": "c3",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Good",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "Upvotes": ["user1"],
            },
            # Implicit good (resolved, no votes)
            {
                "id": "c4",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Implicit good",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
                "IsResolved": True,
            },
            # Implicit bad (approved, not resolved, no votes)
            {
                "id": "c5",
                "ReviewId": "review1",
                "APIRevisionId": "rev1",
                "CommentText": "Implicit bad",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
            },
            # Neutral (unapproved, not resolved, no votes)
            {
                "id": "c6",
                "ReviewId": "review1",
                "APIRevisionId": "rev2",
                "CommentText": "Neutral",
                "CreatedBy": "azure-sdk",
                "CreatedOn": created_on,
                "CommentSource": "AIGenerated",
            },
        ]

        mock_get_reviews.return_value = [review1]
        mock_get_comments.return_value = comments

        report = metrics.get_metrics_report("2026-01-01", "2026-01-31", environment="test")
        cq = report["metrics"]["Python"]["comment_quality"]

        # Verify counts
        assert cq["ai_comment_count"] == 6
        assert cq["deleted_count"] == 1
        assert cq["bad_count"] == 1
        assert cq["good_count"] == 1
        assert cq["implicit_good_count"] == 1
        assert cq["implicit_bad_count"] == 1
        assert cq["neutral_count"] == 1

        # Verify sum equals total
        total_from_categories = (
            cq["deleted_count"]
            + cq["bad_count"]
            + cq["good_count"]
            + cq["implicit_good_count"]
            + cq["implicit_bad_count"]
            + cq["neutral_count"]
        )
        assert total_from_categories == cq["ai_comment_count"]
