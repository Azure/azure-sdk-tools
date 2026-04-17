# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument,protected-access,broad-exception-raised

"""
Tests for `check_links_kb` CLI command.
"""

import sys
from unittest.mock import MagicMock, patch

# Mock azure.cosmos and related modules before importing cli
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()
sys.modules["azure.search.documents.indexes"] = MagicMock()

# Import the function under test
from cli import check_links_kb


class FakeContainer:
    """Minimal fake container that stores items in a dict and supports query_items and get."""

    def __init__(self, items):
        self._items = {item["id"]: dict(item) for item in items}

    @property
    def client(self):
        return self

    def query_items(self, query, parameters=None, enable_cross_partition_query=True):
        """Return all non-deleted items, optionally filtering by language."""
        results = []
        lang_filter = None
        if parameters:
            for p in parameters:
                if p["name"] == "@lang":
                    lang_filter = p["value"]
        for item in self._items.values():
            if item.get("isDeleted"):
                continue
            if lang_filter and item.get("language") != lang_filter:
                continue
            results.append(dict(item))
        return results

    def get(self, item_id):
        if item_id in self._items:
            return dict(self._items[item_id])
        raise Exception(f"Item '{item_id}' not found")

    def upsert_item(self, item):
        self._items[item["id"]] = dict(item)
        return item


class FakeDB:
    """Fake DatabaseManager with injectable container data."""

    def __init__(self, guidelines=None, examples=None, memories=None):
        self._guidelines = FakeContainer(guidelines or [])
        self._examples = FakeContainer(examples or [])
        self._memories = FakeContainer(memories or [])

    @property
    def guidelines(self):
        return self._guidelines

    @property
    def examples(self):
        return self._examples

    @property
    def memories(self):
        return self._memories


def _patch_db(fake_db):
    """Return a patch context that makes DatabaseManager.get_instance() return fake_db."""
    return patch("cli.DatabaseManager.get_instance", return_value=fake_db)


class TestCheckLinksHealthy:
    def test_all_links_bidirectional(self, capsys):
        """When all links are bidirectional, report no issues."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1"],
                    "related_examples": ["e1"],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": ["g1=html=rule"],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
            examples=[
                {
                    "id": "e1",
                    "guideline_ids": ["g1=html=rule"],
                    "memory_ids": [],
                },
            ],
        )
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "No issues found" in output

    def test_empty_containers(self, capsys):
        """Empty containers should report no issues."""
        db = FakeDB()
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "No issues found" in output


class TestCheckLinksDangling:
    def test_dangling_memory_reference(self, capsys):
        """Guideline references a memory that doesn't exist."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["nonexistent"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
        )
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "Dangling references" in output
        assert "nonexistent" in output
        assert "NOT FOUND" in output

    def test_dangling_guideline_reference(self, capsys):
        """Example references a guideline that doesn't exist."""
        db = FakeDB(
            examples=[
                {
                    "id": "e1",
                    "guideline_ids": ["gone=html=rule"],
                    "memory_ids": [],
                },
            ],
        )
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "Dangling references" in output
        assert "gone.html#rule" in output


class TestCheckLinksOneWay:
    def test_one_way_guideline_to_memory(self, capsys):
        """Guideline references memory, but memory does not link back."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],  # missing back-ref
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "One-way links" in output
        assert "g1.html#rule" in output
        assert "m1" in output
        assert "missing reverse" in output

    def test_one_way_example_to_guideline(self, capsys):
        """Example references guideline, but guideline doesn't link back."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": [],
                    "related_examples": [],  # missing back-ref to e1
                    "related_guidelines": [],
                },
            ],
            examples=[
                {
                    "id": "e1",
                    "guideline_ids": ["g1=html=rule"],
                    "memory_ids": [],
                },
            ],
        )
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "One-way links" in output

    def test_one_way_memory_to_memory(self, capsys):
        """Memory references another memory, but it's not reciprocated."""
        db = FakeDB(
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": ["m2"],
                },
                {
                    "id": "m2",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],  # missing back-ref to m1
                },
            ],
        )
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "One-way links" in output
        assert "m1" in output
        assert "m2" in output


class TestCheckLinksFix:
    def test_fix_adds_missing_back_reference(self, capsys):
        """--fix should add the missing back-reference to the target item."""
        memories_data = [
            {
                "id": "m1",
                "related_guidelines": [],
                "related_examples": [],
                "related_memories": [],
            },
        ]
        guidelines_data = [
            {
                "id": "g1=html=rule",
                "related_memories": ["m1"],
                "related_examples": [],
                "related_guidelines": [],
            },
        ]
        db = FakeDB(guidelines=guidelines_data, memories=memories_data)
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="oneway")

        output = capsys.readouterr().out
        assert "Fixing" in output
        assert "Fixed" in output

        # Verify the memory now has the back-reference
        updated_memory = db.memories.client._items["m1"]
        assert "g1=html=rule" in updated_memory["related_guidelines"]

    def test_fix_is_idempotent(self, capsys):
        """Running --fix twice should not produce errors or duplicates."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="all")
            capsys.readouterr()  # clear output
            # Second fix run — the link is now bidirectional, nothing to fix
            check_links_kb(fix="all")
        output = capsys.readouterr().out
        assert "No issues found" in output

    def test_fix_multiple_one_way_links(self, capsys):
        """--fix handles multiple one-way links across different containers."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1"],
                    "related_examples": ["e1"],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
            examples=[
                {
                    "id": "e1",
                    "guideline_ids": [],
                    "memory_ids": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="all")

        updated_memory = db.memories.client._items["m1"]
        assert "g1=html=rule" in updated_memory["related_guidelines"]

        updated_example = db.examples.client._items["e1"]
        assert "g1=html=rule" in updated_example["guideline_ids"]


class TestCheckLinksLanguageFilter:
    def test_language_filter_restricts_scope(self, capsys):
        """With --language, only items of that language are checked."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g_py=html=rule",
                    "language": "python",
                    "related_memories": ["m_bad"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
                {
                    "id": "g_js=html=rule",
                    "language": "typescript",
                    "related_memories": ["m_also_bad"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
        )
        with _patch_db(db):
            check_links_kb(language="python")
        output = capsys.readouterr().out
        # Should report the dangling ref for the Python guideline
        assert "m_bad" in output
        # Should NOT report the TypeScript guideline since we filtered to python
        assert "m_also_bad" not in output


class TestCheckLinksMixed:
    def test_both_dangling_and_one_way(self, capsys):
        """Report both dangling and one-way issues in the same run."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1", "nonexistent"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],  # one-way: guideline->memory but not back
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db):
            check_links_kb()
        output = capsys.readouterr().out
        assert "Dangling references" in output
        assert "One-way links" in output
        assert "1 dangling" in output
        assert "1 one-way" in output


