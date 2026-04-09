"""Web retrieval tools for deterministic URL fetches.

These tools complement web search for cases where the user provides a direct URL
and expects exact content retrieval (for example, llms.txt endpoints).
"""

from __future__ import annotations

import asyncio
import ipaddress
import re
from html.parser import HTMLParser
from typing import Annotated
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

from models.web import FetchWebpageResult
from tools import tool

_DEFAULT_TIMEOUT_SECONDS = 15
_DEFAULT_MAX_CHARS = 6000
_MAX_ALLOWED_CHARS = 20000


class _HtmlOutlineParser(HTMLParser):
    """Collect title and h1-h3 headings from HTML content."""

    def __init__(self) -> None:
        super().__init__()
        self.title = ""
        self.headings: list[str] = []
        self._current_tag: str | None = None
        self._buffer: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in {"title", "h1", "h2", "h3"}:
            self._current_tag = tag
            self._buffer = []

    def handle_data(self, data: str) -> None:
        if self._current_tag:
            self._buffer.append(data)

    def handle_endtag(self, tag: str) -> None:
        if tag != self._current_tag:
            return
        text = " ".join(part.strip() for part in self._buffer if part.strip())
        if text:
            if tag == "title":
                self.title = text
            else:
                self.headings.append(text)
        self._current_tag = None
        self._buffer = []


def _is_public_url(url: str) -> bool:
    parsed = urlparse(url)
    if parsed.scheme not in {"http", "https"}:
        return False

    hostname = (parsed.hostname or "").strip().lower()
    if not hostname:
        return False

    if hostname in {"localhost", "127.0.0.1", "::1"}:
        return False

    try:
        ip = ipaddress.ip_address(hostname)
        if ip.is_private or ip.is_loopback or ip.is_link_local:
            return False
    except ValueError:
        # Non-IP hostnames are allowed.
        pass

    return True


def _trim_excerpt(text: str, max_chars: int) -> str:
    cleaned = re.sub(r"\s+", " ", text).strip()
    return cleaned[:max_chars]


def _fetch_sync(url: str, max_chars: int) -> FetchWebpageResult:
    headers_browser_like = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) "
            "Chrome/123.0.0.0 Safari/537.36"
        ),
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "en-US,en;q=0.9",
        "Cache-Control": "no-cache",
        "Pragma": "no-cache",
    }

    raw = b""
    final_url = url
    content_type = ""
    charset = "utf-8"
    status_code: int | None = None

    try:
        req = Request(url, headers=headers_browser_like)
        with urlopen(req, timeout=_DEFAULT_TIMEOUT_SECONDS) as response:
            final_url = response.geturl()
            content_type = response.headers.get("Content-Type", "")
            charset = response.headers.get_content_charset() or "utf-8"
            raw = response.read()
            status_code = getattr(response, "status", None)
    except HTTPError as e:
        status_code = e.code
    except URLError as e:
        return FetchWebpageResult(
            success=False,
            url=url,
            resolved_url=final_url,
            status_code=status_code,
            content_type=content_type,
            content_excerpt="",
            error=f"Network error: {e.reason}",
        )

    if not raw:
        return FetchWebpageResult(
            success=False,
            url=url,
            resolved_url=final_url,
            status_code=status_code,
            content_type=content_type,
            content_excerpt="",
            error=(
                f"HTTP fetch blocked with status {status_code}. "
                "The site may block automated requests."
                if status_code
                else "HTTP fetch failed."
            ),
        )

    text = raw.decode(charset, errors="replace")
    excerpt = _trim_excerpt(text, max_chars)

    if "html" in content_type.lower():
        parser = _HtmlOutlineParser()
        parser.feed(text)
        title = parser.title
        headings = parser.headings[:50]
    else:
        title = ""
        headings = []

    used_llms_txt_hint = urlparse(final_url).path.endswith("/llms.txt")

    return FetchWebpageResult(
        success=True,
        url=url,
        resolved_url=final_url,
        status_code=status_code,
        content_type=content_type,
        title=title,
        headings=headings,
        content_excerpt=excerpt,
        used_llms_txt_hint=used_llms_txt_hint,
    )


class WebTools:
    """Tools for deterministic retrieval of public web content."""

    @tool
    async def web_fetch(
        self,
        *,
        url: Annotated[
            str,
            "Public URL to fetch directly.",
        ],
        max_chars: Annotated[
            int,
            "Maximum number of response characters to return in content_excerpt.",
        ] = _DEFAULT_MAX_CHARS,
    ) -> FetchWebpageResult:
        """Fetch and summarize a public webpage, markdown file, or llms.txt endpoint."""
        normalized_url = (url or "").strip()
        if not _is_public_url(normalized_url):
            raise ValueError("Only public http/https URLs are allowed.")

        # Block GitHub URLs — they throttle/403 automated requests.
        # Redirect the agent to use GitHub MCP tools instead.
        _github_hosts = {"github.com", "api.github.com"}
        parsed_host = urlparse(normalized_url).hostname or ""
        if parsed_host.lower() in _github_hosts:
            return FetchWebpageResult(
                success=False,
                url=normalized_url,
                error=(
                    "GitHub URLs are blocked in web_fetch due to rate limiting. "
                    "Use GitHub MCP tools instead to access this content."
                ),
            )

        bounded_max_chars = max(1000, min(int(max_chars), _MAX_ALLOWED_CHARS))
        return await asyncio.to_thread(_fetch_sync, normalized_url, bounded_max_chars)
