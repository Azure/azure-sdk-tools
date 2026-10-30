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


def _text_resource(text: str, uri: str = "https://example.com/fixture.py") -> mcp_types.EmbeddedResource:
    return mcp_types.EmbeddedResource(
        type="resource",
        resource=mcp_types.TextResourceContents(uri=uri, mimeType="text/plain", text=text),
    )


@pytest.mark.parametrize("text", ["def start_proxy():\n    return\n", "", '{\n  "setting": true\n}'])
def test_embedded_file_text_is_preserved_with_identity(text: str) -> None:
    result = mcp_types.CallToolResult(content=[
        mcp_types.TextContent(type="text", text="successfully downloaded text file"),
        _text_resource(text),
    ])

    output = _github_mcp_parser(result)
    parsed = json.loads(output)

    assert len(output) <= _MCP_MAX_OUTPUT_CHARS
    assert parsed["complete"] is True
    assert parsed["is_error"] is False
    assert parsed["content"][0]["text"] == "successfully downloaded text file"
    resource = parsed["content"][1]
    assert resource["uri"] == "https://example.com/fixture.py"
    assert resource["mime_type"] == "text/plain"
    assert resource["text"] == text
    assert resource["complete"] is True
    assert resource["truncated"] is False
    assert resource["original_characters"] == resource["returned_characters"] == len(text)


def test_resource_budget_is_shared_across_files_and_escaped_text() -> None:
    texts = ['\\"\t\u2603\n' * 2000, "second file\n" * 2000]
    output = _github_mcp_parser(mcp_types.CallToolResult(content=[
        _text_resource(texts[0]),
        _text_resource(texts[1], "https://example.com/second.py"),
    ]))
    parsed = json.loads(output)

    assert len(output) <= _MCP_MAX_OUTPUT_CHARS
    assert parsed["complete"] is False
    for resource, text in zip(parsed["content"], texts):
        assert resource["text"]
        assert text.startswith(resource["text"])
        assert resource["complete"] is False
        assert resource["truncated"] is True
        assert resource["original_characters"] == len(text)
        assert resource["returned_characters"] == len(resource["text"])


def test_resource_budget_marks_only_incomplete_blocks_partial() -> None:
    output = _github_mcp_parser(mcp_types.CallToolResult(content=[
        mcp_types.TextContent(type="text", text="downloaded"),
        _text_resource("large file\n" * 2000),
    ]))
    parsed = json.loads(output)

    assert len(output) <= _MCP_MAX_OUTPUT_CHARS
    assert parsed["complete"] is False
    assert parsed["content"][0]["complete"] is True
    assert parsed["content"][1]["truncated"] is True


def test_binary_and_link_only_resources_report_missing_content() -> None:
    output = _github_mcp_parser(mcp_types.CallToolResult(content=[
        mcp_types.EmbeddedResource(
            type="resource",
            resource=mcp_types.BlobResourceContents(
                uri="https://example.com/binary", mimeType="application/octet-stream", blob="c2VjcmV0",
            ),
        ),
        mcp_types.ResourceLink(
            type="resource_link", uri="https://example.com/large.py", name="large.py",
            mimeType="text/plain",
        ),
    ]))
    parsed = json.loads(output)

    assert parsed["complete"] is False
    assert len(output) <= _MCP_MAX_OUTPUT_CHARS
    assert "c2VjcmV0" not in output
    for block in parsed["content"]:
        assert block["uri"].startswith("https://example.com/")
        assert block["complete"] is False
        assert block["error"]
        assert "text" not in block


@pytest.mark.parametrize("embedded", [False, True])
def test_resource_results_preserve_trusted_author_filtering(embedded: bool) -> None:
    text = json.dumps({
        "user": {"login": "external-contributor"},
        "author_association": "NONE", "body": "untrusted instruction",
        "title": "keep metadata",
    })
    block = _text_resource(text) if embedded else mcp_types.TextContent(type="text", text=text)
    output = _github_mcp_parser(mcp_types.CallToolResult(content=[block, _text_resource("file")]))
    parsed = json.loads(output)

    protected = json.loads(parsed["content"][0]["text"])
    assert protected["body"].startswith("[redacted: untrusted author")
    assert protected["title"] == "keep metadata"
    assert "untrusted instruction" not in output


