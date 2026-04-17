# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument,protected-access

"""
Tests for _cascade_unlink (generic cascading unlink/delete for all KB item types).
"""

import sys
from unittest.mock import MagicMock

# Mock azure.cosmos and related modules before importing src
sys.modules.setdefault("azure.cosmos", MagicMock())
sys.modules.setdefault("azure.cosmos.exceptions", MagicMock())
sys.modules.setdefault("azure.search.documents.indexes", MagicMock())

from cli import _cascade_unlink


class FakeContainer:
    """Minimal fake container for testing."""

    def __init__(self, items):
        self._items = {item["id"]: dict(item) for item in items}

    @property
    def client(self):
        return self

    def read_item(self, item, partition_key):
        if item in self._items:
            return self._items[item]
        raise KeyError(f"Item '{item}' not found")

    def upsert_item(self, body):
        self._items[body["id"]] = body
        return body

    def get(self, item_id):
        return self.read_item(item_id, item_id)

    def delete(self, item_id, *, run_indexer=True):
        if item_id in self._items:
            self._items[item_id]["isDeleted"] = True
        return self._items.get(item_id)

    def run_indexer(self):
        pass


class FakeDB:
    def __init__(self, guidelines_data=None, examples_data=None, memories_data=None):
        self._guidelines = FakeContainer(guidelines_data or [])
        self._examples = FakeContainer(examples_data or [])
        self._memories = FakeContainer(memories_data or [])

    @property
    def guidelines(self):
        return self._guidelines

    @property
    def examples(self):
        return self._examples

    @property
    def memories(self):
        return self._memories


