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

from src._memory_utils import (
    SIMILARITY_THRESHOLD,
    apply_consolidation,
    check_for_duplicate_memory,
    find_consolidation_candidates,
    merge_and_save_memory,
)


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


def _make_memory_doc(memory_id, title="Memory", content="Content", guidelines=None, examples=None, memories=None):
    """Build a minimal memory document as stored in Cosmos DB."""
    return {
        "id": memory_id,
        "title": title,
        "content": content,
        "source": "mention_agent",
        "is_exception": False,
        "language": "python",
        "related_guidelines": guidelines or [],
        "related_examples": examples or [],
        "related_memories": memories or [],
    }


def _make_guideline_doc(gid, title="Guideline", memories=None, examples=None):
    """Build a minimal guideline document."""
    return {
        "id": gid,
        "title": title,
        "content": "Guideline content",
        "language": "python",
        "related_memories": memories or [],
        "related_examples": examples or [],
        "related_guidelines": [],
    }


def _make_example_doc(eid, title="Example", memory_ids=None, guideline_ids=None):
    """Build a minimal example document."""
    return {
        "id": eid,
        "title": title,
        "content": "Example content",
        "language": "python",
        "example_type": "good",
        "memory_ids": memory_ids or [],
        "guideline_ids": guideline_ids or [],
    }


class TestFindConsolidationCandidates:

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_guideline_with_one_memory_returns_empty(self, mock_db_cls, mock_run_prompt):
        """When a guideline has only 1 memory, returns empty list."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        guideline = _make_guideline_doc("g1", memories=["m1"])
        m1 = _make_memory_doc("m1")

        mock_db.guidelines.get.return_value = guideline
        mock_db.memories.get.return_value = m1

        result = find_consolidation_candidates(kind="guideline", ids=["g1"])
        assert result == []
        mock_run_prompt.assert_not_called()

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_guideline_with_duplicates_returns_actions(self, mock_db_cls, mock_run_prompt):
        """When a guideline has 2+ memories and the LLM finds duplicates, returns actions."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        guideline = _make_guideline_doc("g1", title="Use HTTP 404", memories=["m1", "m2", "m3"])
        m1 = _make_memory_doc("m1", title="Return 404 for missing resources")
        m2 = _make_memory_doc("m2", title="Use 404 for not-found resources")
        m3 = _make_memory_doc("m3", title="Use 409 for conflict errors")

        mock_db.guidelines.get.return_value = guideline
        mock_db.memories.get.side_effect = lambda mid: {"m1": m1, "m2": m2, "m3": m3}[mid]

        mock_run_prompt.return_value = json.dumps({
            "groups": [{
                "memory_ids": ["m1", "m2"],
                "merged_title": "Return 404 for missing resources",
                "merged_content": "APIs should return 404 when a resource is not found.",
                "reason": "Both describe the same 404 pattern.",
            }],
        })

        result = find_consolidation_candidates(kind="guideline", ids=["g1"])
        assert len(result) == 1
        assert result[0]["parent_id"] == "g1"
        assert len(result[0]["groups"]) == 1
        assert result[0]["groups"][0]["memory_ids"] == ["m1", "m2"]

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_no_duplicates_returns_empty(self, mock_db_cls, mock_run_prompt):
        """When the LLM finds no duplicates, returns empty list."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        guideline = _make_guideline_doc("g1", memories=["m1", "m2"])
        m1 = _make_memory_doc("m1", title="Topic A")
        m2 = _make_memory_doc("m2", title="Topic B")

        mock_db.guidelines.get.return_value = guideline
        mock_db.memories.get.side_effect = lambda mid: {"m1": m1, "m2": m2}[mid]

        mock_run_prompt.return_value = json.dumps({"groups": []})

        result = find_consolidation_candidates(kind="guideline", ids=["g1"])
        assert result == []

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_prompt_failure_skips_cluster(self, mock_db_cls, mock_run_prompt):
        """When the LLM prompt fails for a cluster, it is skipped (not raised)."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        guideline = _make_guideline_doc("g1", memories=["m1", "m2"])
        m1 = _make_memory_doc("m1")
        m2 = _make_memory_doc("m2")

        mock_db.guidelines.get.return_value = guideline
        mock_db.memories.get.side_effect = lambda mid: {"m1": m1, "m2": m2}[mid]

        mock_run_prompt.side_effect = Exception("LLM error")

        result = find_consolidation_candidates(kind="guideline", ids=["g1"])
        assert result == []

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_multiple_guideline_ids(self, mock_db_cls, mock_run_prompt):
        """Passing multiple guideline IDs evaluates each one's memory cluster."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        g1 = _make_guideline_doc("g1", title="Guideline A", memories=["m1", "m2"])
        g2 = _make_guideline_doc("g2", title="Guideline B", memories=["m3", "m4"])
        m1 = _make_memory_doc("m1")
        m2 = _make_memory_doc("m2")
        m3 = _make_memory_doc("m3")
        m4 = _make_memory_doc("m4")

        mock_db.guidelines.get.side_effect = lambda gid: {"g1": g1, "g2": g2}[gid]
        mock_db.memories.get.side_effect = lambda mid: {"m1": m1, "m2": m2, "m3": m3, "m4": m4}[mid]

        mock_run_prompt.return_value = json.dumps({"groups": []})

        find_consolidation_candidates(kind="guideline", ids=["g1", "g2"])
        assert mock_run_prompt.call_count == 2

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_deduplicates_identical_memory_sets(self, mock_db_cls, mock_run_prompt):
        """When two IDs produce the exact same memory set, only one cluster is evaluated."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        # Two guidelines with the same two memories
        g1 = _make_guideline_doc("g1", title="Guideline A", memories=["m1", "m2"])
        g2 = _make_guideline_doc("g2", title="Guideline B", memories=["m1", "m2"])
        m1 = _make_memory_doc("m1")
        m2 = _make_memory_doc("m2")

        mock_db.guidelines.get.side_effect = lambda gid: {"g1": g1, "g2": g2}[gid]
        mock_db.memories.get.side_effect = lambda mid: {"m1": m1, "m2": m2}[mid]

        mock_run_prompt.return_value = json.dumps({
            "groups": [{
                "memory_ids": ["m1", "m2"],
                "merged_title": "Merged",
                "merged_content": "Merged content",
                "reason": "Duplicates",
            }],
        })

        result = find_consolidation_candidates(kind="guideline", ids=["g1", "g2"])
        assert mock_run_prompt.call_count == 1
        assert len(result) == 1

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_example_kind(self, mock_db_cls, mock_run_prompt):
        """Kind 'example' should evaluate the example's memory cluster."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        example = _make_example_doc("e1", title="Good example", memory_ids=["m1", "m2"])
        m1 = _make_memory_doc("m1", title="Memory A")
        m2 = _make_memory_doc("m2", title="Memory B")

        mock_db.examples.get.return_value = example
        mock_db.memories.get.side_effect = lambda mid: {"m1": m1, "m2": m2}[mid]

        mock_run_prompt.return_value = json.dumps({"groups": []})

        result = find_consolidation_candidates(kind="example", ids=["e1"])
        assert result == []

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_memory_kind(self, mock_db_cls, mock_run_prompt):
        """Kind 'memory' should find parent guidelines/examples and evaluate those clusters."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        memory_doc = _make_memory_doc("m1", title="Memory A", guidelines=["g1"], examples=[])
        m2 = _make_memory_doc("m2", title="Memory B")
        guideline = _make_guideline_doc("g1", title="Guideline 1", memories=["m1", "m2"])

        mock_db.memories.get.side_effect = lambda mid: {"m1": memory_doc, "m2": m2}[mid]
        mock_db.guidelines.get.return_value = guideline

        mock_run_prompt.return_value = json.dumps({
            "groups": [{
                "memory_ids": ["m1", "m2"],
                "merged_title": "Merged",
                "merged_content": "Merged content",
                "reason": "Duplicates",
            }],
        })

        result = find_consolidation_candidates(kind="memory", ids=["m1"])
        assert len(result) == 1
        assert result[0]["parent_id"] == "g1"


