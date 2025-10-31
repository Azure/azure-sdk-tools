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
from typing import Any, Dict, Optional, Type
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator
from _evals_result import record_run_result
from azure.identity import AzurePipelinesCredential, DefaultAzureCredential, AzureCliCredential
from azure.storage.blob import BlobServiceClient
import aiohttp
import yaml

class EvaluatorConfig:
    """Configuration for an evaluator"""

    column_mapping: Dict[str, str]
    """Dictionary mapping evaluator input name to column in data"""

class EvaluatorClass:
    def __init__(self, name: str, evaluator:Any, evaluator_config: Optional[EvaluatorConfig] = None):
        self._name = name
        self._evaluator = evaluator
        self._evaluator_config = evaluator_config
    
    
    @property
    def name(self):
        """Getter for name"""
        return self._name

    @name.setter
    def name(self, value):
        """Setter for name"""
        if not value:
            raise ValueError("Name cannot be empty")
        self._name = value

    @property
    def evaluator(self):
        """Getter for evaluator"""
        return self._evaluator

    @evaluator.setter
    def evaluator(self, value):
        """Setter for evaluator"""
        if not value:
            raise ValueError("evaluator cannot be None")
        self._evaluator = value

    @property
    def evaluator_config(self):
        """Getter for evaluator config"""
        return self._evaluator_config

    @evaluator_config.setter
    def evaluator_config(self, value):
        """Setter for evaluator config"""
        self._evaluator_config = value

    

