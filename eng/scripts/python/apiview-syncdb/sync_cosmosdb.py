import sys
import argparse
import codecs
from datetime import datetime
import json
import os
import logging
import traceback
from ast import literal_eval
from concurrent.futures import ThreadPoolExecutor, as_completed
from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosHttpResponseError
from azure.identity import AzurePowerShellCredential, ChainedTokenCredential, AzureCliCredential
from azure.storage.blob import BlobServiceClient
import time

logging.getLogger().setLevel(logging.INFO)
http_logger = logging.getLogger("azure.core.pipeline.policies.http_logging_policy")
http_logger.setLevel(logging.WARNING)

# Script to sync cosmosdb from APIView production instance into APIView staging instance.
# This script identifies missing records by fetching ID and partitionKey from all containers in source and destination DB
# and identify ID of missing records. Script read record from source and insert into destination DB for all missing records.


COSMOS_SELECT_ID_PARTITIONKEY_QUERY = "select c.id, c.{0} as partitionKey, c._ts from {1} c"

# Process smaller containers first to ensure they sync before timeout
# Reordered from largest to smallest based on typical record counts
COSMOS_CONTAINERS = ["Permissions", "Projects", "PullRequests", "SamplesRevisions", "Comments", "Reviews", "APIRevisions"]
BACKUP_CONTAINER = "backups"
BLOB_NAME_PATTERN ="cosmos/{0}/{1}"

# Create a AzurePowerShellCredential()
credential_chain = ChainedTokenCredential(AzureCliCredential(), AzurePowerShellCredential())

def restore_data_from_backup(backup_storage_url, dest_url, db_name):

    dest_db_client = get_db_client(dest_url, db_name)
    
    blob_service_client = BlobServiceClient(backup_storage_url, credential = credential_chain)
    container_client = blob_service_client.get_container_client(BACKUP_CONTAINER)
    
    total_containers = len(COSMOS_CONTAINERS)
    for idx, cosmos_container_name in enumerate(COSMOS_CONTAINERS, 1):
        logging.info("=" * 80)
        logging.info("Processing container {}/{}: {}".format(idx, total_containers, cosmos_container_name))
        logging.info("=" * 80)
        
        # Load source records from backup file
        source_contents = get_backup_contents(container_client, "{}.json".format(cosmos_container_name))
        logging.info("Number of records in {0} backup: {1}".format(cosmos_container_name, len(source_contents)))

        # get records from destination DB
        dest_container_client = dest_db_client.get_container_client(cosmos_container_name)
        dest_records = fetch_records(dest_container_client, cosmos_container_name)
        logging.info("Number of existing records in destination {0}: {1}".format(cosmos_container_name, len(dest_records)))

        # find missing or updated records
        # updated records has new timestamp value in column(_ts)
        # Note: _ts in backup may be stored as string or int, so we convert both to int for comparison
        missing_records = []
        for x in source_contents:
            record_id = x.get('id')
            if not record_id:
                logging.warning("Record without 'id' field found, skipping")
                continue
            
            source_ts = x.get('_ts', 0)
            # Handle _ts that might be string or int
            if isinstance(source_ts, str):
                try:
                    source_ts = int(source_ts)
                except ValueError:
                    source_ts = 0
            
            if record_id not in dest_records:
                missing_records.append(x)
            else:
                dest_ts = dest_records[record_id][1]
                if isinstance(dest_ts, str):
                    try:
                        dest_ts = int(dest_ts)
                    except ValueError:
                        dest_ts = 0
                if source_ts > dest_ts:
                    missing_records.append(x)
        if missing_records:
            logging.info("Found {} missing/updated rows to sync".format(len(missing_records)))
            
            # Use parallel upserts for better performance
            # ThreadPoolExecutor allows concurrent upsert operations to Cosmos DB
            max_workers = 20  # Number of parallel threads - increased for better throughput
            batch_size = 100  # Log progress every N records
            total_records = len(missing_records)
            upserted_count = 0
            failed_count = 0
            max_retries = 3  # Number of retries for transient failures
            
            def upsert_item_with_retry(item, retries=max_retries):
                """Helper function to upsert a single item with retry logic"""
                for attempt in range(retries):
                    try:
                        dest_container_client.upsert_item(item)
                        return True
                    except CosmosHttpResponseError as e:
                        if e.status_code == 429:  # Too Many Requests - throttling
                            # Wait with exponential backoff
                            retry_after = float(e.headers.get('x-ms-retry-after-ms', 1000)) / 1000
                            time.sleep(retry_after * (2 ** attempt))
                            continue
                        elif e.status_code >= 500:  # Server error - retry
                            time.sleep(1 * (2 ** attempt))
                            continue
                        else:
                            logging.error("Failed to upsert item {}: {} (status: {})".format(
                                item.get('id', 'unknown'), str(e), e.status_code))
                            return False
                    except Exception as e:
                        logging.error("Failed to upsert item {}: {}".format(item.get('id', 'unknown'), str(e)))
                        return False
                logging.error("Failed to upsert item {} after {} retries".format(item.get('id', 'unknown'), retries))
                return False
            
            # Execute upserts in parallel using ThreadPoolExecutor
            with ThreadPoolExecutor(max_workers=max_workers) as executor:
                # Submit all upsert tasks
                future_to_item = {executor.submit(upsert_item_with_retry, record): record for record in missing_records}
                
                # Process completed tasks and log progress
                for future in as_completed(future_to_item):
                    if future.result():
                        upserted_count += 1
                    else:
                        failed_count += 1
                    
                    processed = upserted_count + failed_count
                    if processed % batch_size == 0:
                        logging.info("Progress: {}/{} records processed ({} upserted, {} failed)".format(
                            processed, total_records, upserted_count, failed_count))
            
            if failed_count > 0:
                logging.warning("⚠ Container {} synced with {} failures ({} records upserted)".format(
                    cosmos_container_name, failed_count, upserted_count))
            else:
                logging.info("✓ Container {} synced successfully ({} records upserted)".format(
                    cosmos_container_name, upserted_count))
        else:
            logging.info("✓ Container {} is already in sync".format(cosmos_container_name))


