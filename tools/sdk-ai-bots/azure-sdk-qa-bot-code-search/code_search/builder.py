from __future__ import annotations

import asyncio
import gc
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import cocoindex as coco
from azure.identity import DefaultAzureCredential
from azure.identity.aio import DefaultAzureCredential as AsyncDefaultAzureCredential
from azure.identity.aio import get_bearer_token_provider as get_async_token_provider
from azure.search.documents import SearchClient
from openai import AsyncAzureOpenAI

from . import CHUNKER_VERSION, CODE_INDEX_SCHEMA_VERSION
from .azure_search_target import (
    SEARCH_CLIENT,
    TARGET_STATS,
    RepositoryTargetSpec,
    TargetStats,
)
from .chunking import PARSER_FAILURES, TYPESPEC_POOL, ParserFailureCollector
from .embeddings import EMBEDDING_CONFIG, EMBEDDING_LIMITER, OPENAI_CLIENT
from .git_workspace import prepare_workspace, resolve_ref
from .index_app import WORKSPACE_ROOT, index_repository
from .models import (
    BuilderSettings,
    GenerationManifest,
    RepositoryIndexConfig,
)
from .path_matcher import RepositoryPathMatcher
from .plan import aggregate_repository_demands
from .storage import BlobCatalogStore, StateCache
from .tenant_source import load_tenant_repository_configs
from .typespec_pool import TypeSpecWorkerPool
from .validation import validate_generation

logger = logging.getLogger(__name__)


async def build_all(
    settings: BuilderSettings,
    credential: DefaultAzureCredential,
) -> None:
    configs = aggregate_repository_demands(
        load_tenant_repository_configs(), settings
    )
    store = BlobCatalogStore(
        settings.storage_endpoint, settings.storage_container, credential
    )
    snapshot = await asyncio.to_thread(store.load_catalog)
    current_entries = {
        (entry["git_url"], entry["git_ref"]): entry
        for entry in snapshot.value.get("repositories", [])
    }
    new_entries: list[dict[str, Any]] = []
    staged_caches: list[tuple[StateCache, int]] = []

    try:
        async with AsyncDefaultAzureCredential(process_timeout=60) as async_credential:
            for config in configs:
                current = current_entries.get((config.git_url, config.git_ref))
                entry, state_cache = await _build_repository(
                    settings,
                    credential,
                    async_credential,
                    store,
                    config,
                    current,
                )
                new_entries.append(entry)
                if state_cache is not None:
                    staged_caches.append((state_cache, int(entry["active_generation"])))

        catalog = {
            "schema_version": CODE_INDEX_SCHEMA_VERSION,
            "tenants": _tenant_catalog(configs),
            "repositories": sorted(
                new_entries, key=lambda item: (item["git_url"], item["git_ref"])
            ),
        }
        if catalog != snapshot.value:
            await asyncio.to_thread(store.publish_catalog, catalog, snapshot.etag)
        for state_cache, generation in staged_caches:
            state_cache.mark_active(generation)
    finally:
        store.close()


