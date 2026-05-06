# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument,protected-access

"""
Tests for _generate_comments prompt scheduling in ApiViewReview.
"""

import json
import sys
from unittest.mock import MagicMock, patch

import pytest

# Mock azure dependencies before importing
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()
sys.modules["azure.ai.inference"] = MagicMock()
sys.modules["azure.ai.inference.models"] = MagicMock()

from src._apiview_reviewer import ApiViewReview


# A minimal API surface that produces exactly 1 section
SMALL_API = "namespace Foo {\\n  class Bar {\\n  }\\n}"

# A response with no comments
EMPTY_RESPONSE = json.dumps({"comments": []})


@pytest.fixture
def review(monkeypatch):
    """Create an ApiViewReview with all external dependencies mocked."""
    mock_search = MagicMock()
    mock_search.language_guidelines = MagicMock(results=[])
    mock_search.search_all.return_value = MagicMock(results=[])
    mock_search.build_context.return_value = MagicMock(to_markdown=lambda: "")

    mock_settings = MagicMock()

    with patch("src._apiview_reviewer.SearchManager", return_value=mock_search), patch(
        "src._apiview_reviewer.SettingsManager", return_value=mock_settings
    ):
        r = ApiViewReview(target=SMALL_API, base=None, language="python")
    # Replace the prompt runner with a mock that returns empty comments
    r.run_prompt = MagicMock(return_value=EMPTY_RESPONSE)
    return r


class TestGenerateCommentsPromptScheduling:
    """Verify that _generate_comments submits the correct prompt tasks."""

    def test_full_mode_submits_guideline_and_context_only(self, review):
        """With generic review disabled, only guideline and context prompts should run."""
        review._generate_comments()

        # Collect all prompt filenames that were submitted
        submitted_filenames = [call.kwargs["filename"] for call in review.run_prompt.call_args_list]

        assert "guidelines_review.prompty" in submitted_filenames
        assert "context_review.prompty" in submitted_filenames
        assert "generic_review.prompty" not in submitted_filenames

    def test_full_mode_submits_two_prompts_per_section(self, review):
        """Each section should produce exactly 2 prompt calls (guideline + context)."""
        review._generate_comments()

        # The small API fits in one section, so we expect exactly 2 calls
        assert review.run_prompt.call_count == 2

    def test_diff_mode_submits_guideline_and_context_only(self, monkeypatch):
        """Diff mode should also submit only guideline and context prompts."""
        mock_search = MagicMock()
        mock_search.language_guidelines = MagicMock(results=[])
        mock_search.search_all.return_value = MagicMock(results=[])
        mock_search.build_context.return_value = MagicMock(to_markdown=lambda: "")

        mock_settings = MagicMock()

        with patch("src._apiview_reviewer.SearchManager", return_value=mock_search), patch(
            "src._apiview_reviewer.SettingsManager", return_value=mock_settings
        ):
            r = ApiViewReview(
                target="namespace Foo {\\n  class Bar {\\n    void baz();\\n  }\\n}",
                base="namespace Foo {\\n  class Bar {\\n  }\\n}",
                language="java",
            )
        r.run_prompt = MagicMock(return_value=EMPTY_RESPONSE)

        r._generate_comments()

        submitted_filenames = [call.kwargs["filename"] for call in r.run_prompt.call_args_list]

        assert "guidelines_diff_review.prompty" in submitted_filenames
        assert "context_diff_review.prompty" in submitted_filenames
        assert "generic_diff_review.prompty" not in submitted_filenames
        # Exactly 2 prompts per section
        assert r.run_prompt.call_count == 2

    def test_multiple_sections_submit_two_prompts_each(self, monkeypatch):
        """Multiple sections should each get exactly 2 prompts."""
        mock_search = MagicMock()
        mock_search.language_guidelines = MagicMock(results=[])
        mock_search.search_all.return_value = MagicMock(results=[])
        mock_search.build_context.return_value = MagicMock(to_markdown=lambda: "")

        mock_settings = MagicMock()

        # Generate an API surface large enough to produce multiple sections (>500 lines)
        lines = ["namespace Foo {"]
        for i in range(600):
            lines.append(f"  void method_{i}();")
        lines.append("}")
        large_api = "\\n".join(lines)

        with patch("src._apiview_reviewer.SearchManager", return_value=mock_search), patch(
            "src._apiview_reviewer.SettingsManager", return_value=mock_settings
        ):
            r = ApiViewReview(target=large_api, base=None, language="python")
        r.run_prompt = MagicMock(return_value=EMPTY_RESPONSE)

        r._generate_comments()

        # Should have submitted 2 prompts for each section
        assert r._chunk_count > 1
        assert r.run_prompt.call_count == r._chunk_count * 2
