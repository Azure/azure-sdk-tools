from __future__ import annotations

import hashlib
import json
from collections import defaultdict
from collections.abc import Iterable
from pathlib import PurePosixPath
from typing import Any
from urllib.parse import urlsplit, urlunsplit

import cocoindex

from . import CHUNKER_VERSION, CODE_INDEX_SCHEMA_VERSION
from .models import (
    BuilderSettings,
    RepositoryIndexConfig,
    TenantRepositoryDemand,
)

COMMON_EXCLUDES = (
    "**/.git/**",
    "**/.venv/**",
    "**/__pycache__/**",
    "**/node_modules/**",
    "**/dist/**",
    "**/coverage/**",
    "**/.pytest_cache/**",
    "**/*.min.js",
    "**/*.map",
)


def aggregate_repository_demands(
    raw_configs: Iterable[tuple[str, Any]], settings: BuilderSettings
) -> list[RepositoryIndexConfig]:
    demands = [_normalize_demand(tenant, config) for tenant, config in raw_configs]
    grouped: dict[tuple[str, str], list[TenantRepositoryDemand]] = defaultdict(list)
    for demand in demands:
        grouped[(demand.git_url, demand.git_ref)].append(demand)

    result: list[RepositoryIndexConfig] = []
    for (git_url, git_ref), entries in sorted(grouped.items()):
        _validate_shared_settings(entries)
        first = entries[0]
        include_patterns = tuple(
            sorted({pattern for entry in entries for pattern in entry.include_patterns})
        )
        path_prefixes = tuple(
            sorted({prefix for entry in entries for prefix in entry.path_prefixes})
        )
        tenant_scopes: dict[str, set[str]] = defaultdict(set)
        for entry in entries:
            tenant_scopes[entry.tenant_id].update(entry.path_prefixes)
        tenant_path_prefixes = tuple(
            (tenant, tuple(sorted(prefixes)))
            for tenant, prefixes in sorted(tenant_scopes.items())
        )
        key = repository_key(git_url, git_ref)
        identity_payload = {
            "schema": CODE_INDEX_SCHEMA_VERSION,
            "cocoindex": cocoindex.__version__,
            "chunker": CHUNKER_VERSION,
            "git_url": git_url,
            "git_ref": git_ref,
            "include": include_patterns,
            "exclude": first.exclude,
            "max_file_bytes": settings.max_file_bytes,
            "splitter": {"target": 1000, "minimum": 250, "overlap": 150},
            "embedding": {
                "endpoint": settings.embedding.endpoint,
                "deployment": settings.embedding.deployment,
                "model": settings.embedding.model,
                "dimensions": settings.embedding.dimensions,
                "api_version": settings.embedding.api_version,
            },
            "search_index": settings.search_index_name,
        }
        result.append(
            RepositoryIndexConfig(
                git_url=git_url,
                git_ref=git_ref,
                repository_key=key,
                path_prefixes=path_prefixes,
                include_patterns=include_patterns,
                exclude=tuple(sorted(set(first.exclude) | set(COMMON_EXCLUDES))),
                tenant_path_prefixes=tenant_path_prefixes,
                max_file_bytes=settings.max_file_bytes,
                index_identity=hashlib.sha256(
                    json.dumps(identity_payload, sort_keys=True).encode("utf-8")
                ).hexdigest(),
            )
        )
    return result


def _normalize_demand(tenant_id: str, config: Any) -> TenantRepositoryDemand:
    try:
        git_url = canonical_git_url(config.git_url)
        git_ref = str(config.git_ref)
        normalized_prefixes = {
            _normalize_path_prefix(value) for value in config.path_prefixes
        }
        path_prefixes = tuple(sorted(normalized_prefixes)) or ("",)
        include_patterns = tuple(
            sorted({_normalize_pattern(value) for value in config.include_patterns})
        )
        exclude = tuple(sorted({_normalize_pattern(value) for value in config.exclude}))
    except AttributeError as exc:
        raise ValueError(
            f"Invalid CodeRepositoryConfig for tenant {tenant_id!r}: missing {exc.name}"
        ) from exc
    if not git_ref.startswith("refs/"):
        raise ValueError(f"git_ref must be a full ref name, got {git_ref!r}")
    if not include_patterns:
        raise ValueError(f"{git_url} {git_ref} has no include_patterns")
    return TenantRepositoryDemand(
        tenant_id=tenant_id,
        git_url=git_url,
        git_ref=git_ref,
        path_prefixes=path_prefixes,
        include_patterns=include_patterns,
        exclude=exclude,
    )


def canonical_git_url(value: str) -> str:
    parts = urlsplit(str(value).strip())
    if parts.scheme.lower() != "https" or not parts.hostname:
        raise ValueError(f"git_url must be a canonical HTTPS clone URL, got {value!r}")
    if parts.query or parts.fragment or parts.username or parts.password:
        raise ValueError(f"git_url contains unsupported URL components: {value!r}")
    path = parts.path.rstrip("/")
    if not path.endswith(".git"):
        path += ".git"
    if path.count("/") < 2:
        raise ValueError(f"git_url must identify a repository, got {value!r}")
    return urlunsplit(("https", parts.netloc.lower(), path, "", ""))


def repository_key(git_url: str, git_ref: str) -> str:
    return _digest(canonical_git_url(git_url), git_ref)


def _normalize_path_prefix(value: Any) -> str:
    raw = str(value).replace("\\", "/").strip("/")
    if raw in ("", "."):
        return ""
    path = PurePosixPath(raw)
    if ".." in path.parts or any(char in raw for char in "*?["):
        raise ValueError(f"path_prefixes must be literal repository paths, got {value!r}")
    return path.as_posix()


def _normalize_pattern(value: Any) -> str:
    pattern = str(value).replace("\\", "/").strip()
    if not pattern:
        raise ValueError("empty file pattern")
    return pattern


def _validate_shared_settings(entries: list[TenantRepositoryDemand]) -> None:
    first = entries[0]
    for entry in entries[1:]:
        if entry.exclude != first.exclude:
            raise ValueError(
                f"Conflicting exclude for shared repository "
                f"{first.git_url} {first.git_ref}"
            )


def _digest(*parts: str) -> str:
    return hashlib.sha256("\0".join(parts).encode("utf-8")).hexdigest()[:24]
