# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument,protected-access,no-member,attribute-defined-outside-init,no-value-for-parameter

"""
Tests for DatabaseManager.link_items() and DatabaseManager.link_and_save().
"""

import sys
from unittest.mock import MagicMock, patch

import pytest

# Mock azure.cosmos and related modules before importing src
sys.modules.setdefault("azure.cosmos", MagicMock())
sys.modules.setdefault("azure.cosmos.exceptions", MagicMock())
sys.modules.setdefault("azure.search.documents.indexes", MagicMock())

from src._database_manager import RELATIONSHIP_FIELDS, DatabaseManager
from src._models import Example, ExampleType, Guideline, Memory


# ── Helpers ──────────────────────────────────────────────────────────────

def _make_guideline(**overrides):
    defaults = {
        "id": "python_design.html#naming",
        "title": "Naming",
        "content": "Use snake_case.",
        "language": "python",
        "related_memories": [],
        "related_examples": [],
        "related_guidelines": [],
    }
    defaults.update(overrides)
    return Guideline(**defaults)


def _make_memory(**overrides):
    defaults = {
        "id": "mem-001",
        "title": "Test memory",
        "content": "Some content.",
        "language": "python",
        "source": "test",
        "related_guidelines": [],
        "related_examples": [],
        "related_memories": [],
    }
    defaults.update(overrides)
    return Memory(**defaults)


def _make_example(**overrides):
    defaults = {
        "id": "ex-001",
        "title": "Test example",
        "content": "print('hello')",
        "language": "python",
        "example_type": ExampleType.GOOD,
        "guideline_ids": [],
        "memory_ids": [],
    }
    defaults.update(overrides)
    return Example(**defaults)


# ── link_items tests ─────────────────────────────────────────────────────

class TestLinkItemsBidirectional:
    """Verify link_items adds cross-references to both items."""

    def test_guideline_memory(self):
        g = _make_guideline()
        m = _make_memory()
        field_a, field_b, changed = DatabaseManager.link_items(g, "guideline", m, "memory")
        assert changed is True
        assert field_a == "related_memories"
        assert field_b == "related_guidelines"
        assert m.id in g.related_memories
        assert g.id in m.related_guidelines

    def test_guideline_example(self):
        g = _make_guideline()
        e = _make_example()
        field_a, field_b, changed = DatabaseManager.link_items(g, "guideline", e, "example")
        assert changed is True
        assert field_a == "related_examples"
        assert field_b == "guideline_ids"
        assert e.id in g.related_examples
        assert g.id in e.guideline_ids

    def test_memory_example(self):
        m = _make_memory()
        e = _make_example()
        field_a, field_b, changed = DatabaseManager.link_items(m, "memory", e, "example")
        assert changed is True
        assert field_a == "related_examples"
        assert field_b == "memory_ids"
        assert e.id in m.related_examples
        assert m.id in e.memory_ids

    def test_guideline_guideline(self):
        g1 = _make_guideline(id="python_design.html#naming")
        g2 = _make_guideline(id="python_design.html#types")
        _, _, changed = DatabaseManager.link_items(g1, "guideline", g2, "guideline")
        assert changed is True
        assert g2.id in g1.related_guidelines
        assert g1.id in g2.related_guidelines

    def test_memory_memory(self):
        m1 = _make_memory(id="mem-001")
        m2 = _make_memory(id="mem-002")
        _, _, changed = DatabaseManager.link_items(m1, "memory", m2, "memory")
        assert changed is True
        assert m2.id in m1.related_memories
        assert m1.id in m2.related_memories

    def test_reverse_order_memory_guideline(self):
        m = _make_memory()
        g = _make_guideline()
        field_a, field_b, changed = DatabaseManager.link_items(m, "memory", g, "guideline")
        assert changed is True
        assert field_a == "related_guidelines"
        assert field_b == "related_memories"
        assert g.id in m.related_guidelines
        assert m.id in g.related_memories


class TestLinkItemsIdempotent:
    """Verify link_items is idempotent — no duplicates on repeated calls."""

    def test_idempotent(self):
        g = _make_guideline()
        m = _make_memory()
        DatabaseManager.link_items(g, "guideline", m, "memory")
        _, _, changed = DatabaseManager.link_items(g, "guideline", m, "memory")
        assert changed is False
        assert g.related_memories.count(m.id) == 1
        assert m.related_guidelines.count(g.id) == 1


class TestLinkItemsInvalidPair:
    """Verify link_items raises ValueError for unsupported type pairs."""

    def test_invalid_pair(self):
        g = _make_guideline()
        with pytest.raises(ValueError, match="No relationship defined"):
            DatabaseManager.link_items(g, "guideline", g, "example_bogus")

    def test_example_example_not_supported(self):
        e1 = _make_example(id="ex-001")
        e2 = _make_example(id="ex-002")
        with pytest.raises(ValueError, match="No relationship defined"):
            DatabaseManager.link_items(e1, "example", e2, "example")


class TestRelationshipFieldsCoverage:
    """Verify RELATIONSHIP_FIELDS covers all expected pairs."""

    def test_all_canonical_pairs_present(self):
        expected = {
            ("guideline", "memory"),
            ("guideline", "example"),
            ("guideline", "guideline"),
            ("memory", "example"),
            ("memory", "guideline"),
            ("memory", "memory"),
            ("example", "guideline"),
            ("example", "memory"),
        }
        assert set(RELATIONSHIP_FIELDS.keys()) == expected