class EvalsRunner:
    channel_to_tenant_id_dict: dict[str, str] = {}

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
        "javascript": "Language - JavaScript"
    }

    def __init__(self, evaluators: dict[str, EvaluatorClass] = {}, num_to_run: int = 1):
        self._evaluators = evaluators
        self._num_to_run = num_to_run
        EvalsRunner.channel_to_tenant_id_dict = EvalsRunner._retrieve_tenant_ids()

    def registerEvaluator(self, name: str, evaluator: EvaluatorClass):
        self._evaluators[name] = evaluator

    @classmethod
    def _retrieve_tenant_ids(cls) -> dict[str, str]:
        credential = DefaultAzureCredential()
        storage_blob_account = os.environ["STORAGE_BLOB_ACCOUNT"]
        blob_service_client = BlobServiceClient(
            account_url=f"https://{storage_blob_account}.blob.core.windows.net",
            credential=credential

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

    async def _process_file(self, input_file: str, output_file: str, scenario: str, retrieve_response: bool) -> None:
        """Process a single input file"""
        logging.info(f"Processing file: {input_file}")
        
        azure_bot_service_access_token = os.environ.get("BOT_AGENT_ACCESS_TOKEN", None)
        bot_service_endpoint = os.environ.get("BOT_SERVICE_ENDPOINT", None) 
        api_url = f"{bot_service_endpoint}/completion" if bot_service_endpoint is not None else "http://localhost:8088/completion"
        start_time = time.time()
        tenant_id = EvalsRunner.channel_to_tenant_id_dict[EvalsRunner.scenario_to_channel[scenario] if scenario in EvalsRunner.scenario_to_channel else scenario]
        if tenant_id is None:
            tenant_id = EvalsRunner.channel_to_tenant_id_dict["default"]
        
        outputFile = open(output_file, 'a', encoding='utf-8')
        with open(input_file, "r", encoding="utf-8") as f:
            for line in f:
                record = json.loads(line)
                logging.debug(record)
                if retrieve_response:
                    try:
                        api_response = await self._call_bot_api(record["query"], api_url, azure_bot_service_access_token, tenant_id)
                        answer = api_response.get("answer", "")
                        full_context = api_response.get("full_context", "")
                        latency = time.time() - start_time
                        processed_test_data = {
                            "query": record["query"],
                            "ground_truth": record["ground_truth"],
                            "response": answer,
                            "context": full_context,
                            "latency": latency,
                            "response_length": len(answer),
                            "testcase": record.get("testcase", "unknown"),
                        }
                        if processed_test_data:
                            outputFile.write(json.dumps(processed_test_data, ensure_ascii=False) + '\n')
                    except Exception as e:
                        logging.error(f"❌ Error occurred when process {input_file}: {str(e)}")
                        import traceback
                        traceback.print_exc()
                else:
                    outputFile.write(line)
        outputFile.flush()
        outputFile.close()

    async def _call_bot_api(self, question: str, bot_endpoint: str, access_token: str, tenant_id: str = None, with_full_context: bool = True) -> Dict[str, Any]:
        """Call the completion API endpoint."""
        headers = {
            "Content-Type": "application/json; charset=utf8",
        }
        if access_token: 
            headers["Authorization"] = f"Bearer {access_token}"
        
        payload = {
            "tenant_id": tenant_id if tenant_id is not None else "azure_sdk_qa_bot",
            "message": {
                "role": "user",
                "content": question
            },
            "with_preprocess": True,
            "with_full_context": with_full_context
        }

        async with aiohttp.ClientSession() as session:
            async with session.post(bot_endpoint, json=payload, headers=headers) as resp:
                if resp.status == 200:
                    return await resp.json()
                else:
                    raise Exception(f"API request failed with status {resp.status}")

    async def _prepare_dataset(self, testdata_dir: str, file_prefix: str = None, retrieve_response: bool = True):
        """
        Process markdown files in the data directory and generate Q&A pairs.
        
        Args:
            prefix: Optional prefix to filter which markdown files to process.
                        If provided, only files starting with this prefix will be processed.
        """
        logging.info("📁 Preparing dataset...")
        data_dir = Path(testdata_dir)
        logging.info(f"📂 Data directory: {data_dir.absolute()}")
        script_directory = os.path.dirname(os.path.abspath(__file__))
        logging.info(f"Script directory:{script_directory}")
        output_dir = Path(os.path.join(script_directory, "output"))
        output_dir.mkdir(exist_ok=True)
        logging.info(f"📂 Output directory: {output_dir.absolute()}")
        
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
            grouped = defaultdict(list)

            for item in matching_files:
                match = re.match(r"^([^_]+)_", item.name)
                if match:
                    key = match.group(1)
                    grouped[key].append(item)
            
            for key, items in grouped.items():
                output_file = os.path.join(output_dir.absolute(), f"{key}_{current_date}.jsonl")
                for file_path in items:
                    logging.info(f"  - {file_path.name}")
                    await self._process_file(str(file_path), str(output_file), key, retrieve_response)
        elif file_prefix:
            logging.info(f"No files found matching prefix '{file_prefix}' in {data_dir}/")
            return None
        else:
            logging.info(f"No files found in {data_dir}/")
            return None
            
        logging.info("Processing complete. Results written to output directory.")
        return output_dir.absolute()

    def evaluate_run(self, test_folder:str, prefix: Optional[str] = None, need_retrieve_response: bool = True, evaluator_filter: Optional[list] = None, evaluation_name_prefix: Optional[str] = None, ai_project_endpoint: Optional[str] = None, **kwargs):

        all_results = {}
        evaluators = {}
        evaluator_config = {}
        metrics = evaluator_filter if evaluator_filter is not None else self._evaluators.keys()
        for index, (key, value) in enumerate(self._evaluators.items()):
            if (evaluator_filter is None or key in evaluator_filter):
                evaluators[key] = value.evaluator
                evaluator_config[key] = value.evaluator_config
        
        if not evaluators or not evaluator_config:
            logging.info("No evaluators. return empty result")
            return {}
        
        output_file_dir = asyncio.run(self._prepare_dataset(test_folder, prefix, need_retrieve_response))    
        for output_file in output_file_dir.glob("*.jsonl"):
            result = evaluate(
                data=output_file,
                evaluators=evaluators,
                evaluation_name=f"{evaluation_name_prefix}-{os.path.splitext(os.path.basename(output_file))[0]}" if evaluation_name_prefix else os.path.splitext(os.path.basename(output_file))[0],
                # column mapping
                evaluator_config=evaluator_config,
                # Optionally provide your Azure AI Foundry project information to track your evaluation results in your project portal
                azure_ai_project = ai_project_endpoint,
                # Optionally provide an output path to dump a json of metric summary, row level data and metric and Azure AI project URL
                output_path="./evalresults.json",
                **kwargs
            )
            # print("✅ Evaluation completed. Results:", result)
            logging.info("✅ Evaluation completed.")
            run_result = record_run_result(result, metrics)
            all_results[output_file.name] = run_result
        
        return all_results


__all__ = [
    EvaluatorClass,
    EvalsRunner
]