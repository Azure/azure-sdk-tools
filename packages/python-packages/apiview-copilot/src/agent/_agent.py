from contextlib import asynccontextmanager
from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from semantic_kernel.agents import AzureAIAgent, AzureAIAgentSettings, RunPollingOptions
from datetime import timedelta

from .plugins import SearchPlugin

load_dotenv(override=True)


@asynccontextmanager
async def get_main_agent():
    ai_agent_settings = AzureAIAgentSettings()
    async with DefaultAzureCredential() as credentials:
        async with AzureAIAgent.create_client(
            credential=credentials, endpoint=ai_agent_settings.endpoint, api_version=ai_agent_settings.api_version
        ) as client:
            agent_definition = await client.agents.create_agent(
                name="ArchAgentMainAgent",
                description="An agent that processed requests and passes work to other agents.",
                model=ai_agent_settings.model_deployment_name,
                instructions="Your job is to receive a request from the user, determine their intent, and pass the request to the appropriate agent for processing. You will then return the response from that agent to the user.",
            )
            agent = AzureAIAgent(
                client=client,
                definition=agent_definition,
                plugins=[SearchPlugin()],
                polling_options=RunPollingOptions(run_polling_interval=timedelta(seconds=1)),
            )
            yield agent
