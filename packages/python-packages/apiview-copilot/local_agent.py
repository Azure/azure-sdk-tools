import logging
import asyncio
from dotenv import load_dotenv
from semantic_kernel.agents import AzureAIAgentThread
import uuid
from src.agent import get_main_agent

load_dotenv(override=True)

# Configure logging to file
logging.basicConfig(
    level=logging.INFO,
    format="[%(asctime)s] %(levelname)s - %(message)s",
    handlers=[
        logging.FileHandler("agent_cli.log", mode="a", encoding="utf-8"),
    ],
)


async def chat():
    print("Interactive API Review Agent Chat (local kernel). Type 'exit' to quit.")
    BLUE = "\033[94m"
    GREEN = "\033[92m"
    RESET = "\033[0m]"
    async with get_main_agent() as agent:
        thread = AzureAIAgentThread(
            thread_id=str(uuid.uuid4()),
            messages=[],
            client=getattr(agent, "client", None),
        )
        while True:
            user_input = input(f"{GREEN}You:{RESET} ")
            if user_input.strip().lower() in {"exit", "quit"}:
                print("Exiting chat.")
                break
            try:
                response = await agent.invoke_async(
                    user_input,
                    thread_id=thread.id,
                    messages=thread.messages,
                )
                print(f"{BLUE}Agent:{RESET} {response}\n")
            except Exception as e:
                print(f"{BLUE}Agent:{RESET} [Error] {e}\n")


if __name__ == "__main__":
    asyncio.run(chat())
