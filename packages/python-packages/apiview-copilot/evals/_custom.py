import copy
import json
import os
import pathlib
import sys
from typing import Any, Set, Tuple

import prompty
import prompty.azure_beta
import yaml

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from azure.ai.evaluation import GroundednessEvaluator, SimilarityEvaluator
from src._settings import SettingsManager
from src._utils import get_prompt_path


class CustomAPIViewEvaluator:
    """Evaluator for comparing expected and actual APIView comments."""

    def __init__(self):
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
