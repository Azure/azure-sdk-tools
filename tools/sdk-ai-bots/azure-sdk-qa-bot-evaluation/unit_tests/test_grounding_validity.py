"""Evidence validity is distinct from the evaluator's reported score."""

import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from _evals_result import EvalsResult, VerificationResult
from _evals_runner import _completion_item, output_items_to_rows, resolve_tenant_for_scenario
from evals_run import OUTPUT_FIELDS, _cache_results


def _graded(context="Independent source", *, rendered_context=None, response="Answer", passed=True):
    item = {"testcase": "case", "query": "Question", "response": response,
            "ground_truth": "Expected", "context": context, "response_id": "response-id"}
    result = {
        "name": "groundedness", "score": 5 if passed else 1, "passed": passed,
        "sample": {"input": [{"role": "user", "content": json.dumps({
            "context": context if rendered_context is None else rendered_context,
            "response": response,
        })}]},
    }
    return {"datasource_item": {"item": _completion_item(item)}, "results": [result]}


@pytest.mark.parametrize(("context", "reason"), [
    ("", "missing_context"),
    (" \n", "missing_context"),
    ("Answer", "answer_used_as_context"),
    ("[]", "missing_document_content"),
    ("{}", "missing_document_content"),
    ("null", "missing_document_content"),
    ('[{"document_title":"Only metadata","document_link":"https://example.com"}]',
     "missing_document_content"),
])
def test_missing_and_self_context_never_pass(context, reason):
    raw = _graded(context)
    row = output_items_to_rows([raw], ["groundedness"])["rows"][0]
    assert row["outputs.groundedness.groundedness_result"] == "not_assessable"
    assert row["outputs.groundedness.groundedness_reason"] == reason
    assert "outputs.groundedness.groundedness" not in row
    assert row["inputs.execution"]["groundedness_grader_result"] == raw["results"][0]


@pytest.mark.parametrize(("rendered", "reason"), [
    ("Answer", "answer_used_as_rendered_context"),
    ("Different source", "rendered_context_mismatch"),
])
def test_rendered_evidence_must_match_submitted_evidence(rendered, reason):
    row = output_items_to_rows([_graded(rendered_context=rendered)], ["groundedness"])["rows"][0]
    assert row["outputs.groundedness.groundedness_reason"] == reason
    assert row["outputs.groundedness.groundedness_result"] == "not_assessable"


def test_uninspectable_grader_input_is_not_assessable():
    raw = _graded()
    raw["results"][0]["sample"] = None
    row = output_items_to_rows([raw], ["groundedness"])["rows"][0]
    assert row["outputs.groundedness.groundedness_reason"] == "rendered_context_unavailable"


@pytest.mark.parametrize("score", [None, True, "5", 0, 6, float("nan")])
def test_invalid_provider_score_cannot_be_a_grounding_pass(score):
    raw = _graded()
    raw["results"][0]["score"] = score
    row = output_items_to_rows([raw], ["groundedness"])["rows"][0]
    assert row["outputs.groundedness.groundedness_reason"] == "invalid_grader_result"
    assert row["outputs.groundedness.groundedness_result"] == "not_assessable"


def test_a_different_answer_in_grader_input_is_not_assessable():
    raw = _graded()
    raw["results"][0]["sample"]["input"][0]["content"] = json.dumps({
        "context": "Independent source", "response": "Different answer",
    })
    row = output_items_to_rows([raw], ["groundedness"])["rows"][0]
    assert row["outputs.groundedness.groundedness_reason"] == "rendered_response_mismatch"


@pytest.mark.parametrize("passed", [True, False])
def test_valid_evidence_preserves_grounding_score_and_verdict(passed):
    context = json.dumps([{"document_content": "Independent source", "document_source": "kb"}])
    row = output_items_to_rows([_graded(context, passed=passed)], ["groundedness"])["rows"][0]
    assert row["inputs.context"] == context
    assert row["inputs.execution"]["response_id"] == "response-id"
    assert row["outputs.groundedness.groundedness"] == (5 if passed else 1)
    assert row["outputs.groundedness.groundedness_result"] == ("pass" if passed else "fail")


def test_missing_grounding_result_is_not_a_pass():
    raw = _graded()
    raw["results"] = []
    row = output_items_to_rows([raw], ["groundedness"])["rows"][0]
    assert row["outputs.groundedness.groundedness_reason"] == "missing_grader_result"


