
import argparse
import asyncio
from collections import defaultdict
from datetime import datetime
import json
import logging
import os
from pathlib import Path
import re
import shutil
import time
from typing import Any, Dict, List
from azure.identity import DefaultAzureCredential
from azure.storage.blob import BlobServiceClient
import aiohttp
import yaml
from dotenv import load_dotenv

channel_to_tenant_id_dict: dict[str, str] | None = None
scenario_to_channel: dict[str, str] = {
    "typespec": "TypeSpec Discussion",
    "python": "Language - Python",
    "advocacy": "Advocacy",
    "ai": "AI Discussion",
    "apispec": "API Spec Review",
    "apiview": "APIView",
    "onboarding": "Azure SDK Onboarding",
    "go": "Language - Go",
    "java": "Language - Java",
    "net": "Language - DotNet",
    "javascript": "Language - JavaScript",
    "general": "General"
}
def extract_title_and_link_from_references(references: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """
    Map an array of reference objects to a string array of their 'link' properties.

    Args:
        references: List of reference objects, each containing a 'link' field

    Returns:
        List of link strings extracted from the reference objects
    """
    if not references:
        return []

    refs = []
    for ref in references:
        title: str = ""
        link: str = ""

        if isinstance(ref, dict) and "title" in ref and ref["title"]:
            title = ref["title"]
        elif isinstance(ref, dict) and "Title" in ref and ref["Title"]:  # Handle capitalized version
            title = ref["Title"]

        if isinstance(ref, dict) and "link" in ref and ref["link"]:
            link = ref["link"]
        elif isinstance(ref, dict) and "Link" in ref and ref["Link"]:  # Handle capitalized version
            link = ref["Link"]
        
        refs.append({"title": title, "link": link})
    return refs

def extract_title_and_link_from_context(context: str) -> List[Dict[str, Any]]:
    if not context:
        return []
    
    docs = []

    docs_obj = json.loads(context)
    for doc in docs_obj:
        title: str = ""
        link: str = ""
        if isinstance(doc, dict) and "document_title" in doc and doc["document_title"]:
            title = doc["document_title"]
        
        if isinstance(doc, dict) and "document_link" in doc and doc["document_link"]:
            link = doc["document_link"]
        
        docs.append({"title": title, "link": link})
    return docs

def retrieve_tenant_ids() -> dict[str, str]:
    credential = DefaultAzureCredential()
    storage_blob_account = os.environ["STORAGE_BLOB_ACCOUNT"]
    blob_service_client = BlobServiceClient(
        account_url=f"https://{storage_blob_account}.blob.core.windows.net", credential=credential
    )
    container_name = os.environ["BOT_CONFIG_CONTAINER"]
    container_client = blob_service_client.get_container_client(container_name)
    filename = os.environ["BOT_CONFIG_CHANNEL_BLOB"]
    blob_client = container_client.get_blob_client(filename)
    download_stream = blob_client.download_blob()
    channel_data_yaml = yaml.safe_load(download_stream.readall())
    channels = channel_data_yaml["channels"]
    channel_to_tenant_id = {}
    channel_to_tenant_id["default"] = channel_data_yaml["default"]["tenant"]
    if channels:
        for item in channels:
            channel_to_tenant_id[item["name"]] = item["tenant"]

    return channel_to_tenant_id

async def process_file(input_file: str, output_file: str, scenario: str, retrieve_response: bool) -> None:
    """Process a single input file"""
    logging.info(f"Processing file: {input_file}")

    azure_bot_service_access_token = os.environ.get("BOT_AGENT_ACCESS_TOKEN", None)
    bot_service_endpoint = os.environ.get("BOT_SERVICE_ENDPOINT", None)
    api_url = (
        f"{bot_service_endpoint}/completion"
        if bot_service_endpoint is not None
        else "http://localhost:8088/completion"
    )
    global scenario_to_channel
    start_time = time.time()
    tenant_id = None
    if channel_to_tenant_id_dict:
        tenant_id = channel_to_tenant_id_dict[
            scenario_to_channel[scenario] if scenario in scenario_to_channel else scenario
        ]
        if tenant_id is None:
            tenant_id = channel_to_tenant_id_dict["default"]

    with open(output_file, "a", encoding="utf-8") as outputFile:
        with open(input_file, "r", encoding="utf-8") as f:
            for line in f:
                record = json.loads(line)
                logging.debug(record)
                if retrieve_response:
                    try:
                        api_response = await call_bot_api(
                            record["query"], api_url, azure_bot_service_access_token, tenant_id
                        )
                        answer = api_response.get("answer", "")
                        full_context = api_response.get("full_context", "")
                        references = api_response.get("references", [])
                        latency = time.time() - start_time
                        processed_test_data = {
                            "testcase": record.get("testcase", "unknown"),
                            "query": record["query"],
                            "ground_truth": record["ground_truth"],
                            "expected_knowledges": extract_title_and_link_from_context(full_context),
                            "expected_references": extract_title_and_link_from_references(references),
                        }
                        if processed_test_data:
                            outputFile.write(json.dumps(processed_test_data, ensure_ascii=False) + "\n")
                    except Exception as e:
                        logging.error(f"❌ Error occurred when process {input_file}: {str(e)}")
                        import traceback

                        traceback.print_exc()
                else:
                    outputFile.write(line)
        outputFile.flush()
        outputFile.close()

async def call_bot_api(
    question: str,
    bot_endpoint: str,
    access_token: str | None,
    tenant_id: str | None = None,
    with_full_context: bool = True,
) -> Any:
    """Call the completion API endpoint."""
    headers = {
        "Content-Type": "application/json; charset=utf8",
    }
    if access_token:
        headers["Authorization"] = f"Bearer {access_token}"

    payload = {
        "tenant_id": tenant_id if tenant_id is not None else "azure_sdk_qa_bot",
        "message": {"role": "user", "content": question},
        "with_preprocess": True,
        "with_full_context": with_full_context,
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(bot_endpoint, json=payload, headers=headers) as resp:
            if resp.status == 200:
                return await resp.json()
            else:
                raise Exception(f"API request failed with status {resp.status}")

async def create_tests(
    testdata_dir: str, file_prefix: str | None = None, retrieve_response: bool = True
) -> Path | None:
    """
    Process markdown files in the data directory and generate Q&A pairs.

    Args:
        prefix: Optional prefix to filter which markdown files to process.
                    If provided, only files starting with this prefix will be processed.
    """
    logging.info("📁 Preparing tests...")
    data_dir = Path(testdata_dir)
    logging.info(f"📂 Data directory: {data_dir.absolute()}")
    script_directory = os.path.dirname(os.path.abspath(__file__))
    logging.info(f"Script directory:{script_directory}")
    output_dir = Path(os.path.join(script_directory, "generated_tests"))
    output_dir.mkdir(exist_ok=True)
    logging.info(f"📂 Output directory: {output_dir.absolute()}")

    global channel_to_tenant_id_dict
    if channel_to_tenant_id_dict is None:
        channel_to_tenant_id_dict = retrieve_tenant_ids()
    for filename in os.listdir(output_dir.absolute()):
        file_path = os.path.join(output_dir.absolute(), filename)
        try:
            if os.path.isfile(file_path) or os.path.islink(file_path):
                os.unlink(file_path)  # remove file or link
            elif os.path.isdir(file_path):
                shutil.rmtree(file_path)  # remove directory
        except Exception as e:
            print(f"Failed to delete {file_path}. Reason: {e}")
    print(f"Directory '{output_dir.absolute()}' cleared.")

    if not data_dir.exists():
        logging.error(f"❌ Data directory {data_dir.absolute()} does not exist!")
        return None

    current_date = datetime.now().strftime("%Y_%m_%d")
    # Process jsonl files in the folder, optionally filtered by prefix
    glob_pattern = f"{file_prefix}*.jsonl" if file_prefix else "*.jsonl"
    matching_files = list(data_dir.glob(glob_pattern))

    logging.info(f"🔍 Looking for files matching pattern: {glob_pattern}")
    logging.info(f"📋 Found {len(matching_files)} matching files")

    if matching_files:
        logging.info(f"Found {len(matching_files)} files matching prefix '{file_prefix}' in {data_dir}/")
        # group files
        grouped: dict[str, list[Path]] = defaultdict(list[Path])

        for item in matching_files:
            match = re.match(r"^([^_]+)_", item.name)
            if match:
                key = match.group(1)
                grouped[key].append(item)

        for key, paths in grouped.items():
            # output_file = os.path.join(output_dir.absolute(), f"{key}_{current_date}.jsonl")
            for input_file_path in paths:
                output_file = os.path.join(output_dir.absolute(), input_file_path.name)
                logging.info(f"  - {input_file_path.name}")
                await process_file(str(input_file_path), str(output_file), key, retrieve_response)

        logging.info("Processing complete. Results written to output directory.")
        return output_dir.absolute()
    elif file_prefix:
        logging.info(f"No files found matching prefix '{file_prefix}' in {data_dir}/")
        return None
    else:
        logging.info(f"No files found in {data_dir}/")
        return None


async def main():
    print("🚀 Starting converting ...")

    parser = argparse.ArgumentParser(description="Convert md to jsonL.")

    parser.add_argument("--source_jsonl_path", type=str, help="the path to the source md file or folder")
    parser.add_argument("--dest_jsonl_folder", type=str, help="the path to the dest jsonl folder.")
    args = parser.parse_args()

    script_directory = os.path.dirname(os.path.abspath(__file__))
    print("Script directory:", script_directory)

    # if args.dest_jsonl_folder == None:
    #     args.dest_jsonl_folder = os.path.join(script_directory, "tests")
    if args.source_jsonl_path == None:
        args.source_jsonl_path = os.path.join(script_directory, "tests")

    # output_test_dir = Path(args.dest_jsonl_folder)
    # output_test_dir.mkdir(exist_ok=True)

    # Required environment variables
    load_dotenv()

    if os.path.isfile(args.source_jsonl_path):
        # output_file = os.path.join(
        #     args.dest_jsonl_folder, f"{os.path.splitext(os.path.basename(args.source_md_path))[0]}.jsonl"
        # )
        await create_tests(args.source_jsonl_path)
    elif os.path.isdir(args.source_jsonl_path):
        await create_tests(args.source_jsonl_path)
        # for md_file in Path(args.source_jsonl_path).glob("*.jsonl"):
        #     # output_file = os.path.join(
        #     #     args.dest_jsonl_folder, f"{os.path.splitext(os.path.basename(md_file))[0]}.jsonl"
        #     # )
        #     await create_tests(str(md_file))
    else:
        print("Path does not exist.")


if __name__ == "__main__":
    asyncio.run(main())