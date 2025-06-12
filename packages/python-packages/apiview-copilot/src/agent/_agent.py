from contextlib import asynccontextmanager
from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from semantic_kernel.agents import AzureAIAgent, AzureAIAgentSettings, RunPollingOptions
from datetime import timedelta
import logging
import os

from .plugins import SearchPlugin, UtilityPlugin, ApiReviewPlugin, DatabasePlugin
from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion

load_dotenv(override=True)


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
process the request.
exit
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
                plugins=[SearchPlugin(), UtilityPlugin(), ApiReviewPlugin(), DatabasePlugin()],
                polling_options=RunPollingOptions(run_polling_interval=timedelta(seconds=1)),
                kernel=create_kernel(),
            )
            yield agent


def create_kernel() -> Kernel:
    base_url = os.getenv("AZURE_OPENAI_ENDPOINT")
    deployment_name = os.getenv("AZURE_OPENAI_DEPLOYMENT", "MCNOODLE")
    logging.info(f"Using Azure OpenAI at {base_url} with deployment {deployment_name}")
    kernel = Kernel(
        plugins={},  # Register your plugins here if needed
        services={
            "AzureChatCompletion": AzureChatCompletion(
                base_url=os.getenv("AZURE_OPENAI_ENDPOINT"),
                deployment_name=os.getenv("AZURE_OPENAI_DEPLOYMENT"),
                api_key=os.getenv("AZURE_OPENAI_API_KEY"),
            )
        },
    )
    return kernel