def get_backup_contents(container_client, blob_name):
    """Download blob from storage and parse its JSON contents"""
    backup_date = datetime.now().strftime("%y%m%d")
    blob_path = BLOB_NAME_PATTERN.format(backup_date, blob_name)
    
    try:
        # Get the blob client and download content
        blob_client = container_client.get_blob_client(blob_path)
        download_stream = blob_client.download_blob()
        content_bytes = download_stream.readall()
        content_text = content_bytes.decode('utf-8-sig')
        
        logging.info(f"Downloaded blob {blob_path}, size: {len(content_text)} bytes")
        
        records = []
        for line in content_text.splitlines():
            line = line.strip()
            if line:  # Skip empty lines
                try:
                    record = json.loads(line)
                    records.append(record)
                except json.JSONDecodeError as e:
                    logging.warning(f"Failed to parse line: {e}")
        
        if records:
            logging.info(f"Successfully parsed {len(records)} JSON records from {blob_path}")
            return records
        else:
            logging.error(f"No valid JSON records found in {blob_path}")
            return []
            
    except Exception as e:
        logging.error(f"Error processing blob {blob_path}: {str(e)}")
        traceback.print_exc()
        return []
     


# Create cosmosdb clients
def get_db_client(dest_url, db_name):

    # Create cosmosdb client for destination db
    dest_cosmos_client = CosmosClient(dest_url, credential=credential_chain)
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
        logging.error("Failed to create database client using CosmosClient")
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
        "--backup-storage-url",
        required=True,
        help=("URL to backup storage account"),
    )
    parser.add_argument(
        "--dest-url",
        required=True,
        help=("URL to destination cosmosdb"),
    )
    parser.add_argument(
        "--db-name",
        required=True,
        help=("Database name in cosmosdb"),
    )

    args = parser.parse_args()

    logging.info("Syncing database..")
    restore_data_from_backup(args.backup_storage_url, args.dest_url, args.db_name)