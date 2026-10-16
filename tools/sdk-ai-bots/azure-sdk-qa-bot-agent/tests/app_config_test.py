"""Tests for shared configuration refresh and failure recovery."""

import asyncio
import logging
from unittest.mock import AsyncMock

import pytest
from azure.core.exceptions import ServiceRequestError

from config import app_config


@pytest.fixture(autouse=True)
def reset_configuration(monkeypatch):
    monkeypatch.setenv("AZURE_APPCONFIG_ENDPOINT", "https://test.azconfig.io")
    monkeypatch.setattr(app_config, "_settings", None)
    monkeypatch.setattr(app_config, "_settings_refresh_at", 0.0)
    monkeypatch.setattr(app_config, "_settings_refresh_lock", asyncio.Lock())
    monkeypatch.setattr(app_config, "monotonic", lambda: 100.0)


@pytest.mark.asyncio
@pytest.mark.parametrize("error", [ServiceRequestError("unavailable"), TimeoutError()])
async def test_startup_failure_propagates(monkeypatch, error):
    load = AsyncMock(side_effect=error)
    monkeypatch.setattr(app_config, "load", load)

    with pytest.raises(type(error)):
        await app_config.init()

    assert app_config._settings is None
    assert app_config._settings_refresh_at == 0.0


@pytest.mark.asyncio
@pytest.mark.parametrize("settings", [{"AGENT_DEPLOYMENTS_REGISTRY": '{"branch":{"agent":"42"}}'}, {}])
@pytest.mark.parametrize("error", [ServiceRequestError("unavailable"), TimeoutError()])
async def test_refresh_retains_snapshot_and_delays_retry(
    monkeypatch, caplog, settings, error
):
    monkeypatch.setattr(app_config, "_settings", settings)
    load = AsyncMock(side_effect=error)
    monkeypatch.setattr(app_config, "load", load)

    with caplog.at_level(logging.ERROR, logger=app_config.__name__):
        await app_config.refresh()
        await app_config.refresh()

    assert app_config._settings is settings
    assert app_config._settings_refresh_at == 160.0
    load.assert_awaited_once()
    assert "retaining cached settings" in caplog.text


@pytest.mark.asyncio
async def test_refresh_recovers_after_retry_deadline(monkeypatch):
    monkeypatch.setattr(app_config, "_settings", {"version": "41"})
    load = AsyncMock(
        side_effect=[
            ServiceRequestError("unavailable"),
            app_config.AppConfigSnapshot(
                endpoint="https://test.azconfig.io", settings={"version": "42"}
            ),
        ]
    )
    monkeypatch.setattr(app_config, "load", load)

    await app_config.refresh()
    assert app_config.get("version") == "41"

    monkeypatch.setattr(app_config, "monotonic", lambda: 160.0)
    await app_config.refresh()

    assert app_config.get("version") == "42"
    assert app_config._settings_refresh_at == 220.0
    assert load.await_count == 2


@pytest.mark.asyncio
async def test_concurrent_refresh_failures_only_load_once(monkeypatch):
    monkeypatch.setattr(app_config, "_settings", {"version": "41"})

    async def fail_load(endpoint):
        await asyncio.sleep(0)
        raise ServiceRequestError("unavailable")

    load = AsyncMock(side_effect=fail_load)
    monkeypatch.setattr(app_config, "load", load)

    await asyncio.gather(*(app_config.refresh() for _ in range(5)))

    load.assert_awaited_once()
    assert app_config.get("version") == "41"
    assert app_config._settings_refresh_at == 160.0


@pytest.mark.asyncio
async def test_unexpected_refresh_errors_propagate(monkeypatch):
    monkeypatch.setattr(app_config, "_settings", {"version": "41"})
    monkeypatch.setattr(
        app_config, "load", AsyncMock(side_effect=ValueError("invalid configuration"))
    )

    with pytest.raises(ValueError, match="invalid configuration"):
        await app_config.refresh()

    assert app_config._settings_refresh_at == 0.0


def test_parse_agent_deployments_preserves_branch_and_agent_names():
    assert app_config.parse_agent_deployments(
        '{"feature/Case/path":{"chat-agent":"42","mcp-agent":"17"},'
        '"other-branch":{"chat-agent":"43"}}'
    ) == {
        "feature/Case/path": {"chat-agent": "42", "mcp-agent": "17"},
        "other-branch": {"chat-agent": "43"},
    }
    assert app_config.parse_agent_deployments("{}") == {}


@pytest.mark.parametrize("value", [
    "", "not json", "null", "[]", '{"branch":"42"}',
    '{"branch":{"agent":42}}', '{"branch":{"agent":null}}',
])
def test_parse_agent_deployments_rejects_invalid_registry(value):
    with pytest.raises(ValueError):
        app_config.parse_agent_deployments(value)
