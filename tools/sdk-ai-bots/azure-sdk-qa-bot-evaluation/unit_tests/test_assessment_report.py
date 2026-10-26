"""Hermetic coverage of API assessment collection and published artifact reporting."""

from __future__ import annotations

import copy
import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import evals_run
from _assessment_report import AssessmentReport, _assessment, _block, _cell
from _evals_result import EvalsResult, VerificationResult
from _evals_runner import (
    COMPLETION_ITEM_SCHEMA,
    CompletionCollector,
    FoundryEvalsRunner,
    _completion_item,
    output_items_to_rows,
)

ASPECTS = ("support", "fit", "source_quality", "consistency", "coverage")
SECRET = "SECRET-SDK-ERROR-MUST-NOT-BE-PUBLISHED"


def metadata(value="pass"):
    return {
        "schema_version": "confidence-runtime-v3",
        "rubric_version": "evidence-v3",
        "status": "abstained" if value is None else "valid",
        "aspects": None if value is None else {
            name: {"value": value, "reason": "The inspected evidence explains this rating."}
            for name in ASPECTS
        },
        "provisional_band": {
            "pass": "High", "limited": "Medium", "fail": "Low", "not_applicable": None, None: None,
        }[value],
    }


def response(index=1, value="pass"):
    return {
        "id": f"response-{index}", "answer": f"Complete answer {index}",
        "assessment": metadata(value), "full_context": '[{"document_title":"Guide","document_link":"https://example.com"}]',
        "references": [{"title": "Guide", "link": "https://example.com"}],
    }


def records():
    return [
        {"testcase": "Duplicate title", "query": "Question one", "ground_truth": "Expected one"},
        {"testcase": "Duplicate title", "query": "Question two", "ground_truth": "Expected two"},
    ]


def report(tmp_path, rows=None):
    result = AssessmentReport(tmp_path, dataset="qa-bot-basic-apispec:latest", run_context="offline-test")
    result.bind_inputs(rows if rows is not None else records(), "apispec")
    return result


@pytest.mark.parametrize("value", ["pass", "limited", "fail", "not_applicable", None])
def test_shared_schema_preserves_complete_metadata(value):
    raw = response(value=value)
    state, validated, diagnostic = _assessment(raw)
    assert state == ("abstained" if value is None else "valid")
    assert validated == raw["assessment"]
    assert diagnostic is None


@pytest.mark.parametrize("change,state", [
    ("legacy", "legacy"), ("null", "absent"), ("array", "malformed"),
    ("version", "unsupported_version"), ("rubric", "unsupported_version"),
    ("reason", "malformed"), ("aspect", "malformed"), ("band", "malformed"),
    ("extra", "malformed"), ("status", "malformed"), ("synthetic", "synthetic"),
])
def test_unavailable_states_never_export_raw_invalid_metadata(change, state):
    raw = response()
    if change == "legacy":
        del raw["assessment"]
    elif change == "null":
        raw["assessment"] = None
    elif change == "array":
        raw["assessment"] = [SECRET]
    elif change == "version":
        raw["assessment"]["schema_version"] = SECRET
    elif change == "rubric":
        raw["assessment"]["rubric_version"] = SECRET
    elif change == "reason":
        raw["assessment"]["aspects"]["support"]["reason"] = SECRET + "\nsecond line"
    elif change == "aspect":
        del raw["assessment"]["aspects"]["coverage"]
    elif change == "band":
        raw["assessment"]["provisional_band"] = "Low"
    elif change == "extra":
        raw["assessment"]["credential"] = SECRET
    elif change == "status":
        raw["assessment"]["status"] = SECRET
    else:
        raw["id"] = "content-filter"
    actual, validated, diagnostic = _assessment(raw)
    assert actual == state
    assert validated is None
    assert diagnostic and SECRET not in diagnostic


