"""Feedback workflow service.

Processes user feedback as a plain Python workflow (no LLM agent).
Saves each feedback submission as a document in Azure Cosmos DB.
"""

from __future__ import annotations

import logging
from datetime import datetime, timezone
from typing import Optional
from uuid import uuid4

from models.feedback import FeedbackRequest, FeedbackResponse, Reaction
from utils.azure_cosmosdb import get_feedback_container

logger = logging.getLogger(__name__)


class FeedbackService:
    """Workflow that persists feedback and creates GitHub issues for bad cases."""

    async def process(self, req: FeedbackRequest) -> FeedbackResponse:
        """Run the full feedback workflow.

        Steps:
          1. Save the feedback record to Azure Cosmos DB.
          2. If reaction is "bad", create a GitHub issue.
        """
        result = FeedbackResponse()

        await self._save_feedback(req)
        result.saved = True

        if req.reaction == Reaction.bad:
            result.issue_url = await self._create_github_issue(req)

        return result

    async def _save_feedback(self, req: FeedbackRequest) -> None:
        """Create a feedback document in the tenant's partition."""
        document = req.model_dump(mode="json")
        document["id"] = str(uuid4())
        document["created_at"] = datetime.now(timezone.utc).isoformat()

        container = await get_feedback_container()
        await container.create_item(body=document)
        logger.info("Saved feedback %s to Cosmos DB", document["id"])

    async def _create_github_issue(self, req: FeedbackRequest) -> Optional[str]:
        """Create a GitHub issue for a bad feedback case.

        Returns the issue URL on success, or None on failure.
        """
        # TODO: implement with GitHub API
        return None