def test_not_assessable_counts_separately_and_blocks_grounding_gate(tmp_path):
    metric_fields = {"groundedness": OUTPUT_FIELDS["groundedness"]}
    result = EvalsResult(metrics=metric_fields, suppressions={"testcases": ["case"]})
    rows = output_items_to_rows([_graded("")], ["groundedness"])
    recorded = result.record_run_result(rows)
    assert recorded[-1]["groundedness_pass_rate"] == 0
    assert recorded[-1]["groundedness_fail_rate"] == 0
    assert recorded[-1]["groundedness_not_assessable_rate"] == 1
    assert result.verify_results({"apispec_run-id": recorded}, False) == VerificationResult.FAIL
    assert "N/A" in result.build_output_table(recorded)
    assert "not_assessable" in result.build_output_table(recorded)
    baseline = {"case": {"groundedness": 5}}
    assert "N/A" in result.build_output_table(recorded, baseline)
    _cache_results("full", tmp_path, {"apispec_run-id": recorded}, metric_fields,
                   {"evaluators": [], "testcases": []}, result)
    failed = next((tmp_path / "cache").glob("*-failed-cases-*.json"))
    assert json.loads(failed.read_text())[0]["groundedness_result"] == "not_assessable"


def test_explicit_evaluator_suppression_still_warns_on_unassessable_grounding():
    result = EvalsResult(metrics={"groundedness": None}, suppressions={"evaluators": ["groundedness"]})
    recorded = result.record_run_result(output_items_to_rows([_graded("")], ["groundedness"]))
    assert result.verify_results({"apispec": recorded}, False) == VerificationResult.PASS_WITH_WARNING


def test_cache_preserves_multiple_run_artifacts_at_the_same_timestamp(tmp_path, monkeypatch):
    import evals_run
    from datetime import datetime

    class Clock:
        @staticmethod
        def now():
            return datetime(2026, 10, 13, 13, 2, 43, 123456)

    monkeypatch.setattr(evals_run, "datetime", Clock)
    result = EvalsResult(metrics={"groundedness": None}, suppressions=None)
    recorded = result.record_run_result(output_items_to_rows([_graded()], ["groundedness"]))
    for run in ("apispec_run-a", "apispec_run-b"):
        _cache_results("full", tmp_path, {run: recorded}, {"groundedness": None},
                       {"evaluators": [], "testcases": []}, result)
    paths = list((tmp_path / "cache").glob("*-result-*.json"))
    assert len(paths) == 2
    assert all("13-02-43-123456" in path.name for path in paths)
    with pytest.raises(FileExistsError):
        _cache_results("full", tmp_path, {"apispec_run-a": recorded}, {"groundedness": None},
                       {"evaluators": [], "testcases": []}, result)


def test_no_routing_map_is_an_error_not_default_tenant():
    with pytest.raises(ValueError, match="channel->tenant map"):
        resolve_tenant_for_scenario("apispec", {})
    with pytest.raises(ValueError, match="No evaluation tenant"):
        resolve_tenant_for_scenario("apispec", {"unrelated": "tenant"})


def test_unreadable_routing_map_stops_before_bot_collection(monkeypatch):
    import azure.ai.projects
    import evals_run
    from unittest.mock import MagicMock

    monkeypatch.setenv("AZURE_AI_PROJECT_ENDPOINT", "https://example.invalid")
    monkeypatch.setenv("AZURE_EVALUATION_MODEL_NAME", "test-model")
    monkeypatch.setattr(evals_run, "credential_for", lambda _: object())
    monkeypatch.setattr(evals_run, "resolve_bot_token", lambda: None)
    monkeypatch.setattr(azure.ai.projects, "AIProjectClient", MagicMock())
    collect = MagicMock()
    monkeypatch.setattr(evals_run.FoundryEvalsRunner, "evaluate_run_completion", collect)

    def unreadable(_):
        raise PermissionError("Cannot read channel mapping")

    monkeypatch.setattr(evals_run, "retrieve_channel_tenant_map", unreadable)
    assert evals_run.main(["--dataset", "qa-bot-basic-apispec:latest", "--is_ci", "False"]) == 1
    collect.assert_not_called()
