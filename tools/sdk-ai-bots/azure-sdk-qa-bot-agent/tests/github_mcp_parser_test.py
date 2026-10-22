"""Tests for bounded GitHub Actions diagnostic excerpts."""

import json

import pytest
from mcp import types as mcp_types

from tools import _MCP_MAX_OUTPUT_CHARS
from tools.github_mcp_tools import _github_mcp_parser


def _parse(*values: dict) -> tuple[str, dict]:
    result = mcp_types.CallToolResult(
        content=[
            mcp_types.TextContent(type="text", text=json.dumps(value))
            for value in values
        ]
    )
    output = _github_mcp_parser(result)
    assert len(output) <= _MCP_MAX_OUTPUT_CHARS
    return output, json.loads(output)


def test_small_job_log_preserves_native_payload() -> None:
    native = {
        "job_id": 42,
        "logs_content": "Error: invalid configuration\n    at validate",
        "message": "Job logs content retrieved successfully",
        "original_length": 2,
        "extra_metadata": {"retained": True},
    }

    _, parsed = _parse(native)

    assert parsed == native


def test_oversized_job_log_returns_exact_error_context_before_cleanup() -> None:
    lines = [
        "setup " + "x" * _MCP_MAX_OUTPUT_CHARS,
        "Correlating runs...",
        "correlateResults.ts:49",
        "throw new Error(...)",
        "^",
        "Error: No default tag found for readme Insights/readme.md in before state",
        "    at correlateRuns (correlateResults.ts:49:15)",
        "    at async runLintDiff",
        "Node.js v24.22.0",
        "##[error]Process completed with exit code 1.",
        *["Post-job cleanup"] * 100,
    ]

    _, parsed = _parse({"job_id": 42, "logs_content": "\n".join(lines)})

    assert parsed["status"] == "overflow"
    assert parsed["complete"] is False
    assert parsed["returned_objects"] == 0
    assert parsed["log_excerpts"]
    assert "\n" not in parsed["error"]
    assert len(parsed["error"]) <= 120
    assert "partial" in parsed["error"]
    for excerpt in parsed["log_excerpts"]:
        assert excerpt["content"] == "\n".join(
            lines[excerpt["start_line"] - 1:excerpt["end_line"]]
        )
        assert excerpt["job_id"] == 42
    content = "\n".join(item["content"] for item in parsed["log_excerpts"])
    assert "Error: No default tag found" in content
    assert "at correlateRuns" in content
    assert "setup " not in content
    assert content.count("Post-job cleanup") < 100


def test_failed_run_logs_and_multiple_content_items_are_supported() -> None:
    _, parsed = _parse(
        {
            "failed_jobs": 2,
            "logs": [
                {"job_id": 41, "logs_content": "noise" * 2000 + "\nError: first"},
                {"job_id": 42, "logs_content": "noise" * 2000 + "\nError: second"},
            ],
        },
        {"job_id": 43, "logs_content": "noise" * 2000 + "\nError: third"},
    )

    assert {item["job_id"] for item in parsed["log_excerpts"]} == {41, 42, 43}


@pytest.mark.parametrize(
    "next_group",
    ["##[group]Run actions/upload-artifact", "##[start-action display=Cleanup]"],
)
def test_error_context_stops_before_a_new_log_group(next_group: str) -> None:
    _, parsed = _parse({
        "job_id": 42,
        "logs_content": "\n".join([
            "setup " + "x" * _MCP_MAX_OUTPUT_CHARS,
            "Error: missing configuration",
            "    at validate",
            next_group,
            "unrelated artifact-upload configuration",
        ]),
    })

    content = parsed["log_excerpts"][0]["content"]
    assert "at validate" in content
    assert next_group not in content
    assert "artifact-upload configuration" not in content


@pytest.mark.parametrize(
    "diagnostic",
    [
        "2026-10-21T22:08:23Z Error: missing default tag",
        "2026-10-21T22:08:23Z TypeError: invalid argument",
        "Unhandled exception. System.InvalidOperationException: invalid state",
        "src/index.ts(12,4): error TS2345: argument is not assignable",
        "[ERROR] Could not resolve dependency",
        "npm ERR! command failed",
        "fatal: repository not found",
        "panic: invalid memory address",
        "##[error]Process completed with exit code 1.",
    ],
)
def test_common_error_markers_are_excerpted(diagnostic: str) -> None:
    _, parsed = _parse({
        "job_id": 42,
        "logs_content": "noise" * 2000 + "\n" + diagnostic,
    })

    assert diagnostic in parsed["log_excerpts"][0]["content"]


