import json
import logging
import math
import pathlib
import re
from typing import Any, Optional
from tabulate import tabulate

weights: dict[str, float] = {
    "similarity_weight": 0.6,  # Similarity between expected and actual
    "groundedness_weight": 0.4,  # Staying grounded in guidelines
    "response_completeness_weight": 0.4
}

def calculate_overall_score(row: dict[str, Any], metrics: list[str]) -> float:
    """Calculate weighted score based on various metrics."""
    # calculate the overall score when there are multiple metrics.
    overall_score = 0
    for metric in metrics:
        metric_key = f"outputs.{metric}.{metric}"
        if (metric_key not in row):
            return 0.0
        else:
            score = float(row[metric_key])
            if math.isnan(score):
                return 0.0
            overall_score += score * weights[f"{metric}_weight"]
    
    return overall_score

    # if ("outputs.similarity.similarity" not in row) or ( "outputs.groundedness.groundedness" not in row):
    #     return 0.0
    
    # similarity = float(row["outputs.similarity.similarity"])
    # groundedness = float(row["outputs.groundedness.groundedness"])
    # if math.isnan(similarity) or math.isnan(groundedness):
    #     return 0.0
    # else:
    #     return similarity * weights["similarity_weight"] + groundedness * weights["groundedness_weight"]

def record_run_result(result: dict[str, Any], metrics: list[str]) -> list[dict[str, Any]]:
    run_result = []
    total_score = 0

    pass_rates: dict[str, int] = {}
    for metric in metrics:
        pass_rates[metric] = 0
    
    for row in result["rows"]:
        score = calculate_overall_score(row, metrics)
        total_score += score

        row_result = {}
        row_result["testcase"] = row["inputs.testcase"]
        row_result["expected"] = row["inputs.ground_truth"]
        row_result["actual"] = row["inputs.response"]
        pattern = r"^outputs\.(\w+)\.(\w+)$"
        for index, (key, value) in enumerate(row.items()):
            match = re.match(pattern, key)
            if match:
                metric = match.group(1)
                metric_name = match.group(2)
                # logging.info(f"Metric: {metric}, Name: {metric_name}")
                if key == f"outputs.{metric}.{metric}_result" and value == "pass":
                    pass_rates[metric] += 1
                if key == f"outputs.{metric}.{metric}":
                    row_result[metric_name] = float(value)
                else:
                    row_result[metric_name] = value
        row_result["overall_score"] = score
        run_result.append(row_result)

    # similarity_pass_rate = 0
    # groundedness_pass_rate = 0
    # for row in result["rows"]:
    #     score = calculate_overall_score(row)
    #     total_score += score

    #     if "outputs.similarity.similarity_result" in row and row["outputs.similarity.similarity_result"] == "pass":
    #         similarity_pass_rate += 1

    #     if "outputs.groundedness.groundedness_result" in row and row["outputs.groundedness.groundedness_result"] == "pass":
    #         groundedness_pass_rate += 1

    #     run_result.append(
    #         {
    #             "testcase": row["inputs.testcase"],
    #             "expected": row["inputs.ground_truth"],
    #             "actual": row["inputs.response"],
    #             "similarity": float(row["outputs.similarity.similarity"]) if "outputs.similarity.similarity" in row else -1,
    #             "gpt_similarity": float(row["outputs.similarity.gpt_similarity"]) if "outputs.similarity.gpt_similarity" in row else -1,
    #             "similarity_threshold": float(row["outputs.similarity.similarity_threshold"]) if "outputs.similarity.similarity_threshold" in row else 3,
    #             "similarity_result": row["outputs.similarity.similarity_result"] if "outputs.similarity.similarity_result" in row else "N/A",
    #             "groundedness": float(row["outputs.groundedness.groundedness"]) if "outputs.groundedness.groundedness" in row else -1,
    #             "gpt_groundedness": float(row["outputs.groundedness.gpt_groundedness"]) if "outputs.groundedness.gpt_groundedness" in row else -1,
    #             "groundedness_threshold": float(row["outputs.groundedness.groundedness_threshold"]) if "outputs.groundedness.groundedness_threshold" in row else 3,
    #             "groundedness_result": row["outputs.groundedness.groundedness_result"] if "outputs.groundedness.groundedness_result" in row else "N/A",
    #             "overall_score": score,
    #         }
    #     )

    if result:
        average_score = total_score / len(result["rows"])
    else:
        average_score = 0
    # run_result.append({"average_score": average_score, "total_evals": len(result["rows"]), "similarity_pass_rate": similarity_pass_rate, "groundedness_pass_rate": groundedness_pass_rate})
    summary_result = {"average_score": average_score, "total_evals": len(result["rows"])}
    for index, (key, value) in enumerate(pass_rates.items()):
        summary_result[f"{key}_pass_rate"] = value

    run_result.append(summary_result)
    return run_result

