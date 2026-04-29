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
        _build_comment_context_text,
        _generate_report_issue,
        _get_report_issue_labels,
        _assemble_report_issue_body,
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
        assert title == "[APIView] Issue with APIView Copilot comment"

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
    def test_llm_success_with_additional_context(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({
            "title": "[APIView] Diff view freezes on large reviews",
            "body": "This is likely related to DOM rendering of large diff trees. APIView renders all API definitions in a single page without virtualization.",
        })
        request = ReportIssueRequest(mode="general", description="When I open the diff view for large reviews it freezes.")
        result = _generate_report_issue(request)
        assert result["title"] == "[APIView] Diff view freezes on large reviews"
        # Body should contain user's description and LLM's additional context
        assert "## Description" in result["body"]
        assert "When I open the diff view" in result["body"]
        assert "## Suggested Next Steps" in result["body"]
        assert "DOM rendering" in result["body"]
        mock_run_prompt.assert_called_once()

    @patch("app.run_prompt")
    def test_llm_empty_additional_context(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({
            "title": "[APIView] Something broke",
            "body": "",
        })
        request = ReportIssueRequest(mode="general", description="Something broke")
        result = _generate_report_issue(request)
        assert result["title"] == "[APIView] Something broke"
        # No additional context section when LLM has nothing to add
        assert "## Description" in result["body"]
        assert "## Suggested Next Steps" not in result["body"]

    @patch("app.run_prompt")
    def test_llm_with_comment_context(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({
            "title": "[APIView] APIView Copilot incorrectly suggests removing async",
            "body": "Per Azure SDK guidelines, methods performing network I/O must be async to avoid blocking the event loop.",
        })
        ctx = CommentContextRequest(comment_source="copilot", comment_text="Remove async")
        request = ReportIssueRequest(mode="comment", description="Bad suggestion", comment_context=ctx)
        result = _generate_report_issue(request)
        assert "APIView Copilot" in result["title"]
        assert "## Comment Context" in result["body"]
        assert "> Remove async" in result["body"]
        assert "## Suggested Next Steps" in result["body"]
        assert "Azure SDK guidelines" in result["body"]
        # Verify the prompt received the comment context
        call_args = mock_run_prompt.call_args
        assert call_args[1]["inputs"]["comment_context"] != ""

    @patch("app.run_prompt")
    def test_fallback_on_llm_exception(self, mock_run_prompt):
        mock_run_prompt.side_effect = RuntimeError("LLM unavailable")
        request = ReportIssueRequest(mode="general", description="Something broke")
        result = _generate_report_issue(request)
        assert result["title"] == "[APIView] Something broke"
        assert "## Description" in result["body"]
        assert "Something broke" in result["body"]
        assert "## Suggested Next Steps" not in result["body"]

    @patch("app.run_prompt")
    def test_fallback_on_invalid_json(self, mock_run_prompt):
        mock_run_prompt.return_value = "not valid json"
        request = ReportIssueRequest(mode="general", description="Bug report")
        result = _generate_report_issue(request)
        assert result["title"] == "[APIView] Bug report"
        assert "## Description" in result["body"]

    @patch("app.run_prompt")
    def test_fallback_on_empty_title(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({"title": "", "body": "Some context here."})
        request = ReportIssueRequest(mode="general", description="Missing title test")
        result = _generate_report_issue(request)
        # Empty title triggers fallback to template title
        assert result["title"] == "[APIView] Missing title test"
        # But additional context is still included
        assert "## Suggested Next Steps" in result["body"]
        assert "Some context here." in result["body"]


class TestAssembleReportIssueBody:

    def test_with_additional_context(self):
        request = ReportIssueRequest(mode="general", description="The page is slow.")
        body = _assemble_report_issue_body(request, next_steps="Likely a rendering bottleneck.")
        assert "## Description" in body
        assert "The page is slow." in body
        assert "## Suggested Next Steps" in body
        assert "Likely a rendering bottleneck." in body

    def test_without_additional_context(self):
        request = ReportIssueRequest(mode="general", description="A bug")
        body = _assemble_report_issue_body(request, next_steps=None)
        assert "## Description" in body
        assert "A bug" in body
        assert "## Suggested Next Steps" not in body

    def test_empty_string_additional_context(self):
        request = ReportIssueRequest(mode="general", description="Issue")
        body = _assemble_report_issue_body(request, next_steps="")
        assert "## Suggested Next Steps" not in body

    def test_includes_comment_context_and_review_link(self):
        ctx = CommentContextRequest(comment_source="copilot", comment_text="Bad advice")
        request = ReportIssueRequest(
            mode="comment", description="Wrong", comment_context=ctx,
            review_link="https://apiview.dev/review/456",
        )
        body = _assemble_report_issue_body(request, next_steps="The suggestion contradicts Azure SDK guidelines.")
        assert "## Description" in body
        assert "## Comment Context" in body
        assert "> Bad advice" in body
        assert "## Suggested Next Steps" in body
        assert "## Review Link" in body
        assert "https://apiview.dev/review/456" in body
        assert "Reported via APIView" in body
