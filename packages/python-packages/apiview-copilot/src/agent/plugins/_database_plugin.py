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

    def _delete_item(self, container_name: str, item_id: str):
        """
        Dummy shared delete implementation for all delete_* functions.
        """
        return {"status": "not_implemented", "message": "Sorry, deleting is scary! This isn't implemented yet."}

    def _create_item(self, container_name: str, data: dict, id_field: str = "id"):
        """
        Centralized method to create an item in a CosmosDB container.
        If 'id' is not present in data, generate a UUID.
        Fails if the item already exists.
        Filters out None values from data.
        """
        # Remove None values so Pydantic default_factory works
        data = {k: v for k, v in data.items() if v is not None}
        client = get_database_client()
        container = client.get_database_client(COSMOS_DB_NAME).get_container_client(container_name)
        if id_field not in data or not data.get(id_field):
            data[id_field] = str(uuid.uuid4())
        # Check if item exists
        try:
            _ = container.read_item(item=data[id_field], partition_key=data[id_field])
            return {"status": "error", "message": f"Item with {id_field} '{data[id_field]}' already exists."}
        except Exception:
            # Not found, safe to create
            pass
        container.create_item(body=data)
        return {"status": "created", id_field: data[id_field]}

    @kernel_function(description="Create a new Guideline in the database.")
    async def create_guideline(
        self,
        id: str,
        content: str,
        title: str = None,
        language: Optional[str] = None,
    ):
        """
        Create a new guideline in the database.
        Args:
            id (str): Unique identifier for the guideline.
            title (str): Short descriptive title of the guideline.
            content (str): Full text of the guideline.
            language (str, optional): Language the guideline applies to.
        """
        data = {
            "id": id,
            "title": title,
            "content": content,
            "language": language,
        }
        return self._create_item("guidelines", data)

    @kernel_function(description="Create a new Memory in the database.")
    async def create_memory(
        self,
        id: str,
        title: str,
        content: str,
        language: Optional[str] = None,
        service: Optional[str] = None,
        is_exception: bool = False,
        source: str = None,
    ):
        """
        Create a new memory in the database.
        Args:
            id (str): Unique identifier for the memory.
            title (str): Short descriptive title of the memory.
            content (str): Content of the memory.
            language (str, optional): Language the memory applies to.
            service (str, optional): Service the memory applies to.
            is_exception (bool, optional): If the memory is an exception to guidelines.
            source (str): The source of the memory.
        """
        data = {
            "id": id,
            "title": title,
            "content": content,
            "language": language,
            "service": service,
            "is_exception": is_exception,
            "source": source,
        }
        return self._create_item("memories", data)

    @kernel_function(description="Create a new Example in the database.")
    async def create_example(
        self,
        title: str,
        content: str,
        id: str = None,
        language: Optional[str] = None,
        service: Optional[str] = None,
        is_exception: bool = False,
        example_type: str = None,
    ):
        """
        Create a new example in the database.
        Args:
            id (str): Unique identifier for the example.
            title (str): Short descriptive title of the example.
            content (str): Code snippet containing the example.
            language (str, optional): Language the example applies to.
            service (str, optional): Service the example applies to.
            is_exception (bool, optional): If the example is an exception to guidelines.
            example_type (str): Whether this example is 'good' or 'bad'.
        """
        data = {
            "id": id,
            "title": title,
            "content": content,
            "language": language,
            "service": service,
            "is_exception": is_exception,
            "example_type": example_type,
        }
        return self._create_item("examples", data)

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

    @kernel_function(
        description="Link one or more target items to a source item by adding their IDs to a related field in the source item."
    )
    async def link_items(
        self,
        source_id: str,
        source_container: str,
        target_ids: list[str],
        target_container: str,
        related_field: str = None,
    ):
        """
        Link one or more target items to a source item by adding their IDs to a related field in the source item.
        Args:
            source_id (str): The ID of the source item.
            source_container (str): The container name of the source item (examples, memories, guidelines).
            target_ids (list): The IDs of the target items.
            target_container (str): The container name of the target items (examples, memories, guidelines).
            related_field (str, optional): The field in the source item to update (default: 'related_' + target_container).
        """
        client = get_database_client()
        db = client.get_database_client(COSMOS_DB_NAME)
        source_c = db.get_container_client(source_container)
        target_c = db.get_container_client(target_container)

        # Check source item exists
        try:
            source_item = source_c.read_item(item=source_id, partition_key=source_id)
        except Exception as e:
            return {"status": "error", "message": f"Source item not found: {e}"}

        # Determine the related field name
        if not related_field:
            related_field = f"related_{target_container}"
        # Prepare the related list
        related = source_item.get(related_field, [])
        if not isinstance(related, list):
            related = []

        results = {"linked": [], "already_linked": [], "not_found": []}
        for target_id in target_ids:
            try:
                _ = target_c.read_item(item=target_id, partition_key=target_id)
            except Exception:
                results["not_found"].append(target_id)
                continue
            if target_id in related:
                results["already_linked"].append(target_id)
            else:
                related.append(target_id)
                results["linked"].append(target_id)
        if results["linked"]:
            source_item[related_field] = related
            source_c.upsert_item(source_item)
        return {"status": "done", "source_id": source_id, "related_field": related_field, **results}

    @kernel_function(
        description="Unlink one or more target items from a source item by removing their IDs from a related field in the source item."
    )
    async def unlink_items(
        self,
        source_id: str,
        source_container: str,
        target_ids: list[str],
        target_container: str,
        related_field: str = None,
    ):
        """
        Unlink one or more target items from a source item by removing their IDs from a related field in the source item.
        Args:
            source_id (str): The ID of the source item.
            source_container (str): The container name of the source item (examples, memories, guidelines).
            target_ids (list): The IDs of the target items to remove.
            target_container (str): The container name of the target items (examples, memories, guidelines).
            related_field (str, optional): The field in the source item to update (default: 'related_' + target_container).
        """
        client = get_database_client()
        db = client.get_database_client(COSMOS_DB_NAME)
        source_c = db.get_container_client(source_container)

        # Check source item exists
        try:
            source_item = source_c.read_item(item=source_id, partition_key=source_id)
        except Exception as e:
            return {"status": "error", "message": f"Source item not found: {e}"}

        # Determine the related field name
        if not related_field:
            related_field = f"related_{target_container}"
        # Prepare the related list
        related = source_item.get(related_field, [])
        if not isinstance(related, list):
            related = []

        removed = []
        not_found = []
        for target_id in target_ids:
            if target_id in related:
                related.remove(target_id)
                removed.append(target_id)
            else:
                not_found.append(target_id)
        if removed:
            source_item[related_field] = related
            source_c.upsert_item(source_item)
        return {
            "status": "done",
            "source_id": source_id,
            "related_field": related_field,
            "removed": removed,
            "not_found": not_found,
        }

    @kernel_function(description="Delete a Guideline from the database by its ID.")
    async def delete_guideline(self, id: str):
        return self._delete_item("guidelines", id)

    @kernel_function(description="Delete a Memory from the database by its ID.")
    async def delete_memory(self, id: str):
        return self._delete_item("memories", id)

    @kernel_function(description="Delete an Example from the database by its ID.")
    async def delete_example(self, id: str):
        return self._delete_item("examples", id)
