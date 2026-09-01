"""Bot configuration lookup service."""

from __future__ import annotations

import logging

import yaml

from config.app_config import get as cfg
from models.bot_config import ChannelConfigResponse
from utils.azure_storage import download_blob

logger = logging.getLogger(__name__)


class BotConfigService:
    async def get_channel_config(self, channel_id: str) -> ChannelConfigResponse:
        container = cfg("STORAGE_CONFIG_CONTAINER", "bot-configs")
        blob = cfg("CHANNEL_CONFIG_BLOB", "channel.yaml")
        data = await download_blob(container, blob)
        if not data:
            raise RuntimeError(
                f"Channel config blob is empty or missing: {container}/{blob}"
            )

        parsed = yaml.safe_load(data.decode("utf-8")) or {}
        default_tenant = parsed.get("tenant")

        for entry in parsed.get("channels", []) or []:
            if entry.get("id") == channel_id:
                return ChannelConfigResponse(
                    channel_id=channel_id,
                    tenant_id=entry.get("tenant") or default_tenant,
                )

        logger.info("No channel-specific tenant configured for channel: %s", channel_id)
        return ChannelConfigResponse(channel_id=channel_id, tenant_id=default_tenant)
