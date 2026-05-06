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

from src._github_manager import GithubManager, LanguageLabel
from src._report_issue import (
    _build_fallback_body,
    _build_fallback_title_snippet,
    _build_labels,
    _build_review_link,
    _format_comment_context_for_prompt,
    _generate_issue_content,
    _lookup_comment_context,
    _title_prefix,
    handle_report_issue_request,
)


class TestTitlePrefix:
    def test_apiview(self):
        assert _title_prefix("apiview", None) == "[APIView]"

    def test_apiview_ignores_language(self):
        assert _title_prefix("apiview", "python") == "[APIView]"

    def test_parser_known_language(self):
        assert _title_prefix("parser", "python") == "[Python APIView]"
        assert _title_prefix("parser", "C#") == "[.NET APIView]"
        assert _title_prefix("parser", "go") == "[Go APIView]"

    def test_parser_unknown_language_falls_back_to_apiview(self):
        assert _title_prefix("parser", "kotlin") == "[APIView]"

    def test_parser_missing_language_falls_back_to_apiview(self):
        assert _title_prefix("parser", None) == "[APIView]"

    def test_avc(self):
        assert _title_prefix("avc", None) == "[AVC]"

    def test_avc_ignores_language(self):
        assert _title_prefix("avc", "python") == "[AVC]"


class TestBuildLabels:
    def test_apiview(self):
        assert _build_labels("apiview", None) == ["APIView"]

    def test_apiview_ignores_language(self):
        assert _build_labels("apiview", "python") == ["APIView"]

    def test_parser_with_known_language(self):
        assert _build_labels("parser", "python") == ["APIView", "Python"]

    def test_parser_with_unknown_language(self):
        assert _build_labels("parser", "kotlin") == ["APIView"]

    def test_parser_without_language(self):
        assert _build_labels("parser", None) == ["APIView"]

    def test_avc(self):
        assert _build_labels("avc", None) == ["APIView Copilot"]

    def test_avc_ignores_language(self):
        assert _build_labels("avc", "python") == ["APIView Copilot"]


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