# ---------------------------------------------------------------------------
# Deleting a MEMORY
# ---------------------------------------------------------------------------
class TestCascadeDeleteMemory:

    def _make_db(self):
        return FakeDB(
            guidelines_data=[
                {
                    "id": "python_design=html=naming",
                    "title": "Naming",
                    "content": "Use snake_case.",
                    "language": "python",
                    "related_memories": ["mem-001", "mem-002"],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            examples_data=[
                {
                    "id": "mem-001-example-1",
                    "title": "Good example",
                    "content": "print('hello')",
                    "language": "python",
                    "example_type": "good",
                    "guideline_ids": [],
                    "memory_ids": ["mem-001"],
                },
                {
                    "id": "mem-001-example-2",
                    "title": "Bad example",
                    "content": "Print('hello')",
                    "language": "python",
                    "example_type": "bad",
                    "guideline_ids": [],
                    "memory_ids": ["mem-001"],
                },
            ],
            memories_data=[
                {
                    "id": "mem-001",
                    "title": "Test memory",
                    "content": "Content.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": ["python_design=html=naming"],
                    "related_examples": ["mem-001-example-1", "mem-001-example-2"],
                    "related_memories": ["mem-002"],
                },
                {
                    "id": "mem-002",
                    "title": "Other memory",
                    "content": "Other content.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": ["python_design=html=naming"],
                    "related_examples": [],
                    "related_memories": ["mem-001"],
                },
            ],
        )

    def test_orphaned_examples_are_soft_deleted(self):
        """Examples linked only to the deleted memory should be soft-deleted."""
        db = self._make_db()
        memory_item = db.memories.get("mem-001")
        _cascade_unlink(db, memory_item, "memory")

        assert db.examples._items["mem-001-example-1"].get("isDeleted") is True
        assert db.examples._items["mem-001-example-2"].get("isDeleted") is True

    def test_shared_example_is_retained(self):
        """An example linked to another memory should NOT be soft-deleted."""
        db = self._make_db()
        db.examples._items["mem-001-example-1"]["memory_ids"] = ["mem-001", "mem-002"]

        memory_item = db.memories.get("mem-001")
        _cascade_unlink(db, memory_item, "memory")

        ex1 = db.examples._items["mem-001-example-1"]
        assert ex1.get("isDeleted") is not True
        assert "mem-001" not in ex1["memory_ids"]
        assert "mem-002" in ex1["memory_ids"]

        assert db.examples._items["mem-001-example-2"].get("isDeleted") is True

    def test_example_with_guideline_link_is_retained(self):
        """An example linked to a guideline should NOT be soft-deleted."""
        db = self._make_db()
        db.examples._items["mem-001-example-1"]["guideline_ids"] = ["python_design=html=naming"]

        memory_item = db.memories.get("mem-001")
        _cascade_unlink(db, memory_item, "memory")

        ex1 = db.examples._items["mem-001-example-1"]
        assert ex1.get("isDeleted") is not True
        assert "mem-001" not in ex1["memory_ids"]

        assert db.examples._items["mem-001-example-2"].get("isDeleted") is True

    def test_removes_backlink_from_guideline(self):
        db = self._make_db()
        memory_item = db.memories.get("mem-001")
        _cascade_unlink(db, memory_item, "memory")

        guideline = db.guidelines._items["python_design=html=naming"]
        assert "mem-001" not in guideline["related_memories"]
        assert "mem-002" in guideline["related_memories"]

    def test_removes_backlink_from_related_memory(self):
        db = self._make_db()
        memory_item = db.memories.get("mem-001")
        _cascade_unlink(db, memory_item, "memory")

        other_memory = db.memories._items["mem-002"]
        assert "mem-001" not in other_memory["related_memories"]

    def test_no_related_items_is_noop(self):
        db = FakeDB(
            memories_data=[
                {
                    "id": "mem-solo",
                    "title": "Solo memory",
                    "content": "No links.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        memory_item = db.memories.get("mem-solo")
        _cascade_unlink(db, memory_item, "memory")

    def test_missing_example_does_not_crash(self):
        """If an example ID is stale and doesn't exist, cascade should continue."""
        db = FakeDB(
            memories_data=[
                {
                    "id": "mem-001",
                    "title": "Memory",
                    "content": "Content.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": [],
                    "related_examples": ["nonexistent-example"],
                    "related_memories": [],
                },
            ],
        )
        memory_item = db.memories.get("mem-001")
        _cascade_unlink(db, memory_item, "memory")

    def test_missing_guideline_does_not_crash(self):
        """If a guideline ID is stale and doesn't exist, cascade should continue."""
        db = FakeDB(
            memories_data=[
                {
                    "id": "mem-001",
                    "title": "Memory",
                    "content": "Content.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": ["nonexistent-guideline"],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )
        memory_item = db.memories.get("mem-001")
        _cascade_unlink(db, memory_item, "memory")


# ---------------------------------------------------------------------------
# Deleting a GUIDELINE
# ---------------------------------------------------------------------------
class TestCascadeDeleteGuideline:

    def _make_db(self):
        return FakeDB(
            guidelines_data=[
                {
                    "id": "gl-001",
                    "title": "Naming guideline",
                    "content": "Use snake_case.",
                    "language": "python",
                    "related_memories": ["mem-001"],
                    "related_examples": ["ex-001", "ex-002"],
                    "related_guidelines": ["gl-002"],
                },
                {
                    "id": "gl-002",
                    "title": "Other guideline",
                    "content": "Use docstrings.",
                    "language": "python",
                    "related_memories": [],
                    "related_examples": [],
                    "related_guidelines": ["gl-001"],
                },
            ],
            examples_data=[
                {
                    "id": "ex-001",
                    "title": "Good example",
                    "content": "print('hello')",
                    "language": "python",
                    "example_type": "good",
                    "guideline_ids": ["gl-001"],
                    "memory_ids": [],
                },
                {
                    "id": "ex-002",
                    "title": "Shared example",
                    "content": "print('shared')",
                    "language": "python",
                    "example_type": "good",
                    "guideline_ids": ["gl-001", "gl-002"],
                    "memory_ids": [],
                },
            ],
            memories_data=[
                {
                    "id": "mem-001",
                    "title": "Related memory",
                    "content": "Memory content.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": ["gl-001"],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
        )

    def test_orphaned_example_is_soft_deleted(self):
        """Example linked only to the deleted guideline should be soft-deleted."""
        db = self._make_db()
        guideline_item = db.guidelines.get("gl-001")
        _cascade_unlink(db, guideline_item, "guideline")

        assert db.examples._items["ex-001"].get("isDeleted") is True

    def test_shared_example_is_retained(self):
        """Example linked to another guideline should NOT be soft-deleted."""
        db = self._make_db()
        guideline_item = db.guidelines.get("gl-001")
        _cascade_unlink(db, guideline_item, "guideline")

        ex2 = db.examples._items["ex-002"]
        assert ex2.get("isDeleted") is not True
        assert "gl-001" not in ex2["guideline_ids"]
        assert "gl-002" in ex2["guideline_ids"]

    def test_removes_backlink_from_memory(self):
        """Memory's related_guidelines should no longer contain the deleted guideline."""
        db = self._make_db()
        guideline_item = db.guidelines.get("gl-001")
        _cascade_unlink(db, guideline_item, "guideline")

        mem = db.memories._items["mem-001"]
        assert "gl-001" not in mem["related_guidelines"]

    def test_memory_is_always_retained(self):
        """An orphaned memory should NOT be soft-deleted (only examples get deleted)."""
        db = self._make_db()
        guideline_item = db.guidelines.get("gl-001")
        _cascade_unlink(db, guideline_item, "guideline")

        mem = db.memories._items["mem-001"]
        assert mem.get("isDeleted") is not True

    def test_removes_backlink_from_related_guideline(self):
        """Other guideline's related_guidelines should no longer contain deleted one."""
        db = self._make_db()
        guideline_item = db.guidelines.get("gl-001")
        _cascade_unlink(db, guideline_item, "guideline")

        gl2 = db.guidelines._items["gl-002"]
        assert "gl-001" not in gl2["related_guidelines"]

    def test_no_related_items_is_noop(self):
        db = FakeDB(
            guidelines_data=[
                {
                    "id": "gl-solo",
                    "title": "Solo guideline",
                    "content": "No links.",
                    "language": "python",
                    "related_memories": [],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
        )
        guideline_item = db.guidelines.get("gl-solo")
        _cascade_unlink(db, guideline_item, "guideline")


# ---------------------------------------------------------------------------
# Deleting an EXAMPLE
# ---------------------------------------------------------------------------
class TestCascadeDeleteExample:

    def _make_db(self):
        return FakeDB(
            guidelines_data=[
                {
                    "id": "gl-001",
                    "title": "Naming guideline",
                    "content": "Use snake_case.",
                    "language": "python",
                    "related_memories": [],
                    "related_examples": ["ex-001"],
                    "related_guidelines": [],
                },
            ],
            examples_data=[
                {
                    "id": "ex-001",
                    "title": "Good example",
                    "content": "print('hello')",
                    "language": "python",
                    "example_type": "good",
                    "guideline_ids": ["gl-001"],
                    "memory_ids": ["mem-001"],
                },
            ],
            memories_data=[
                {
                    "id": "mem-001",
                    "title": "Related memory",
                    "content": "Memory content.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": [],
                    "related_examples": ["ex-001"],
                    "related_memories": [],
                },
            ],
        )

    def test_removes_backlink_from_guideline(self):
        """Guideline's related_examples should no longer contain the deleted example."""
        db = self._make_db()
        example_item = db.examples.get("ex-001")
        _cascade_unlink(db, example_item, "example")

        gl = db.guidelines._items["gl-001"]
        assert "ex-001" not in gl["related_examples"]

    def test_removes_backlink_from_memory(self):
        """Memory's related_examples should no longer contain the deleted example."""
        db = self._make_db()
        example_item = db.examples.get("ex-001")
        _cascade_unlink(db, example_item, "example")

        mem = db.memories._items["mem-001"]
        assert "ex-001" not in mem["related_examples"]

    def test_guideline_and_memory_are_always_retained(self):
        """Even if guideline/memory lose their last example, they should NOT be deleted."""
        db = self._make_db()
        example_item = db.examples.get("ex-001")
        _cascade_unlink(db, example_item, "example")

        assert db.guidelines._items["gl-001"].get("isDeleted") is not True
        assert db.memories._items["mem-001"].get("isDeleted") is not True

    def test_no_links_is_noop(self):
        db = FakeDB(
            examples_data=[
                {
                    "id": "ex-solo",
                    "title": "Solo example",
                    "content": "No links.",
                    "language": "python",
                    "example_type": "good",
                    "guideline_ids": [],
                    "memory_ids": [],
                },
            ],
        )
        example_item = db.examples.get("ex-solo")
        _cascade_unlink(db, example_item, "example")
