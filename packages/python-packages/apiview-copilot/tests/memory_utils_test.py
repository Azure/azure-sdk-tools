# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument

"""
Tests for memory deduplication utilities in src/_memory_utils.py
"""

import json
from unittest.mock import MagicMock, patch

from src._memory_utils import SIMILARITY_THRESHOLD, check_for_duplicate_memory, merge_and_save_memory


def _make_search_result(memory_id="mem-existing", title="Existing title", content="Existing content", score=3.5):
    """Build a mock SearchItem-like object."""
    result = MagicMock()
    result.id = memory_id
    result.title = title
    result.content = content
    result.reranker_score = score
    return result


def _make_raw_memory(**overrides):
    """Build a minimal raw_memory dict."""
    base = {
        "id": "mem-new",
        "title": "New memory title",
        "content": "New memory content",
        "source": "mention_agent",
        "service": None,
        "is_exception": False,
    }
    base.update(overrides)
    return base


class TestCheckForDuplicateMemory:

    @patch("src._memory_utils.SearchManager")
    def test_no_matches_returns_none(self, mock_search_cls):
        """When search returns no results, should return None."""
        mock_instance = MagicMock()
        mock_instance.search_memories.return_value = []
        mock_search_cls.return_value = mock_instance

        result = check_for_duplicate_memory(raw_memory=_make_raw_memory(), language="python")
        assert result is None

    @patch("src._memory_utils.SearchManager")
    def test_below_threshold_returns_none(self, mock_search_cls):
        """When all results are below the similarity threshold, should return None."""
        low_score = _make_search_result(score=SIMILARITY_THRESHOLD - 0.5)
        mock_instance = MagicMock()
        mock_instance.search_memories.return_value = [low_score]
        mock_search_cls.return_value = mock_instance

        result = check_for_duplicate_memory(raw_memory=_make_raw_memory(), language="python")
        assert result is None

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.SearchManager")
    def test_above_threshold_returns_merge_result(self, mock_search_cls, mock_run_prompt):
        """When a result exceeds the threshold, should call merge prompt and return merge dict."""
        high_score = _make_search_result(score=SIMILARITY_THRESHOLD + 0.3)
        mock_instance = MagicMock()
        mock_instance.search_memories.return_value = [high_score]
        mock_search_cls.return_value = mock_instance

        mock_run_prompt.return_value = json.dumps({"title": "Merged title", "content": "Merged content"})

        result = check_for_duplicate_memory(raw_memory=_make_raw_memory(), language="python")
        assert result is not None
        assert result["existing_memory_id"] == "mem-existing"
        assert result["merged_title"] == "Merged title"
        assert result["merged_content"] == "Merged content"

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.SearchManager")
    def test_picks_highest_scoring_match(self, mock_search_cls, mock_run_prompt):
        """When multiple results exceed the threshold, should pick the highest-scoring one."""
        match_a = _make_search_result(memory_id="mem-a", score=SIMILARITY_THRESHOLD + 0.1)
        match_b = _make_search_result(memory_id="mem-b", score=SIMILARITY_THRESHOLD + 0.5)
        mock_instance = MagicMock()
        mock_instance.search_memories.return_value = [match_a, match_b]
        mock_search_cls.return_value = mock_instance

        mock_run_prompt.return_value = json.dumps({"title": "Merged", "content": "Merged"})

        result = check_for_duplicate_memory(raw_memory=_make_raw_memory(), language="python")
        assert result["existing_memory_id"] == "mem-b"

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.SearchManager")
    def test_merge_prompt_failure_returns_none(self, mock_search_cls, mock_run_prompt):
        """When the merge prompt fails, should return None (fallback to create)."""
        high_score = _make_search_result(score=SIMILARITY_THRESHOLD + 0.3)
        mock_instance = MagicMock()
        mock_instance.search_memories.return_value = [high_score]
        mock_search_cls.return_value = mock_instance

        mock_run_prompt.side_effect = Exception("LLM error")

        result = check_for_duplicate_memory(raw_memory=_make_raw_memory(), language="python")
        assert result is None

    @patch("src._memory_utils.SearchManager")
    def test_search_failure_returns_none(self, mock_search_cls):
        """When the search itself fails, should return None (fallback to create)."""
        mock_search_cls.side_effect = Exception("Search unavailable")

        result = check_for_duplicate_memory(raw_memory=_make_raw_memory(), language="python")
        assert result is None

    def test_empty_memory_returns_none(self):
        """When title and content are empty, should return None immediately."""
        result = check_for_duplicate_memory(raw_memory={"title": "", "content": ""}, language="python")
        assert result is None


class TestMergeAndSaveMemory:

    @patch("src._memory_utils.SearchManager")
    @patch("src._memory_utils.DatabaseManager")
    def test_merge_updates_existing_memory(self, mock_db_cls, mock_search_cls):
        """Should update the existing memory with merged content."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        # Mock fetching the existing memory
        mock_db.memories.get.return_value = {
            "id": "mem-existing",
            "title": "Old title",
            "content": "Old content",
            "source": "mention_agent",
            "is_exception": False,
            "related_guidelines": [],
            "related_examples": [],
            "related_memories": [],
        }

        merge_result = {
            "existing_memory_id": "mem-existing",
            "merged_title": "Merged title",
            "merged_content": "Merged content",
        }

        result = merge_and_save_memory(
            merge_result=merge_result,
            raw_memory=_make_raw_memory(),
            guideline_ids=[],
            raw_examples=[],
        )

        assert result["success"] == ["mem-existing"]
        assert result["failures"] == {}
        # Verify upsert was called for the memory
        mock_db.memories.upsert.assert_called_once()

    @patch("src._memory_utils.SearchManager")
    @patch("src._memory_utils.DatabaseManager")
    def test_merge_fetch_failure_falls_back_to_create(self, mock_db_cls, mock_search_cls):
        """If fetching the existing memory fails, should fall back to save_memory_with_links."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.memories.get.side_effect = Exception("Not found")
        mock_db.save_memory_with_links.return_value = {"success": ["mem-new"], "failures": {}}

        merge_result = {
            "existing_memory_id": "mem-existing",
            "merged_title": "Merged title",
            "merged_content": "Merged content",
        }

        raw_memory = _make_raw_memory()
        result = merge_and_save_memory(
            merge_result=merge_result,
            raw_memory=raw_memory,
            guideline_ids=[],
            raw_examples=[],
        )

        assert result["success"] == ["mem-new"]
        mock_db.save_memory_with_links.assert_called_once()
