import uuid
from typing import Optional
from semantic_kernel.functions import kernel_function

from src._database_manager import get_database_manager


class DatabasePlugin:

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
        db = get_database_manager()
        return db.guidelines.create(id, data=data)

    @kernel_function(description="Create a new Memory in the database.")
    async def create_memory(
        self,
        id: str,
        title: str,
        content: str,
        language: Optional[str] = None,
        service_name: Optional[str] = None,
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
            service_name (str, optional): The Azure service the memory applies to.
            is_exception (bool, optional): If the memory is an exception to guidelines.
            source (str): The source of the memory.
        """
        data = {
            "id": id,
            "title": title,
            "content": content,
            "language": language,
            "service": service_name,
            "is_exception": is_exception,
            "source": source,
        }
        db = get_database_manager()
        return db.memories.create(id, data=data)

    @kernel_function(description="Create a new Example in the database.")
    async def create_example(
        self,
        title: str,
        content: str,
        id: str = None,
        language: Optional[str] = None,
        service_name: Optional[str] = None,
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
            service_name (str, optional): The Azure service the example applies to.
            is_exception (bool, optional): If the example is an exception to guidelines.
            example_type (str): Whether this example is 'good' or 'bad'.
        """
        data = {
            "id": id,
            "title": title,
            "content": content,
            "language": language,
            "service": service_name,
            "is_exception": is_exception,
            "example_type": example_type,
        }
        db = get_database_manager()
        return db.examples.create(id, data=data)

    @kernel_function(description="Retrieve a memory from the database by its ID.")
    async def get_memory(self, memory_id: str):
        """
        Retrieve a memory from the database by its ID.
        Args:
            memory_id (str): The ID of the memory to retrieve.
        """
        db = get_database_manager()
        return db.memories.get(memory_id)

    @kernel_function(description="Retrieve an example from the database by its ID.")
    async def get_example(self, example_id: str):
        """
        Retrieve an example from the database by its ID.
        Args:
            example_id (str): The ID of the example to retrieve.
        """
        db = get_database_manager()
        return db.examples.get(example_id)

    @kernel_function(description="Retrieve a guideline from the database by its ID.")
    async def get_guideline(self, guideline_id: str):
        """
        Retrieve a guideline from the database by its ID.
        Args:
            guideline_id (str): The ID of the guideline to retrieve.
        """
        db = get_database_manager()
        return db.guidelines.get(guideline_id)

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
        db = get_database_manager()
        source_c = db.get_container_client(source_container)
        target_c = db.get_container_client(target_container)

        # Check source item exists
        try:
            source_item = source_c.get(source_id)
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
                _ = target_c.get(target_id)
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
            source_c.upsert(source_id, data=source_item)
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
        db = get_database_manager()
        source_c = db.get_container_client(source_container)

        # Check source item exists
        try:
            source_item = source_c.get(source_id)
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
            source_c.upsert(source_id, data=source_item)
        return {
            "status": "done",
            "source_id": source_id,
            "related_field": related_field,
            "removed": removed,
            "not_found": not_found,
        }

    @kernel_function(description="Delete a Guideline from the database by its ID.")
    async def delete_guideline(self, id: str):
        db = get_database_manager()
        return db.guidelines.delete(id)

    @kernel_function(description="Delete a Memory from the database by its ID.")
    async def delete_memory(self, id: str):
        db = get_database_manager()
        return db.memories.delete(id)

    @kernel_function(description="Delete an Example from the database by its ID.")
    async def delete_example(self, id: str):
        db = get_database_manager()
        return db.examples.delete(id)
