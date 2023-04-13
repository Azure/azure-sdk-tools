import sys
import argparse
import codecs
from datetime import datetime
import json
import os
import logging
import traceback
from ast import literal_eval
from azure.cosmos import CosmosClient
from azure.storage.blob import BlobServiceClient

logging.getLogger().setLevel(logging.INFO)
http_logger = logging.getLogger("azure.core.pipeline.policies.http_logging_policy")
http_logger.setLevel(logging.WARNING)

# Script to sync cosmosdb from APIView production instance into APIView staging instance.
# This script identifies missing records by fetching ID and partitionKey from all containers in source and destination DB
# and identify ID of missing records. Script read record from source and insert into destination DB for all missing records.


COSMOS_SELECT_ID_PARTITIONKEY_QUERY = "select c.id, c.{0} as partitionKey, c._ts from {1} c"

COSMOS_CONTAINERS = ["Reviews", "Comments", "PullRequests"]
BACKUP_CONTAINER = "backups"
BLOB_NAME_PATTERN ="cosmos/{0}/{1}"



def restore_data_from_backup(connection_string, dest_url, dest_key, db_name):    

    dest_db_client = get_db_client(dest_url, dest_key, db_name)
    
    blob_service_client = BlobServiceClient.from_connection_string(connection_string)
    container_client = blob_service_client.get_container_client(BACKUP_CONTAINER)
    for cosmos_container_name in COSMOS_CONTAINERS:
        # Load source records from backup file
        source_contents = get_backup_contents(container_client, "{}.json".format(cosmos_container_name))
        logging.info("Number of records in {0} backup:{1}".format(cosmos_container_name, len(source_contents)))

        # get records from destination DB
        dest_container_client = dest_db_client.get_container_client(cosmos_container_name)
        dest_records = fetch_records(dest_container_client, cosmos_container_name)

        # find missing or updated records
        # updated records has new timestamp value in column(_ts)
        missing_records = [
            x for x in source_contents if x['id'] not in dest_records or x['_ts'] > dest_records[x['id']][1]
        ]
        if missing_records:
            logging.info(
                "Found {} missing/updated rows in source DB".format(len(missing_records))
            )
            for row in missing_records:
                dest_container_client.upsert_item(row)
            logging.info("Records in cosmosdb source container {} is synced successfully to destination container.".format(cosmos_container_name))
        else:
            logging.info("Destination DB container is in sync with source cosmosDB")


def get_backup_contents(container_client, blob_name):
    backup_date = datetime.now().strftime("%y%m%d")
    blob_path = BLOB_NAME_PATTERN.format(backup_date, blob_name)
    contents = codecs.decode(container_client.get_blob_client(blob_path).download_blob().content_as_bytes(), 'utf-8-sig')
    records = json.loads(contents)
    return records
     


# Create cosmosdb clients
def get_db_client(dest_url, dest_key, db_name):

    # Create cosmosdb client for destination db
    dest_cosmos_client = CosmosClient(dest_url, credential=dest_key)
    if not dest_cosmos_client:
        logging.error("Failed to create cosmos client for destination db")
        exit(1)

    logging.info("Created cosmos client for destination cosmosdb")
    # Create database client object using CosmosClient
    dest_db_client = None
    try:
        dest_db_client = dest_cosmos_client.get_database_client(db_name)
        logging.info("Created database clients")
    except:
        logging.error("Failed to create databae client using CosmosClient")
        traceback.print_exc()
        exit(1)
    return dest_db_client


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
            records[row["id"]] = row["partitionKey"], row["_ts"]
    except:
        logging.error("Failed to query database")
        traceback.print_exc()
        exit(1)
    return records


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Sync Azure cosmosDB from source DB instance to destination DB instance"
    )

    parser.add_argument(
        "--backup-connection-string",
        required=True,
        help=("Connection string to backup storage account"),
    )
    parser.add_argument(
        "--dest-url",
        required=True,
        help=("URL to destination cosmosdb"),
    )
    parser.add_argument(
        "--dest-key",
        required=True,
        help=("Destination cosmosdb account key"),
    )
    parser.add_argument(
        "--db-name",
        required=True,
        help=("Database name in cosmosdb"),
    )

    args = parser.parse_args()

    logging.info("Syncing database..")
    restore_data_from_backup(args.backup_connection_string, args.dest_url, args.dest_key, args.db_name)