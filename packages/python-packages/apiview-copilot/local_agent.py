import argparse
import asyncio
from dotenv import load_dotenv
import logging
from semantic_kernel.agents import AzureAIAgentThread

from src.agent._agent import get_main_agent, SearchPlugin, UtilityPlugin, ApiReviewPlugin, DatabasePlugin

load_dotenv(override=True)

# Enable INFO-level logging for kernel function invocation messages
logging.getLogger("semantic_kernel.functions.kernel_function_log_messages").setLevel(logging.INFO)

_PLUGINS = {
    "SearchPlugin": SearchPlugin,
    "UtilityPlugin": UtilityPlugin,
    "ApiReviewPlugin": ApiReviewPlugin,
    "DatabasePlugin": DatabasePlugin,
}


async def call_plugin_function(*, plugin_name: str, function_name: str, function_args: dict):
    """
    Dynamically call a function on the specified plugin.

    :param plugins: dict mapping plugin names to plugin instances
    :param plugin_name: name of the plugin/tool to use
    :param function_name: name of the function to call
    :param function_args: dict of arguments for the function
    :return: result of the function call, or error message
    """
    plugin = _PLUGINS.get(plugin_name)
    if not plugin:
        logging.error(f"Plugin '{plugin_name}' not found.")
        return f"Plugin '{plugin_name}' not found."

    func = getattr(plugin, function_name, None)
    if not func:
        logging.error(f"Function '{function_name}' not found in plugin '{plugin_name}'.")
        return f"Function '{function_name}' not found in plugin '{plugin_name}'."

    try:
        if callable(func):
            # Support both async and sync functions
            if hasattr(func, "__await__"):
                return await func(**function_args)
            else:
                return func(**function_args)
        else:
            return f"Attribute '{function_name}' of plugin '{plugin_name}' is not callable."
    except Exception as e:
        logging.exception(f"Error calling {plugin_name}.{function_name}: {e}")
        return f"Error calling {plugin_name}.{function_name}: {e}"


async def chat(thread_id=None):
    print("Interactive API Review Agent Chat. Type 'exit' to quit.")
    BLUE = "\033[94m"
    GREEN = "\033[92m"
    RESET = "\033[0m"
    async with get_main_agent() as agent:
        thread = None
        while True:
            user_input = input(f"{GREEN}You:{RESET} ")
            if user_input.strip().lower() in {"exit", "quit"}:
                print("Exiting chat.")
                if thread:
                    print(f"Supply thread ID {thread.id} to continue the discussion later.")
                break
            # Only use thread_id if it is a valid Azure thread id (starts with 'thread')
            if thread is None:
                if thread_id and isinstance(thread_id, str) and thread_id.startswith("thread"):
                    thread = AzureAIAgentThread(client=agent.client, thread_id=thread_id)
                else:
                    thread = AzureAIAgentThread(client=agent.client)
            try:
                response = await agent.get_response(thread=thread, messages=user_input)
                print(f"{BLUE}Agent:{RESET} {response}\n")
                thread = response.thread
            except Exception as e:
                print(f"Error: {e}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Interactive API Review Agent Chat")
    parser.add_argument(
        "--thread-id", type=str, default=None, help="Optional Azure thread id to continue a previous session"
    )
    args = parser.parse_args()
    asyncio.run(chat(thread_id=args.thread_id))
