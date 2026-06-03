"""Unit tests for web retrieval tools."""

from __future__ import annotations

import socket
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

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient, patch(
        "tools.web_tools.socket.getaddrinfo",
        return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("104.16.0.1", 0)),
        ],
    ):
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

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient, patch(
        "tools.web_tools.socket.getaddrinfo",
        return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("104.16.0.1", 0)),
        ],
    ):
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

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient, patch(
        "tools.web_tools.socket.getaddrinfo",
        return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("104.16.0.1", 0)),
        ],
    ):
        instance = AsyncMock()
        instance.get.return_value = fake_resp
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        result = await WebTools().web_fetch(url=url)

    assert result.success is False
    assert result.status_code == 403
    assert result.error is not None


@pytest.mark.asyncio
async def test_web_fetch_blocks_redirect_to_internal_address() -> None:
    """An attacker's public URL that 302s to an internal address must be blocked."""
    initial_url = "https://attacker.example.com/redirect"
    redirect_resp = httpx.Response(
        status_code=302,
        headers={"location": "http://169.254.169.254/metadata/instance"},
        content=b"",
        request=httpx.Request("GET", initial_url),
    )

    # First call resolves attacker.example.com to a public IP, then the
    # follow-up validation of the redirect target must reject 169.254.169.254
    # (a link-local IMDS address).
    getaddrinfo_results = {
        "attacker.example.com": [
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("93.184.216.34", 0)),
        ],
    }

    def fake_getaddrinfo(host, *args, **kwargs):
        if host in getaddrinfo_results:
            return getaddrinfo_results[host]
        # Default: resolve to itself if it's an IP literal, otherwise raise.
        try:
            socket.inet_aton(host)
            return [(socket.AF_INET, socket.SOCK_STREAM, 0, "", (host, 0))]
        except OSError:
            raise socket.gaierror(f"unknown host {host}")

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient, patch(
        "tools.web_tools.socket.getaddrinfo", side_effect=fake_getaddrinfo
    ):
        instance = AsyncMock()
        instance.get.return_value = redirect_resp
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        result = await WebTools().web_fetch(url=initial_url)

    assert result.success is False
    assert result.error == "Redirect to non-public URL blocked."
    assert "169.254.169.254" in result.resolved_url
    # Internal response body must NOT be exposed to the caller.
    assert result.content_excerpt == ""


@pytest.mark.asyncio
async def test_web_fetch_follows_redirect_to_public_url() -> None:
    """Redirects between public URLs should be transparently followed."""
    initial_url = "https://typespec.io/old"
    final_url = "https://typespec.io/new"

    redirect_resp = httpx.Response(
        status_code=301,
        headers={"location": final_url},
        content=b"",
        request=httpx.Request("GET", initial_url),
    )
    final_resp = _make_response(
        final_url,
        "<html><head><title>New</title></head><body><h1>Hi</h1></body></html>",
        "text/html; charset=utf-8",
    )

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient, patch(
        "tools.web_tools.socket.getaddrinfo",
        return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("104.16.0.1", 0)),
        ],
    ):
        instance = AsyncMock()
        instance.get.side_effect = [redirect_resp, final_resp]
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        result = await WebTools().web_fetch(url=initial_url)

    assert result.success is True
    assert result.resolved_url == final_url
    assert result.title == "New"


@pytest.mark.asyncio
async def test_web_fetch_blocks_redirect_loop_after_max_hops() -> None:
    """A redirect chain longer than _MAX_REDIRECTS must terminate with an error."""
    url = "https://typespec.io/loop"
    loop_resp = httpx.Response(
        status_code=302,
        headers={"location": url},
        content=b"",
        request=httpx.Request("GET", url),
    )

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient, patch(
        "tools.web_tools.socket.getaddrinfo",
        return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("104.16.0.1", 0)),
        ],
    ):
        instance = AsyncMock()
        instance.get.return_value = loop_resp
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        result = await WebTools().web_fetch(url=url)

    assert result.success is False
    assert result.error == "Too many redirects."


@pytest.mark.asyncio
async def test_web_fetch_pins_connection_to_validated_ip() -> None:
    """The HTTP request must go to the resolved IP, not the hostname, with the
    original Host header and sni_hostname extension preserved. This closes the
    DNS-rebinding TOCTOU window between validation and connection."""
    url = "https://example.com/path"
    fake_resp = _make_response(url, "<html></html>", "text/html; charset=utf-8")

    with patch("tools.web_tools.httpx.AsyncClient") as MockClient, patch(
        "tools.web_tools.socket.getaddrinfo",
        return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("93.184.216.34", 0)),
        ],
    ):
        instance = AsyncMock()
        instance.get.return_value = fake_resp
        instance.__aenter__ = AsyncMock(return_value=instance)
        instance.__aexit__ = AsyncMock(return_value=False)
        MockClient.return_value = instance

        await WebTools().web_fetch(url=url)

    call_args = instance.get.call_args
    pinned_url = call_args.args[0]
    assert pinned_url == "https://93.184.216.34/path"
    assert call_args.kwargs["headers"] == {"Host": "example.com"}
    assert call_args.kwargs["extensions"] == {"sni_hostname": "example.com"}
