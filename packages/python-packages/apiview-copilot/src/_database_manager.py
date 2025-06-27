import os
from azure.cosmos import CosmosClient
import logging
import uuid
import time

from src._credential import get_credential

COSMOS_ACC_NAME = os.environ.get("AZURE_COSMOS_ACC_NAME")
COSMOS_DB_NAME = os.environ.get("AZURE_COSMOS_DB_NAME")
COSMOS_ENDPOINT = f"https://{COSMOS_ACC_NAME}.documents.azure.com:443/"
CREDENTIAL = get_credential()

logger = logging.getLogger("uvicorn")


class DatabaseManager:
    def __init__(self):
        self._ensure_env_vars(["AZURE_COSMOS_ACC_NAME", "AZURE_COSMOS_DB_NAME"])
        self.client = CosmosClient(COSMOS_ENDPOINT, credential=CREDENTIAL)

    def _ensure_env_vars(self, var_names):
        missing = [var for var in var_names if not os.environ.get(var)]
        if missing:
            raise EnvironmentError(f"Missing required environment variables: {', '.join(missing)}")

    def insert_job(self, job_id, job_data):
        database = self.client.get_database_client(COSMOS_DB_NAME)
        container = database.get_container_client("review-jobs")

        item = {"id": job_id, **job_data}
        container.create_item(body=item)

    def upsert_job(self, job_id, job_data):
        database = self.client.get_database_client(COSMOS_DB_NAME)
        container = database.get_container_client("review-jobs")

        item = {"id": job_id, **job_data}
        container.upsert_item(body=item)

    def get_job(self, job_id):
        try:
            database = self.client.get_database_client(COSMOS_DB_NAME)
            container = database.get_container_client("review-jobs")
            return container.read_item(item=job_id, partition_key=job_id)
        except Exception as e:
            logger.error(f"Error fetching job {job_id}: {e}")
            return None

    def delete_job(self, job_id):
        try:
            database = self.client.get_database_client(COSMOS_DB_NAME)
            container = database.get_container_client("review-jobs")
            container.delete_item(item=job_id, partition_key=job_id)
        except Exception as e:
            logger.error(f"Error deleting job {job_id}: {e}")

    def cleanup_old_jobs(self, retention_seconds):
        database = self.client.get_database_client(COSMOS_DB_NAME)
        container = database.get_container_client("review-jobs")
        now = time.time()
        query = (
            f"SELECT c.id, c.finished FROM c WHERE IS_DEFINED(c.finished) AND c.finished < {now - retention_seconds}"
        )
        for item in container.query_items(query=query, enable_cross_partition_query=True):
            self.delete_job(item["id"])


# Singleton instance
_db_manager = None


def get_database_manager():
    global _db_manager
    if _db_manager is None:
        _db_manager = DatabaseManager()
    return _db_manager
