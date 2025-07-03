import os
import uuid
from typing import Optional
from semantic_kernel.functions import kernel_function

from src._models import Memory, Example, ExampleType

from azure.cosmos import CosmosClient
from azure.identity import DefaultAzureCredential

COSMOS_ACC_NAME = os.environ.get("AZURE_COSMOS_ACC_NAME")
COSMOS_DB_NAME = os.environ.get("AZURE_COSMOS_DB_NAME")
COSMOS_ENDPOINT = f"https://{COSMOS_ACC_NAME}.documents.azure.com:443/"


def get_database_client() -> CosmosClient:
    return CosmosClient(COSMOS_ENDPOINT, credential=DefaultAzureCredential())


class DatabasePlugin:

    def _get_item_by_id(self, container_name: str, item_id: str, preprocess_id=None):
        """
        Centralized method to retrieve an item from a CosmosDB container by its ID.
        Optionally preprocess the ID (e.g., for guidelines).
        """
        client = get_database_client()
        container = client.get_database_client(COSMOS_DB_NAME).get_container_client(container_name)
        if preprocess_id:
            item_id = preprocess_id(item_id)
        return container.read_item(item=item_id, partition_key=item_id)

    @kernel_function(description="Retrieve a memory from the database by its ID.")
    async def get_memory(self, memory_id: str):
        """
        Retrieve a memory from the database by its ID.
        Args:
            memory_id (str): The ID of the memory to retrieve.
        """
        return self._get_item_by_id("memories", memory_id)

    @kernel_function(description="Retrieve an example from the database by its ID.")
    async def get_example(self, example_id: str):
        """
        Retrieve an example from the database by its ID.
        Args:
            example_id (str): The ID of the example to retrieve.
        """
        return self._get_item_by_id("examples", example_id)

    @kernel_function(description="Retrieve a guideline from the database by its ID.")
    async def get_guideline(self, guideline_id: str):
        """
        Retrieve a guideline from the database by its ID.
        Args:
            guideline_id (str): The ID of the guideline to retrieve.
        """

        def preprocess(gid):
            return gid.replace(".html#", "=html=")

        return self._get_item_by_id("guidelines", guideline_id, preprocess_id=preprocess)

    @kernel_function(description="Retrieve a job status from the database by its ID.")
    async def get_api_review_job_status(self, job_id: str):
        """
        Retrieve an API review job status from the database by its ID.
        Args:
            job_id (str): The ID of the job to retrieve.
        """
        return self._get_item_by_id("apireview-jobs", job_id)

    # @kernel_function(description="Add a memory related to a guideline in the ArchAgent Knowledge Base.")
    # async def add_guideline_memory(
    #     self,
    #     title: str,
    #     content: str,
    #     guideline_id: str,
    #     is_exception: bool,
    #     service_name: Optional[str],
    #     language: Optional[str],
    # ):
    #     """
    #     Add a memory related to a guideline in the ArchAgent Knowledge Base.
    #     Args:
    #         guideline_id (str): The ID of the guideline to which the memory is related.
    #         content (str): The memory content to add.
    #         title (str): The title of the memory.
    #         is_exception (bool): Whether the memory is an exception to established guidelines.
    #         service_name (str): The service related to the memory, if any.
    #         language (str): The programming language of the memory, if any.
    #     """
    #     if not language:
    #         return {"status": "error", "message": "Language must be specified."}

    #     client = get_database_client()
    #     guideline_container = client.get_database_client(COSMOS_DB_NAME).get_container_client("guidelines")
    #     memory_container = client.get_database_client(COSMOS_DB_NAME).get_container_client("memories")

    #     # replace .html# with =html= in guideline_id
    #     guideline_id = guideline_id.replace(".html#", "=html=")

    #     guideline = guideline_container.read_item(item=guideline_id, partition_key=guideline_id)
    #     memory_id = str(uuid.uuid4())
    #     memory = Memory(
    #         id=memory_id,
    #         related_guidelines=[guideline_id],
    #         title=title,
    #         content=content,
    #         source="agent",
    #         is_exception=is_exception,
    #         service=service_name,
    #         language=language,
    #     )
    #     related_memories = guideline.get("related_memories", [])
    #     related_memories.append(memory_id)
    #     guideline["related_memories"] = related_memories
    #     guideline_container.upsert_item(guideline)
    #     memory_container.upsert_item(memory.model_dump())
    #     return {"status": "Memory added successfully"}

    # @kernel_function(description="Add an example related to a memory in the ArchAgent Knowledge Base.")
    # async def add_memory_example(
    #     self,
    #     title: str,
    #     content: str,
    #     memory_id: str,
    #     is_exception: bool,
    #     service_name: Optional[str],
    #     language: Optional[str],
    # ):
    #     """
    #     Add an example related to a memory in the ArchAgent Knowledge Base.
    #     Args:
    #         memory_id (str): The ID of the memory to which the example is related.
    #         content (str): The example content to add.
    #         title (str): The title of the example.
    #         is_exception (bool): Whether the example is an exception to established guidelines.
    #         service_name (str): The service related to the memory, if any.
    #         language (str): The programming language of the memory, if any.
    #     """
    #     if not language:
    #         return {"status": "error", "message": "Language must be specified."}

    #     client = get_database_client()
    #     example_container = client.get_database_client(COSMOS_DB_NAME).get_container_client("examples")
    #     memory_container = client.get_database_client(COSMOS_DB_NAME).get_container_client("memories")

    #     memory = memory_container.read_item(item=memory_id, partition_key=memory_id)
    #     example_id = str(uuid.uuid4())
    #     example = Example(
    #         id=example_id,
    #         related_memories=[memory_id],
    #         title=title,
    #         content=content,
    #         source="agent",
    #         is_exception=is_exception,
    #         service=service_name,
    #         language=language,
    #     )
    #     related_memories = example.get("related_memories", [])
    #     related_memories.append(memory_id)
    #     example["related_memories"] = related_memories
    #     memory_container.upsert_item(memory)
    #     example_container.upsert_item(example.model_dump())
    #     return {"status": "Example added successfully"}
