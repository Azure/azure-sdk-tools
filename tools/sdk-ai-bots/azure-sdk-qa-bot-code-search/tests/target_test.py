from __future__ import annotations

import cocoindex as coco
from cocoindex.connectorkits.fingerprint import fingerprint_object

from code_search.azure_search_target import (
    AzureSearchDocumentHandler,
    DocumentTrackingRecord,
)
from code_search.models import CodeSearchDocument


def _document(chunk_id: str, generation: int, content: str) -> CodeSearchDocument:
    return CodeSearchDocument(
        chunk_id=chunk_id,
        git_url="https://github.com/Azure/example.git",
        git_ref="refs/heads/main",
        valid_from_generation=generation,
        valid_to_generation=0,
        path="src/example.py",
        path_prefixes=("src",),
        language="python",
        artifact_type="source",
        content=content,
        content_vector=(0.1, 0.2),
        content_hash=content,
        start_line=1,
        end_line=2,
        symbol_name="example",
        symbol_kind="function",
        identifiers=("example",),
        parser="recursive",
    )


def test_reconcile_unchanged_change_and_delete() -> None:
    handler = AzureSearchDocumentHandler(generation=2)
    old = _document("old-id", 1, "old")
    old_record = DocumentTrackingRecord(
        fingerprint=fingerprint_object(old.target_identity()),
        current_document_id=old.chunk_id,
        valid_from_generation=1,
    )

    assert handler.reconcile("logical", old, [old_record], False) is None

    duplicate_record = DocumentTrackingRecord(
        fingerprint=old_record.fingerprint,
        current_document_id="duplicate-id",
        valid_from_generation=2,
    )
    repair_output = handler.reconcile(
        "logical", old, [old_record, duplicate_record], True
    )
    assert repair_output is not None
    assert repair_output.action.closes == ("duplicate-id",)
    assert repair_output.action.upsert is not None
    assert repair_output.action.upsert.chunk_id == "old-id"

    changed = _document("new-id", 2, "new")
    changed_output = handler.reconcile("logical", changed, [old_record], False)
    assert changed_output is not None
    assert changed_output.action.closes == ("old-id",)
    assert changed_output.action.upsert == changed
    assert changed_output.tracking_record.current_document_id == "new-id"

    deleted_output = handler.reconcile(
        "logical", coco.NON_EXISTENCE, [old_record], False
    )
    assert deleted_output is not None
    assert deleted_output.action.closes == ("old-id",)
    assert deleted_output.action.upsert is None
    assert coco.is_non_existence(deleted_output.tracking_record)
