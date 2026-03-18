"""Feedback workflow service.

Processes user feedback as a plain Python workflow (no LLM agent).
Saves feedback records to Azure Blob Storage and creates GitHub issues
for negative feedback (bad cases).
"""

from __future__ import annotations

from typing import Optional

from models.feedback import FeedbackRequest, FeedbackResponse


class FeedbackService:
    """Workflow that persists feedback and creates GitHub issues for bad cases."""

    async def process(self, req: FeedbackRequest) -> FeedbackResponse:
        """Run the full feedback workflow.

        Steps:
          1. Save the feedback record to Azure Blob Storage.
          2. If reaction is "bad", create a GitHub issue.
        """
        result = FeedbackResponse()

        await self._save_feedback(req)
        result.saved = True

        if req.reaction == "bad":
            result.issue_url = await self._create_github_issue(req)

        return result

    async def _save_feedback(self, req: FeedbackRequest) -> None:
        """Save feedback record to Azure Blob Storage.

        Appends the entry to a monthly feedback file (feedback_YYYY_MM.xlsx)
        in the configured storage container.
        """
        # TODO: implement with Azure Storage client from utils.azure_storage
        pass

    async def _create_github_issue(self, req: FeedbackRequest) -> Optional[str]:
        """Create a GitHub issue for a bad feedback case.

        Returns the issue URL on success, or None on failure.
        """
        # TODO: implement with GitHub API
        return None
