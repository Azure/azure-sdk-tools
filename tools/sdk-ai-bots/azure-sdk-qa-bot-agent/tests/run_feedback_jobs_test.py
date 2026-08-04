"""Offline tests for feedback-job orchestration helpers."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import scripts.run_feedback_jobs as feedback_jobs


def _configure_storage(monkeypatch) -> None:
    values = {
        "STORAGE_CONFIG_CONTAINER": "config",
        "CHANNEL_CONFIG_BLOB": "channel.yaml",
    }
    monkeypatch.setattr(feedback_jobs.app_config, "get", values.get)


@pytest.mark.asyncio
async def test_load_excluded_channels_filters_testing_suffix(monkeypatch):
    _configure_storage(monkeypatch)
    monkeypatch.setattr(
        feedback_jobs,
        "download_blob",
        AsyncMock(
            return_value=(
                b"channels:\n"
                b"  - id: production\n"
                b"    name: TypeSpec\n"
                b"  - id: test\n"
                b"    name: TypeSpec Testing\n"
            )
        ),
    )

    assert await feedback_jobs._load_excluded_channels() == {"test"}


@pytest.mark.asyncio
async def test_load_excluded_channels_fails_closed_on_download_error(monkeypatch):
    _configure_storage(monkeypatch)
    monkeypatch.setattr(
        feedback_jobs,
        "download_blob",
        AsyncMock(side_effect=RuntimeError("unavailable")),
    )

    assert await feedback_jobs._load_excluded_channels() is None


@pytest.mark.parametrize(
    "data",
    [
        b"channels: invalid\n",
        b"missing_channels: []\n",
        b"channels:\n  - id: test\n",
        b"\xff",
    ],
)
@pytest.mark.asyncio
async def test_load_excluded_channels_fails_closed_on_invalid_shape(
    monkeypatch,
    data,
):
    _configure_storage(monkeypatch)
    monkeypatch.setattr(
        feedback_jobs,
        "download_blob",
        AsyncMock(return_value=data),
    )

    assert await feedback_jobs._load_excluded_channels() is None
