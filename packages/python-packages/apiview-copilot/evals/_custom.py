import copy
import json
import os
import pathlib
import sys
from abc import ABC, abstractmethod
from typing import Any, Dict, Set, Tuple

import prompty
import prompty.azure_beta
import yaml

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from pathlib import Path

from azure.ai.evaluation import GroundednessEvaluator, SimilarityEvaluator
from evals._config_loader import EvaluationConfig, WorkflowConfigError
from src._settings import SettingsManager
from src._utils import get_prompt_path


def _review_apiview(query: str, language: str):
    """APIView review target function for evals framework."""
    from src._apiview_reviewer import ApiViewReview

    reviewer = ApiViewReview(target=query, language=language, base=None)
    review = reviewer.run()
    reviewer.close()
    return {"actual": review.model_dump_json()}


def _mention_action_workflow(
    testcase: str, response: str, language: str, package_name: str, code: str, other_comments: str, trigger_comment: str
):
    prompty_path = Path(__file__).parent.parent / "prompts" / "mention" / "parse_conversation_action.prompty"
    prompty_kwargs = {
        "testcase": testcase,
        "response": response,
        "language": language,
        "package_name": package_name,
        "code": code,
        "other_comments": other_comments,
        "trigger_comment": trigger_comment,
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}


def _thread_resolution_action_workflow(
    testcase: str, response: str, language: str, package_name: str, code: str, comments: str
):
    prompty_path = (
        Path(__file__).parent.parent / "prompts" / "thread_resolution" / "parse_thread_resolution_action.prompty"
    )
    prompty_kwargs = {
        "testcase": testcase,
        "response": response,
        "language": language,
        "package_name": package_name,
        "code": code,
        "comments": comments,
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}


def _filter_comment_metadata(testcase: str, response: str, language: str, exceptions: str, outline: str, content: str):
    prompty_path = Path(__file__).parent.parent / "prompts" / "api_review" / "filter_comment_with_metadata.prompty"
    prompty_kwargs = {
        "testcase": testcase,
        "response": response,
        "language": language,
        "exceptions": exceptions,
        "outline": outline,
        "content": content,
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}


class BaseEvaluator(ABC):
    """Base class for custom evaluators in the evals framework.

    This abstract base class defines the minimal interface that all custom evaluators
    must implement to be compatible with the evals runner framework.
    """

    @abstractmethod
    def __call__(self, **kwargs) -> Dict[str, Any]:
        """Evaluate the given inputs and return evaluation metrics.

        Args:
            **kwargs: Arbitrary keyword arguments containing evaluation inputs.
                     The specific arguments depend on the evaluator implementation.

        Returns:
            Dict[str, Any]: A dictionary containing evaluation metrics and results.
                           The structure depends on the evaluator implementation.
        """
        pass

    @property
    @abstractmethod
    def evaluator_config(self) -> Dict[str, Any]:
        """Return the evaluator configuration for the Azure AI evaluation framework.

        Returns:
            Dict[str, Any]: Configuration dictionary containing column mappings
                           and other evaluator-specific settings.
        """
        pass

    @property
    @abstractmethod
    def target_function(self) -> callable:
        """Return the target function that generates data for this evaluator."""
        pass

    @abstractmethod
    def process_results(self, raw_results: list, guideline_ids: set) -> dict:
        """Process raw evaluation results into final format.

        Args:
            raw_results: List of run results from multiple evaluation runs
            guideline_ids: Set to collect guideline IDs (modified in place)

        Returns:
            dict: Processed results in evaluator-specific format
        """
        pass

    @abstractmethod
    def show_results(self, processed_results: dict) -> None:
        """Display results in evaluator-specific format.

        Args:
            processed_results: Results from process_results method
        """
        pass

    def post_process(
        self, processed_results: dict, tests_directory: str, test_file: str, guideline_ids: set = None
    ) -> None:
        """Optional post-processing after results are shown (e.g., baselines, coverage).

        Override this method if your evaluator needs additional processing like
        baseline establishment or coverage calculation.

        Args:
            processed_results: Results from process_results method
            tests_directory: Path to tests directory
            test_file: Name of test file being run
            guideline_ids: Set of guideline IDs collected during evaluation (optional)
        """
        pass  # Default: no post-processing

    @classmethod
    def validate_config_schema(cls, raw_config: dict) -> dict | None:
        """Base validation - subclasses should override as needed."""
        return None


