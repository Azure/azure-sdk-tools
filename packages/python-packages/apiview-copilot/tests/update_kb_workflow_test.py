# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument

"""
Tests for UpdateKnowledgeBaseWorkflow to ensure summarize passes full results
to the prompt (not the GitHub-filtered version from the base class).
"""

from unittest.mock import patch

from src.mention._base import MentionWorkflow
from src.mention._update_kb_workflow import UpdateKnowledgeBaseWorkflow


def _make_kb_results():
    """Build a realistic execute_plan result dict with memory data."""
    return {
        "success": [
            {
                "id": "guideline-abc",
                "title": "Use async/await keywords",
                "related_memories": ["mem-123"],
            },
            {
                "id": "mem-123",
                "title": "Exception for async list operations",
                "content": "Do not use async keyword for async iterators.",
                "source": "mention_agent",
                "related_guidelines": ["guideline-abc"],
                "related_examples": [],
            },
        ],
        "failures": {},
    }


class TestUpdateKBSummarizePassesFullResults:
    """Verify that UpdateKnowledgeBaseWorkflow.summarize sends the full
    results dict (including 'success' and 'failures' keys) to the prompt,
    rather than the GitHub-property-filtered version from the base class."""

    def test_summarize_passes_success_and_failures_keys(self):
        """The prompt must receive the 'success' and 'failures' keys so it
        can extract the memory ID.  If this test fails, the summarize prompt
        will receive an empty dict and produce 'memory id: unknown'."""
        results = _make_kb_results()
        captured_inputs = {}

        def fake_run_prompt(*, folder, filename, inputs):
            captured_inputs.update(inputs)
            return "Thank you! (memory id: `mem-123`)"

        workflow = UpdateKnowledgeBaseWorkflow(
            language="python",
            code="",
            package_name="azure-test",
            trigger_comment="test comment",
            other_comments=[],
            reasoning="test",
        )

        with patch("src.mention._update_kb_workflow.run_prompt", side_effect=fake_run_prompt):
            workflow.summarize(results)

        # The prompt must receive the full results dict
        assert "results" in captured_inputs
        assert "success" in captured_inputs["results"]
        assert "failures" in captured_inputs["results"]
        assert len(captured_inputs["results"]["success"]) == 2

    def test_summarize_does_not_strip_to_github_properties(self):
        """Ensure the results are NOT filtered to GitHub-only properties
        (url, repository_url, title, created_at, body, action) which would
        discard all the memory/guideline data."""
        results = _make_kb_results()
        captured_inputs = {}

        def fake_run_prompt(*, folder, filename, inputs):
            captured_inputs.update(inputs)
            return "Thank you!"

        workflow = UpdateKnowledgeBaseWorkflow(
            language="python",
            code="",
            package_name="azure-test",
            trigger_comment="test comment",
            other_comments=[],
            reasoning="test",
        )

        with patch("src.mention._update_kb_workflow.run_prompt", side_effect=fake_run_prompt):
            workflow.summarize(results)

        # Verify critical fields survived (these would be lost with GitHub filtering)
        success_items = captured_inputs["results"]["success"]
        memory_item = next(item for item in success_items if item["id"] == "mem-123")
        assert "content" in memory_item
        assert "source" in memory_item
        assert "related_guidelines" in memory_item

    def test_base_class_summarize_would_lose_kb_data(self):
        """Prove that using the base class summarize directly would strip
        all the KB-specific data — this is the regression we are guarding against."""
        results = _make_kb_results()
        captured_inputs = {}

        def fake_run_prompt(*, folder, filename, inputs):
            captured_inputs.update(inputs)
            return "Thank you!"

        workflow = UpdateKnowledgeBaseWorkflow(
            language="python",
            code="",
            package_name="azure-test",
            trigger_comment="test comment",
            other_comments=[],
            reasoning="test",
        )

        with patch("src.mention._base.run_prompt", side_effect=fake_run_prompt):
            # Call the BASE class summarize directly
            MentionWorkflow.summarize(workflow, results)

        # The base class filters to GitHub properties only — all KB data is lost
        prompt_results = captured_inputs["results"]
        assert "success" not in prompt_results
        assert "failures" not in prompt_results
