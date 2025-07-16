import os
import time

from azure.cosmos import CosmosClient
from azure.search.documents import SearchClient
from azure.search.documents.indexes import SearchIndexerClient
from src._credential import get_credential


class GarbageCollector:
    def __init__(self):
        credential = get_credential()
        required_env_vars = ["AZURE_COSMOS_ACC_NAME", "AZURE_COSMOS_DB_NAME", "AZURE_SEARCH_NAME"]
        missing = [var for var in required_env_vars if os.environ.get(var) is None]
        if missing:
            raise ValueError(f"Missing required environment variables: {', '.join(missing)}")
        cosmos_acc_name = os.environ.get("AZURE_COSMOS_ACC_NAME")
        cosmos_db_name = os.environ.get("AZURE_COSMOS_DB_NAME")
        cosmos_endpoint = f"https://{cosmos_acc_name}.documents.azure.com:443/"
        search_name = os.environ.get("AZURE_SEARCH_NAME")
        search_endpoint = f"https://{search_name}.search.windows.net"
        search_index_name = os.environ.get("AZURE_SEARCH_INDEX_NAME", "archagent-index")
        self.cosmos_client = CosmosClient(cosmos_endpoint, credential=credential)
        self.database = self.cosmos_client.get_database_client(cosmos_db_name)
        self.search_client = SearchClient(search_endpoint, search_index_name, credential=credential)
        self.search_indexer_client = SearchIndexerClient(search_endpoint, credential=credential)
        self.soft_delete_field = "isDeleted"

    def collect_garbage(self, container_name: str):
        container = self.database.get_container_client(container_name)
        # Query for soft-deleted items
        query = f"SELECT * FROM c WHERE c.{self.soft_delete_field} = true"
        items = list(container.query_items(query=query, enable_cross_partition_query=True))
        for item in items:
            item_id = item.get("id")
            # Check if item is still in Azure Search
            search_results = self.search_client.search(f"id eq '{item_id}'")
            if not any(search_results):
                # Hard delete from Cosmos
                container.delete_item(item, partition_key=item["partitionKey"])
                print(f"Hard deleted item {item_id} from Cosmos DB.")
            else:
                print(f"Item {item_id} still exists in Azure Search. Skipping hard delete.")

    def run_indexer_and_cleanup(self, container_name: str):
        indexer_name = f"{container_name}-indexer"
        status = self.search_indexer_client.get_indexer_status(indexer_name)
        if status.status != "inProgress":
            self.search_indexer_client.run_indexer(indexer_name)
        # Wait for indexer to complete
        while True:
            status = self.search_indexer_client.get_indexer_status(indexer_name)
            state = getattr(status, "status", None)
            if state not in ("inProgress"):
                break
            print(f"Indexer '{indexer_name}' still inProgress...")
            time.sleep(5)
        print(f"Indexer '{indexer_name}' completed with status: {state}")
        self.collect_garbage(container_name)
