from enum import Enum
from functools import lru_cache
import os
from pydantic import BaseModel
import time

from azure.identity import ChainedTokenCredential
from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosResourceNotFoundError
from azure.search.documents.indexes import SearchIndexerClient

from src._credential import get_credential


class ContainerNames(Enum):
    GUIDELINES = "guidelines"
    MEMORIES = "memories"
    EXAMPLES = "examples"
    REVIEW_JOBS = "review-jobs"

    @classmethod
    def values(cls) -> list[str]:
        return [name.value for name in cls]


class DatabaseManager:
    def __init__(self, endpoint: str, db_name: str, credential: ChainedTokenCredential):
        self.client = CosmosClient(endpoint, credential=credential)
        self.database = self.client.get_database_client(db_name)
        self.containers = {}

    def get_container_client(self, name: str) -> "BasicContainer":
        # Decide which container class to use
        if name not in self.containers:
            if name == ContainerNames.REVIEW_JOBS.value:
                self.containers[name] = ReviewJobsContainer(self, name)
            elif name == ContainerNames.GUIDELINES.value:
                self.containers[name] = GuidelinesContainer(self, name)
            else:
                self.containers[name] = BasicContainer(self, name)
        return self.containers[name]

    @property
    def guidelines(self):
        return self.get_container_client(ContainerNames.GUIDELINES.value)

    @property
    def memories(self):
        return self.get_container_client(ContainerNames.MEMORIES.value)

    @property
    def examples(self):
        return self.get_container_client(ContainerNames.EXAMPLES.value)

    @property
    def review_jobs(self):
        return self.get_container_client(ContainerNames.REVIEW_JOBS.value)


class BasicContainer:
    def __init__(self, manager: DatabaseManager, container_name: str):
        self.client = manager.database.get_container_client(container_name)
        self.preprocess_id = None
        self.container_name = container_name

    def _to_dict(self, data):
        if BaseModel and isinstance(data, BaseModel):
            return data.model_dump()
        return dict(data) if not isinstance(data, dict) else data

    def create(self, item_id: str, *, data):
        """
        Create a new item. Raises an error if the item already exists.
        """
        if self.preprocess_id:
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
            self.run_indexer()
            return value

    def upsert(self, item_id: str, *, data):
        """
        Upsert an item. If it exists, update it; if not, create it.
        """
        if self.preprocess_id:
            item_id = self.preprocess_id(item_id)
        data_dict = self._to_dict(data)
        value = self.client.upsert_item({"id": item_id, **data_dict})
        self.run_indexer()
        return value

    def get(self, item_id: str):
        """
        Get an item by its ID. If preprocess_id is provided, it will be applied to the item_id before fetching.
        """
        if self.preprocess_id:
            item_id = self.preprocess_id(item_id)
        return self.client.read_item(item=item_id, partition_key=item_id)

    def delete(self, item_id: str):
        """
        Soft-delete an item by its ID. Sets 'isDeleted' to True instead of removing the document.
        """
        if self.preprocess_id:
            item_id = self.preprocess_id(item_id)
        item = self.get(item_id)
        item["isDeleted"] = True
        value = self.client.upsert_item(item)
        self.run_indexer()
        return value

    def run_indexer(self):
        """
        Trigger the Azure Search indexer for this container (examples, guidelines, or memories).
        """
        indexer_name = f"{self.container_name}-indexer"
        search_service = os.environ.get("AZURE_SEARCH_NAME")
        if not search_service:
            raise RuntimeError("AZURE_SEARCH_NAME not set in environment.")
        endpoint = f"https://{search_service}.search.windows.net"
        client = SearchIndexerClient(endpoint=endpoint, credential=get_credential())
        status = client.get_indexer_status(indexer_name)
        if status.status in ["inProgress"]:
            return
        client.run_indexer(indexer_name)


class GuidelinesContainer(BasicContainer):
    def __init__(self, manager: DatabaseManager, container_name: str):
        super().__init__(manager, container_name)
        self.preprocess_id = lambda x: x.replace(".html#", "=html=")


class ReviewJobsContainer(BasicContainer):
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


@lru_cache()
def get_database_manager():
    from src._credential import get_credential

    acc_name = os.environ.get("AZURE_COSMOS_ACC_NAME")
    db_name = os.environ.get("AZURE_COSMOS_DB_NAME")
    endpoint = f"https://{acc_name}.documents.azure.com:443/"
    if not acc_name or not db_name:
        raise ValueError(
            "Missing Azure Cosmos DB configuration. Set AZURE_COSMOS_ACC_NAME and AZURE_COSMOS_DB_NAME environment variables."
        )
    credential = get_credential()
    return DatabaseManager(endpoint, db_name, credential)