class CustomAPIViewEvaluator(BaseEvaluator):
    """Evaluator for comparing expected and actual APIView comments."""

    def __init__(self, workflow_config=None):
        settings = SettingsManager()
        # for best results, this should always be a different model from the one we are evaluating
        self._judge_model = "gpt-4.1"
        self._model_config: dict[str, str] = {
            "azure_endpoint": settings.get("OPENAI_ENDPOINT"),
            "api_key": settings.get("OPENAI_API_KEY"),
            "azure_deployment": self._judge_model,
            "api_version": "2025-03-01-preview",
        }
        self._weights: dict[str, float] = {
            "exact_match_weight": 0.7,  # Exact match (rule id and line number)
            "groundedness_weight": 0.2,  # Staying grounded in guidelines
            "similarity_weight": 0.1,  # Similarity between expected and actual
            "false_positive_penalty": 0.3,  # Penalty for false positives
            "fuzzy_match_bonus": 0.2,  # Bonus for fuzzy match (right rule, wrong line)
        }

    @property
    def evaluator_config(self) -> Dict[str, Any]:
        """Return the evaluator configuration for the Azure AI evaluation framework."""
        return {
            "column_mapping": {
                "response": "${data.response}",
                "query": "${data.query}",
                "language": "${data.language}",
                "actual": "${target.actual}",
                "testcase": "${data.testcase}",
                "context": "${data.context}",
            },
        }

    @property
    def target_function(self) -> callable:
        """Return the APIView review function."""
        return _review_apiview

    def _calculate_overall_score(self, review_eval: dict[str, Any]) -> float:
        """Calculate the overall score based on the review evaluation metrics."""
        if review_eval["expected_comments"] == 0:
            # tests with no violations are all or nothing
            # but still give credit if no violations found, but valid generic comments found
            if (
                review_eval["comments_found"] == 0
                or review_eval["comments_found"] == review_eval["valid_generic_comments"]
            ):
                # give credit b/c we have no violations to calc groundedness or similarity
                review_eval["groundedness"] = 5
                review_eval["similarity"] = 5
                return 100.0
            return 0.0

        exact_match_score = review_eval["true_positives"] / review_eval["expected_comments"]

        remaining_comments = review_eval["expected_comments"] - review_eval["true_positives"]
        fuzzy_match_score = (
            review_eval["rule_matches_wrong_line"] / remaining_comments if remaining_comments > 0 else 0.0
        )

        false_positive_rate = (
            review_eval["false_positives"] / review_eval["comments_found"] if review_eval["comments_found"] > 0 else 0.0
        )
        score = (
            self._weights["exact_match_weight"] * exact_match_score
            + self._weights["groundedness_weight"] * (review_eval["groundedness"] - 1) / 4
            + self._weights["similarity_weight"] * (review_eval["similarity"] - 1) / 4
            + self._weights["fuzzy_match_bonus"] * fuzzy_match_score
            - self._weights["false_positive_penalty"] * false_positive_rate
        )

        normalized_score = max(0, min(100, score * 100))
        return round(normalized_score)

    def _get_comment_matches(self, expected: dict[str, Any], actual: dict[str, Any]) -> Tuple[Set, Set, Set]:
        """Compare comments based on both line numbers and rule IDs."""
        exact_matches = set()
        rule_matches_wrong_line = set()

        # Create a copy to work with
        comments_left = copy.deepcopy(actual["comments"])

        for expected_comment in expected["comments"]:
            e_line = expected_comment["line_no"]
            e_rules = frozenset(expected_comment["guideline_ids"])

            for actual_comment in comments_left:
                a_line = actual_comment["line_no"]
                a_rules = frozenset(actual_comment["guideline_ids"])

                rule_match = any(rule for rule in a_rules if rule in e_rules)
                if e_line == a_line and rule_match:
                    exact_matches.add((e_line, tuple(sorted(e_rules))))
                    # Remove the matched actual comment to avoid double counting
                    comments_left.remove(actual_comment)
                    break
                if rule_match:
                    if abs(e_line - a_line) <= 5:
                        # If the line numbers are close, consider it a match
                        rule_matches_wrong_line.add((tuple(sorted(e_rules)), e_line, a_line))
                        comments_left.remove(actual_comment)
                        break

        return exact_matches, rule_matches_wrong_line, comments_left

    def _evaluate_generic_comments(self, query: str, language: str, generic_comments: list[dict[str, Any]]) -> None:
        """Evaluate generic comments. If they are invalid, they count as false positives and receive penalty."""

        filter_path = pathlib.Path(__file__).parent.parent / "metadata" / language / "filter.yaml"
        with open(filter_path, "r", encoding="utf-8") as f:
            filter_data = yaml.safe_load(f)
            exceptions = filter_data["exceptions"].strip().split("\n")
            exceptions = [e.split(". ", 1)[1] for e in exceptions]

        settings = SettingsManager()
        for comment in generic_comments:
            if comment.get("source") != "generic":
                continue
            line_no = comment["line_no"]
            start_idx = max(0, line_no - 10)
            end_idx = min(len(query), line_no + 10)
            context = query[start_idx:end_idx]
            prompt_path = get_prompt_path(folder="evals", filename="eval_judge_prompt.prompty")
            response = prompty.execute(
                prompt_path,
                inputs={
                    "code": context,
                    "comment": comment["comment"],
                    "exceptions": exceptions,
                    "language": language,
                },
                configuration={"api_key": settings.get("OPENAI_API_KEY")},
            )
            comment["valid"] = "true" in response.lower()

    def _groundedness(self, actual: dict[str, Any], context: str) -> None:
        actual = [c for c in actual["comments"] if c["guideline_ids"]]
        if not actual:
            return {"groundedness": 0.0, "groundedness_reason": "No comments found."}
        groundedness = GroundednessEvaluator(model_config=self._model_config)(
            response=json.dumps(actual), context=context
        )
        return groundedness

    def _similarity(self, expected: dict[str, Any], actual: dict[str, Any], query: str) -> None:
        actual = [c for c in actual["comments"] if c["guideline_ids"]]
        if not actual:
            return {"similarity": 0.0}

        similarity = SimilarityEvaluator(model_config=self._model_config)(
            response=json.dumps(actual),
            query=query,
            ground_truth=json.dumps([c for c in expected["comments"] if c["guideline_ids"]]),
        )
        return similarity

    def __call__(self, *, response: str, query: str, language: str, actual: str, testcase: str, context: str, **kwargs):
        expected = json.loads(response)
        actual = json.loads(actual)

        # Filter out summary comments
        expected["comments"] = [c for c in expected["comments"] if c.get("source") != "summary"]
        actual["comments"] = [c for c in actual["comments"] if c.get("source") != "summary"]

        groundedness = self._groundedness(actual, context)
        similarity = self._similarity(expected, actual, query)

        exact_matches, rule_matches_wrong_line, generic_comments = self._get_comment_matches(expected, actual)
        self._evaluate_generic_comments(query, language, generic_comments)

        exact_matches_count = len(exact_matches)
        rule_matches_wrong_line_count = len(rule_matches_wrong_line)
        expected_comment_count = len([c for c in expected["comments"] if c["guideline_ids"]])
        valid_generic_comment_count = len([c for c in generic_comments if c.get("valid") is True])
        invalid_generic_comment_count = len([c for c in generic_comments if c.get("valid") is False])
        total_comment_count = len(actual["comments"])
        true_positive_count = len(exact_matches)
        false_positive_count = (
            total_comment_count - exact_matches_count - rule_matches_wrong_line_count - valid_generic_comment_count
        )
        false_negative_count = expected_comment_count - exact_matches_count - rule_matches_wrong_line_count
        percent_coverage = (exact_matches_count / expected_comment_count * 100) if expected_comment_count else 0

        review_eval = {
            "expected_comments": expected_comment_count,
            "comments_found": total_comment_count,
            "true_positives": true_positive_count,
            "valid_generic_comments": valid_generic_comment_count,
            "invalid_generic_comments": invalid_generic_comment_count,
            "false_positives": false_positive_count,
            "false_negatives": false_negative_count,
            "percent_coverage": percent_coverage,
            "rule_matches_wrong_line": rule_matches_wrong_line_count,
            "wrong_line_details": list(rule_matches_wrong_line),
            "fuzzy_matches": rule_matches_wrong_line_count,
            "groundedness": groundedness["groundedness"],
            "groundedness_reason": groundedness["groundedness_reason"],
            "similarity": similarity["similarity"],
            "testcase": testcase,
        }
        review_eval["score"] = self._calculate_overall_score(review_eval)
        return review_eval

    def post_process(
        self, processed_results: dict, tests_directory: str, test_file: str, guideline_ids: set = None
    ) -> None:
        """APIView-specific post-processing: baselines and coverage."""
        self._establish_baseline(processed_results, tests_directory)

        # Only calculate coverage if all tests were run and we have guideline_ids
        if test_file == "all" and guideline_ids is not None:
            self._calculate_coverage(guideline_ids, tests_directory)

    def process_results(self, raw_results: list, guideline_ids: set) -> dict:
        """Process  evaluation results for APIView evaluator."""
        all_results = {}

        for run_result_data in raw_results:
            for file_name, result in run_result_data.items():
                if file_name not in all_results:
                    all_results[file_name] = []

                run_result = self._record_run_result(result, guideline_ids)
                all_results[file_name].append(run_result)

        # take the median run based on average score
        final_results = {}
        for file_name, run_results_list in all_results.items():
            median_result = sorted(run_results_list, key=lambda x: x[-1]["average_score"])[len(run_results_list) // 2]
            final_results[file_name] = median_result

        return final_results

    def _record_run_result(self, result: dict, guideline_ids: set) -> list:
        """Process a single run result (same logic as original)."""
        run_result = []
        total_score = 0

        for row in result["rows"]:
            # Use the score calculated by the evaluator itself
            score = row["outputs.metrics.score"]
            total_score += score
            rules = [rule["guideline_ids"] for rule in json.loads(row["inputs.response"])["comments"]]
            guideline_ids.update(*rules)

            run_result.append(
                {
                    "testcase": row["inputs.testcase"],
                    "expected": json.loads(row["inputs.response"]),
                    "actual": json.loads(row["outputs.actual"]),
                    "expected_comments": row["outputs.metrics.expected_comments"],
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

    def show_results(self, processed_results: dict) -> None:
        """Display APIView results in table format."""
        for name, test_results in processed_results.items():
            baseline_results = {}
            baseline_path = (
                pathlib.Path(__file__).parent / "results" / "python" / name[:-1]
            )  # TODO: make language dynamic

            if baseline_path.exists():
                with open(baseline_path, "r", encoding="utf-8") as f:
                    baseline_data = json.load(f)
                    for result in baseline_data[:-1]:  # Skip summary
                        baseline_results[result["testcase"]] = result
                        try:
                            baseline_results["average_score"] = baseline_data[-1]["average_score"]
                        except KeyError:
                            baseline_results["average_score"] = 0.0

            self._output_table(baseline_results, test_results, name)

    def _establish_baseline(
        self, processed_results: dict, tests_directory: str, test_file: str, guideline_ids: set = None
    ) -> None:
        """Establish baseline for APIView evaluator."""
        # only ask if we're not in CI
        if not bool(os.getenv("TF_BUILD")):
            establish_baseline = input("\nDo you want to establish this as the new baseline? (y/n): ")
            if establish_baseline.lower() == "y":
                for name, result in processed_results.items():
                    output_path = pathlib.Path(tests_directory).parent / "results" / name[:-1]
                    output_path.parent.mkdir(parents=True, exist_ok=True)
                    with open(str(output_path), "w", encoding="utf-8") as f:
                        json.dump(result, indent=4, fp=f)

        # whether or not we establish a baseline, we want to write results to a temp dir
        log_path = pathlib.Path(tests_directory).parent / "results" / ".log"
        if not log_path.exists():
            log_path.mkdir(parents=True, exist_ok=True)

        for name, result in processed_results.items():
            output_path = log_path / name[:-1]
            with open(str(output_path), "w", encoding="utf-8") as f:
                json.dump(result, indent=4, fp=f)

    def _calculate_coverage(self, guideline_ids: set, language: str, tests_directory: str) -> None:
        """Calculate coverage for APIView evaluator."""
        # only update coverage if all tests are run (this logic might need to be passed in)
        output_path = pathlib.Path(tests_directory).parent / "results" / language / "coverage.json"
        guidelines_path = pathlib.Path(tests_directory).parent.parent / "guidelines" / language
        guidelines = []
        for file in guidelines_path.glob("*.json"):
            with open(file, "r", encoding="utf-8") as f:
                guidelines.extend(json.loads(f.read()))
        guideline_rule_ids = [rule["id"] for rule in guidelines]
        difference = set(guideline_rule_ids).difference(guideline_ids)

        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(str(output_path), "w+", encoding="utf-8") as f:
            f.write(
                json.dumps(
                    {
                        "tested": list(guideline_ids),
                        "not_tested": list(difference),
                        "coverage": len(guideline_ids) / len(guideline_rule_ids) * 100,
                    },
                    indent=4,
                )
            )
        print(f"\nTest coverage for {language}: {len(guideline_ids) / len(guideline_rule_ids) * 100:.2f}%")

    def _format_terminal_diff(self, new: float, old: float, format_str: str = ".1f", reverse: bool = False) -> str:
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

    def _output_table(self, baseline_results: dict, eval_results: list, file_name: str) -> None:
        """Output results table for APIView evaluator."""
        from tabulate import tabulate

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
            comments_found = f"{result['comments_found']} / {result['expected_comments']}"

            terminal_row = [testcase]
            if testcase in baseline_results:
                base = baseline_results[testcase]
                terminal_row.extend(
                    [
                        f"{score:.1f}{self._format_terminal_diff(score, base['overall_score'])}",
                        comments_found,
                        f"{exact}{self._format_terminal_diff(exact, base['true_positives'], 'd')}",
                        f"{valid_generic}{self._format_terminal_diff(valid_generic, base['valid_generic_comments'], 'd')}",
                        f"{rule}{self._format_terminal_diff(rule, base['rule_matches_wrong_line'], 'd')}",
                        f"{fp}{self._format_terminal_diff(fp, base['false_positives'], 'd', reverse=True)}",
                        f"{ground:.1f}{self._format_terminal_diff(ground, base['groundedness'])}",
                        f"{sim:.1f}{self._format_terminal_diff(sim, base['similarity'])}",
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
                f"\n{file_name} average score: {eval_results[-1]['average_score']} {self._format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}\n\n"
            )


class PromptyEvaluator(BaseEvaluator):
    def __init__(self, config):
        if config is None:
            raise ValueError("PromptyEvaluator requires config")
        self.config = config

        # Get evaluation fields from config
        self.evaluation_config = self.config.evaluation_config
        self.comparison_field = self.evaluation_config.comparison_field
        self.display_name = self.evaluation_config.display_name
        self.breakdown_categories = self.evaluation_config.breakdown_categories
        self.correct_field_name = f"correct_{self.comparison_field}"

    def __call__(self, *, response: str, actual: str, testcase: str, **kwargs):
        # response and actual may already be parsed dicts (from prompt execution or JSONL loader).
        # Accept dicts directly and fall back to json.loads for strings.
        try:
            if isinstance(response, (dict, list)):
                expected_data = response
            else:
                expected_data = json.loads(response)
        except (TypeError, json.JSONDecodeError):
            # If response is not JSON, treat it as a plain string.
            expected_data = response

        try:
            if isinstance(actual, (dict, list)):
                actual_data = actual
            else:
                actual_data = json.loads(actual)
        except (TypeError, json.JSONDecodeError):
            actual_data = actual

        # Safely extract comparison values
        try:
            expected_value = (
                expected_data.get(self.comparison_field, "").strip()
                if isinstance(expected_data, dict)
                else str(expected_data).strip()
            )
        except Exception:
            expected_value = str(expected_data).strip()

        try:
            actual_value = (
                actual_data.get(self.comparison_field, "").strip()
                if isinstance(actual_data, dict)
                else str(actual_data).strip()
            )
        except Exception:
            actual_value = str(actual_data).strip()

        is_correct = expected_value == actual_value

        return {
            self.correct_field_name: is_correct,
            "actual": actual,
            "expected": response,
            "testcase": testcase,
            "score": 100 if is_correct else 0,
            f"expected_{self.comparison_field}": expected_value,
            f"actual_{self.comparison_field}": actual_value,
        }

    def process_results(self, raw_results: list, guideline_ids: set = None) -> dict:
        """Process prompt workflow results."""
        all_results = {}

        for run_result_data in raw_results:
            for file_name, result in run_result_data.items():
                if file_name not in all_results:
                    all_results[file_name] = []

                run_summary = {
                    "total_tests": 0,
                    f"{self.correct_field_name}s": 0,
                    "test_results": [],
                    f"{self.comparison_field}_breakdown": self.breakdown_categories.copy(),
                }

                # Process each test case in this run
                for row in result.get("rows", []):
                    test_result = {
                        "testcase": row.get("inputs.testcase", "unknown"),
                        "correct": row.get(f"outputs.metrics.{self.correct_field_name}", False),
                        f"expected_{self.comparison_field}": row.get(
                            f"outputs.metrics.expected_{self.comparison_field}", ""
                        ),
                        f"actual_{self.comparison_field}": row.get(
                            f"outputs.metrics.actual_{self.comparison_field}", ""
                        ),
                        "score": row.get("outputs.metrics.score", 0),
                    }

                    run_summary["test_results"].append(test_result)
                    run_summary["total_tests"] += 1

                    if test_result["correct"]:
                        run_summary[f"{self.correct_field_name}s"] += 1

                    # Track breakdown by comparison field
                    expected_val = test_result[f"expected_{self.comparison_field}"]
                    if expected_val in run_summary[f"{self.comparison_field}_breakdown"]:
                        run_summary[f"{self.comparison_field}_breakdown"][expected_val]["total"] += 1
                        if test_result["correct"]:
                            run_summary[f"{self.comparison_field}_breakdown"][expected_val]["correct"] += 1

                run_summary["accuracy"] = (
                    (run_summary[f"{self.correct_field_name}s"] / run_summary["total_tests"]) * 100
                    if run_summary["total_tests"] > 0
                    else 0
                )
                all_results[file_name].append(run_summary)

        # For multiple runs: take the median accuracy run
        final_results = {}
        for file_name, runs in all_results.items():
            if len(runs) == 1:
                final_results[file_name] = runs[0]
            else:
                # Find median by accuracy
                sorted_runs = sorted(runs, key=lambda x: x["accuracy"])
                median_idx = len(sorted_runs) // 2
                final_results[file_name] = sorted_runs[median_idx]

        return final_results

    def show_results(self, processed_results: dict) -> None:
        """Display prompt workflow results with category breakdown."""
        from tabulate import tabulate

        for file_name, results in processed_results.items():
            accuracy = results["accuracy"]
            correct = results[f"{self.correct_field_name}s"]
            total = results["total_tests"]

            print("====================================================")
            print(f"\n\n✨ {file_name} results:\n")
            print(f"Overall {self.display_name} Accuracy: {correct}/{total} ({accuracy:.1f}%)\n")

            # Show breakdown by category
            breakdown = results[f"{self.comparison_field}_breakdown"]
            breakdown_rows = []
            for rec_type, stats in breakdown.items():
                if stats["total"] > 0:
                    type_accuracy = (stats["correct"] / stats["total"]) * 100
                    breakdown_rows.append([rec_type, f"{stats['correct']}/{stats['total']}", f"{type_accuracy:.1f}%"])

            if breakdown_rows:
                print(f"{self.display_name} Breakdown:")
                print(tabulate(breakdown_rows, headers=["Type", "Correct", "Accuracy"], tablefmt="simple"))
                print()

            # Show failed cases with details
            failed_cases = [test for test in results["test_results"] if not test["correct"]]
            if failed_cases:
                print("Failed Cases:")
                for test in failed_cases:
                    print(f"  ❌ {test['testcase']}")
                    print(f"     Expected {self.display_name}: {test[f'expected_{self.comparison_field}']}")
                    print(f"     Actual {self.display_name}:   {test[f'actual_{self.comparison_field}']}")
                    print()

    def _discover_jsonl_fields(self) -> set[str]:
        """Peek at JSONL to see what fields are available."""

    @property
    def evaluator_config(self) -> Dict[str, Any]:
        config = {}
        with open(self._jsonl_file, encoding="utf-8") as f:
            first_line = json.loads(f.readline())
        fields = set(first_line.keys())

        for field in fields:
            config[field] = f"${{data.{field}}}"
        config["actual"] = "${target.actual}"
        return {"column_mapping": config}

    @property
    def target_function(self) -> callable:
        workflow_targets = {
            "mention_action": _mention_action_workflow,
            "thread_resolution_action": _thread_resolution_action_workflow,
            "filter_comment_metadata": _filter_comment_metadata,
            # Add more workflows as needed
        }

        workflow_name = self.config.name
        if workflow_name not in workflow_targets:
            raise ValueError(f"No target function defined for workflow: {workflow_name}")

        return workflow_targets[workflow_name]

    @classmethod
    def validate_config_schema(cls, raw_config: dict) -> dict | None:
        """Validate prompt workflow configuration."""
        if not raw_config or not isinstance(raw_config, dict):
            return None

        comparison_field = raw_config.get("comparison_field", "action")
        display_name = raw_config.get("display_name", "Action")
        breakdown_categories = raw_config.get("breakdown_categories", {})

        # Validate breakdown_categories structure
        if breakdown_categories:
            for key, value in breakdown_categories.items():
                if not isinstance(value, dict) or "correct" not in value or "total" not in value:
                    raise WorkflowConfigError(f"breakdown_categories.{key} must have 'correct' and 'total' fields")
                if not isinstance(value["correct"], int) or not isinstance(value["total"], int):
                    raise WorkflowConfigError(f"breakdown_categories.{key} correct/total must be integers")

        return EvaluationConfig(
            comparison_field=comparison_field, display_name=display_name, breakdown_categories=breakdown_categories
        )


# Register evaluators at module load time to prevent circular imports
from evals._config_loader import register_evaluator

register_evaluator("apiview", CustomAPIViewEvaluator)
register_evaluator("prompt", PromptyEvaluator)
