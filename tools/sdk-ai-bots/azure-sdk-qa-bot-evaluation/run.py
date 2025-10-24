import asyncio
from collections import defaultdict
from datetime import datetime
import json
import math
import os
from pathlib import Path
import pathlib
import re
import sys
import time
from typing import Any, Dict, Optional
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator
import aiohttp
from azure.identity import AzurePipelinesCredential, DefaultAzureCredential, AzureCliCredential
from tabulate import tabulate
import argparse
from dotenv import load_dotenv
from azure.storage.blob import BlobServiceClient
import yaml
import logging
import shutil

weights: dict[str, float] = {
    "similarity_weight": 0.6,  # Similarity between expected and actual
    "groundedness_weight": 0.4,  # Staying grounded in guidelines
}

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

def retrieve_tenant_ids() -> dict[str, str]:
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

async def process_file(input_file: str, output_file: str, scenario: str, is_bot: bool) -> None:
    """Process a single input file"""
    logging.info(f"Processing file: {input_file}")
    
    azure_bot_service_access_token = os.environ.get("BOT_AGENT_ACCESS_TOKEN", None)
    bot_service_endpoint = os.environ.get("BOT_SERVICE_ENDPOINT", None) 
    api_url = f"{bot_service_endpoint}/completion" if bot_service_endpoint is not None else "http://localhost:8088/completion"
    start_time = time.time()
    tenant_id = channel_to_tenant_id_dict[scenario_to_channel[scenario] if scenario in scenario_to_channel else scenario]
    if tenant_id is None:
        tenant_id = channel_to_tenant_id_dict["default"]
    
    outputFile = open(output_file, 'a', encoding='utf-8')
    with open(input_file, "r", encoding="utf-8") as f:
        for line in f:
            record = json.loads(line)
            logging.debug(record)
            if is_bot:
                try:
                    api_response = await call_bot_api(record["query"], api_url, azure_bot_service_access_token, tenant_id)
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
    outputFile.flush()
    outputFile.close()

async def call_bot_api(question: str, bot_endpoint: str, access_token: str, tenant_id: str = None, with_full_context: bool = True) -> Dict[str, Any]:
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

async def prepare_dataset(testdata_dir: str, file_prefix: str = None, is_bot: bool = True):
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
                await process_file(str(file_path), str(output_file), key, is_bot)
    elif file_prefix:
        logging.info(f"No files found matching prefix '{file_prefix}' in {data_dir}/")
        return None
    else:
        logging.info(f"No files found in {data_dir}/")
        return None
        
    logging.info("Processing complete. Results written to output directory.")
    return output_dir.absolute()

def calculate_overall_score(row: dict[str, Any]) -> float:
    """Calculate weighted score based on various metrics."""
    # calculate the overall score when there are multiple metrics.
    if ("outputs.similarity.similarity" not in row) or ( "outputs.groundedness.groundedness" not in row):
        return 0.0
    
    similarity = float(row["outputs.similarity.similarity"])
    groundedness = float(row["outputs.groundedness.groundedness"])
    if math.isnan(similarity) or math.isnan(groundedness):
        return 0.0
    else:
        return similarity * weights["similarity_weight"] + groundedness * weights["groundedness_weight"]