async def _build_repository(
    settings: BuilderSettings,
    credential: DefaultAzureCredential,
    async_credential: AsyncDefaultAzureCredential,
    store: BlobCatalogStore,
    config: RepositoryIndexConfig,
    current: dict[str, Any] | None,
) -> tuple[dict[str, Any], StateCache | None]:
    resolved_commit = await asyncio.to_thread(
        resolve_ref, config.git_url, config.git_ref
    )
    active_generation = int(current["active_generation"]) if current else 0
    active_manifest = (
        await asyncio.to_thread(
            store.read_manifest,
            f"manifests/{config.repository_key}/{active_generation}.json",
        )
        if current
        else None
    )
    if (
        current
        and current.get("resolved_commit_sha") == resolved_commit
        and active_manifest
        and active_manifest.get("index_identity") == config.index_identity
        and active_manifest.get("search_index_name") == settings.search_index_name
    ):
        logger.info(
            "skipping unchanged repository %s %s at %s",
            config.git_url,
            config.git_ref,
            resolved_commit,
        )
        return current, None

    generation = active_generation + 1
    build_identity = f"{resolved_commit}:{config.index_identity}"
    await asyncio.to_thread(
        store.reserve_generation,
        config.repository_key,
        generation,
        build_identity,
        resolved_commit,
        config.git_url,
        config.git_ref,
    )
    staged_manifest = await asyncio.to_thread(
        store.try_read_manifest, config.repository_key, generation
    )
    if staged_manifest is not None:
        manifest_blob, manifest_uri, manifest = staged_manifest
        if (
            manifest.get("resolved_commit_sha") != resolved_commit
            or manifest.get("index_identity") != config.index_identity
        ):
            raise RuntimeError(
                f"staged generation {generation} does not match the current build"
            )
        validation_client = SearchClient(
            settings.search_endpoint,
            settings.search_index_name,
            credential=credential,
        )
        try:
            await asyncio.to_thread(
                validate_generation,
                validation_client,
                config.git_url,
                config.git_ref,
                generation,
                settings.validation_timeout_seconds,
                int(manifest.get("documents_added", 0))
                + int(manifest.get("documents_updated", 0)),
                int(manifest.get("documents_closed", 0)),
            )
        finally:
            validation_client.close()
        logger.info(
            "reusing complete unpublished generation %d for %s %s",
            generation,
            config.git_url,
            config.git_ref,
        )
        return _repository_entry(manifest_blob, manifest_uri, manifest), None

    snapshot = await asyncio.to_thread(
        prepare_workspace, config, settings.work_root, resolved_commit
    )
    state_cache = StateCache(settings.work_root, config.repository_key)
    await asyncio.to_thread(
        state_cache.prepare,
        store,
        active_generation,
        generation,
        build_identity,
        str(active_manifest.get("state_database_sha256", ""))
        if active_manifest
        else "",
    )

    search_client = SearchClient(
        settings.search_endpoint,
        settings.search_index_name,
        credential=credential,
    )
    token_provider = get_async_token_provider(
        async_credential, "https://cognitiveservices.azure.com/.default"
    )
    openai_client = AsyncAzureOpenAI(
        azure_endpoint=settings.embedding.endpoint,
        azure_deployment=settings.embedding.deployment,
        api_version=settings.embedding.api_version,
        azure_ad_token_provider=token_provider,
    )
    parser_failures = ParserFailureCollector()
    target_stats = TargetStats()
    typespec_pool = TypeSpecWorkerPool()
    context = coco.ContextProvider()
    context.provide(WORKSPACE_ROOT, snapshot.workspace)
    context.provide(SEARCH_CLIENT, search_client)
    context.provide(TARGET_STATS, target_stats)
    context.provide(TYPESPEC_POOL, typespec_pool)
    context.provide(PARSER_FAILURES, parser_failures)
    context.provide(OPENAI_CLIENT, openai_client)
    context.provide(EMBEDDING_CONFIG, settings.embedding)
    context.provide(
        EMBEDDING_LIMITER, asyncio.Semaphore(settings.embedding_concurrency)
    )
    environment = coco.Environment(
        coco.Settings(db_path=state_cache.db_path),
        name=config.repository_key,
        context_provider=context,
        event_loop=asyncio.get_running_loop(),
    )
    target_spec = RepositoryTargetSpec(
        git_url=config.git_url,
        git_ref=config.git_ref,
        generation=generation,
        search_endpoint=settings.search_endpoint,
        search_index_name=settings.search_index_name,
    )
    app = coco.App(
        coco.AppConfig(
            name=f"azure-sdk-code-{config.repository_key}",
            environment=environment,
        ),
        index_repository,
        config,
        target_spec,
    )
    try:
        await app.update(
            full_reprocess=bool(
                active_manifest
                and active_manifest.get("index_identity") != config.index_identity
            )
        )
    finally:
        typespec_pool.close()
        await openai_client.close()
        search_client.close()
    del app, environment, context
    gc.collect()
    documents_added = max(
        target_stats.documents_added, state_cache.prior_documents_added
    )
    documents_updated = max(
        target_stats.documents_updated, state_cache.prior_documents_updated
    )
    documents_closed = max(
        target_stats.documents_closed, state_cache.prior_documents_closed
    )
    state_cache.mark_staged(
        generation, documents_added, documents_updated, documents_closed
    )

    validation_client = SearchClient(
        settings.search_endpoint,
        settings.search_index_name,
        credential=credential,
    )
    try:
        validation = await asyncio.to_thread(
            validate_generation,
            validation_client,
            config.git_url,
            config.git_ref,
            generation,
            settings.validation_timeout_seconds,
            documents_added + documents_updated,
            documents_closed,
        )
    finally:
        validation_client.close()
    file_count = await asyncio.to_thread(_count_files, snapshot.workspace, config)
    archive, state_checksum = await asyncio.to_thread(state_cache.archive, generation)
    try:
        state_checksum = await asyncio.to_thread(
            store.upload_state_archive,
            config.repository_key,
            generation,
            archive,
            state_checksum,
        )
    finally:
        archive.unlink(missing_ok=True)

    manifest = GenerationManifest(
        schema_version=1,
        git_url=config.git_url,
        git_ref=config.git_ref,
        previous_commit_sha=(
            str(current.get("resolved_commit_sha", "")) if current else ""
        ),
        resolved_commit_sha=snapshot.commit_sha,
        tree_sha=snapshot.tree_sha,
        generation=generation,
        indexed_at=datetime.now(timezone.utc).isoformat(),
        cocoindex_version=coco.__version__,
        code_index_schema_version=CODE_INDEX_SCHEMA_VERSION,
        search_index_alias=settings.search_alias,
        search_index_name=settings.search_index_name,
        embedding_provider="azure-openai",
        embedding_model=settings.embedding.model,
        embedding_deployment=settings.embedding.deployment,
        embedding_dimensions=settings.embedding.dimensions,
        chunker_version=CHUNKER_VERSION,
        index_identity=config.index_identity,
        files=file_count,
        chunks=validation.chunks,
        languages=validation.languages,
        parser_failures=parser_failures.snapshot(),
        documents_added=documents_added,
        documents_updated=documents_updated,
        documents_closed=documents_closed,
        git_cache_sha256=snapshot.cache_sha256,
        state_database_sha256=state_checksum,
    )
    manifest_blob, manifest_uri, persisted_manifest = await asyncio.to_thread(
        store.upload_manifest,
        config.repository_key,
        generation,
        manifest.to_dict(),
    )
    return (
        _repository_entry(manifest_blob, manifest_uri, persisted_manifest),
        state_cache,
    )


