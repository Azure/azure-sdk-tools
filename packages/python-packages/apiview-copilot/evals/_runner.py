import json
import os
import pathlib
import sys
from typing import Any, Set

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from evals._custom import CustomAPIViewEvaluator
from src._apiview_reviewer import ApiViewReview
from src._settings import SettingsManager

# set before azure.ai.evaluation import to make PF output less noisy
os.environ["PF_LOGGING_LEVEL"] = "CRITICAL"

from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential
from tabulate import tabulate

DEFAULT_NUM_RUNS: int = 1


def _review_apiview(query: str, language: str):
    """Internal global callable for evals framework."""
    reviewer = ApiViewReview(target=query, language=language, base=None)
    review = reviewer.run()
    reviewer.close()
    return {"actual": review.model_dump_json()}


class EvalRunner:
    """Class to run evals for APIView copilot."""

    def __init__(self, *, language: str, test_path: str, num_runs: int = DEFAULT_NUM_RUNS):
        self.language = language
        self.test_path = test_path
        self.num_runs = num_runs
        self.settings = SettingsManager()

        self._tests_directory = pathlib.Path(__file__).parent / "tests" / self.language
        self._test_file = pathlib.Path(test_path).name
        self._weights: dict[str, float] = {
            "exact_match_weight": 0.7,  # Exact match (rule id and line number)
            "groundedness_weight": 0.2,  # Staying grounded in guidelines
            "similarity_weight": 0.1,  # Similarity between expected and actual
            "false_positive_penalty": 0.3,  # Penalty for false positives
            "fuzzy_match_bonus": 0.2,  # Bonus for fuzzy match (right rule, wrong line)
        }

    def run(self):
        """Run the evaluation."""
        custom_eval = CustomAPIViewEvaluator()
        rule_ids = set()

        all_results = {}
        for file in self._tests_directory.glob("*.jsonl"):
            if self._test_file != "all" and file.name != self._test_file:
                continue

            azure_ai_project = {
                "subscription_id": self.settings.get("EVALS_SUBSCRIPTION"),
                "resource_group_name": self.settings.get("EVALS_RG"),
                "project_name": self.settings.get("EVALS_PROJECT_NAME"),
            }
            if self.in_ci():
                service_connection_id = os.environ["AZURESUBSCRIPTION_SERVICE_CONNECTION_ID"]
                client_id = os.environ["AZURESUBSCRIPTION_CLIENT_ID"]
                tenant_id = os.environ["AZURESUBSCRIPTION_TENANT_ID"]
                system_access_token = os.environ["SYSTEM_ACCESSTOKEN"]
                kwargs = {
                    "credential": AzurePipelinesCredential(
                        service_connection_id=service_connection_id,
                        client_id=client_id,
                        tenant_id=tenant_id,
                        system_access_token=system_access_token,
                    )
                }
            else:
                kwargs = {}

            run_results = []
            for run in range(self.num_runs):
                print(f"Running evals {run + 1}/{self.num_runs} for {file.name}...")
                result = evaluate(
                    data=str(file),
                    evaluators={
                        "metrics": custom_eval,
                    },
                    evaluator_config={
                        "metrics": {
                            "column_mapping": {
                                "response": "${data.response}",
                                "query": "${data.query}",
                                "language": "${data.language}",
                                "actual": "${target.actual}",
                                "testcase": "${data.testcase}",
                                "context": "${data.context}",
                            },
                        },
                    },
                    target=_review_apiview,
                    fail_on_evaluator_errors=False,
                    azure_ai_project=azure_ai_project,
                    **kwargs,
                )

                run_result = self.record_run_result(result, rule_ids)
                print(
                    f"Average score for {file.name} run {run + 1}/{self.num_runs}: {run_result[-1]['average_score']:.2f}"
                )
                run_results.append(run_result)

            # take the median run based on the average score
            median_result = sorted(run_results, key=lambda x: x[-1]["average_score"])[len(run_results) // 2]
            all_results[file.name] = median_result

        if not all_results:
            raise ValueError(f"No tests found in: {self._test_file}")

        self.show_results(all_results)
        self.establish_baseline(all_results)
        self.calculate_coverage(rule_ids)

    def in_ci(self) -> bool:
        return bool(os.getenv("TF_BUILD"))

    def record_run_result(self, result: dict[str, Any], rule_ids: Set[str]) -> list[dict[str, Any]]:
        run_result = []
        total_score = 0

        for row in result["rows"]:
            score = self.calculate_overall_score(row)
            total_score += score
            rules = [rule["rule_ids"] for rule in json.loads(row["inputs.response"])["comments"]]
            rule_ids.update(*rules)

            run_result.append(
                {
                    "testcase": row["inputs.testcase"],
                    "expected": json.loads(row["inputs.response"]),
                    "actual": json.loads(row["outputs.actual"]),
                    # "expected_comments": row["outputs.metrics.expected_comments"],
                    "comments_found": row["outputs.metrics.comments_found"],
                    "valid_generic_comments": row["outputs.metrics.valid_generic_comments"],
                    "invalid_generic_comments": row["outputs.metrics.invalid_generic_comments"],
                    "true_positives": row["outputs.metrics.true_positives"],
                    "false_positives": row["outputs.metrics.false_positives"],
                    "false_negatives": row["outputs.metrics.false_negatives"],
                    "percent_coverage": row["outputs.metrics.percent_coverage"],
                    "rule_matches_wrong_line": row["outputs.metrics.rule_matches_wrong_line"],
                    "wrong_line_details": row["outputs.metrics.wrong_line_details"],
                    "fuzzy_matches": row["outputs.metrics.fuzzy_matches"],
                    "similarity": row["outputs.metrics.similarity"],
                    "groundedness": row["outputs.metrics.groundedness"],
                    "groundedness_reason": row["outputs.metrics.groundedness_reason"],
                    "overall_score": score,
                }
            )
        average_score = total_score / len(result["rows"])
        run_result.append({"average_score": average_score, "total_evals": len(result["rows"])})
        return run_result

    def show_results(self, all_results: dict[str, Any]) -> None:
        """Display results in a table format."""
        for name, test_results in all_results.items():
            baseline_results = {}
            baseline_path = pathlib.Path(__file__).parent / "results" / self.language / name[:-1]

            if baseline_path.exists():
                with open(baseline_path, "r", encoding="utf-8") as f:
                    baseline_data = json.load(f)
                    for result in baseline_data[:-1]:  # Skip summary
                        baseline_results[result["testcase"]] = result
                        try:
                            baseline_results["average_score"] = baseline_data[-1]["average_score"]
                        except KeyError:
                            baseline_results["average_score"] = 0.0

            self.output_table(baseline_results, test_results, name)

    def establish_baseline(self, all_results: dict[str, Any]) -> None:
        """Establish the current results as the new baseline."""

        # only ask if we're not in CI
        if self.in_ci() is False:
            establish_baseline = input("\nDo you want to establish this as the new baseline? (y/n): ")
            if establish_baseline.lower() == "y":
                for name, result in all_results.items():
                    output_path = pathlib.Path(__file__).parent / "results" / self.language / name[:-1]
                    with open(str(output_path), "w", encoding="utf-8") as f:
                        json.dump(result, indent=4, fp=f)

        # whether or not we establish a baseline, we want to write results to a temp dir
        log_path = pathlib.Path(__file__).parent / "results" / self.language / ".log"
        if not log_path.exists():
            log_path.mkdir(parents=True, exist_ok=True)

        for name, result in all_results.items():
            output_path = log_path / name[:-1]
            with open(str(output_path), "w", encoding="utf-8") as f:
                json.dump(result, indent=4, fp=f)

    def calculate_coverage(self, rule_ids: set[str]) -> None:
        """Calculate and output the coverage of tests based on the rule IDs."""

        if self._test_file == "all":
            # only update coverage if all tests are run
            output_path = pathlib.Path(__file__).parent / "results" / self.language / "coverage.json"
            guidelines_path = pathlib.Path(__file__).parent.parent / "guidelines" / self.language
            guidelines = []
            for file in guidelines_path.glob("*.json"):
                with open(file, "r", encoding="utf-8") as f:
                    guidelines.extend(json.loads(f.read()))
            guideline_rule_ids = [rule["id"] for rule in guidelines]
            difference = set(guideline_rule_ids).difference(rule_ids)
            with open(str(output_path), "w+", encoding="utf-8") as f:
                f.write(
                    json.dumps(
                        {
                            "tested": list(rule_ids),
                            "not_tested": list(difference),
                            "coverage": len(rule_ids) / len(guideline_rule_ids) * 100,
                        },
                        indent=4,
                    )
                )
            print(f"\nTest coverage for {self.language}: {len(rule_ids) / len(guideline_rule_ids) * 100:.2f}%")

    def calculate_overall_score(self, row: dict[str, Any]) -> float:
        """Calculate weighted score based on various metrics."""
        if row["outputs.metrics.expected_comments"] == 0:
            # tests with no violations are all or nothing
            # but still give credit if no violations found, but valid generic comments found
            if (
                row["outputs.metrics.comments_found"] == 0
                or row["outputs.metrics.comments_found"] == row["outputs.metrics.valid_generic_comments"]
            ):
                # give credit b/c we have no violations to calc groundedness or similarity
                row["outputs.metrics.groundedness"] = 5
                row["outputs.metrics.similarity"] = 5
                return 100.0

            return 0.0

        exact_match_score = row["outputs.metrics.true_positives"] / row["outputs.metrics.expected_comments"]

        remaining_comments = row["outputs.metrics.expected_comments"] - row["outputs.metrics.true_positives"]
        fuzzy_match_score = (
            row["outputs.metrics.rule_matches_wrong_line"] / remaining_comments if remaining_comments > 0 else 0.0
        )

        false_positive_rate = (
            row["outputs.metrics.false_positives"] / row["outputs.metrics.comments_found"]
            if row["outputs.metrics.comments_found"] > 0
            else 0.0
        )

        groundedness_normalized = (row["outputs.metrics.groundedness"] - 1) / 4
        similarity_normalized = (row["outputs.metrics.similarity"] - 1) / 4

        score = (
            self._weights["exact_match_weight"] * exact_match_score
            + self._weights["groundedness_weight"] * groundedness_normalized
            + self._weights["similarity_weight"] * similarity_normalized
            + self._weights["fuzzy_match_bonus"] * fuzzy_match_score
            - self._weights["false_positive_penalty"] * false_positive_rate
        )

        normalized_score = max(0, min(100, score * 100))
        return round(normalized_score)

    def format_terminal_diff(self, new: float, old: float, format_str: str = ".1f", reverse: bool = False) -> str:
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

    def output_table(
        self, baseline_results: dict[str, Any], eval_results: list[dict[str, Any]], file_name: str
    ) -> None:
        headers = [
            "Test Case",
            "Score",
            "Violations found",
            "Exact matches (TP)",
            "Valid generic comments",
            "Fuzzy matches",
            "False positives (FP)",
            "Groundedness",
            "Similarity",
        ]
        terminal_rows = []

        for result in eval_results[:-1]:  # Skip summary object
            testcase = result["testcase"]
            score = result["overall_score"]
            exact = result["true_positives"]
            rule = result["rule_matches_wrong_line"]
            fp = result["false_positives"]
            ground = result["groundedness"]
            sim = result["similarity"]
            valid_generic = result["valid_generic_comments"]
            # comments_found = f"{result['comments_found']} / {result['expected_comments']}"
            comments_found = f"{result['comments_found']}"

            terminal_row = [testcase]
            if testcase in baseline_results:
                base = baseline_results[testcase]
                terminal_row.extend(
                    [
                        f"{score:.1f}{self.format_terminal_diff(score, base['overall_score'])}",
                        comments_found,
                        f"{exact}{self.format_terminal_diff(exact, base['true_positives'], 'd')}",
                        f"{valid_generic}{self.format_terminal_diff(valid_generic, base['valid_generic_comments'], 'd')}",
                        f"{rule}{self.format_terminal_diff(rule, base['rule_matches_wrong_line'], 'd')}",
                        f"{fp}{self.format_terminal_diff(fp, base['false_positives'], 'd', reverse=True)}",
                        f"{ground:.1f}{self.format_terminal_diff(ground, base['groundedness'])}",
                        f"{sim:.1f}{self.format_terminal_diff(sim, base['similarity'])}",
                    ]
                )
            else:
                values = [
                    f"{score:.1f}",
                    comments_found,
                    f"{exact}",
                    f"{valid_generic}",
                    str(rule),
                    str(fp),
                    f"{ground:.1f}",
                    f"{sim:.1f}",
                ]
                terminal_row.extend(values)

            terminal_rows.append(terminal_row)

        print("====================================================")
        print(f"\n\n✨ {file_name} results:\n")
        print(tabulate(terminal_rows, headers, tablefmt="simple"))
        if baseline_results:
            print(
                f"\n{file_name} average score: {eval_results[-1]['average_score']} {self.format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}\n\n"
            )
