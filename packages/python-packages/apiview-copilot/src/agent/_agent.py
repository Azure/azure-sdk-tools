from contextlib import asynccontextmanager
from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from semantic_kernel.agents import AzureAIAgent, AzureAIAgentSettings, RunPollingOptions, AzureAIAgentThread

from datetime import timedelta
import logging
import os

from src._models import ExistingComment

from .plugins import SearchPlugin, UtilityPlugin, ApiReviewPlugin, DatabasePlugin
from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion

load_dotenv(override=True)


async def invoke_agent(*, agent, user_input, thread_id=None, messages=None):
    messages = messages or []
    # Only append user_input if not already the last message
    if not messages or messages[-1] != user_input:
        messages.append(user_input)
    # Only use thread_id if it is a valid Azure thread id (starts with 'thread')
    if thread_id and isinstance(thread_id, str) and thread_id.startswith("thread"):
        thread = AzureAIAgentThread(client=agent.client, thread_id=thread_id)
    else:
        thread = AzureAIAgentThread(client=agent.client)
    response = await agent.get_response(messages=messages, thread=thread)
    thread_id_out = getattr(thread, "id", None) or thread_id
    return str(response), thread_id_out, messages


@asynccontextmanager
async def get_main_agent():
    ai_agent_settings = AzureAIAgentSettings(
        endpoint=os.getenv("AZURE_AI_AGENT_ENDPOINT"),
        model_deployment_name=os.getenv("AZURE_AI_AGENT_MODEL_DEPLOYMENT_NAME"),
        api_version=os.getenv("AZURE_AI_AGENT_API_VERSION"),
    )
    ai_instructions = """
Your job is to receive a request from the user, determine their intent, and pass the request to the
appropriate agent or agents for processing. You will then return the response from that agent to the user.
If there's no agent that can handle the request, you will respond with a message indicating that you cannot
process the request. You will also handle any errors that occur during the processing of the request and return an appropriate
error message to the user.
"""

    async with DefaultAzureCredential() as credentials:
        async with AzureAIAgent.create_client(
            credential=credentials, endpoint=ai_agent_settings.endpoint, api_version=ai_agent_settings.api_version
        ) as client:
            agent_definition = await client.agents.create_agent(
                name="ArchAgentMainAgent",
                description="An agent that processed requests and passes work to other agents.",
                model=ai_agent_settings.model_deployment_name,
                instructions=ai_instructions,
            )
            agent = AzureAIAgent(
                client=client,
                definition=agent_definition,
                plugins=[SearchPlugin(), UtilityPlugin(), ApiReviewPlugin(), DatabasePlugin()],
                polling_options=RunPollingOptions(run_polling_interval=timedelta(seconds=1)),
                kernel=create_kernel(),
            )
            yield agent


def create_kernel() -> Kernel:
    base_url = os.getenv("AZURE_OPENAI_ENDPOINT")
    deployment_name = os.getenv("AZURE_OPENAI_DEPLOYMENT")
    api_key = os.getenv("AZURE_OPENAI_API_KEY")
    if not base_url:
        raise RuntimeError("AZURE_OPENAI_ENDPOINT environment variable is required.")
    if not deployment_name:
        raise RuntimeError("AZURE_OPENAI_DEPLOYMENT environment variable is required.")
    if not api_key:
        raise RuntimeError("AZURE_OPENAI_API_KEY environment variable is required.")
    logging.info(f"Using Azure OpenAI at {base_url} with deployment {deployment_name}")
    kernel = Kernel(
        plugins={},  # Register your plugins here if needed
        services={
            "AzureChatCompletion": AzureChatCompletion(
                base_url=base_url,
                deployment_name=deployment_name,
                api_key=api_key,
            )
        },
    )
    return kernel


@asynccontextmanager
async def get_mention_agent(*, comments: ExistingComment, auth: str):
    # TODO: handle auth properly

    ai_agent_settings = AzureAIAgentSettings(
        endpoint=os.getenv("AZURE_AI_AGENT_ENDPOINT"),
        model_deployment_name=os.getenv("AZURE_AI_AGENT_MODEL_DEPLOYMENT_NAME"),
        api_version=os.getenv("AZURE_AI_AGENT_API_VERSION"),
    )
    comments.sort(key=lambda x: x.created_on)  # Sort comments by create_on time
    comment_lines = [f"{comment.created_by}: {comment.comment_text}" for comment in comments]
    ai_instructions = f"""
You are an agent that processes @mention requests from APIView.
**CONTEXT**
{comment_lines.join('\n\n')}

**INSTRUCTIONS**
Update the knowledge base to incorporate the feedback provided in the @mention request.
"""
    async with DefaultAzureCredential() as credentials:
        async with AzureAIAgent.create_client(
            credential=credentials, endpoint=ai_agent_settings.endpoint, api_version=ai_agent_settings.api_version
        ) as client:
            agent_definition = await client.agents.create_agent(
                name="MentionAgent",
                description="Handles @mention requests from APIView.",
                model=ai_agent_settings.model_deployment_name,
                instructions=ai_instructions,
            )
            agent = AzureAIAgent(
                client=client,
                definition=agent_definition,
                plugins=[SearchPlugin(), DatabasePlugin()],
                polling_options=RunPollingOptions(run_polling_interval=timedelta(seconds=1)),
                kernel=create_kernel(),
            )
            yield agent