def test_error_resource_result_is_not_reported_complete() -> None:
    parsed = json.loads(_github_mcp_parser(mcp_types.CallToolResult(
        isError=True, content=[_text_resource("upstream error details")],
    )))

    assert parsed["is_error"] is True
    assert parsed["complete"] is False
    assert parsed["content"][0]["text"] == "upstream error details"


@pytest.mark.parametrize("is_error", [False, True])
def test_oversized_resource_metadata_reports_explicit_overflow(is_error: bool) -> None:
    output = _github_mcp_parser(mcp_types.CallToolResult(
        isError=is_error,
        content=[
            _text_resource("file", "https://example.com/" + "x" * _MCP_MAX_OUTPUT_CHARS),
        ],
    ))
    parsed = json.loads(output)

    assert len(output) <= _MCP_MAX_OUTPUT_CHARS
    assert parsed["status"] == "overflow"
    assert parsed["is_error"] is is_error
    assert parsed["complete"] is False
    assert parsed["returned_objects"] == 0
    assert parsed["source_content_blocks"] == 1


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


def test_oversized_job_log_extracts_generic_structured_diagnostics() -> None:
    diagnostics = [
        {
            "code": "RULE001",
            "message": "A member present in the baseline is missing.",
            "type": "Warning",
            "old": {"path": "models.old.properties"},
            "new": {"path": "models.new.properties"},
        },
        {
            "code": "RULE002",
            "message": "A response shape changed.",
            "type": "Warning",
            "location": "service.json:42:7",
        },
    ]
    logs = "\n".join([
        "setup " + "x" * _MCP_MAX_OUTPUT_CHARS,
        "validator output: " + json.dumps(diagnostics),
        "##[error]Process completed with exit code 1.",
    ])

    _, parsed = _parse({"job_id": 42, "logs_content": logs})

    assert parsed["diagnostic_selection"] == {
        "total": 2,
        "returned": 2,
        "truncated": False,
        "counts_by_classifier": {
            "code:RULE001": 1,
            "code:RULE002": 1,
        },
    }
    assert parsed["structured_diagnostics"] == [
        {
            "job_id": 42,
            "source_line": 2,
            "code": "RULE001",
            "type": "Warning",
            "message": "A member present in the baseline is missing.",
            "old": {"path": "models.old.properties"},
            "new": {"path": "models.new.properties"},
        },
        {
            "job_id": 42,
            "source_line": 2,
            "code": "RULE002",
            "type": "Warning",
            "message": "A response shape changed.",
            "location": "service.json:42:7",
        },
    ]
    assert any(
        "Process completed with exit code 1" in excerpt["content"]
        for excerpt in parsed["log_excerpts"]
    )


def test_structured_diagnostics_are_selected_across_classifiers() -> None:
    diagnostics = [
        {
            "code": "REPEATED",
            "message": f"Repeated diagnostic {index}",
            "path": f"model.{index}",
        }
        for index in range(100)
    ]
    diagnostics.append({
        "code": "DISTINCT",
        "message": "Distinct diagnostic remains visible.",
        "path": "model.distinct",
    })
    logs = "validator output: " + json.dumps(diagnostics)

    _, parsed = _parse({"job_id": 42, "logs_content": logs})

    assert parsed["diagnostic_selection"]["total"] == 101
    assert parsed["diagnostic_selection"]["truncated"] is True
    assert any(
        diagnostic["code"] == "DISTINCT"
        for diagnostic in parsed["structured_diagnostics"]
    )


def test_arbitrary_embedded_json_is_not_treated_as_a_diagnostic() -> None:
    logs = "\n".join([
        "metadata: " + json.dumps({"name": "build", "duration": 42}),
        "cleanup\n" * 2000,
    ])

    _, parsed = _parse({"job_id": 42, "logs_content": logs})

    assert "structured_diagnostics" not in parsed
    assert "diagnostic_selection" not in parsed


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