class TestBuildFallbackTitleSnippet:
    def test_short(self):
        assert _build_fallback_title_snippet("Page is broken") == "Page is broken"

    def test_long_truncates_to_first_14_words(self):
        desc = " ".join(["word"] * 50)
        title = _build_fallback_title_snippet(desc)
        assert len(title.split()) == 14
        assert "..." not in title

    def test_only_uses_first_line(self):
        title = _build_fallback_title_snippet("first line\nsecond line should be ignored")
        assert title == "first line"

    def test_empty_description_uses_default(self):
        assert _build_fallback_title_snippet("   ") == "Issue reported from APIView"


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
    def test_llm_apiview_success(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps(
            {
                "category": "apiview",
                "language": None,
                "title": "Tree fails to expand for deep namespaces",
                "body": "## Summary\n\nx\n\n---\n*Reported via APIView*",
            }
        )
        result = _generate_issue_content(
            description="tree wont expand",
            review_link=None,
            language=None,
            comment_context=None,
        )
        assert result["category"] == "apiview"
        assert result["language"] is None
        assert result["title"].startswith("[APIView] ")
        assert "Tree fails to expand" in result["title"]

    @patch("src._report_issue.run_prompt")
    def test_llm_parser_success_uses_language_prefix(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps(
            {
                "category": "parser",
                "language": "python",
                "title": "Wrong tokens for overloaded methods",
                "body": "## Summary\n\nx",
            }
        )
        result = _generate_issue_content(
            description="parser is broken",
            review_link=None,
            language=None,
            comment_context=None,
        )
        assert result["category"] == "parser"
        assert result["language"] == "python"
        assert result["title"] == "[Python APIView] Wrong tokens for overloaded methods"

    @patch("src._report_issue.run_prompt")
    def test_llm_emits_prefix_in_title_is_not_double_prepended(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps(
            {
                "category": "apiview",
                "language": None,
                "title": "[APIView] Tree fails to expand",
                "body": "x",
            }
        )
        result = _generate_issue_content(
            description="x", review_link=None, language=None, comment_context=None
        )
        assert result["title"] == "[APIView] Tree fails to expand"

    @patch("src._report_issue.run_prompt")
    def test_unknown_category_defaults_to_apiview(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps(
            {"category": "infra", "language": None, "title": "x", "body": "y"}
        )
        result = _generate_issue_content(
            description="x", review_link=None, language=None, comment_context=None
        )
        assert result["category"] == "apiview"
        assert result["title"] == "[APIView] x"

    @patch("src._report_issue.run_prompt")
    def test_avc_category_uses_avc_prefix(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps(
            {"category": "avc", "language": None, "title": "Bad suggestion", "body": "details"}
        )
        result = _generate_issue_content(
            description="x", review_link=None, language=None, comment_context=None
        )
        assert result["category"] == "avc"
        assert result["language"] is None
        assert result["title"] == "[AVC] Bad suggestion"

    @patch("src._report_issue.run_prompt")
    def test_falls_back_when_llm_raises(self, mock_run_prompt):
        mock_run_prompt.side_effect = RuntimeError("LLM unavailable")
        result = _generate_issue_content(
            description="thing broke",
            review_link="https://apiview.dev/r/1",
            language=None,
            comment_context=None,
        )
        assert result["category"] == "apiview"
        assert result["title"] == "[APIView] thing broke"
        assert "## Description" in result["body"]
        assert "thing broke" in result["body"]

    @patch("src._report_issue.run_prompt")
    def test_falls_back_on_invalid_json(self, mock_run_prompt):
        mock_run_prompt.return_value = "not json"
        result = _generate_issue_content(
            description="bad thing", review_link=None, language=None, comment_context=None
        )
        assert result["category"] == "apiview"
        assert result["title"] == "[APIView] bad thing"

    @patch("src._report_issue.run_prompt")
    def test_apiview_category_strips_language_from_llm(self, mock_run_prompt):
        mock_run_prompt.return_value = json.dumps(
            {"category": "apiview", "language": "python", "title": "Issue", "body": "body"}
        )
        result = _generate_issue_content(
            description="x", review_link=None, language=None, comment_context=None
        )
        assert result["language"] is None
        assert result["title"] == "[APIView] Issue"

    @patch("src._report_issue.run_prompt")
    def test_parser_with_empty_title_uses_language_aware_prefix(self, mock_run_prompt):
        # Regression: when the LLM emits a parser category + language but
        # an empty title, the fallback title must still get the
        # ``[Java APIView]`` prefix, not the generic ``[APIView]``.
        mock_run_prompt.return_value = json.dumps(
            {"category": "parser", "language": "java", "title": "", "body": "real body"}
        )
        result = _generate_issue_content(
            description="parser swallows generics", review_link=None, language=None, comment_context=None
        )
        assert result["category"] == "parser"
        assert result["language"] == "java"
        assert result["title"].startswith("[Java APIView] ")
        assert "parser swallows generics" in result["title"]


class TestLookupCommentContext:
    @patch("src._report_issue.get_comment_with_context")
    def test_maps_db_payload_to_comment_context(self, mock_get):
        mock_get.return_value = {
            "comment": {
                "CommentText": "remove async",
                "CommentSource": "copilot",
                "ElementId": "AsyncBlobClient.upload_blob",
                "ReviewId": "r-1",
                "APIRevisionId": "rev-2",
            },
            "code": "async def upload_blob(self, name: str, data: bytes) -> None: ...",
            "language": "Python",
            "package_name": "azure-storage-blob",
        }
        result = _lookup_comment_context("comment-123")
        assert result == {
            "comment_text": "remove async",
            "comment_source": "copilot",
            "code_snippet": "async def upload_blob(self, name: str, data: bytes) -> None: ...",
            "language": "Python",
            "element_id": "AsyncBlobClient.upload_blob",
            "review_id": "r-1",
            "revision_id": "rev-2",
        }

    @patch("src._report_issue.os.getenv")
    @patch("src._report_issue.get_comment_with_context")
    def test_passes_environment_from_env_var(self, mock_get, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: {"ENVIRONMENT_NAME": "staging"}.get(key, default)
        mock_get.return_value = {"comment": {}, "code": None, "language": None}
        _lookup_comment_context("abc")
        assert mock_get.call_args.kwargs["environment"] == "staging"

    @patch("src._report_issue.os.getenv")
    @patch("src._report_issue.get_comment_with_context")
    def test_environment_defaults_to_production(self, mock_get, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: default
        mock_get.return_value = {"comment": {}, "code": None, "language": None}
        _lookup_comment_context("abc")
        assert mock_get.call_args.kwargs["environment"] == "production"

    @patch("src._report_issue.get_comment_with_context")
    def test_returns_none_when_comment_not_found(self, mock_get):
        mock_get.return_value = None
        assert _lookup_comment_context("missing") is None

    @patch("src._report_issue.get_comment_with_context")
    def test_returns_none_on_exception(self, mock_get):
        mock_get.side_effect = RuntimeError("db down")
        assert _lookup_comment_context("x") is None


class TestHandleReportIssueRequestValidation:
    def test_empty_description(self):
        with pytest.raises(ValueError, match="non-empty"):
            handle_report_issue_request(description="   ")


class TestHandleReportIssueRequestEndToEnd:
    @patch("src._report_issue.GithubManager.get_instance")
    @patch("src._report_issue.create_issue")
    @patch("src._report_issue.run_prompt")
    def test_apiview_issue(self, mock_run_prompt, mock_create_issue, _mock_get_instance):
        mock_run_prompt.return_value = json.dumps(
            {
                "category": "apiview",
                "language": None,
                "title": "Tree fails to expand",
                "body": "## Summary\n\nx\n---\n*Reported via APIView*",
            }
        )
        mock_create_issue.return_value = {"html_url": "https://github.com/foo/bar/issues/1", "number": 1}

        result = handle_report_issue_request(
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
        assert kwargs["title"] == "[APIView] Tree fails to expand"

    @patch("src._report_issue.GithubManager.get_instance")
    @patch("src._report_issue.create_issue")
    @patch("src._report_issue.run_prompt")
    def test_parser_issue_uses_language_label(self, mock_run_prompt, mock_create_issue, _mock_get_instance):
        mock_run_prompt.return_value = json.dumps(
            {"category": "parser", "language": "python", "title": "Wrong tokens", "body": "body"}
        )
        mock_create_issue.return_value = {"html_url": "u", "number": 3}

        handle_report_issue_request(
            description="parser is broken here",
            language="python",
        )

        kwargs = mock_create_issue.call_args.kwargs
        assert kwargs["title"] == "[Python APIView] Wrong tokens"
        assert kwargs["labels"] == ["APIView", "Python"]

    @patch("src._report_issue.GithubManager.get_instance")
    @patch("src._report_issue.create_issue")
    @patch("src._report_issue.run_prompt")
    @patch("src._report_issue.get_comment_with_context")
    def test_comment_id_path_fetches_context(
        self, mock_get, mock_run_prompt, mock_create_issue, _mock_get_instance
    ):
        mock_get.return_value = {
            "comment": {
                "CommentText": "this is wrong",
                "CommentSource": "apiview",
                "ElementId": "BlobClient.upload_blob",
                "ReviewId": "rev-abc",
                "APIRevisionId": "rev-def",
            },
            "code": "def upload_blob(...): ...",
            "language": "Python",
        }
        mock_run_prompt.return_value = json.dumps(
            {"category": "parser", "language": "Python", "title": "Wrong tokens", "body": "b"}
        )
        mock_create_issue.return_value = {"html_url": "u", "number": 5}

        handle_report_issue_request(
            description="parser broken",
            comment_id="comment-xyz",
        )

        prompt_inputs = mock_run_prompt.call_args.kwargs["inputs"]
        assert "this is wrong" in prompt_inputs["comment_context"]
        assert "BlobClient.upload_blob" in prompt_inputs["comment_context"]
        assert prompt_inputs["language"] == "Python"
        # review link auto-built from comment context when caller didn't supply one
        assert "rev-abc" in prompt_inputs["review_link"]
        assert "rev-def" in prompt_inputs["review_link"]
        kwargs = mock_create_issue.call_args.kwargs
        assert kwargs["title"] == "[Python APIView] Wrong tokens"
        assert kwargs["labels"] == ["APIView", "Python"]

    @patch("src._report_issue.GithubManager.get_instance")
    @patch("src._report_issue.create_issue")
    @patch("src._report_issue.run_prompt")
    @patch("src._report_issue.get_comment_with_context")
    def test_explicit_review_link_overrides_auto_built(
        self, mock_get, mock_run_prompt, mock_create_issue, _mock_get_instance
    ):
        mock_get.return_value = {
            "comment": {
                "CommentText": "x",
                "ReviewId": "db-review",
                "APIRevisionId": "db-rev",
            },
            "code": None,
            "language": "Python",
        }
        mock_run_prompt.return_value = json.dumps(
            {"category": "avc", "language": None, "title": "t", "body": "b"}
        )
        mock_create_issue.return_value = {"html_url": "u", "number": 6}

        handle_report_issue_request(
            description="d",
            comment_id="c",
            review_link="https://example.invalid/explicit",
        )

        prompt_inputs = mock_run_prompt.call_args.kwargs["inputs"]
        assert prompt_inputs["review_link"] == "https://example.invalid/explicit"


class TestBuildReviewLink:
    def test_returns_none_without_review_id(self):
        assert _build_review_link(None, None) is None
        assert _build_review_link(None, "rev") is None

    @patch("src._report_issue.os.getenv")
    def test_production_host(self, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: {"ENVIRONMENT_NAME": "production"}.get(key, default)
        assert _build_review_link("r1", "rev1") == "https://spa.apiview.dev/review/r1?activeApiRevisionId=rev1"

    @patch("src._report_issue.os.getenv")
    def test_staging_host(self, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: {"ENVIRONMENT_NAME": "staging"}.get(key, default)
        assert (
            _build_review_link("r1", "rev1")
            == "https://spa.apiviewstagingtest.com/review/r1?activeApiRevisionId=rev1"
        )

    @patch("src._report_issue.os.getenv")
    def test_omits_revision_when_missing(self, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: {"ENVIRONMENT_NAME": "production"}.get(key, default)
        assert _build_review_link("r1", None) == "https://spa.apiview.dev/review/r1"


class TestGithubManagerOwner:
    @patch("src._github_manager.os.getenv")
    def test_production(self, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: {"ENVIRONMENT_NAME": "production"}.get(key, default)
        assert GithubManager.resolve_owner() == "Azure"

    @patch("src._github_manager.os.getenv")
    def test_staging(self, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: {"ENVIRONMENT_NAME": "staging"}.get(key, default)
        assert GithubManager.resolve_owner() == "tjprescott"

    @patch("src._github_manager.os.getenv")
    def test_unset(self, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: default
        assert GithubManager.resolve_owner() == "tjprescott"

    @patch("src._github_manager.os.getenv")
    def test_staging_owner_env_override(self, mock_getenv):
        mock_getenv.side_effect = lambda key, default=None: {
            "ENVIRONMENT_NAME": "staging",
            "APIVIEW_STAGING_REPO_OWNER": "my-fork",
        }.get(key, default)
        assert GithubManager.resolve_owner() == "my-fork"


class TestGithubManagerLanguageLabels:
    def test_canonical_languages_present(self):
        assert GithubManager.LANGUAGE_LABELS["python"] is LanguageLabel.PYTHON
        assert GithubManager.LANGUAGE_LABELS["c#"] is LanguageLabel.DOTNET
        assert GithubManager.LANGUAGE_LABELS["go"] is LanguageLabel.GO
        assert GithubManager.LANGUAGE_LABELS["java"] is LanguageLabel.JAVA

    def test_language_label_returns_enum(self):
        label = GithubManager.language_label("python")
        assert label is LanguageLabel.PYTHON
        assert label.value == "Python"

    def test_language_label_unknown_returns_none(self):
        assert GithubManager.language_label("kotlin") is None
        assert GithubManager.language_label(None) is None

    def test_build_issue_labels_appends_known_language(self):
        # build_issue_labels returns plain strings (the canonical label values),
        # not enum members, so the result is API-ready for github.
        assert GithubManager.build_issue_labels(["APIView"], "python") == ["APIView", "Python"]

    def test_build_issue_labels_skips_unknown_language(self):
        assert GithubManager.build_issue_labels(["APIView"], "kotlin") == ["APIView"]

    def test_build_issue_labels_no_language(self):
        assert GithubManager.build_issue_labels(["APIView"], None) == ["APIView"]

    def test_build_issue_labels_no_duplicate(self):
        assert GithubManager.build_issue_labels(["APIView", "Python"], "python") == ["APIView", "Python"]
