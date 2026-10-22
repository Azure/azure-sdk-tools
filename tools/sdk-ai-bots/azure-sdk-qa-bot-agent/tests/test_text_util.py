"""Tests for text_util HTML preprocessing — link preservation."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.text_util import clean_html_tags, preprocess_message

# ---------------------------------------------------------------------------
# Fixtures: realistic Teams HTML payloads
# ---------------------------------------------------------------------------

# Teams wraps link text in <span> inside <a>
TEAMS_HTML_WITH_PR_LINK = (
    '<p><span style="font-size:inherit">Hello all,&nbsp;</span></p>\n'
    '<p><span style="font-size:inherit">I am trying to get the PR - </span>'
    '<a href="https://github.com/Azure/azure-rest-api-specs/pull/41376" '
    'rel="noreferrer noopener" '
    'title="https://github.com/azure/azure-rest-api-specs/pull/41376" '
    'target="_blank">'
    '<span style="font-size:inherit">Fix BotService schema: update models.tsp '
    "and regenerate swagger files by Kundanha \u00b7 Pull Request #4\u2026</span></a></p>\n"
    '<p><span style="font-size:inherit">Approved from my teammate.</span></p>'
)

# Simple <a> without nested tags
SIMPLE_HTML_WITH_LINK = (
    "<p>Check this link: "
    '<a href="https://example.com/doc">Some Doc</a> for details.</p>'
)

# Multiple links in one message
MULTI_LINK_HTML = (
    '<p>See <a href="https://github.com/Azure/PR/1">PR #1</a> and '
    '<a href="https://github.com/Azure/PR/2"><span>PR #2</span></a></p>'
)

# No links at all
PLAIN_HTML = '<p><span style="font-size:inherit">No links here.</span></p>'

# Plain text (no HTML markers)
PLAIN_TEXT = "Just a plain text message with no HTML."


# ---------------------------------------------------------------------------
# Tests: clean_html_tags
# ---------------------------------------------------------------------------


def test_clean_html_tags_preserves_teams_pr_link() -> None:
    """Teams <a> with nested <span> should become markdown link."""
    result = clean_html_tags(TEAMS_HTML_WITH_PR_LINK)
    assert (
        "[Fix BotService schema: update models.tsp and regenerate swagger files by Kundanha \u00b7 Pull Request #4\u2026]"
        in result
    )
    assert "(https://github.com/Azure/azure-rest-api-specs/pull/41376)" in result


def test_clean_html_tags_preserves_simple_link() -> None:
    """Simple <a> without nested tags should become markdown link."""
    result = clean_html_tags(SIMPLE_HTML_WITH_LINK)
    assert "[Some Doc](https://example.com/doc)" in result


def test_clean_html_tags_preserves_multiple_links() -> None:
    """Multiple links in one message should all be preserved."""
    result = clean_html_tags(MULTI_LINK_HTML)
    assert "[PR #1](https://github.com/Azure/PR/1)" in result
    assert "[PR #2](https://github.com/Azure/PR/2)" in result


def test_clean_html_tags_strips_non_link_html() -> None:
    """Non-link HTML tags should be stripped."""
    result = clean_html_tags(PLAIN_HTML)
    assert "<" not in result
    assert "No links here." in result


# ---------------------------------------------------------------------------
# Tests: preprocess_message (full pipeline)
# ---------------------------------------------------------------------------


def test_preprocess_message_preserves_pr_url() -> None:
    """Full pipeline should keep the PR URL in markdown format with original casing."""
    result = preprocess_message(TEAMS_HTML_WITH_PR_LINK)
    assert "https://github.com/Azure/azure-rest-api-specs/pull/41376" in result


def test_preprocess_message_applies_keyword_replacement() -> None:
    """Keyword replacement should still apply (e.g. swagger -> Open API)."""
    result = preprocess_message(TEAMS_HTML_WITH_PR_LINK)
    assert "Open API" in result


def test_preprocess_message_plain_text_passthrough() -> None:
    """Plain text without HTML markers should pass through unchanged (except keywords/lowercase)."""
    result = preprocess_message(PLAIN_TEXT)
    assert "plain text message" in result


@pytest.mark.parametrize("message,expected", [
    ("[branch:feature/foo] question", "question"),
    ("[branch:feature/foo]question", "question"),
    ("[branch:feature/foo]问题", "问题"),
    ("title: [branch:feature/foo]question", "title: question"),
    ("<p>[branch:feature/foo] question</p>", "question"),
    ("<p>[branch:feature/foo]</p><p>question</p>", "question"),
    ("[branch:feature/foo]<br />question", "question"),
    ("<div>[branch:feature/foo]</div><div>question</div>", "question"),
    ("<p><span>[branch:feature/</span><b>foo]</b></p><p>question</p>", "question"),
    (
        'title: [branch:jiaqizhang-dev-api-stewardship-chatbot] test message\n\n<p>test</p>"',
        'title: test message test"',
    ),
    ("title: [branch:feature/foo] question", "title: question"),
    ("title: question\n\n<p>test</p>", "title: question test"),
    ("Explain [branch:feature/foo] in this example", "Explain [branch:feature/foo] in this example"),
    ("[agent:v42] question", "[agent:v42] question"),
])
def test_preprocess_message_branch_prefix(message: str, expected: str) -> None:
    assert preprocess_message(message) == expected