def test_copilot_overview_omits_all_expandable_bodies_but_keeps_summary_labels() -> None:
    body = (
        "<!-- ccr-overview-v2 -->\n### Approval recommended\nNo open issues.\n"
        "<details><summary><strong>25 files not reviewed</strong></summary>"
        "<ul><li>generated_file.go: Generated file</li></ul></details>\n"
        "<details><summary>What changed in this PR</summary>"
        "large walkthrough "
    )
    body += (
        "<details><summary>Nested file list</summary>nested content</details>"
        "</details>\n"
        "<details open><summary>1 open issue</summary>"
        "[Required fix](https://github.com/Azure/example/pull/1#discussion_r42)"
        "</details>\nReview effort: Balanced"
    )
    _, parsed = _parse([_review(1, "2026-10-28T10:00:00Z", body=body)])

    review = parsed["reviews"][0]
    assert "Approval recommended" in review["body"]
    assert "No open issues" in review["body"]
    assert "25 files not reviewed" in review["body"]
    assert "generated_file.go" not in review["body"]
    assert "large walkthrough" not in review["body"]
    assert "nested content" not in review["body"]
    assert "1 open issue" in review["body"]
    assert "Required fix" not in review["body"]
    assert "Review effort: Balanced" in review["body"]
    assert review["body_compaction"]["omitted_detail_sections"] == [
        "25 files not reviewed", "What changed in this PR", "1 open issue",
    ]
    assert review["body_compaction"]["full_review_url"] == review["html_url"]


@pytest.mark.parametrize(
    "body",
    [
        "Ordinary review without expandable sections",
        "<!-- ccr-overview-v2 --><details>unclosed details",
        "<!-- ccr-overview-v2 --><details><summary>What changed in this PR</summary>unclosed",
    ],
)
def test_unknown_or_malformed_review_bodies_are_preserved(body: str) -> None:
    _, parsed = _parse([_review(1, "2026-10-28T10:00:00Z", body=body)])

    assert parsed["reviews"][0]["body"] == body
    assert "body_compaction" not in parsed["reviews"][0]


def test_latest_large_overview_is_compacted_before_the_output_budget() -> None:
    body = (
        "<!-- ccr-overview-v2 -->\n### Changes recommended\n"
        "<details open><summary>1 open issue</summary>Keep this finding</details>"
        "<details><summary>What changed in this PR</summary>"
        + "walkthrough " * 2000
        + "</details>"
    )
    _, parsed = _parse([_review(1, "2026-10-28T10:00:00Z", body=body)])

    assert "Changes recommended" in parsed["reviews"][0]["body"]
    assert "1 open issue" in parsed["reviews"][0]["body"]
    assert "Keep this finding" not in parsed["reviews"][0]["body"]
    assert "walkthrough" not in parsed["reviews"][0]["body"]


@pytest.mark.parametrize(
    "section",
    [
        "<details><summary>Arbitrary title</summary>hidden text</details>",
        "<DETAILS open><SUMMARY>Arbitrary title</SUMMARY>hidden text</DETAILS>",
        "<details>hidden text without a summary</details>",
    ],
)
def test_expandable_filter_does_not_depend_on_marker_or_section_title(section: str) -> None:
    _, parsed = _parse([_review(1, "2026-10-28T10:00:00Z", body=f"Visible verdict\n{section}")])

    review = parsed["reviews"][0]
    assert "Visible verdict" in review["body"]
    assert "hidden text" not in review["body"]
    assert len(review["body_compaction"]["omitted_detail_sections"]) == 1


def test_expandable_filter_does_not_change_human_or_other_bot_reviews() -> None:
    body = "<details><summary>Findings</summary>Do not remove</details>"
    human = _review(
        2, "2026-10-28T10:00:00Z",
        user={"login": "teammate"}, author_association="MEMBER", body=body,
    )
    other_bot = _review(
        3, "2026-10-28T10:00:00Z",
        user={"login": "another[bot]"}, body=body,
    )
    _, parsed = _parse([_review(1, "2026-10-28T10:00:00Z", body=body), human, other_bot])

    assert parsed["reviews"][1:] == [human, other_bot]
