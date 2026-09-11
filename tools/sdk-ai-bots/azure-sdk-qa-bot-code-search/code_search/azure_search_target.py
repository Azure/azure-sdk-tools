from __future__ import annotations

import asyncio
import json
import threading
import time
from collections.abc import Collection, Sequence
from dataclasses import dataclass, replace
from typing import Literal, NamedTuple, cast

import cocoindex as coco
from azure.search.documents import SearchClient
from cocoindex.connectorkits.fingerprint import fingerprint_object

from .models import CodeSearchDocument

SEARCH_CLIENT = coco.ContextKey[SearchClient]("azure-sdk-code-search-client")
TARGET_STATS = coco.ContextKey["TargetStats"]("azure-sdk-code-target-stats")

_TRANSIENT_STATUS_CODES = {408, 429, 500, 502, 503, 504}
_MAX_BATCH_DOCUMENTS = 500
_MAX_BATCH_BYTES = 12 * 1024 * 1024
SearchOperation = Literal["merge", "upsert", "delete"]


class TargetStats:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self.documents_added = 0
        self.documents_updated = 0
        self.documents_closed = 0

    def record(self, added: int, updated: int, closed: int) -> None:
        with self._lock:
            self.documents_added += added
            self.documents_updated += updated
            self.documents_closed += closed


@dataclass(frozen=True, slots=True)
class RepositoryTargetSpec:
    git_url: str
    git_ref: str
    generation: int
    search_endpoint: str
    search_index_name: str


class _RepositoryTrackingRecord(NamedTuple):
    target_fingerprint: bytes


class DocumentTrackingRecord(NamedTuple):
    fingerprint: bytes
    current_document_id: str
    valid_from_generation: int


class DocumentAction(NamedTuple):
    closes: tuple[str, ...]
    upsert: CodeSearchDocument | None
    kind: Literal["new", "change", "delete", "repair"]


class _RepositoryAction(NamedTuple):
    spec: RepositoryTargetSpec | None


class AzureSearchDocumentHandler(
    coco.TargetHandler[CodeSearchDocument, DocumentTrackingRecord]
):
    def __init__(self, generation: int) -> None:
        self._generation = generation
        self._sink = coco.TargetActionSink.from_async_fn(self._apply_actions)

    def reconcile(
        self,
        key: coco.StableKey,
        desired: CodeSearchDocument | coco.NonExistenceType,
        prev_possible_records: Collection[DocumentTrackingRecord],
        prev_may_be_missing: bool,
        /,
    ) -> (
        coco.TargetReconcileOutput[DocumentAction, DocumentTrackingRecord] | None
    ):
        if coco.is_non_existence(desired):
            if not prev_possible_records and not prev_may_be_missing:
                return None
            return coco.TargetReconcileOutput(
                action=DocumentAction(
                    closes=tuple(
                        sorted(
                            {
                                record.current_document_id
                                for record in prev_possible_records
                            }
                        )
                    ),
                    upsert=None,
                    kind="delete",
                ),
                sink=self._sink,
                tracking_record=coco.NON_EXISTENCE,
            )

        target_fingerprint = fingerprint_object(desired.target_identity())
        if prev_possible_records and all(
            record.fingerprint == target_fingerprint
            for record in prev_possible_records
        ):
            if not prev_may_be_missing:
                return None
            current = min(
                prev_possible_records,
                key=lambda record: record.valid_from_generation,
            )
            closes = tuple(
                sorted(
                    {
                        record.current_document_id
                        for record in prev_possible_records
                        if record.current_document_id != current.current_document_id
                    }
                )
            )
            repair_document = replace(
                desired,
                chunk_id=current.current_document_id,
                valid_from_generation=current.valid_from_generation,
            )
            return coco.TargetReconcileOutput(
                action=DocumentAction(closes, repair_document, "repair"),
                sink=self._sink,
                tracking_record=current,
            )

        closes = tuple(
            sorted(
                {
                    record.current_document_id
                    for record in prev_possible_records
                    if record.current_document_id != desired.chunk_id
                }
            )
        )
        return coco.TargetReconcileOutput(
            action=DocumentAction(
                closes=closes,
                upsert=desired,
                kind="change" if prev_possible_records else "new",
            ),
            sink=self._sink,
            tracking_record=DocumentTrackingRecord(
                fingerprint=target_fingerprint,
                current_document_id=desired.chunk_id,
                valid_from_generation=desired.valid_from_generation,
            ),
        )

    async def _apply_actions(
        self,
        context_provider: coco.ContextProvider,
        actions: Sequence[DocumentAction],
        /,
    ) -> None:
        search_client = context_provider.get(SEARCH_CLIENT)
        close_documents = {
            document_id: {
                "chunk_id": document_id,
                "valid_to_generation": self._generation,
            }
            for action in actions
            for document_id in action.closes
        }
        upsert_documents = {
            action.upsert.chunk_id: action.upsert.to_search_document()
            for action in actions
            if action.upsert is not None
        }
        if close_documents:
            await asyncio.to_thread(
                submit_search_batches,
                search_client,
                "merge",
                list(close_documents.values()),
            )
        if upsert_documents:
            await asyncio.to_thread(
                submit_search_batches,
                search_client,
                "upsert",
                list(upsert_documents.values()),
            )

        stats = context_provider.get(TARGET_STATS)
        stats.record(
            added=sum(action.kind == "new" for action in actions),
            updated=sum(action.kind == "change" for action in actions),
            closed=len(close_documents),
        )

