# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Tool for database operations."""

from contextlib import contextmanager
from typing import Optional

from azure.ai.agents import AgentsClient
from azure.cosmos.exceptions import CosmosResourceNotFoundError

# pylint: disable=no-name-in-module
from src._credential import get_credential
from src._database_manager import ContainerNames, DatabaseManager
from src._models import Example, Guideline, Memory
from src._settings import SettingsManager
from src.agent.tools._base import Tool


@contextmanager
def get_delete_agent():
    """Agent for handling database delete requests."""
    # pylint: disable=import-outside-toplevel
    from src.agent._agent import _get_agent_settings

    credential = get_credential()
    settings = SettingsManager()
    endpoint = settings.get("FOUNDRY_ENDPOINT")
    model_deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")

    ai_instructions = f"""
You are an agent that processes database delete requests for guidelines, examples, memories or review jobs.

You must ensure you adhere to the following guidelines.

# INSTRUCTIONS

## General
- The only valid container names are: {', '.join([c.value for c in ContainerNames])}

## Deletion Process
- Never delete guidelines. You MUST deny any request to delete a guideline.
- Never delete review jobs. You MUST deny any request to delete a review job. Review jobs are deleted programmatically ONLY.
"""
    client = AgentsClient(endpoint=endpoint.endpoint, credential=credential)
    agent = client.create_agent(
        name="DeleteAgent",
        description="Handles database delete requests.",
        model=model_deployment_name,
        instructions=ai_instructions,
        tools=[DatabaseDeleteTool()],
    )
    try:
        yield client, agent.id
    finally:
        client.delete_agent(agent.id)


@contextmanager
def get_create_agent():
    """Agent for handling database create requests."""
    from src._apiview_reviewer import SUPPORTED_LANGUAGES
    from src.agent._agent import _get_agent_settings

    credential = get_credential()
    settings = SettingsManager()
    endpoint = settings.get("FOUNDRY_ENDPOINT")
    model_deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")

    guideline_schema = Guideline.model_json_schema()
    example_schema = Example.model_json_schema()
    memory_schema = Memory.model_json_schema()
    ai_instructions = f"""
You are an agent that processes database create requests for guidelines, examples, memories or review jobs.

You must ensure you adhere to the following guidelines.

# INSTRUCTIONS

## General
- The only valid container names are: {', '.join([c.value for c in ContainerNames])}
- The only valid language values are: {', '.join(SUPPORTED_LANGUAGES)}
- cpp means C++
- dotnet means C#
- ios means Swift
- clang means C
- golang means Go
- android means Java for Android
- typescript means TypeScript or JavaScript
- DO NOT simply reuse the ID as the title. If you need to infer title, infer it from the content or ask the user for it.

## Guidelines

The Guideline schema you must adhere to is:
{guideline_schema}

For specific fields:
- content: you must be provided this. If you are not, ask for it.
- title: you can be provided this. If you are not, you can infer it from the content, or, as a last resort, ask for it.
- id: you can be provided this. If you are not, you can infer it from the content
- language: this identifies whether the guideline is specific to a particular programming language or applies to all languages. If it is not clear from context, you should ask the user.

## Examples

The Example schema you must adhere to is:
{example_schema}

For specific fields:
- content: this must be a code snippet. If not provided by the user, you can try to infer it from the conversation or ask the user.
- title: you can be provided this. If you are not, you can infer it from the content, or, as a last resort, ask for it.
- id: you can be provided this. If you are not, you can infer it from the content
- language: this should be inferred from the code.
- service: this indicates if this example is specific to a particular service or ALL services (in which case it is null). If it is not clear from the conversation, you should ALWAYS clarify with the user.
- is_exception: should be set to true if the example represents an exception to established policies rather than a reinforcement of them. If it is not clear from the conversation, you should clarify with the user.
- example_type: this specifies if this is a code example representing something GOOD (what they SHOULD do) or BAD (what they SHOULD NOT do). If it is not clear from the conversation, you should ALWAYS clarify with the user.

## Memories

The Memory schema you must adhere to is:
{memory_schema}

For specific fields:
- content: you must be provided this. If not provided by the user, you can try to infer it from the conversation or ask the user.
- title: you can be provided this. If you are not, you can infer it from the content, or, as a last resort, ask for it.
- id: you can be provided this. If you are not, you can infer it from the content
- language: this identifies whether the memory is specific to a particular programming language or applies to all languages. If it is not clear from context, you should ask the user.
- service: this indicates if this memory is specific to a particular service or ALL services (in which case it is null). If it is not clear from the conversation, you should ALWAYS clarify with the user.
- is_exception: should be set to true if the memory represents an exception to established policies rather than a reinforcement of them. If it is not clear from the conversation, you should clarify with the user.
- source: this should always be set to `agent` when created by the agent.

## Review Jobs
- You should never create a review job via the agent. Review jobs are created programmatically ONLY.

## Linking After Creation
- After creating any new item (example, memory, or guideline), you MUST:
  1. Identify all items it should be linked to (e.g., examples to memories/guidelines, memories to guidelines/examples).
  2. Use the `link_items` function to create these links in both directions.
  3. Confirm that the links exist by retrieving the items and checking their related fields.
  4. Report all linking actions in your response.
- If you do not know what to link, ask the user.
"""
    client = AgentsClient(endpoint=endpoint, credential=credential)
    agent = client.create_agent(
        name="CreateAgent",
        description="Handles database create or insert requests.",
        model=model_deployment_name,
        instructions=ai_instructions,
        tools=[DatabaseCreateTool()],
    )
    try:
        yield client, agent.id
    finally:
        client.delete_agent(agent.id)


