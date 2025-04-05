import os
import json
import pathlib
import argparse
from typing import Set, Tuple, Any
import copy

import dotenv
from tabulate import tabulate
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator

dotenv.load_dotenv()

NUM_RUNS: int = 3


class CustomAPIViewEvaluator:

    def __init__(self):
        pass

    def _get_violation_matches(self, expected: dict[str, Any], actual: dict[str, Any]) -> Tuple[Set, Set, Set]:
        """Compare violations based on both line numbers and rule IDs."""
        exact_matches = set()
        rule_matches_wrong_line = set()
        line_matches_wrong_rule = set()

        violations_left = copy.deepcopy(actual["violations"])
        for expected_violation in expected["violations"]:
            e_line = expected_violation["line_no"]
            e_rules = frozenset(expected_violation["rule_ids"])

            for actual_violation in violations_left:
                a_line = actual_violation["line_no"]
                a_rules = frozenset(actual_violation["rule_ids"])

                rule_match = any(rule for rule in a_rules if rule in e_rules)
                if e_line == a_line and rule_match:
                    exact_matches.add((e_line, tuple(sorted(e_rules))))
                    # Remove the matched actual violation to avoid double counting
                    violations_left.remove(actual_violation)
                    break
                if rule_match:
                    if abs(e_line - a_line) <= 5:
                        # If the line numbers are close, consider it a match
                        rule_matches_wrong_line.add((tuple(sorted(e_rules)), e_line, a_line))
                elif e_line == a_line:
                    line_matches_wrong_rule.add((e_line, tuple(sorted(e_rules)), tuple(sorted(a_rules))))

        return exact_matches, rule_matches_wrong_line, line_matches_wrong_rule

    def __call__(self, *, response: str, query: str, language: str, output: str, **kwargs):
        expected = json.loads(response)
        actual = json.loads(output)

        exact_matches, rule_matches_wrong_line, line_matches_wrong_rule = self._get_violation_matches(expected, actual)

        review_eval = {
            "total_violations": len(expected["violations"]),
            "violations_found": len(actual["violations"]),
            "rule_matches_wrong_line": len(rule_matches_wrong_line),
            "line_matches_wrong_rule": len(line_matches_wrong_rule),
            "true_positives": len(exact_matches),
            "false_positives": len(actual["violations"]) - (len(exact_matches) + len(rule_matches_wrong_line)),
            "false_negatives": len(expected["violations"]) - (len(exact_matches) + len(rule_matches_wrong_line)),
            "percent_coverage": (len(exact_matches) / len(expected["violations"]) * 100) if expected["violations"] else 0,
            "wrong_line_details": list(rule_matches_wrong_line),
            "wrong_rule_details": list(line_matches_wrong_rule)
        }
        return review_eval


def review_apiview(query: str, language: str):
    from src._gpt_reviewer_openai import GptReviewer  # pylint: disable=import-error,no-name-in-module
    rg = GptReviewer()
    review = rg.get_response(query, language)
    return {"response": review.model_dump_json()}


def calculate_overall_score(row: dict[str, Any]) -> float:
    """Calculate weighted score based on various metrics.
    """
    weights = {
        'exact_match_weight': 0.6,     # Perfect match - right rule, right line
        'fuzzy_match_weight': 0.2,     # Fuzzy match - right rule, wrong line
        'false_positive_penalty': 0.3, # Penalty for incorrect violations
        'groundedness_weight': 0.15,   # Weight for staying grounded in guidelines
        'similarity_weight': 0.05      # Smaller weight for similarity in expected vs actual responses
    }

    exact_match_score = (row["outputs.custom_eval.true_positives"] /
                        row["outputs.custom_eval.total_violations"]
                        if row["outputs.custom_eval.total_violations"] > 0 else 1.0)

    # Only consider fuzzy matches if there are remaining unmatched violations
    remaining_violations = row["outputs.custom_eval.total_violations"] - row["outputs.custom_eval.true_positives"]
    rule_match_score = (row["outputs.custom_eval.rule_matches_wrong_line"] /
                       remaining_violations
                       if remaining_violations > 0 else 0.0)

    # Perfect match case - give full credit for exact + rule match weights
    if exact_match_score == 1.0:
        rule_match_score = 1.0

    false_positive_rate = (row["outputs.custom_eval.false_positives"] /
                          row["outputs.custom_eval.violations_found"]
                          if row["outputs.custom_eval.violations_found"] > 0 else 0.0)

    if row["outputs.custom_eval.total_violations"] == 0 and row["outputs.custom_eval.true_positives"] == 0:
        # test with no violations / no violations found should get credit for groundedness
        groundedness_normalized = 1.0
    else:
        groundedness_normalized = (row["outputs.groundedness.groundedness"] - 1) / 4
    similarity_normalized = (row["outputs.similarity.similarity"] - 1) / 4

    score = (
        weights['exact_match_weight'] * exact_match_score +
        weights['fuzzy_match_weight'] * rule_match_score -
        weights['false_positive_penalty'] * false_positive_rate +
        weights['groundedness_weight'] * groundedness_normalized +
        weights['similarity_weight'] * similarity_normalized
    )

    normalized_score = max(0, min(100, score * 100))
    return round(normalized_score)


