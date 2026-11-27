"""Unit tests for Foundry web_search configuration helpers."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.azure_ai_foundry import (
    create_web_search_tool,
    get_web_search_allowed_domains,
)


def test_get_web_search_allowed_domains_normalizes_entries() -> None:
    def fake_cfg(key: str, default: str = "") -> str:
        if key == "WEB_SEARCH_ALLOWED_DOMAINS":
            return "https://learn.microsoft.com/, .typespec.io, learn.microsoft.com"
        return default

    with patch("utils.azure_ai_foundry.cfg", side_effect=fake_cfg):
        assert get_web_search_allowed_domains() == [
            "learn.microsoft.com",
            "typespec.io",
        ]


def test_get_web_search_allowed_domains_returns_none_when_unset() -> None:
    with patch("utils.azure_ai_foundry.cfg", return_value=""):
        assert get_web_search_allowed_domains() is None


def test_create_web_search_tool_passes_allowed_domains_when_configured() -> None:
    mock_client = MagicMock()
    with patch(
        "utils.azure_ai_foundry.get_web_search_allowed_domains",
        return_value=["typespec.io", "microsoft.com"],
    ):
        create_web_search_tool(mock_client)

    mock_client.get_web_search_tool.assert_called_once_with(
        search_context_size="medium",
        allowed_domains=["typespec.io", "microsoft.com"],
    )


def test_create_web_search_tool_omits_allowed_domains_when_unset() -> None:
    mock_client = MagicMock()
    with patch(
        "utils.azure_ai_foundry.get_web_search_allowed_domains",
        return_value=None,
    ):
        create_web_search_tool(mock_client)

    mock_client.get_web_search_tool.assert_called_once_with(
        search_context_size="medium",
    )
