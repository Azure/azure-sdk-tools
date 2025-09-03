# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for managing APIView Copilot agents.
"""

import logging
from contextlib import AsyncExitStack, asynccontextmanager
from datetime import timedelta

from azure.identity.aio import DefaultAzureCredential
from semantic_kernel import Kernel

# pylint: disable=no-name-in-module
from semantic_kernel.agents import (
    AzureAIAgent,
    AzureAIAgentSettings,
    AzureAIAgentThread,
    RunPollingOptions,
)
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion
from src._settings import SettingsManager

from .plugins import (
    ApiReviewPlugin,
    SearchPlugin,
    UtilityPlugin,
    get_create_agent,
    get_delete_agent,
    get_link_agent,
    get_retrieve_agent,
)


def create_kernel() -> Kernel:
    """Creates a Kernel instance configured for Azure OpenAI."""
    settings = SettingsManager()
    base_url = settings.get("OPENAI_ENDPOINT")
    deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")
    api_key = settings.get("OPENAI_API_KEY")
    logging.info("Using Azure OpenAI at %s with deployment %s", base_url, deployment_name)
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


async def invoke_agent(*, agent, user_input, thread_id=None, messages=None):
    """Invoke an agent with the provided user input and thread ID."""
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


def _get_agent_settings() -> AzureAIAgentSettings:
    """Retrieve the Azure AI Agent settings from the configuration."""
    settings = SettingsManager()
    return AzureAIAgentSettings(
        endpoint=settings.get("FOUNDRY_ENDPOINT"),
        model_deployment_name=settings.get("FOUNDRY_KERNEL_MODEL"),
        api_version=settings.get("FOUNDRY_API_VERSION"),
    )


@asynccontextmanager
async def get_main_agent():
    """Create and yield the main APIView Copilot agent."""
    kernel = create_kernel()
    ai_agent_settings = _get_agent_settings()
    ai_instructions = """
Your job is to receive a request from the user, determine their intent, and pass the request to the
appropriate agent or agents for processing. You will then return the response from that agent to the user.
If there's no agent that can handle the request, you will respond with a message indicating that you cannot
process the request. You will also handle any errors that occur during the processing of the request and return
an appropriate error message to the user.
"""

    async with AsyncExitStack() as stack:
        credentials = await stack.enter_async_context(DefaultAzureCredential())
        client = await stack.enter_async_context(
            AzureAIAgent.create_client(
                credential=credentials, endpoint=ai_agent_settings.endpoint, api_version=ai_agent_settings.api_version
            )
        )
        delete_agent = await stack.enter_async_context(get_delete_agent())
        create_agent = await stack.enter_async_context(get_create_agent())
        retrieve_agent = await stack.enter_async_context(get_retrieve_agent())
        link_agent = await stack.enter_async_context(get_link_agent())

        agent_definition = await client.agents.create_agent(
            name="ArchAgentMainAgent",
            description="An agent that processed requests and passes work to other agents.",
            model=ai_agent_settings.model_deployment_name,
            instructions=ai_instructions,
        )
        agent = AzureAIAgent(
            client=client,
            definition=agent_definition,
            plugins=[
                SearchPlugin(),
                UtilityPlugin(),
                ApiReviewPlugin(),
                delete_agent,
                create_agent,
                retrieve_agent,
                link_agent,
            ],
            polling_options=RunPollingOptions(run_polling_interval=timedelta(seconds=1)),
            kernel=kernel,
        )
        yield agent
