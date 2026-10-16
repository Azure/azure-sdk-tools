"""Hosted Teams agent: scheduled Q&A summarization of stored channel threads.

Importing the threads is not hosted here. Backfill never calls a model, so the
backend server exposes it as an endpoint and this agent only reads what that
import already stored.
"""

import asyncio
import json
import logging
import sys
from pathlib import Path

from agent_framework import Agent, AgentResponse, AgentResponseUpdate, BaseAgent, Content, Message
from agent_framework_foundry import FoundryChatOptions
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.ai.agentserver.core.tasks import set_resilient_tasks_enabled
from dotenv import load_dotenv
from starlette.responses import JSONResponse

PROJECT = Path(__file__).resolve().parents[2]
if str(PROJECT) not in sys.path:
    sys.path.insert(0, str(PROJECT))

from config import app_config
from services.conversation_service import validate_channels
from services.teams_operations import operation_input, parse_operation
from services.teams_qa_summary_service import summarize_configured_channels
from services.teams_thread_processor import TeamsThreadProcessor
from tools.web_tools import WebTools
from utils.azure_ai_foundry import close_clients, get_agent_client
from utils.azure_cosmosdb import close_cosmos_client
from utils.azure_credential import close_credential
from utils.tool_security import ToolOutputSecurityMiddleware


logger = logging.getLogger(__name__)


class BackgroundCollectionRequests:
    """Use SDK-managed background Responses so Routine delivery does not wait for a full run."""

    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http" or scope["method"] != "POST" or scope["path"] != "/responses":
            return await self.app(scope, receive, send)
        body = bytearray()
        while True:
            event = await receive()
            if event["type"] == "http.disconnect":
                return
            body.extend(event.get("body", b""))
            if len(body) > 65536:
                return await JSONResponse({"error": "Collection request too large"}, status_code=413)(scope, receive, send)
            if not event.get("more_body"):
                break
        try:
            payload = json.loads(body)
            if not isinstance(payload, dict):
                raise ValueError("Expected a JSON object")
            request = parse_operation(payload.get("input"))
        except ValueError as error:
            return await JSONResponse({"error": str(error)}, status_code=400)(scope, receive, send)
        payload.update({"background": True, "stream": False, "store": True,
                        "input": operation_input(request)})
        encoded = json.dumps(payload).encode("utf-8")
        forwarded = dict(scope)
        forwarded["headers"] = [(key, value) for key, value in scope["headers"] if key.lower() != b"content-length"]
        forwarded["headers"].append((b"content-length", str(len(encoded)).encode("ascii")))
        delivered = False

        async def receive_body():
            nonlocal delivered
            if not delivered:
                delivered = True
                return {"type": "http.request", "body": encoded, "more_body": False}
            return await receive()

        return await self.app(forwarded, receive_body, send)


def create_server(agent):
    set_resilient_tasks_enabled(True)
    server = ResponsesHostServer(agent)
    server.add_middleware(BackgroundCollectionRequests)
    return server


class TeamsCollectionAgent(BaseAgent):
    def __init__(self, summarize):
        super().__init__(name="azure-sdk-teams-collection-agent")
        self._summarize = summarize
        self._lock = asyncio.Lock()

    def run(self, messages=None, *, stream=False, **kwargs):
        request = parse_operation(self._text(messages))
        if stream:
            return self._stream(request)
        return self._run(request)

    @staticmethod
    def _text(messages):
        if isinstance(messages, str):
            return messages
        if isinstance(messages, Message):
            return messages.text
        if isinstance(messages, list):
            return " ".join(message.text for message in messages if isinstance(message, Message))
        return ""

    async def _run(self, request):
        if self._lock.locked():
            raise RuntimeError("A Teams summarization run is already active in this agent instance.")
        async with self._lock:
            try:
                result = await self._summarize(request)
            except Exception:
                logger.exception("Teams %s operation failed.", request["operation"])
                raise RuntimeError(
                    f"Teams {request['operation']} operation failed; inspect the agent logs."
                ) from None
        return AgentResponse(messages=[Message("assistant", [json.dumps(result)])], agent_id=self.id)

    async def _stream(self, request):
        response = await self._run(request)
        yield AgentResponseUpdate(
            role="assistant", contents=[Content.from_text(response.text)], agent_id=self.id,
            message_id="collection-summary", finish_reason="stop",
        )


def create_thread_processor(config):
    processing = config.get("processing")
    if not isinstance(processing, dict):
        raise ValueError("Teams collection requires processing configuration.")
    instructions = (Path(__file__).parent / "instructions.md").read_text(
        encoding="utf-8"
    ).strip()
    client = get_agent_client()
    client.function_invocation_configuration["max_iterations"] = 5
    web_tools = WebTools()
    options: FoundryChatOptions[None] = {
        "max_tool_calls": 6,
        "include": ["web_search_call.action.sources"],
    }
    agent = Agent(
        client,
        name="teams-thread-qa-processor",
        instructions=instructions,
        tools=[
            web_tools.web_fetch,
            client.get_web_search_tool(search_context_size="medium"),
        ],
        middleware=[ToolOutputSecurityMiddleware()],
        default_options=options,
    )
    processor = TeamsThreadProcessor(agent, processing["version"])
    for channel in config["channels"]:
        processor.validate_channel(channel)
    return processor


async def main():
    load_dotenv(PROJECT / ".env", override=False)
    await app_config.init()
    config = json.loads((PROJECT / "config/teams_collection_config.json").read_text(encoding="utf-8"))
    validate_channels(config["channels"])
    processor = create_thread_processor(config)
    agent = TeamsCollectionAgent(
        lambda request: summarize_configured_channels(
            config, processor, request.get("channelId"),
        ),
    )
    try:
        await create_server(agent).run_async()
    finally:
        await close_clients()
        await close_cosmos_client()
        await close_credential()


if __name__ == "__main__":
    logging.basicConfig(level=logging.WARNING)
    for name in ("httpx", "httpcore", "azure.core.pipeline.policies.http_logging_policy",
                 "azure.cosmos._cosmos_http_logging_policy"):
        logging.getLogger(name).setLevel(logging.WARNING)
    asyncio.run(main())
