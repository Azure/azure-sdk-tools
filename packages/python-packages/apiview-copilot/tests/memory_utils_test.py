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
    _NEW_MEMORY_SENTINEL,
    apply_consolidation,
    check_for_duplicate_memory,
    find_consolidation_candidates,
    merge_and_save_memory,
)


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

    def test_no_guideline_ids_returns_none(self):
        """When no guideline_ids are provided, should skip dedup."""
        result = check_for_duplicate_memory(raw_memory=_make_raw_memory())
        assert result is None

    def test_empty_guideline_ids_returns_none(self):
        """When guideline_ids is an empty list, should skip dedup."""
        result = check_for_duplicate_memory(
            raw_memory=_make_raw_memory(), guideline_ids=[]
        )
        assert result is None

    def test_empty_memory_returns_none(self):
        """When title and content are empty, should return None immediately."""
        result = check_for_duplicate_memory(
            raw_memory={"title": "", "content": ""}, guideline_ids=["g1"]
        )
        assert result is None

    @patch("src._memory_utils.DatabaseManager")
    def test_guideline_not_found_returns_none(self, mock_db_cls):
        """When the guideline can't be fetched, should return None."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.guidelines.get_batched.side_effect = Exception("Not found")

        result = check_for_duplicate_memory(
            raw_memory=_make_raw_memory(), guideline_ids=["g1"]
        )
        assert result is None

    @patch("src._memory_utils.DatabaseManager")
    def test_guideline_with_no_memories_returns_none(self, mock_db_cls):
        """When the guideline has no related memories, should return None."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.guidelines.get_batched.return_value = [{"id": "g1", "title": "Guideline", "related_memories": []}]

        result = check_for_duplicate_memory(
            raw_memory=_make_raw_memory(), guideline_ids=["g1"]
        )
        assert result is None

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_new_memory_grouped_with_existing_returns_merge(self, mock_db_cls, mock_run_prompt):
        """When the consolidation prompt groups the new memory with an existing one, should return merge result."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.guidelines.get_batched.return_value = [
            {"id": "g1", "title": "Guideline", "related_memories": ["mem-existing"]}
        ]
        mock_db.memories.get_batched.return_value = [
            {"id": "mem-existing", "title": "Existing title", "content": "Existing content"}
        ]

        mock_run_prompt.return_value = json.dumps({
            "groups": [{
                "memory_ids": ["mem-existing", _NEW_MEMORY_SENTINEL],
                "merged_title": "Merged title",
                "merged_content": "Merged content",
                "reason": "Same core recommendation",
            }]
        })

        result = check_for_duplicate_memory(
            raw_memory=_make_raw_memory(), guideline_ids=["g1"]
        )
        assert result is not None
        assert result["existing_memory_id"] == "mem-existing"
        assert result["merged_title"] == "Merged title"
        assert result["merged_content"] == "Merged content"

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_new_memory_not_grouped_returns_none(self, mock_db_cls, mock_run_prompt):
        """When the consolidation prompt doesn't group the new memory, should return None."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.guidelines.get_batched.return_value = [
            {"id": "g1", "title": "Guideline", "related_memories": ["mem-existing"]}
        ]
        mock_db.memories.get_batched.return_value = [
            {"id": "mem-existing", "title": "Existing title", "content": "Existing content"}
        ]

        mock_run_prompt.return_value = json.dumps({"groups": []})

        result = check_for_duplicate_memory(
            raw_memory=_make_raw_memory(), guideline_ids=["g1"]
        )
        assert result is None

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_consolidation_prompt_failure_returns_none(self, mock_db_cls, mock_run_prompt):
        """When the consolidation prompt fails, should return None (fallback to create)."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.guidelines.get_batched.return_value = [
            {"id": "g1", "title": "Guideline", "related_memories": ["mem-existing"]}
        ]
        mock_db.memories.get_batched.return_value = [
            {"id": "mem-existing", "title": "Existing title", "content": "Existing content"}
        ]

        mock_run_prompt.side_effect = Exception("LLM error")

        result = check_for_duplicate_memory(
            raw_memory=_make_raw_memory(), guideline_ids=["g1"]
        )
        assert result is None

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_bad_guideline_ids_are_passed_through_container_preprocessing(self, mock_db_cls, mock_run_prompt):
        """The caller should pass guideline IDs through unchanged and let the container preprocess them."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db
        mock_db.guidelines.get_batched.return_value = [
            {"id": "python_design=html=some-rule", "title": "Some Rule", "related_memories": ["mem-1"]}
        ]
        mock_db.memories.get_batched.return_value = [
            {"id": "mem-1", "title": "M1", "content": "C1"}
        ]
        mock_run_prompt.return_value = json.dumps({"groups": []})

        check_for_duplicate_memory(
            raw_memory=_make_raw_memory(),
            guideline_ids=["https://azure.github.io/azure-sdk/python_design.html#some-rule"],
        )
        mock_db.guidelines.get_batched.assert_called_once_with(
            ["https://azure.github.io/azure-sdk/python_design.html#some-rule"]
        )

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_collects_memories_across_multiple_guidelines(self, mock_db_cls, mock_run_prompt):
        """Should collect existing memories from all provided guidelines."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        mock_db.guidelines.get_batched.return_value = [
            {"id": "g1", "title": "G1", "related_memories": ["mem-1"]},
            {"id": "g2", "title": "G2", "related_memories": ["mem-2"]},
        ]
        mock_db.memories.get_batched.return_value = [
            {"id": "mem-1", "title": "Title mem-1", "content": "Content mem-1"},
            {"id": "mem-2", "title": "Title mem-2", "content": "Content mem-2"},
        ]
        mock_run_prompt.return_value = json.dumps({"groups": []})

        check_for_duplicate_memory(
            raw_memory=_make_raw_memory(), guideline_ids=["g1", "g2"]
        )

        call_args = mock_run_prompt.call_args
        memories_json = json.loads(call_args[1]["inputs"]["memories"])
        assert len(memories_json) == 3
        memory_ids = {m["id"] for m in memories_json}
        assert memory_ids == {"mem-1", "mem-2", _NEW_MEMORY_SENTINEL}

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_batches_guideline_and_memory_fetches(self, mock_db_cls, mock_run_prompt):
        """Should batch guideline and memory reads instead of fetching each item individually."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        mock_db.guidelines.get.side_effect = AssertionError("unexpected individual guideline get")
        mock_db.memories.get.side_effect = AssertionError("unexpected individual memory get")

        mock_db.guidelines.get_batched.return_value = [
            {"id": "g1", "title": "G1", "related_memories": ["mem-1", "mem-2"]},
            {"id": "g2", "title": "G2", "related_memories": ["mem-2", "mem-3"]},
        ]
        mock_db.memories.get_batched.return_value = [
            {"id": "mem-1", "title": "Title 1", "content": "Content 1"},
            {"id": "mem-2", "title": "Title 2", "content": "Content 2"},
            {"id": "mem-3", "title": "Title 3", "content": "Content 3"},
        ]
        mock_run_prompt.return_value = json.dumps({
            "groups": [{
                "memory_ids": ["mem-1", _NEW_MEMORY_SENTINEL],
                "merged_title": "Merged title",
                "merged_content": "Merged content",
            }]
        })

        result = check_for_duplicate_memory(
            raw_memory=_make_raw_memory(),
            guideline_ids=["g1", "g2"],
        )

        assert result is not None
        assert result["existing_memory_id"] == "mem-1"
        mock_db.guidelines.get_batched.assert_called_once_with(["g1", "g2"])
        mock_db.memories.get_batched.assert_called_once_with(["mem-1", "mem-2", "mem-2", "mem-3"])

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

        mock_db.guidelines.get_batched.return_value = [guideline]
        mock_db.memories.get_batched.return_value = [m1]

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

        mock_db.guidelines.get_batched.return_value = [guideline]
        mock_db.memories.get_batched.side_effect = lambda ids: [{"m1": m1, "m2": m2, "m3": m3}[mid] for mid in ids]

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

        mock_db.guidelines.get_batched.return_value = [guideline]
        mock_db.memories.get_batched.side_effect = lambda ids: [{"m1": m1, "m2": m2}[mid] for mid in ids]

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

        mock_db.guidelines.get_batched.return_value = [guideline]
        mock_db.memories.get_batched.side_effect = lambda ids: [{"m1": m1, "m2": m2}[mid] for mid in ids]

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

        mock_db.guidelines.get_batched.return_value = [g1, g2]
        mock_db.memories.get_batched.side_effect = lambda ids: [{"m1": m1, "m2": m2, "m3": m3, "m4": m4}[mid] for mid in ids]

        mock_run_prompt.return_value = json.dumps({"groups": []})

        find_consolidation_candidates(kind="guideline", ids=["g1", "g2"])
        assert mock_run_prompt.call_count == 2

    @patch("src._memory_utils.run_prompt")
    @patch("src._memory_utils.DatabaseManager")
    def test_deduplicates_identical_memory_sets(self, mock_db_cls, mock_run_prompt):
        """When two IDs produce the exact same memory set, only one cluster is evaluated."""
        mock_db = MagicMock()
        mock_db_cls.get_instance.return_value = mock_db

        g1 = _make_guideline_doc("g1", title="Guideline A", memories=["m1", "m2"])
        g2 = _make_guideline_doc("g2", title="Guideline B", memories=["m1", "m2"])
        m1 = _make_memory_doc("m1")
        m2 = _make_memory_doc("m2")

        mock_db.guidelines.get_batched.return_value = [g1, g2]
        mock_db.memories.get_batched.side_effect = lambda ids: [{"m1": m1, "m2": m2}[mid] for mid in ids]

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

        mock_db.examples.get_batched.return_value = [example]
        mock_db.memories.get_batched.side_effect = lambda ids: [{"m1": m1, "m2": m2}[mid] for mid in ids]

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
        mock_db.memories.get_batched.side_effect = lambda ids: [{"m1": memory_doc, "m2": m2}[mid] for mid in ids]
        mock_db.guidelines.get_batched.return_value = [guideline]

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
