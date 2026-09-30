"""Offline checks for temporal isolation, citations, and inconclusive results."""

import copy
import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from decision_grading import (
    METRICS, consensus, evidence_text, grade_payloads, indexed_payload,
    run_payloads, turn_payload, validate_judgment,
)


@pytest.fixture
def payload():
    return {
        "answer": "Inspect the required review gate.",
        "sources": [{"id": "user-0", "text": "Write access is confirmed."}],
        "history": [],
    }


def judgment(label="pass"):
    return {
        key: {
            "verdict": label, "answer_lines": [0],
            "citations": [{"source_id": "user-0", "lines": [0]}],
            "reason": "Uses the known access fact to select the next inspection.",
        } for key in METRICS
    }


def test_turn_payload_never_exposes_future_or_judge_only_fields():
    case = {"evidence": "Initial facts", "expected_behavior": "SECRET_RUBRIC",
            "ground_truth": "SECRET_ANSWER", "follow_ups": ["FUTURE_REPLY"]}
    row = {"arm": "TREATMENT_LABEL", "turns": [
        {"query": "Question", "response": "Answer", "tool_calls": [{"output": "FIRST_TOOL"}]},
        {"query": "FUTURE_REPLY", "response": "FUTURE_ANSWER", "tool_calls": [{"output": "FUTURE_TOOL"}]},
    ]}
    first = json.dumps(turn_payload(case, row, 0))
    assert all(token not in first for token in ("FUTURE", "SECRET", "TREATMENT_LABEL"))
    second = turn_payload(case, row, 1)
    assert second["current_turn"] == 1
    assert second["answer"] == "FUTURE_ANSWER"
    assert second["history"] == [{"user": "Question", "assistant": "Answer"}]
    assert {source["id"] for source in second["sources"]} == {
        "case-evidence", "user-0", "tool-0-0", "user-1", "tool-1-0"
    }
    with pytest.raises(ValueError, match="range"):
        turn_payload(case, row, 2)


@pytest.mark.parametrize("change", ["answer", "source", "quote", "empty", "verdict", "extra", "reason"])
def test_invalid_judgment_rejected(payload, change):
    value = judgment()
    finding = value["next_action"]
    if change == "answer":
        finding["answer_lines"] = [99]
    elif change == "source":
        finding["citations"][0]["source_id"] = "future-tool"
    elif change == "quote":
        finding["citations"][0]["lines"] = [99]
    elif change == "empty":
        finding["citations"] = []
    elif change == "verdict":
        finding["verdict"] = "mostly pass"
    elif change == "extra":
        finding["score"] = 5
    else:
        finding["reason"] = ""
    with pytest.raises(ValueError):
        validate_judgment(value, payload)


def test_citations_verified_but_uncertainty_permitted(payload):
    checked = validate_judgment(judgment(), payload)
    assert checked["next_action"]["answer_quotes"] == [payload["answer"]]
    assert checked["next_action"]["citations"][0]["quotes"] == ["Write access is confirmed."]
    value = judgment("uncertain")
    for finding in value.values():
        finding["citations"] = []
    assert validate_judgment(value, payload)["next_action"]["citations"] == []


def test_indexed_evidence_preserves_markup_and_decoded_document_lines(payload):
    text = evidence_text({"result": {"content": "**Owner**\r\nUse `tool`."}})
    assert "**Owner**\r\nUse `tool`." in text
    payload["answer"] = "**Check** the gate.\n\nUse `CODEOWNERS`."
    payload["sources"].append({"id": "tool", "text": text})
    indexed = indexed_payload(payload)
    assert indexed["answer_lines"] == {"0": "**Check** the gate.", "2": "Use `CODEOWNERS`."}
    value = judgment()
    value["next_action"]["answer_lines"] = [2]
    value["next_action"]["citations"] = [{"source_id": "tool", "lines": [2, 3]}]
    checked = validate_judgment(value, payload)
    assert checked["next_action"]["answer_quotes"] == ["Use `CODEOWNERS`."]
    assert checked["next_action"]["citations"][0]["quotes"] == ["**Owner**", "Use `tool`."]
    for invalid in ([True], [-1], [0, 0], ["0"], [1]):
        value["next_action"]["answer_lines"] = invalid
        with pytest.raises(ValueError):
            validate_judgment(value, payload)


