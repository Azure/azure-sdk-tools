from pathlib import Path

import yaml


def _pipeline():
    path = Path(__file__).resolve().parents[2] / "perf-evaluation.yml"
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def test_perf_pipeline_bounds_parallel_scenarios_and_collection():
    pipeline = _pipeline()
    parameters = {item["name"]: item for item in pipeline["parameters"]}
    assert parameters["maxParallelScenarios"]["default"] == 2
    assert parameters["maxConcurrency"]["default"] == 1
    for name in ("maxParallelScenarios", "maxConcurrency"):
        assert all(value > 0 for value in parameters[name]["values"])

    jobs = pipeline["extends"]["parameters"]["stages"][0]["jobs"]
    assert len(jobs) == 1
    job = jobs[0]
    assert job["timeoutInMinutes"] == 120
    assert job["strategy"]["maxParallel"] == "${{ parameters.maxParallelScenarios }}"
    matrix = job["strategy"]["matrix"]
    assert matrix["${{ each scenario in parameters.scenarios }}"] == {
        "${{ scenario }}": {"Scenario": "${{ scenario }}"},
    }
    task = next(step for step in job["steps"] if step.get("displayName") == "Run Perf Evals ($(Scenario))")
    script = task["inputs"]["inlineScript"]
    assert '--dataset "qa-bot-perf-$(Scenario):latest"' in script
    assert "--max_concurrency ${{ parameters.maxConcurrency }}" in script
    assert "--cache_result full" in script
    outputs = job["templateContext"]["outputs"]
    assert outputs[0]["artifact"] == "perf-evaluation-results-$(Scenario)"
    assert outputs[0]["condition"] == "always()"


def test_perf_pipeline_can_target_a_deployed_agent_branch():
    pipeline = _pipeline()
    parameters = {item["name"]: item for item in pipeline["parameters"]}
    assert parameters["agentBranch"]["default"] == ""
    job = pipeline["extends"]["parameters"]["stages"][0]["jobs"][0]
    assert job["variables"] == {
        "${{ if ne(parameters.agentBranch, '') }}": {
            "EvalAgentBranch": "${{ parameters.agentBranch }}",
        },
        "${{ else }}": {"EvalAgentBranch": "$(Build.SourceBranch)"},
    }
    task = next(step for step in job["steps"] if step.get("displayName") == "Run Perf Evals ($(Scenario))")
    assert task["env"]["EVAL_AGENT_BRANCH"] == "$(EvalAgentBranch)"
