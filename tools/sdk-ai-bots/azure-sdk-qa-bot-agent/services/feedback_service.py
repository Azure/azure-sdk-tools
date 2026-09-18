"""Feedback workflow service.

Processes user feedback as a plain Python workflow (no LLM agent).
Saves each feedback submission as a document in Azure Cosmos DB.
"""

from __future__ import annotations

import logging
from datetime import datetime, timezone
from uuid import uuid4

from models.feedback import FeedbackRequest, FeedbackResponse, Reaction
from models.qa_record import QARecord
from utils.azure_cosmosdb import get_feedback_container, requeue_qa_record_for_analysis

logger = logging.getLogger(__name__)


class FeedbackService:
    """Persist feedback and reopen completed no-issue QA records for reanalysis."""

    async def process(self, req: FeedbackRequest) -> FeedbackResponse:
        """Save feedback, then reopen a completed no-issue record if negative."""
        result = FeedbackResponse()

        await self._save_feedback(req)
        result.saved = True

        if req.reaction == Reaction.bad:
            if req.conversation_id and req.conversation_type:
                record_id = QARecord.build_id(req.conversation_type, req.conversation_id)
                if await requeue_qa_record_for_analysis(
                    record_id=record_id, tenant_id=req.tenant_id,
                ):
                    logger.info("Reopened QA record %s after negative feedback", record_id)

        return result

    async def _save_feedback(self, req: FeedbackRequest) -> None:
        """Create a feedback document in the tenant's partition."""
        document = req.model_dump(mode="json")
        document["id"] = str(uuid4())
        document["created_at"] = datetime.now(timezone.utc).isoformat()

        container = await get_feedback_container()
        await container.create_item(body=document)
        logger.info("Saved feedback %s to Cosmos DB", document["id"])