def record_run_result(result: dict[str, Any]) -> list[dict[str, Any]]:
    run_result = []
    total_score = 0

    similarity_pass_rate = 0
    groundedness_pass_rate = 0
    for row in result["rows"]:
        score = calculate_overall_score(row)
        total_score += score

        if "outputs.similarity.similarity_result" in row and row["outputs.similarity.similarity_result"] == "pass":
            similarity_pass_rate += 1

        if "outputs.groundedness.groundedness_result" in row and row["outputs.groundedness.groundedness_result"] == "pass":
            groundedness_pass_rate += 1

        run_result.append(
            {
                "testcase": row["inputs.testcase"],
                "expected": row["inputs.ground_truth"],
                "actual": row["inputs.response"],
                "similarity": float(row["outputs.similarity.similarity"]) if "outputs.similarity.similarity" in row else -1,
                "gpt_similarity": float(row["outputs.similarity.gpt_similarity"]) if "outputs.similarity.gpt_similarity" in row else -1,
                "similarity_threshold": float(row["outputs.similarity.similarity_threshold"]) if "outputs.similarity.similarity_threshold" in row else 3,
                "similarity_result": row["outputs.similarity.similarity_result"] if "outputs.similarity.similarity_result" in row else "N/A",
                "groundedness": float(row["outputs.groundedness.groundedness"]) if "outputs.groundedness.groundedness" in row else -1,
                "gpt_groundedness": float(row["outputs.groundedness.gpt_groundedness"]) if "outputs.groundedness.gpt_groundedness" in row else -1,
                "groundedness_threshold": float(row["outputs.groundedness.groundedness_threshold"]) if "outputs.groundedness.groundedness_threshold" in row else 3,
                "groundedness_result": row["outputs.groundedness.groundedness_result"] if "outputs.groundedness.groundedness_result" in row else "N/A",
                "overall_score": score,
            }
        )

    if result:
        average_score = total_score / len(result["rows"])
    else:
        average_score = 0
    run_result.append({"average_score": average_score, "total_evals": len(result["rows"]), "similarity_pass_rate": similarity_pass_rate, "groundedness_pass_rate": groundedness_pass_rate})
    return run_result

def format_terminal_diff(new: float, old: float, format_str: str = ".1f", reverse: bool = False) -> str:
    """Format difference with ANSI colors for terminal output."""

    diff = new - old
    if diff > 0:
        if reverse:
            return f" (\033[31m+{diff:{format_str}}\033[0m)"  # Red
        return f" (\033[32m+{diff:{format_str}}\033[0m)"  # Green
    elif diff < 0:
        if reverse:
            return f" (\033[32m{diff:{format_str}}\033[0m)"  # Green
        return f" (\033[31m{diff:{format_str}}\033[0m)"  # Red
    return f" ({diff:{format_str}})"

def output_table(eval_results: list[dict[str, Any]], file_name: str, baseline_results: Optional[dict[str, Any]] = None) -> None:
    headers = [
        "Test Case",
        "Similarity",
        "Similarity Result",
        "Groundedness",
        "Groundedness Result",
        "Score"
    ]
    terminal_rows = []

    similarity_pass_rate = eval_results[-1]['similarity_pass_rate']
    groundedness_pass_rate = eval_results[-1]['groundedness_pass_rate']

    for result in eval_results[:-1]:  # Skip summary object
        testcase = result["testcase"]
        score = result["overall_score"]
        sim = result["similarity"]
        sim_result = result["similarity_result"]

        groundedness = result['groundedness']
        groundedness_result = result['groundedness_result']
        
        terminal_row = [testcase]
        if baseline_results is not None and testcase in baseline_results:
            base = baseline_results[testcase]
            values =[
                f"{sim:.1f}{format_terminal_diff(sim, base['similarity'])}",
                f"{sim_result}",
                f"{groundedness}{format_terminal_diff(groundedness, base['groundedness'])}",
                f"{groundedness_result}",
                f"{score:.1f}{format_terminal_diff(score, base['overall_score'])}",
            ]
        else:
            values = [
                f"{sim:.1f}",
                f"{sim_result}",
                f"{groundedness}",
                f"{groundedness_result}",
                f"{score:.1f}"
            ]
        terminal_row.extend(values)
        terminal_rows.append(terminal_row)

    logging.info("====================================================")
    logging.info(f"\n\n✨ {file_name} results:\n")
    print(tabulate(terminal_rows, headers, tablefmt="simple"))
    if baseline_results:
        print(
            f"\n{file_name} average score: {eval_results[-1]['average_score']} {format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}",
            f" similarity: pass({similarity_pass_rate}) fail({len(eval_results)-1 - similarity_pass_rate})",
            f" groundedness: pass({groundedness_pass_rate}) fail({len(eval_results)-1 - groundedness_pass_rate})"
            "\n\n"
        )