def _tenant_catalog(
    configs: list[RepositoryIndexConfig],
) -> dict[str, list[dict[str, Any]]]:
    tenants: dict[str, list[dict[str, Any]]] = {}
    for config in configs:
        for tenant, prefixes in config.tenant_path_prefixes:
            tenants.setdefault(tenant, []).append(
                {
                    "git_url": config.git_url,
                    "git_ref": config.git_ref,
                    "path_prefixes": list(prefixes),
                }
            )
    for entries in tenants.values():
        entries.sort(key=lambda item: (item["git_url"], item["git_ref"]))
    return dict(sorted(tenants.items()))


def _count_files(workspace: Path, config: RepositoryIndexConfig) -> int:
    matcher = RepositoryPathMatcher(
        workspace,
        config.include_patterns,
        config.exclude,
        config.max_file_bytes,
    )
    count = 0
    for path in workspace.rglob("*"):
        if path.is_file() and matcher.is_file_included(path.relative_to(workspace)):
            count += 1
    return count


def _repository_entry(
    _manifest_blob: str, manifest_uri: str, manifest: dict[str, Any]
) -> dict[str, Any]:
    return {
        "git_url": manifest["git_url"],
        "git_ref": manifest["git_ref"],
        "active_generation": int(manifest["generation"]),
        "resolved_commit_sha": manifest["resolved_commit_sha"],
        "indexed_at": manifest["indexed_at"],
        "manifest_uri": manifest_uri,
    }
