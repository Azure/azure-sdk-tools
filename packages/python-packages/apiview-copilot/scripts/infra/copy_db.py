# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import sys
from typing import Dict, Iterable, List

from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosResourceNotFoundError
from azure.identity import DefaultAzureCredential

SYSTEM_FIELDS = {"_rid", "_self", "_etag", "_attachments", "_ts"}

# ---- HARDCODED CONFIGURATION ----
SRC_ENDPOINT = "https://avc-cosmos-staging.documents.azure.com"
DST_ENDPOINT = "https://avc-cosmos.documents.azure.com"
SRC_DATABASE_NAME = "avc-db"
DST_DATABASE_NAME = "avc-db"
CONTAINERS = ["guidelines", "examples", "memories"]  # Specify containers here
PAGE_SIZE = 1000
BATCH_SIZE = 200
# ---------------------------------


def get_client(endpoint: str) -> CosmosClient:
    """
    Create a CosmosClient using Entra ID (AAD) with DefaultAzureCredential.
    RBAC scope must include the account (Cosmos DB Built-in Data Contributor/Reader).
    """
    # For AAD, the cosmos SDK will request a token for scope "https://cosmos.azure.com/.default"
    credential = DefaultAzureCredential()
    return CosmosClient(endpoint, credential=credential)


def clean_item(doc: Dict) -> Dict:
    """Remove read-only/system fields that cannot be written back."""
    return {k: v for k, v in doc.items() if k not in SYSTEM_FIELDS}


def iter_items_with_ts(container, page_size: int = 1000) -> Iterable[Dict]:
    """
    Stream all items from a container using a cross-partition query.
    """
    # SELECT c.id, c._ts FROM c to only fetch id and _ts fields for indexing
    iterator = container.query_items(
        query="SELECT c.id, c._ts FROM c",
        enable_cross_partition_query=True,
        max_item_count=page_size,
    )
    for item in iterator:
        yield item


def iter_full_items(container, page_size: int = 1000) -> Iterable[Dict]:
    """
    Stream all items from a container using a cross-partition query.
    """
    # SELECT * FROM c avoids server-side projection issues; SDK paginates client-side
    iterator = container.query_items(
        query="SELECT * FROM c",
        enable_cross_partition_query=True,
        max_item_count=page_size,
    )
    for item in iterator:
        yield item


def upsert_batch(dst_container, batch: List[Dict]):
    """Upsert a batch of items into the destination container."""
    for doc in batch:
        dst_container.upsert_item(doc)


def get_id_to_ts(container) -> Dict[str, int]:
    """Return a mapping of id -> _ts for all docs in the container."""
    id_to_ts = {}
    for item in iter_items_with_ts(container):
        id_to_ts[item["id"]] = item["_ts"]
    return id_to_ts


def copy_container_incremental(
    src_client: CosmosClient,
    dst_client: CosmosClient,
    src_database_name: str,
    dst_database_name: str,
    container_name: str,
    page_size: int = 1000,
    batch_size: int = 200,
) -> int:
    src_db = src_client.get_database_client(src_database_name)
    dst_db = dst_client.get_database_client(dst_database_name)
    src_container = src_db.get_container_client(container_name)
    dst_container = dst_db.get_container_client(container_name)

    # Build id -> _ts maps for both source and dest
    print("   Indexing destination IDs and timestamps...")
    dst_id_to_ts = get_id_to_ts(dst_container)

    copied = 0
    batch: List[Dict] = []
    print("   Scanning source for new/updated docs...")
    for item in iter_full_items(src_container, page_size=page_size):
        doc = clean_item(item)
        doc_id = doc.get("id")
        src_ts = item.get("_ts")
        dst_ts = dst_id_to_ts.get(doc_id)
        # Only upsert if new or updated
        if dst_ts is None or (src_ts is not None and src_ts > dst_ts):
            batch.append(doc)
        if len(batch) >= batch_size:
            upsert_batch(dst_container, batch)
            copied += len(batch)
            batch.clear()
    if batch:
        upsert_batch(dst_container, batch)
        copied += len(batch)
    return copied


def main():
    if not CONTAINERS or len(CONTAINERS) == 0:
        print("ERROR: You must specify at least one container in CONTAINERS.")
        sys.exit(1)
    src_client = get_client(SRC_ENDPOINT)
    dst_client = get_client(DST_ENDPOINT)
    print(f"Incremental sync of containers: {CONTAINERS}")
    total = 0
    for c in CONTAINERS:
        print(f"\n→ Syncing container '{c}' ...")
        try:
            count = copy_container_incremental(
                src_client,
                dst_client,
                SRC_DATABASE_NAME,
                DST_DATABASE_NAME,
                c,
                page_size=PAGE_SIZE,
                batch_size=BATCH_SIZE,
            )
            total += count
            print(f"   Upserted {count} new/updated documents.")
        except CosmosResourceNotFoundError:
            print(f"   ERROR: Container '{c}' not found in source or destination. Ensure it exists in BOTH accounts.")
        except Exception as e:
            print(f"   ERROR syncing '{c}': {e}")
    print(f"\nDone. Total new/updated documents upserted: {total}")


if __name__ == "__main__":
    main()
