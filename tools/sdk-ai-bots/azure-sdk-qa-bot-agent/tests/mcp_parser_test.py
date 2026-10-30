"""Regression tests for MCP text and embedded resource parsing."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest
from mcp import types as mcp_types

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from tools import _MCP_MAX_OUTPUT_CHARS, truncating_mcp_parser
from tools.github_mcp_tools import _BODY_REDACTION_NOTICE, _github_mcp_parser

_RESOURCE_URI = "https://example.com/docs/guide.md"
_RESOURCE_PREFIX = f"Resource URI: {_RESOURCE_URI}\n"


def _text_resource(text: str) -> mcp_types.EmbeddedResource:
    return mcp_types.EmbeddedResource(
        type="resource",
        resource=mcp_types.TextResourceContents(
            uri=_RESOURCE_URI, mimeType="text/markdown", text=text
        ),
    )


@pytest.fixture(params=[truncating_mcp_parser, _github_mcp_parser], ids=["shared", "github"])
def parser(request):
    return request.param


def test_preserves_acknowledgement_and_embedded_file(parser):
    acknowledgement = "successfully downloaded text file (SHA: abc123)"
    body = "# Recorded Testing\n\nRun the tests in playback mode."
    result = mcp_types.CallToolResult.model_validate(
        {
            "content": [
                {"type": "text", "text": acknowledgement},
                {
                    "type": "resource",
                    "resource": {
                        "uri": _RESOURCE_URI,
                        "mimeType": "text/markdown",
                        "text": body,
                    },
                },
                {"type": "text", "text": "Trailing text"},
            ],
            "isError": False,
        }
    )

    output = parser(result)
    if parser is _github_mcp_parser:
        parsed = json.loads(output)
        assert parsed["complete"]
        assert [block["text"] for block in parsed["content"]] == [
            acknowledgement, body, "Trailing text"
        ]
        assert parsed["content"][1]["uri"] == _RESOURCE_URI
    else:
        assert output == f"{acknowledgement}\n{_RESOURCE_PREFIX}{body}\nTrailing text"


def test_preserves_resource_only_result(parser):
    body = "# Guide\n\nThe full document body."

    output = parser(mcp_types.CallToolResult(content=[_text_resource(body)]))
    if parser is _github_mcp_parser:
        parsed = json.loads(output)
        assert parsed["complete"]
        assert parsed["content"][0]["text"] == body
        assert parsed["content"][0]["uri"] == _RESOURCE_URI
    else:
        assert output == _RESOURCE_PREFIX + body


@pytest.mark.parametrize("embedded", [False, True], ids=["text", "resource"])
@pytest.mark.parametrize(
    "length", [_MCP_MAX_OUTPUT_CHARS - 1, _MCP_MAX_OUTPUT_CHARS, _MCP_MAX_OUTPUT_CHARS + 1]
)
def test_truncation_boundary(parser, embedded, length, caplog):
    prefix = _RESOURCE_PREFIX if embedded else ""
    body = "x" * (length - len(prefix))
    content = _text_resource(body) if embedded else mcp_types.TextContent(type="text", text=body)
    expected = prefix + body
    if length > _MCP_MAX_OUTPUT_CHARS:
        expected = expected[:_MCP_MAX_OUTPUT_CHARS] + "\n... [truncated]"

    with caplog.at_level("INFO", logger="tools"):
        actual = parser(mcp_types.CallToolResult(content=[content]))

    if parser is _github_mcp_parser and embedded:
        parsed = json.loads(actual)
        block = parsed["content"][0]
        assert len(actual) <= _MCP_MAX_OUTPUT_CHARS
        assert block["uri"] == _RESOURCE_URI
        assert block["original_characters"] == len(body)
        assert body.startswith(block["text"])
        assert block["returned_characters"] == len(block["text"])
        assert block["truncated"]
        assert not parsed["complete"]
    else:
        assert actual == expected
        assert ("Truncating MCP text content" in caplog.text) == (length > _MCP_MAX_OUTPUT_CHARS)


@pytest.mark.parametrize("include_text", [False, True])
def test_unsupported_content_preserves_existing_behavior(parser, include_text):
    content: list[mcp_types.ContentBlock] = [
        mcp_types.ImageContent(type="image", data="aW1hZ2U=", mimeType="image/png"),
        mcp_types.EmbeddedResource(
            type="resource",
            resource=mcp_types.BlobResourceContents(
                uri="https://example.com/file.bin",
                mimeType="application/octet-stream",
                blob="YmluYXJ5",
            ),
        ),
    ]
    if include_text:
        content.append(mcp_types.TextContent(type="text", text="Supported text"))

    output = parser(mcp_types.CallToolResult(content=content))
    if parser is _github_mcp_parser:
        parsed = json.loads(output)
        assert not parsed["complete"]
        assert all(block.get("error") for block in parsed["content"][:2])
        if include_text:
            assert parsed["content"][2]["text"] == "Supported text"
    else:
        assert output == ("Supported text" if include_text else "null")


def test_empty_result_preserves_existing_behavior(parser):
    assert parser(mcp_types.CallToolResult(content=[])) == "null"


@pytest.mark.parametrize("embedded", [False, True], ids=["text", "resource"])
@pytest.mark.parametrize(
    "association,login,trusted",
    [("NONE", "contributor", False), ("MEMBER", "member", True), ("NONE", "ci[bot]", True)],
)
def test_github_author_redaction_applies_to_both_content_types(embedded, association, login, trusted):
    payload = {
        "user": {"login": login},
        "author_association": association,
        "body": "Authored content",
    }
    text = json.dumps(payload)
    content = _text_resource(text) if embedded else mcp_types.TextContent(type="text", text=text)

    output = _github_mcp_parser(mcp_types.CallToolResult(content=[content]))

    expected = {**payload, "body": payload["body"] if trusted else _BODY_REDACTION_NOTICE}
    parsed = json.loads(output)
    assert (json.loads(parsed["content"][0]["text"]) if embedded else parsed) == expected


def test_github_redacts_embedded_content_before_truncation():
    payload = {
        "user": {"login": "contributor"},
        "author_association": "NONE",
        "body": "x" * (_MCP_MAX_OUTPUT_CHARS + 1),
    }

    output = _github_mcp_parser(
        mcp_types.CallToolResult(content=[_text_resource(json.dumps(payload))])
    )

    parsed = json.loads(output)
    assert json.loads(parsed["content"][0]["text"])["body"] == _BODY_REDACTION_NOTICE
    assert parsed["complete"]
    assert "[truncated]" not in output
