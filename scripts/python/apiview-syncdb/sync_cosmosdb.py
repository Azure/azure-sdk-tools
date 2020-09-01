import sys
import argparse
import os
import logging
import traceback
from azure.cosmos import CosmosClient

logging.getLogger().setLevel(logging.INFO)
http_logger = logging.getLogger("azure.core.pipeline.policies.http_logging_policy")
http_logger.setLevel(logging.WARNING)

# Script to sync cosmosdb from APIView production instance into APIView staging instance.
# This script identifies missing records by fetching ID and partitionKey from all containers in source and destination DB
# and identify ID of missing records. Script read record from source and insert into destination DB for all missing records.


COSMOS_SELECT_ID_PARTITIONKEY_QUERY = "select c.id, c.{0} as partitionKey from {1} c"
COSMOS_SELECT_WHERE_ID_CLAUSE = "select * from {0} c where c.id='{1}'"

# Create cosmosdb clients
def get_db_clients(source_url, dest_url, source_key, dest_key, db_name):
    # Create cosmosdb client for source db
    source_cosmos_client = CosmosClient(source_url, credential=source_key)
    if not source_cosmos_client:
        logging.error("Failed to create cosmos client for source db")
        exit(1)

    # Create cosmosdb client for destination db
    dest_cosmos_client = CosmosClient(dest_url, credential=dest_key)
    if not dest_cosmos_client:
        logging.error("Failed to create cosmos client for destination db")
        exit(1)

    logging.info("Created cosmos client for source and destination cosmosdb")
    # Create database client object using CosmosClient
    src_db_client = None
    dest_db_client = None
    try:
        src_db_client = source_cosmos_client.get_database_client(db_name)
        dest_db_client = dest_cosmos_client.get_database_client(db_name)
        logging.info("Created database clients")
    except:
        logging.error("Failed to create databae client using CosmosClient")
        traceback.print_exc()
        exit(1)
    return src_db_client, dest_db_client


# Copy all records in containers from source cosmosDB to destination DB
def sync_database(src_db_client, dest_db_client):
    # Find containers in source cosmosDB
    container_names = []
    try:
        container_names = [c["id"] for c in src_db_client.list_containers()]
    except:
        logging.error("Failed to get container list from cosmosDB client")
        traceback.print_exc()

    if not container_names:
        logging.error(
            "Container is not found in source cosmosDB. Please check database name parameter"
        )
        exit(1)

    # Sync records for each containers
    for container in container_names:
        # Sync records in container
        logging.info("Syncing records in containers:{}".format(container))
        sync_database_container(src_db_client, dest_db_client, container)


# This function fetches records in both source and destination DB and
# identifies missing records in destination side
# One future enhancement is to use point in time reference to fetch only new data
def sync_database_container(src_db_client, dest_db_client, container_name):
    # Find records and insert missing records into dest container
    src_container_client = None
    dest_container_client = None
    try:
        src_container_client = src_db_client.get_container_client(container_name)
        dest_container_client = dest_db_client.get_container_client(container_name)
    except:
        logging.error("Failed to get container client for {}".format(container_name))
        traceback.print_exc()
        exit(1)

    source_records = fetch_records(src_container_client, container_name)
    dest_records = fetch_records(dest_container_client, container_name)
    missing_records = dict(
        [(x, source_records[x]) for x in source_records.keys() if x not in dest_records]
    )
    if missing_records:
        logging.info(
            "Found {} missing rows in destination DB".format(len(missing_records))
        )
        logging.info("Copying missing records....")
        copy_missing_records(
            src_container_client, dest_container_client, missing_records, container_name
        )
        logging.info(
            "Records in cosmosdb source container {} is synced successfully to destination container.".format(
                container_name
            )
        )
    else:
        logging.info("Destionation DB container is in sync with source cosmosDB")


# Fetch records in a database container from given client
def fetch_records(container_client, container_name):

    records = {}
    try:
        # Find partition key in container
        container_props = container_client.read()
        # Get partition key from cosmos container properties to be used in read_item request
        partitionKey = container_props["partitionKey"]["paths"][0][1:]
        query_string = COSMOS_SELECT_ID_PARTITIONKEY_QUERY.format(
            partitionKey, container_name
        )
        logging.debug("query string: {}".format(query_string))

        # Fetch and create a map of ID and row
        # This map will be helpful in finding missing rows to insert into destination DB
        for row in container_client.query_items(
            query=query_string, enable_cross_partition_query=True
        ):
            records[row["id"]] = row["partitionKey"]
    except:
        logging.error("Failed to query database")
        traceback.print_exc()
        exit(1)
    return records


# Method to fetch row for each missing id from source and insert into destination db
def copy_missing_records(
    src_container_client, dest_container_client, missing_records, container_name
):

    for row_id in missing_records:
        logging.debug("Copying '{0}'".format(row_id))
        insert_row(
            dest_container_client,
            container_name,
            get_row(
                src_container_client, container_name, row_id, missing_records[row_id]
            ),
        )


# Read a row using row ID and partition key
def get_row(container_client, container_name, row_id, partitionKey):
    try:
        return container_client.read_item(row_id, partitionKey)
    except:
        logging.error(
            "Failed to read row with {0} from container {1}".format(
                row_id, container_name
            )
        )
        traceback.print_exc()
        exit(1)


# Insert row into container
def insert_row(container_client, container_name, row):
    try:
        container_client.upsert_item(row)
    except:
        logging.error(
            "Failed to insert row with {0} to container {1}".format(
                row["id"], container_name
            )
        )
        traceback.print_exc()
        exit(1)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Sync Azure cosmosDB from source DB instance to destination DB instance"
    )

    parser.add_argument(
        "--source_url",
        required=True,
        help=("URL to source cosmosdb"),
    )
    parser.add_argument(
        "--source_key",
        required=True,
        help=("Source cosmosdb account key"),
    )
    parser.add_argument(
        "--dest_url",
        required=True,
        help=("URL to destination cosmosdb"),
    )
    parser.add_argument(
        "--dest_key",
        required=True,
        help=("Destination cosmosdb account key"),
    )
    parser.add_argument(
        "--db_name",
        required=True,
        help=("Database name in cosmosdb"),
    )

    args = parser.parse_args()

    logging.info("Creating cosmosDB clients...")
    src_db_client, dest_db_client = get_db_clients(
        args.source_url, args.dest_url, args.source_key, args.dest_key, args.db_name
    )
    logging.info("Syncing database..")
    sync_database(src_db_client, dest_db_client)