def test_markdown_preserves_entire_answers_without_active_content(tmp_path):
    question = "# Spoofed heading\n<img src=x onerror=alert(1)>\n```"
    answer = "Start\n" + "x" * 20000 + "\n`````\n[click](javascript:alert(1))\n<script>bad()</script>\nEnd"
    ledger = report(tmp_path, [{"testcase": "| hostile title", "query": question}])
    raw = response()
    raw["answer"] = answer
    reason = "| [link](javascript:alert(1)) <img src=x onerror=bad()>"
    raw["assessment"]["aspects"]["support"]["reason"] = reason
    ledger.collected(0, raw)
    ledger.finish(0)
    text = (ledger.directory / "report.md").read_text(encoding="utf-8")
    data = json.loads((ledger.directory / "results.json").read_text(encoding="utf-8"))
    assert _block(question) in text
    assert _block(answer) in text
    assert _block(answer).startswith("``````text\n")
    assert _cell(reason) in text
    assert "<img" not in _cell(reason)
    assert "\\|" in _cell(reason) and "\\[" in _cell(reason)
    assert data["cases"][0]["answer"] == answer
    assert data["cases"][0]["assessment"] == raw["assessment"]
    assert data["outcome"] == "passed"


def test_abstention_is_not_all_not_applicable_and_invocations_do_not_overwrite(tmp_path):
    first, second = report(tmp_path), report(tmp_path)
    assert first.directory != second.directory
    first.collected(0, response(value=None))
    first.collected(1, response(2, "not_applicable"))
    first.finish(0)
    cases = json.loads((first.directory / "results.json").read_text())["cases"]
    assert cases[0]["state"] == "abstained" and cases[0]["assessment"]["aspects"] is None
    assert cases[1]["state"] == "valid" and len(cases[1]["assessment"]["aspects"]) == 5
    section = first.markdown().split("## case-000001", 1)[1].split("## case-000002", 1)[0]
    assert "| support | Unavailable |" in section
    assert "not\\_applicable" not in section


def test_collection_keeps_input_order_and_duplicate_title_failure_identity(tmp_path, monkeypatch):
    ledger = report(tmp_path)

    async def call(self, question, tenant_id, session):
        if question == "Question one":
            raise RuntimeError(SECRET)
        return response(2, "fail")

    monkeypatch.setattr(CompletionCollector, "_call_bot_api", call)
    collector = CompletionCollector(api_url="http://unused.invalid", access_token=None, assessment_report=ledger)
    items = collector.collect(records(), None)
    assert len(items) == 1 and items[0]["case_id"] == "case-000002"
    assert ledger.data["cases"][0]["state"] == "collection_failed"
    assert ledger.data["cases"][1]["assessment"] == metadata("fail")
    assert SECRET not in (ledger.directory / "results.json").read_text()
    assert _completion_item(items[0])["context"] == response(2)["full_context"]
    assert "assessment" not in _completion_item(items[0])
    assert "assessment_report" not in _completion_item(items[0])


@pytest.mark.parametrize("branch", ["feature/confidence", "refs/heads/feature/confidence"])
def test_branch_routing_preserves_assessment_and_original_question(tmp_path, monkeypatch, branch):
    rows = records()[:1]
    ledger = report(tmp_path, rows)
    monkeypatch.setenv("EVAL_AGENT_BRANCH", branch)
    session = MagicMock()
    session.__aenter__ = AsyncMock(return_value=session)
    session.__aexit__ = AsyncMock(return_value=False)
    session.post.return_value.__aenter__ = AsyncMock(return_value=SimpleNamespace(
        status=200, json=AsyncMock(return_value=response()),
    ))
    monkeypatch.setattr("aiohttp.ClientSession", MagicMock(return_value=session))
    collector = CompletionCollector(
        api_url="http://unused.invalid", access_token=None, assessment_report=ledger,
    )

    items = collector.collect(rows, "api_spec_review_bot")

    payload = session.post.call_args.kwargs["json"]
    assert payload["message"]["content"] == "[branch:feature/confidence] Question one"
    assert payload["tenant_id"] == "api_spec_review_bot"
    assert items[0]["query"] == ledger.data["cases"][0]["question"] == rows[0]["query"]
    assert items[0]["case_id"] == ledger.data["cases"][0]["case_id"]
    assert ledger.data["cases"][0]["state"] == "valid"
    assert ledger.data["cases"][0]["assessment"] == metadata()
    assert _completion_item(items[0])["query"] == rows[0]["query"]
    assert "assessment" not in _completion_item(items[0])


