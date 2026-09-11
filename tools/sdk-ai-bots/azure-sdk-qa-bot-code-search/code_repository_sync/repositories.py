from __future__ import annotations

from collections.abc import Iterable
from pathlib import PurePosixPath, PureWindowsPath
import re
from typing import Any
from urllib.parse import unquote, urlsplit, urlunsplit

from .models import RepositoryConfig


def aggregate_repository_configs(
    raw_configs: Iterable[tuple[str, Any]],
) -> list[RepositoryConfig]:
    """Deduplicate identical URL/ref declarations and reject ambiguity."""
    by_url_ref: dict[tuple[str, str], RepositoryConfig] = {}
    by_namespace: dict[str, RepositoryConfig] = {}

    for tenant_id, raw in raw_configs:
        config = _normalize_config(tenant_id, raw)
        url_ref_key = (config.git_url.casefold(), config.git_ref)
        previous = by_url_ref.get(url_ref_key)
        if previous is not None:
            if _selection(previous) != _selection(config):
                raise ValueError(
                    "Conflicting repository selection for "
                    f"{config.git_url} {config.git_ref}"
                )
            continue

        namespace_key = config.namespace.casefold()
        namespace_owner = by_namespace.get(namespace_key)
        if namespace_owner is not None:
            raise ValueError(
                f"Conflicting refs or URLs map to blob namespace "
                f"{config.namespace!r}: {namespace_owner.git_url} "
                f"{namespace_owner.git_ref} and {config.git_url} {config.git_ref}"
            )
        by_url_ref[url_ref_key] = config
        by_namespace[namespace_key] = config

    return sorted(by_url_ref.values(), key=lambda item: item.namespace.casefold())


def canonical_git_url(value: str) -> tuple[str, str, str]:
    raw = str(value).strip()
    parts = urlsplit(raw)
    if (
        parts.scheme.lower() != "https"
        or parts.hostname is None
        or parts.hostname.lower() != "github.com"
        or parts.port not in (None, 443)
        or parts.query
        or parts.fragment
        or parts.username
        or parts.password
    ):
        raise ValueError(f"git_url must be a canonical HTTPS clone URL: {value!r}")

    segments = [unquote(segment) for segment in parts.path.rstrip("/").split("/") if segment]
    if len(segments) < 2:
        raise ValueError(f"git_url must identify a repository: {value!r}")
    owner = segments[-2]
    repository = segments[-1]
    if repository.endswith(".git"):
        repository = repository[:-4]
    for label, segment in (("owner", owner), ("repository", repository)):
        if re.fullmatch(r"[A-Za-z0-9._-]+", segment) is None:
            raise ValueError(f"git_url has an unsafe {label}: {value!r}")

    canonical_path = parts.path.rstrip("/")
    if not canonical_path.endswith(".git"):
        canonical_path += ".git"
    canonical = urlunsplit(("https", "github.com", canonical_path, "", ""))
    return canonical, owner, repository


def _normalize_config(tenant_id: str, raw: Any) -> RepositoryConfig:
    try:
        git_url, owner, repository = canonical_git_url(str(raw.git_url))
        git_ref = str(raw.git_ref).strip()
        path_prefixes = tuple(
            sorted({_normalize_path_prefix(value) for value in raw.path_prefixes})
        )
        include_patterns = tuple(
            sorted({_normalize_pattern(value) for value in raw.include_patterns})
        )
        exclude_patterns = tuple(
            sorted({_normalize_pattern(value) for value in raw.exclude})
        )
    except AttributeError as exc:
        raise ValueError(
            f"Invalid CodeRepositoryConfig for tenant {tenant_id!r}: "
            f"missing {exc.name}"
        ) from exc

    if not git_ref.startswith("refs/"):
        raise ValueError(f"git_ref must be a full ref name, got {git_ref!r}")
    if not path_prefixes:
        path_prefixes = ("",)
    if not include_patterns:
        raise ValueError(f"{git_url} {git_ref} has no include_patterns")
    return RepositoryConfig(
        git_url=git_url,
        git_ref=git_ref,
        owner=owner,
        repository=repository,
        path_prefixes=path_prefixes,
        include_patterns=include_patterns,
        exclude_patterns=exclude_patterns,
    )


def _normalize_path_prefix(value: Any) -> str:
    raw = str(value).replace("\\", "/").strip()
    if raw.startswith("/") or PureWindowsPath(raw).is_absolute():
        raise ValueError(f"unsafe repository path prefix: {value!r}")
    raw = raw.rstrip("/")
    if raw in ("", "."):
        return ""
    path = PurePosixPath(raw)
    if (
        path.is_absolute()
        or ".." in path.parts
        or any(character in raw for character in "*?[")
    ):
        raise ValueError(f"unsafe repository path prefix: {value!r}")
    return path.as_posix()


def _normalize_pattern(value: Any) -> str:
    pattern = str(value).replace("\\", "/").strip()
    if (
        not pattern
        or pattern.startswith("/")
        or pattern.startswith("!")
        or PureWindowsPath(pattern).is_absolute()
        or ".." in PurePosixPath(pattern).parts
    ):
        raise ValueError(f"unsafe repository file pattern: {value!r}")
    return pattern.removeprefix("./")


def _selection(
    config: RepositoryConfig,
) -> tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...]]:
    return (
        config.path_prefixes,
        config.include_patterns,
        config.exclude_patterns,
    )
