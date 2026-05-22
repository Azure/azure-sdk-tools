"""Web retrieval tools for deterministic URL fetches.

These tools complement web search for cases where the user provides a direct URL
and expects exact content retrieval (for example, llms.txt endpoints).
"""

from __future__ import annotations

import ipaddress
import logging
import re
import socket
from html.parser import HTMLParser
from typing import Annotated
from urllib.parse import urlparse

import httpx

from models.web import FetchWebpageResult
from tools import tool

logger = logging.getLogger(__name__)

_DEFAULT_TIMEOUT_SECONDS = 8
_DEFAULT_MAX_CHARS = 4000
_MAX_ALLOWED_CHARS = 12000
_MAX_HEADINGS = 50
_MIN_ALLOWED_CHARS = 1000


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


class _HtmlTextExtractor(HTMLParser):
    """Extract visible text from HTML, skipping script/style/nav/footer noise."""

    _SKIP_TAGS = frozenset(
        {"script", "style", "noscript", "nav", "footer", "header", "svg", "head"}
    )

    def __init__(self) -> None:
        super().__init__()
        self.parts: list[str] = []
        self._skip_depth = 0

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in self._SKIP_TAGS:
            self._skip_depth += 1

    def handle_endtag(self, tag: str) -> None:
        if tag in self._SKIP_TAGS and self._skip_depth > 0:
            self._skip_depth -= 1

    def handle_data(self, data: str) -> None:
        if self._skip_depth == 0:
            stripped = data.strip()
            if stripped:
                self.parts.append(stripped)

    def get_text(self) -> str:
        return " ".join(self.parts)


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
        # Only globally routable IPs are allowed.
        if not ip.is_global:
            return False
    except ValueError:
        # Resolve hostnames and reject if *any* address is non-public.
        try:
            addrinfos = socket.getaddrinfo(hostname, None, type=socket.SOCK_STREAM)
        except socket.gaierror:
            return False

        if not addrinfos:
            return False

        for family, _, _, _, sockaddr in addrinfos:
            if family == socket.AF_INET:
                resolved_ip = ipaddress.ip_address(sockaddr[0])
            elif family == socket.AF_INET6:
                resolved_ip = ipaddress.ip_address(sockaddr[0])
            else:
                continue

            if not resolved_ip.is_global:
                return False

    return True


def _trim_excerpt(text: str, max_chars: int) -> str:
    cleaned = re.sub(r"\s+", " ", text).strip()
    return cleaned[:max_chars]


async def _fetch_async(url: str, max_chars: int) -> FetchWebpageResult:
    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) "
            "Chrome/131.0.0.0 Safari/537.36"
        ),
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "en-US,en;q=0.9",
        "Accept-Encoding": "gzip, deflate, br",
        "Cache-Control": "no-cache",
        "Pragma": "no-cache",
    }

    try:
        async with httpx.AsyncClient(
            headers=headers,
            follow_redirects=True,
            timeout=httpx.Timeout(_DEFAULT_TIMEOUT_SECONDS),
            http2=True,
        ) as client:
            response = await client.get(url)
            final_url = str(response.url)
            status_code = response.status_code
            content_type = response.headers.get("content-type", "")

            if status_code >= 400:
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
                    ),
                )

            raw = response.content
            charset = response.charset_encoding or "utf-8"
    except httpx.HTTPError as e:
        logger.warning("web_fetch failed for %s: %s", url, e)
        return FetchWebpageResult(
            success=False,
            url=url,
            resolved_url=url,
            status_code=None,
            content_type="",
            content_excerpt="",
            error=f"Network error: {e}",
        )

    text = raw.decode(charset, errors="replace")

    if "html" in content_type.lower():
        parser = _HtmlOutlineParser()
        parser.feed(text)
        title = parser.title
        headings = parser.headings[:_MAX_HEADINGS]

        # Extract visible text only, stripping tags and noise
        extractor = _HtmlTextExtractor()
        extractor.feed(text)
        excerpt = _trim_excerpt(extractor.get_text(), max_chars)
    else:
        title = ""
        headings = []
        excerpt = _trim_excerpt(text, max_chars)

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

        bounded_max_chars = max(
            _MIN_ALLOWED_CHARS, min(int(max_chars), _MAX_ALLOWED_CHARS)
        )
        return await _fetch_async(normalized_url, bounded_max_chars)
