# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``review`` CLI commands."""

import os
from unittest.mock import MagicMock, patch

from .conftest import make_temp_file, make_temp_json


class TestReviewGenerateLocal:
    """Tests for `review generate` command (local mode)."""

    @patch("cli.ApiViewReview")
    def test_local_review_reaches_reviewer(self, mock_reviewer_cls, sample_apiview_file):
        """Validate that _local_review instantiates ApiViewReview and calls run()."""
        from cli import _local_review

        mock_instance = MagicMock()
        mock_reviewer_cls.return_value = mock_instance

        _local_review(language="python", target=sample_apiview_file)

        mock_reviewer_cls.assert_called_once()
        mock_instance.run.assert_called_once()
        mock_instance.close.assert_called_once()

    @patch("cli.ApiViewReview")
    def test_local_review_with_base(self, mock_reviewer_cls, sample_apiview_file):
        """Validate local review with a base file for diff mode."""
        from cli import _local_review

        base_path = make_temp_file("namespace Azure.OldTest {}\n", suffix=".txt")
        mock_instance = MagicMock()
        mock_reviewer_cls.return_value = mock_instance

        try:
            _local_review(language="python", target=sample_apiview_file, base=base_path)
            mock_reviewer_cls.assert_called_once()
            call_kwargs = mock_reviewer_cls.call_args
            assert call_kwargs.kwargs.get("base") is not None or call_kwargs[1].get("base") is not None
        finally:
            os.unlink(base_path)

    @patch("cli.ApiViewReview")
    def test_local_review_with_outline_and_comments(self, mock_reviewer_cls, sample_apiview_file):
        """Validate local review with outline and existing comments."""
        from cli import _local_review

        outline_path = make_temp_file("Outline text here", suffix=".txt")
        comments_path = make_temp_json([{"line_no": "5", "comment": "Existing comment"}], suffix=".json")
        mock_instance = MagicMock()
        mock_reviewer_cls.return_value = mock_instance

        try:
            _local_review(
                language="python",
                target=sample_apiview_file,
                outline=outline_path,
                existing_comments=comments_path,
            )
            mock_reviewer_cls.assert_called_once()
        finally:
            os.unlink(outline_path)
            os.unlink(comments_path)


class TestReviewGenerateRemote:
    """Tests for `review generate --remote` command."""

    @patch("cli.review_job_get")
    @patch("cli.review_job_start")
    def test_remote_review_starts_job(self, mock_start, mock_get, sample_apiview_file):
        """Validate remote review starts a job and polls for result."""
        from cli import generate_review

        mock_start.return_value = {"job_id": "test-job-123"}
        mock_get.return_value = {"status": "Success", "comments": []}

        generate_review(language="python", target=sample_apiview_file, remote=True)

        mock_start.assert_called_once()
        mock_get.assert_called()


class TestReviewStartJob:
    """Tests for `review start-job` command."""

    @patch("cli.requests.post")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_start_job_posts_to_api(self, mock_auth, mock_post, mock_settings, sample_apiview_file):
        """Validate that start-job reads the file and POSTs to the API."""
        from cli import review_job_start

        mock_resp = MagicMock()
        mock_resp.status_code = 202
        mock_resp.json.return_value = {"job_id": "job-456"}
        mock_post.return_value = mock_resp

        result = review_job_start(language="python", target=sample_apiview_file)

        mock_post.assert_called_once()
        call_kwargs = mock_post.call_args
        assert "json" in call_kwargs.kwargs or len(call_kwargs[1]) > 0
        assert result == {"job_id": "job-456"}

    @patch("cli.requests.post")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_start_job_with_base_and_outline(self, mock_auth, mock_post, mock_settings, sample_apiview_file):
        """Validate start-job with optional base and outline files."""
        from cli import review_job_start

        base_path = make_temp_file("base content", suffix=".txt")
        outline_path = make_temp_file("outline content", suffix=".txt")

        mock_resp = MagicMock()
        mock_resp.status_code = 202
        mock_resp.json.return_value = {"job_id": "job-789"}
        mock_post.return_value = mock_resp

        try:
            result = review_job_start(
                language="python", target=sample_apiview_file, base=base_path, outline=outline_path
            )
            payload = mock_post.call_args.kwargs.get("json") or mock_post.call_args[1].get("json")
            assert "base" in payload
            assert "outline" in payload
        finally:
            os.unlink(base_path)
            os.unlink(outline_path)


class TestReviewGetJob:
    """Tests for `review get-job` command."""

    @patch("cli.requests.get")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_get_job_calls_api(self, mock_auth, mock_get, mock_settings):
        """Validate get-job GETs the correct endpoint."""
        from cli import review_job_get

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = {"status": "Success", "comments": []}
        mock_get.return_value = mock_resp

        result = review_job_get("job-123")

        mock_get.assert_called_once()
        assert result["status"] == "Success"


class TestReviewSummarize:
    """Tests for `review summarize` command."""

    @patch("cli.requests.post")
    @patch("cli._build_auth_header", return_value={"Authorization": "Bearer fake"})
    def test_summarize_posts_to_api(self, mock_auth, mock_post, mock_settings):
        """Validate summarize sends language and target to API."""
        from cli import review_summarize

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = {"summary": "This API looks good."}
        mock_post.return_value = mock_resp

        review_summarize(language="python", target="some_apiview_content")

        mock_post.assert_called_once()
        payload = mock_post.call_args.kwargs.get("json") or mock_post.call_args[1].get("json")
        assert payload["language"] == "python"
        assert payload["target"] == "some_apiview_content"


class TestReviewGroupComments:
    """Tests for `review group-comments` command."""

    def test_group_comments_file_not_found(self, capsys):
        """Validate that nonexistent file prints an error."""
        from cli import group_apiview_comments

        group_apiview_comments("/nonexistent/path.json")
        captured = capsys.readouterr()
        assert "does not exist" in captured.out

    @patch("src._comment_grouper.CommentGrouper")
    def test_group_comments_valid_file(self, mock_grouper_cls):
        """Validate that a valid comments file triggers the grouper."""
        from cli import group_apiview_comments

        comments_data = {
            "comments": [
                {
                    "line_no": 10,
                    "comment": "Test comment",
                    "bad_code": "old_code()",
                    "suggestion": "new_code()",
                },
            ]
        }
        path = make_temp_json(comments_data, suffix=".json")
        mock_instance = MagicMock()
        mock_instance.group.return_value = []
        mock_grouper_cls.return_value = mock_instance

        try:
            group_apiview_comments(path)
            mock_grouper_cls.assert_called_once()
            mock_instance.group.assert_called_once()
        finally:
            os.unlink(path)
