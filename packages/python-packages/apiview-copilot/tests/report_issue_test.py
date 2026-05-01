# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""Tests for the shared report-issue core in src/_report_issue.py."""

import json
from unittest.mock import patch

import pytest

from src._report_issue import (
    LANGUAGE_LABELS,
    _build_fallback_body,
    _build_fallback_title,
    _build_labels,
    _format_comment_context_for_prompt,
    _generate_issue_content,
    _title_prefix,
    get_owner,
    handle_report_issue_request,
)


class TestTitlePrefix:
    def test_apiview(self):
        assert _title_prefix("apiview", None) == "[APIView]"

    def test_copilot(self):
        assert _title_prefix("copilot", "python") == "[AVC]"

    def test_parser_known_language(self):
        assert _title_prefix("parser", "python") == "[Python APIView]"
        assert _title_prefix("parser", "C#") == "[.NET APIView]"
        assert _title_prefix("parser", "go") == "[Go APIView]"

    def test_parser_unknown_language_uses_raw(self):
        assert _title_prefix("parser", "kotlin") == "[kotlin APIView]"

    def test_parser_missing_language_falls_back(self):
        assert _title_prefix("parser", None) == "[Unknown APIView]"


class TestBuildLabels:
    def test_apiview(self):
        assert _build_labels("apiview", None) == ["APIView"]

    def test_copilot(self):
        assert _build_labels("copilot", None) == ["APIView Copilot"]

    def test_parser_with_known_language(self):
        assert _build_labels("parser", "python") == ["APIView", "Python"]

    def test_parser_with_unknown_language(self):
        assert _build_labels("parser", "kotlin") == ["APIView"]

    def test_parser_without_language(self):
        assert _build_labels("parser", None) == ["APIView"]


class TestFormatCommentContextForPrompt:
    def test_full(self):
        ctx = {
            "comment_source": "copilot",
            "language": "python",
            "comment_text": "Remove async",
            "code_snippet": "async def foo():",
            "element_id": "foo",
        }
        text = _format_comment_context_for_prompt(ctx)
        assert "Source: copilot" in text
        assert "Language: python" in text
        assert "Comment: Remove async" in text
        assert "Code Snippet: async def foo():" in text
        assert "Element ID: foo" in text

    def test_partial(self):
        text = _format_comment_context_for_prompt({"comment_source": "apiview"})
        assert text == "Source: apiview"

    def test_empty_dict(self):
        assert _format_comment_context_for_prompt({}) == ""

    def test_none(self):
        assert _format_comment_context_for_prompt(None) == ""


class TestBuildFallbackTitle:
    def test_short_apiview(self):
        assert _build_fallback_title("apiview", "Page is broken", None) == "[APIView] Page is broken"

    def test_long_truncates(self):
        long = "X" * 200
        title = _build_fallback_title("apiview", long, None)
        assert title.startswith("[APIView] ")
        assert title.endswith("...")
        assert len(title) - len("[APIView] ") - len("...") == 80

    def test_newlines_replaced(self):
        title = _build_fallback_title("apiview", "line one\nline two", None)
        assert "\n" not in title

    def test_copilot_prefix(self):
        assert _build_fallback_title("copilot", "Bad suggestion", None).startswith("[AVC] ")

    def test_parser_prefix(self):
        assert _build_fallback_title("parser", "Wrong tokens", "python").startswith("[Python APIView] ")


class TestBuildFallbackBody:
    def test_minimal(self):
        body = _build_fallback_body("Something is broken", None, None)
        assert "## Description" in body
        assert "Something is broken" in body
        assert "Reported via APIView" in body

    def test_with_review_link(self):
        body = _build_fallback_body("desc", "https://apiview.dev/Review/x", None)
        assert "## Review Link" in body
        assert "https://apiview.dev/Review/x" in body

    def test_with_comment_context(self):
        body = _build_fallback_body("desc", None, {"comment_text": "the parser is broken here"})
        assert "## Comment Context" in body
        assert "the parser is broken here" in body


