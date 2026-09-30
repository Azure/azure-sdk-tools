"""Selective-disclosure integrity checks; no cloud calls or private cases."""

import copy
import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock
from urllib.error import URLError

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import evidence_acquisition as acquisition


@pytest.fixture
def fixture():
    bundle = {
        "seed": 13,
        "cases": [{"testcase": "case", "query": "What caused this failure?", "evidence": "Initial observation."}],
        "jobs": [{"case_id": "case", "repeat": 0, "arm": "baseline"}],
    }
    environment = {"schema_version": 1, "cases": [{
        "case_id": "case", "fields": {"error": "Exact error output", "other": "Other observation"},
        "worlds": [{
            "id": "one", "values": {"error": "HIDDEN_ERROR", "other": "HIDDEN_OTHER"},
            "required_fields": ["error"], "useful_fields": ["error"],
            "expected_behavior": "PRIVATE_EXPECTED_ACTION",
        }],
    }]}
    return bundle, environment


def decision(fields=(), unavailable=False):
    return {"status": "conclusive", "fields": list(fields), "unavailable": unavailable}


def assessment(field="error", line=0):
    return {"status": "completed", "result": {
        "requests": [{"field": field, "lines": [line]}], "unavailable_lines": [],
    }}


def test_environment_requires_complete_unique_worlds(fixture):
    bundle, environment = fixture
    assert acquisition.validate_environment(environment, bundle) == environment
    variants = []
    value = copy.deepcopy(environment)
    value["cases"][0]["worlds"][0]["values"].pop("other")
    variants.append(value)
    value = copy.deepcopy(environment)
    value["cases"][0]["worlds"][0]["useful_fields"] = []
    variants.append(value)
    value = copy.deepcopy(environment)
    value["cases"] *= 2
    variants.append(value)
    value = copy.deepcopy(environment)
    value["cases"][0]["worlds"] *= 2
    variants.append(value)
    variants.append({"schema_version": 1, "cases": []})
    for value in variants:
        with pytest.raises(ValueError):
            acquisition.validate_environment(value, bundle)


@pytest.mark.parametrize("change", ["field", "line", "bool", "blank", "duplicate", "extra"])
def test_invalid_request_references_rejected(change):
    value = copy.deepcopy(assessment()["result"])
    if change == "field":
        value["requests"][0]["field"] = "secret"
    elif change == "line":
        value["requests"][0]["lines"] = [8]
    elif change == "bool":
        value["requests"][0]["lines"] = [True]
    elif change == "blank":
        value["requests"][0]["lines"] = [1]
    elif change == "duplicate":
        value["requests"] *= 2
    else:
        value["answer"] = "Invented reply"
    with pytest.raises(ValueError):
        acquisition.validate_requests(value, "Please paste the error.\n\nThanks.", {"error": "Error output"})


def test_consensus_requires_matching_fields_and_overlapping_requests():
    assert acquisition.request_consensus([assessment(), assessment()]) == decision(["error"])
    for values in (
        [assessment()], [assessment(), {"status": "failed"}],
        [assessment(), assessment("other")], [assessment(), assessment(line=1)],
    ):
        assert acquisition.request_consensus(values)["status"] == "inconclusive"
    first, second = assessment(), assessment()
    second["result"]["unavailable_lines"] = [2]
    assert acquisition.request_consensus([first, second])["status"] == "inconclusive"


def test_router_never_receives_hidden_values_or_outcomes(fixture):
    bundle, environment = fixture
    fields = environment["cases"][0]["fields"]
    client = Mock()
    client.chat.completions.create.return_value = SimpleNamespace(
        id="request-id", choices=[SimpleNamespace(message=SimpleNamespace(
            content=json.dumps(assessment()["result"])
        ))]
    )
    events = []
    result = acquisition.assess_requests(
        client, "model", "Please paste the error.", fields,
        lambda row: events.append(copy.deepcopy(row)),
    )
    assert result["status"] == "conclusive"
    assert [row["status"] for row in events] == ["attempted", "completed", "attempted", "completed"]
    sent = json.dumps(client.chat.completions.create.call_args_list[0].kwargs["messages"])
    assert "json" in client.chat.completions.create.call_args_list[0].kwargs["messages"][0]["content"].lower()
    assert all(token not in sent for token in ("HIDDEN_ERROR", "HIDDEN_OTHER", "PRIVATE_EXPECTED_ACTION"))
    assert "Exact error output" in sent


