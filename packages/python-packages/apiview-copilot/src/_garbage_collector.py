import time

from azure.cosmos import CosmosClient
from azure.search.documents import SearchClient
from azure.search.documents.indexes import SearchIndexerClient
from src._credential import get_credential
from src._settings import SettingsManager


class GarbageCollector:
    """Handles the proper deletion of items using soft-delete."""

    def __init__(self):
        settings = SettingsManager()
        credential = get_credential()
        cosmos_endpoint = settings.get("COSMOS_ENDPOINT")
        cosmos_db_name = settings.get("COSMOS_DB_NAME")
        search_endpoint = settings.get("SEARCH_ENDPOINT")
        search_index_name = settings.get("SEARCH_INDEX_NAME")
        self.cosmos_client = CosmosClient(cosmos_endpoint, credential=credential)
        self.database = self.cosmos_client.get_database_client(cosmos_db_name)
        self.search_client = SearchClient(search_endpoint, search_index_name, credential=credential)
        self.search_indexer_client = SearchIndexerClient(search_endpoint, credential=credential)
        self.soft_delete_field = "isDeleted"

    def purge_items(self, container_name: str):
        """Permanently delete items marked as soft-deleted and not present in Azure Search."""
        container = self.database.get_container_client(container_name)
        # Query for soft-deleted items
        query = f"SELECT * FROM c WHERE c.{self.soft_delete_field} = true"
        items = list(container.query_items(query=query, enable_cross_partition_query=True))
        for item in items:
            item_id = item.get("id")
            # check if document still exists in Azure Search
            try:
                self.search_client.get_document(key=item_id)
                print(f"Item {item_id} still exists in Azure Search. Skipping hard delete.")
            except Exception:
                # Not found in search, safe to hard delete
                partition_key = item.get("partitionKey") or item.get("id")
                container.delete_item(item, partition_key=partition_key)

    def get_item_count(self, container_name: str) -> int:
        """Return the total number of items in the specified container."""
        container = self.database.get_container_client(container_name)
        result = container.query_items(query="SELECT VALUE COUNT(1) FROM c", enable_cross_partition_query=True)
        return next(result)

    def run_indexer_and_purge(self, container_name: str):
        """Run the search indexer and purge soft-deleted items only after indexer is truly finished."""
        indexer_name = f"{container_name}-indexer"
        status = self.search_indexer_client.get_indexer_status(indexer_name)
        if status.status != "inProgress":
            self.search_indexer_client.run_indexer(indexer_name)
        # Wait for indexer to complete (success, reset, or error, but not running)
        while True:
            status = self.search_indexer_client.get_indexer_status(indexer_name)
            state = getattr(status, "status", None)
            if state != "inProgress":
                break
            print(f"Indexer '{indexer_name}' still in progress (status: {state})...")
            time.sleep(5)
        print(f"Indexer '{indexer_name}' completed with status: {state}")
        self.purge_items(container_name)
