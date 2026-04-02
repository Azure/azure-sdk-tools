"""Text preprocessing utilities.

Ports the preprocessing logic from the Go backend
(azure-sdk-qa-bot-backend/service/preprocess/service.go) to Python.

Handles:
  - HTML entity and Unicode escape decoding
  - HTML tag removal (preserving anchor links)
  - Keyword replacement (common + tenant-specific)
"""

from __future__ import annotations

import html
import logging
import re
from urllib.parse import unquote

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Common keyword replacements (mirrored from Go backend model/constants.go)
# ---------------------------------------------------------------------------

COMMON_KEYWORD_REPLACE_MAP: dict[str, str] = {
    "tsp": "typespec",
    "oa3": "openapi3",
    "tcgc": "typespec-client-generator-core",
    "dpg": "data plane",
    "mpg": "management plane",
    "arm": "Azure Resource Manager",
    "common types": "Common Types to Azure Resource Manager (ARM)",
    "common-types": "Common Types to Azure Resource Manager (ARM)",
    "swagger": "Open API",
}

# Precompiled regex patterns
_UNICODE_ESCAPE_RE = re.compile(r"\\u([0-9a-fA-F]{4})")
_LINK_RE = re.compile(r'<a\s+(?:[^>]*?\s+)?href=["\']([^"\']+)["\'][^>]*>([^<]+)</a>')
_HTML_TAG_RE = re.compile(r"<[^>]*>")
_WHITESPACE_RE = re.compile(r"\s+")


# ---------------------------------------------------------------------------
# Decoding helpers
# ---------------------------------------------------------------------------


def decode_html_content(text: str) -> str:
    """Decode HTML entities, Unicode escape sequences, and URL encoding."""
    # Decode HTML entities (&lt;, &amp;, &nbsp; etc.)
    decoded = html.unescape(text)

    # Decode common Unicode escape sequences
    _COMMON_UNICODE = {
        "\\u003c": "<",
        "\\u003e": ">",
        "\\u0026": "&",
        "\\u0027": "'",
        "\\u0022": '"',
        "\\u002f": "/",
        "\\u003d": "=",
        "\\u0020": " ",
        "\\u00a0": " ",  # non-breaking space -> regular space
        "\\u000a": "\n",
        "\\u000d": "\r",
        "\\u0009": "\t",
    }
    for escape, replacement in _COMMON_UNICODE.items():
        decoded = decoded.replace(escape, replacement)

    # Handle remaining Unicode escapes via regex
    def _replace_unicode(m: re.Match) -> str:
        try:
            return chr(int(m.group(1), 16))
        except ValueError:
            return m.group(0)

    decoded = _UNICODE_ESCAPE_RE.sub(_replace_unicode, decoded)

    # Decode URL encoding (%20 -> space, %E2%80%A6 -> ..., etc.)
    try:
        decoded = unquote(decoded)
    except Exception:
        pass  # keep as-is on decode failure

    return decoded


def clean_html_tags(text: str) -> str:
    """Remove HTML tags while preserving anchor link text and URLs."""
    # Step 1: Extract <a> tags, replace with placeholders
    links: list[str] = []

    def _replace_link(m: re.Match) -> str:
        href, label = m.group(1), m.group(2)
        link_html = f'<a href="{href}">{label}</a>'
        links.append(link_html)
        return f"___LINK_PLACEHOLDER_{len(links) - 1}___"

    cleaned = _LINK_RE.sub(_replace_link, text)

    # Step 2: Strip all remaining HTML tags
    cleaned = _HTML_TAG_RE.sub("", cleaned)

    # Step 3: Restore anchor tags from placeholders
    for i, link in enumerate(links):
        cleaned = cleaned.replace(f"___LINK_PLACEHOLDER_{i}___", link)

    # Collapse whitespace
    cleaned = _WHITESPACE_RE.sub(" ", cleaned).strip()
    return cleaned


def preprocess_html_content(text: str) -> str:
    """Decode and clean HTML content if HTML markers are detected."""
    html_markers = ("\\u003c", "&lt;", "<", "&amp;", "\\u0026")
    if not any(marker in text for marker in html_markers):
        return text

    logger.debug("Detected HTML content, preprocessing...")
    decoded = decode_html_content(text)
    cleaned = clean_html_tags(decoded)
    return cleaned


# ---------------------------------------------------------------------------
# Keyword replacement
# ---------------------------------------------------------------------------


def replace_keywords(text: str) -> str:
    """Apply common keyword replacements.

    Keywords are matched as whole words surrounded by spaces, matching
    the Go backend behaviour (`` " keyword " `` -> `` " replacement " ``).
    """
    lowered = text.lower()

    for keyword, replacement in COMMON_KEYWORD_REPLACE_MAP.items():
        lowered = lowered.replace(f" {keyword} ", f" {replacement} ")

    return lowered


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------


def preprocess_message(text: str) -> str:
    """Full preprocessing pipeline: HTML handling -> keyword replacement.

    This mirrors the Go backend's ``PreprocessHTMLContent`` +
    ``PreprocessInput`` flow.
    """
    result = preprocess_html_content(text)
    result = replace_keywords(result)
    return result