@contextmanager
def get_retrieve_agent():
    """Agent for handling database retrieval requests."""
    from src.agent._agent import _get_agent_settings

    credential = get_credential()
    settings = SettingsManager()
    endpoint = settings.get("FOUNDRY_ENDPOINT")
    model_deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")

    ai_instructions = """
You are an agent that processes database get or retrieval requests for guidelines, examples, memories, or review jobs.
"""
    client = AgentsClient(endpoint=endpoint, credential=credential)
    agent = client.create_agent(
        name="RetrieveAgent",
        description="Handles database retrieval requests.",
        model=model_deployment_name,
        instructions=ai_instructions,
        tools=[DatabaseRetrieveTool()],
    )
    try:
        yield client, agent.id
    finally:
        client.delete_agent(agent.id)


@contextmanager
def get_link_agent():
    """Agent for handling database linking and unlinking requests."""
    from src.agent._agent import _get_agent_settings

    credential = get_credential()
    settings = SettingsManager()
    endpoint = settings.get("FOUNDRY_ENDPOINT")
    model_deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")

    ai_instructions = f"""
You are an agent that processes database requests to link or unlink guidelines, examples, memories or review jobs.

You must ensure you adhere to the following guidelines.

# INSTRUCTIONS

- source_id can refer to Guidelines or Memories, but NOT Examples.
- target_id can refer to Guidelines, Examples or Memories.
- The only valid container names are: {', '.join([c.value for c in ContainerNames])}
- Guidelines have the following link fields:
    - related_guidelines
    - related_examples
    - related_memories
- Memories have the following link fields:
    - related_guidelines
    - related_examples
    - related_memories
- Examples have the following link fields:
    - guideline_ids
    - memory_ids
"""
    client = AgentsClient(endpoint=endpoint, credential=credential)
    agent = client.create_agent(
        name="LinkUnlinkAgent",
        description="Handles database linking and unlinking requests.",
        model=model_deployment_name,
        instructions=ai_instructions,
        tools=[DatabaseLinkUnlinkTool()],
    )
    try:
        yield client, agent.id
    finally:
        client.delete_agent(agent.id)


