"""Tenant-scoped hybrid retrieval from the published code index."""

from __future__ import annotations

import asyncio
import json
import re
import time
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import Any
from urllib.parse import quote, urlparse, urlunparse

from azure.core.exceptions import ResourceNotFoundError
from azure.search.documents.aio import SearchClient as AzureSearchClient
from azure.search.documents.models import QueryType, VectorizableTextQuery
from azure.storage.blob.aio import BlobServiceClient

from config.app_config import get as cfg
from config.tenant_config import TenantID, get_tenant_config
from models.code_search import (
    CodeReference,
    GitRepositoryRef,
    SearchIndexedCodeResult,
    UnavailableRepository,
)
from utils.azure_credential import get_credential
from utils.azure_storage import create_blob_service_client

_CATALOG_BLOB = "catalog.json"
_SEMANTIC_CONFIGURATION = "code-semantic"
_SELECT_FIELDS = [
    "chunk_id",
    "git_url",
    "git_ref",
    "path",
    "language",
    "artifact_type",
    "content",
    "content_hash",
    "start_line",
    "end_line",
    "symbol_name",
    "symbol_kind",
]
_RRF_K = 60
_COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")


@dataclass(frozen=True)
class _Binding:
    git_url: str
    git_ref: str
    path_prefixes: tuple[str, ...]


@dataclass(frozen=True)
class _Repository:
    git_url: str
    git_ref: str
    active_generation: int
    resolved_commit_sha: str
    indexed_at: str


@dataclass(frozen=True)
class _Catalog:
    tenants: dict[str, tuple[_Binding, ...]]
    repositories: dict[tuple[str, str], _Repository]


class CodeSearchClient:
    def __init__(
        self,
        settings: Callable[[str, str], str | None] = cfg,
        *,
        search_client: Any | None = None,
        blob_client: BlobServiceClient | None = None,
    ) -> None:
        self._settings = settings
        self._search_client = search_client
        self._blob_client = blob_client
        self._catalog: _Catalog | None = None
        self._catalog_expires_at = 0.0
        self._catalog_lock = asyncio.Lock()
        self._top_k = min(max(int(settings("CODE_INDEX_SEARCH_TOPK", "6") or "6"), 1), 10)
        self._candidate_top_k = min(
            max(
                int(settings("CODE_INDEX_SEARCH_CANDIDATE_TOPK", "24") or "24"),
                self._top_k,
            ),
            50,
        )
        self._content_limit = min(
            max(int(settings("CODE_INDEX_SEARCH_CONTENT_CHARS", "2400") or "2400"), 500),
            5000,
        )
        self._catalog_ttl = max(
            int(settings("CODE_INDEX_CATALOG_CACHE_SECONDS", "60") or "60"), 0
        )
        self._timeout_seconds = min(
            max(int(settings("CODE_INDEX_SEARCH_TIMEOUT_SECONDS", "20") or "20"), 1),
            60,
        )

    async def search(
        self,
        *,
        tenant_id: TenantID,
        queries: list[str],
        repositories: list[GitRepositoryRef] | None = None,
        languages: list[str] | None = None,
        path_prefixes: list[str] | None = None,
        top_k: int | None = None,
    ) -> SearchIndexedCodeResult:
        normalized_queries = _queries(queries)
        catalog = await self._load_catalog()
        tenant_config = get_tenant_config(tenant_id)
        if tenant_config is None:
            raise ValueError(f"Unknown tenant_id: {tenant_id.value}")

        bindings = catalog.tenants.get(tenant_id.value, ())
        selected, unavailable = _select_bindings(bindings, repositories)
        requested_paths = (
            tuple(_normalize_prefix(value) for value in path_prefixes)
            if path_prefixes
            else ()
        )
        search_filter, metadata, unpublished = _build_filter(
            selected,
            catalog.repositories,
            requested_paths,
            languages,
        )
        unavailable.extend(unpublished)
        if not search_filter:
            return SearchIndexedCodeResult(unavailable_repositories=unavailable)

        client = self._get_search_client()
        ranked_lists = await asyncio.gather(
            *[
                asyncio.wait_for(
                    self._search_one(client, query, search_filter),
                    timeout=self._timeout_seconds,
                )
                for query in normalized_queries
            ]
        )
        limit = min(max(top_k or self._top_k, 1), self._top_k)
        results = _rank_and_format(
            ranked_lists,
            metadata,
            limit,
            self._content_limit,
        )
        return SearchIndexedCodeResult(
            results=results,
            unavailable_repositories=unavailable,
        )

    async def _search_one(
        self, client: Any, query: str, search_filter: str
    ) -> list[dict[str, Any]]:
        results = await client.search(
            search_text=query,
            filter=search_filter,
            query_type=QueryType.SEMANTIC,
            semantic_configuration_name=_SEMANTIC_CONFIGURATION,
            vector_queries=[
                VectorizableTextQuery(
                    text=query,
                    k_nearest_neighbors=self._candidate_top_k,
                    fields="content_vector",
                )
            ],
            select=_SELECT_FIELDS,
            top=self._candidate_top_k,
        )
        return [dict(item) async for item in results]

    def _get_search_client(self) -> Any:
        if self._search_client is None:
            endpoint = (self._settings("AI_SEARCH_BASE_URL", "") or "").rstrip("/")
            index_name = self._settings(
                "CODE_INDEX_SEARCH_ALIAS", "azure-sdk-code"
            ) or "azure-sdk-code"
            if not endpoint:
                raise RuntimeError("AI_SEARCH_BASE_URL is not configured")
            self._search_client = AzureSearchClient(
                endpoint=endpoint,
                index_name=index_name,
                credential=get_credential(),
            )
        return self._search_client

    async def _load_catalog(self) -> _Catalog:
        if self._catalog is not None and time.monotonic() < self._catalog_expires_at:
            return self._catalog
        async with self._catalog_lock:
            if self._catalog is not None and time.monotonic() < self._catalog_expires_at:
                return self._catalog
            if self._blob_client is None:
                self._blob_client = create_blob_service_client(self._settings)
            container = self._settings(
                "STORAGE_CODE_INDEX_CONTAINER", "code-index"
            ) or "code-index"
            blob = self._blob_client.get_blob_client(
                container=container, blob=_CATALOG_BLOB
            )
            try:
                async with asyncio.timeout(self._timeout_seconds):
                    stream = await blob.download_blob()
                    value = json.loads((await stream.readall()).decode("utf-8"))
            except ResourceNotFoundError as exc:
                raise RuntimeError("code index catalog has not been published") from exc
            except (UnicodeDecodeError, json.JSONDecodeError) as exc:
                raise RuntimeError("code index catalog is not valid UTF-8 JSON") from exc
            catalog = _parse_catalog(value)
            self._catalog = catalog
            self._catalog_expires_at = time.monotonic() + self._catalog_ttl
            return catalog


