# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``apiview`` CLI commands."""

from unittest.mock import MagicMock, patch

import pytest


class TestApiviewGetComments:
    """Tests for `apiview get-comments` command."""

    @patch("cli.ApiViewClient")
    def test_get_comments_reaches_client(self, mock_client_cls):
        """Validate that get_apiview_comments instantiates ApiViewClient and fetches comments."""
        from cli import get_apiview_comments

        mock_instance = MagicMock()
        mock_client_cls.return_value = mock_instance

        async def fake_get_comments(**kwargs):
            return [
                {"lineNo": "10", "comment": "test", "createdOn": "2025-01-01"},
                {"lineNo": "10", "comment": "reply", "createdOn": "2025-01-02"},
            ]

        mock_instance.get_review_comments = fake_get_comments

        result = get_apiview_comments(revision_id="rev-123")

        mock_client_cls.assert_called_once_with(environment="production")
        assert "10" in result
        assert len(result["10"]) == 2


class TestApiviewGetActiveReviews:
    """Tests for `apiview get-active-reviews` command."""

    @patch("cli._get_active_reviews")
    def test_get_active_reviews_filters_by_language(self, mock_get_reviews):
        """Validate that reviews are filtered by language."""
        from cli import get_active_reviews

        mock_review = MagicMock()
        mock_review.language = "Python"
        mock_review.review_id = "rev-1"
        mock_review.name = "azure-test"
        mock_review.revisions = []
        mock_get_reviews.return_value = [mock_review]

        result = get_active_reviews(start_date="2025-01-01", end_date="2025-12-31", language="python")

        mock_get_reviews.assert_called_once()
        assert isinstance(result, list)

    @patch("cli._get_active_reviews")
    def test_get_active_reviews_summary_mode(self, mock_get_reviews, capsys):
        """Validate summary mode output."""
        from cli import get_active_reviews

        mock_rev = MagicMock()
        mock_rev.approval = "2025-06-01T00:00:00Z"
        mock_rev.has_copilot_review = True
        mock_rev.package_version = "1.0.0"
        mock_rev.version_type = "GA"

        mock_review = MagicMock()
        mock_review.language = "Python"
        mock_review.name = "azure-test"
        mock_review.revisions = [mock_rev]
        mock_get_reviews.return_value = [mock_review]

        result = get_active_reviews(start_date="2025-01-01", end_date="2025-12-31", language="python", summary=True)

        assert result is None  # summary mode returns None
        captured = capsys.readouterr()
        assert "azure-test" in captured.out


class TestApiviewResolvePackage:
    """Tests for `apiview resolve-package` command."""

    @patch("cli.resolve_package")
    def test_resolve_package_local(self, mock_resolve, capsys):
        """Validate local resolve-package calls resolve_package."""
        from cli import resolve_package_info

        mock_resolve.return_value = {
            "package_name": "azure-test",
            "review_id": "rev-1",
            "revision_id": "rev-id-1",
        }

        resolve_package_info(package_query="azure-test", language="python")

        mock_resolve.assert_called_once_with(
            package_query="azure-test", language="python", version=None, environment="production"
        )
        captured = capsys.readouterr()
        assert "azure-test" in captured.out

    @patch("cli.requests.post")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_resolve_package_remote(self, mock_auth, mock_post, mock_settings, capsys):
        """Validate remote resolve-package posts to API."""
        from cli import resolve_package_info

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = {"package_name": "azure-test"}
        mock_post.return_value = mock_resp

        resolve_package_info(package_query="azure-test", language="python", remote=True)

        mock_post.assert_called_once()

    @patch("cli.resolve_package")
    def test_resolve_package_not_found(self, mock_resolve, capsys):
        """Validate output when package is not found."""
        from cli import resolve_package_info

        mock_resolve.return_value = None

        resolve_package_info(package_query="nonexistent", language="python")

        captured = capsys.readouterr()
        assert "No package found" in captured.out


class TestApiviewGetCommentFeedback:
    """Tests for `apiview get-comment-feedback` command."""

    @patch("cli._get_ai_comment_feedback")
    def test_get_comment_feedback_json(self, mock_feedback, capsys):
        """Validate JSON output mode."""
        from cli import get_ai_comment_feedback

        mock_feedback.return_value = [{"comment": "test", "feedback": "good"}]

        get_ai_comment_feedback(
            language="python",
            start_date="2025-01-01",
            end_date="2025-12-31",
        )

        mock_feedback.assert_called_once()
        captured = capsys.readouterr()
        assert "test" in captured.out

    @patch("cli._get_ai_comment_feedback")
    def test_get_comment_feedback_yaml(self, mock_feedback, capsys):
        """Validate YAML output mode."""
        from cli import get_ai_comment_feedback

        mock_feedback.return_value = [{"comment": "test", "feedback": "good"}]

        get_ai_comment_feedback(
            language="python",
            start_date="2025-01-01",
            end_date="2025-12-31",
            output_format="yaml",
        )

        captured = capsys.readouterr()
        assert "comment:" in captured.out or "test" in captured.out


class TestApiviewAnalyzeComments:
    """Tests for `apiview analyze-comments` command."""

    @patch("cli.run_prompty")
    @patch("cli.get_approvers")
    @patch("cli.get_apiview_cosmos_client")
    @patch("cli.get_comments_in_date_range")
    def test_analyze_comments_reaches_prompty(
        self, mock_get_comments, mock_cosmos_client, mock_approvers, mock_prompty, capsys
    ):
        """Validate that analyze_comments filters comments and calls run_prompty."""
        from cli import analyze_comments

        mock_get_comments.return_value = [
            {
                "ReviewId": "rev-1",
                "CommentSource": "Human",
                "IsDeleted": False,
                "CreatedBy": "user1",
                "Comment": "This method should be renamed.",
            }
        ]
        mock_approvers.return_value = ["user1"]

        mock_container = MagicMock()
        mock_container.query_items.return_value = [{"id": "rev-1", "Language": "python"}]
        mock_cosmos_client.return_value = mock_container

        mock_prompty.return_value = "Theme analysis result"

        analyze_comments(language="python", start_date="2025-01-01", end_date="2025-12-31")

        mock_prompty.assert_called_once()
        captured = capsys.readouterr()
        assert "Theme analysis result" in captured.out
