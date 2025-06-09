from dotenv import load_dotenv
import asyncio
from src.agent._agent import get_main_agent, SearchPlugin, UtilityPlugin, ApiReviewPlugin
import logging

load_dotenv(override=True)

# Enable INFO-level logging for kernel function invocation messages
logging.getLogger("semantic_kernel.functions.kernel_function_log_messages").setLevel(logging.INFO)

_PLUGINS = {"SearchPlugin": SearchPlugin, "UtilityPlugin": UtilityPlugin, "ApiReviewPlugin": ApiReviewPlugin}


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


async def chat():
    print("Interactive API Review Agent Chat. Type 'exit' to quit.")
    user_inputs = []
    BLUE = "\033[94m"
    GREEN = "\033[92m"
    RESET = "\033[0m"
    async with get_main_agent() as agent:
        from semantic_kernel.agents import AzureAIAgentThread

        thread = AzureAIAgentThread(client=agent.client)
        try:
            while True:
                user_input = input(f"{GREEN}You:{RESET} ")
                if user_input.strip().lower() in {"exit", "quit"}:
                    print("Exiting chat.")
                    break
                user_inputs.append(user_input)
                async for response in agent.invoke(messages=user_inputs, thread=thread):
                    # handle each response (likely just one per turn, but this is the correct pattern)
                    # Check for function call (adjust attribute access as needed)
                    if hasattr(response, "is_function_call") and response.is_function_call:
                        function_name = response.function_name
                        function_args = response.arguments
                        # Try to extract plugin/tool name from the response
                        plugin_name = (
                            getattr(response, "plugin_name", None)
                            or getattr(response, "tool_name", None)
                            or "SearchPlugin"
                        )  # Default/fallback
                        logging.info(
                            f"Invoking function: {function_name} from plugin: {plugin_name} with args: {function_args}"
                        )
                        # Call the plugin function
                        function_result = await call_plugin_function(plugin_name, function_name, function_args)
                        # Append the function result to the conversation
                        thread.append({"role": "function", "name": function_name, "content": function_result})
                    else:
                        # Final answer from the agent
                        print(f"{BLUE}Agent:{RESET} {response}\n")
                        thread = response.thread
                    break  # If you only expect one response, break after the first
        finally:
            await thread.delete() if thread else None


if __name__ == "__main__":
    asyncio.run(chat())
