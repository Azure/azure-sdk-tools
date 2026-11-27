"""Unit tests for bot configuration lookup."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock

import pytest
from azure.core.exceptions import AzureError

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from services.bot_config_service import BotConfigService
from models.bot_config import BotSettings


_CHANNEL_CONFIG = b"""
tenant: typespec_channel_qa_bot
channels:
  - name: Python
    id: python-channel
    tenant: python_channel_qa_bot
  - name: TypeSpec
    id: typespec-channel
"""


@pytest.mark.asyncio
async def test_get_channel_config_returns_channel_tenant(monkeypatch) -> None:
    async def fake_download_blob(container: str, blob: str) -> bytes:
        assert container == "bot-configs"
        assert blob == "channel.yaml"
        return _CHANNEL_CONFIG

    monkeypatch.setattr("services.bot_config_service.download_blob", fake_download_blob)
    monkeypatch.setattr(
        "services.bot_config_service.cfg",
        lambda key, default=None: default,
    )

    response = await BotConfigService().get_channel_config("python-channel")

    assert response.channel_id == "python-channel"
    assert response.tenant_id == "python_channel_qa_bot"


@pytest.mark.asyncio
async def test_get_channel_config_falls_back_to_default_tenant(monkeypatch) -> None:
    async def fake_download_blob(container: str, blob: str) -> bytes:
        return _CHANNEL_CONFIG

    monkeypatch.setattr("services.bot_config_service.download_blob", fake_download_blob)
    monkeypatch.setattr(
        "services.bot_config_service.cfg",
        lambda key, default=None: default,
    )

    response = await BotConfigService().get_channel_config("unknown-channel")

    assert response.channel_id == "unknown-channel"
    assert response.tenant_id == "typespec_channel_qa_bot"


@pytest.mark.asyncio
async def test_get_channel_config_uses_default_when_channel_tenant_missing(
    monkeypatch,
) -> None:
    async def fake_download_blob(container: str, blob: str) -> bytes:
        return _CHANNEL_CONFIG

    monkeypatch.setattr("services.bot_config_service.download_blob", fake_download_blob)
    monkeypatch.setattr(
        "services.bot_config_service.cfg",
        lambda key, default=None: default,
    )

    response = await BotConfigService().get_channel_config("typespec-channel")

    assert response.channel_id == "typespec-channel"
    assert response.tenant_id == "typespec_channel_qa_bot"


@pytest.mark.asyncio
async def test_get_channel_config_reuses_cached_config(monkeypatch) -> None:
    downloads = 0

    async def fake_download_blob(container: str, blob: str) -> bytes:
        nonlocal downloads
        downloads += 1
        return _CHANNEL_CONFIG

    monkeypatch.setattr("services.bot_config_service.download_blob", fake_download_blob)
    monkeypatch.setattr(
        "services.bot_config_service.cfg",
        lambda key, default=None: (
            "60" if key == "CHANNEL_CONFIG_CACHE_TTL_SECONDS" else default
        ),
    )

    service = BotConfigService()

    first_response = await service.get_channel_config("python-channel")
    second_response = await service.get_channel_config("typespec-channel")

    assert downloads == 1
    assert first_response.tenant_id == "python_channel_qa_bot"
    assert second_response.tenant_id == "typespec_channel_qa_bot"


@pytest.mark.asyncio
async def test_get_channels_reuses_cache_and_keeps_ids_without_names(monkeypatch) -> None:
    download = AsyncMock(return_value=b"""
channels:
  - id: channel-without-prefix
    name: Channel A
  - id: channel-without-name
  - id: channel-with-empty-name
    name: ''
""")
    monkeypatch.setattr("services.bot_config_service.download_blob", download)
    monkeypatch.setattr("services.bot_config_service.cfg", lambda key, default=None: default)
    service = BotConfigService()
    expected = {
        "channel-without-prefix": "Channel A",
        "channel-without-name": "channel-without-name",
        "channel-with-empty-name": "channel-with-empty-name",
    }

    assert await service.get_channels() == expected
    assert await service.get_channels() == expected
    download.assert_awaited_once_with("bot-configs", "channel.yaml")


@pytest.mark.asyncio
@pytest.mark.parametrize("configuration", [
    {},
    {"channels": None},
    {"channels": []},
    {"channels": [{"id": "channel"}]},
    {"channels": [{"id": "channel", "bot_settings": None}]},
])
async def test_missing_bot_settings_preserves_legacy_behavior(configuration) -> None:
    service = BotConfigService()
    service._load_config = AsyncMock(return_value=configuration)

    assert await service.get_bot_settings("channel;messageid=root") == BotSettings()


@pytest.mark.asyncio
async def test_channel_bot_settings_reuse_blob_configuration() -> None:
    service = BotConfigService()
    service._load_config = AsyncMock(return_value={"channels": [{
        "id": "channel",
        "bot_settings": {
            "show_confidence_label": True,
            "allow_replies_after_humans": True,
        },
    }]})

    settings = await service.get_bot_settings("channel;messageid=root")

    assert settings.show_confidence_label
    assert settings.allow_replies_after_humans
    assert settings.requires_confidence
    assert settings.enhanced_intention_rules_enabled


@pytest.mark.asyncio
@pytest.mark.parametrize("invalid", [
    {"show_confidence_label": "false"},
    {"show_confidence_label": None},
    {"allow_replies_after_humans": "true"},
    {"allow_replies_after_humans": None},
    False,
    "",
    [],
])
async def test_invalid_bot_settings_are_not_silently_disabled(invalid) -> None:
    service = BotConfigService()
    service._load_config = AsyncMock(return_value={"channels": [{
        "id": "channel",
        "bot_settings": invalid,
    }]})

    with pytest.raises(ValueError):
        await service.get_bot_settings("channel;messageid=root")


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "failure",
    [
        AzureError("Blob unavailable"),
        TimeoutError("Timed out"),
        RuntimeError("Storage not configured"),
    ],
)
async def test_retrieval_failure_defaults_features_off_without_caching(
    monkeypatch, failure
) -> None:
    download = AsyncMock(side_effect=[
        failure,
        b"channels:\n  - id: channel\n    bot_settings:\n      show_confidence_label: true\n",
    ])
    monkeypatch.setattr("services.bot_config_service.download_blob", download)
    monkeypatch.setattr(
        "services.bot_config_service.cfg", lambda key, default=None: default
    )
    service = BotConfigService()

    assert await service.get_bot_settings("channel;messageid=root") == BotSettings()
    assert service._channel_config is None
    assert (await service.get_bot_settings("channel;messageid=root")).show_confidence_label
    assert download.await_count == 2
