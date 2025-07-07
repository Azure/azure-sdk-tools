from azure.identity import DefaultAzureCredential
from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosResourceNotFoundError

from functools import lru_cache
import os
from pydantic import BaseModel
import time


class DatabaseManager:
    def __init__(self, endpoint: str, db_name: str, credential: DefaultAzureCredential):
        self.client = CosmosClient(endpoint, credential=credential)
        self.database = self.client.get_database_client(db_name)
        self.containers = {}

    def get_container_client(self, name: str) -> "BasicContainer":
        # Decide which container class to use
        if name not in self.containers:
            if name == "review_jobs":
                self.containers[name] = ReviewJobsContainer(self, name)
            elif name == "guidelines":
                self.containers[name] = GuidelinesContainer(self, name)
            else:
                self.containers[name] = BasicContainer(self, name)
        return self.containers[name]

    @property
    def guidelines(self):
        return self.get_container_client("guidelines")

    @property
    def memories(self):
        return self.get_container_client("memories")

    @property
    def examples(self):
        return self.get_container_client("examples")

    @property
    def review_jobs(self):
        return self.get_container_client("review_jobs")


class BasicContainer:
    def __init__(self, manager: DatabaseManager, container_name: str):
        self.client = manager.database.get_container_client(container_name)
        self.preprocess_id = None  # Optional preprocessing function for item IDs

    def _to_dict(self, data):
        if BaseModel and isinstance(data, BaseModel):
            return data.model_dump()
        return dict(data) if not isinstance(data, dict) else data

    def create(self, item_id: str, data):
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
            return {"status": "created", "id": data_dict["id"]}

    def upsert(self, item_id: str, *, data):
        """
        Upsert an item. If it exists, update it; if not, create it.
        """
        if self.preprocess_id:
            item_id = self.preprocess_id(item_id)
        data_dict = self._to_dict(data)
        return self.client.upsert_item({"id": item_id, **data_dict})

    def get(self, item_id: str):
        """
        Get an item by its ID. If preprocess_id is provided, it will be applied to the item_id before fetching.
        """
        if self.preprocess_id:
            item_id = self.preprocess_id(item_id)
        return self.client.read_item(item=item_id, partition_key=item_id)

    def delete(self, item_id: str):
        """
        Delete an item by its ID. Returns the response from the delete operation.
        """
        if self.preprocess_id:
            item_id = self.preprocess_id(item_id)
        return self.client.delete_item(item=item_id, partition_key=item_id)


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
        for item in self.container.query_items(query=query, enable_cross_partition_query=True):
            self.delete(item["id"])


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