def test_reordered_grades_join_by_case_id_not_title_and_leave_scores_unchanged(tmp_path):
    ledger = report(tmp_path)
    items = []
    for index, row in enumerate(records()):
        raw = response(index + 1, "pass" if index == 0 else "fail")
        ledger.collected(index, raw)
        items.append({
            **row, "response": raw["answer"], "response_id": raw["id"],
            "case_id": ledger.case_id(index), "context": raw["full_context"],
        })
    output = [
        {"datasource_item": _completion_item(item), "results": [{"name": "similarity", "score": 4.0, "passed": True}]}
        for item in reversed(items)
    ]
    baseline = output_items_to_rows(output, ["similarity"])
    actual = output_items_to_rows(output, ["similarity"], assessment_report=ledger)
    for row, expected in zip(actual["rows"], baseline["rows"]):
        attached = row["inputs.execution"].pop("assessment_report")
        assert attached["case_id"] == ("case-000002" if row["inputs.query"] == "Question two" else "case-000001")
        assert row == expected
    assert [case["evaluation"]["similarity.similarity"] for case in ledger.data["cases"]] == [4.0, 4.0]
    assert [case["assessment"]["provisional_band"] for case in ledger.data["cases"]] == ["High", "Low"]


@pytest.mark.parametrize("change", ["unknown", "mismatched", "mismatched_answer", "repeated"])
def test_ambiguous_or_corrupt_case_joins_fail_explicitly(tmp_path, change):
    ledger = report(tmp_path)
    ledger.collected(0, response())
    item = {
        **records()[0], "case_id": "case-000001", "response_id": "response-1", "response": "Complete answer 1",
    }
    if change == "unknown":
        item["case_id"] = "case-999999"
    elif change == "mismatched":
        item["response_id"] = "wrong-response"
    elif change == "mismatched_answer":
        item["response"] = "A different answer"
    output = [{"datasource_item": item, "results": []}]
    if change == "repeated":
        output_items_to_rows(output, [], assessment_report=ledger)
    with pytest.raises(ValueError):
        output_items_to_rows(output, [], assessment_report=ledger)


def test_retrieval_failure_keeps_all_collected_assessments_without_error_secrets(tmp_path):
    ledger = report(tmp_path)
    for index in range(2):
        ledger.collected(index, response(index + 1))
    client = MagicMock()
    client.responses.retrieve.side_effect = [{"output": []}, RuntimeError(SECRET)]
    items = [{"case_id": ledger.case_id(index), "response_id": f"response-{index + 1}"} for index in range(2)]
    with pytest.raises(RuntimeError, match=SECRET):
        FoundryEvalsRunner._retrieve_tool_calls(client, items, ledger)
    ledger.finish(1)
    data = json.loads((ledger.directory / "results.json").read_text())
    assert [case["state"] for case in data["cases"]] == ["valid", "valid"]
    assert [case["retrieval_status"] for case in data["cases"]] == ["retrieved", "retrieval_failed"]
    assert data["outcome"] == "failed"
    assert SECRET not in (ledger.directory / "results.json").read_text()
    assert SECRET not in (ledger.directory / "report.md").read_text()


def fake_clients():
    evals, responses, project = MagicMock(), MagicMock(), MagicMock()
    evals.__enter__.return_value = evals
    responses.__enter__.return_value = responses
    project.__enter__.return_value = project
    project.get_openai_client.side_effect = [evals, responses]
    evals.evals.create.return_value = SimpleNamespace(id="evaluation")
    evals.evals.runs.create.return_value = SimpleNamespace(id="run")
    evals.evals.runs.retrieve.return_value = SimpleNamespace(status="completed", report_url=None)
    evals.evals.runs.output_items.list.side_effect = lambda **kwargs: [
        {
            "datasource_item": content["item"],
            "results": [{"name": "similarity", "score": 4.0, "passed": True}],
        }
        for content in reversed(evals.evals.runs.create.call_args.kwargs["data_source"]["source"]["content"])
    ]
    responses.responses.retrieve.return_value = {"output": []}
    return evals, responses, project


