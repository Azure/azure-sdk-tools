from dotenv import load_dotenv
import asyncio
from src.agent._agent import get_main_agent

load_dotenv(override=True)


async def chat():
    print("Interactive API Review Agent Chat. Type 'exit' to quit.")
    user_inputs = []
    async with get_main_agent() as agent:
        from semantic_kernel.agents import AzureAIAgentThread

        thread = AzureAIAgentThread(client=agent.client)
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
