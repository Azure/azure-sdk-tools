from __future__ import annotations

import hashlib
from pathlib import Path

import cocoindex as coco
from cocoindex.connectors import localfs

from .azure_search_target import (
    AzureSearchTarget,
    RepositoryTargetSpec,
    repository_target,
)
from .chunking import PARSER_FAILURES, prepare_file
from .embeddings import embed_content
from .models import CodeSearchDocument, RepositoryIndexConfig
from .path_matcher import RepositoryPathMatcher

WORKSPACE_ROOT = coco.ContextKey[Path]("azure-sdk-code-workspace-root")


async def index_repository(
    config: RepositoryIndexConfig,
    target_spec: RepositoryTargetSpec,
) -> None:
    child_provider = await coco.mount_target(
        repository_target((config.git_url, config.git_ref), target_spec)
    )
    target = AzureSearchTarget(child_provider, target_spec)
    workspace = coco.use_context(WORKSPACE_ROOT)
    matcher = RepositoryPathMatcher(
        workspace,
        config.include_patterns,
        config.exclude,
        config.max_file_bytes,
    )
    files = localfs.walk_dir(
        localfs.FilePath(base_dir=WORKSPACE_ROOT),
        recursive=True,
        path_matcher=matcher,
    )
    handle = await coco.mount_each(_process_file, files.items(), target)
    await handle.ready()


async def _process_file(
    file: localfs.File,
    target: AzureSearchTarget,
) -> None:
    chunks = await prepare_file(file)
    recorded_failures: set[str] = set()
    for chunk in chunks:
        if (
            chunk.parser_failure is not None
            and chunk.parser_failure not in recorded_failures
        ):
            coco.use_context(PARSER_FAILURES).add(chunk.path, chunk.parser_failure)
            recorded_failures.add(chunk.parser_failure)
        vector = await embed_content(chunk.content)
        chunk_id = hashlib.sha256(
            (
                f"{target.git_url}\0{target.git_ref}\0{chunk.logical_id}\0"
                f"{chunk.content_hash}\0{target.generation}"
            ).encode("utf-8")
        ).hexdigest()
        target.declare_document(
            chunk.logical_id,
            CodeSearchDocument(
                chunk_id=chunk_id,
                git_url=target.git_url,
                git_ref=target.git_ref,
                valid_from_generation=target.generation,
                valid_to_generation=0,
                path=chunk.path,
                path_prefixes=chunk.path_prefixes,
                language=chunk.language,
                artifact_type=chunk.artifact_type,
                content=chunk.content,
                content_vector=vector,
                content_hash=chunk.content_hash,
                start_line=chunk.start_line,
                end_line=chunk.end_line,
                symbol_name=chunk.symbol_name,
                symbol_kind=chunk.symbol_kind,
                identifiers=chunk.identifiers,
                parser=chunk.parser,
            ),
        )
