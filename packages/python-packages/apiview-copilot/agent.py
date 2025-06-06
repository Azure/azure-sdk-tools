from dotenv import load_dotenv
import asyncio

from azure.identity.aio import DefaultAzureCredential
from semantic_kernel.agents import AzureAIAgent, AzureAIAgentSettings, AzureAIAgentThread, RunPollingOptions
from datetime import timedelta
from src.agent.plugins import ChunkingPlugin

load_dotenv(override=True)


async def chat():
    ai_agent_settings = AzureAIAgentSettings(model_deployment_name="gpt-4.1")
    async with DefaultAzureCredential() as credentials:
        async with AzureAIAgent.create_client(
            credential=credentials, endpoint=ai_agent_settings.endpoint, api_version=ai_agent_settings.api_version
        ) as client:
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
            print("Interactive API Review Agent Chat. Type 'exit' to quit.")
            user_inputs = []
            thread = AzureAIAgentThread(client=client)
            try:
                while True:
                    user_input = input("You: ")
                    if user_input.strip().lower() in {"exit", "quit"}:
                        print("Exiting chat.")
                        break
                    user_inputs.append(user_input)
                    response = await agent.get_response(messages=user_inputs, thread=thread)
                    print(f"Agent: {response}")
                    thread = response.thread
            finally:
                await thread.delete() if thread else None


if __name__ == "__main__":
    asyncio.run(chat())
