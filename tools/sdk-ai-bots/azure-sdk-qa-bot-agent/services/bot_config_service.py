"""Bot configuration lookup service."""

from __future__ import annotations

import asyncio
import logging
import time
from typing import Any

import yaml

from config.app_config import get as cfg
from models.bot_config import ChannelConfigResponse
from utils.azure_storage import download_blob

logger = logging.getLogger(__name__)

_DEFAULT_CACHE_TTL_SECONDS = 300.0


class BotConfigService:
    def __init__(self) -> None:
        self._channel_config: dict[str, Any] | None = None
        self._cache_expires_at = 0.0
        self._cache_lock = asyncio.Lock()

    async def get_channel_config(self, channel_id: str) -> ChannelConfigResponse:
        parsed = await self._get_config()
        default_tenant = parsed.get("tenant")

        for entry in parsed.get("channels", []) or []:
            if entry.get("id") == channel_id:
                return ChannelConfigResponse(
                    channel_id=channel_id,
                    tenant_id=entry.get("tenant") or default_tenant,
                )

        logger.info("No channel-specific tenant configured for channel: %s", channel_id)
        return ChannelConfigResponse(channel_id=channel_id, tenant_id=default_tenant)

    async def _get_config(self) -> dict[str, Any]:
        now = time.monotonic()
        if self._channel_config is not None and now < self._cache_expires_at:
            return self._channel_config

        async with self._cache_lock:
            now = time.monotonic()
            if self._channel_config is not None and now < self._cache_expires_at:
                return self._channel_config

            self._channel_config = await self._load_config()
            self._cache_expires_at = now + self._get_cache_ttl_seconds()
            return self._channel_config

    async def _load_config(self) -> dict[str, Any]:
        container = cfg("STORAGE_CONFIG_CONTAINER", "bot-configs")
        blob = cfg("CHANNEL_CONFIG_BLOB", "channel.yaml")
        data = await download_blob(container, blob)
        if not data:
            raise RuntimeError(
                f"Channel config blob is empty or missing: {container}/{blob}"
            )

        parsed = yaml.safe_load(data.decode("utf-8")) or {}
        if not isinstance(parsed, dict):
            raise RuntimeError(
                f"Channel config blob must contain a YAML object: {container}/{blob}"
            )
        return parsed

    def _get_cache_ttl_seconds(self) -> float:
        raw_ttl = cfg(
            "CHANNEL_CONFIG_CACHE_TTL_SECONDS", str(_DEFAULT_CACHE_TTL_SECONDS)
        )
        try:
            return max(float(raw_ttl), 0.0)
        except (TypeError, ValueError):
            logger.warning(
                "Invalid CHANNEL_CONFIG_CACHE_TTL_SECONDS value: %s. Using default.",
                raw_ttl,
            )
            return _DEFAULT_CACHE_TTL_SECONDS
