from azure.identity import DefaultAzureCredential
from azure.cosmos import CosmosClient

from functools import lru_cache
import os
from pydantic import BaseModel
import time


class DatabaseManager:
    def __init__(self, endpoint: str, db_name: str, credential: DefaultAzureCredential):
        self.client = CosmosClient(endpoint, credential=credential)
        self.database = self.client.get_database_client(db_name)
        self.containers = {}

    def _get_container(self, name: str, container_cls: type = "BasicContainer"):
        if name not in self.containers:
            self.containers[name] = container_cls(self.database, name)
        return self.containers[name]

    @property
    def guidelines(self):
        return self._get_container("guidelines")

    @property
    def memories(self):
        return self._get_container("memories")

    @property
    def examples(self):
        return self._get_container("examples")

    @property
    def review_jobs(self):
        return self._get_container("review_jobs", ReviewJobsContainer)


class BasicContainer:
    def __init__(self, database: DatabaseManager, container_name: str):
        self.container = database._get_container(container_name)

    def _to_dict(self, data):
        # Accepts dict or Pydantic model
        if BaseModel and isinstance(data, BaseModel):
            return data.model_dump()
        return dict(data) if not isinstance(data, dict) else data

    def insert(self, item_id: str, data):
        data_dict = self._to_dict(data)
        return self.container.create_item({"id": item_id, **data_dict})

    def upsert(self, item_id: str, data):
        data_dict = self._to_dict(data)
        return self.container.upsert_item({"id": item_id, **data_dict})

    def get(self, item_id: str):
        return self.container.read_item(item=item_id, partition_key=item_id)

    def delete(self, item_id: str):
        return self.container.delete_item(item=item_id, partition_key=item_id)


class ReviewJobsContainer(BasicContainer):
    def cleanup_old_jobs(self, retention_seconds):
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