def configure_main(tmp_path, monkeypatch):
    import azure.ai.projects

    created = []
    original = AssessmentReport

    def factory(cache_dir, **kwargs):
        result = original(tmp_path, **kwargs)
        created.append(result)
        return result

    monkeypatch.setattr(evals_run, "AssessmentReport", factory)
    monkeypatch.setenv("AZURE_AI_PROJECT_ENDPOINT", "https://unused.invalid")
    monkeypatch.setenv("AZURE_EVALUATION_MODEL_NAME", "unused-model")
    monkeypatch.setattr(evals_run, "credential_for", lambda is_ci: object())
    monkeypatch.setattr(evals_run, "resolve_bot_token", lambda: None)
    monkeypatch.setattr(evals_run, "retrieve_channel_tenant_map", lambda credential: {"default": "api_spec_review_bot"})
    monkeypatch.setattr(evals_run, "resolve_records", lambda *args, **kwargs: (records(), "apispec"))
    monkeypatch.setattr(evals_run, "_load_suppression", lambda root: {"evaluators": [], "testcases": []})
    monkeypatch.setattr(evals_run, "_cache_results", MagicMock())
    monkeypatch.setattr(EvalsResult, "show_results", lambda *args: None)
    clients = fake_clients()
    monkeypatch.setattr(azure.ai.projects, "AIProjectClient", lambda **kwargs: clients[2])

    async def call(self, question, tenant_id, session):
        return response(1 if question == "Question one" else 2)

    monkeypatch.setattr(CompletionCollector, "_call_bot_api", call)
    return created, clients


CLI = ["--dataset", "qa-bot-basic-apispec:latest", "--evaluators", "similarity",
       "--baseline_check", "False", "--is_ci", "False", "--cache_result", "full", "--assessment_report"]


def test_full_cli_creates_report_and_keeps_grader_payload_and_schema_clean(tmp_path, monkeypatch):
    created, (client, _, _) = configure_main(tmp_path, monkeypatch)
    schema_before = copy.deepcopy(COMPLETION_ITEM_SCHEMA)
    assert evals_run.main(CLI) == 0
    data = json.loads((created[0].directory / "results.json").read_text())
    assert data["outcome"] == "passed" and data["counts"] == {"valid": 2}
    assert data["evaluation_id"] == "evaluation" and data["evaluation_run_id"] == "run"
    assert [case["case_id"] for case in data["cases"]] == ["case-000001", "case-000002"]
    assert all(case["grading_status"] == "completed" for case in data["cases"])
    contents = client.evals.runs.create.call_args.kwargs["data_source"]["source"]["content"]
    for content in contents:
        item = content["item"]
        assert "assessment" not in item and "assessment_report" not in item
        assert item["context"] == response()["full_context"]
    assert COMPLETION_ITEM_SCHEMA == schema_before
    assert "case_id" in client.evals.create.call_args.kwargs["data_source_config"]["item_schema"]["properties"]
    assert "case_id" in client.evals.create.call_args.kwargs["data_source_config"]["item_schema"]["required"]
    cached = evals_run._cache_results.call_args.args[2]["apispec_run"]
    assert cached[-1]["similarity_fail_rate"] == 0
    assert cached[0]["execution"]["assessment_report"]["case_id"] == "case-000002"