class TestGenerateIssueContent:
    @patch("src._report_issue.run_prompt")
    def test_llm_success(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps(
            {
                "title": "[APIView] Java review tree expansion broken beyond 3 levels",
                "body": "## Summary\n\nNavigation tree fails to expand.\n\n---\n*Reported via APIView*",
            }
        )
        result = _generate_issue_content(
            category="apiview",
            description="tree wont expand for deep namespaces",
            review_link=None,
            language=None,
            comment_context=None,
        )
        assert result["title"].startswith("[APIView] ")
        assert "## Summary" in result["body"]

    @patch("src._report_issue.run_prompt")
    def test_llm_passes_title_prefix(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({"title": "[Python APIView] Wrong tokens", "body": "x"})
        _generate_issue_content(
            category="parser",
            description="wrong tokens",
            review_link=None,
            language="python",
            comment_context=None,
        )
        inputs = mock_run_prompt.call_args[1]["inputs"]
        assert inputs["category"] == "parser"
        assert inputs["title_prefix"] == "[Python APIView]"
        assert inputs["language"] == "python"

    @patch("src._report_issue.run_prompt")
    def test_falls_back_when_llm_raises(self, mock_run_prompt):
        mock_run_prompt.side_effect = RuntimeError("LLM unavailable")
        result = _generate_issue_content(
            category="apiview",
            description="thing broke",
            review_link="https://apiview.dev/r/1",
            language=None,
            comment_context=None,
        )
        assert result["title"] == "[APIView] thing broke"
        assert "## Description" in result["body"]
        assert "thing broke" in result["body"]

    @patch("src._report_issue.run_prompt")
    def test_falls_back_on_invalid_json(self, mock_run_prompt):
        mock_run_prompt.return_value = "not json"
        result = _generate_issue_content(
            category="copilot",
            description="bad suggestion",
            review_link=None,
            language=None,
            comment_context=None,
        )
        assert result["title"] == "[AVC] bad suggestion"

    @patch("src._report_issue.run_prompt")
    def test_partial_fallback_keeps_llm_body_when_only_title_missing(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps({"title": "", "body": "## Summary\n\nGood body content."})
        result = _generate_issue_content(
            category="apiview",
            description="something",
            review_link=None,
            language=None,
            comment_context=None,
        )
        assert result["title"] == "[APIView] something"
        assert "Good body content" in result["body"]


class TestHandleReportIssueRequestValidation:
    def test_invalid_category(self):
        with pytest.raises(ValueError, match="Invalid category"):
            handle_report_issue_request(category="banana", description="x")

    def test_empty_description(self):
        with pytest.raises(ValueError, match="non-empty"):
            handle_report_issue_request(category="apiview", description="   ")

    def test_parser_requires_language(self):
        with pytest.raises(ValueError, match="language is required"):
            handle_report_issue_request(category="parser", description="parser broken")

    def test_description_too_long(self):
        with pytest.raises(ValueError, match="at most 5000"):
            handle_report_issue_request(category="apiview", description="x" * 5001)

    def test_comment_text_too_long(self):
        with pytest.raises(ValueError, match=r"comment_text must be at most 10000"):
            handle_report_issue_request(
                category="apiview",
                description="ok",
                comment_context={"comment_text": "x" * 10001},
            )

    def test_code_snippet_too_long(self):
        with pytest.raises(ValueError, match=r"code_snippet must be at most 10000"):
            handle_report_issue_request(
                category="apiview",
                description="ok",
                comment_context={"code_snippet": "x" * 10001},
            )


class TestHandleReportIssueRequestEndToEnd:
    @patch("src._report_issue.GithubManager")
    @patch("src._report_issue.create_issue")
    @patch("src._report_issue.run_prompt")
    def test_apiview_issue(self, mock_run_prompt, mock_create_issue, _mock_github_manager):
        mock_run_prompt.return_value = json.dumps(
            {
                "title": "[APIView] Tree fails to expand",
                "body": "## Summary\n\nx\n---\n*Reported via APIView*",
            }
        )
        mock_create_issue.return_value = {"html_url": "https://github.com/foo/bar/issues/1", "number": 1}

        result = handle_report_issue_request(
            category="apiview",
            description="tree wont expand",
            review_link="https://apiview.dev/Review/x",
        )

        assert result["issue_url"] == "https://github.com/foo/bar/issues/1"
        assert result["issue_number"] == 1
        kwargs = mock_create_issue.call_args.kwargs
        assert kwargs["labels"] == ["APIView"]
        assert kwargs["workflow_tag"] == "report-issue"
        assert kwargs["source_tag"] == "APIView"
        assert kwargs["repo"] == "azure-sdk-tools"

    @patch("src._report_issue.GithubManager")
    @patch("src._report_issue.create_issue")
    @patch("src._report_issue.run_prompt")
    def test_copilot_issue_uses_avc_prefix_and_label(self, mock_run_prompt, mock_create_issue, _mock_github_manager):
        mock_run_prompt.return_value = json.dumps({"title": "[AVC] Suggestion is wrong", "body": "body"})
        mock_create_issue.return_value = {"html_url": "u", "number": 2}

        handle_report_issue_request(
            category="copilot",
            description="suggestion is wrong",
            comment_context={"comment_source": "copilot", "comment_text": "remove async"},
        )

        kwargs = mock_create_issue.call_args.kwargs
        assert kwargs["title"] == "[AVC] Suggestion is wrong"
        assert kwargs["labels"] == ["APIView Copilot"]

    @patch("src._report_issue.GithubManager")
    @patch("src._report_issue.create_issue")
    @patch("src._report_issue.run_prompt")
    def test_parser_issue_uses_language_label(self, mock_run_prompt, mock_create_issue, _mock_github_manager):
        mock_run_prompt.return_value = json.dumps({"title": "[Python APIView] Wrong tokens", "body": "body"})
        mock_create_issue.return_value = {"html_url": "u", "number": 3}

        handle_report_issue_request(
            category="parser",
            description="parser is broken here",
            language="python",
        )

        kwargs = mock_create_issue.call_args.kwargs
        assert kwargs["title"] == "[Python APIView] Wrong tokens"
        assert kwargs["labels"] == ["APIView", "Python"]


class TestGetOwner:
    @patch.dict("os.environ", {"ENVIRONMENT_NAME": "production"})
    def test_production(self):
        assert get_owner() == "Azure"

    @patch.dict("os.environ", {"ENVIRONMENT_NAME": "staging"})
    def test_staging(self):
        assert get_owner() == "tjprescott"

    @patch.dict("os.environ", {}, clear=True)
    def test_unset(self):
        assert get_owner() == "tjprescott"


class TestLanguageLabels:
    def test_canonical_languages_present(self):
        # Must mirror OpenParserIssueWorkflow.LANGUAGE_LABELS so issues get the correct language label
        assert LANGUAGE_LABELS["python"] == "Python"
        assert LANGUAGE_LABELS["c#"] == ".NET"
        assert LANGUAGE_LABELS["go"] == "Go"
        assert LANGUAGE_LABELS["java"] == "Java"