def test_oversized_context_line_does_not_hide_a_short_error() -> None:
    _, parsed = _parse({
        "job_id": 42,
        "logs_content": "\n".join([
            "x" * _MCP_MAX_OUTPUT_CHARS,
            "Error: specific diagnosis",
            "y" * _MCP_MAX_OUTPUT_CHARS,
        ]),
    })

    assert parsed["log_excerpts"] == [{
        "job_id": 42,
        "selection": "error_context",
        "start_line": 2,
        "end_line": 2,
        "content": "Error: specific diagnosis",
    }]


def test_single_oversized_error_returns_an_explicit_line_fragment() -> None:
    original = "Error: " + "x" * _MCP_MAX_OUTPUT_CHARS
    _, parsed = _parse({
        "job_id": 42,
        "logs_content": original,
    })

    assert parsed["complete"] is False
    excerpt = parsed["log_excerpts"][0]
    assert excerpt["selection"] == "error_context"
    assert excerpt["content"].startswith("Error: ")
    assert original.startswith(excerpt["content"])
    assert len(excerpt["content"]) < len(original)
    assert excerpt["last_line_truncated"] is True


def test_long_error_does_not_hide_a_later_short_diagnostic() -> None:
    _, parsed = _parse({
        "job_id": 42,
        "logs_content": "\n".join([
            "Error: " + "x" * _MCP_MAX_OUTPUT_CHARS,
            "Error: useful short diagnostic",
        ]),
    })

    assert any(
        excerpt["content"] == "Error: useful short diagnostic"
        for excerpt in parsed["log_excerpts"]
    )


def test_excerpts_respect_serialized_budget_with_escaping_and_many_errors() -> None:
    lines = [
        f'Error: failure {index} ' + '\\\t"\u2603' * 50
        for index in range(100)
    ]

    _, parsed = _parse({"job_id": 42, "logs_content": "\n".join(lines)})

    assert parsed["log_excerpts"]
    for excerpt in parsed["log_excerpts"]:
        source = "\n".join(
            lines[excerpt["start_line"] - 1:excerpt["end_line"]]
        )
        if excerpt.get("last_line_truncated"):
            assert source.startswith(excerpt["content"])
        else:
            assert excerpt["content"] == source
    assert parsed["log_excerpts"][-1]["end_line"] < len(lines)


def test_log_without_error_markers_returns_a_labeled_tail() -> None:
    _, parsed = _parse({"job_id": 42, "logs_content": "cleanup\n" * 2000})

    assert parsed["log_excerpts"] == [{
        "job_id": 42,
        "selection": "tail",
        "start_line": 1996,
        "end_line": 2000,
        "content": "\n".join(["cleanup"] * 5),
    }]
    assert parsed["complete"] is False
    assert "partial" in parsed["error"]


def test_oversized_tail_line_is_explicitly_marked_as_a_fragment() -> None:
    original = "setup " * 2000 + "last log text"
    _, parsed = _parse({"job_id": 42, "logs_content": original})

    excerpt = parsed["log_excerpts"][0]
    assert excerpt["selection"] == "tail"
    assert original.endswith(excerpt["content"])
    assert excerpt["content"].endswith("last log text")
    assert excerpt["first_line_truncated"] is True
    assert len(excerpt["content"]) < len(original)


def test_other_oversized_github_payloads_keep_pagination_handling() -> None:
    _, parsed = _parse({
        "id": 42,
        "logs_content": "Error: " + "x" * _MCP_MAX_OUTPUT_CHARS,
    })

    assert "log_excerpts" not in parsed
    assert "smaller perPage if supported" in parsed["error"]
    assert "\n" not in parsed["error"]
    assert len(parsed["error"]) <= 120
    assert parsed["returned_objects"] == 0


def test_trusted_author_filtering_is_unchanged() -> None:
    native = {
        "id": 42,
        "user": {"login": "external-contributor"},
        "author_association": "NONE",
        "body": "untrusted instructions " * 1000,
        "title": "retain native metadata",
    }

    _, parsed = _parse(native)

    assert parsed["body"].startswith("[redacted: untrusted author")
    assert parsed["title"] == native["title"]
    assert parsed["user"] == native["user"]
    assert "log_excerpts" not in parsed
