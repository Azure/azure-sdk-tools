"""Knowledge retrieval service for the Azure SDK QA Bot."""

from __future__ import annotations

import logging
from models.knowledge_retrieve import KnowledgeRetrieveResponse, KnowledgeRetrieveRequest
from tools.knowledge_tools import KnowledgeTools

logger = logging.getLogger(__name__)


class KnowledgeService:
    """Service for retrieving knowledge via the search_knowledge_base tool."""

    def __init__(self) -> None:
        """Initialize the knowledge service with the KnowledgeTools instance."""
        self._knowledge_tools = KnowledgeTools()

    async def retrieve(self, req: KnowledgeRetrieveRequest) -> KnowledgeRetrieveResponse:
        """Retrieve knowledge for a chat request.
        
        Calls search_knowledge_base with queries derived from the user message
        and returns structured knowledge.
        
        Args:
            req: KnowledgeRetrieveRequest containing the user message and tenant context
            
        Returns:
            KnowledgeRetrieveResponse with search results and references
        """
        # Extract search query from the user message
        user_message = req.message.content.strip()
        
        # Build primary queries for search
        # First query: use the full message as problem-phrased query
        queries = [user_message]
        
        # Optionally add more specific searches if the message is long
        # Extract first sentence as a secondary query
        if len(user_message) > 100:
            first_sentence = user_message.split('.')[0].strip()
            if first_sentence and first_sentence != user_message:
                queries.append(first_sentence)
        
        logger.info(
            "Calling search_knowledge_base: tenant=%s, queries=%s",
            req.tenant_id.value,
            queries,
        )
        
        try:
            # Call the search_knowledge_base tool
            search_result = await self._knowledge_tools.search_knowledge_base(
                queries=queries,
                tenant_id=req.tenant_id.value,
                sources=None,  # Use default tenant-configured sources
                service_type=req.service_type,  # No explicit service type filtering
                search_mode=req.search_mode or "quick",  # Default to quick search
            )
            
            logger.info(
                "search_knowledge_base returned %d results",
                len(search_result.results) if search_result.results else 0,
            )

            has_result = True if len(search_result.results) else False
            return KnowledgeRetrieveResponse(has_result=has_result, knowledgeList=search_result.results)
            
        except Exception as e:
            logger.error("search_knowledge_base failed: %s", str(e), exc_info=True)
            raise
