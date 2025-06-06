from dotenv import load_dotenv
import os
from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion

from .plugins import ChunkingPlugin

load_dotenv(override=True)


def create_kernel() -> Kernel:
    """
    Creates and configures a Semantic Kernel instance with Azure OpenAI.
    """
    kernel = Kernel(
        plugins={"ChunkingPlugin": ChunkingPlugin()},
        services={
            "AzureChatCompletion": AzureChatCompletion(
                base_url=os.getenv("AZURE_OPENAI_ENDPOINT"),
                deployment_name="gpt-4.1",
                api_key=os.getenv("AZURE_OPENAI_API_KEY"),
            )
        },
    )
    return kernel
