"""Offline tests: isolation, leakage, failures, grading contract and paired results."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from _evals_result import EvalsResult
from _evals_runner import FoundryEvalsRunner, _completion_item, output_items_to_rows
from decision_study import (
    ARMS, collect_local, collect_replay, load_bundle, load_cases, local_request,
    main, make_local_variant, messages_for, paired_summary, parse_local_response,
    prepare, run_local, summarize_local, verify_local_receipt,
)
from eval.criteria import DECISION_RUBRICS, build_testing_criteria


@pytest.fixture
def prepared(tmp_path):
    case = {
        "testcase": "missing-actor", "query": "403: who needs access?",
        "ground_truth": "PRIVATE_REFERENCE", "expected_behavior": "PRIVATE_RUBRIC",
        "reviewed": "todo", "scenario": "apispec", "source": "synthetic:test",
        "case_group": "actor", "evidence": "Only status=403 is known", "guide_ids": ["access"],
    }
    guide = {
        "id": "access", "title": "Access", "lesson": "Identify actor first.",
        "inspect": ["Actor"], "avoid": ["Assume author"], "context_dependencies": [],
        "conditional_actions": [], "owner_role": "Access owner", "stop_when": "Actor known",
        "evidence": ["PRIVATE_SOURCE_ID"],
    }
    paths = {}
    for name, content in {
        "apispec.jsonl": json.dumps(case) + "\n",
        "baseline.txt": "BASELINE_ONLY",
        "general.txt": "GENERAL_ONLY",
        "guides.json": json.dumps({"version": "test", "guides": [guide]}),
    }.items():
        path = tmp_path / name
        path.write_text(content, encoding="utf-8")
        paths[name] = path
    bundle = prepare(
        paths["apispec.jsonl"], [paths["baseline.txt"]], paths["general.txt"],
        paths["guides.json"], tmp_path / "prepared", repeats=2,
    )
    return bundle, paths, tmp_path


def test_preparation_balanced_and_reproducible(prepared):
    bundle, paths, tmp = prepared
    assert len(bundle["jobs"]) == 8
    for repeat in range(2):
        assert {job["arm"] for job in bundle["jobs"] if job["repeat"] == repeat} == set(ARMS)
    again = prepare(
        paths["apispec.jsonl"], [paths["baseline.txt"]], paths["general.txt"],
        paths["guides.json"], tmp / "again", repeats=2,
    )
    assert bundle["jobs"] == again["jobs"]
    assert load_bundle(tmp / "prepared" / "bundle.json") == bundle


@pytest.mark.parametrize("arm", ARMS)
def test_generation_excludes_answers_rubrics_and_provenance(prepared, arm):
    bundle, _, _ = prepared
    messages = messages_for(bundle, bundle["cases"][0], arm)
    content = json.dumps(messages)
    assert "PRIVATE_" not in content
    assert "BASELINE_ONLY" in content
    assert ("GENERAL_ONLY" in content) == (arm in ("general", "combined"))
    assert ("Identify actor first" in content) == (arm in ("topic", "combined"))
    assert messages[-1]["content"] == messages_for(bundle, bundle["cases"][0], "baseline")[-1]["content"]


def test_bundle_tamper_rejected(prepared):
    _, _, tmp = prepared
    path = tmp / "prepared" / "bundle.json"
    data = json.loads(path.read_text())
    data["baseline"] = "changed"
    path.write_text(json.dumps(data))
    with pytest.raises(ValueError, match="integrity"):
        load_bundle(path)


def test_preparation_never_overwrites(prepared):
    _, paths, tmp = prepared
    with pytest.raises(FileExistsError):
        prepare(
            paths["apispec.jsonl"], [paths["baseline.txt"]], paths["general.txt"],
            paths["guides.json"], tmp / "prepared",
        )


@pytest.mark.parametrize("change", ["duplicate", "missing-behavior", "wrong-scenario", "abandoned"])
def test_case_validation(prepared, change):
    bundle, paths, _ = prepared
    case = copy.deepcopy(bundle["cases"][0])
    if change == "missing-behavior":
        del case["expected_behavior"]
    elif change == "wrong-scenario":
        case["scenario"] = "general"
    elif change == "abandoned":
        case["reviewed"] = "abandoned"
    text = json.dumps(case) + "\n"
    paths["apispec.jsonl"].write_text(text * (2 if change == "duplicate" else 1))
    with pytest.raises(ValueError):
        load_cases(paths["apispec.jsonl"])


def test_unknown_guide_fails_before_output(prepared):
    _, paths, tmp = prepared
    paths["guides.json"].write_text('{"guides":[]}')
    with pytest.raises(ValueError, match="Unknown guide"):
        prepare(
            paths["apispec.jsonl"], [paths["baseline.txt"]], paths["general.txt"],
            paths["guides.json"], tmp / "bad",
        )
    assert not (tmp / "bad").exists()


def test_collect_journals_failures_and_usage(prepared):
    bundle, _, tmp = prepared
    client = Mock()
    answer = SimpleNamespace(
        id="chat-id", model="model-version",
        choices=[SimpleNamespace(message=SimpleNamespace(content="Which actor?"))],
        usage=SimpleNamespace(model_dump=lambda **_: {"total_tokens": 17}),
    )
    client.chat.completions.create.side_effect = [ValueError("empty")] + [answer] * 7
    rows = collect_replay(client, bundle, "deployment", tmp / "journal.jsonl")
    assert len(rows) == 8 and rows[0]["status"] == "failed"
    assert all(r["status"] == "completed" for r in rows[1:])
    assert rows[1]["usage"]["total_tokens"] == 17
    assert rows[1]["latency_seconds"] >= 0
    assert len((tmp / "journal.jsonl").read_text().splitlines()) == 16
    assert client.chat.completions.create.call_count == 8
    for call in client.chat.completions.create.call_args_list:
        assert "PRIVATE_" not in json.dumps(call.kwargs)


def test_paired_results_retain_failures_and_missing_grades(prepared):
    bundle, _, _ = prepared
    generations = [
        {**j, "status": "completed", "latency_seconds": 1} for j in bundle["jobs"]
    ]
    graded = [
        {"testcase": j["id"], **{metric: 2 for metric in DECISION_RUBRICS}}
        for j in bundle["jobs"]
    ]
    result = paired_summary(bundle, generations, graded)
    assert result["generation_failures"] == result["grading_failures"] == 0
    assert result["comparisons_to_baseline"]["general"]["ties"] == 2
    assert result["comparisons_to_baseline"]["general"]["group_bootstrap_95_interval"] == [0, 0]
    generations[0]["status"] = "failed"
    graded.pop()
    result = paired_summary(bundle, generations, graded)
    assert result["generation_failures"] == 1
    assert result["grading_failures"] == 1
    assert len(result["samples"]) == 8


def test_critical_failure_cannot_be_averaged_away(prepared):
    bundle, _, _ = prepared
    job = bundle["jobs"][0]
    grades = [{"testcase": job["id"], **{metric: 2 for metric in DECISION_RUBRICS}}]
    grades[0]["authority_discipline"] = 0
    result = paired_summary(
        bundle, [{**job, "status": "completed", "latency_seconds": 1}], grades
    )
    sample = next(s for s in result["samples"] if s["id"] == job["id"])
    assert sample["critical_failure"] and sample["utility"] == 0


def test_decision_criteria_opt_in_and_blinded():
    assert build_testing_criteria(["not-a-grader"], model="judge") == []
    criteria = build_testing_criteria(list(DECISION_RUBRICS), model="judge")
    assert len(criteria) == 4
    for criterion in criteria:
        assert criterion["type"] == "score_model"
        assert criterion["range"] == [0, 2] and criterion["pass_threshold"] == 2
        text = json.dumps(criterion)
        assert "{{item.tool_evidence}}" in text and "{{item.expected_behavior}}" in text
        assert "{{item.execution}}" not in text and "{{item.testcase}}" not in text


def test_inline_adapter_preserves_metadata_and_rejects_invalid_grade():
    item = _completion_item({
        "testcase": "opaque-id", "query": "q", "response": "a",
        "expected_behavior": "ask", "execution": {"latency_seconds": 1.2},
    })
    raw = output_items_to_rows([{
        "datasource_item": item, "status": "completed",
        "results": [{"name": "next_action", "score": 2, "passed": True, "sample": {"reason": "Useful"}}],
    }], ["next_action"])
    result = EvalsResult({"next_action": ["next_action"]}, None).record_run_result(raw)[0]
    assert result["execution"]["latency_seconds"] == 1.2
    assert result["execution"]["decision_grader_results"]["next_action"]["sample"]["reason"] == "Useful"
    bad = output_items_to_rows([{
        "datasource_item": item,
        "results": [{"name": "next_action", "score": float("nan"), "passed": False}],
    }], ["next_action"])
    assert "outputs.next_action.next_action" not in bad["rows"][0]


def test_foundry_nested_datasource_item_preserves_sample_identity_and_trace():
    raw = output_items_to_rows([{
        "datasource_item": {
            "item": {
                "testcase": "sample-00001", "query": "q", "response": "a",
                "response_id": "sample-00001", "execution": {"latency_seconds": 1},
            },
            "item.testcase": "sample-00001",
        },
        "status": "completed",
        "results": [{"name": "next_action", "score": 2, "passed": True}],
    }], ["next_action"], tool_calls_by_response_id={
        "sample-00001": [{"tool_name": "search", "output": "evidence"}],
    })
    result = EvalsResult({"next_action": ["next_action"]}, None).record_run_result(raw)[0]
    assert result["testcase"] == "sample-00001"
    assert result["execution"]["tool_calls"] == [
        {"tool_name": "search", "output": "evidence"}
    ]
    with pytest.raises(ValueError, match="datasource"):
        output_items_to_rows([{"datasource_item": {}}], ["next_action"])


@pytest.mark.parametrize("status", ["completed", "pass", "fail"])
def test_quality_verdict_is_not_an_infrastructure_failure(status):
    result = output_items_to_rows([{
        "datasource_item": {"testcase": "t"},
        "status": status,
        "results": [{"name": "next_action", "score": 1, "passed": False}],
    }], ["next_action"])
    assert result["rows"][0]["outputs.next_action.next_action"] == 1


def test_missing_decision_expectations_rejected_before_cloud_call():
    client = Mock()
    runner = FoundryEvalsRunner(
        ["next_action"], EvalsResult({"next_action": ["next_action"]}, None), model="judge"
    )
    with pytest.raises(ValueError, match="expected_behavior"):
        runner.evaluate_collected(
            client, [{"testcase": "t", "response": "answer"}], "apispec",
            tool_calls_by_response_id={},
        )
    client.evals.create.assert_not_called()


def test_full_replay_run_wires_generation_grading_and_summary(prepared, monkeypatch):
    import azure.ai.projects
    import dataset._storage
    from decision_study import run

    bundle, _, tmp = prepared
    client = Mock()
    client.__enter__ = Mock(return_value=client)
    client.__exit__ = Mock(return_value=False)
    client.with_options.return_value = client
    client.chat.completions.create.return_value = SimpleNamespace(
        id="response-id", model="fixed-model-version",
        choices=[SimpleNamespace(message=SimpleNamespace(content="Which actor?"))],
        usage=SimpleNamespace(model_dump=lambda **_: {"total_tokens": 11}),
    )
    client.evals.create.return_value.id = "eval-id"
    client.evals.runs.create.return_value.id = "run-id"
    client.evals.runs.retrieve.return_value = SimpleNamespace(status="completed", report_url="local")

    def output_items(**_):
        content = client.evals.runs.create.call_args.kwargs["data_source"]["source"]["content"]
        return [
            {
                "datasource_item": row["item"], "status": "pass",
                "results": [
                    {"name": metric, "score": 2, "passed": True, "sample": {"reason": "Useful"}}
                    for metric in DECISION_RUBRICS
                ],
            }
            for row in content
        ]

    client.evals.runs.output_items.list.side_effect = output_items
    project = Mock()
    project.__enter__ = Mock(return_value=project)
    project.__exit__ = Mock(return_value=False)
    project.get_openai_client.return_value = client
    credential = Mock()
    credential.__enter__ = Mock(return_value=credential)
    credential.__exit__ = Mock(return_value=False)
    monkeypatch.setattr(azure.ai.projects, "AIProjectClient", Mock(return_value=project))
    monkeypatch.setattr(dataset._storage, "credential_for", Mock(return_value=credential))
    run(bundle, tmp / "run", "https://unused.example", "generation", "judge")
    summary = json.loads((tmp / "run" / "summary.json").read_text())
    assert summary["generation_failures"] == summary["grading_failures"] == 0
    assert len(summary["samples"]) == 8
    assert all(row["mean_utility"] == 2 for row in summary["arms"].values())
    assert client.chat.completions.create.call_count == 8
    for row in client.evals.runs.create.call_args.kwargs["data_source"]["source"]["content"]:
        assert "arm" not in row["item"]["execution"]
        assert "PRIVATE_RUBRIC" == row["item"]["expected_behavior"]


def test_precollected_runner_does_not_regenerate_and_transmits_trace():
    client = Mock()
    client.evals.create.return_value.id = "eval-id"
    client.evals.runs.create.return_value.id = "run-id"
    runner = FoundryEvalsRunner(
        ["next_action"], EvalsResult({"next_action": ["next_action"]}, None), model="judge"
    )
    runner._poll_and_adapt = Mock(return_value={"done": []})
    item = {
        "testcase": "opaque-id", "query": "q", "response": "a",
        "expected_behavior": "ask", "response_id": "response-id",
    }
    trace = {"response-id": [{"tool_name": "read", "output": "actual observation"}]}
    runner.evaluate_collected(client, [item], "apispec", tool_calls_by_response_id=trace)
    client.chat.completions.create.assert_not_called()
    content = client.evals.runs.create.call_args.kwargs["data_source"]["source"]["content"]
    assert "actual observation" in content[0]["item"]["tool_evidence"]
    assert content[0]["item"]["expected_behavior"] == "ask"
    assert "tool_evidence" not in item  # caller data is not mutated


def test_run_requires_explicit_consent_before_any_cloud_call(tmp_path):
    assert main([
        "run", "--bundle", str(tmp_path / "missing"), "--output", str(tmp_path / "out"),
        "--project-endpoint", "https://unused.example", "--model", "m", "--judge-model", "j",
    ]) == 1
    assert not (tmp_path / "out").exists()


@pytest.mark.parametrize("arm", ARMS)
def test_local_variant_contains_only_selected_guidance(prepared, arm):
    bundle, paths, tmp = prepared
    root = tmp / "root.md"
    tenant = tmp / "tenant.md"
    root.write_text("BASELINE_", encoding="utf-8")
    tenant.write_text("ONLY", encoding="utf-8")
    bundle["baseline"] = "BASELINE_\n\nONLY"
    variant = make_local_variant(bundle, arm, root, tenant, tmp / f"{arm}.json")
    content = json.dumps(variant)
    assert "PRIVATE_" not in content
    assert "GENERAL_ONLY" in content if arm in ("general", "combined") else "GENERAL_ONLY" not in content
    assert "Identify actor first" in content if arm in ("topic", "combined") else "Identify actor first" not in content
    assert variant["arm"] == arm
    assert variant["root_sha256"] == hashlib.sha256(b"BASELINE_").hexdigest()


def test_local_variant_refuses_stale_prompts_and_agent_refuses_mismatch(prepared, tmp_path, monkeypatch):
    bundle, _, tmp = prepared
    root, tenant = tmp / "root.md", tmp / "tenant.md"
    root.write_text("BASELINE_", encoding="utf-8")
    tenant.write_text("ONLY", encoding="utf-8")
    bundle["baseline"] = "BASELINE_\n\nONLY"
    with pytest.raises(ValueError, match="differ"):
        make_local_variant(bundle, "topic", root, tmp / "general.txt", tmp / "bad.json")
    variant_path = tmp / "variant.json"
    make_local_variant(bundle, "topic", root, tenant, variant_path)
    agent_root = tmp / "agent"
    prompt = agent_root / "prompts" / "tenants" / "api_spec_review.md"
    prompt.parent.mkdir(parents=True)
    prompt.write_text("ONLY", encoding="utf-8")
    module_path = (Path(__file__).resolve().parents[2] /
                   "azure-sdk-qa-bot-agent" / "agents" / "chat_agent" / "study_variant.py")
    spec = importlib.util.spec_from_file_location("study_variant_test", module_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    receipt = tmp / "receipt.json"
    monkeypatch.setenv("SDK_QA_STUDY_VARIANT_FILE", str(variant_path))
    monkeypatch.setenv("SDK_QA_STUDY_RECEIPT_FILE", str(receipt))
    with pytest.raises(ValueError, match="baseline"):
        module.load_study_variant(agent_root, "different")
    assert not receipt.exists()
    loaded = module.load_study_variant(agent_root, "BASELINE_")
    assert loaded["arm"] == "topic"
    assert verify_local_receipt(bundle, variant_path, receipt) == "topic"
    with pytest.raises(FileExistsError):
        module.load_study_variant(agent_root, "BASELINE_")
    variant_path.write_text(variant_path.read_text() + " ", encoding="utf-8")
    with pytest.raises(ValueError, match="did not start"):
        verify_local_receipt(bundle, variant_path, receipt)


def test_local_request_payload_and_response_trace(monkeypatch):
    captured = {}
    class Response:
        def __enter__(self):
            return self
        def __exit__(self, *_):
            return False
        def read(self, *_):
            return json.dumps({
                "id": "local-1", "status": "completed", "output": [
                    {"type": "function_call", "call_id": "c1", "name": "wiki_search",
                     "arguments": '{"query":"q"}'},
                    {"type": "function_call_output", "call_id": "c1", "output": '{"found":true}'},
                    {"type": "message", "content": [{"type": "output_text", "text": "Ask actor"}]},
                ],
            }).encode()
    def send(request, timeout):
        captured["url"] = request.full_url
        captured["payload"] = json.loads(request.data)
        return Response()
    monkeypatch.setattr("decision_study.urlopen", send)
    result = local_request("http://127.0.0.1:8088", "Question", "Evidence")
    assert captured["url"] == "http://127.0.0.1:8088/responses"
    assert captured["payload"]["input"][0]["content"] == (
        "[tenant_context] original_tenant_id=api_spec_review_bot"
    )
    assert "Evidence" in captured["payload"]["input"][1]["content"]
    assert parse_local_response(result)[2] == [
        {"tool_name": "wiki_search", "arguments": {"query": "q"}, "output": {"found": True}}
    ]
    assert parse_local_response(result)[1] == "Ask actor"
    with pytest.raises(ValueError, match="loopback"):
        local_request("https://example.com", "q", "e")
    with pytest.raises(ValueError, match="status"):
        parse_local_response({**result, "status": "failed"})


def test_local_collection_preserves_failures_and_four_arm_summary(prepared, tmp_path, monkeypatch):
    bundle, _, tmp = prepared
    calls = 0
    def respond(*_):
        nonlocal calls
        calls += 1
        if calls == 1:
            raise ValueError("agent failed")
        return {"id": "reused-id", "output": [
            {"type": "message", "content": [{"type": "output_text", "text": "Which actor?"}]},
        ]}
    monkeypatch.setattr("decision_study.local_request", respond)
    generations, traces = collect_local(bundle, "baseline", "http://127.0.0.1:8088",
                                        tmp / "local.jsonl")
    assert len(generations) == 2 and len(traces) == 1
    assert generations[0]["status"] == "failed"
    assert len((tmp / "local.jsonl").read_text().splitlines()) == 4
    assert generations[1]["response_id"] == generations[1]["id"]
    runs = []
    for arm in ARMS:
        path = tmp / f"run-{arm}"
        path.mkdir()
        rows = [
            {**job, "status": "completed", "latency_seconds": 1}
            for job in bundle["jobs"] if job["arm"] == arm
        ]
        if arm == "topic":
            rows[0]["tool_calls"] = [{
                "tool_name": "load_skill",
                "arguments": {"skill_name": "api-spec-review"},
                "output": "Experimental API Spec Review topic guidance",
            }]
        if arm == "combined":
            rows[0]["tool_calls"] = [{
                "tool_name": "load_skill",
                "arguments": {"skill_name": "sdk-onboarding"},
                "output": "Experimental API Spec Review topic guidance",
            }]
        (path / "run.json").write_text(json.dumps({
            "mode": "local-agent", "arm": arm, "bundle_sha256": bundle["bundle_sha256"]
        }))
        (path / "generations.json").write_text(json.dumps(rows))
        (path / "graded.json").write_text(json.dumps({"run": [
            {"testcase": row["id"], **{key: 2 for key in DECISION_RUBRICS}}
            for row in rows
        ]}))
        runs.append(path)
    summary = summarize_local(bundle, runs, tmp / "local-summary.json")
    assert summary["mode"] == "local-agent" and len(summary["samples"]) == 8
    assert summary["comparisons_to_baseline"]["topic"]["ties"] == 2
    assert summary["arms"]["topic"]["topic_guidance_loaded"] == 1
    assert summary["arms"]["combined"]["topic_guidance_loaded"] == 0
    assert sum(sample["topic_guidance_loaded"] for sample in summary["samples"]) == 1
    with pytest.raises(ValueError, match="Duplicate"):
        summarize_local(bundle, runs[:3] + [runs[0]], tmp / "again.json")


def test_local_run_grades_inline_traces_without_hosted_response_retrieval(prepared, monkeypatch):
    import azure.ai.projects
    import dataset._storage

    bundle, _, tmp = prepared
    root, tenant = tmp / "root.md", tmp / "tenant.md"
    root.write_text("BASELINE_", encoding="utf-8")
    tenant.write_text("ONLY", encoding="utf-8")
    bundle["baseline"] = "BASELINE_\n\nONLY"
    variant_path = tmp / "variant.json"
    make_local_variant(bundle, "topic", root, tenant, variant_path)
    receipt_path = tmp / "receipt.json"
    receipt_path.write_text(json.dumps({
        "arm": "topic", "bundle_sha256": bundle["bundle_sha256"],
        "variant_sha256": hashlib.sha256(variant_path.read_bytes()).hexdigest(),
    }))
    monkeypatch.setattr("decision_study.local_request", lambda *_: {
        "id": "local-only", "status": "completed", "output": [
            {"type": "function_call", "call_id": "c", "name": "wiki_search",
             "arguments": "{}", "output": "evidence"},
            {"type": "message", "content": [{"type": "output_text", "text": "Which actor?"}]},
        ]
    })
    captured = {}
    def grade(self, client, items, scenario, *, tool_calls_by_response_id, **kwargs):
        captured["items"] = items
        captured["traces"] = tool_calls_by_response_id
        return {"graded": [
            {"testcase": item["testcase"], **{key: 2 for key in DECISION_RUBRICS}}
            for item in items
        ]}
    monkeypatch.setattr(FoundryEvalsRunner, "evaluate_collected", grade)
    client = Mock()
    client.__enter__ = Mock(return_value=client)
    client.__exit__ = Mock(return_value=False)
    client.with_options.return_value = client
    project = Mock()
    project.__enter__ = Mock(return_value=project)
    project.__exit__ = Mock(return_value=False)
    project.get_openai_client.return_value = client
    credential = Mock()
    credential.__enter__ = Mock(return_value=credential)
    credential.__exit__ = Mock(return_value=False)
    monkeypatch.setattr(azure.ai.projects, "AIProjectClient", Mock(return_value=project))
    monkeypatch.setattr(dataset._storage, "credential_for", Mock(return_value=credential))
    run_local(
        bundle, tmp / "local-run", variant_path, receipt_path,
        "http://127.0.0.1:8088", "https://unused.example", "judge"
    )
    assert len(captured["items"]) == 2
    assert all(item["response_id"] == item["testcase"] for item in captured["items"])
    assert all(trace[0]["tool_name"] == "wiki_search" for trace in captured["traces"].values())
    client.responses.retrieve.assert_not_called()
    assert (tmp / "local-run" / "graded.json").exists()


def test_seed_cases_are_synthetic_unreviewed_pairs():
    path = Path(__file__).resolve().parents[1] / "evaluation_datasets" / "decision-study" / "apispec.jsonl"
    cases = load_cases(path)
    assert len(cases) == 12
    assert {case["reviewed"] for case in cases} == {"todo"}
    assert all(case["source"].startswith("synthetic:") for case in cases)
    groups = {case["case_group"] for case in cases}
    assert len(groups) == 6
    assert all(sum(c["case_group"] == group for c in cases) == 2 for group in groups)
