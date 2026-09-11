from __future__ import annotations

import logging
import shutil
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from azure.core.credentials import TokenCredential
from azure.search.documents import SearchClient

from .azure_search_target import submit_search_batches
from .models import BuilderSettings
from .plan import repository_key
from .storage import BlobCatalogStore

logger = logging.getLogger(__name__)

_ROLLBACK_AGE = timedelta(days=7)
_UNPUBLISHED_AGE = timedelta(hours=24)
_MANIFEST_AGE = timedelta(days=90)


def collect_garbage(
    settings: BuilderSettings,
    credential: TokenCredential,
) -> None:
    store = BlobCatalogStore(
        settings.storage_endpoint, settings.storage_container, credential
    )
    client = SearchClient(
        settings.search_endpoint,
        settings.search_index_name,
        credential=credential,
    )
    try:
        catalog = store.load_catalog().value
        active = {
            repository_key(item["git_url"], item["git_ref"]): item
            for item in catalog.get("repositories", [])
        }
        for key in sorted(store.list_repository_keys() | active.keys()):
            manifests = store.list_generation_records("manifests", key)
            staging = store.list_generation_records("staging", key)
            if key in active:
                store.clear_removal_marker(key)
                _clean_active_repository(
                    client,
                    store,
                    settings.work_root,
                    key,
                    active[key],
                    manifests,
                    staging,
                )
            else:
                _clean_removed_repository(client, store, key, manifests, staging)
    finally:
        client.close()
        store.close()


def _clean_active_repository(
    client: SearchClient,
    store: BlobCatalogStore,
    work_root: Path,
    key: str,
    catalog_entry: dict[str, Any],
    manifests: dict[int, dict[str, Any]],
    staging: dict[int, dict[str, Any]],
) -> None:
    now = datetime.now(timezone.utc)
    active_generation = int(catalog_entry["active_generation"])
    if active_generation not in manifests:
        raise RuntimeError(
            f"active manifest is missing for {key} generation {active_generation}"
        )
    published = sorted(
        generation for generation in manifests if generation <= active_generation
    )
    retained = {active_generation, *published[-3:-1]}
    retained.update(
        generation
        for generation in published
        if now - _timestamp(manifests[generation]["indexed_at"]) < _ROLLBACK_AGE
    )
    rollback_floor = min(retained)
    pair_filter = _pair_filter(catalog_entry["git_url"], catalog_entry["git_ref"])
    _delete_matching(
        client,
        f"{pair_filter} and valid_to_generation ne 0 "
        f"and valid_to_generation le {rollback_floor}",
    )

    for generation, manifest in manifests.items():
        if generation < rollback_floor:
            store.delete_generation_artifacts(
                key, generation, delete_manifest=False
            )
        if (
            generation not in retained
            and now - _timestamp(manifest["indexed_at"]) >= _MANIFEST_AGE
        ):
            store.delete_generation_artifacts(key, generation, delete_manifest=True)

    for generation, reservation in staging.items():
        if generation <= active_generation:
            store.delete_staging_record(key, generation)
            continue
        if now - _timestamp(reservation["created_at"]) < _UNPUBLISHED_AGE:
            continue
        _abandon_generation(client, reservation, generation)
        store.delete_generation_artifacts(key, generation, delete_manifest=True)
        state_dir = work_root / "states" / key
        if state_dir.exists():
            shutil.rmtree(state_dir)


def _clean_removed_repository(
    client: SearchClient,
    store: BlobCatalogStore,
    key: str,
    manifests: dict[int, dict[str, Any]],
    staging: dict[int, dict[str, Any]],
) -> None:
    latest = (
        manifests[max(manifests)]
        if manifests
        else staging[max(staging)]
        if staging
        else None
    )
    if latest is None:
        store.delete_repository_artifacts(key)
        return
    git_url = str(latest.get("git_url") or "")
    git_ref = str(latest.get("git_ref") or "")
    if not git_url or not git_ref:
        logger.warning("cannot identify removed repository for cache key %s", key)
        return
    marker = store.removal_marker(key, git_url, git_ref)
    if datetime.now(timezone.utc) - _timestamp(marker["removed_at"]) < _ROLLBACK_AGE:
        return
    _delete_matching(client, _pair_filter(git_url, git_ref))
    store.delete_repository_artifacts(key)


def _abandon_generation(
    client: SearchClient,
    reservation: dict[str, Any],
    generation: int,
) -> None:
    pair_filter = _pair_filter(reservation["git_url"], reservation["git_ref"])
    _merge_matching(
        client,
        f"{pair_filter} and valid_to_generation eq {generation} "
        f"and valid_from_generation lt {generation}",
        {"valid_to_generation": 0},
    )
    _delete_matching(
        client,
        f"{pair_filter} and valid_from_generation eq {generation}",
    )


def _delete_matching(client: SearchClient, filter_value: str) -> None:
    documents = [
        {"chunk_id": result["chunk_id"]}
        for result in client.search(
            search_text="*",
            filter=filter_value,
            select=["chunk_id"],
        )
    ]
    if documents:
        submit_search_batches(client, "delete", documents)


def _merge_matching(
    client: SearchClient,
    filter_value: str,
    values: dict[str, Any],
) -> None:
    documents = [
        {"chunk_id": result["chunk_id"], **values}
        for result in client.search(
            search_text="*",
            filter=filter_value,
            select=["chunk_id"],
        )
    ]
    if documents:
        submit_search_batches(client, "merge", documents)


def _pair_filter(git_url: str, git_ref: str) -> str:
    return (
        f"git_url eq '{_escape(git_url)}' and "
        f"git_ref eq '{_escape(git_ref)}'"
    )


def _timestamp(value: Any) -> datetime:
    timestamp = datetime.fromisoformat(str(value))
    return timestamp if timestamp.tzinfo else timestamp.replace(tzinfo=timezone.utc)


def _escape(value: str) -> str:
    return value.replace("'", "''")
