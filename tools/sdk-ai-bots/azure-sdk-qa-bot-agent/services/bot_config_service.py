"""Bot configuration lookup service."""

from __future__ import annotations

import asyncio
import logging
import time
from typing import Any

import yaml
from azure.core.exceptions import AzureError

from config.app_config import get as cfg
from models.bot_config import BotSettings, ChannelConfigResponse
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
                bot_settings = entry.get("bot_settings")
                return ChannelConfigResponse(
                    channel_id=channel_id,
                    tenant_id=entry.get("tenant") or default_tenant,
                    bot_settings=BotSettings.model_validate(
                        {} if bot_settings is None else bot_settings
                    ),
                )

        logger.info("No channel-specific tenant configured for channel: %s", channel_id)
        return ChannelConfigResponse(channel_id=channel_id, tenant_id=default_tenant)

    async def get_bot_settings(
        self, conversation_id: str | None, channel_id: str | None = None
    ) -> BotSettings:
        thread_channel = (
            conversation_id.split(";messageid=", 1)[0]
            if conversation_id and ";messageid=" in conversation_id
            else None
        )
        if channel_id and thread_channel and channel_id != thread_channel:
            raise ValueError("Channel ID does not match conversation ID")
        resolved = thread_channel or channel_id
        if not resolved:
            return BotSettings()
        return (await self.get_channel_config(resolved)).bot_settings

    async def _get_config(self) -> dict[str, Any]:
        now = time.monotonic()
        if self._channel_config is not None and now < self._cache_expires_at:
            return self._channel_config

        async with self._cache_lock:
            now = time.monotonic()
            if self._channel_config is not None and now < self._cache_expires_at:
                return self._channel_config

            parsed = await self._load_config()
            if parsed is None:
                return {}
            self._channel_config = parsed
            self._cache_expires_at = now + self._get_cache_ttl_seconds()
            return self._channel_config

    async def _load_config(self) -> dict[str, Any] | None:
        container = cfg("STORAGE_CONFIG_CONTAINER", "bot-configs")
        blob = cfg("CHANNEL_CONFIG_BLOB", "channel.yaml")
        try:
            data = await download_blob(container, blob)
        except (AzureError, TimeoutError, RuntimeError):
            logger.exception(
                "Cannot retrieve channel settings from %s/%s; using default settings with optional features disabled",
                container, blob,
            )
            return None
        if not data:
            logger.warning(
                "Channel config blob is empty or missing: %s/%s; using default settings with optional features disabled",
                container, blob,
            )
            return None

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
