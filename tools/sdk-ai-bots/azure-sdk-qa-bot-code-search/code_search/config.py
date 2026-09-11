from __future__ import annotations

import logging
import os
from pathlib import Path

from azure.core.credentials import TokenCredential

from .models import BuilderSettings, EmbeddingConfig

logger = logging.getLogger(__name__)

_settings: dict[str, str] = {}


def load(credential: TokenCredential) -> None:
    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        logger.info("AZURE_APPCONFIG_ENDPOINT not set; using environment only")
        return

    from azure.appconfiguration import AzureAppConfigurationClient

    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)
    with client:
        for item in client.list_configuration_settings():
            if item.value is not None:
                _settings[item.key] = item.value
    logger.info("loaded %d settings from App Configuration", len(_settings))


def get(name: str, default: str = "") -> str:
    return os.environ.get(name) or _settings.get(name, default)


def require(name: str) -> str:
    value = get(name)
    if not value:
        raise RuntimeError(
            f"{name} is required; set it in the environment or App Configuration "
            "(AZURE_APPCONFIG_ENDPOINT)."
        )
    return value


def _positive_int(name: str, default: str = "") -> int:
    raw = get(name, default) if default else require(name)
    try:
        value = int(raw)
    except ValueError as exc:
        raise RuntimeError(f"{name} must be an integer, got {raw!r}") from exc
    if value <= 0:
        raise RuntimeError(f"{name} must be positive, got {value}")
    return value


def builder_settings() -> BuilderSettings:
    storage_endpoint = get("STORAGE_BLOB_ENDPOINT") or get("STORAGE_BASE_URL")
    if not storage_endpoint:
        raise RuntimeError(
            "STORAGE_BLOB_ENDPOINT or STORAGE_BASE_URL is required in the environment "
            "or App Configuration."
        )

    search_alias = get("CODE_INDEX_SEARCH_ALIAS", "azure-sdk-code")
    embedding = EmbeddingConfig(
        endpoint=require("AZURE_OPENAI_ENDPOINT").rstrip("/"),
        deployment=require("CODE_INDEX_EMBEDDING_DEPLOYMENT"),
        model=require("CODE_INDEX_EMBEDDING_MODEL"),
        dimensions=_positive_int("CODE_INDEX_EMBEDDING_DIMENSIONS"),
        api_version=get("AZURE_OPENAI_API_VERSION", "2024-02-01"),
    )
    return BuilderSettings(
        search_endpoint=require("AI_SEARCH_BASE_URL").rstrip("/"),
        search_alias=search_alias,
        search_index_name=get("CODE_INDEX_SEARCH_INDEX", f"{search_alias}-v1"),
        search_user_assigned_identity_resource_id=get(
            "SEARCH_USER_ASSIGNED_IDENTITY_RESOURCE_ID"
        ),
        storage_endpoint=storage_endpoint.rstrip("/"),
        storage_container=get("STORAGE_CODE_INDEX_CONTAINER", "code-index"),
        work_root=Path(get("CODE_INDEX_WORK_ROOT", ".code-index")).resolve(),
        max_file_bytes=_positive_int("CODE_INDEX_MAX_FILE_BYTES", "2097152"),
        embedding_concurrency=_positive_int(
            "CODE_INDEX_EMBEDDING_CONCURRENCY", "2"
        ),
        validation_timeout_seconds=_positive_int(
            "CODE_INDEX_VALIDATION_TIMEOUT_SECONDS", "180"
        ),
        embedding=embedding,
    )
