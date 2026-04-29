# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument

"""
Tests for the report-issue endpoint helpers in app.py.

We must mock heavy module-level dependencies (Cosmos DB, Settings, telemetry)
before importing app.py, since it initialises them at import time.
"""

import json
import sys
from unittest.mock import MagicMock, patch

import pytest

# Patch module-level singletons before importing app
with patch("src._database_manager.DatabaseManager.get_instance", return_value=MagicMock()), \
     patch("src._settings.SettingsManager.__init__", return_value=None), \
     patch("src._settings.SettingsManager.get", return_value=""), \
     patch("azure.monitor.opentelemetry.configure_azure_monitor", return_value=None):
    from app import (
        CommentContextRequest,
        ReportIssueRequest,
        _build_report_issue_title,
        _build_report_issue_body,
        _build_comment_context_text,
        _generate_report_issue,
        _get_report_issue_labels,
    )


class TestBuildReportIssueTitle:

    def test_general_mode_short_description(self):
        request = ReportIssueRequest(mode="general", description="Something is broken")
        title = _build_report_issue_title(request)
        assert title == "[APIView] Something is broken"

    def test_general_mode_long_description_truncates(self):
        long_desc = "A" * 100
        request = ReportIssueRequest(mode="general", description=long_desc)
        title = _build_report_issue_title(request)
        assert title == f"[APIView] {'A' * 80}..."
        assert len(title) <= 94  # "[APIView] " (10) + 80 + "..." (3)

    def test_general_mode_newlines_replaced(self):
        request = ReportIssueRequest(mode="general", description="Line one\nLine two\nLine three")
        title = _build_report_issue_title(request)
        assert "\n" not in title
        assert title == "[APIView] Line one Line two Line three"

    def test_comment_mode_copilot_source(self):
        ctx = CommentContextRequest(comment_source="copilot")
        request = ReportIssueRequest(mode="comment", description="Bad suggestion", comment_context=ctx)
        title = _build_report_issue_title(request)
        assert title == "[APIView] Issue with AVC comment"

    def test_comment_mode_apiview_source(self):
        ctx = CommentContextRequest(comment_source="apiview")
        request = ReportIssueRequest(mode="comment", description="Wrong comment", comment_context=ctx)
        title = _build_report_issue_title(request)
        assert title == "[APIView] Issue with APIView comment"

    def test_comment_mode_no_source(self):
        ctx = CommentContextRequest()
        request = ReportIssueRequest(mode="comment", description="Issue", comment_context=ctx)
        title = _build_report_issue_title(request)
        assert title == "[APIView] Issue with APIView comment"

    def test_comment_mode_no_context(self):
        request = ReportIssueRequest(mode="comment", description="Issue")
        title = _build_report_issue_title(request)
        assert title == "[APIView] Issue with APIView comment"


class TestBuildReportIssueBody:

    def test_general_mode_body(self):
        request = ReportIssueRequest(mode="general", description="Something is broken")
        body = _build_report_issue_body(request)
        assert "## Description" in body
        assert "Something is broken" in body
        assert "Reported via APIView" in body

    def test_includes_review_link(self):
        request = ReportIssueRequest(
            mode="general",
            description="Bug",
            review_link="https://apiview.dev/review/123",
        )
        body = _build_report_issue_body(request)
        assert "## Review Link" in body
        assert "https://apiview.dev/review/123" in body

    def test_includes_comment_context(self):
        ctx = CommentContextRequest(
            comment_text="This API is wrong",
            code_snippet="public void Foo() {}",
            language="csharp",
            comment_source="copilot",
            element_id="Foo",
        )
        request = ReportIssueRequest(
            mode="comment",
            description="The suggestion is incorrect",
            comment_context=ctx,
        )
        body = _build_report_issue_body(request)
        assert "## Comment Context" in body
        assert "**Source:** copilot" in body
        assert "**Language:** csharp" in body
        assert "> This API is wrong" in body
        assert "public void Foo() {}" in body
        assert "**Element ID:** `Foo`" in body

    def test_minimal_comment_context(self):
        ctx = CommentContextRequest(comment_source="apiview")
        request = ReportIssueRequest(
            mode="comment",
            description="Problem",
            comment_context=ctx,
        )
        body = _build_report_issue_body(request)
        assert "## Comment Context" in body
        assert "**Source:** apiview" in body
        # Should not contain fields that are None
        assert "**Language:**" not in body
        assert "**Comment:**" not in body


