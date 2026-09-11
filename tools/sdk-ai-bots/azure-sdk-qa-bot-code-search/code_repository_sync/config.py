from __future__ import annotations

import logging
import os
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any

from .models import SyncSettings

logger = logging.getLogger(__name__)

_settings: dict[str, str] = {}


async def load(credential: Any) -> None:
    """Load App Configuration values while preserving environment overrides."""
    _settings.clear()
    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        logger.info("AZURE_APPCONFIG_ENDPOINT not set; using environment only")
        return

    from azure.appconfiguration.aio import AzureAppConfigurationClient

    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)
    async with client:
        async for item in client.list_configuration_settings():
            if item.value is not None:
                _settings[item.key] = item.value
    logger.info("loaded %d settings from App Configuration", len(_settings))


def get(name: str, default: str = "") -> str:
    return os.environ.get(name) or _settings.get(name, default)


def settings() -> SyncSettings:
    endpoint = get("STORAGE_BLOB_ENDPOINT") or get("STORAGE_BASE_URL")
    if not endpoint:
        raise RuntimeError(
            "STORAGE_BLOB_ENDPOINT or STORAGE_BASE_URL is required in the "
            "environment or App Configuration (AZURE_APPCONFIG_ENDPOINT)."
        )
    return SyncSettings(
        storage_endpoint=endpoint.rstrip("/"),
        storage_container=get(
            "STORAGE_CODE_REPOSITORY_CONTAINER", "code-repositories"
        ),
        blob_prefix=_normalize_prefix(get("CODE_REPOSITORY_PREFIX", "")),
        work_root=Path(
            get("CODE_REPOSITORY_WORK_ROOT", ".repository-sync-work")
        ).resolve(),
        max_file_bytes=_positive_int(
            "CODE_REPOSITORY_MAX_FILE_BYTES", 2 * 1024 * 1024
        ),
        storage_concurrency=_positive_int(
            "CODE_REPOSITORY_SYNC_CONCURRENCY", 32
        ),
    )


def _positive_int(name: str, default: int) -> int:
    raw = get(name, str(default))
    try:
        value = int(raw)
    except ValueError as exc:
        raise RuntimeError(f"{name} must be an integer, got {raw!r}") from exc
    if value <= 0:
        raise RuntimeError(f"{name} must be positive, got {value}")
    return value


def _normalize_prefix(value: str) -> str:
    raw = value.replace("\\", "/").strip()
    if raw.startswith("/") or PureWindowsPath(raw).is_absolute():
        raise RuntimeError(f"CODE_REPOSITORY_PREFIX is unsafe: {value!r}")
    raw = raw.rstrip("/")
    if not raw:
        return ""
    path = PurePosixPath(raw)
    if path.is_absolute() or ".." in path.parts or "." in path.parts:
        raise RuntimeError(f"CODE_REPOSITORY_PREFIX is unsafe: {value!r}")
    return path.as_posix()
