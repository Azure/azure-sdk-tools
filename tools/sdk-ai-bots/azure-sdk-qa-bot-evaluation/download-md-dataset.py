import argparse
from datetime import datetime, timedelta
import logging
import os
from pathlib import Path
import re
import sys
from dotenv import load_dotenv
from azure.identity import AzurePipelinesCredential, DefaultAzureCredential, AzureCliCredential
from azure.storage.blob import BlobServiceClient


def extract_date(filename: str, date_patterns: list[str]) -> datetime | None:
    for pattern in date_patterns:
        match = re.search(pattern, filename)
        if match:
            try:
                year, month, day = map(int, match.groups())
                return datetime(year, month, day)
            except ValueError:
                continue
    return None


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(asctime)s - %(levelname)s - %(message)s")
    logging.info("🚀 Starting download dataset ...")

    parser = argparse.ArgumentParser(description="Download MD dataset from Azure Blob Storage.")
    parser.add_argument("--test_folder", type=str, help="the path to the test folder")
    parser.add_argument("--days_before", type=int, help="only filter the files which are created the days before")
    parser.add_argument("--is_ci", type=str, default="False", help="Run in CI/CD pipeline (True/False)")
    args = parser.parse_args()

    args.is_ci = args.is_ci.lower() in ("true", "1", "yes", "on")

    script_directory = os.path.dirname(os.path.abspath(__file__))
    logging.info(f"Script directory: {script_directory}")

    if args.test_folder is None:
        args.test_folder = os.path.join(script_directory, "online-qa-tests")

    if args.days_before is None:
        args.days_before = 21

    logging.info(f"test folder: {args.test_folder}")
    output_test_dir = Path(args.test_folder)
    output_test_dir.mkdir(exist_ok=True)

    load_dotenv()

    try:
        if args.is_ci:
            service_connection_id = os.getenv("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID")
            client_id = os.getenv("AZURESUBSCRIPTION_CLIENT_ID")
            tenant_id = os.getenv("AZURESUBSCRIPTION_TENANT_ID")
            system_access_token = os.getenv("SYSTEM_ACCESSTOKEN")
            if all([service_connection_id, client_id, tenant_id, system_access_token]):
                credential = AzurePipelinesCredential(
                    service_connection_id=service_connection_id,
                    client_id=client_id,
                    tenant_id=tenant_id,
                    system_access_token=system_access_token,
                )
            else:
                logging.warning(
                    "One or more AZURESUBSCRIPTION_* or SYSTEM_ACCESSTOKEN "
                    "environment variables are missing. Falling back to default credentials."
                )
                credential = DefaultAzureCredential()
        else:
            credential = AzureCliCredential()
        storage_blob_account = os.environ["STORAGE_BLOB_ACCOUNT"]
        blob_service_client = BlobServiceClient(
            account_url=f"https://{storage_blob_account}.blob.core.windows.net", credential=credential
        )
        container_name = os.environ["AI_ONLINE_PERFORMANCE_EVALUATION_STORAGE_CONTAINER"]
        container_client = blob_service_client.get_container_client(container_name)
        blobs = container_client.list_blobs()
        today = datetime.today()
        date_days_before = today - timedelta(days=args.days_before)
        date_patterns = [r"(\d{4})(\d{2})(\d{2})", r"(\d{4})_(\d{2})_(\d{2})"]  # YYYYMMDD  # YYYY-MM-DD
        if blobs:
            for item in blobs:
                filename = re.split(r"[\\/]", item.name)[-1]
                file_date = extract_date(filename=filename, date_patterns=date_patterns)
                if file_date is not None and file_date >= date_days_before:
                    logging.info(f"download {item.name}")
                    blob_client = container_client.get_blob_client(item.name)
                    download_file_path = os.path.join(args.test_folder, filename)

                    with open(download_file_path, "wb") as download_file:
                        download_stream = blob_client.download_blob()
                        download_file.write(download_stream.readall())
    except Exception as e:
        logging.exception(f"❌ Error occurred: {str(e)}")
        sys.exit(1)