class _RepositoryHandler(
    coco.TargetHandler[
        RepositoryTargetSpec, _RepositoryTrackingRecord, AzureSearchDocumentHandler
    ]
):
    def __init__(self) -> None:
        self._sink = coco.TargetActionSink[
            _RepositoryAction, AzureSearchDocumentHandler
        ].from_fn(self._apply_actions)

    @staticmethod
    def _apply_actions(
        context_provider: coco.ContextProvider,
        actions: Sequence[_RepositoryAction],
        /,
    ) -> list[coco.ChildTargetDef[AzureSearchDocumentHandler] | None]:
        return [
            coco.ChildTargetDef(AzureSearchDocumentHandler(action.spec.generation))
            if action.spec is not None
            else None
            for action in actions
        ]

    def reconcile(
        self,
        key: coco.StableKey,
        desired: RepositoryTargetSpec | coco.NonExistenceType,
        prev_possible_records: Collection[_RepositoryTrackingRecord],
        prev_may_be_missing: bool,
        /,
    ) -> (
        coco.TargetReconcileOutput[
            _RepositoryAction,
            _RepositoryTrackingRecord,
            AzureSearchDocumentHandler,
        ]
        | None
    ):
        if coco.is_non_existence(desired):
            return coco.TargetReconcileOutput[
                _RepositoryAction,
                _RepositoryTrackingRecord,
                AzureSearchDocumentHandler,
            ](
                action=_RepositoryAction(None),
                sink=self._sink,
                tracking_record=cast(_RepositoryTrackingRecord, coco.NON_EXISTENCE),
                child_invalidation="lossy",
            )
        fingerprint = fingerprint_object(
            (desired.search_endpoint, desired.search_index_name)
        )
        destructive = bool(
            prev_possible_records
            and any(
                record.target_fingerprint != fingerprint
                for record in prev_possible_records
            )
        )
        return coco.TargetReconcileOutput[
            _RepositoryAction,
            _RepositoryTrackingRecord,
            AzureSearchDocumentHandler,
        ](
            action=_RepositoryAction(desired),
            sink=self._sink,
            tracking_record=_RepositoryTrackingRecord(fingerprint),
            child_invalidation="destructive" if destructive else None,
        )


_REPOSITORY_PROVIDER = coco.register_root_target_states_provider(
    "azure-sdk/ai-search/code-repository/v1", _RepositoryHandler()
)


class AzureSearchTarget:
    def __init__(
        self,
        provider: coco.TargetStateProvider[CodeSearchDocument, None],
        spec: RepositoryTargetSpec,
    ) -> None:
        self._provider = provider
        self.git_url = spec.git_url
        self.git_ref = spec.git_ref
        self.generation = spec.generation

    def declare_document(self, logical_id: str, document: CodeSearchDocument) -> None:
        coco.declare_target_state(self._provider.target_state(logical_id, document))

    def __coco_memo_key__(self) -> object:
        return (self._provider.memo_key, self.generation)


def repository_target(
    config_key: tuple[str, str], spec: RepositoryTargetSpec
) -> coco.TargetState[AzureSearchDocumentHandler]:
    return _REPOSITORY_PROVIDER.target_state(config_key, spec)


def _batches(documents: list[dict]) -> list[list[dict]]:
    batches: list[list[dict]] = []
    batch: list[dict] = []
    batch_bytes = 0
    for document in documents:
        document_bytes = len(
            json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode(
                "utf-8"
            )
        )
        if batch and (
            len(batch) >= _MAX_BATCH_DOCUMENTS
            or batch_bytes + document_bytes > _MAX_BATCH_BYTES
        ):
            batches.append(batch)
            batch = []
            batch_bytes = 0
        batch.append(document)
        batch_bytes += document_bytes
    if batch:
        batches.append(batch)
    return batches


def submit_search_batches(
    client: SearchClient,
    operation: SearchOperation,
    documents: list[dict],
) -> None:
    for batch in _batches(documents):
        _submit_search_batch(client, operation, batch)


def _submit_search_batch(
    client: SearchClient,
    operation: SearchOperation,
    documents: list[dict],
) -> None:
    pending = {str(document["chunk_id"]): document for document in documents}
    for attempt in range(4):
        payload = list(pending.values())
        if operation == "merge":
            results = client.merge_documents(payload)
        elif operation == "delete":
            results = client.delete_documents(payload)
        else:
            results = client.merge_or_upload_documents(payload)
        seen: set[str] = set()
        transient: dict[str, dict] = {}
        permanent: list[str] = []
        for result in results:
            key = str(result.key)
            seen.add(key)
            if result.succeeded:
                continue
            detail = (
                f"{key}: status={result.status_code}, "
                f"error={result.error_message or 'unknown'}"
            )
            if result.status_code in _TRANSIENT_STATUS_CODES and key in pending:
                transient[key] = pending[key]
            else:
                permanent.append(detail)
        for missing in pending.keys() - seen:
            permanent.append(f"{missing}: no IndexingResult returned")
        if permanent:
            raise RuntimeError(
                f"Azure AI Search {operation} failed: {'; '.join(permanent)}"
            )
        if not transient:
            return
        if attempt == 3:
            failed = ", ".join(sorted(transient))
            raise RuntimeError(
                f"Azure AI Search {operation} exhausted retries for: {failed}"
            )
        pending = transient
        time.sleep(2**attempt)