class TestCheckLinksFixDangling:
    def test_fix_removes_dangling_references(self, capsys):
        """--fix should remove IDs that point to non-existent items."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["gone_memory"],
                    "related_examples": ["gone_example"],
                    "related_guidelines": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="broken")
        output = capsys.readouterr().out
        assert "dangling" in output.lower()

        # Verify the dangling refs were removed
        updated = db.guidelines.client._items["g1=html=rule"]
        assert updated["related_memories"] == []
        assert updated["related_examples"] == []

    def test_fix_broken_does_not_fix_oneway(self, capsys):
        """--fix broken should only remove dangling refs, not add back-references."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1", "nonexistent"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="broken")
        output = capsys.readouterr().out
        assert "dangling" in output.lower()
        # Should NOT fix one-way links (no "one-way link" fix action)
        assert "one-way link" not in output
        updated_m = db.memories.client._items["m1"]
        assert updated_m["related_guidelines"] == []

    def test_fix_dangling_and_one_way_together(self, capsys):
        """--fix handles both dangling removal and back-reference addition."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1", "nonexistent"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="all")
        output = capsys.readouterr().out
        assert "dangling" in output.lower()
        assert "Fixing" in output

        # Dangling ref removed from guideline
        updated_g = db.guidelines.client._items["g1=html=rule"]
        assert "nonexistent" not in updated_g["related_memories"]
        # Back-reference added to memory
        updated_m = db.memories.client._items["m1"]
        assert "g1=html=rule" in updated_m["related_guidelines"]
        assert "1 one-way" in output


class TestCheckLinksFixOneway:
    def test_fix_oneway_does_not_remove_dangling(self, capsys):
        """--fix oneway should only add back-refs, not remove dangling."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": ["m1", "nonexistent"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="oneway")
        output = capsys.readouterr().out
        # Should fix one-way links
        assert "Fixing" in output
        updated_m = db.memories.client._items["m1"]
        assert "g1=html=rule" in updated_m["related_guidelines"]
        # Should NOT remove dangling (verify the dangling ref is still in the item)
        updated_g = db.guidelines.client._items["g1=html=rule"]
        assert "nonexistent" in updated_g["related_memories"]


class TestCheckLinksHealGuidelineFormat:
    def test_heals_web_format_guideline_id(self, capsys):
        """A web-format guideline ref should be converted to DB format when the target exists."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": [],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    # Web format — should be healed to g1=html=rule
                    "related_guidelines": ["g1.html#rule"],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="broken")
        output = capsys.readouterr().out
        assert "Healed" in output

        updated_m = db.memories.client._items["m1"]
        assert "g1=html=rule" in updated_m["related_guidelines"]
        assert "g1.html#rule" not in updated_m["related_guidelines"]

    def test_removes_web_format_when_db_format_already_present(self, capsys):
        """If both web and DB format exist, the web-format duplicate is removed."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": [],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories=[
                {
                    "id": "m1",
                    # Both formats present — web format is a duplicate
                    "related_guidelines": ["g1.html#rule", "g1=html=rule"],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="broken")
        output = capsys.readouterr().out
        assert "removed" in output

        updated_m = db.memories.client._items["m1"]
        assert updated_m["related_guidelines"] == ["g1=html=rule"]

    def test_removes_web_format_when_guideline_does_not_exist(self, capsys):
        """If the converted ID still doesn't match a real guideline, remove it."""
        db = FakeDB(
            memories=[
                {
                    "id": "m1",
                    "related_guidelines": ["fake_page.html#fake-rule"],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="broken")

        updated_m = db.memories.client._items["m1"]
        assert updated_m["related_guidelines"] == []

    def test_heals_guideline_ids_on_example(self, capsys):
        """Web-format guideline_ids on examples should also be healed."""
        db = FakeDB(
            guidelines=[
                {
                    "id": "g1=html=rule",
                    "related_memories": [],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            examples=[
                {
                    "id": "e1",
                    "guideline_ids": ["g1.html#rule"],
                    "memory_ids": [],
                },
            ],
        )
        with _patch_db(db), patch("cli._try_run_indexers"):
            check_links_kb(fix="broken")
        output = capsys.readouterr().out
        assert "Healed" in output

        updated_e = db.examples.client._items["e1"]
        assert "g1=html=rule" in updated_e["guideline_ids"]
        assert "g1.html#rule" not in updated_e["guideline_ids"]
