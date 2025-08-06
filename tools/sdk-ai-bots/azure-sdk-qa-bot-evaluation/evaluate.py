import asyncio
from datetime import datetime
import json
import os
from pathlib import Path
import time
from typing import Any, Dict
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator
import aiohttp

async def process_file(input_file: str, output_file: str, is_bot: bool) -> None:
    """Process a single input file"""
    print(f"Processing file: {input_file}")
    
    azure_openai_api_key = os.environ["AZURE_OPENAI_API_KEY"]
    bot_service_endpoint = os.environ.get("BOT_SERVICE_ENDPOINT", None) 
    api_url = f"{bot_service_endpoint}/completion" if bot_service_endpoint is not None else "http://localhost:8088/completion"
    start_time = time.time()
    outputFile = open(output_file, 'a', encoding='utf-8')
    with open(input_file, "r", encoding="utf-8") as f:
        for line in f:
            record = json.loads(line)
            print(record)
            if is_bot:
                try:
                    api_response = await call_bot_api(record["query"], api_url, azure_openai_api_key)
                    answer = api_response.get("answer", "")
                    latency = time.time() - start_time
                    processed_test_data = {
                        "query": record["query"],
                        "ground_truth": record["ground_truth"],
                        "response": answer,
                        "context": "",
                        "latency": latency,
                        "response_length": len(answer)
                    }
                    if processed_test_data:
                        outputFile.write(json.dumps(processed_test_data, ensure_ascii=False) + '\n')
                except Exception as e:
                    print(f"❌ Error occurred when process {input_file}: {str(e)}")
                    import traceback
                    traceback.print_exc()
    outputFile.flush()
    outputFile.close()

async def call_bot_api(question: str, bot_endpoint: str, api_key: str, tenant_id: str = None) -> Dict[str, Any]:
    """Call the completion API endpoint."""
    headers = {
        "Content-Type": "application/json; charset=utf8",
        "X-API-Key": api_key
    }
    payload = {
        "tenant_id": tenant_id if tenant_id is not None else "azure_sdk_qa_bot",
        "message": {
            "role": "user",
            "content": question
        },
        "with_preprocess": True,
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(bot_endpoint, json=payload, headers=headers) as resp:
            if resp.status == 200:
                return await resp.json()
            else:
                raise Exception(f"API request failed with status {resp.status}")

async def prepare_dataset(testdata_dir: str, file_prefix: str = None, is_bot: bool = True):
    """
    Process markdown files in the data directory and generate Q&A pairs.
    
    Args:
        prefix: Optional prefix to filter which markdown files to process.
                    If provided, only files starting with this prefix will be processed.
    """
    print("📁 Preparing dataset...")
    data_dir = Path(testdata_dir)
    script_directory = os.path.dirname(os.path.abspath(__file__))
    print("Script directory:", script_directory)
    output_dir = os.path.join(script_directory, "output")
    output_dir.mkdir(exist_ok=True)
    
    print(f"📂 Data directory: {data_dir.absolute()}")
    print(f"📂 Output directory: {output_dir.absolute()}")
    
    if not data_dir.exists():
        print(f"❌ Data directory {data_dir.absolute()} does not exist!")
        return None
    
    current_date = datetime.now().strftime("%Y_%m_%d")
    output_file_name = f"{file_prefix}_{current_date}.jsonl" if file_prefix else f"collected_qa_{current_date}.jsonl"
    output_file = os.path.join(output_dir.absolute(), output_file_name)
    print(f"📄 Output file will be: {output_file}")

    
    # Process markdown files in the folder, optionally filtered by prefix
    glob_pattern = f"{file_prefix}*.jsonl" if file_prefix else "*.jsonl"
    matching_files = list(data_dir.glob(glob_pattern))
    
    print(f"🔍 Looking for files matching pattern: {glob_pattern}")
    print(f"📋 Found {len(matching_files)} matching files")
    
    if matching_files:
        print(f"Found {len(matching_files)} files matching prefix '{file_prefix}' in {data_dir}/")
        for file_path in matching_files:
            print(f"  - {file_path.name}")
            await process_file(str(file_path), str(output_file), is_bot)
    elif file_prefix:
        print(f"No files found matching prefix '{file_prefix}' in {data_dir}/")
        return None
    else:
        print(f"No files found in {data_dir}/")
        return None
        
    print("Processing complete. Results written to output directory.")
    return output_file

if __name__ == "__main__":
    import argparse

    print("🚀 Starting evaluation ...")

    parser = argparse.ArgumentParser(description="Run evals for Azure Chat Bot.")

    parser.add_argument("--test_folder", type=str, help="the path to the test folder")
    parser.add_argument("--prefix", type=str, help="Process only files starting with this prefix")
    parser.add_argument("--is_bot", type=str, default="True", help="Use bot API for processing Q&A pairs (True/False)")
    parser.add_argument("--is_cli", type=str, default="True", help="Run in CI/CD pipeline (True/False)")
    args = parser.parse_args()

    args.is_bot = args.is_bot.lower() in ('true', '1', 'yes', 'on')
    args.is_cli = args.is_cli.lower() in ('true', '1', 'yes', 'on')

    
    script_directory = os.path.dirname(os.path.abspath(__file__))
    print("Script directory:", script_directory)

    
    current_file_path = os.getcwd()
    print("Current working directory:", current_file_path)


    if (args.test_folder == None):
        args.test_folder = os.path.join(script_directory, "tests")
    
    print(f"test folder: {args.test_folder}")
    # Required environment variables
    azure_ai_project_endpoint = os.environ["AZURE_AI_PROJECT_ENDPOINT"]
    print(f"📋 Using project endpoint: {azure_ai_project_endpoint}")
    model_config: dict[str, str] = {
        "azure_endpoint": os.environ["AZURE_OPENAI_ENDPOINT"],
        "api_key": os.environ["AZURE_OPENAI_API_KEY"],
        "azure_deployment": os.environ["AZURE_EVALUATION_MODEL_NAME"],
        "api_version": os.environ["AZURE_API_VERSION"],
    }
    azure_ai_project = {
        "subscription_id": os.environ["AZURE_SUBSCRIPTION_ID"],
        "resource_group_name": os.environ["AZURE_FOUNDRY_RESOURCE_GROUP"],
        "project_name": os.environ["AZURE_FOUNDRY_PROJECT_NAME"],
    }
    try: 
        print("📊 Preparing dataset...")
        output_file = asyncio.run(prepare_dataset(args.test_folder, args.prefix, args.is_bot))
        result = evaluate(
            data=output_file,
            evaluators={
                "similarity": SimilarityEvaluator(model_config=model_config) 
            },
            # column mapping
            evaluator_config={
                "similarity": {
                    "column_mapping": {
                        "query": "${data.query}",
                        "response": "${data.response}",
                        "context": "${data.context}",
                        "ground_truth": "${data.ground_truth}",
                    } 
                }
            },
            # Optionally provide your Azure AI Foundry project information to track your evaluation results in your project portal
            azure_ai_project = azure_ai_project,
            # Optionally provide an output path to dump a json of metric summary, row level data and metric and Azure AI project URL
            output_path="./evalresults.json"
        )
        print("✅ Evaluation completed. Results:", result)
    except Exception as e:
        print(f"❌ Error occurred: {str(e)}")
        import traceback
        traceback.print_exc()