def format_terminal_diff(new: float, old: float, format_str: str = ".1f", reverse: bool = False) -> str:
    """Format difference with ANSI colors for terminal output."""
    diff = new - old
    if diff > 0:
        if reverse:
            return f" (\033[31m+{diff:{format_str}}\033[0m)" # Red
        return f" (\033[32m+{diff:{format_str}}\033[0m)"  # Green
    elif diff < 0:
        if reverse:
            return f" (\033[32m{diff:{format_str}}\033[0m)"  # Green
        return f" (\033[31m{diff:{format_str}}\033[0m)"   # Red
    return f" ({diff:{format_str}})"


def create_table(baseline_results: dict[str, Any], eval_results: list[dict[str, Any]], file_name: str) -> None:
    headers = ["Test Case", "Score", "Violations found", "Exact matches (TP)", "Fuzzy matches", "False positives (FP)", "Groundedness", "Similarity"]
    terminal_rows = []

    for result in eval_results[:-1]:  # Skip summary object
        testcase = result['testcase']
        score = result['overall_score']
        exact = result['true_positives']
        rule = result['rule_matches_wrong_line']
        fp = result['false_positives']
        ground = result['groundedness']
        sim = result['similarity']
        violations_found = f"{result['violations_found']} / {result['total_violations']}"

        terminal_row = [testcase]
        if testcase in baseline_results:
            base = baseline_results[testcase]
            terminal_row.extend([
                f"{score:.1f}{format_terminal_diff(score, base['overall_score'])}",
                violations_found,
                f"{exact}{format_terminal_diff(exact, base['true_positives'], 'd')}",
                f"{rule}{format_terminal_diff(rule, base['rule_matches_wrong_line'], 'd')}",
                f"{fp}{format_terminal_diff(fp, base['false_positives'], 'd', reverse=True)}",
                f"{ground:.1f}{format_terminal_diff(ground, base['groundedness'])}",
                f"{sim:.1f}{format_terminal_diff(sim, base['similarity'])}"
            ])
        else:
            values = [
                f"{score:.1f}",
                violations_found,
                f"{exact}",
                str(rule),
                str(fp),
                f"{ground:.1f}",
                f"{sim:.1f}"
            ]
            terminal_row.extend(values)

        terminal_rows.append(terminal_row)

    print("====================================================")
    print(f"\n\n✨ {file_name} results:\n")
    print(tabulate(terminal_rows, headers, tablefmt="simple"))
    if baseline_results:
        print(f"\n{file_name} average score: {eval_results[-1]['average_score']} {format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}\n\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run evals.")
    parser.add_argument(
        "--language",
        type=str,
        default="python",
        help="The language to run evals for.",
    )
    parser.add_argument(
        "--n",
        type=int,
        default=NUM_RUNS,
        help="The number of runs to perform, with the median of results kept.",
    )

    parser.add_argument(
        "--test-file",
        type=str,
        default="all",
        help="Only run a particular jsonl test file.",
    )
    args = parser.parse_args()

    # needed for AI-assisted evaluation
    model_config: dict[str, str] = {
        "azure_endpoint": os.environ["AZURE_OPENAI_ENDPOINT"],
        "api_key": os.environ["AZURE_OPENAI_API_KEY"],
        # for best results, this should always be a different model from the one we are evaluating
        "azure_deployment": "gpt-4o",
        "api_version": "2025-01-01-preview",
    }

    custom_eval = CustomAPIViewEvaluator()
    groundedness = GroundednessEvaluator(model_config=model_config)
    similarity_eval = SimilarityEvaluator(model_config=model_config)

    rule_ids = set()

    tests_directory = pathlib.Path(__file__).parent / "tests" / args.language

    all_results = {}
    for file in tests_directory.glob("*.jsonl"):
        if args.test_file != "all" and file.name != args.test_file:
            continue

        path = file
        eval_results = []
        for run in range(args.n):
            print(f"Running evals {run + 1}/{args.n} for {file.name}...")
            result = evaluate(
                data=str(path),
                evaluators={
                    "custom_eval": custom_eval,
                    "similarity": similarity_eval,
                    "groundedness": groundedness,
                },
                evaluator_config={
                    "similarity": {
                        "column_mapping": {
                            "response": "${target.response}",
                            "query": "${data.query}",
                            "language": "${data.language}",
                            "ground_truth": "${data.response}",
                        },
                    },
                    "groundedness": {
                        "column_mapping": {
                            "response": "${target.response}",
                            "query": "${data.query}",
                            "language": "${data.language}",
                            "context": "${data.context}",
                        },
                    },
                    "custom_eval": {
                        "column_mapping": {
                            "response": "${data.response}",
                            "query": "${data.query}",
                            "language": "${data.language}",
                            "output": "${target.response}",
                        },
                    },
                },
                target=review_apiview,
                # TODO we can send data to our foundry project for history / more graphical insights
                # azure_ai_project={
                #     "subscription_id": os.environ["AZURE_SUBSCRIPTION_ID"],
                #     "resource_group_name": os.environ["AZURE_FOUNDRY_RESOURCE_GROUP"],
                #     "project_name": os.environ["AZURE_FOUNDRY_PROJECT_NAME"],
                # }
            )

            results_list = []
            total_score = 0

            for row in result["rows"]:
                score = calculate_overall_score(row)
                total_score += score
                rules = [rule["rule_ids"] for rule in json.loads(row["inputs.response"])["violations"]]
                rule_ids.update(*rules)

                results_list.append({
                    "testcase": row["inputs.testcase"],
                    "expected": json.loads(row["inputs.response"]),
                    "actual": json.loads(row["outputs.response"]),
                    "total_violations": row["outputs.custom_eval.total_violations"],
                    "violations_found": row["outputs.custom_eval.violations_found"],
                    "true_positives": row["outputs.custom_eval.true_positives"],
                    "false_positives": row["outputs.custom_eval.false_positives"],
                    "false_negatives": row["outputs.custom_eval.false_negatives"],
                    "percent_coverage": row["outputs.custom_eval.percent_coverage"],
                    "rule_matches_wrong_line": row["outputs.custom_eval.rule_matches_wrong_line"],
                    "wrong_rule_details": row["outputs.custom_eval.wrong_rule_details"],
                    "line_matches_wrong_rule": row["outputs.custom_eval.line_matches_wrong_rule"],
                    "wrong_line_details": row["outputs.custom_eval.wrong_line_details"],
                    "similarity": row["outputs.similarity.similarity"],
                    "groundedness": row["outputs.groundedness.groundedness"],
                    "groundedness_reason": row["outputs.groundedness.groundedness_reason"],
                    "overall_score": score,
                })

            average_score = total_score / len(result["rows"])
            results_list.append({
                "average_score": average_score,
                "total_evals": len(result["rows"])
            })
            print(f"Average score for {file.name} run {run + 1}/{args.n}: {average_score:.2f}")
            eval_results.append(results_list)

        # take the median run based on the average score
        eval_result = sorted(eval_results, key=lambda x: x[-1]["average_score"])[len(eval_results) // 2]

        all_results[file.name] = eval_result

    for name, test_results in all_results.items():
        baseline_results = {}
        baseline_path = pathlib.Path(__file__).parent / "results" / args.language / name[:-1]
        
        if baseline_path.exists():
            with open(baseline_path, 'r') as f:
                baseline_data = json.load(f)
                for result in baseline_data[:-1]:  # Skip summary
                    baseline_results[result['testcase']] = result
                baseline_results["average_score"] = baseline_data[-1]["average_score"]

        create_table(baseline_results, test_results, name)

    establish_baseline = input("\nDo you want to establish this as the new baseline? (y/n): ")
    if establish_baseline.lower() == "y":
        for name, result in all_results.items():
            output_path = pathlib.Path(__file__).parent / "results" / args.language / name[:-1]
            with open(str(output_path), "w") as f:
                json.dump(result, indent=4, fp=f)

    if args.test_file == "all":
        # only update coverage if all tests are run
        output_path = pathlib.Path(__file__).parent / "results" / args.language / "coverage.json"
        guidelines_path = pathlib.Path(__file__).parent.parent / "guidelines" / args.language / "guidelines.json"
        with open(str(guidelines_path), "r") as f:
            guidelines = json.load(f)
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
                    indent=4
                )
            )

        print(f"\nTest coverage for {args.language}: {len(rule_ids) / len(guideline_rule_ids) * 100:.2f}%")
