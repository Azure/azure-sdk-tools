import copy
import json
import os
import pathlib
import sys
from abc import ABC, abstractmethod
from typing import Any, Set

import prompty
import prompty.azure_beta
import yaml

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from pathlib import Path

from azure.ai.evaluation import GroundednessEvaluator, SimilarityEvaluator
from evals._util import ensure_json_obj
from src._settings import SettingsManager


def _review_apiview(testcase: str, query: str, language: str):
    """APIView review target function for evals framework."""
    from src._apiview_reviewer import ApiViewReview

    reviewer = ApiViewReview(target=query, language=language, base=None)
    review = reviewer.run()
    reviewer.close()
    return {"actual": review.model_dump_json()}


def _mention_summarize_workflow(testcase: str, results: dict):
    prompty_path = Path(__file__).parent.parent / "prompts" / "mention" / "summarize_github_actions.prompty"
    prompty_kwargs = {
        "testcase": testcase,
        "results": results,
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}


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


def _filter_existing_comment(testcase: str, response: str, language: str, existing: str, comment: str):
    prompty_path = Path(__file__).parent.parent / "prompts" / "api_review" / "filter_existing_comment.prompty"
    prompty_kwargs = {
        "testcase": testcase,
        "response": response,
        "language": language,
        "existing": existing,
        "comment": comment,
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}


def _deduplicate_parser_issue(
    testcase: str, response: str, language: str, package_name: str, code: str, issue_context: str, existing_issues: str
):
    prompty_path = Path(__file__).parent.parent / "prompts" / "mention" / "deduplicate_parser_issue.prompty"
    prompty_kwargs = {
        "testcase": testcase,
        "response": response,
        "language": language,
        "package_name": package_name,
        "code": code,
        "issue_context": issue_context,
        "existing_issues": existing_issues,
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}


def _deduplicate_guidelines_issue(
    testcase: str, response: str, language: str, package_name: str, code: str, issue_context: str, existing_issues: str
):
    prompty_path = Path(__file__).parent.parent / "prompts" / "mention" / "deduplicate_guidelines_issue.prompty"
    prompty_kwargs = {
        "testcase": testcase,
        "response": response,
        "language": language,
        "package_name": package_name,
        "code": code,
        "issue_context": issue_context,
        "existing_issues": existing_issues,
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}