class TestApplyConsolidation:

    @patch("src._memory_utils.SearchManager")
    @patch("src._memory_utils.DatabaseManager")
    def test_merges_and_deletes(self, mock_db_cls, mock_search_cls):
        """Should update survivor, transfer links, and soft-delete redundant memories."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        survivor_doc = _make_memory_doc("m1", guidelines=["g1"], examples=["e1"])
        redundant_doc = _make_memory_doc("m2", guidelines=["g1", "g2"], examples=["e2"])
        guideline_g1 = _make_guideline_doc("g1", memories=["m1", "m2"])
        guideline_g2 = _make_guideline_doc("g2", memories=["m2"])
        example_e2 = _make_example_doc("e2", memory_ids=["m2"])

        def mock_get(item_id):
            items = {
                "m1": survivor_doc,
                "m2": redundant_doc,
                "g1": guideline_g1,
                "g2": guideline_g2,
                "e2": example_e2,
            }
            return items[item_id]

        mock_db.memories.get.side_effect = mock_get
        mock_db.guidelines.get.side_effect = mock_get
        mock_db.examples.get.side_effect = mock_get

        actions = [{
            "parent_type": "guideline",
            "parent_id": "g1",
            "parent_title": "Use HTTP 404",
            "groups": [{
                "memory_ids": ["m1", "m2"],
                "merged_title": "Merged 404 memory",
                "merged_content": "Return 404 for missing resources.",
                "reason": "Duplicates",
            }],
        }]

        result = apply_consolidation(actions)
        assert result["merged"] == 1
        assert result["deleted"] == 1
        assert result["errors"] == []

        # Verify the survivor was upserted
        mock_db.memories.upsert.assert_called()
        # Verify the redundant memory was soft-deleted
        mock_db.memories.delete.assert_called_once_with("m2", run_indexer=False)
        # Verify guidelines were updated
        assert mock_db.guidelines.upsert.call_count >= 1

    @patch("src._memory_utils.SearchManager")
    @patch("src._memory_utils.DatabaseManager")
    def test_empty_actions_is_noop(self, mock_db_cls, mock_search_cls):
        """Empty actions list should return zeros and no errors."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        result = apply_consolidation([])
        assert result == {"merged": 0, "deleted": 0, "errors": []}

    @patch("src._memory_utils.SearchManager")
    @patch("src._memory_utils.DatabaseManager")
    def test_survivor_fetch_failure_records_error(self, mock_db_cls, mock_search_cls):
        """If the survivor memory can't be fetched, should record error and skip."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.memories.get.side_effect = Exception("Not found")

        actions = [{
            "parent_type": "guideline",
            "parent_id": "g1",
            "parent_title": "Test",
            "groups": [{
                "memory_ids": ["m1", "m2"],
                "merged_title": "Merged",
                "merged_content": "Content",
                "reason": "Duplicates",
            }],
        }]

        result = apply_consolidation(actions)
        assert result["merged"] == 0
        assert result["deleted"] == 0
        assert len(result["errors"]) == 1