@pytest.mark.parametrize("labels,expected", [
    (["pass", "pass"], "pass"),
    (["fail", "fail"], "fail"),
    (["pass", "fail"], "inconclusive"),
    (["uncertain", "uncertain"], "inconclusive"),
    (["pass"], "inconclusive"),
    (["pass", "pass", "pass"], "inconclusive"),
])
def test_consensus_never_forces_a_winner(labels, expected):
    attempts = [{"status": "completed", "judgment": judgment(label)} for label in labels]
    assert set(consensus(attempts, 2).values()) == {expected}


def test_grading_failure_is_not_a_bot_failure():
    attempts = [{"status": "completed", "judgment": judgment()}, {"status": "failed"}]
    assert set(consensus(attempts, 2).values()) == {"inconclusive"}


def test_failures_on_different_answer_claims_are_not_consensus():
    first, second = judgment("fail"), judgment("fail")
    for metric in METRICS:
        second[metric]["answer_lines"] = [2]
    assert set(consensus([
        {"status": "completed", "judgment": first},
        {"status": "completed", "judgment": second},
    ], 2).values()) == {"inconclusive"}


def test_latest_user_facts_precede_large_tool_sources(payload):
    payload["current_turn"] = 1
    payload["sources"] = [
        {"id": "case-evidence", "text": "No inner log yet."},
        {"id": "tool-0-0", "text": "Long documentation."},
        {"id": "user-1", "text": "Here is the inner log."},
    ]
    result = indexed_payload(payload)
    assert result["current_turn"] == 1
    assert [source["id"] for source in result["sources"]] == [
        "case-evidence", "user-1", "tool-0-0"
    ]


def test_grading_journals_invalid_spans_and_preserves_denominators(payload, tmp_path):
    client = Mock()
    client.chat.completions.create.side_effect = [
        SimpleNamespace(id="one", choices=[SimpleNamespace(
            message=SimpleNamespace(content=json.dumps(judgment()))
        )]),
        SimpleNamespace(id="two", choices=[SimpleNamespace(
            message=SimpleNamespace(content='{"wrong":"shape"}')
        )]),
    ]
    output = tmp_path / "run"
    summary = grade_payloads(client, [{"id": "opaque", "payload": payload}], "judge", output)
    assert summary["grading_failures"] == 1
    assert set(summary["items"]["opaque"].values()) == {"inconclusive"}
    rows = [json.loads(line) for line in (output / "judgments.jsonl").read_text().splitlines()]
    assert len(rows) == 4 and rows[-1]["raw"] == '{"wrong":"shape"}'
    assert json.loads((output / "manifest.json").read_text())["repeats"] == 2
    for call in client.chat.completions.create.call_args_list:
        assert json.loads(call.kwargs["messages"][1]["content"]) == indexed_payload(payload)
    with pytest.raises(FileExistsError):
        grade_payloads(client, [{"id": "opaque", "payload": payload}], "judge", output)
    with pytest.raises(ValueError, match="two"):
        grade_payloads(client, [{"id": "opaque", "payload": payload}], "judge", tmp_path / "one", repeats=1)


def test_run_payloads_checks_bundle_completeness_and_frozen_replies(tmp_path):
    bundle = {
        "bundle_sha256": "hash",
        "cases": [{"testcase": "case", "query": "q", "evidence": "e", "follow_ups": ["reply"]}],
        "jobs": [{"arm": "baseline", "case_id": "case", "repeat": 0}],
    }
    (tmp_path / "run.json").write_text(json.dumps({
        "bundle_sha256": "hash", "mode": "local-agent", "arm": "baseline"
    }))
    row = {"case_id": "case", "repeat": 0, "status": "completed", "turns": [
        {"query": "q", "response": "a"}, {"query": "reply", "response": "a2"},
    ]}
    path = tmp_path / "generations.json"
    path.write_text(json.dumps([row]))
    assert [item["id"] for item in run_payloads(bundle, tmp_path)] == ["case:0:0", "case:0:1"]
    for bad_rows in ([], [row, row], [{**row, "status": "failed"}]):
        path.write_text(json.dumps(bad_rows))
        with pytest.raises(ValueError):
            run_payloads(bundle, tmp_path)
    wrong = copy.deepcopy(row)
    wrong["turns"][1]["query"] = "Unfrozen reply"
    path.write_text(json.dumps([wrong]))
    with pytest.raises(ValueError, match="frozen"):
        run_payloads(bundle, tmp_path)
