"""Unit tests for web retrieval tools."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock, patch

import httpx
import pytest

# Ensure the project root is on sys.path so ``tools`` resolves.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from tools.web_tools import WebTools, _is_public_url, _HtmlTextExtractor


def _make_response(
    url: str, body: str, content_type: str, status: int = 200
) -> httpx.Response:
    """Build a fake httpx.Response for mocking."""
    return httpx.Response(
        status_code=status,
        headers={"content-type": content_type},
        content=body.encode("utf-8"),
        request=httpx.Request("GET", url),
    )


def test_is_public_url_blocks_private_hosts() -> None:
    with patch("tools.web_tools.socket.getaddrinfo") as mock_getaddrinfo:
        mock_getaddrinfo.return_value = [
            (2, 1, 6, "", ("93.184.216.34", 0)),
        ]
        assert _is_public_url("https://typespec.io/docs/llms.txt") is True

    assert _is_public_url("http://localhost:8000") is False
    assert _is_public_url("http://127.0.0.1:8080") is False
    assert _is_public_url("ftp://typespec.io/docs/llms.txt") is False


def test_is_public_url_blocks_hostname_resolving_to_private_ip() -> None:
    with patch("tools.web_tools.socket.getaddrinfo") as mock_getaddrinfo:
        mock_getaddrinfo.return_value = [
            (2, 1, 6, "", ("169.254.169.254", 0)),
        ]
        assert _is_public_url("https://example.com/path") is False


def test_html_text_extractor_strips_tags() -> None:
    html = (
        "<html><head><style>body{color:red}</style><title>T</title></head>"
        "<body><nav>Menu</nav><h1>Hello</h1><p>World</p>"
        "<script>alert(1)</script><footer>F</footer></body></html>"
    )
    extractor = _HtmlTextExtractor()
    extractor.feed(html)
    text = extractor.get_text()
    assert "Hello" in text
    assert "World" in text
    assert "Menu" not in text  # nav stripped
    assert "alert" not in text  # script stripped
    assert "color:red" not in text  # style stripped
    assert "F" not in text  # footer stripped


@pytest.mark.asyncio
async def test_web_fetch_parses_html() -> None:
    html = """<html><head><title>Decorators</title></head><body><h1>Main</h1><h2>Details</h2><p>Some content here</p></body></html>"""
    url = "https://typespec.io/docs/language-basics/decorators/"

    fake_resp = _make_response(url, html, "text/html; charset=utf-8")

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient:
        instance = AsyncMock()
        instance.get.return_value = fake_resp
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        result = await WebTools().web_fetch(url=url)

    assert result.title == "Decorators"
    assert result.headings == ["Main", "Details"]
    assert result.used_llms_txt_hint is False
    assert "Some content here" in result.content_excerpt
    assert "<html" not in result.content_excerpt


@pytest.mark.asyncio
async def test_web_fetch_marks_llms_txt() -> None:
    body = "# TypeSpec Documentation\n- [Decorators](https://typespec.io/docs/language-basics/decorators/index.html.md)"
    url = "https://typespec.io/docs/llms.txt"

    fake_resp = _make_response(url, body, "text/plain; charset=utf-8")

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient:
        instance = AsyncMock()
        instance.get.return_value = fake_resp
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        result = await WebTools().web_fetch(url=url)

    assert result.used_llms_txt_hint is True
    assert "TypeSpec Documentation" in result.content_excerpt


@pytest.mark.asyncio
async def test_web_fetch_returns_error_on_http_forbidden() -> None:
    url = "https://www.npmjs.com/package/@azure-tools/typespec-azure-core"
    fake_resp = _make_response(url, "Forbidden", "text/html", status=403)

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient:
        instance = AsyncMock()
        instance.get.return_value = fake_resp
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        result = await WebTools().web_fetch(url=url)

    assert result.success is False
    assert result.status_code == 403
    assert result.error is not None