def format_terminal_diff(new: float, old: float, format_str: str = ".1f", reverse: bool = False) -> str:
    """Format difference with ANSI colors for terminal output."""

    diff = new - old
    if diff > 0:
        if reverse:
            return f" (\033[31m+{diff:{format_str}}\033[0m)"  # Red
        return f" (\033[32m+{diff:{format_str}}\033[0m)"  # Green
    elif diff < 0:
        if reverse:
            return f" (\033[32m{diff:{format_str}}\033[0m)"  # Green
        return f" (\033[31m{diff:{format_str}}\033[0m)"  # Red
    return f" ({diff:{format_str}})"

def build_output_table(eval_results: list[dict[str, Any]], metrics: list[str], baseline_results: Optional[dict[str, Any]] = None) -> str:
    headers = [
        "Test Case",
    ]
    for metric in metrics:
        headers.append(metric)
        headers.append(f"{metric} Result")
    
    headers.append("Score")
    # headers = [
    #     "Test Case",
    #     "Similarity",
    #     "Similarity Result",
    #     "Groundedness",
    #     "Groundedness Result",
    #     "Score"
    # ]
    terminal_rows = []

    for result in eval_results[: -1]: # Skip summary object
        # logging.info(json.dumps(result, ensure_ascii=False))
        testcase = result["testcase"]
        score = result["overall_score"]

        terminal_row = [testcase]
        values = []
        if baseline_results is not None and testcase in baseline_results:
            base = baseline_results[testcase]
            for metric in metrics:
                metric_score = result[f"{metric}"] if f"{metric}" in result else -1
                metric_result = result[f"{metric}_result"] if f"{metric}_result" in result else "N/A"
                base_score = base[f"{metric}"]
                values.append(f"{metric_score:.1f}{format_terminal_diff(metric_score, base_score)}")
                values.append(f"{metric_result}")
            values.append(f"{score:.1f}{format_terminal_diff(score, base['overall_score'])}")
        else:
            for metric in metrics:
                metric_score = result[f"{metric}"] if f"{metric}" in result else -1
                metric_result = result[f"{metric}_result"] if f"{metric}_result" in result else "N/A"
                values.append(f"{metric_score:.1f}")
                values.append(f"{metric_result}")
            values.append(f"{score:.1f}")
        
        terminal_row.extend(values)
        terminal_rows.append(terminal_row)

    # for result in eval_results[:-1]:  # Skip summary object
    #     testcase = result["testcase"]
    #     score = result["overall_score"]
    #     sim = result["similarity"]
    #     sim_result = result["similarity_result"]

    #     groundedness = result['groundedness']
    #     groundedness_result = result['groundedness_result']
        
    #     terminal_row = [testcase]
    #     if baseline_results is not None and testcase in baseline_results:
    #         base = baseline_results[testcase]
    #         values =[
    #             f"{sim:.1f}{format_terminal_diff(sim, base['similarity'])}",
    #             f"{sim_result}",
    #             f"{groundedness}{format_terminal_diff(groundedness, base['groundedness'])}",
    #             f"{groundedness_result}",
    #             f"{score:.1f}{format_terminal_diff(score, base['overall_score'])}",
    #         ]
    #     else:
    #         values = [
    #             f"{sim:.1f}",
    #             f"{sim_result}",
    #             f"{groundedness}",
    #             f"{groundedness_result}",
    #             f"{score:.1f}"
    #         ]
    #     terminal_row.extend(values)
    #     terminal_rows.append(terminal_row)

    return tabulate(terminal_rows, headers, tablefmt="simple")