def show_results(all_results: dict[str, Any], with_baseline: bool = True) -> None:
    """Display results in a table format."""
    for name, test_results in all_results.items():
        baseline_results = None
        if with_baseline:
            baseline_results = {}
            baselineName = f"{name.split('_')[0]}-test.json"
            baseline_path = pathlib.Path(__file__).parent / "results" / baselineName

            if baseline_path.exists():
                with open(baseline_path, "r") as f:
                    baseline_data = json.load(f)
                    for result in baseline_data[:-1]:  # Skip summary
                        baseline_results[result["testcase"]] = result
                    baseline_results["average_score"] = baseline_data[-1]["average_score"]

        output_table(test_results, name, baseline_results)

def verify_results(all_results: dict[str, Any], with_baseline: bool = True) -> bool:
    ret = True
    failed_scenarios = []
    for name, test_results in all_results.items():
        scenario_ret = True
        
        if with_baseline:
            baseline_results = {}
            baselineName = f"{name.split('_')[0]}-test.json"
            baseline_path = pathlib.Path(__file__).parent / "results" / baselineName

            if baseline_path.exists():
                with open(baseline_path, "r") as f:
                    baseline_data = json.load(f)
                    for result in baseline_data[:-1]:  # Skip summary
                        baseline_results[result["testcase"]] = result
                    baseline_results["average_score"] = baseline_data[-1]["average_score"]
                    if test_results[-1]["average_score"] < baseline_data[-1]["average_score"]:
                        # scenario_ret = False //ignore decrease in average score
                        logging.warning(f"scenario {name} avarage score decrease!")
        
        if test_results[-1]["similarity_pass_rate"] < test_results[-1]["total_evals"] or test_results[-1]["groundedness_pass_rate"] < test_results[-1]["total_evals"]:
            scenario_ret = False

        if not scenario_ret:
            failed_scenarios.append(name)
            ret = False
    if failed_scenarios:
        logging.info(f"Failed Scenarios: {' '.join(failed_scenarios)}")
    return ret 

def establish_baseline(args: argparse.Namespace, all_results: dict[str, Any]) -> None:
    """Establish the current results as the new baseline."""

    # only ask if we're not in CI
    if args.is_ci is False:
        establish_baseline = input("\nDo you want to establish this as the new baseline? (y/n): ")
        if establish_baseline.lower() == "y":
            for name, result in all_results.items():
                baselineName = f"{name.split('_')[0]}-test.json"
                baseline_path = pathlib.Path(__file__).parent / "results" / baselineName
                with open(str(baseline_path), "w") as f:
                    json.dump(result, indent=4, fp=f)

    # whether or not we establish a baseline, we want to write results to a temp dir
    log_path = pathlib.Path(__file__).parent / "results" / ".log"
    if not log_path.exists():
        log_path.mkdir(parents=True, exist_ok=True)

    for name, result in all_results.items():
        baselineName = f"{name.split('_')[0]}-test.json"
        output_path = log_path / baselineName
        with open(str(output_path), "w") as f:
            json.dump(result, indent=4, fp=f)

