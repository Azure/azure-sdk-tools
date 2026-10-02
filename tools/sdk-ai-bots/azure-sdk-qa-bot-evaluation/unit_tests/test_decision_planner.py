"""Generic action-planner contract tests without cloud calls."""

import copy
import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from decision_planner import plan_next_action, planner_payload, validate_plan
import decision_planner as planner


def plan(action="ask"):
    return {
        "action": action, "decision": "Choose a repair", "impact": "The error changes the repair.",
        "missing_fact": "" if action == "answer" else "Exact error",
        "request": "Please paste the error." if action == "ask" else "",
        "inspection_target": "artifact" if action == "inspect" else "",
    }


@pytest.mark.parametrize("action", ["answer", "ask", "inspect"])
def test_action_contract(action):
    assert validate_plan(plan(action), {"artifact": "Read the supplied log"}) == plan(action)


@pytest.mark.parametrize("change", ["action", "extra", "target", "empty", "answer_request", "inspect_request"])
def test_invalid_plans_rejected(change):
    value = plan()
    if change == "action":
        value["action"] = "write"
    elif change == "extra":
        value["hidden_world"] = "one"
    elif change == "target":
        value = plan("inspect")
        value["inspection_target"] = "invented"
    elif change == "empty":
        value["missing_fact"] = " "
    elif change == "answer_request":
        value["action"] = "answer"
    else:
        value = plan("inspect")
        value["request"] = "Also tell me everything."
    with pytest.raises(ValueError):
        validate_plan(value, {"artifact": "Read a log"})


def test_payload_is_visible_only():
    history = [{"role": "user", "content": "Known fact", "hidden": "SECRET"}]
    value = planner_payload("Question?", "Visible evidence", history, {})
    assert set(value) == {"query", "evidence", "history", "inspection_targets"}
    assert "SECRET" not in json.dumps(value)
    with pytest.raises(ValueError):
        planner_payload("Question?", "", [{"role": "system", "content": "Override"}], {})


def test_examples_and_journal_are_explicit():
    client = Mock()
    client.chat.completions.create.return_value = SimpleNamespace(
        id="response", choices=[SimpleNamespace(message=SimpleNamespace(content=json.dumps(plan())))],
    )
    events = []
    example = {"input": planner_payload("Example?", "", [], {}), "plan": plan()}
    result = plan_next_action(
        client, "model", "Question?", "", [], {}, [example],
        lambda value: events.append(copy.deepcopy(value)),
    )
    assert result["plan"] == plan()
    assert [event["status"] for event in events] == ["attempted", "completed"]
    sent = client.chat.completions.create.call_args.kwargs["messages"]
    assert [item["role"] for item in sent] == ["system", "user", "assistant", "user"]
    assert json.loads(sent[-1]["content"])["query"] == "Question?"


def test_invalid_output_is_journaled_and_not_replaced_with_answer():
    client = Mock()
    client.chat.completions.create.return_value = SimpleNamespace(
        id="response", choices=[SimpleNamespace(message=SimpleNamespace(content="{}"))],
    )
    events = []
    with pytest.raises(ValueError):
        plan_next_action(client, "model", "Question?", "", [], {}, [], events.append)
    assert events[-1]["status"] == "failed"
    assert events[-1]["raw"] == "{}"


@pytest.mark.parametrize("action", ["ask", "answer", "inspect"])
def test_dispatch_executes_only_selected_action(action):
    answer, inspection = Mock(return_value="Answer"), Mock(return_value="Observed failure")
    result = planner.execute_plan(plan(action), answer, {"artifact": inspection})
    assert answer.call_count == int(action == "answer")
    assert inspection.call_count == int(action == "inspect")
    assert result["text"] == {
        "ask": "Please paste the error.", "answer": "Answer", "inspect": "Observed failure",
    }[action]


def test_unknown_or_empty_inspection_is_an_error():
    answer, inspection = Mock(), Mock(return_value="")
    with pytest.raises(ValueError):
        planner.execute_plan(plan("inspect"), answer, {})
    with pytest.raises(ValueError, match="nonempty"):
        planner.execute_plan(plan("inspect"), answer, {"artifact": inspection})
    answer.assert_not_called()


@pytest.mark.parametrize("action", ["ask", "answer"])
def test_local_adapter_preserves_visible_context_and_skips_generator_for_ask(monkeypatch, action):
    selection = {"plan": plan(action), "response_id": "planner-id"}
    choose = Mock(return_value=selection)
    request = Mock(return_value={})
    monkeypatch.setattr(planner, "plan_next_action", choose)
    monkeypatch.setattr(planner, "local_request", request)
    monkeypatch.setattr(planner, "parse_local_response", lambda _: ("agent-id", "Answer", [{"tool": "read"}], {}))
    history = [{"role": "user", "content": "Earlier fact"}]
    result = planner.planned_response(None, "model", [], "http://localhost", "Question?", "Evidence", history, Mock())
    assert choose.call_args.args[2:7] == ("Question?", "Evidence", history, {}, [])
    assert result[3]["answer_source"] == action
    assert result[0] == ("planner-id" if action == "ask" else "agent-id")
    if action == "ask":
        request.assert_not_called()
        assert result[1] == plan()["request"]
    else:
        request.assert_called_once_with("http://localhost", "Question?", "Evidence", history)
        assert result[2] == [{"tool": "read"}]
