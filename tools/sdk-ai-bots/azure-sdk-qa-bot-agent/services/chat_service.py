"""Chat service backed by Azure AI Foundry hosted agents."""

from __future__ import annotations

import asyncio
from datetime import datetime, timezone
import json
import logging
import re
from urllib.parse import urlparse

from config.app_config import get as cfg
from config.tenant_config import (
    get_tenant_scope_description,
)
from models.chat import (
    AdditionalInfo,
    AdditionalInfoType,
    AgentReferenceType,
    ChatRequest,
    ChatResponse,
    ConversationItem,
    Role,
)
from models.conversation import ConversationMessage
from models.knowledge import Reference
from services.conversation_service import ConversationService
from tools import TOOL_REGISTRY
from skills.tenant_skills import get_skill_to_tenant_map
from utils.azure_ai_foundry import get_openai_client, get_project_client
from utils.teams_image import get_image_data_uri
from utils.text_util import preprocess_message
from utils.azure_memory_store import sanitize_scope
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import AgentDetails
from openai import AsyncOpenAI
from openai.types.responses import Response as OpenAIResponse
from openai.types.responses import (
    ResponseFunctionToolCall,
    ResponseOutputItem,
    ResponseOutputMessage,
)
from openai.types.responses.response_input_item_param import ResponseInputItemParam
from config.tenant_config import TenantID

logger = logging.getLogger(__name__)

# -- Polling constants for empty-response retry loop ----------------------
POLL_MAX_RETRIES = 5
POLL_RETRY_DELAY_SECS = 3.0

COMPACT_THRESHOLD = 100000
"""Token count at which conversation history is compacted."""

_CITATION_RE = re.compile(r"[^\w\s]*cite[^\w\s]*turn\d+\S*")


