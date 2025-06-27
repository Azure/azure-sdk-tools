import os
import uuid
from typing import Optional
from semantic_kernel.functions import kernel_function
import logging

from src._models import Memory

from azure.cosmos import CosmosClient
from azure.identity import DefaultAzureCredential

COSMOS_ACC_NAME = os.environ.get("AZURE_COSMOS_ACC_NAME")
COSMOS_DB_NAME = os.environ.get("AZURE_COSMOS_DB_NAME")
COSMOS_ENDPOINT = f"https://{COSMOS_ACC_NAME}.documents.azure.com:443/"


def get_database_client() -> CosmosClient:
    return CosmosClient(COSMOS_ENDPOINT, credential=DefaultAzureCredential())


class DatabasePlugin:

    @kernel_function(
        description="Search for Guidelines in the ArchAgent Knowledge Base by programming language (e.g. python, csharp, etc.)."
    )
    async def get_guideline(self, guideline_id: str):
        """
        Retrieve a guideline from the ArchAgent Knowledge Base by its ID.
        Args:
            guideline_id (str): The ID of the guideline to retrieve.
        """
        client = get_database_client()
        container = client.get_database_client(COSMOS_DB_NAME).get_container_client("guidelines")

        # replace .html# with =html= in guideline_id
        guideline_id = guideline_id.replace(".html#", "=html=")

        guideline = container.read_item(item=guideline_id, partition_key=guideline_id)
        # TODO: Expand the guideline into a context object with links resolved

        return guideline

    @kernel_function(description="Add a memory related to a guideline in the ArchAgent Knowledge Base.")
    async def add_guideline_memory(
        self,
        title: str,
        content: str,
        guideline_id: str,
        is_exception: bool,
        service_name: Optional[str],
        language: Optional[str],
    ):
        """
        Add a memory related to a guideline in the ArchAgent Knowledge Base.
        Args:
            guideline_id (str): The ID of the guideline to which the memory is related.
            content (str): The memory content to add.
            title (str): The title of the memory.
            is_exception (bool): Whether the memory is an exception to established guidelines.
            service_name (str): The service related to the memory, if any.
            language (str): The programming language of the memory, if any.
        """
        if not language:
            return {"status": "error", "message": "Language must be specified."}

        client = get_database_client()
        guideline_container = client.get_database_client(COSMOS_DB_NAME).get_container_client("guidelines")
        memory_container = client.get_database_client(COSMOS_DB_NAME).get_container_client("memories")

        # replace .html# with =html= in guideline_id
        guideline_id = guideline_id.replace(".html#", "=html=")

        guideline = guideline_container.read_item(item=guideline_id, partition_key=guideline_id)
        memory_id = str(uuid.uuid4())
        memory = Memory(
            id=memory_id,
            related_guidelines=[guideline_id],
            title=title,
            content=content,
            source="agent",
            is_exception=is_exception,
            service=service_name,
            language=language,
        )
        related_memories = guideline.get("related_memories", [])
        related_memories.append(memory_id)
        guideline["related_memories"] = related_memories
        guideline_container.upsert_item(guideline)
        memory_container.upsert_item(memory.model_dump())
        return {"status": "Memory added successfully"}