def _queries(values: list[str]) -> tuple[str, ...]:
    if not 1 <= len(values) <= 3:
        raise ValueError("queries must contain between one and three values")
    output = tuple(value.strip() for value in values)
    if any(not value for value in output):
        raise ValueError("queries must not contain empty values")
    if any(len(value) > 1000 for value in output):
        raise ValueError("each query must be at most 1000 characters")
    return output


def _parse_catalog(value: Any) -> _Catalog:
    if not isinstance(value, dict) or value.get("schema_version") != 1:
        raise RuntimeError("unsupported code index catalog schema")
    raw_tenants = value.get("tenants")
    raw_repositories = value.get("repositories")
    if not isinstance(raw_tenants, dict) or not isinstance(raw_repositories, list):
        raise RuntimeError("code index catalog is missing tenant or repository data")

    tenants: dict[str, tuple[_Binding, ...]] = {}
    for tenant_id, entries in raw_tenants.items():
        if not isinstance(tenant_id, str) or not isinstance(entries, list):
            raise RuntimeError("code index catalog contains invalid tenant data")
        tenants[tenant_id] = tuple(_parse_binding(item) for item in entries)

    repositories: dict[tuple[str, str], _Repository] = {}
    for item in raw_repositories:
        if not isinstance(item, dict):
            raise RuntimeError("code index catalog contains invalid repository data")
        repository = _Repository(
            git_url=_required_string(item, "git_url"),
            git_ref=_required_string(item, "git_ref"),
            active_generation=int(item["active_generation"]),
            resolved_commit_sha=_required_string(item, "resolved_commit_sha"),
            indexed_at=_required_string(item, "indexed_at"),
        )
        if repository.active_generation < 1 or not _COMMIT_RE.fullmatch(
            repository.resolved_commit_sha
        ):
            raise RuntimeError("code index catalog contains invalid publication metadata")
        repositories[(repository.git_url, repository.git_ref)] = repository
    return _Catalog(tenants, repositories)


