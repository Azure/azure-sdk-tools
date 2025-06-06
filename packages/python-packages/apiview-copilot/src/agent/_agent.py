from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from semantic_kernel.agents import AzureAIAgent, AzureAIAgentSettings
from .plugins import ChunkingPlugin

load_dotenv(override=True)

credentials = DefaultAzureCredential()


async def get_review_agent() -> AzureAIAgent:
    async with (AzureAIAgent.create_client(credential=credentials) as client,):
        agent_definition = await client.agents.create_agent(
            name="ApiReviewAgent",
            description="An agent for reviewing API code.",
            model=AzureAIAgentSettings().model_deployment_name,
            instructions="Review the provided API code and provide feedback.",
        )

        agent = AzureAIAgent(
            client=client,
            definition=agent_definition,
            plugins=[ChunkingPlugin()],
        )
        return agent
