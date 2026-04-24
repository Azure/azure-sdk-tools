# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Tests for guideline ID format conversion across models, utilities, and DB layer.
"""

from unittest.mock import MagicMock

import azure.cosmos.exceptions as _cosmos_exc

from src._database_manager import BasicContainer, GuidelinesContainer
from src._models import Example, ExampleType, Guideline, Memory, ReviewResult
from src._utils import guideline_id_from_db, guideline_id_to_db


# ---------------------------------------------------------------------------
# Utility function tests
# ---------------------------------------------------------------------------


class TestGuidelineIdConversion:
    def test_to_db_format(self):
        assert guideline_id_to_db("python_design.html#naming") == "python_design=html=naming"

    def test_from_db_format(self):
        assert guideline_id_from_db("python_design=html=naming") == "python_design.html#naming"

    def test_round_trip_web_to_db_and_back(self):
        web = "python_design.html#naming-conventions"
        assert guideline_id_from_db(guideline_id_to_db(web)) == web

    def test_round_trip_db_to_web_and_back(self):
        db = "python_design=html=naming-conventions"
        assert guideline_id_to_db(guideline_id_from_db(db)) == db

    def test_no_op_when_already_db_format(self):
        db = "python_design=html=naming"
        assert guideline_id_to_db(db) == db

    def test_no_op_when_already_web_format(self):
        web = "python_design.html#naming"
        assert guideline_id_from_db(web) == web

    def test_plain_id_unchanged(self):
        plain = "some-plain-id-no-html"
        assert guideline_id_to_db(plain) == plain
        assert guideline_id_from_db(plain) == plain


# ---------------------------------------------------------------------------
# Guideline model tests
# ---------------------------------------------------------------------------


class TestGuidelineModel:
    def test_id_normalized_from_db_format(self):
        g = Guideline(
            id="python_design=html=naming",
            title="Naming",
            content="Use snake_case.",
        )
        assert g.id == "python_design.html#naming"

    def test_id_unchanged_when_already_web_format(self):
        g = Guideline(
            id="python_design.html#naming",
            title="Naming",
            content="Use snake_case.",
        )
        assert g.id == "python_design.html#naming"

    def test_related_guidelines_normalized(self):
        g = Guideline(
            id="python_design=html=naming",
            title="Naming",
            content="Content",
            related_guidelines=[
                "python_design=html=other-guideline",
                "general_implementation=html=pagination",
            ],
        )
        assert g.related_guidelines == [
            "python_design.html#other-guideline",
            "general_implementation.html#pagination",
        ]

    def test_related_guidelines_unchanged_when_web_format(self):
        g = Guideline(
            id="python_design.html#naming",
            title="Naming",
            content="Content",
            related_guidelines=["python_design.html#other"],
        )
        assert g.related_guidelines == ["python_design.html#other"]

    def test_model_dump_db_converts_to_db_format(self):
        g = Guideline(
            id="python_design.html#naming",
            title="Naming",
            content="Content",
            related_guidelines=[
                "python_design.html#other",
                "general_implementation.html#pagination",
            ],
        )
        data = g.model_dump_db()
        assert data["id"] == "python_design=html=naming"
        assert data["related_guidelines"] == [
            "python_design=html=other",
            "general_implementation=html=pagination",
        ]

    def test_model_dump_db_round_trip(self):
        """model_dump_db -> model_validate should reproduce the same model."""
        g = Guideline(
            id="python_design.html#naming",
            title="Naming",
            content="Content",
            related_guidelines=["python_design.html#other"],
        )
        db_dict = g.model_dump_db()
        g2 = Guideline.model_validate(db_dict)
        assert g2.id == g.id
        assert g2.related_guidelines == g.related_guidelines

    def test_model_validate_from_db_dict(self):
        """Simulates loading a document directly from Cosmos DB."""
        db_doc = {
            "id": "python_design=html=naming",
            "title": "Naming",
            "content": "Content",
            "related_guidelines": ["python_design=html=other"],
            "related_examples": [],
            "related_memories": [],
        }
        g = Guideline.model_validate(db_doc)
        assert g.id == "python_design.html#naming"
        assert g.related_guidelines == ["python_design.html#other"]

    def test_empty_related_guidelines(self):
        g = Guideline(id="x=html=y", title="T", content="C", related_guidelines=[])
        assert g.related_guidelines == []
        assert g.model_dump_db()["related_guidelines"] == []

    def test_none_related_guidelines_defaults_to_empty(self):
        g = Guideline(id="x=html=y", title="T", content="C", related_guidelines=None)
        assert g.related_guidelines == []


# ---------------------------------------------------------------------------
# Example model tests
# ---------------------------------------------------------------------------


class TestExampleModel:
    def test_guideline_ids_normalized_from_db_format(self):
        e = Example(
            id="ex-1",
            title="Example",
            content="code",
            example_type=ExampleType.GOOD,
            guideline_ids=["python_design=html=naming", "general_implementation=html=pagination"],
        )
        assert e.guideline_ids == [
            "python_design.html#naming",
            "general_implementation.html#pagination",
        ]

    def test_guideline_ids_unchanged_when_web_format(self):
        e = Example(
            id="ex-1",
            title="Example",
            content="code",
            example_type=ExampleType.GOOD,
            guideline_ids=["python_design.html#naming"],
        )
        assert e.guideline_ids == ["python_design.html#naming"]

    def test_model_dump_db_converts_guideline_ids(self):
        e = Example(
            id="ex-1",
            title="Example",
            content="code",
            example_type=ExampleType.GOOD,
            guideline_ids=["python_design.html#naming"],
        )
        data = e.model_dump_db()
        assert data["guideline_ids"] == ["python_design=html=naming"]
        # id is NOT a guideline id, should be unchanged
        assert data["id"] == "ex-1"

    def test_model_dump_db_round_trip(self):
        e = Example(
            id="ex-1",
            title="Example",
            content="code",
            example_type=ExampleType.GOOD,
            guideline_ids=["python_design.html#naming"],
        )
        db_dict = e.model_dump_db()
        e2 = Example.model_validate(db_dict)
        assert e2.guideline_ids == e.guideline_ids

    def test_model_validate_from_db_dict(self):
        db_doc = {
            "id": "ex-1",
            "title": "Example",
            "content": "code",
            "example_type": "good",
            "guideline_ids": ["python_design=html=naming"],
            "memory_ids": [],
        }
        e = Example.model_validate(db_doc)
        assert e.guideline_ids == ["python_design.html#naming"]

    def test_empty_guideline_ids(self):
        e = Example(id="ex-1", title="T", content="C", example_type=ExampleType.GOOD, guideline_ids=[])
        assert e.guideline_ids == []
        assert e.model_dump_db()["guideline_ids"] == []

    def test_none_guideline_ids_defaults_to_empty(self):
        e = Example(id="ex-1", title="T", content="C", example_type=ExampleType.GOOD, guideline_ids=None)
        assert e.guideline_ids == []


# ---------------------------------------------------------------------------
# Memory model tests
# ---------------------------------------------------------------------------


class TestMemoryModel:
    def test_related_guidelines_normalized_from_db_format(self):
        m = Memory(
            id="mem-1",
            title="Memory",
            content="Content",
            source="manual",
            related_guidelines=["python_design=html=naming"],
        )
        assert m.related_guidelines == ["python_design.html#naming"]

    def test_related_guidelines_unchanged_when_web_format(self):
        m = Memory(
            id="mem-1",
            title="Memory",
            content="Content",
            source="manual",
            related_guidelines=["python_design.html#naming"],
        )
        assert m.related_guidelines == ["python_design.html#naming"]

    def test_model_dump_db_converts_related_guidelines(self):
        m = Memory(
            id="mem-1",
            title="Memory",
            content="Content",
            source="manual",
            related_guidelines=["python_design.html#naming", "general_implementation.html#pagination"],
        )
        data = m.model_dump_db()
        assert data["related_guidelines"] == [
            "python_design=html=naming",
            "general_implementation=html=pagination",
        ]
        assert data["id"] == "mem-1"

    def test_model_dump_db_round_trip(self):
        m = Memory(
            id="mem-1",
            title="Memory",
            content="Content",
            source="manual",
            related_guidelines=["python_design.html#naming"],
        )
        db_dict = m.model_dump_db()
        m2 = Memory.model_validate(db_dict)
        assert m2.related_guidelines == m.related_guidelines

    def test_model_validate_from_db_dict(self):
        db_doc = {
            "id": "mem-1",
            "title": "Memory",
            "content": "Content",
            "source": "manual",
            "related_guidelines": ["python_design=html=naming"],
            "related_examples": [],
            "related_memories": [],
        }
        m = Memory.model_validate(db_doc)
        assert m.related_guidelines == ["python_design.html#naming"]

    def test_empty_related_guidelines(self):
        m = Memory(id="mem-1", title="T", content="C", source="manual", related_guidelines=[])
        assert m.related_guidelines == []
        assert m.model_dump_db()["related_guidelines"] == []

    def test_none_related_guidelines_defaults_to_empty(self):
        m = Memory(id="mem-1", title="T", content="C", source="manual", related_guidelines=None)
        assert m.related_guidelines == []


# ---------------------------------------------------------------------------
# ReviewResult allowed_ids conversion test
# ---------------------------------------------------------------------------


class TestReviewResultAllowedIds:
    """Ensure ReviewResult normalizes allowed_ids from DB format."""

    def _make_section(self):
        class Line:
            def __init__(self, line, line_no):
                self.line = line
                self.line_no = line_no

        class Section:
            def __init__(self):
                self.lines = [Line("bad_code", 1)]

            def idx_for_line_no(self, line_no):
                for idx, l in enumerate(self.lines):
                    if l.line_no == line_no:
                        return idx
                return None

        return Section()

    def test_allowed_ids_converted_from_db_format(self):
        section = self._make_section()
        comments = [
            {
                "guideline_ids": ["python_design.html#naming"],
                "line_no": 1,
                "bad_code": "bad_code",
                "suggestion": "fix",
                "comment": "msg",
            }
        ]
        rr = ReviewResult(
            comments=comments,
            allowed_ids=["python_design=html=naming"],
            section=section,
        )
        # The comment's guideline_id should survive filtering because allowed_ids
        # were converted from DB format to web format internally
        assert rr.comments[0].guideline_ids == ["python_design.html#naming"]

    def test_allowed_ids_already_web_format(self):
        section = self._make_section()
        comments = [
            {
                "guideline_ids": ["python_design.html#naming"],
                "line_no": 1,
                "bad_code": "bad_code",
                "suggestion": "fix",
                "comment": "msg",
            }
        ]
        rr = ReviewResult(
            comments=comments,
            allowed_ids=["python_design.html#naming"],
            section=section,
        )
        assert rr.comments[0].guideline_ids == ["python_design.html#naming"]


# ---------------------------------------------------------------------------
# Database layer dict-payload write path tests
# ---------------------------------------------------------------------------


class TestGuidelinesContainerIdPreprocessing:
    """Ensures web-format IDs in plain dict payloads are converted to DB format."""

    def _make_container(self):
        """Create a GuidelinesContainer with a mocked Cosmos client."""
        manager = MagicMock()
        container = GuidelinesContainer(manager, "guidelines")
        container.client = MagicMock()
        # Simulate item not found for create
        container.client.read_item.side_effect = _cosmos_exc.CosmosResourceNotFoundError(
            status_code=404, message="Not found"
        )
        return container

    def test_create_with_dict_converts_id_to_db_format(self):
        container = self._make_container()
        data = {
            "id": "python_design.html#naming",
            "title": "Naming",
            "content": "Content",
        }
        container.create("python_design.html#naming", data=data, run_indexer=False)
        created_body = container.client.create_item.call_args[1]["body"]
        assert created_body["id"] == "python_design=html=naming"

    def test_create_with_model_converts_id_to_db_format(self):
        container = self._make_container()
        g = Guideline(
            id="python_design.html#naming",
            title="Naming",
            content="Content",
        )
        container.create("python_design.html#naming", data=g, run_indexer=False)
        created_body = container.client.create_item.call_args[1]["body"]
        assert created_body["id"] == "python_design=html=naming"

    def test_upsert_with_dict_converts_id_to_db_format(self):
        container = self._make_container()
        data = {
            "id": "python_design.html#naming",
            "title": "Naming",
            "content": "Content",
        }
        container.upsert("python_design.html#naming", data=data, run_indexer=False)
        upserted_body = container.client.upsert_item.call_args[0][0]
        assert upserted_body["id"] == "python_design=html=naming"

    def test_create_without_id_in_dict_uses_preprocessed_item_id(self):
        container = self._make_container()
        data = {"title": "Naming", "content": "Content"}
        container.create("python_design.html#naming", data=data, run_indexer=False)
        created_body = container.client.create_item.call_args[1]["body"]
        assert created_body["id"] == "python_design=html=naming"

    def test_basic_container_without_preprocess_id_passes_through(self):
        """BasicContainer (no preprocess_id) should not alter dict IDs."""
        manager = MagicMock()
        container = BasicContainer(manager, "examples")
        container.client = MagicMock()
        container.client.read_item.side_effect = _cosmos_exc.CosmosResourceNotFoundError(
            status_code=404, message="Not found"
        )
        data = {"id": "ex-1", "title": "Example", "content": "code"}
        container.create("ex-1", data=data, run_indexer=False)
        created_body = container.client.create_item.call_args[1]["body"]
        assert created_body["id"] == "ex-1"
