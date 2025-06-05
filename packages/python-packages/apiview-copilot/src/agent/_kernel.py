from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion
from .skills import ChunkingSkill


def create_kernel() -> Kernel:
    """
    Creates and configures a Semantic Kernel instance with Azure OpenAI.
    """
    kernel = Kernel()

    # Configure the Azure OpenAI service
    kernel.add_service(
        AzureChatCompletion(api_base="https://your-openai-api-endpoint", deployment_name="your-deployment-name")
    )

    # Register skills
    kernel.add_skill("ChunkingSkill", ChunkingSkill())

    return kernel