def test_disclosure_is_literal_and_whitelisted(fixture):
    _, environment = fixture
    case = environment["cases"][0]
    reply = acquisition.disclose(decision(["error"]), case["fields"], case["worlds"][0])
    assert "HIDDEN_ERROR" in reply and "HIDDEN_OTHER" not in reply
    assert "PRIVATE_EXPECTED_ACTION" not in reply
    assert acquisition.disclose(
        decision(unavailable=True), case["fields"], case["worlds"][0]
    ) == acquisition.UNAVAILABLE_REPLY
    for value in (decision(), decision(["unknown"]), {"status": "inconclusive"}):
        with pytest.raises(ValueError):
            acquisition.disclose(value, case["fields"], case["worlds"][0])


def install_agent(monkeypatch, decisions, answers=None):
    answers = answers or ["Please paste the error.", "Correct the malformed field."]
    request = Mock(side_effect=answers)
    monkeypatch.setattr(acquisition, "local_request", request)
    monkeypatch.setattr(acquisition, "parse_local_response", lambda text: ("local-id", text, [], {}))
    monkeypatch.setattr(acquisition, "assess_requests", Mock(side_effect=decisions))
    return request


def test_only_requested_fact_enters_next_turn_and_worlds_share_initial_input(fixture, monkeypatch, tmp_path):
    bundle, environment = fixture
    second = copy.deepcopy(environment["cases"][0]["worlds"][0])
    second["id"] = "two"
    second["values"]["error"] = "DIFFERENT_HIDDEN_ERROR"
    environment["cases"][0]["worlds"].append(second)
    requests = install_agent(
        monkeypatch, [decision(["error"]), decision()] * 2,
        ["Please paste the error.", "Correct it."] * 2,
    )
    rows = acquisition.collect_acquisition(
        bundle, environment, "baseline", "http://127.0.0.1:8088", None, "model", tmp_path / "journal"
    )
    assert len(rows) == 2 and all(row["required_missing"] == [] for row in rows)
    assert requests.call_args_list[0].args[:3] == requests.call_args_list[2].args[:3]
    for call in requests.call_args_list:
        wire = json.dumps(call.args)
        assert "HIDDEN_OTHER" not in wire and "PRIVATE_EXPECTED_ACTION" not in wire
    assert "HIDDEN_ERROR" not in requests.call_args_list[0].args[1]
    assert "HIDDEN_ERROR" in requests.call_args_list[1].args[1]
    assert all(len(row["turns"]) == 2 for row in rows)


def test_no_request_does_not_trigger_automatic_followup(fixture, monkeypatch, tmp_path):
    bundle, environment = fixture
    requests = install_agent(monkeypatch, [decision()], ["If X, do Y; otherwise do Z."])
    rows = acquisition.collect_acquisition(
        bundle, environment, "baseline", "http://localhost", None, "model", tmp_path / "journal"
    )
    assert requests.call_count == 1
    assert rows[0]["disclosures"] == [] and rows[0]["required_missing"] == ["error"]
    assert rows[0]["stop_reason"] == "no_further_request"


@pytest.mark.parametrize("selector", [False, True])
def test_failures_preserve_completed_answers_without_inventing_success(fixture, monkeypatch, tmp_path, selector):
    bundle, environment = fixture
    decisions = [{"status": "inconclusive", "reason": "disagreement"}] if selector else [decision(["error"])]
    install_agent(monkeypatch, decisions, ["Please paste the error.", URLError("offline")])
    rows = acquisition.collect_acquisition(
        bundle, environment, "baseline", "http://localhost", None, "model", tmp_path / "journal"
    )
    assert rows[0]["status"] == ("selector_inconclusive" if selector else "failed")
    assert len(rows[0]["turns"]) == 1
    assert "turn_completed" in (tmp_path / "journal").read_text()


def test_repeated_and_extra_requests_are_recorded_and_budget_is_bounded(fixture, monkeypatch, tmp_path):
    bundle, environment = fixture
    install_agent(monkeypatch, [decision(["error", "other"])] * 3, ["Paste both again."] * 3)
    rows = acquisition.collect_acquisition(
        bundle, environment, "baseline", "http://localhost", None, "model", tmp_path / "journal",
        max_replies=2,
    )
    assert rows[0]["stop_reason"] == "reply_budget_exhausted"
    assert len(rows[0]["turns"]) == 3 and len(rows[0]["disclosures"]) == 2
    assert rows[0]["disclosures"][1]["repeated_fields"] == ["error", "other"]
    assert rows[0]["disclosures"][0]["extra_fields"] == ["other"]
