"""Chat service.

Orchestrates the chat flow using the Azure AI Foundry SDK (azure-ai-projects).

Workflow (per the design diagram):
  1. Receive user message
  2. Get provisioned agent  ->  project_client.agents.get(agent_name, version)
  3. Agent exist?  ->  No: raise error
  4. Retrieve conversation_id from conversation store
  5. Get conversation  ->  openai_client.conversations.retrieve
  6. Conversation exist?  ->  No: openai_client.conversations.create
  7. Add message to conversation  ->  openai_client.conversations.items.create
  8. Call agent  ->  openai_client.responses.create
  9. Reply to user
"""

from __future__ import annotations

import logging

from config.app_config import get as cfg
from models.chat import ChatRequest, ChatResponse
from services.conversation_service import ConversationService
from utils.azure_ai_foundry import get_project_client

logger = logging.getLogger(__name__)

AGENT_NAME = cfg("AI_FOUNDRY_AGENT_NAME", "azure-sdk-qa-bot-chat-agent")

class ChatService:
    """Handles the full chat lifecycle using the AI Foundry SDK."""

    def __init__(self) -> None:
        self._conversation_service = ConversationService()

    async def chat(self, req: ChatRequest) -> ChatResponse:
        """Run the complete chat pipeline.

        Steps:
          1. Preprocess the user input.
          2. Get the hosted agent from AI Foundry.
          3. Resolve or create a conversation.
          4. Add the user message and call the hosted agent.
          5. Postprocess the hosted agent response.
        """
        preprocessed_message = self._preprocess(req)

        project_client = get_project_client()

        # --- Step 2: Get hosted agent ---
        agent = await self._get_agent(project_client)

        async with project_client.get_openai_client() as openai_client:
            # --- Step 3-4: Resolve conversation ---
            conversation_id = await self._resolve_conversation(
                openai_client, req
            )

            # --- Step 5: Add message to conversation ---
            await openai_client.conversations.items.create(
                conversation_id=conversation_id,
                items=[
                    {
                        "type": "message",
                        "role": "user",
                        "content": preprocessed_message,
                    }
                ],
            )

            # --- Step 6: Call hosted agent ---
            response = await openai_client.responses.create(
                conversation=conversation_id,
                extra_body={
                    "agent_reference": {
                        "name": agent.name,
                        "type": "agent_reference",
                    }
                },
            )

        return self._postprocess(req, response, conversation_id)

    # ------------------------------------------------------------------
    # Preprocessing
    # ------------------------------------------------------------------

    def _preprocess(self, req: ChatRequest) -> str:
        """Clean the user input before sending it to the agent.

        Preprocessing steps:
          - Decode HTML entities from Teams/Slack messages.
          - Normalize keywords (e.g. tsp -> typespec).
        """
        message = req.message

        # TODO: implement HTML decoding (mirrors Go PreprocessHTMLContent)
        # TODO: implement keyword normalization (mirrors Go PreprocessInput)

        return message

    # ------------------------------------------------------------------
    # Agent resolution
    # ------------------------------------------------------------------

    async def _get_agent(self, project_client):
        """Get the provisioned agent from AI Foundry.

        Calls ``project_client.agents.get(agent_name)`` and raises
        if the agent does not exist.
        """
        # TODO: implement agent caching to avoid repeated lookups
        agent = await project_client.agents.get(AGENT_NAME)
        if agent is None:
            raise RuntimeError(
                f"Agent '{AGENT_NAME}' not found in AI Foundry. "
                "Make sure the agent has been deployed."
            )
        logger.info("Using agent: name=%s", agent.name)
        return agent

    # ------------------------------------------------------------------
    # Conversation management
    # ------------------------------------------------------------------

    async def _resolve_conversation(self, openai_client, req: ChatRequest) -> str:
        """Resolve or create an AI Foundry conversation.

        1. Look up the AI Foundry conversation_id from the local conversation
              store (keyed by source conversation_id + source conversation_type).
        2. If found, verify it still exists via conversations.retrieve.
        3. If not found or expired, create a new one via conversations.create.
        """
        # Try to get existing conversation_id from local store
        stored_conversation_id = await self._conversation_service.get_agent_conversation_id(
            req.conversation_id,
            req.conversation_type,
        )

        if stored_conversation_id:
            try:
                await openai_client.conversations.retrieve(stored_conversation_id)
                return stored_conversation_id
            except Exception:
                logger.info(
                    "Stored conversation %s no longer exists, creating new one",
                    stored_conversation_id,
                )

        # Create new conversation in AI Foundry
        conversation = await openai_client.conversations.create()
        new_id = conversation.id

        # Persist the mapping in local store
        await self._conversation_service.save_agent_conversation_mapping(
            req.conversation_id,
            req.conversation_type,
            agent_conversation_id=new_id,
        )

        logger.info("Created new AI Foundry conversation: %s", new_id)
        return new_id

    # ------------------------------------------------------------------
    # Postprocessing
    # ------------------------------------------------------------------

    def _postprocess(
        self, req: ChatRequest, response, conversation_id: str
    ) -> ChatResponse:
        """Transform the agent response into a ChatResponse.

        Postprocessing steps:
          - Extract the answer text from the agent output.
          - Parse and filter references.
          - Classify the response intention.
        """
        # TODO: extract answer from agent response format
        # TODO: parse references from agent citations
        # TODO: filter references by relevance / source
        # TODO: determine response intention category

        answer = response.output_text if hasattr(response, "output_text") else str(response)

        return ChatResponse(
            answer=answer,
            conversation_id=conversation_id,
        )