"""Unit tests for bot configuration lookup."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from services.bot_config_service import BotConfigService


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