class BaseEvaluator(ABC):
    """Base class for custom evaluators in the evals framework.

    This abstract base class defines the minimal interface that all custom evaluators
    must implement to be compatible with the evals runner framework.
    """

    @abstractmethod
    def __call__(self, **kwargs) -> dict[str, Any]:
        """Evaluate the given inputs and return evaluation metrics.

        Args:
            **kwargs: Arbitrary keyword arguments containing evaluation inputs.
                     The specific arguments depend on the evaluator implementation.

        Returns:
            dict[str, Any]: A dictionary containing evaluation metrics and results.
                           The structure depends on the evaluator implementation.
        """
        pass

    @property
    @abstractmethod
    def evaluator_config(self) -> dict[str, Any]:
        """Return the evaluator configuration for the Azure AI evaluation framework.

        Returns:
            dict[str, Any]: Configuration dictionary containing column mappings
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


class PromptyEvaluator(BaseEvaluator):
    def __init__(self, config, jsonl_file=None):
        self.config = config
        self._jsonl_file = jsonl_file

        # Optionally, you can set up a model config for SimilarityEvaluator if needed
        settings = SettingsManager()
        self._model_config = {
            "azure_endpoint": settings.get("OPENAI_ENDPOINT"),
            "api_key": settings.get("OPENAI_API_KEY"),
            "azure_deployment": "gpt-4.1",
            "api_version": "2025-03-01-preview",
        }

    def __call__(self, *, response: str, actual: str, testcase: str, **kwargs):
        # Use ensure_json_obj for both response and actual
        expected_data = ensure_json_obj(response)
        actual_data = ensure_json_obj(actual)

        # Safely extract comparison values
        try:
            expected_value = (
                expected_data.get("action", "").strip()
                if isinstance(expected_data, dict)
                else str(expected_data).strip()
            )
        except Exception:
            expected_value = str(expected_data).strip()

        try:
            actual_value = (
                actual_data.get("action", "").strip() if isinstance(actual_data, dict) else str(actual_data).strip()
            )
        except Exception:
            actual_value = str(actual_data).strip()

        is_correct = expected_value == actual_value

        expected_rationale = expected_data.get("rationale", "").strip() if isinstance(expected_data, dict) else ""
        actual_rationale = actual_data.get("rationale", "").strip() if isinstance(actual_data, dict) else ""

        if expected_rationale or actual_rationale:
            similarity_result = SimilarityEvaluator(model_config=self._model_config)(
                response=actual_rationale,
                query="",  # not used for rationale
                ground_truth=expected_rationale,
            )
            rationale_similarity = similarity_result.get("similarity", 0.0) / 5.0
        else:
            rationale_similarity = 1.0 if expected_rationale == actual_rationale else 0.0

        return {
            "success": is_correct,
            "actual": actual,
            "expected": response,
            "testcase": testcase,
            "score": rationale_similarity * 100 if is_correct else 0,
            "expected_action": expected_value,
            "actual_action": actual_value,
        }

    def process_results(self, raw_results: list, guideline_ids: set = None) -> dict:
        """Process prompt workflow results."""
        all_results = {}

        for run_result_data in raw_results:
            for file_name, result in run_result_data.items():
                if file_name not in all_results:
                    all_results[file_name] = []

                run_summary = {
                    "sum_score": 0.0,
                    "max_score": 0.0,
                    "test_results": [],
                }

                # Process each test case in this run
                for row in result.get("rows", []):
                    test_result = {
                        "testcase": row.get("inputs.testcase", "unknown"),
                        "correct": row.get("outputs.metrics.success", False),
                        "expected_action": row.get("outputs.metrics.expected_action", ""),
                        "actual_action": row.get("outputs.metrics.actual_action", ""),
                        "score": row.get("outputs.metrics.score", 0),
                    }

                    run_summary["test_results"].append(test_result)
                    run_summary["max_score"] += 100

                    if test_result["correct"]:
                        run_summary["sum_score"] += test_result["score"]

                run_summary["accuracy"] = (
                    (run_summary["sum_score"] / run_summary["max_score"]) * 100 if run_summary["max_score"] > 0 else 0
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
        """Display prompt workflow results."""
        for file_name, results in processed_results.items():
            accuracy = results["accuracy"]

            print("====================================================")
            print(f"\n\n✨ {file_name} results:\n")
            print(f"Overall Score: ({accuracy:.0f}%)\n")

            # Show each test result
            print("== TEST RESULTS ==")
            for test in results["test_results"]:
                status = "✅" if test["correct"] else "❌"
                print(f"  {status} {test['score']}% - {test['testcase']} ")

    def _discover_jsonl_fields(self) -> set[str]:
        """Peek at JSONL to see what fields are available."""

    @property
    def evaluator_config(self) -> dict[str, Any]:
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
            "filter_existing_comment": _filter_existing_comment,
            "deduplicate_parser_issue": _deduplicate_parser_issue,
            "deduplicate_guidelines_issue": _deduplicate_guidelines_issue,
            # Add more workflows as needed
        }

        workflow_name = self.config.name
        if workflow_name not in workflow_targets:
            raise ValueError(f"No target function defined for workflow: {workflow_name}")

        return workflow_targets[workflow_name]


class PromptySummaryEvaluator(PromptyEvaluator):
    """Evaluator for summarization prompts using Prompty."""

    def __call__(self, *, response: str, actual: str, testcase: str, **kwargs):
        expected_summary = response.strip()
        actual_summary = actual.strip()
        similarity_result = SimilarityEvaluator(model_config=self._model_config)(
            response=actual_summary,
            query=expected_summary,
            ground_truth=expected_summary,
        )
        similarity = similarity_result.get("similarity", 0.0) / 5.0 * 100
        return {
            "actual": actual,
            "expected": response,
            "testcase": testcase,
            "score": similarity,
            "success": similarity > 70,
        }

    @property
    def target_function(self) -> callable:
        workflow_targets = {
            "mention_summarize": _mention_summarize_workflow,
            # Add more workflows as needed
        }

        workflow_name = self.config.name
        if workflow_name not in workflow_targets:
            raise ValueError(f"No target function defined for workflow: {workflow_name}")

        return workflow_targets[workflow_name]

    def process_results(self, raw_results: list, guideline_ids: set = None) -> dict:
        """Process prompt workflow results."""
        all_results = {}

        for run_result_data in raw_results:
            for file_name, result in run_result_data.items():
                if file_name not in all_results:
                    all_results[file_name] = []

                run_summary = {
                    "sum_score": 0.0,
                    "max_score": 0.0,
                    "test_results": [],
                }

                # Process each test case in this run
                for row in result.get("rows", []):
                    score = row.get("outputs.metrics.score", 0)
                    test_result = {
                        "testcase": row.get("inputs.testcase", "unknown"),
                        "success": True if score > 70 else False,
                        "score": score,
                    }

                    run_summary["test_results"].append(test_result)
                    run_summary["max_score"] += 100
                    run_summary["sum_score"] += test_result["score"]

                run_summary["accuracy"] = (
                    (run_summary["sum_score"] / run_summary["max_score"]) * 100 if run_summary["max_score"] > 0 else 0
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
        """Display prompt workflow results."""
        for file_name, results in processed_results.items():
            accuracy = results["accuracy"]

            print("====================================================")
            print(f"\n\n✨ {file_name} results:\n")
            print(f"Overall Score: ({accuracy:.0f}%)\n")

            # Show each test result
            print("== TEST RESULTS ==")
            for test in results["test_results"]:
                status = "✅" if test["success"] else "❌"
                print(f"  {status} {test['score']}% - {test['testcase']} ")
