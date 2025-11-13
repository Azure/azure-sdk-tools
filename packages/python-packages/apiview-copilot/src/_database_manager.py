# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for managing the database connections and operations for APIView Copilot.
"""

import time
from enum import Enum
from functools import lru_cache
from typing import Any

from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosResourceNotFoundError
from azure.search.documents.indexes import SearchIndexerClient
from pydantic import BaseModel
from src._credential import get_credential
from src._settings import SettingsManager


class ContainerNames(Enum):
    """Enumeration for container names in the database."""

    GUIDELINES = "guidelines"
    MEMORIES = "memories"
    EXAMPLES = "examples"
    REVIEW_JOBS = "review-jobs"
    METRICS = "metrics"
    EVALS = "evals"

    @classmethod
    def values(cls) -> list[str]:
        """Return a list of container names."""
        return [name.value for name in cls]

    @classmethod
    def data_containers(cls) -> list[str]:
        """Return a list of all data container names, omitting containers that are for internal bookkeeping."""
        return [name.value for name in cls if name not in {cls.REVIEW_JOBS, cls.METRICS}]


class DatabaseManager:
    """Manager for Azure Cosmos DB operations."""

    _instance: "DatabaseManager" = None

    @classmethod
    def get_instance(cls, force_new: bool = False) -> "DatabaseManager":
        """
        Returns a singleton instance of DatabaseManager.
        """
        if cls._instance is None or force_new:
            cls._instance = cls()
        return cls._instance

    def __init__(self):
        settings = SettingsManager()
        db_name = settings.get("COSMOS_DB_NAME")
        endpoint = settings.get("COSMOS_ENDPOINT")
        credential = get_credential()

        self.client = CosmosClient(endpoint, credential=credential)
        self.database = self.client.get_database_client(db_name)
        self.containers = {}

    def get_container_client(self, name: str) -> "BasicContainer":
        """Get a container client by name."""
        # Decide which container class to use
        if name not in self.containers:
            if name == ContainerNames.REVIEW_JOBS.value:
                self.containers[name] = ReviewJobsContainer(self, name)
            elif name == ContainerNames.METRICS.value:
                self.containers[name] = MetricsContainer(self, name)
            elif name == ContainerNames.EVALS.value:
                self.containers[name] = EvalsContainer(self, name)
            elif name == ContainerNames.GUIDELINES.value:
                self.containers[name] = GuidelinesContainer(self, name)
            else:
                self.containers[name] = BasicContainer(self, name)
        return self.containers[name]

    @property
    def guidelines(self):
        """Get the guidelines container client."""
        return self.get_container_client(ContainerNames.GUIDELINES.value)

    @property
    def memories(self):
        """Get the memories container client."""
        return self.get_container_client(ContainerNames.MEMORIES.value)

    @property
    def examples(self):
        """Get the examples container client."""
        return self.get_container_client(ContainerNames.EXAMPLES.value)

    @property
    def review_jobs(self):
        """Get the review jobs container client."""
        return self.get_container_client(ContainerNames.REVIEW_JOBS.value)

    @property
    def metrics(self):
        """Get the metrics container client."""
        return self.get_container_client(ContainerNames.METRICS.value)

    @property
    def evals(self):
        """Get the evaluations container client."""
        return self.get_container_client(ContainerNames.EVALS.value)


class BasicContainer:
    """Basic container client for Azure Cosmos DB operations."""

    def __init__(self, manager: DatabaseManager, container_name: str):
        self.client = manager.database.get_container_client(container_name)
        self.preprocess_id: callable = None
        self.container_name = container_name

    def _to_dict(self, data):
        if BaseModel and isinstance(data, BaseModel):
            return data.model_dump()
        return dict(data) if not isinstance(data, dict) else data

    def create(self, item_id: str, *, data: Any, run_indexer: bool = True):
        """
        Create a new item. Raises an error if the item already exists.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        data_dict = self._to_dict(data)
        # Remove None values
        data_dict = {k: v for k, v in data_dict.items() if v is not None}
        # Ensure 'id' is set
        if not data_dict.get("id"):
            data_dict["id"] = item_id
        # Check if item exists
        try:
            self.client.read_item(item=data_dict["id"], partition_key=data_dict["id"])
            # If no exception, item exists
            return {"status": "error", "message": f"Item with id '{data_dict['id']}' already exists."}
        except CosmosResourceNotFoundError:
            # Item does not exist, safe to create
            self.client.create_item(body=data_dict)
            value = {"status": "created", "id": data_dict["id"]}
            if run_indexer:
                self.run_indexer()
            return value

    def upsert(self, item_id: str, *, data: Any, run_indexer: bool = True):
        """
        Upsert an item. If it exists, update it; if not, create it.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        data_dict = self._to_dict(data)
        value = self.client.upsert_item({"id": item_id, **data_dict})
        if run_indexer:
            self.run_indexer()
        return value

    def get(self, item_id: str):
        """
        Get an item by its ID. If preprocess_id is provided, it will be applied to the item_id before fetching.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        return self.client.read_item(item=item_id, partition_key=item_id)

    def delete(self, item_id: str, *, run_indexer: bool = True):
        """
        Soft-delete an item by its ID. Sets 'isDeleted' to True instead of removing the document.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        item = self.get(item_id)
        item["isDeleted"] = True
        value = self.client.upsert_item(item)

        if run_indexer:
            self.run_indexer()
        return value

    def run_indexer(self):
        """
        Trigger the Azure Search indexer for this container (examples, guidelines, or memories).
        """
        settings = SettingsManager()
        indexer_name = f"{self.container_name}-indexer"
        search_endpoint = settings.get("SEARCH_ENDPOINT")
        client = SearchIndexerClient(endpoint=search_endpoint, credential=get_credential())
        status = client.get_indexer_status(indexer_name)
        if status.status in ["inProgress"]:
            return
        client.run_indexer(indexer_name)


class GuidelinesContainer(BasicContainer):
    """Container client for guidelines operations."""

    def __init__(self, manager: DatabaseManager, container_name: str):
        super().__init__(manager, container_name)
        self.preprocess_id = lambda x: x.replace(".html#", "=html=")


class ReviewJobsContainer(BasicContainer):
    """Container client for review jobs operations."""

    def cleanup_old_jobs(self, retention_seconds):
        """
        Clean up old review jobs that have a 'finished' timestamp older than the specified retention period
        """
        now = time.time()
        query = (
            f"SELECT c.id, c.finished FROM c WHERE IS_DEFINED(c.finished) AND c.finished < {now - retention_seconds}"
        )
        for item in self.client.query_items(query=query, enable_cross_partition_query=True):
            self.delete(item["id"])

    def run_indexer(self):
        # reviews_jobs container does not have an indexer
        pass


class MetricsContainer(BasicContainer):
    """Container client for metrics operations."""

    def run_indexer(self):
        # metrics container does not have an indexer
        pass


class EvalsContainer(BasicContainer):
    """Container client for evaluations operations."""

    def run_indexer(self):
        # evaluations container does not have an indexer
        pass


@lru_cache()
def get_database_manager():
    """Get a singleton instance of the DatabaseManager."""
    return DatabaseManager.get_instance()