def test_full_cli_without_report_preserves_original_item_and_execution_shapes(tmp_path, monkeypatch):
    created, (client, _, _) = configure_main(tmp_path, monkeypatch)
    assert evals_run.main(CLI[:-1]) == 0
    assert created == []
    assert client.evals.create.call_args.kwargs["data_source_config"]["item_schema"] == COMPLETION_ITEM_SCHEMA
    contents = client.evals.runs.create.call_args.kwargs["data_source"]["source"]["content"]
    assert all(set(content["item"]) == set(COMPLETION_ITEM_SCHEMA["properties"]) for content in contents)
    cached = evals_run._cache_results.call_args.args[2]["apispec_run"]
    assert all(set(case["execution"]) == {"response_id", "tool_calls"} for case in cached[:-1])


@pytest.mark.parametrize("value", ["fail", None, "legacy", "malformed", "unsupported"])
def test_assessment_state_never_changes_existing_grades_or_gate(tmp_path, monkeypatch, value):
    created, _ = configure_main(tmp_path, monkeypatch)

    async def call(self, question, tenant_id, session):
        raw = response(1 if question == "Question one" else 2, "pass" if value in ("legacy", "malformed", "unsupported") else value)
        if value == "legacy":
            del raw["assessment"]
        elif value == "malformed":
            raw["assessment"]["credential"] = SECRET
        elif value == "unsupported":
            raw["assessment"]["schema_version"] = "future-version"
        return raw

    monkeypatch.setattr(CompletionCollector, "_call_bot_api", call)
    assert evals_run.main(CLI) == 0
    assert all(case["evaluation"]["similarity.similarity"] == 4.0 for case in created[0].data["cases"])
    assert SECRET not in (created[0].directory / "results.json").read_text()


def test_normal_full_cache_retains_assessment_in_execution(tmp_path):
    ledger = report(tmp_path)
    ledger.collected(0, response())
    output = [{
        "datasource_item": {
            **records()[0], "case_id": "case-000001", "response_id": "response-1", "response": "Complete answer 1",
        },
        "results": [{"name": "similarity", "score": 4.0, "passed": True}],
    }]
    metrics = {"similarity": ["similarity"]}
    results = EvalsResult(metrics, None)
    raw = output_items_to_rows(output, ["similarity"], assessment_report=ledger)
    all_results = {"apispec_run": results.record_run_result(raw)}
    evals_run._cache_results(
        "full", tmp_path, all_results, metrics, {"evaluators": [], "testcases": []}, results,
    )
    cached = json.loads(next((tmp_path / "cache").glob("apispec-result-*.json")).read_text())
    assert cached[0]["execution"]["assessment_report"]["assessment"] == metadata()
    assert cached[0]["actual"]["answer"] == "Complete answer 1"


def test_existing_suppressed_warning_is_visible_without_failing_run(tmp_path, monkeypatch):
    created, _ = configure_main(tmp_path, monkeypatch)
    monkeypatch.setattr(EvalsResult, "verify_results", lambda *args: VerificationResult.PASS_WITH_WARNING)
    assert evals_run.main(CLI) == 0
    assert created[0].data["verification_result"] == VerificationResult.PASS_WITH_WARNING.value
    assert created[0].data["outcome"] == "passed"


@pytest.mark.parametrize("phase", ["client", "dataset", "retrieval", "evaluation", "run", "poll", "output", "cache", "verification"])
def test_main_finalizes_partial_artifacts_without_changing_failure_exit(tmp_path, monkeypatch, phase):
    created, (client, responses, project) = configure_main(tmp_path, monkeypatch)
    failure = RuntimeError(SECRET)
    if phase == "client":
        project.__enter__.side_effect = failure
    elif phase == "dataset":
        monkeypatch.setattr(evals_run, "resolve_records", MagicMock(side_effect=failure))
    elif phase == "retrieval":
        responses.responses.retrieve.side_effect = failure
    elif phase == "evaluation":
        client.evals.create.side_effect = failure
    elif phase == "run":
        client.evals.runs.create.side_effect = failure
    elif phase == "poll":
        client.evals.runs.retrieve.side_effect = failure
    elif phase == "output":
        client.evals.runs.output_items.list.side_effect = failure
    elif phase == "cache":
        evals_run._cache_results.side_effect = failure
    else:
        monkeypatch.setattr(EvalsResult, "verify_results", lambda *args: VerificationResult.FAIL)
    assert evals_run.main(CLI) == 1
    ledger = created[0]
    data = json.loads((ledger.directory / "results.json").read_text())
    assert data["outcome"] == "failed" and data["finished_at"]
    assert SECRET not in (ledger.directory / "results.json").read_text()
    assert SECRET not in (ledger.directory / "report.md").read_text()
    if phase not in ("client", "dataset"):
        assert [case["state"] for case in data["cases"]] == ["valid", "valid"]


