import argparse
import copy
import json
import os
import pathlib
import sys
from typing import Any, Set, Tuple

import prompty
import prompty.azure_beta
import yaml

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from src._apiview_reviewer import ApiViewReview
from src._settings import SettingsManager
from src._utils import get_prompt_path

# set before azure.ai.evaluation import to make PF output less noisy
os.environ["PF_LOGGING_LEVEL"] = "CRITICAL"

import dotenv
from azure.ai.evaluation import GroundednessEvaluator, SimilarityEvaluator, evaluate
from azure.identity import AzurePipelinesCredential
from tabulate import tabulate

dotenv.load_dotenv()

NUM_RUNS: int = 3
# for best results, this should always be a different model from the one we are evaluating
MODEL_JUDGE = "gpt-4.1-nano"

weights: dict[str, float] = {
    "exact_match_weight": 0.7,  # Exact match (rule id and line number)
    "groundedness_weight": 0.2,  # Staying grounded in guidelines
    "similarity_weight": 0.1,  # Similarity between expected and actual
    "false_positive_penalty": 0.3,  # Penalty for false positives
    "fuzzy_match_bonus": 0.2,  # Bonus for fuzzy match (right rule, wrong line)
}


def in_ci():
    return os.getenv("TF_BUILD", False)


def get_model_config():
    settings = SettingsManager()
    model_config: dict[str, str] = {
        "azure_endpoint": settings.get("OPENAI_ENDPOINT"),
        "api_key": settings.get("OPENAI_API_KEY"),
        "azure_deployment": MODEL_JUDGE,
        "api_version": "2025-03-01-preview",
    }
    return model_config


class CustomAPIViewEvaluator:
    """Evaluator for comparing expected and actual APIView comments."""

    def __init__(self):
        self._groundedness_eval = GroundednessEvaluator(model_config=get_model_config())
        self._similarity_eval = SimilarityEvaluator(model_config=get_model_config())

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
            weights["exact_match_weight"] * exact_match_score
            + weights["groundedness_weight"] * (review_eval["groundedness"] - 1) / 4
            + weights["similarity_weight"] * (review_eval["similarity"] - 1) / 4
            + weights["fuzzy_match_bonus"] * fuzzy_match_score
            - weights["false_positive_penalty"] * false_positive_rate
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
            e_rules = frozenset(expected_comment["rule_ids"])

            for actual_comment in comments_left:
                a_line = actual_comment["line_no"]
                a_rules = frozenset(actual_comment["rule_ids"])

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

        settings = SettingsManager()
        filter_path = pathlib.Path(__file__).parent.parent / "metadata" / language / "filter.yaml"
        with open(filter_path, "r", encoding="utf-8") as f:
            filter_data = yaml.safe_load(f)
            exceptions = filter_data["exceptions"].strip().split("\n")
            exceptions = [e.split(". ", 1)[1] for e in exceptions]

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
        actual = [c for c in actual["comments"] if c["rule_ids"]]
        if not actual:
            return {"groundedness": 0.0, "groundedness_reason": "No comments found."}
        groundedness = self._groundedness_eval(response=json.dumps(actual), context=context)
        return groundedness

    def _similarity(self, expected: dict[str, Any], actual: dict[str, Any], query: str) -> None:
        actual = [c for c in actual["comments"] if c["rule_ids"]]
        if not actual:
            return {"similarity": 0.0}
        similarity = self._similarity_eval(
            response=json.dumps(actual),
            query=query,
            ground_truth=json.dumps([c for c in expected["comments"] if c["rule_ids"]]),
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
        expected_comments = len([c for c in expected["comments"] if c["rule_ids"]])
        valid_generic_comments = len([c for c in generic_comments if c.get("valid") is True])
        invalid_generic_comments = [c for c in generic_comments if c.get("valid") is False]
        review_eval = {
            "expected_comments": expected_comments,
            "comments_found": len(actual["comments"]),
            "true_positives": len(exact_matches),
            "valid_generic_comments": valid_generic_comments,
            "invalid_generic_comments": invalid_generic_comments,
            "false_positives": len(actual["comments"])
            - (len(exact_matches) + len(rule_matches_wrong_line))
            - valid_generic_comments,
            "false_negatives": expected_comments - (len(exact_matches) + len(rule_matches_wrong_line)),
            "percent_coverage": ((len(exact_matches) / expected_comments * 100) if expected_comments else 0),
            "rule_matches_wrong_line": len(rule_matches_wrong_line),
            "wrong_line_details": list(rule_matches_wrong_line),
            "fuzzy_matches": len(rule_matches_wrong_line),
            "groundedness": groundedness["groundedness"],
            "groundedness_reason": groundedness["groundedness_reason"],
            "similarity": similarity["similarity"],
            "testcase": testcase,
        }
        review_eval["score"] = self._calculate_overall_score(review_eval)
        return review_eval


def review_apiview(query: str, language: str):
    reviewer = ApiViewReview(target=query, language=language, base=None)
    review = reviewer.run()
    reviewer.close()
    return {"actual": review.model_dump_json()}


def calculate_overall_score(row: dict[str, Any]) -> float:
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
        weights["exact_match_weight"] * exact_match_score
        + weights["groundedness_weight"] * groundedness_normalized
        + weights["similarity_weight"] * similarity_normalized
        + weights["fuzzy_match_bonus"] * fuzzy_match_score
        - weights["false_positive_penalty"] * false_positive_rate
    )

    normalized_score = max(0, min(100, score * 100))
    return round(normalized_score)


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


