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
@pytest.mark.parametrize("configuration", [
    {},
    {"channels": None},
    {"channels": []},
    {"channels": [{"id": "other", "bot_settings": {"show_confidence_label": True}}]},
    {"channels": [{"id": "channel"}]},
    {"channels": [{"id": "channel", "bot_settings": None}]},
    {"channels": [{"id": "channel", "bot_settings": {}}]},
])
async def test_missing_bot_settings_is_legacy(configuration) -> None:
    service = BotConfigService()
    service._load_config = AsyncMock(return_value=configuration)
    settings = await service.get_bot_settings("channel;messageid=root")
    assert not settings.enhanced_intention_rules_enabled
    assert settings.model_dump() == {
        "show_confidence_label": False,
        "allow_replies_after_humans": False,
        "allow_notify_experts": False,
        "expert_help_threshold": "high",
        "experts": [],
    }


@pytest.mark.asyncio
async def test_channel_policy_reuses_blob_configuration() -> None:
    service = BotConfigService()
    service._load_config = AsyncMock(return_value={"channels": [{
        "id": "channel",
        "bot_settings": {
            "show_confidence_label": True,
            "expert_help_threshold": "medium",
            "experts": [{"id": "expert@example.com", "name": "Expert"}],
        },
    }]})
    settings = await service.get_bot_settings("channel;messageid=root", "channel")
    assert settings.enhanced_intention_rules_enabled
    assert settings.show_confidence_label
    assert not settings.allow_replies_after_humans
    assert not settings.allow_notify_experts
    assert settings.expert_help_threshold == "medium"
    assert settings.experts[0].id == "expert@example.com"


@pytest.mark.asyncio
async def test_mismatched_channel_cannot_select_other_policy() -> None:
    with pytest.raises(ValueError, match="does not match"):
        await BotConfigService().get_bot_settings("channel;messageid=root", "other")


@pytest.mark.asyncio
@pytest.mark.parametrize("invalid", [
    {"show_confidence_label": "false"},
    {"show_confidence_label": 0},
    {"show_confidence_label": None},
    {"allow_replies_after_humans": "true"},
    {"allow_replies_after_humans": 1},
    {"allow_replies_after_humans": None},
    {"allow_notify_experts": "true"},
    {"allow_notify_experts": 1},
    {"allow_notify_experts": None},
    {"expert_help_threshold": "certain"},
    {"expert_help_threshold": None},
    {"experts": None},
    {"experts": "expert"},
    {"experts": [{"id": "", "name": "Expert"}]},
    {"experts": [{"id": "expert"}]},
    False,
    "",
    [],
])
async def test_invalid_policy_is_not_silently_disabled(invalid) -> None:
    service = BotConfigService()
    service._load_config = AsyncMock(return_value={"channels": [{
        "id": "channel", "bot_settings": invalid,
    }]})
    with pytest.raises(ValueError):
        await service.get_bot_settings("channel;messageid=root")


@pytest.mark.parametrize("partial,active", [
    ({}, False),
    ({"expert_help_threshold": "low"}, False),
    ({"show_confidence_label": True}, True),
    ({"allow_replies_after_humans": True}, True),
    ({"allow_notify_experts": True}, True),
    ({"experts": [{"id": "expert", "name": "Expert"}]}, False),
    ({"show_confidence_label": True, "allow_replies_after_humans": True,
      "experts": [{"id": "expert", "name": "Expert"}]}, True),
])
def test_independent_defaults_and_nonserialized_activation(partial, active):
    settings = BotSettings.model_validate(partial)
    assert settings.enhanced_intention_rules_enabled is active
    assert settings.requires_confidence is (
        partial.get("show_confidence_label", False)
        or partial.get("allow_notify_experts", False)
    )
    assert settings.show_confidence_label is partial.get("show_confidence_label", False)
    assert settings.allow_replies_after_humans is partial.get("allow_replies_after_humans", False)
    assert settings.allow_notify_experts is partial.get("allow_notify_experts", False)
    assert settings.model_dump()["experts"] == partial.get("experts", [])
    for fields in (settings.model_dump(), settings.model_json_schema()["properties"]):
        assert not {
            "enhanced_intention_rules_enabled", "requires_confidence",
        } & fields.keys()


@pytest.mark.asyncio
async def test_no_channel_coordinates_do_not_load_configuration():
    service = BotConfigService()
    service._load_config = AsyncMock()
    assert not (await service.get_bot_settings(None)).enhanced_intention_rules_enabled
    service._load_config.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", [AzureError("Blob unavailable"), TimeoutError("Timed out"), RuntimeError("Storage not configured")])
async def test_retrieval_failure_defaults_all_features_off_without_caching(monkeypatch, caplog, failure):
    download = AsyncMock(side_effect=[
        failure,
        b"channels:\n  - id: channel\n    bot_settings:\n      show_confidence_label: true\n",
    ])
    monkeypatch.setattr("services.bot_config_service.download_blob", download)
    monkeypatch.setattr("services.bot_config_service.cfg", lambda key, default=None: default)
    service = BotConfigService()

    channel = await service.get_channel_config("channel")
    assert channel.channel_id == "channel"
    assert channel.tenant_id is None
    assert channel.bot_settings == BotSettings()
    assert service._channel_config is None
    assert "using default settings with optional features disabled" in caplog.text
    assert (await service.get_bot_settings("channel;messageid=root")).show_confidence_label
    assert download.await_count == 2


@pytest.mark.asyncio
@pytest.mark.parametrize("data", [None, b""])
async def test_unavailable_blob_uses_defaults(monkeypatch, caplog, data):
    monkeypatch.setattr("services.bot_config_service.download_blob", AsyncMock(return_value=data))
    service = BotConfigService()
    assert await service.get_bot_settings("channel;messageid=root") == BotSettings()
    assert service._channel_config is None
    assert "empty or missing" in caplog.text


@pytest.mark.asyncio
async def test_failed_refresh_does_not_reuse_expired_optins(monkeypatch):
    enabled = b"channels:\n  - id: channel\n    bot_settings:\n      allow_notify_experts: true\n"
    download = AsyncMock(side_effect=[enabled, TimeoutError("Unavailable"), enabled])
    monkeypatch.setattr("services.bot_config_service.download_blob", download)
    monkeypatch.setattr(
        "services.bot_config_service.cfg",
        lambda key, default=None: "0" if key == "CHANNEL_CONFIG_CACHE_TTL_SECONDS" else default,
    )
    service = BotConfigService()
    assert (await service.get_bot_settings("channel;messageid=root")).allow_notify_experts
    assert await service.get_bot_settings("channel;messageid=root") == BotSettings()
    assert (await service.get_bot_settings("channel;messageid=root")).allow_notify_experts
    assert download.await_count == 3


@pytest.mark.asyncio
async def test_retrieved_invalid_document_remains_an_error(monkeypatch):
    monkeypatch.setattr(
        "services.bot_config_service.download_blob", AsyncMock(return_value=b"- not-a-config-object")
    )
    with pytest.raises(RuntimeError, match="must contain a YAML object"):
        await BotConfigService().get_channel_config("channel")
