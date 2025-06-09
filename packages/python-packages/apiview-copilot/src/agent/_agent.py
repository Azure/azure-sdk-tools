from contextlib import asynccontextmanager
from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from semantic_kernel.agents import AzureAIAgent, AzureAIAgentSettings, RunPollingOptions
from datetime import timedelta

from .plugins import SearchPlugin, UtilityPlugin, ApiReviewPlugin

load_dotenv(override=True)


@asynccontextmanager
async def get_main_agent():
    ai_agent_settings = AzureAIAgentSettings()
    ai_instructions = """
Your job is to receive a request from the user, determine their intent, and pass the request to the
appropriate agent or agents for processing. You will then return the response from that agent to the user.
If there's no agent that can handle the request, you will respond with a message indicating that you cannot
process the request.

You will also handle any errors that occur during the processing of the request and return an appropriate
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
                plugins=[SearchPlugin(), UtilityPlugin(), ApiReviewPlugin()],
                polling_options=RunPollingOptions(run_polling_interval=timedelta(seconds=1)),
            )
            yield agent