if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
    logging.info("🚀 Starting evaluation ...")

    parser = argparse.ArgumentParser(description="Run evals for Azure Chat Bot.")

    parser.add_argument("--test_folder", type=str, help="the path to the test folder")
    parser.add_argument("--prefix", type=str, help="Process only files starting with this prefix")
    parser.add_argument("--is_bot", type=str, default="True", help="Use bot API for processing Q&A pairs (True/False)")
    parser.add_argument("--is_ci", type=str, default="True", help="Run in CI/CD pipeline (True/False)")
    parser.add_argument("--evaluation_name_prefix", type=str, help="the prefix of evaluation name")
    parser.add_argument("--send_result", type=str, default="True", help="Send the evaluation result to AI foundry project")
    parser.add_argument("--baseline_check", type=str, default="True", help="Compare the result with baseline.")
    args = parser.parse_args()

    args.is_bot = args.is_bot.lower() in ('true', '1', 'yes', 'on')
    args.is_ci = args.is_ci.lower() in ('true', '1', 'yes', 'on')
    args.send_result = args.send_result.lower() in ('true', '1', 'yes')
    args.baseline_check = args.baseline_check.lower() in ('true', '1', 'yes')

    
    script_directory = os.path.dirname(os.path.abspath(__file__))
    logging.info(f"Script directory:{script_directory}")

    
    current_file_path = os.getcwd()
    logging.info(f"Current working directory:{current_file_path}")


    if (args.test_folder is None):
        args.test_folder = os.path.join(script_directory, "tests")
    
    logging.info(f"test folder: {args.test_folder}")
    # Required environment variables
    load_dotenv() 
    channel_to_tenant_id_dict = retrieve_tenant_ids()
    all_results = {}
    try: 
        logging.info("📊 Preparing dataset...")
        azure_ai_project_endpoint = os.environ["AZURE_AI_PROJECT_ENDPOINT"]
        logging.info(f"📋 Using project endpoint: {azure_ai_project_endpoint}")
        model_config: dict[str, str] = {
            "azure_endpoint": os.environ["AZURE_OPENAI_ENDPOINT"],
            "api_key": os.environ["AZURE_OPENAI_API_KEY"],
            "azure_deployment": os.environ["AZURE_EVALUATION_MODEL_NAME"],
            "api_version": os.environ["AZURE_API_VERSION"],
        }
        similarity_threshold = os.environ.get("SIMILARITY_THRESHOLD", 3)
        kwargs = {}
        if args.send_result:
            if args.is_ci:
                kwargs = {
                    "credential": DefaultAzureCredential()
                }
            else:
                kwargs = {
                    # run in local, use Azure Cli Credential, make sure you already run `az login`
                    "credential": AzureCliCredential()
                }
        
        output_file_dir = asyncio.run(prepare_dataset(args.test_folder, args.prefix, args.is_bot))
        if output_file_dir is None:
            logging.info(f"No test data file to process. Exitting")
            sys.exit(1)
        
        for output_file in output_file_dir.glob("*.jsonl"):
            run_results = []
            result = evaluate(
                data=output_file,
                evaluators={
                    "similarity": SimilarityEvaluator(model_config=model_config, threshold=similarity_threshold),
                    "groundedness": GroundednessEvaluator(model_config=model_config)
                },
                evaluation_name=f"{args.evaluation_name_prefix}-{os.path.splitext(os.path.basename(output_file))[0]}" if args.evaluation_name_prefix else os.path.splitext(os.path.basename(output_file))[0],
                # column mapping
                evaluator_config={
                    "similarity": {
                        "column_mapping": {
                            "query": "${data.query}",
                            "response": "${data.response}",
                            "ground_truth": "${data.ground_truth}",
                            "testcase": "${data.testcase}"
                        } 
                    },
                    "groundedness": {
                        "column_mapping": {
                            "query": "${data.query}",
                            "response": "${data.response}",
                            "context": "${data.context}",
                            "testcase": "${data.testcase}"
                        } 
                    }
                },
                # Optionally provide your Azure AI Foundry project information to track your evaluation results in your project portal
                azure_ai_project = azure_ai_project_endpoint if args.send_result else None,
                # Optionally provide an output path to dump a json of metric summary, row level data and metric and Azure AI project URL
                output_path="./evalresults.json",
                **kwargs
            )
            # print("✅ Evaluation completed. Results:", result)
            logging.info("✅ Evaluation completed.")
            run_result = record_run_result(result)
            all_results[output_file.name] = run_result
    except Exception as e:
        logging.info(f"❌ Error occurred: {str(e)}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
    
    show_results(all_results, args.baseline_check)
    if args.baseline_check:
        establish_baseline(args, all_results)
    isPass = verify_results(all_results, args.baseline_check)
    if not isPass:
        sys.exit(1)