def _parse_binding(value: Any) -> _Binding:
    if not isinstance(value, dict):
        raise RuntimeError("code index catalog contains invalid tenant binding")
    prefixes = value.get("path_prefixes", [])
    if not isinstance(prefixes, list) or not all(
        isinstance(prefix, str) for prefix in prefixes
    ):
        raise RuntimeError("code index catalog contains invalid path prefixes")
    return _Binding(
        git_url=_required_string(value, "git_url"),
        git_ref=_required_string(value, "git_ref"),
        path_prefixes=tuple(_normalize_prefix(prefix) for prefix in prefixes if prefix),
    )


def _required_string(value: dict[str, Any], key: str) -> str:
    item = value.get(key)
    if not isinstance(item, str) or not item:
        raise RuntimeError(f"code index catalog field {key!r} is invalid")
    return item


def _select_bindings(
    allowed: tuple[_Binding, ...],
    requested: list[GitRepositoryRef] | None,
) -> tuple[tuple[_Binding, ...], list[UnavailableRepository]]:
    if not requested:
        return allowed, []
    allowed_map = {(item.git_url, item.git_ref): item for item in allowed}
    selected: list[_Binding] = []
    unavailable: list[UnavailableRepository] = []
    seen: set[tuple[str, str]] = set()
    for item in requested:
        repository = GitRepositoryRef(
            git_url=_canonical_git_url(item.git_url),
            git_ref=item.git_ref.strip(),
        )
        key = (repository.git_url, repository.git_ref)
        if key in seen:
            continue
        seen.add(key)
        binding = allowed_map.get(key)
        if binding is None:
            unavailable.append(
                UnavailableRepository(
                    repository=repository,
                    reason="repository/ref is not configured for this tenant",
                )
            )
        else:
            selected.append(binding)
    return tuple(selected), unavailable


def _build_filter(
    bindings: tuple[_Binding, ...],
    repositories: dict[tuple[str, str], _Repository],
    requested_paths: tuple[str, ...],
    languages: list[str] | None,
) -> tuple[
    str,
    dict[tuple[str, str], _Repository],
    list[UnavailableRepository],
]:
    clauses: list[str] = []
    metadata: dict[tuple[str, str], _Repository] = {}
    unavailable: list[UnavailableRepository] = []
    for binding in bindings:
        key = (binding.git_url, binding.git_ref)
        repository = repositories.get(key)
        if repository is None:
            unavailable.append(
                UnavailableRepository(
                    repository=GitRepositoryRef(
                        git_url=binding.git_url, git_ref=binding.git_ref
                    ),
                    reason="repository/ref has no published generation",
                )
            )
            continue
        effective_paths = _intersect_prefixes(
            binding.path_prefixes, requested_paths
        )
        if requested_paths and effective_paths is None:
            continue
        generation = repository.active_generation
        clause = (
            f"git_url eq '{_escape(binding.git_url)}' and "
            f"git_ref eq '{_escape(binding.git_ref)}' and "
            f"valid_from_generation le {generation} and "
            f"(valid_to_generation eq 0 or valid_to_generation gt {generation})"
        )
        if effective_paths:
            path_clauses = [
                f"(path eq '{_escape(prefix)}' or "
                f"path_prefixes/any(p: p eq '{_escape(prefix)}'))"
                for prefix in effective_paths
            ]
            clause += f" and ({' or '.join(path_clauses)})"
        clauses.append(f"({clause})")
        metadata[key] = repository

    search_filter = " or ".join(clauses)
    normalized_languages = sorted(
        {value.strip().lower() for value in languages or [] if value.strip()}
    )
    if len(normalized_languages) > 10:
        raise ValueError("languages must contain at most 10 values")
    if search_filter and normalized_languages:
        language_filter = " or ".join(
            f"language eq '{_escape(value)}'" for value in normalized_languages
        )
        search_filter = f"({search_filter}) and ({language_filter})"
    return search_filter, metadata, unavailable


def _intersect_prefixes(
    allowed: tuple[str, ...], requested: tuple[str, ...]
) -> tuple[str, ...] | None:
    if not requested:
        return allowed
    if not allowed:
        return requested
    output: set[str] = set()
    for allowed_prefix in allowed:
        for requested_prefix in requested:
            if _contains(allowed_prefix, requested_prefix):
                output.add(requested_prefix)
            elif _contains(requested_prefix, allowed_prefix):
                output.add(allowed_prefix)
    return tuple(sorted(output)) if output else None