def test_all_collection_failures_still_fail_existing_gate(tmp_path, monkeypatch):
    created, _ = configure_main(tmp_path, monkeypatch)
    monkeypatch.setattr(CompletionCollector, "_call_bot_api", AsyncMock(side_effect=RuntimeError(SECRET)))
    assert evals_run.main(CLI) == 1
    data = json.loads((created[0].directory / "results.json").read_text())
    assert data["counts"] == {"collection_failed": 2}
    assert all(case["evaluation"]["similarity.similarity_result"] == "fail" for case in data["cases"])


def test_startup_failure_has_an_artifact_before_collection(tmp_path, monkeypatch):
    created, _ = configure_main(tmp_path, monkeypatch)
    monkeypatch.delenv("AZURE_EVALUATION_MODEL_NAME", raising=False)
    monkeypatch.setattr(evals_run, "load_dotenv", lambda: None)
    assert evals_run.main(CLI) == 1
    assert created[0].data["outcome"] == "failed" and created[0].data["phase"] == "startup"
    assert (created[0].directory / "report.md").exists()


def test_report_requires_full_cache_and_disabled_mode_does_not_create_artifacts(tmp_path, monkeypatch):
    with pytest.raises(SystemExit) as error:
        evals_run.main(["--dataset", "unused", "--assessment_report"])
    assert error.value.code == 2
    monkeypatch.setattr(evals_run, "_run_evaluation", lambda args, report: 0 if report is None else 1)
    monkeypatch.setattr(evals_run, "AssessmentReport", MagicMock(side_effect=AssertionError("must not instantiate")))
    assert evals_run.main(["--dataset", "unused"]) == 0


def test_report_finalization_error_cannot_turn_into_success(tmp_path, monkeypatch):
    configure_main(tmp_path, monkeypatch)
    monkeypatch.setattr(AssessmentReport, "finish", MagicMock(side_effect=OSError("unavailable disk")))
    assert evals_run.main(CLI) == 1


@pytest.mark.parametrize("scenario", ["apispec", "apispecconfidence"])
def test_assessment_cache_and_baseline_stay_scenario_scoped(tmp_path, monkeypatch, scenario):
    import _evals_result

    monkeypatch.setattr(_evals_result, "__file__", str(tmp_path / "_evals_result.py"))
    result = EvalsResult({"similarity": ["similarity"]}, None)
    case = {
        "testcase": "Example", "similarity": 4.0, "similarity_result": "pass",
        "actual": {"answer": "Complete answer", "context": "Existing document context"},
        "execution": {"response_id": "response-1", "assessment_report": {"assessment": metadata()}},
    }
    results = {f"{scenario}_evalrun_1234": [case, {"total_evals": 1}]}
    result.establish_baseline(results, is_ci=True)
    baseline = json.loads((tmp_path / "results" / ".log" / f"{scenario}-test.json").read_text())
    assert "execution" not in baseline[0] and "assessment" not in json.dumps(baseline)
    assert baseline[0]["actual"] == {"answer": "Complete answer"}

    case["similarity_result"] = "fail"
    evals_run._cache_results(
        "full", tmp_path, results, {"similarity": ["similarity"]}, {"evaluators": []}, result,
    )
    cache = tmp_path / "cache"
    assert len(list(cache.glob("*.json"))) == 2
    assert json.loads(next(cache.glob(f"{scenario}-result-*.json")).read_text()) == results[next(iter(results))]
    assert json.loads(next(cache.glob(f"{scenario}-failed-cases-*.json")).read_text()) == [case]
