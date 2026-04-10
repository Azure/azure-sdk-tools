# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from src._database_manager import DatabaseManager
from src._memory_utils import check_for_duplicate_memory, merge_and_save_memory
from src._prompt_runner import run_prompt

from ._base import MentionWorkflow


class UpdateKnowledgeBaseWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_memory.prompty"
    summarize_prompt_file = "summarize_actions.prompty"

    def _prepare_memory_args(self, plan: dict):
        """Extract raw_memory, guideline_ids, and raw_examples from the plan."""
        guideline_ids = plan.get("guideline_ids", [])
        raw_memory = plan.get("memory", {})
        raw_memory["source"] = "mention_agent"
        if self.source_comment_id:
            raw_memory["source_comment_id"] = self.source_comment_id
        raw_memory["service"] = None
        raw_examples = raw_memory.pop("related_examples", [])
        return raw_memory, guideline_ids, raw_examples

    def check_for_duplicates(self, plan: dict):
        """Check for duplicate memories and merge if found."""
        raw_memory, guideline_ids, raw_examples = self._prepare_memory_args(plan)
        merge_result = check_for_duplicate_memory(
            raw_memory=raw_memory, guideline_ids=guideline_ids
        )
        if merge_result is not None:
            return merge_and_save_memory(
                merge_result=merge_result,
                raw_memory=raw_memory,
                guideline_ids=guideline_ids,
                raw_examples=raw_examples,
                example_service=None,
            )
        # Stash prepared args so execute_plan doesn't re-extract them
        self._prepared_memory_args = (raw_memory, guideline_ids, raw_examples)
        return None

    def execute_plan(self, plan: dict):
        raw_memory, guideline_ids, raw_examples = self._prepared_memory_args
        db_manager = DatabaseManager.get_instance()
        return db_manager.save_memory_with_links(
            raw_memory=raw_memory,
            guideline_ids=guideline_ids,
            raw_examples=raw_examples,
            example_service=None,
        )

    def summarize(self, results: dict):
        """Pass full results to the summarize prompt (not GitHub-filtered)."""
        inputs = {"results": results}
        try:
            summary = run_prompt(folder="mention", filename=self.summarize_prompt_file, inputs=inputs)
            return summary
        except Exception as e:
            print(f"Error summarizing results: {e}")
            return "Error summarizing results."
