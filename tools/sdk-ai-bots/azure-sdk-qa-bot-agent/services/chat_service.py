"""Chat service backed by Azure AI Foundry hosted agents."""

from __future__ import annotations

import json
import logging

from config.app_config import get as cfg
from config.tenant_config import (
    get_tenant_scope_description,
    get_tenant_sources_display,
    load_tenant_qa_guideline,
)
from models.chat import (
    AgentReferenceType,
    ChatRequest,
    ChatResponse,
    ConversationItem,
    ConversationItemType,
    Role,
)
from services.conversation_service import ConversationService
from tools import TOOL_REGISTRY
from tools.skills import get_skill_to_tenant_map
from utils.azure_ai_foundry import get_openai_client, get_project_client
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import AgentDetails
from openai import AsyncOpenAI
from openai.types.responses import Response as OpenAIResponse
from openai.types.responses import (
    ResponseFunctionToolCall,
    ResponseOutputMessage,
    ResponseOutputItem,
)
from openai.types.responses.response_input_item_param import ResponseInputItemParam
from config.tenant_config import TenantID

logger = logging.getLogger(__name__)


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

        conversation_items.append(
            ConversationItem(
                role=req.message.role,
                content=req.message.content,
                user_id=req.message.user_id,
                user_name=req.message.user_name,
            ).model_dump(mode="json", exclude_none=True)
        )

        response = await openai_client.responses.create(
            input=conversation_items,
            conversation=agent_conversation_id,
            extra_body={
                "agent_reference": {
                    "name": agent.name,
                    "type": AgentReferenceType.agent_reference.value,
                }
            },
        )

        return self._postprocess(req, response, agent_conversation_id)

    async def _get_agent(self, project_client: AIProjectClient) -> AgentDetails:
        """Load hosted-agent definition from Foundry."""
        agent_name = cfg("AI_FOUNDRY_AGENT_NAME", "azure-sdk-qa-bot-chat-agent")
        agent = await project_client.agents.get(agent_name)
        if agent is None:
            raise RuntimeError(
                f"Agent '{agent_name}' not found in AI Foundry. "
                "Make sure the agent has been deployed."
            )
        logger.info("Using agent: name=%s", agent.name)
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

    def _build_tenant_system_message(self, tenant_id: TenantID) -> str:
        """Inject tenant context so the agent knows the current domain."""
        parts: list[str] = [f"[tenant_context] original_tenant_id={tenant_id}"]

        scope_desc = get_tenant_scope_description(tenant_id)
        if scope_desc:
            parts.append(f"\n[tenant_scope]\n{scope_desc}")

        guideline = load_tenant_qa_guideline(tenant_id)
        if guideline:
            parts.append(f"\n[tenant_guideline]\n{guideline}")

        sources = get_tenant_sources_display(tenant_id)
        if sources:
            src_lines = [f"- {s['name']}: {s['description']}" for s in sources]
            parts.append("\n[tenant_knowledge_sources]\n" + "\n".join(src_lines))

        return "\n".join(parts)

    def _postprocess(
        self, req: ChatRequest, response: OpenAIResponse, agent_conversation_id: str
    ) -> ChatResponse:
        """Map hosted-agent response to `ChatResponse`."""
        tool_results = self._extract_tool_results(response.output)
        search = tool_results.get("search_knowledge_base")
        references = search.results if search else []
        tenant = self._extract_routed_tenant(response.output)
        answer = response.output_text or ""
        resp = ChatResponse(
            id=response.id,
            answer=answer,  # Result exists if answer is non-empty
            references=references if references else None,
        )
        if req.tenant_id != tenant:
            resp.route_tenant = tenant
        return resp

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