# ── link_and_save tests ─────────────────────────────────────────────────

class FakeContainer:
    """Minimal fake container that stores items in a dict."""

    def __init__(self, items):
        self._items = {item["id"]: dict(item) for item in items}

    @property
    def client(self):
        return self

    def read_item(self, item, partition_key):
        if item in self._items:
            return dict(self._items[item])
        from azure.cosmos.exceptions import CosmosResourceNotFoundError
        raise CosmosResourceNotFoundError(status_code=404, message=f"Item '{item}' not found")

    def upsert_item(self, body):
        self._items[body["id"]] = dict(body)
        return body

    def get(self, item_id):
        return self.read_item(item_id, item_id)

    def upsert(self, item_id, *, data, run_indexer=True):
        if hasattr(data, "model_dump_db"):
            body = data.model_dump_db()
        elif hasattr(data, "model_dump"):
            body = data.model_dump()
        else:
            body = dict(data)
        if "id" not in body:
            body["id"] = item_id
        return self.upsert_item(body)

    def run_indexer(self):
        pass


class FakeDB:
    """Fake DatabaseManager with injectable container data."""

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


class TestLinkAndSave:
    """Tests for DatabaseManager.link_and_save()."""

    def _make_db(self):
        return FakeDB(
            guidelines_data=[
                {
                    "id": "python_design=html=naming",
                    "title": "Naming",
                    "content": "Use snake_case.",
                    "language": "python",
                    "related_memories": [],
                    "related_examples": [],
                    "related_guidelines": [],
                },
            ],
            memories_data=[
                {
                    "id": "mem-001",
                    "title": "Test memory",
                    "content": "Content.",
                    "language": "python",
                    "source": "test",
                    "related_guidelines": [],
                    "related_examples": [],
                    "related_memories": [],
                },
            ],
            examples_data=[
                {
                    "id": "ex-001",
                    "title": "Test example",
                    "content": "print('hi')",
                    "language": "python",
                    "example_type": "good",
                    "guideline_ids": [],
                    "memory_ids": [],
                },
            ],
        )

    def test_links_guideline_and_memory(self):
        fake_db = self._make_db()
        with patch.object(DatabaseManager, "get_instance", return_value=fake_db):
            db = fake_db
            # Monkey-patch link_and_save onto our fake
            db.link_and_save = DatabaseManager.link_and_save.__get__(db, DatabaseManager)
            result = db.link_and_save("guideline", "python_design=html=naming", "memory", "mem-001", run_indexer=False)

        assert result["changed"] is True
        assert result["field_a"] == "related_memories"
        assert result["field_b"] == "related_guidelines"

        # Verify DB state
        g = fake_db.guidelines._items["python_design=html=naming"]
        m = fake_db.memories._items["mem-001"]
        assert "mem-001" in g["related_memories"]
        assert "python_design=html=naming" in m["related_guidelines"]

    def test_already_linked_returns_changed_false(self):
        fake_db = self._make_db()
        # Pre-link them
        fake_db.guidelines._items["python_design=html=naming"]["related_memories"] = ["mem-001"]
        fake_db.memories._items["mem-001"]["related_guidelines"] = ["python_design=html=naming"]

        with patch.object(DatabaseManager, "get_instance", return_value=fake_db):
            db = fake_db
            db.link_and_save = DatabaseManager.link_and_save.__get__(db, DatabaseManager)
            result = db.link_and_save("guideline", "python_design=html=naming", "memory", "mem-001", run_indexer=False)

        assert result["changed"] is False

    def test_rollback_on_second_write_failure(self):
        fake_db = self._make_db()
        original_g = dict(fake_db.guidelines._items["python_design=html=naming"])

        # Make memory upsert fail
        def fail_upsert(item_id, *, data, run_indexer=True):
            raise RuntimeError("Simulated write failure")

        fake_db.memories.upsert = fail_upsert

        with patch.object(DatabaseManager, "get_instance", return_value=fake_db):
            db = fake_db
            db.link_and_save = DatabaseManager.link_and_save.__get__(db, DatabaseManager)
            with pytest.raises(RuntimeError, match="Simulated write failure"):
                db.link_and_save("guideline", "python_design=html=naming", "memory", "mem-001", run_indexer=False)

        # Guideline should be rolled back to original state
        g = fake_db.guidelines._items["python_design=html=naming"]
        assert g["related_memories"] == original_g["related_memories"]

    def test_invalid_type_raises_value_error(self):
        fake_db = self._make_db()
        db = fake_db
        db.link_and_save = DatabaseManager.link_and_save.__get__(db, DatabaseManager)
        with pytest.raises(ValueError, match="Unknown item type"):
            db.link_and_save("bogus", "id1", "memory", "mem-001", run_indexer=False)

    def test_links_example_and_memory(self):
        fake_db = self._make_db()
        db = fake_db
        db.link_and_save = DatabaseManager.link_and_save.__get__(db, DatabaseManager)
        result = db.link_and_save("example", "ex-001", "memory", "mem-001", run_indexer=False)

        assert result["changed"] is True
        e = fake_db.examples._items["ex-001"]
        m = fake_db.memories._items["mem-001"]
        assert "mem-001" in e["memory_ids"]
        assert "ex-001" in m["related_examples"]
