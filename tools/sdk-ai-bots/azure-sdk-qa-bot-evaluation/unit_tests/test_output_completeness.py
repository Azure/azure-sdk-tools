from types import SimpleNamespace
from unittest.mock import Mock

import pytest

import _evals_runner
from _evals_runner import FoundryEvalsRunner


def make_runner(monkeypatch):
    recorder = Mock()
    recorder.record_run_result.side_effect = lambda raw: raw["rows"]
    runner = FoundryEvalsRunner(
        ["groundedness"], recorder, model="model", poll_interval=0, poll_timeout=10,
    )
    monkeypatch.setattr(_evals_runner, "output_items_to_rows", lambda items, *args, **kwargs: {"rows": items})
    return runner, recorder


def test_completed_run_waits_for_all_submitted_outputs(monkeypatch):
    runner, recorder = make_runner(monkeypatch)
    client = Mock()
    client.evals.runs.retrieve.return_value = SimpleNamespace(status="completed", report_url=None)
    client.evals.runs.output_items.list.side_effect = [
        [{"id": "one"}], [{"id": "one"}, {"id": "two"}],
    ]
    result = runner._poll_and_adapt(client, "eval", "run", "apispec", {}, expected_count=2)
    assert result == {"apispec_run": [{"id": "one"}, {"id": "two"}]}
    assert client.evals.runs.output_items.list.call_count == 2
    recorder.record_run_result.assert_called_once()


def test_incomplete_outputs_timeout_without_recording_partial_results(monkeypatch):
    runner, recorder = make_runner(monkeypatch)
    client = Mock()
    client.evals.runs.retrieve.return_value = SimpleNamespace(status="completed", report_url=None)
    client.evals.runs.output_items.list.return_value = [{"id": "one"}]
    clock = iter([0, 11])
    monkeypatch.setattr(_evals_runner.time, "time", lambda: next(clock))
    with pytest.raises(TimeoutError, match="only 1/2 outputs"):
        runner._poll_and_adapt(client, "eval", "run", "apispec", {}, expected_count=2)
    recorder.record_run_result.assert_not_called()


def test_provider_total_used_for_fixed_answer_callers(monkeypatch):
    runner, _ = make_runner(monkeypatch)
    client = Mock()
    client.evals.runs.retrieve.return_value = SimpleNamespace(
        status="completed", report_url=None, result_counts=SimpleNamespace(total=2),
    )
    client.evals.runs.output_items.list.side_effect = [
        [], [{"id": "one"}, {"id": "two"}],
    ]
    result = runner._poll_and_adapt(client, "eval", "run", "apispec", {})
    assert len(result["apispec_run"]) == 2


def test_excess_outputs_cannot_be_accepted(monkeypatch):
    runner, recorder = make_runner(monkeypatch)
    client = Mock()
    client.evals.runs.retrieve.return_value = SimpleNamespace(status="completed", report_url=None)
    client.evals.runs.output_items.list.return_value = [{"id": "one"}, {"id": "two"}]
    with pytest.raises(RuntimeError, match="2 outputs for 1 inputs"):
        runner._poll_and_adapt(client, "eval", "run", "apispec", {}, expected_count=1)
    recorder.record_run_result.assert_not_called()