def _contains(parent: str, child: str) -> bool:
    return parent == child or child.startswith(f"{parent}/")


def _normalize_prefix(value: str) -> str:
    raw = value.strip().replace("\\", "/").strip("/")
    if not raw or any(character in raw for character in "*?[]"):
        raise ValueError(f"invalid path prefix: {value!r}")
    path = PurePosixPath(raw)
    if any(part in {".", ".."} for part in path.parts):
        raise ValueError(f"invalid path prefix: {value!r}")
    return path.as_posix()


def _canonical_git_url(value: str) -> str:
    parsed = urlparse(value.strip())
    if (
        parsed.scheme not in {"http", "https"}
        or not parsed.hostname
        or parsed.username
        or parsed.password
        or parsed.query
        or parsed.fragment
    ):
        raise ValueError(f"invalid Git URL: {value!r}")
    path = parsed.path.rstrip("/")
    if not path or path == "/":
        raise ValueError(f"invalid Git URL: {value!r}")
    if not path.endswith(".git"):
        path += ".git"
    return urlunparse(("https", parsed.netloc.lower(), path, "", "", ""))


def _rank_and_format(
    ranked_lists: list[list[dict[str, Any]]],
    metadata: dict[tuple[str, str], _Repository],
    limit: int,
    content_limit: int,
) -> list[CodeReference]:
    scores: dict[str, float] = {}
    documents: dict[str, dict[str, Any]] = {}
    for ranked in ranked_lists:
        seen: set[str] = set()
        for rank, document in enumerate(ranked, 1):
            chunk_id = str(document.get("chunk_id") or "")
            if not chunk_id or chunk_id in seen:
                continue
            seen.add(chunk_id)
            documents.setdefault(chunk_id, document)
            scores[chunk_id] = scores.get(chunk_id, 0.0) + 1.0 / (_RRF_K + rank)

    per_repository: dict[tuple[str, str], int] = {}
    per_file: dict[tuple[str, str, str], int] = {}
    output: list[CodeReference] = []
    for chunk_id in sorted(scores, key=lambda value: scores[value], reverse=True):
        document = documents[chunk_id]
        key = (str(document.get("git_url")), str(document.get("git_ref")))
        repository = metadata.get(key)
        if repository is None:
            continue
        path = str(document.get("path") or "")
        file_key = (*key, path)
        if per_repository.get(key, 0) >= 4 or per_file.get(file_key, 0) >= 2:
            continue
        output.append(
            _reference(document, repository, scores[chunk_id], content_limit)
        )
        per_repository[key] = per_repository.get(key, 0) + 1
        per_file[file_key] = per_file.get(file_key, 0) + 1
        if len(output) >= limit:
            break
    return output


def _reference(
    document: dict[str, Any],
    repository: _Repository,
    score: float,
    content_limit: int,
) -> CodeReference:
    path = str(document.get("path") or "")
    start_line = max(int(document.get("start_line") or 1), 1)
    end_line = max(int(document.get("end_line") or start_line), start_line)
    content = str(document.get("content") or "")
    if len(content) > content_limit:
        content = content[:content_limit].rstrip() + "\n... [truncated]"
    return CodeReference(
        git_url=repository.git_url,
        git_ref=repository.git_ref,
        commit_sha=repository.resolved_commit_sha,
        indexed_at=repository.indexed_at,
        path=path,
        start_line=start_line,
        end_line=end_line,
        language=str(document.get("language") or ""),
        artifact_type=str(document.get("artifact_type") or ""),
        symbol_name=_optional_string(document.get("symbol_name")),
        symbol_kind=_optional_string(document.get("symbol_kind")),
        content=content,
        link=_github_link(repository, path, start_line, end_line),
        score=score,
    )


def _github_link(
    repository: _Repository, path: str, start_line: int, end_line: int
) -> str:
    parsed = urlparse(repository.git_url)
    if parsed.hostname != "github.com":
        raise RuntimeError(f"immutable links are unsupported for {repository.git_url}")
    repository_path = parsed.path.removesuffix(".git").strip("/")
    encoded_path = quote(path, safe="/")
    return (
        f"https://github.com/{repository_path}/blob/"
        f"{repository.resolved_commit_sha}/{encoded_path}"
        f"#L{start_line}-L{end_line}"
    )


def _optional_string(value: Any) -> str | None:
    return str(value) if value else None


def _escape(value: str) -> str:
    return value.replace("'", "''")
