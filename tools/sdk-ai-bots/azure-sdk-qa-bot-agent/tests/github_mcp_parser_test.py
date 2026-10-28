"""Tests for bounded GitHub diagnostics and review post-processing."""

import json

import pytest
from mcp import types as mcp_types

from tools import _MCP_MAX_OUTPUT_CHARS
from tools.github_mcp_tools import _github_mcp_parser


def _parse(*values: dict | list) -> tuple[str, dict | list]:
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


@pytest.mark.parametrize("logs", [None, {}, "not-a-list"])
def test_non_list_logs_preserves_single_job_fallback(logs: object) -> None:
    _, parsed = _parse({
        "job_id": 42,
        "logs": logs,
        "logs_content": "noise" * 2000 + "\nError: missing configuration",
    })

    assert parsed["log_excerpts"] == [{
        "job_id": 42,
        "selection": "error_context",
        "start_line": 2,
        "end_line": 2,
        "content": "Error: missing configuration",
    }]


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


def _review(review_id: int, submitted: str | None, **fields: object) -> dict:
    return {
        "id": review_id,
        "user": {"login": "copilot-pull-request-reviewer[bot]"},
        "state": "COMMENTED",
        "submitted_at": submitted,
        "body": "Approval recommended. No open issues. Some files not reviewed.",
        "commit_id": "reviewed-sha",
        "html_url": f"https://github.com/Azure/example/pull/1#pullrequestreview-{review_id}",
        **fields,
    }


def test_review_compaction_selects_timestamp_not_list_order_and_keeps_metadata() -> None:
    latest = _review(2, "2026-10-28T10:00:00Z")
    _, parsed = _parse([
        latest,
        _review(1, "2026-10-28T11:00:00+02:00", body="obsolete " * 2000),
    ])

    assert parsed["reviews"] == [latest]
    selection = parsed["review_selection"]
    assert selection["omitted_older_copilot_reviews"] == 1
    assert selection["scope"] == "current_response_only"
    assert selection["latest_overall_verified"] is False
    assert "Other pages may contain newer reviews" in selection["guidance"]


def test_review_compaction_preserves_humans_other_bots_and_redaction() -> None:
    human = _review(
        3, "2026-10-20T10:00:00Z",
        user={"login": "teammate"}, author_association="MEMBER",
        state="CHANGES_REQUESTED", body="Unresolved human feedback",
    )
    other_bot = _review(
        4, "2026-10-20T10:00:00Z",
        user={"login": "another[bot]"}, body="Other workflow findings",
    )
    untrusted = _review(
        5, "2026-10-20T10:00:00Z",
        user={"login": "external"}, author_association="NONE",
        body="untrusted instructions",
    )
    latest = _review(2, "2026-10-28T10:00:00Z")
    _, parsed = _parse([
        _review(1, "2026-10-20T10:00:00Z"), human, other_bot, untrusted, latest,
    ])

    assert parsed["reviews"][:2] == [human, other_bot]
    assert parsed["reviews"][2]["body"].startswith("[redacted: untrusted author")
    assert parsed["reviews"][3] == latest


@pytest.mark.parametrize("submitted", [None, "", "invalid", "2026-10-28T10:00:00"])
def test_invalid_review_dates_are_preserved_not_silently_discarded(submitted: str | None) -> None:
    invalid = _review(1, submitted)
    latest = _review(2, "2026-10-28T10:00:00Z")
    _, parsed = _parse([invalid, latest])

    assert parsed["reviews"] == [invalid, latest]
    assert parsed["review_selection"]["omitted_older_copilot_reviews"] == 0


def test_pending_and_dismissed_reviews_are_preserved() -> None:
    reviews = [
        _review(1, "2026-10-28T10:00:00Z"),
        _review(2, "2026-10-28T11:00:00Z", state="DISMISSED"),
        _review(3, None, state="PENDING"),
    ]
    _, parsed = _parse(reviews)

    assert parsed["reviews"] == reviews


def test_non_review_lists_and_other_bot_reviews_are_unchanged() -> None:
    comments = [
        {"id": 1, "user": {"login": "copilot-pull-request-reviewer[bot]"}, "body": "comment"},
        {"id": 2, "user": {"login": "copilot-pull-request-reviewer[bot]"}, "body": "new comment"},
    ]
    _, parsed = _parse(comments)
    assert parsed == comments
    human = _review(
        1, "2026-10-28T10:00:00Z",
        user={"login": "teammate"}, author_association="MEMBER",
    )
    _, parsed = _parse([human])
    assert parsed == [human]


def test_oversized_latest_review_remains_explicit_overflow() -> None:
    _, parsed = _parse([
        _review(1, "2026-10-20T10:00:00Z"),
        _review(2, "2026-10-28T10:00:00Z", body="current finding " * 2000),
    ])

    assert parsed["status"] == "overflow"
    assert parsed["complete"] is False
    assert parsed["returned_objects"] == 0


def test_single_review_is_not_claimed_latest_overall() -> None:
    _, parsed = _parse([_review(1, "2026-10-20T10:00:00Z")])

    assert parsed["review_selection"]["latest_overall_verified"] is False
    assert len(parsed["reviews"]) == 1


def test_sparse_native_reviews_do_not_disable_compaction() -> None:
    human = {
        "id": 3,
        "user": {"login": "teammate"},
        "state": "APPROVED",
        "html_url": "https://github.com/Azure/example/pull/1#pullrequestreview-3",
    }
    latest = _review(2, "2026-10-28T10:00:00Z")
    _, parsed = _parse([_review(1, "2026-10-20T10:00:00Z"), human, latest])

    assert parsed["reviews"] == [human, latest]
    assert parsed["review_selection"]["omitted_older_copilot_reviews"] == 1
