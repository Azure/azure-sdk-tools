from enum import IntFlag
import json
import logging
import math
import pathlib
import re
from typing import Any, Optional
from tabulate import tabulate


class EvalReturnCode(IntFlag):
    SUCCESS = 0
    FAIL = 1
    WARNING = 2 << 1

class EvalsResult:
    def __init__(self, weights: dict[str, float] | None, metrics: dict[str, list[str] | None], suppressions: dict[str, list[str]] | None):
        """
        Initialize an EvalsResult instance for managing evaluation results.

        Args:
            weights: A dictionary mapping metric names to their weights for calculating
                     overall scores. Keys should be in the format "{metric}_weight".
                     If None, an empty dictionary is used and metrics are weighted equally.
            metrics: A dictionary mapping evaluator names to their output field names.
                     Each key is an evaluator name, and the value is a list of field names
                     that the evaluator outputs, or None if only the default field is used.
            suppressions: A dictionary specifying which evaluators and test cases should be
                          suppressed (excluded from failure counting). Expected keys are:
                          - "evaluators": List of evaluator names to suppress.
                          - "testcases": List of test case names to suppress.
                          If None, defaults to empty lists for both.
        """
        self._weights = weights or {}
        self._metrics = metrics
        # Ensure suppressions dict always has lists, never None
        self._suppressions: dict[str, list[str]]
        if suppressions is None:
            self._suppressions = {"evaluators": [], "testcases": []}
        else:
            self._suppressions = {
                "evaluators": suppressions.get("evaluators") or [],
                "testcases": suppressions.get("testcases") or []
            }

    def calculate_overall_score(self, row: dict[str, Any]) -> float:
        """Calculate weighted score based on various metrics."""
        # calculate the overall score when there are multiple metrics.
        metrics = self._metrics.keys()
        overall_score = 0.0
        for metric in metrics:
            metric_key = f"outputs.{metric}.{metric}"
            if metric_key not in row:
                return 0.0
            else:
                score = float(row[metric_key])
                if math.isnan(score):
                    return 0.0
                if f"{metric}_weight" in self._weights:
                    overall_score += score * self._weights[f"{metric}_weight"]
                else:
                    overall_score += score

        return overall_score

    def record_run_result(self, result: dict[str, Any]) -> list[dict[str, Any]]:
        run_result: list[dict[str, Any]] = []
        total_score = 0.0

        metrics = self._metrics.keys()

        pass_rates: dict[str, int] = {}
        fail_rates: dict[str, int] = {}
        for metric in metrics:
            pass_rates[metric] = 0
            fail_rates[metric] = 0

        for row in result["rows"]:
            score = self.calculate_overall_score(row)
            total_score += score

            row_result: dict[str, Any] = {}
            row_result["testcase"] = row["inputs.testcase"]
            row_result["expected"] = {
                "answer": row["inputs.ground_truth"],
                "references": row["inputs.expected_references"],
                "knowledges": row["inputs.expected_knowledges"],
            }
            row_result["actual"] = {
                "answer": row["inputs.response"],
                "references": row["inputs.references"],
                "knowledges": row["inputs.knowledges"],
            }
            pattern = r"^outputs\.(\w+)\.(\w+)$"
            for index, (key, value) in enumerate(row.items()):
                match = re.match(pattern, key)
                if match:
                    metric = match.group(1)
                    metric_name = match.group(2)
                    logging.debug(f"Metric: {metric}, Name: {metric_name}")
                    if key == f"outputs.{metric}.{metric}_result":
                        if value == "fail":
                            if row["inputs.testcase"] not in self._suppressions["testcases"]:
                                fail_rates[metric] += 1
                            else:
                                pass_rates[metric] += 1 #record to pass rate if the testcase is suppressed.
                        if value == "pass":
                            pass_rates[metric] += 1

                    if key == f"outputs.{metric}.{metric}":
                        row_result[metric_name] = float(value)
                    else:
                        row_result[metric_name] = value
            row_result["overall_score"] = score
            run_result.append(row_result)

        if result:
            average_score = total_score / len(result["rows"])
        else:
            average_score = 0

        summary_result: dict[str, Any] = {"average_score": average_score, "total_evals": len(result["rows"])}
        for index, (key, value) in enumerate(pass_rates.items()):
            summary_result[f"{key}_pass_rate"] = value
        for index, (key, value) in enumerate(fail_rates.items()):
            summary_result[f"{key}_fail_rate"] = value

        run_result.append(summary_result)
        return run_result

    @classmethod
    def format_terminal_diff(cls, new: float, old: float, format_str: str = ".2f", reverse: bool = False) -> str:
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

    def build_output_table(
        self, eval_results: list[dict[str, Any]], baseline_results: Optional[dict[str, Any]] = None
    ) -> str:
        metrics = self._metrics.keys()
        headers = [
            "Test Case",
        ]
        for metric in metrics:
            headers.append(metric)
            fields = self._metrics[metric]
            if fields:
                for field in fields:
                    if field != metric:
                        headers.append(field)
            else:
                headers.append(f"{metric} Result")

        headers.append("Score")

        terminal_rows = []

        for result in eval_results[:-1]:  # Skip summary object
            logging.debug(json.dumps(result, ensure_ascii=False))
            testcase = result["testcase"]
            score = float(result["overall_score"])

            terminal_row = [testcase]
            values = []
            if baseline_results is not None and testcase in baseline_results:
                base = baseline_results[testcase]
                for metric in metrics:
                    metric_score = float(result[f"{metric}"]) if f"{metric}" in result else -1
                    base_score = base[f"{metric}"] if f"{metric}" in base else None
                    if base_score is not None:
                        values.append(
                            f"{metric_score:.2f}{EvalsResult.format_terminal_diff(metric_score, float(base_score))}"
                        )
                    else:
                        values.append(f"{metric_score:.2f}")
                    fields = self._metrics[metric]
                    if fields:
                        for field in fields:
                            if field != metric:
                                metric_value = result[f"{field}"] if f"{field}" in result else "N/A"
                                values.append(f"{metric_value}")
                    else:
                        metric_result = result[f"{metric}_result"] if f"{metric}_result" in result else "N/A"
                        values.append(f"{metric_result}")
                values.append(f"{score:.2f}{EvalsResult.format_terminal_diff(score, float(base['overall_score']))}")
            else:
                for metric in metrics:
                    metric_score = result[f"{metric}"] if f"{metric}" in result else -1
                    values.append(f"{metric_score:.2f}")
                    fields = self._metrics[metric]
                    if fields:
                        for field in fields:
                            if field != metric:
                                metric_value = result[f"{field}"] if f"{field}" in result else "N/A"
                                values.append(f"{metric_value}")
                    else:
                        metric_result = result[f"{metric}_result"] if f"{metric}_result" in result else "N/A"
                        values.append(f"{metric_result}")
                values.append(f"{score:.2f}")

            terminal_row.extend(values)
            terminal_rows.append(terminal_row)

        return tabulate(terminal_rows, headers, tablefmt="simple")

    def output_table(
        self, eval_results: list[dict[str, Any]], file_name: str, baseline_results: Optional[dict[str, Any]] = None
    ) -> None:
        logging.debug(json.dumps(eval_results[-1], ensure_ascii=False))
        logging.info("====================================================")
        logging.info(f"\n\n✨ {file_name} results:\n")
        metrics = self._metrics.keys()
        print(self.build_output_table(eval_results, baseline_results), flush=True)

        if baseline_results:
            print(
                f"\n{file_name} average score: {eval_results[-1]['average_score']} {EvalsResult.format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}",
                flush=True
            )
            for metric in metrics:
                pass_rate = eval_results[-1][f"{metric}_pass_rate"] if f"{metric}_pass_rate" in eval_results[-1] else 0
                fail_rate = eval_results[-1][f"{metric}_fail_rate"] if f"{metric}_fail_rate" in eval_results[-1] else 0
                print(
                    f" {metric}: pass({pass_rate}) fail({fail_rate})",
                    flush=True
                )

    def show_results(self, all_results: dict[str, Any], with_baseline: bool = True) -> None:
        """Display results in a table format."""
        for name, test_results in all_results.items():
            baseline_results = None
            if with_baseline:
                baseline_results = {}
                baseline_name = f"{name.split('_')[0]}-test.json"
                baseline_path = pathlib.Path(__file__).parent / "results" / baseline_name

                if baseline_path.exists():
                    with open(baseline_path, "r") as f:
                        baseline_data = json.load(f)
                        for result in baseline_data[:-1]:  # Skip summary
                            baseline_results[result["testcase"]] = result
                        baseline_results["average_score"] = baseline_data[-1]["average_score"]

            self.output_table(test_results, name, baseline_results)

    def verify_results(self, all_results: dict[str, Any], with_baseline: bool = True) -> int:
        """
        Verify evaluation results against baseline and suppression rules.

        Args:
            all_results: A dictionary mapping scenario names to their evaluation results (list of dicts).
            with_baseline: Whether to compare results with baseline data (default: True).

        Returns:
            int: 0 if all scenarios pass, 1 if any scenario fails, 2 if all pass but some have warnings (suppressed failures).
        """
        ret = 0
        failed_scenarios = []
        warning_scenarios = []
        metrics = self._metrics.keys()
        for name, test_results in all_results.items():
            scenario_ret = EvalReturnCode.SUCCESS

            if with_baseline:
                baseline_results = {}
                baseline_name = f"{name.split('_')[0]}-test.json"
                baseline_path = pathlib.Path(__file__).parent / "results" / baseline_name

                if baseline_path.exists():
                    with open(baseline_path, "r") as f:
                        baseline_data = json.load(f)
                        for result in baseline_data[:-1]:  # Skip summary
                            baseline_results[result["testcase"]] = result
                        baseline_results["average_score"] = baseline_data[-1]["average_score"]
                        if test_results[-1]["average_score"] < baseline_data[-1]["average_score"]:
                            logging.warning(f"scenario {name} avarage score decrease!")
                            warning = True

            for metric in metrics:
                pass_rate = test_results[-1][f"{metric}_pass_rate"] if f"{metric}_pass_rate" in test_results[-1] else 0
                fail_rate = test_results[-1][f"{metric}_fail_rate"] if f"{metric}_fail_rate" in test_results[-1] else 0
                # workaround: for groundedness, only caculate the `fail`
                metric_ret = EvalReturnCode.SUCCESS
                if metric == "groundedness":
                    if fail_rate > 0:
                        metric_ret = EvalReturnCode.FAIL
                else:
                    if pass_rate < test_results[-1]["total_evals"]:
                        metric_ret = EvalReturnCode.FAIL
                if metric in self._suppressions["evaluators"] and metric_ret == EvalReturnCode.FAIL:
                    metric_ret = EvalReturnCode.WARNING #fallback to warning if evaluator is suppressed
                scenario_ret = scenario_ret | metric_ret

            if scenario_ret & EvalReturnCode.FAIL:
                failed_scenarios.append(name)
                ret = 1 # failed
            elif scenario_ret & EvalReturnCode.WARNING:
                warning_scenarios.append(name)
                ret = 2 # succeed with warning

        if failed_scenarios:
            logging.info(f"Failed Scenarios: {' '.join(failed_scenarios)}")
        elif warning_scenarios:
            logging.info(f"Scenarios with warnings: {' '.join(warning_scenarios)}")
        else:
            logging.info(f"All scenarios passed without issues.")

        return ret

    def establish_baseline(self, all_results: dict[str, Any], is_ci: bool) -> None:
        """Establish the current results as the new baseline."""

        # only ask if we're not in CI
        if is_ci is False:
            establish_baseline = input("\nDo you want to establish this as the new baseline? (y/n): ")
            if establish_baseline.lower() == "y":
                for name, result in all_results.items():
                    baseline_name = f"{name.split('_')[0]}-test.json"
                    baseline_path = pathlib.Path(__file__).parent / "results" / baseline_name
                    with open(str(baseline_path), "w") as f:
                        json.dump(result, indent=4, fp=f)

        # whether or not we establish a baseline, we want to write results to a temp dir
        log_path = pathlib.Path(__file__).parent / "results" / ".log"
        if not log_path.exists():
            log_path.mkdir(parents=True, exist_ok=True)

        for name, result in all_results.items():
            baseline_name = f"{name.split('_')[0]}-test.json"
            output_path = log_path / baseline_name
            with open(str(output_path), "w") as f:
                json.dump(result, indent=4, fp=f)


__all__ = ["EvalsResult"]