class ChatService:
    """Coordinates conversation state, hosted-agent invocation, and response mapping."""

    def __init__(self) -> None:
        self._conversation_service = ConversationService()

    async def chat(self, req: ChatRequest) -> ChatResponse:
        """Process one chat turn and return API response shape."""
        project_client = get_project_client()

        agent = await self._get_agent(project_client)
        openai_client = get_openai_client()

        agent_conversation_id, is_new = await self._resolve_conversation(
            openai_client, req
        )
        conversation_items: list[ResponseInputItemParam] = []
        if is_new:
            tenant_system_msg = self._build_tenant_system_message(req.tenant_id)
            conversation_items.append(
                ConversationItem(
                    role=Role.System,
                    content=tenant_system_msg,
                ).model_dump(mode="json", exclude_none=True)
            )

        memory_scope = self._resolve_memory_scope(req)
        if memory_scope:
            memory_scope_msg = self._build_memory_scope_message(memory_scope)
            conversation_items.append(
                ConversationItem(
                    role=Role.System,
                    content=memory_scope_msg,
                ).model_dump(mode="json", exclude_none=True)
            )
            logger.info(
                "Memory scope injected into conversation: scope=%s, conversation=%s",
                memory_scope,
                agent_conversation_id,
            )
        else:
            logger.info(
                "No memory scope injected: user_id missing, conversation=%s",
                agent_conversation_id,
            )

        conversation_items.append(
            ConversationItem(
                role=req.message.role,
                content=preprocess_message(req.message.content),
                user_id=req.message.user_id,
                user_name=req.message.user_name,
            ).model_dump(mode="json", exclude_none=True)
        )

        # Process additional info (images, links, text) from the frontend.
        image_items = await self._build_image_items(req.additional_infos or [])
        conversation_items.extend(image_items)

        # Streaming is broken for hosted agents — text is lost entirely.
        # See https://github.com/Azure/azure-sdk-for-python/issues/45282
        # and https://github.com/Azure/azure-sdk-for-python/issues/46015
        response: OpenAIResponse = await openai_client.responses.create(
            input=conversation_items,
            conversation=agent_conversation_id,
            store=True,
            stream=False,
            extra_body={
                "agent_reference": {
                    "name": agent.name,
                    "type": AgentReferenceType.agent_reference.value,
                }
            },
        )

        # Poll if response completed with empty text (Foundry persistence delay).
        if response.status == "completed" and not response.output_text:
            response = await self._poll_response_text(openai_client, response)

        if response.status != "completed":
            logger.warning(
                "Agent response not completed: id=%s, status=%s, error=%s, "
                "incomplete_details=%s, usage=%s, conversation=%s",
                response.id,
                response.status,
                response.error,
                response.incomplete_details,
                response.usage,
                agent_conversation_id,
            )
        else:
            logger.info(
                "Agent response completed: id=%s, output_items=%d, output_text_len=%d, "
                "conversation=%s",
                response.id,
                len(response.output) if response.output else 0,
                len(response.output_text) if response.output_text else 0,
                agent_conversation_id,
            )

        chat_response = self._postprocess(req, response, agent_conversation_id)
        asyncio.create_task(
            self._save_bot_answer_to_conversation(
                req, response.id, chat_response.answer
            )
        )
        return chat_response

    @staticmethod
    async def _poll_response_text(
        openai_client: AsyncOpenAI,
        response: OpenAIResponse,
        max_retries: int = POLL_MAX_RETRIES,
        retry_delay: float = POLL_RETRY_DELAY_SECS,
    ) -> OpenAIResponse:
        """Poll ``responses.retrieve()`` until output_text appears."""
        for attempt in range(1, max_retries + 1):
            await asyncio.sleep(retry_delay)
            try:
                refreshed = await openai_client.responses.retrieve(response.id)
                if refreshed.output_text:
                    logger.info(
                        "Poll retrieved text on attempt %d/%d: response=%s, "
                        "text_len=%d",
                        attempt,
                        max_retries,
                        response.id,
                        len(refreshed.output_text),
                    )
                    return refreshed
                logger.info(
                    "Poll attempt %d/%d: still no text, response=%s",
                    attempt,
                    max_retries,
                    response.id,
                )
            except Exception:
                logger.warning(
                    "Poll attempt %d/%d failed: response=%s",
                    attempt,
                    max_retries,
                    response.id,
                    exc_info=True,
                )
        logger.warning(
            "Poll exhausted %d retries without text: response=%s",
            max_retries,
            response.id,
        )
        return response

    async def _save_bot_answer_to_conversation(
        self,
        req: ChatRequest,
        response_id: str,
        answer: str,
    ) -> None:
        """Persist the final bot answer so intention uses the real reply, not placeholders."""
        if not req.conversation_id or not req.conversation_type:
            return

        content = answer.strip()
        if not content:
            return

        bot_message = ConversationMessage(
            id=f"bot-{response_id}",
            tenant_id=req.tenant_id.value,
            sender_role=Role.System,
            sender_id="azure-sdk-qa-bot",
            sender_name="Azure SDK Q&A Bot",
            content=content,
            created_at=datetime.now(timezone.utc),
            conversation_id=req.conversation_id,
            conversation_type=req.conversation_type,
        )

        try:
            await self._conversation_service.save_conversation(bot_message)
        except Exception:
            logger.warning(
                "Failed to persist bot answer for conversation=%s",
                req.conversation_id,
                exc_info=True,
            )

    async def _get_agent(self, project_client: AIProjectClient) -> AgentDetails:
        """Load hosted-agent definition from Foundry."""
        agent_name = cfg("AI_FOUNDRY_AGENT_NAME", "azure-sdk-qa-bot-chat-agent")
        agent = await project_client.agents.get(agent_name)
        if agent is None:
            raise RuntimeError(
                f"Agent '{agent_name}' not found in AI Foundry. "
                "Make sure the agent has been deployed."
            )
        logger.info("Using agent: name=%s, versions=%s", agent.name, agent.versions)
        return agent

    async def _resolve_conversation(
        self, openai_client: AsyncOpenAI, req: ChatRequest
    ) -> tuple[str, bool]:
        """Get an existing conversation id or create a new conversation."""
        stored_conversation_id = (
            await self._conversation_service.get_agent_conversation_id(
                req.conversation_id,
                req.conversation_type,
            )
        )

        if stored_conversation_id:
            try:
                await openai_client.conversations.retrieve(stored_conversation_id)
                return stored_conversation_id, False
            except Exception:
                logger.info(
                    "Stored conversation %s no longer exists, creating new one",
                    stored_conversation_id,
                )

        conversation = await openai_client.conversations.create()
        new_id = conversation.id

        await self._conversation_service.save_agent_conversation_mapping(
            req.conversation_id,
            req.conversation_type,
            agent_conversation_id=new_id,
        )

        logger.info("Created new AI Foundry conversation: %s", new_id)
        return new_id, True

    @staticmethod
    async def _build_image_items(
        infos: list[AdditionalInfo],
    ) -> list[ResponseInputItemParam]:
        """Convert image additional_infos into Responses API input items."""
        items: list[ResponseInputItemParam] = []
        for info in infos:
            if info.type != AdditionalInfoType.Image or not info.link:
                continue
            try:
                data_uri = await get_image_data_uri(info.link)
            except Exception:
                logger.warning(
                    "Failed to fetch Teams image: %s", info.link, exc_info=True
                )
                continue
            items.append(
                {
                    "type": "message",
                    "role": "user",
                    "content": [
                        {
                            "type": "input_image",
                            "image_url": data_uri,
                            "detail": "auto",
                        },
                    ],
                }
            )
        return items

    def _build_tenant_system_message(self, tenant_id: TenantID) -> str:
        """Inject tenant context so the agent knows the current domain."""
        parts: list[str] = [f"[tenant_context] original_tenant_id={tenant_id.value}"]

        scope_desc = get_tenant_scope_description(tenant_id)
        if scope_desc:
            parts.append(f"\n[tenant_scope]\n{scope_desc}")

        return "\n".join(parts)

    def _resolve_memory_scope(self, req: ChatRequest) -> str | None:
        """Derive user memory scope from user_id. Returns None if no user_id."""
        user_id = getattr(req.message, "user_id", None)
        if user_id and user_id.strip():
            scope = sanitize_scope(f"user_{user_id.strip()}")
            logger.info("Memory scope resolved: user_id=%s -> scope=%s", user_id, scope)
            return scope
        logger.info("Memory scope not resolved: no user_id in request message")
        return None

    @staticmethod
    def _build_memory_scope_message(memory_scope: str) -> str:
        return f"[memory_scope] value={memory_scope}"

    def _postprocess(
        self, req: ChatRequest, response: OpenAIResponse, agent_conversation_id: str
    ) -> ChatResponse:
        """Map hosted-agent response to `ChatResponse`."""
        tool_results = self._extract_tool_results(response.output)
        search = tool_results.get("search_knowledge_base")
        tool_references = search.results if search else []
        tenant = self._extract_routed_tenant(response.output)

        output_text = response.output_text or ""
        if not output_text:
            output_text = (
                "Sorry, something went wrong and I couldn't generate a response. "
                "Please send your message again to retry."
            )
            logger.error(
                "Empty output_text for response %s (status=%s), returning error message",
                response.id,
                response.status,
            )

        # Strip model citation artifacts (e.g. "citeturn0search0").
        output_text = _CITATION_RE.sub("", output_text)

        # Extract structured references from the agent's markdown output
        # and strip the references section from the answer text.
        answer, references = self._extract_references_from_text(
            output_text, tool_references
        )

        # Build full_context from search tool results when requested
        full_context = None
        if req.with_full_context and tool_references:
            full_context = json.dumps(
                [r.model_dump(mode="json") for r in tool_references]
            )

        resp = ChatResponse(
            id=response.id,
            answer=answer,
            references=references if references else None,
            full_context=full_context,
            agent_conversation_id=agent_conversation_id,
        )
        if req.tenant_id != tenant:
            resp.route_tenant = tenant
        return resp

    @staticmethod
    def _extract_references_from_text(
        text: str, tool_references: list[Reference] | None = None
    ) -> tuple[str, list[Reference]]:
        """Parse the **References** section from agent output text.

        Returns ``(answer_without_references, extracted_references)``.
        The extracted references are enriched with ``source`` and ``content``
        from *tool_references* when a matching link or title is found,
        preserving compatibility with the old backend response shape.
        """
        # Locate the **References** header
        header_match = re.search(r"\n*\*\*References\*\*\s*\n", text)
        if not header_match:
            return text, []

        answer = text[: header_match.start()].rstrip()
        refs_block = text[header_match.end() :]

        # Build lookups from the tool results for enrichment
        link_lookup: dict[str, Reference] = {}
        path_lookup: dict[str, Reference] = {}
        title_lookup: dict[str, Reference] = {}
        if tool_references:
            for ref in tool_references:
                if ref.link:
                    link_lookup[ref.link] = ref
                    path = urlparse(ref.link).path.rstrip("/")
                    if path:
                        path_lookup[path] = ref
                if ref.title:
                    title_lookup[ref.title] = ref

        def _match_reference(link: str, title: str) -> Reference | None:
            """Try exact link, URL path, then title matching."""
            matched = link_lookup.get(link)
            if matched:
                return matched
            matched = title_lookup.get(title)
            if matched:
                return matched
            path = urlparse(link).path.rstrip("/")
            if path:
                matched = path_lookup.get(path)
                if matched:
                    return matched
            return None

        # Extract markdown links: - [title](link)
        extracted: list[Reference] = []
        for m in re.finditer(r"-\s*\[([^\]]+)\]\(([^)]+)\)", refs_block):
            title = m.group(1).strip()
            link = m.group(2)
            matched = _match_reference(link, title)
            source = matched.source if matched else ""
            content = matched.content if matched else ""
            # When the title from the knowledge base includes source info
            # (e.g. "topic | source"), prefer that over the agent's title.
            if matched and matched.title and matched.source:
                title = matched.title
            extracted.append(
                Reference(
                    title=title,
                    source=source,
                    link=link,
                    content=content,
                )
            )

        return answer, extracted

    @staticmethod
    def _unwrap_json(raw_output) -> str | None:
        """Unwrap a tool output to the innermost JSON string.

        The hosted-agent output is typically double-serialised:
        ``'["{\\"key\\": \\"value\\"}"]'`` → ``'{"key": "value"}'``.
        This peels off outer layers until it reaches the JSON object string
        that Pydantic's ``model_validate_json`` can parse directly.
        """
        value = raw_output
        for _ in range(4):
            if isinstance(value, str):
                try:
                    value = json.loads(value)
                except (json.JSONDecodeError, TypeError):
                    return None
                continue
            if isinstance(value, list) and len(value) == 1:
                value = value[0]
                continue
            break
        # If we ended up with a dict, serialise it back so
        # model_validate_json can consume it.
        if isinstance(value, dict):
            return json.dumps(value)
        return None

    def _extract_tool_results(
        self, items: list[ResponseOutputItem]
    ) -> dict[str, object]:
        """Decode tool outputs using TOOL_REGISTRY models.

        Returns a dict mapping tool name to its decoded Pydantic model instance.
        """
        results: dict[str, object] = {}

        if not items:
            return results

        # Build mapping from call_id to tool name
        call_id_to_name: dict[str, str] = {}
        for item in items:
            if isinstance(item, ResponseFunctionToolCall):
                if item.call_id and item.name:
                    call_id_to_name[item.call_id] = item.name

        # Decode each tool output using its registered response model
        for item in items:
            if not isinstance(item, ResponseOutputMessage):
                continue
            call_id = item.model_extra.get("call_id", None)
            output = item.model_extra.get("output", None)
            tool_name = call_id_to_name.get(call_id, "")
            if not tool_name or not output:
                continue

            response_model = TOOL_REGISTRY.get(tool_name)
            if not response_model:
                continue

            try:
                json_str = self._unwrap_json(output)
                if json_str:
                    results[tool_name] = response_model.model_validate_json(json_str)
            except Exception as e:
                logger.warning("Failed to decode tool output for %s: %s", tool_name, e)

        return results

    @staticmethod
    def _extract_routed_tenant(items: list[ResponseOutputItem]) -> TenantID | None:
        """Detect the routed tenant from load_skill calls.

        When the agent calls ``load_skill("<skill-name>")``, map the skill
        name back to a tenant ID using the reverse skill→tenant map.
        """
        if not items:
            return None

        skill_to_tenant = get_skill_to_tenant_map()

        for item in items:
            if isinstance(item, ResponseFunctionToolCall) and item.name == "load_skill":
                try:
                    args = (
                        json.loads(item.arguments)
                        if isinstance(item.arguments, str)
                        else item.arguments
                    )
                    skill_name = args.get("skill_name", "")
                    tenant_id = skill_to_tenant.get(skill_name)
                    if tenant_id:
                        logger.info(
                            "Routed via skill %s → tenant %s", skill_name, tenant_id
                        )
                        return tenant_id
                except (json.JSONDecodeError, AttributeError):
                    pass
        return None