class DatabaseCreateTool(Tool):
    """Tool for creating database items."""

    def create_guideline(
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
        db = DatabaseManager.get_instance()
        return db.guidelines.create(id, data=data)

    def create_memory(
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
        # If service is not a string or None, set it to None
        if service is not None and not isinstance(service, str):
            service = None
        data = {
            "id": id,
            "title": title,
            "content": content,
            "language": language,
            "service": service_name,
            "is_exception": is_exception,
            "source": source,
        }
        db = DatabaseManager.get_instance()
        return db.memories.create(id, data=data)

    def create_example(
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
        # If service is not a string or None, set it to None
        if service is not None and not isinstance(service, str):
            service = None
        data = {
            "id": id,
            "title": title,
            "content": content,
            "language": language,
            "service": service_name,
            "is_exception": is_exception,
            "example_type": example_type,
        }
        db = DatabaseManager.get_instance()
        return db.examples.create(id, data=data)


class DatabaseRetrieveTool(Tool):
    """Tool for retrieving database items."""

    def get_memory(self, memory_id: str):
        """
        Retrieve a memory from the database by its ID.
        Args:
            memory_id (str): The ID of the memory to retrieve.
        """
        db = DatabaseManager.get_instance()
        return db.memories.get(memory_id)

    def get_memory_schema(self):
        """
        Retrieve the Memory schema.
        """
        return Memory.model_json_schema()

    def get_example(self, example_id: str):
        """
        Retrieve an example from the database by its ID.
        Args:
            example_id (str): The ID of the example to retrieve.
        """
        db = DatabaseManager.get_instance()
        return db.examples.get(example_id)

    def get_example_schema(self):
        """
        Retrieve the Example schema.
        """
        return Example.model_json_schema()

    def get_guideline(self, guideline_id: str):
        """
        Retrieve a guideline from the database by its ID.
        Args:
            guideline_id (str): The ID of the guideline to retrieve.
        """
        db = DatabaseManager.get_instance()
        return db.guidelines.get(guideline_id)

    def get_guideline_schema(self):
        """
        Retrieve the Guideline schema.
        """
        return Guideline.model_json_schema()


class DatabaseLinkUnlinkTool(Tool):
    """Tool for linking and unlinking database items."""

    def link_items(
        self,
        source_id: str,
        source_container: str,
        source_field: str,
        target_ids: list[str],
        target_container: str,
        target_field: str,
    ):
        """
        Link one or more target items to a source item by adding their IDs to a related field in the source item.
        Args:
            source_id (str): The ID of the source item.
            source_container (str): The container name of the source item.
            source_field (str): The field in the source item to update.
            target_ids (list): The IDs of the target items.
            target_container (str): The container name of the target items.
            target_field (str): The field in the target items to update.
        """
        db = DatabaseManager.get_instance()
        source_c = db.get_container_client(source_container)
        target_c = db.get_container_client(target_container)

        # Check source item exists
        try:
            source_item = source_c.get(source_id)
        except Exception as e:
            return {"status": "error", "message": f"Source item not found in {source_container}: {e}"}

        # Prepare the related list
        source_field_value = source_item.get(source_field, [])
        if not isinstance(source_field_value, list):
            source_field_value = []

        results = {"linked": [], "already_linked": [], "not_found": []}
        # add the target ID to the source item
        for target_id in target_ids:
            try:
                _ = target_c.get(target_id)
            except Exception:
                results["not_found"].append(target_id)
                continue
            if target_id in source_field_value:
                results["already_linked"].append(target_id)
            else:
                source_field_value.append(target_id)
                results["linked"].append(target_id)
        if results["linked"]:
            source_item[source_field] = source_field_value
            source_c.upsert(source_id, data=source_item)

        # now add the source ID to the target items
        for target_id in target_ids:
            try:
                target_item = target_c.get(target_id)
            except Exception:
                continue
            # Prepare the related field in the target item
            target_field_value = target_item.get(target_field, [])
            if not isinstance(target_field_value, list):
                target_field_value = []
            if source_id not in target_field_value:
                target_field_value.append(source_id)
                target_item[target_field] = target_field_value
                target_c.upsert(target_id, data=target_item)
        return {"status": "done", "source_id": source_id, "source_field": source_field, **results}

    def unlink_items(
        self,
        source_id: str,
        source_container: str,
        source_field: str,
        target_ids: list[str],
        target_container: str,
        target_field: str,
    ):
        """
        Unlink one or more target items from a source item by removing their IDs from a related field
        in the source item.
        Args:
            source_id (str): The ID of the source item.
            source_container (str): The container name of the source item.
            source_field (str): The field in the source item to update.
            target_ids (list): The IDs of the target items to remove.
            target_container (str): The container name of the target items.
            target_field (str): The field in the target item to update.
        """
        db = DatabaseManager.get_instance()
        source_c = db.get_container_client(source_container)

        # Check source item exists
        try:
            source_item = source_c.get(source_id)
        except Exception as e:
            return {"status": "error", "message": f"Source item not found in {source_container}: {e}"}

        # Prepare the related list
        source_field_value = source_item.get(source_field, [])
        if not isinstance(source_field_value, list):
            source_field_value = []

        removed = []
        not_found = []
        for target_id in target_ids:
            if target_id in source_field_value:
                source_field_value.remove(target_id)
                removed.append(target_id)
            else:
                not_found.append(target_id)
        if removed:
            source_item[source_field] = source_field_value
            source_c.upsert(source_id, data=source_item)

        # now remove the source ID from the target items
        target_c = db.get_container_client(target_container)
        for target_id in target_ids:
            try:
                target_item = target_c.get(target_id)
            except Exception:
                continue
            # Prepare the related field in the target item
            target_field_value = target_item.get(target_field, [])
            if not isinstance(target_field_value, list):
                target_field_value = []
            if source_id in target_field_value:
                target_field_value.remove(source_id)
                target_item[target_field] = target_field_value
                target_c.upsert(target_id, data=target_item)
        return {
            "status": "done",
            "source_id": source_id,
            "source_field": source_field,
            "removed": removed,
            "not_found": not_found,
        }


class DatabaseDeleteTool(Tool):
    """Tool for deleting database items."""

    def delete_guideline(self, id: str):
        """Delete a guideline from the database by its ID."""
        db = DatabaseManager.get_instance()
        try:
            db.guidelines.delete(id)
            return f"Guideline with id '{id}' deleted successfully."
        except CosmosResourceNotFoundError:
            return f"Guideline with id '{id}' not found."

    def delete_memory(self, id: str):
        """Delete a memory from the database by its ID."""
        db = DatabaseManager.get_instance()
        try:
            db.memories.delete(id)
            return f"Memory with id '{id}' deleted successfully."
        except CosmosResourceNotFoundError:
            return f"Memory with id '{id}' not found."

    def delete_example(self, id: str):
        """Delete an example from the database by its ID."""
        db = DatabaseManager.get_instance()
        try:
            db.examples.delete(id)
            return f"Example with id '{id}' deleted successfully."
        except CosmosResourceNotFoundError:
            return f"Example with id '{id}' not found."

    def delete_review_job(self, id: str):
        """Delete a review job from the database by its ID."""
        db = DatabaseManager.get_instance()
        try:
            db.review_jobs.delete(id)
            return f"Review Job with id '{id}' deleted successfully."
        except CosmosResourceNotFoundError:
            return f"Review Job with id '{id}' not found."