def show_results(args: argparse.Namespace, all_results: dict[str, Any]) -> None:
    """Display results in a table format."""
    for name, test_results in all_results.items():
        baseline_results = {}
        baseline_path = pathlib.Path(__file__).parent / "results" / args.language / name[:-1]

        if baseline_path.exists():
            with open(baseline_path, "r") as f:
                baseline_data = json.load(f)
                for result in baseline_data[:-1]:  # Skip summary
                    baseline_results[result["testcase"]] = result
                baseline_results["average_score"] = baseline_data[-1]["average_score"]

        output_table(baseline_results, test_results, name)


def output_table(baseline_results: dict[str, Any], eval_results: list[dict[str, Any]], file_name: str) -> None:
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
                    f"{score:.1f}{format_terminal_diff(score, base['overall_score'])}",
                    comments_found,
                    f"{exact}{format_terminal_diff(exact, base['true_positives'], 'd')}",
                    f"{valid_generic}{format_terminal_diff(valid_generic, base['valid_generic_comments'], 'd')}",
                    f"{rule}{format_terminal_diff(rule, base['rule_matches_wrong_line'], 'd')}",
                    f"{fp}{format_terminal_diff(fp, base['false_positives'], 'd', reverse=True)}",
                    f"{ground:.1f}{format_terminal_diff(ground, base['groundedness'])}",
                    f"{sim:.1f}{format_terminal_diff(sim, base['similarity'])}",
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
            f"\n{file_name} average score: {eval_results[-1]['average_score']} {format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}\n\n"
        )


def calculate_coverage(args: argparse.Namespace, rule_ids: set[str]) -> None:
    """Calculate and output the coverage of tests based on the rule IDs."""

    if args.test_file == "all":
        # only update coverage if all tests are run
        output_path = pathlib.Path(__file__).parent / "results" / args.language / "coverage.json"
        guidelines_path = pathlib.Path(__file__).parent.parent / "guidelines" / args.language
        guidelines = []
        for file in guidelines_path.glob("*.json"):
            with open(file, "r") as f:
                guidelines.extend(json.loads(f.read()))
        guideline_rule_ids = [rule["id"] for rule in guidelines]
        difference = set(guideline_rule_ids).difference(rule_ids)
        with open(str(output_path), "w+") as f:
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

        print(f"\nTest coverage for {args.language}: {len(rule_ids) / len(guideline_rule_ids) * 100:.2f}%")


def establish_baseline(args: argparse.Namespace, all_results: dict[str, Any]) -> None:
    """Establish the current results as the new baseline."""

    # only ask if we're not in CI
    if in_ci() is False:
        establish_baseline = input("\nDo you want to establish this as the new baseline? (y/n): ")
        if establish_baseline.lower() == "y":
            for name, result in all_results.items():
                output_path = pathlib.Path(__file__).parent / "results" / args.language / name[:-1]
                with open(str(output_path), "w") as f:
                    json.dump(result, indent=4, fp=f)

    # whether or not we establish a baseline, we want to write results to a temp dir
    log_path = pathlib.Path(__file__).parent / "results" / args.language / ".log"
    if not log_path.exists():
        log_path.mkdir(parents=True, exist_ok=True)

    for name, result in all_results.items():
        output_path = log_path / name[:-1]
        with open(str(output_path), "w") as f:
            json.dump(result, indent=4, fp=f)


def record_run_result(result: dict[str, Any], rule_ids: Set[str]) -> list[dict[str, Any]]:
    run_result = []
    total_score = 0

    for row in result["rows"]:
        score = calculate_overall_score(row)
        total_score += score
        rules = [rule["rule_ids"] for rule in json.loads(row["inputs.response"])["comments"]]
        rule_ids.update(*rules)

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


if __name__ == "__main__":
    settings = SettingsManager()

    parser = argparse.ArgumentParser(description="Run evals for APIview copilot.")
    parser.add_argument(
        "--language",
        "-l",
        type=str,
        default="python",
        help="The language to run evals for. Defaults to python.",
    )
    parser.add_argument(
        "--num-runs",
        "-n",
        type=int,
        default=1,
        help="The number of runs to perform, with the median of results kept. Defaults to 3.",
    )
    parser.add_argument(
        "--test-file",
        "-t",
        type=str,
        default="reviews.jsonl",
        help="Only run a particular jsonl test file, takes the name or path to the file. Defaults to all.",
    )
    args = parser.parse_args()

    custom_eval = CustomAPIViewEvaluator()
    rule_ids = set()

    tests_directory = pathlib.Path(__file__).parent / "tests" / args.language
    args.test_file = pathlib.Path(args.test_file).name

    all_results = {}
    for file in tests_directory.glob("*.jsonl"):
        if args.test_file != "all" and file.name != args.test_file:
            continue

        azure_ai_project = {
            "subscription_id": settings.get("EVALS_SUBSCRIPTION"),
            "resource_group_name": settings.get("EVALS_RG"),
            "project_name": settings.get("EVALS_PROJECT_NAME"),
        }
        if in_ci():
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
        for run in range(args.num_runs):
            print(f"Running evals {run + 1}/{args.num_runs} for {file.name}...")
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
                target=review_apiview,
                fail_on_evaluator_errors=False,
                azure_ai_project=azure_ai_project,
                **kwargs,
            )

            run_result = record_run_result(result, rule_ids)
            print(f"Average score for {file.name} run {run + 1}/{args.num_runs}: {run_result[-1]['average_score']:.2f}")
            run_results.append(run_result)

        # take the median run based on the average score
        median_result = sorted(run_results, key=lambda x: x[-1]["average_score"])[len(run_results) // 2]
        all_results[file.name] = median_result

    if not all_results:
        raise ValueError(f"No tests found for arguments: {args}")

    show_results(args, all_results)
    establish_baseline(args, all_results)
    calculate_coverage(args, rule_ids)