def output_table(eval_results: list[dict[str, Any]], file_name: str, metrics: list[str], baseline_results: Optional[dict[str, Any]] = None) -> None:
    # logging.info(json.dumps(eval_results[-1], ensure_ascii=False))
    # similarity_pass_rate = eval_results[-1]['similarity_pass_rate']
    # groundedness_pass_rate = eval_results[-1]['groundedness_pass_rate']

    logging.info("====================================================")
    logging.info(f"\n\n✨ {file_name} results:\n")
    print(build_output_table(eval_results, metrics, baseline_results))

    if baseline_results:
        print(f"\n{file_name} average score: {eval_results[-1]['average_score']} {format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}")
        for metric in metrics:
            pass_rate = eval_results[-1][f"{metric}_pass_rate"] if f"{metric}_pass_rate" in eval_results[-1] else 0
            print(f" {metric}: pass({pass_rate}) fail({len(eval_results)-1 - pass_rate})",)
    # if baseline_results:
    #     print(
    #         f"\n{file_name} average score: {eval_results[-1]['average_score']} {format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}",
    #         f" similarity: pass({similarity_pass_rate}) fail({len(eval_results)-1 - similarity_pass_rate})",
    #         f" groundedness: pass({groundedness_pass_rate}) fail({len(eval_results)-1 - groundedness_pass_rate})"
    #         "\n\n"
    #     )

def show_results(all_results: dict[str, Any], metrics: list[str], with_baseline: bool = True) -> None:
    """Display results in a table format."""
    for name, test_results in all_results.items():
        baseline_results = None
        if with_baseline:
            baseline_results = {}
            baselineName = f"{name.split('_')[0]}-test.json"
            baseline_path = pathlib.Path(__file__).parent / "results" / baselineName

            if baseline_path.exists():
                with open(baseline_path, "r") as f:
                    baseline_data = json.load(f)
                    for result in baseline_data[:-1]:  # Skip summary
                        baseline_results[result["testcase"]] = result
                    baseline_results["average_score"] = baseline_data[-1]["average_score"]

        output_table(test_results, name, metrics, baseline_results)

def verify_results(all_results: dict[str, Any], metrics: list[str], with_baseline: bool = True) -> bool:
    ret = True
    failed_scenarios = []
    for name, test_results in all_results.items():
        scenario_ret = True
        
        if with_baseline:
            baseline_results = {}
            baselineName = f"{name.split('_')[0]}-test.json"
            baseline_path = pathlib.Path(__file__).parent / "results" / baselineName

            if baseline_path.exists():
                with open(baseline_path, "r") as f:
                    baseline_data = json.load(f)
                    for result in baseline_data[:-1]:  # Skip summary
                        baseline_results[result["testcase"]] = result
                    baseline_results["average_score"] = baseline_data[-1]["average_score"]
                    if test_results[-1]["average_score"] < baseline_data[-1]["average_score"]:
                        # scenario_ret = False //ignore decrease in average score
                        logging.warning(f"scenario {name} avarage score decrease!")
        
        for metric in metrics:
            pass_rate = test_results[-1][f"{metric}_pass_rate"] if f"{metric}_pass_rate" in test_results[-1] else 0
            if pass_rate < test_results[-1]["total_evals"]:
                scenario_ret = False
        # if test_results[-1]["similarity_pass_rate"] < test_results[-1]["total_evals"] or test_results[-1]["groundedness_pass_rate"] < test_results[-1]["total_evals"]:
        #     scenario_ret = False

        if not scenario_ret:
            failed_scenarios.append(name)
            ret = False
    if failed_scenarios:
        logging.info(f"Failed Scenarios: {' '.join(failed_scenarios)}")
    return ret 

def establish_baseline(all_results: dict[str, Any], is_ci: bool) -> None:
    """Establish the current results as the new baseline."""

    # only ask if we're not in CI
    if is_ci is False:
        establish_baseline = input("\nDo you want to establish this as the new baseline? (y/n): ")
        if establish_baseline.lower() == "y":
            for name, result in all_results.items():
                baselineName = f"{name.split('_')[0]}-test.json"
                baseline_path = pathlib.Path(__file__).parent / "results" / baselineName
                with open(str(baseline_path), "w") as f:
                    json.dump(result, indent=4, fp=f)

    # whether or not we establish a baseline, we want to write results to a temp dir
    log_path = pathlib.Path(__file__).parent / "results" / ".log"
    if not log_path.exists():
        log_path.mkdir(parents=True, exist_ok=True)

    for name, result in all_results.items():
        baselineName = f"{name.split('_')[0]}-test.json"
        output_path = log_path / baselineName
        with open(str(output_path), "w") as f:
            json.dump(result, indent=4, fp=f)

__all__ = [
    build_output_table,
    show_results,
    verify_results,
    establish_baseline,
    record_run_result
]