"""Opt-in support catalog loading with per-turn validated snapshots."""

from __future__ import annotations

import asyncio
import logging
import math
import time
from collections.abc import Awaitable, Callable

import yaml
from azure.core.exceptions import AzureError

from config.app_config import Settings, get as cfg
from models.guided_next_step import SupportContactCatalog
from utils.azure_storage import download_blob

logger = logging.getLogger(__name__)
BlobLoader = Callable[[str, str], Awaitable[bytes | None]]


class _UniqueKeyLoader(yaml.SafeLoader):
    pass


def _unique_mapping(loader: _UniqueKeyLoader, node: yaml.MappingNode) -> dict:
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node)
        if not isinstance(key, str) or key in result:
            raise yaml.constructor.ConstructorError(
                None, None, "Configuration keys must be unique strings.", key_node.start_mark
            )
        result[key] = loader.construct_object(value_node)
    return result


_UniqueKeyLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, _unique_mapping
)


def parse_support_catalog(data: bytes) -> SupportContactCatalog:
    if not data:
        raise ValueError("Support-contact configuration is empty or missing.")
    return SupportContactCatalog.model_validate(
        yaml.load(data.decode("utf-8"), Loader=_UniqueKeyLoader)
    )


class SupportContactService:
    def __init__(
        self,
        *,
        settings: Settings | None = None,
        loader: BlobLoader | None = None,
    ) -> None:
        self._settings = settings or cfg
        self._loader = loader or download_blob
        self._catalog: SupportContactCatalog | None = None
        self._cache_key: tuple[str, str] | None = None
        self._cache_expires_at = 0.0
        self._cache_lock = asyncio.Lock()

    async def get_catalog(self) -> SupportContactCatalog | None:
        blob = self._settings("SUPPORT_CONTACT_BLOB", "").strip()
        if not blob:
            return None
        container = self._settings("STORAGE_CONFIG_CONTAINER", "bot-configs")
        key = (container, blob)
        if self._fresh(key):
            return self._catalog
        async with self._cache_lock:
            if self._fresh(key):
                return self._catalog
            try:
                ttl = float(self._settings("SUPPORT_CONTACT_CACHE_TTL_SECONDS", "300"))
                if not math.isfinite(ttl) or ttl < 0:
                    raise ValueError("Support-contact cache TTL must be finite and nonnegative.")
                data = await self._loader(container, blob)
                catalog = parse_support_catalog(data or b"")
            except (AzureError, ValueError, yaml.YAMLError) as exc:
                logger.error(
                    "Support-contact configuration failed: container=%s blob=%s error=%s",
                    container, blob, type(exc).__name__,
                )
                raise RuntimeError("Unable to load valid support-contact configuration.") from exc
            self._catalog = catalog
            self._cache_key = key
            self._cache_expires_at = time.monotonic() + ttl
            logger.info("Support-contact catalog loaded: revision=%s", catalog.revision)
            return catalog

    def _fresh(self, key: tuple[str, str]) -> bool:
        return (
            self._catalog is not None
            and self._cache_key == key
            and time.monotonic() < self._cache_expires_at
        )