class TestGetReportIssueLabels:

    def test_general_mode_labels(self):
        request = ReportIssueRequest(mode="general", description="Bug")
        labels = _get_report_issue_labels(request)
        assert labels == ["APIView"]

    def test_comment_mode_copilot_labels(self):
        ctx = CommentContextRequest(comment_source="copilot")
        request = ReportIssueRequest(mode="comment", description="Bad", comment_context=ctx)
        labels = _get_report_issue_labels(request)
        assert labels == ["APIView Copilot"]

    def test_comment_mode_apiview_labels(self):
        ctx = CommentContextRequest(comment_source="apiview")
        request = ReportIssueRequest(mode="comment", description="Bad", comment_context=ctx)
        labels = _get_report_issue_labels(request)
        assert labels == ["APIView"]

    def test_comment_mode_no_context_labels(self):
        request = ReportIssueRequest(mode="comment", description="Bad")
        labels = _get_report_issue_labels(request)
        assert labels == ["APIView"]


class TestBuildCommentContextText:

    def test_full_context(self):
        ctx = CommentContextRequest(
            comment_source="copilot",
            language="python",
            comment_text="Remove async",
            code_snippet="async def foo():",
            element_id="foo",
        )
        text = _build_comment_context_text(ctx)
        assert "Source: copilot" in text
        assert "Language: python" in text
        assert "Comment: Remove async" in text
        assert "Code Snippet: async def foo():" in text
        assert "Element ID: foo" in text

    def test_partial_context(self):
        ctx = CommentContextRequest(comment_source="apiview")
        text = _build_comment_context_text(ctx)
        assert "Source: apiview" in text
        assert "Language:" not in text
        assert "Comment:" not in text

    def test_empty_context(self):
        ctx = CommentContextRequest()
        text = _build_comment_context_text(ctx)
        assert text == ""


class TestGenerateReportIssue:

    @patch("app.run_prompt")
    def test_llm_success(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({
            "title": "[APIView] LLM generated title",
            "body": "## Description\n\nLLM generated body",
        })
        request = ReportIssueRequest(mode="general", description="Something broke")
        result = _generate_report_issue(request)
        assert result["title"] == "[APIView] LLM generated title"
        assert result["body"] == "## Description\n\nLLM generated body"
        mock_run_prompt.assert_called_once()

    @patch("app.run_prompt")
    def test_llm_with_comment_context(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({
            "title": "[APIView] Issue with AVC comment",
            "body": "## Description\n\nBad suggestion\n\n## Comment Context\n\ncopilot comment",
        })
        ctx = CommentContextRequest(comment_source="copilot", comment_text="Remove async")
        request = ReportIssueRequest(mode="comment", description="Bad suggestion", comment_context=ctx)
        result = _generate_report_issue(request)
        assert "AVC" in result["title"]
        # Verify the prompt received the comment context
        call_args = mock_run_prompt.call_args
        assert call_args[1]["inputs"]["comment_context"] != ""

    @patch("app.run_prompt")
    def test_fallback_on_llm_exception(self, mock_run_prompt):
        mock_run_prompt.side_effect = RuntimeError("LLM unavailable")
        request = ReportIssueRequest(mode="general", description="Something broke")
        result = _generate_report_issue(request)
        # Should fall back to template
        assert result["title"] == "[APIView] Something broke"
        assert "## Description" in result["body"]
        assert "Something broke" in result["body"]

    @patch("app.run_prompt")
    def test_fallback_on_invalid_json(self, mock_run_prompt):
        mock_run_prompt.return_value = "not valid json"
        request = ReportIssueRequest(mode="general", description="Bug report")
        result = _generate_report_issue(request)
        assert result["title"] == "[APIView] Bug report"
        assert "## Description" in result["body"]

    @patch("app.run_prompt")
    def test_fallback_on_empty_title(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({"title": "", "body": "some body"})
        request = ReportIssueRequest(mode="general", description="Missing title test")
        result = _generate_report_issue(request)
        # Empty title triggers fallback
        assert result["title"] == "[APIView] Missing title test"

    @patch("app.run_prompt")
    def test_fallback_on_empty_body(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({"title": "[APIView] Good title", "body": ""})
        request = ReportIssueRequest(mode="general", description="Missing body test")
        result = _generate_report_issue(request)
        # Empty body triggers fallback
        assert "## Description" in result["body"]
        assert "Missing body test" in result["body"]
