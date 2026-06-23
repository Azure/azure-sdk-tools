"""Offline unit tests for the pure logic of the new evaluation package.

These avoid any network/Foundry calls: schema validation and the output-items
adapter.

Run:
    python -m pytest unit_tests -q
    # or without pytest:
    python unit_tests/test_pure_logic.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from dataset.schema import ValidationError, validate_case  # noqa: E402
from _evals_runner import (  # noqa: E402
    output_items_to_rows,
    extract_title_and_link_from_references,
    extract_title_and_link_from_context,
    resolve_tenant_for_scenario,
)
from eval.criteria import build_testing_criteria, BUILTIN_EVALUATORS  # noqa: E402


def test_validate_case_ok():
    validate_case(
        {
            "testcase": "t",
            "query": "q",
            "ground_truth": "a",
            "scenario": "typespec",
            "reviewed": True,
        }
    )


def test_validate_case_missing_field():
    try:
        validate_case({"query": "q", "ground_truth": "a", "scenario": "s", "reviewed": True})
    except ValidationError as exc:
        assert "testcase" in str(exc)
    else:
        raise AssertionError("expected ValidationError")


def test_validate_case_bad_reviewed_type():
    try:
        validate_case({"testcase": "t", "query": "q", "ground_truth": "a", "scenario": "s", "reviewed": "yes"})
    except ValidationError as exc:
        assert "reviewed" in str(exc)
    else:
        raise AssertionError("expected ValidationError")


def test_output_items_to_rows_builtin_and_composite():
    output_items = [
        {
            "datasource_item": {"testcase": "t1", "query": "q1", "ground_truth": "gt1", "response": "the answer"},
            "results": [
                {"name": "similarity", "score": 4.0, "passed": True},
                {"name": "response_completeness", "score": 3.0, "passed": True},
            ],
        }
    ]
    rows = output_items_to_rows(output_items, ["bot_evals"], threshold=3.0)["rows"]
    assert len(rows) == 1
    row = rows[0]
    assert row["inputs.testcase"] == "t1"
    assert row["inputs.response"] == "the answer"
    assert row["outputs.similarity.similarity"] == 4.0
    # composite = 4.0*0.6 + 3.0*0.4 = 3.6 >= 3 -> pass
    assert abs(row["outputs.bot_evals.bot_evals"] - 3.6) < 1e-9
    assert row["outputs.bot_evals.bot_evals_result"] == "pass"


def test_output_items_to_rows_groundedness_fail():
    output_items = [
        {
            "datasource_item": {"testcase": "t2", "ground_truth": "gt", "response": "x"},
            "results": [{"name": "groundedness", "score": 1.0, "passed": False}],
        }
    ]
    rows = output_items_to_rows(output_items, ["groundedness"])["rows"]
    assert rows[0]["outputs.groundedness.groundedness_result"] == "fail"


def test_build_testing_criteria_all_builtins():
    crit = build_testing_criteria(list(BUILTIN_EVALUATORS), model="gpt-4o")
    by_name = {c["evaluator_name"]: c for c in crit}
    # All builtin evaluators are produced.
    assert len(crit) == len(BUILTIN_EVALUATORS)
    # LLM quality with threshold.
    assert by_name["builtin.similarity"]["initialization_parameters"] == {"model": "gpt-4o", "threshold": 3}
    # LLM with model only.
    assert by_name["builtin.relevance"]["initialization_parameters"] == {"model": "gpt-4o"}
    assert by_name["builtin.coherence"]["initialization_parameters"] == {"model": "gpt-4o"}
    assert by_name["builtin.fluency"]["initialization_parameters"] == {"model": "gpt-4o"}
    # Groundedness uses deployment_name + retrieved context from the item.
    assert by_name["builtin.groundedness"]["initialization_parameters"] == {"deployment_name": "gpt-4o"}
    assert by_name["builtin.groundedness"]["data_mapping"]["context"] == "{{item.context}}"
    # Answer is read from the collected item response, ground_truth from the item.
    assert by_name["builtin.similarity"]["data_mapping"]["response"] == "{{item.response}}"
    assert by_name["builtin.similarity"]["data_mapping"]["ground_truth"] == "{{item.ground_truth}}"


def test_build_testing_criteria_bot_evals_expands():
    crit = build_testing_criteria(["bot_evals"], model="gpt-4o")
    names = {c["evaluator_name"] for c in crit}
    assert names == {"builtin.similarity", "builtin.response_completeness"}


def test_criteria_read_from_item():
    crit = build_testing_criteria(["similarity", "groundedness"], model="m")
    by_name = {c["evaluator_name"]: c for c in crit}
    assert by_name["builtin.similarity"]["data_mapping"]["response"] == "{{item.response}}"
    assert by_name["builtin.groundedness"]["data_mapping"]["context"] == "{{item.context}}"


def test_extract_references():
    refs = extract_title_and_link_from_references(
        [{"title": "T", "link": "http://x"}, {"Title": "U", "Link": "http://y"}, {}]
    )
    assert refs == [
        {"title": "T", "link": "http://x"},
        {"title": "U", "link": "http://y"},
        {"title": "", "link": ""},
    ]


def test_extract_context_from_full_context_json():
    import json as _json

    ctx = _json.dumps([{"document_title": "D", "document_link": "http://d"}, {"title": "E", "link": "http://e"}])
    out = extract_title_and_link_from_context(ctx)
    assert out == [{"title": "D", "link": "http://d"}, {"title": "E", "link": "http://e"}]
    assert extract_title_and_link_from_context("") == []
    assert extract_title_and_link_from_context("not-json") == []


def test_resolve_tenant_for_scenario():
    m = {"default": "tenant-default", "TypeSpec Discussion": "tenant-ts"}
    assert resolve_tenant_for_scenario("typespec", m) == "tenant-ts"
    assert resolve_tenant_for_scenario("unknown", m) == "tenant-default"
    assert resolve_tenant_for_scenario("typespec", None) is None


def test_output_items_to_rows_completion_item_response():
    # In completion mode the answer/references live in the datasource_item.
    output_items = [
        {
            "datasource_item": {
                "testcase": "t", "ground_truth": "gt", "response": "collected answer",
                "references": [{"title": "R", "link": "http://r"}],
                "knowledges": [{"title": "K", "link": "http://k"}],
            },
            "results": [{"name": "similarity", "score": 5.0, "passed": True}],
            "sample": None,
        }
    ]
    rows = output_items_to_rows(output_items, ["similarity"])["rows"]
    assert rows[0]["inputs.response"] == "collected answer"
    assert rows[0]["inputs.references"] == [{"title": "R", "link": "http://r"}]
    assert rows[0]["inputs.knowledges"] == [{"title": "K", "link": "http://k"}]


def test_failed_row_counts_as_failure_in_gate():
    # A dropped /completion case becomes a synthetic failing row so the gate sees it.
    from _evals_runner import FoundryEvalsRunner
    from _evals_result import EvalsResult

    evaluators = ["bot_evals", "groundedness"]
    metrics = {e: None for e in evaluators}
    er = EvalsResult(metrics=metrics, suppressions=None)
    runner = FoundryEvalsRunner(evaluators, er, model="m")
    row = runner._failed_row({"testcase": "lost", "ground_truth": "gt"})
    assert row["outputs.bot_evals.bot_evals_result"] == "fail"
    assert row["outputs.groundedness.groundedness_result"] == "fail"
    # record_run_result must count it as a failure (no KeyError, total_evals=1).
    recorded = er.record_run_result({"rows": [row]})
    summary = recorded[-1]
    assert summary["total_evals"] == 1
    assert summary["bot_evals_fail_rate"] == 1
    assert summary["groundedness_fail_rate"] == 1


def test_record_run_result_empty_rows_no_crash():
    from _evals_result import EvalsResult

    er = EvalsResult(metrics={"bot_evals": None}, suppressions=None)
    recorded = er.record_run_result({"rows": []})
    assert recorded[-1]["total_evals"] == 0


def _run_all() -> int:
    funcs = [v for k, v in sorted(globals().items()) if k.startswith("test_") and callable(v)]
    failed = 0
    for fn in funcs:
        try:
            fn()
            print(f"PASS {fn.__name__}")
        except Exception as exc:  # noqa: BLE001
            failed += 1
            print(f"FAIL {fn.__name__}: {exc}")
    print(f"\n{len(funcs) - failed}/{len(funcs)} passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(_run_all())
