"""Tests for shared startup configuration."""

from unittest.mock import AsyncMock

import pytest
from azure.core.exceptions import ServiceRequestError

from config import app_config


@pytest.fixture(autouse=True)
def reset_configuration(monkeypatch):
    monkeypatch.setenv("AZURE_APPCONFIG_ENDPOINT", "https://test.azconfig.io")
    monkeypatch.setattr(app_config, "_settings", None)


@pytest.mark.asyncio
@pytest.mark.parametrize("error", [ServiceRequestError("unavailable"), TimeoutError()])
async def test_startup_failure_propagates(monkeypatch, error):
    load = AsyncMock(side_effect=error)
    monkeypatch.setattr(app_config, "load", load)

    with pytest.raises(type(error)):
        await app_config.init()

    assert app_config._settings is None


@pytest.mark.asyncio
async def test_init_loads_settings_once(monkeypatch):
    settings = {"version": "42"}
    load = AsyncMock(return_value=app_config.AppConfigSnapshot(
        endpoint="https://test.azconfig.io", settings=settings,
    ))
    monkeypatch.setattr(app_config, "load", load)

    await app_config.init()
    await app_config.init()

    load.assert_awaited_once_with("https://test.azconfig.io")
    assert app_config.get("version") == "42"
    assert app_config.get("missing", "default") == "default"


@pytest.mark.asyncio
async def test_init_requires_endpoint(monkeypatch):
    monkeypatch.delenv("AZURE_APPCONFIG_ENDPOINT")

    with pytest.raises(RuntimeError, match="AZURE_APPCONFIG_ENDPOINT"):
        await app_config.init()
