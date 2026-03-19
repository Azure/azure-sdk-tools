# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import json
from abc import ABC, abstractmethod

from src._prompt_runner import run_prompt


class MentionWorkflow(ABC):
    """
    Base class for mention workflows. Enforces the template method pattern.
    Provides a shared create_plan implementation; subclasses override prompty_filename.
    """

    prompty_filename: str = None  # Subclasses must set this
    summarize_prompt_file: str = None  # Subclasses must set this

    def __init__(
        self,
        *,
        language: str,
        code: str,
        package_name: str,
        trigger_comment: dict,
        other_comments: dict,
        reasoning: str,
    ):
        self.language = language
        self.code = code
        self.package_name = package_name
        self.trigger_comment = trigger_comment
        self.other_comments = other_comments
        self.reasoning = reasoning
        self.plan = None
        self.results = None

    def run(self):
        self.plan = self.create_plan()
        self.results = self.execute_plan(self.plan)
        return self.summarize(self.results)

    def create_plan(self):
        if not self.prompty_filename:
            raise NotImplementedError("Subclasses must set prompty_filename or override create_plan.")
        inputs = {
            "language": self.language,
            "code": self.code,
            "package_name": self.package_name,
            "trigger_comment": self.trigger_comment,
            "other_comments": self.other_comments,
            "reasoning": self.reasoning,
        }
        raw_results = run_prompt(folder="mention", filename=self.prompty_filename, inputs=inputs)
        try:
            results = json.loads(raw_results)
            return results
        except Exception:
            return raw_results

    @abstractmethod
    def execute_plan(self, plan: dict):
        pass

    def summarize(self, results: dict):
        if not self.summarize_prompt_file:
            raise NotImplementedError("Subclasses must set summarize_prompt_file or override summarize().")
        properties_to_keep = ["url", "repository_url", "title", "created_at", "body", "action"]
        filtered_results = (
            [{k: item.get(k) for k in properties_to_keep} for item in results]
            if isinstance(results, list)
            else {k: results.get(k) for k in properties_to_keep}
        )
        inputs = {"results": filtered_results}
        try:
            summary = run_prompt(folder="mention", filename=self.summarize_prompt_file, inputs=inputs)
            return summary
        except Exception as e:
            print(f"Error summarizing results: {e}")
            return "Error summarizing results."
