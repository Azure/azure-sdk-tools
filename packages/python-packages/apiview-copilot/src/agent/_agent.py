from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from semantic_kernel.agents import AzureAIAgent, AzureAIAgentSettings, RunPollingOptions
from datetime import timedelta
from .plugins import ChunkingPlugin

load_dotenv(override=True)


async def get_review_agent() -> AzureAIAgent:
    ai_agent_settings = AzureAIAgentSettings()
    async with (
        DefaultAzureCredential() as credentials,
        AzureAIAgent.create_client(
            credential=credentials, endpoint=ai_agent_settings.endpoint, api_version=ai_agent_settings.api_version
        ) as client,
    ):
        agent_definition = await client.agents.create_agent(
            name="ApiReviewAgent",
            description="An agent for reviewing API code.",
            model=ai_agent_settings.model_deployment_name,
            instructions="Review the provided API code and provide feedback.",
        )
        agent = AzureAIAgent(
            client=client,
            definition=agent_definition,
            plugins=[ChunkingPlugin()],
            polling_options=RunPollingOptions(run_polling_interval=timedelta(seconds=1)),
        )
        return agent
