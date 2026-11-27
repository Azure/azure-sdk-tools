"""Unit tests for Foundry web_search configuration helpers."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.azure_ai_foundry import (
    create_web_search_tool,
    get_web_search_allowed_domains,
)


def test_get_web_search_allowed_domains_reads_repo_yaml() -> None:
    domains = get_web_search_allowed_domains()
    assert "typespec.io" in domains
    assert "microsoft.com" in domains
    assert "pypi.org" in domains


def test_get_web_search_allowed_domains_raises_when_file_missing(tmp_path: Path) -> None:
    with patch("utils.azure_ai_foundry._WEB_SEARCH_DOMAINS_PATH", tmp_path / "missing.yaml"):
        with pytest.raises(FileNotFoundError, match="Web search allowlist config not found"):
            get_web_search_allowed_domains()


def test_get_web_search_allowed_domains_raises_when_empty(tmp_path: Path) -> None:
    cfg_file = tmp_path / "web-search-domains.yaml"
    cfg_file.write_text("allowed_domains: []\n", encoding="utf-8")
    with patch("utils.azure_ai_foundry._WEB_SEARCH_DOMAINS_PATH", cfg_file):
        with pytest.raises(ValueError, match="non-empty 'allowed_domains' list"):
            get_web_search_allowed_domains()


def test_create_web_search_tool_passes_allowed_domains() -> None:
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
