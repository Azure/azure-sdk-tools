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
from urllib.parse import urljoin, urlparse

import httpx

from config.app_config import get as cfg
from models.web import FetchWebpageResult
from tools import tool

logger = logging.getLogger(__name__)

_DEFAULT_TIMEOUT_SECONDS = 8
_DEFAULT_MAX_CHARS = 4000
_MAX_ALLOWED_CHARS = 12000
_MAX_HEADINGS = 50
_MIN_ALLOWED_CHARS = 1000
_MAX_REDIRECTS = 5


class _PublicIPTransport(httpx.AsyncBaseTransport):
    """Resolve and pin requests to public IP addresses."""

    def __init__(self, transport: httpx.AsyncBaseTransport | None = None) -> None:
        self._transport = transport or httpx.AsyncHTTPTransport(
            http2=True,
            trust_env=False,
            limits=httpx.Limits(max_keepalive_connections=0),
        )

    async def handle_async_request(self, request: httpx.Request) -> httpx.Response:
        hostname = request.url.host
        try:
            addrinfos = socket.getaddrinfo(
                hostname, request.url.port, type=socket.SOCK_STREAM
            )
        except socket.gaierror as e:
            raise httpx.ConnectError(
                f"Could not resolve host {hostname}.", request=request
            ) from e

        resolved_ips = list(
            dict.fromkeys(
                ipaddress.ip_address(sockaddr[0])
                for family, _, _, _, sockaddr in addrinfos
                if family in {socket.AF_INET, socket.AF_INET6}
            )
        )
        if not resolved_ips or any(not ip.is_global for ip in resolved_ips):
            raise httpx.ConnectError(
                f"Host {hostname} resolved to a non-public IP address.",
                request=request,
            )

        extensions = dict(request.extensions)
        if request.url.scheme == "https":
            extensions["sni_hostname"] = hostname

        last_error: httpx.ConnectError | httpx.ConnectTimeout | None = None
        for resolved_ip in resolved_ips:
            pinned_request = httpx.Request(
                method=request.method,
                url=request.url.copy_with(host=str(resolved_ip)),
                headers=request.headers,
                stream=request.stream,
                extensions=extensions,
            )
            try:
                return await self._transport.handle_async_request(pinned_request)
            except (httpx.ConnectError, httpx.ConnectTimeout) as e:
                last_error = e

        assert last_error is not None
        raise last_error

    async def aclose(self) -> None:
        await self._transport.aclose()


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


def _get_allowed_domains() -> set[str] | None:
    """Return the configured domain allow-list, or ``None`` if unrestricted.

    Reads the ``WEB_FETCH_ALLOWED_DOMAINS`` key from App Configuration.
    The value is a comma-separated list of domain suffixes (e.g.
    ``learn.microsoft.com,aka.ms,pypi.org``).  When set, only URLs whose
    hostname matches one of these suffixes are permitted.
    """
    raw = cfg("WEB_FETCH_ALLOWED_DOMAINS", "")
    if not raw:
        return None
    return {d.strip().lower() for d in raw.split(",") if d.strip()}


def _is_domain_allowed(url: str) -> bool:
    """Return ``True`` if the URL's domain passes the allow-list check.

    If no allow-list is configured, all domains are accepted (deny-list
    mode via ``_is_public_url`` still applies).
    """
    allowed = _get_allowed_domains()
    if allowed is None:
        return True
    hostname = (urlparse(url).hostname or "").strip().lower()
    return any(hostname == d or hostname.endswith("." + d) for d in allowed)


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
        # Follow redirects manually so every hop is re-validated against the
        # SSRF allow-list (_is_public_url). httpx's built-in redirect handling
        # would only validate the original URL, letting an attacker bounce a
        # public URL to an internal address (CWE-918).
        async with httpx.AsyncClient(
            headers=headers,
            follow_redirects=False,
            timeout=httpx.Timeout(_DEFAULT_TIMEOUT_SECONDS),
            transport=_PublicIPTransport(),
            trust_env=False,
        ) as client:
            current_url = url
            redirects = 0
            while True:
                response = await client.get(current_url)
                if not (300 <= response.status_code < 400):
                    break

                location = response.headers.get("location")
                if not location:
                    break

                if redirects >= _MAX_REDIRECTS:
                    return FetchWebpageResult(
                        success=False,
                        url=url,
                        resolved_url=current_url,
                        status_code=response.status_code,
                        content_type=response.headers.get("content-type", ""),
                        content_excerpt="",
                        error=f"Too many redirects (>{_MAX_REDIRECTS}).",
                    )

                next_url = urljoin(current_url, location)
                if not _is_public_url(next_url) or not _is_domain_allowed(next_url):
                    return FetchWebpageResult(
                        success=False,
                        url=url,
                        resolved_url=next_url,
                        status_code=response.status_code,
                        content_type=response.headers.get("content-type", ""),
                        content_excerpt="",
                        error="Redirect target is not a public http/https URL.",
                    )

                redirects += 1
                current_url = next_url

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
        if not _is_domain_allowed(normalized_url):
            raise ValueError(
                "Domain not in the allowed list for web_fetch. "
                "Use web_search to find information on other domains."
            )

        bounded_max_chars = max(
            _MIN_ALLOWED_CHARS, min(int(max_chars), _MAX_ALLOWED_CHARS)
        )
        return await _fetch_async(normalized_url, bounded_max_chars)
