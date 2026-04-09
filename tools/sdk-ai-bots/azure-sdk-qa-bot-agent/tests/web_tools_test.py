"""Unit tests for web retrieval tools."""

from __future__ import annotations

import sys
from pathlib import Path
from urllib.error import HTTPError

import pytest

# Ensure the project root is on sys.path so ``tools`` resolves.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from tools.web_tools import WebTools, _is_public_url


class _FakeResponse:
    def __init__(self, url: str, body: str, content_type: str) -> None:
        self._url = url
        self._body = body.encode("utf-8")
        self.headers = _FakeHeaders(content_type)

    def geturl(self) -> str:
        return self._url

    def read(self) -> bytes:
        return self._body

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        return None


class _FakeHeaders(dict):
    def __init__(self, content_type: str) -> None:
        super().__init__({"Content-Type": content_type})

    def get_content_charset(self) -> str:
        return "utf-8"


def test_is_public_url_blocks_private_hosts() -> None:
    assert _is_public_url("https://typespec.io/docs/llms.txt") is True
    assert _is_public_url("http://localhost:8000") is False
    assert _is_public_url("http://127.0.0.1:8080") is False
    assert _is_public_url("ftp://typespec.io/docs/llms.txt") is False


@pytest.mark.asyncio
async def test_web_fetch_parses_html(monkeypatch: pytest.MonkeyPatch) -> None:
    html = """<html><head><title>Decorators</title></head><body><h1>Main</h1><h2>Details</h2></body></html>"""

    def _fake_urlopen(req, timeout=15):
        return _FakeResponse(
            "https://typespec.io/docs/language-basics/decorators/",
            html,
            "text/html; charset=utf-8",
        )

    monkeypatch.setattr("tools.web_tools.urlopen", _fake_urlopen)

    result = await WebTools().web_fetch(
        url="https://typespec.io/docs/language-basics/decorators/"
    )
    assert result.title == "Decorators"
    assert result.headings == ["Main", "Details"]
    assert result.used_llms_txt_hint is False


@pytest.mark.asyncio
async def test_web_fetch_marks_llms_txt(monkeypatch: pytest.MonkeyPatch) -> None:
    body = "# TypeSpec Documentation\n- [Decorators](https://typespec.io/docs/language-basics/decorators/index.html.md)"

    def _fake_urlopen(req, timeout=15):
        return _FakeResponse(
            "https://typespec.io/docs/llms.txt",
            body,
            "text/plain; charset=utf-8",
        )

    monkeypatch.setattr("tools.web_tools.urlopen", _fake_urlopen)

    result = await WebTools().web_fetch(url="https://typespec.io/docs/llms.txt")
    assert result.used_llms_txt_hint is True
    assert "TypeSpec Documentation" in result.content_excerpt


@pytest.mark.asyncio
async def test_web_fetch_returns_error_on_http_forbidden(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def _fake_urlopen(req, timeout=15):
        raise HTTPError(req.full_url, 403, "Forbidden", hdrs=None, fp=None)

    monkeypatch.setattr("tools.web_tools.urlopen", _fake_urlopen)

    result = await WebTools().web_fetch(
        url="https://www.npmjs.com/package/@azure-tools/typespec-azure-core"
    )
    assert result.success is False
    assert result.status_code == 403
    assert result.error is not None
