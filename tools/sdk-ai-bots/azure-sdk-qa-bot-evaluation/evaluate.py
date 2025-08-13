import asyncio
from collections import defaultdict
from datetime import datetime
import json
import os
from pathlib import Path
import pathlib
import re
import time
from typing import Any, Dict
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator
import aiohttp
from azure.identity import AzurePipelinesCredential, DefaultAzureCredential, AzureCliCredential
from tabulate import tabulate
import argparse
from dotenv import load_dotenv

async def process_file(input_file: str, output_file: str, is_bot: bool) -> None:
    """Process a single input file"""
    print(f"Processing file: {input_file}")
    
    azure_openai_api_key = os.environ["BOT_AGENT_API_KEY"]
    bot_service_endpoint = os.environ.get("BOT_SERVICE_ENDPOINT", None) 
    api_url = f"{bot_service_endpoint}/completion" if bot_service_endpoint is not None else "http://localhost:8088/completion"
    start_time = time.time()
    outputFile = open(output_file, 'a', encoding='utf-8')
    with open(input_file, "r", encoding="utf-8") as f:
        for line in f:
            record = json.loads(line)
            # print(record)
            if is_bot:
                try:
                    api_response = await call_bot_api(record["query"], api_url, azure_openai_api_key)
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
    output_dir = Path(os.path.join(script_directory, "output"))
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
        # group files
        grouped = defaultdict(list)

        for item in matching_files:
            match = re.match(r"^([^-]+)-", item.name)
            if match:
                key = match.group(1)
                grouped[key].append(item)
        
        for key, items in grouped.items():
            output_file = os.path.join(output_dir.absolute(), f"{key}_{current_date}.jsonl")
            for file_path in items:
                print(f"  - {file_path.name}")
                await process_file(str(file_path), str(output_file), is_bot)
    elif file_prefix:
        print(f"No files found matching prefix '{file_prefix}' in {data_dir}/")
        return None
    else:
        print(f"No files found in {data_dir}/")
        return None
        
    print("Processing complete. Results written to output directory.")
    return output_dir.absolute()

def calculate_overall_score(row: dict[str, Any]) -> float:
    """Calculate weighted score based on various metrics."""
    # calculate the overall score when there are multiple metrics.
    similarityWeight = 0.6
    groundedNessWeight = 0.4
    return (row["outputs.similarity.similarity"] * similarityWeight + row["outputs.groundedness.groundedness"] * groundedNessWeight)/5

def record_run_result(result: dict[str, Any]) -> list[dict[str, Any]]:
    run_result = []
    total_score = 0

    for row in result["rows"]:
        score = calculate_overall_score(row)
        total_score += score
        # rules = [rule["rule_ids"] for rule in json.loads(row["inputs.response"])["comments"]]
        # rule_ids.update(*rules)

        run_result.append(
            {
                "testcase": row["inputs.testcase"],
                "similarity": row["outputs.similarity.similarity"],
                "gpt_similarity": row["outputs.similarity.gpt_similarity"],
                "similarity_threshold": row["outputs.similarity.similarity_threshold"],
                "similarity_result": row["outputs.similarity.similarity_result"],
                "groundedness": row["outputs.groundedness.groundedness"],
                "gpt_groundedness": row["outputs.groundedness.gpt_groundedness"],
                "groundedness_threshold": row["outputs.groundedness.groundedness_threshold"],
                "groundedness_result": row["outputs.groundedness.groundedness_result"],
                "overall_score": score,
            }
        )

    average_score = total_score / len(result["rows"])
    run_result.append({"average_score": average_score, "total_evals": len(result["rows"])})
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

def output_table(baseline_results: dict[str, Any], eval_results: list[dict[str, Any]], file_name: str) -> None:
    headers = [
        "Test Case",
        "Similarity",
        "Similarity Result",
        "Groundedness",
        "Groundedness Result",
        "Score"
    ]
    terminal_rows = []

    for result in eval_results[:-1]:  # Skip summary object
        testcase = result["testcase"]
        score = result["overall_score"]
        sim = result["similarity"]
        sim_result = result["similarity_result"]
        groundedness = result['groundedness']
        groundedness_result = result['groundedness_result']
        terminal_row = [testcase]
        values = [
            f"{sim:.1f}",
            f"{sim_result}",
            f"{groundedness}",
            f"{groundedness_result}",
            f"{score:.1f}"
        ]
        terminal_row.extend(values)
        terminal_rows.append(terminal_row)

    print("====================================================")
    print(f"\n\n✨ {file_name} results:\n")
    print(tabulate(terminal_rows, headers, tablefmt="simple"))
    if baseline_results:
        print(
            f"\n{file_name} average score: {eval_results[-1]['average_score']} {format_terminal_diff(eval_results[-1]['average_score'], baseline_results['average_score'])}\n\n"
        )

def show_results(args: argparse.Namespace, all_results: dict[str, Any]) -> None:
    """Display results in a table format."""
    for name, test_results in all_results.items():
        baseline_results = {}
        baseline_path = pathlib.Path(__file__).parent / "results" / name[:-1]

        if baseline_path.exists():
            with open(baseline_path, "r") as f:
                baseline_data = json.load(f)
                for result in baseline_data[:-1]:  # Skip summary
                    baseline_results[result["testcase"]] = result
                baseline_results["average_score"] = baseline_data[-1]["average_score"]

        output_table(baseline_results, test_results, name)

if __name__ == "__main__":

    print("🚀 Starting evaluation ...")

    parser = argparse.ArgumentParser(description="Run evals for Azure Chat Bot.")

    parser.add_argument("--test_folder", type=str, help="the path to the test folder")
    parser.add_argument("--prefix", type=str, help="Process only files starting with this prefix")
    parser.add_argument("--is_bot", type=str, default="True", help="Use bot API for processing Q&A pairs (True/False)")
    parser.add_argument("--is_cli", type=str, default="True", help="Run in CI/CD pipeline (True/False)")
    parser.add_argument("--evaluation_name", type=str, help="the name of evaluation")
    parser.add_argument("--skip_data_process", type=str, default="False", help="skip to pre-process the test data")
    parser.add_argument("--send_result", type=str, default="True", help="Send the evaluation result to AI foundry project")
    args = parser.parse_args()

    args.is_bot = args.is_bot.lower() in ('true', '1', 'yes', 'on')
    args.is_cli = args.is_cli.lower() in ('true', '1', 'yes', 'on')
    args.skip_data_process = args.skip_data_process.lower() in ('true', '1', 'yes')
    args.send_result = args.send_result.lower() in ('true', '1', 'yes')

    
    script_directory = os.path.dirname(os.path.abspath(__file__))
    print("Script directory:", script_directory)

    
    current_file_path = os.getcwd()
    print("Current working directory:", current_file_path)


    if (args.test_folder == None):
        args.test_folder = os.path.join(script_directory, "tests")
    
    print(f"test folder: {args.test_folder}")
    # Required environment variables
    load_dotenv()
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
    all_results = {}
    try: 
        print("📊 Preparing dataset...")
        kwargs = {}
        if args.send_result:
            if args.is_cli:
                # service_connection_id = os.environ["AZURESUBSCRIPTION_SERVICE_CONNECTION_ID"]
                # client_id = os.environ["AZURESUBSCRIPTION_CLIENT_ID"]
                # tenant_id = os.environ["AZURESUBSCRIPTION_TENANT_ID"]
                # system_access_token = os.environ["SYSTEM_ACCESSTOKEN"]

                # kwargs = {
                #     "credential": AzurePipelinesCredential(
                #         service_connection_id=service_connection_id,
                #         client_id=client_id,
                #         tenant_id=tenant_id,
                #         system_access_token=system_access_token,
                #     )
                # }
                kwargs = {
                    "credential": DefaultAzureCredential()
                }
            else:
                kwargs = {
                    # run in local, use Azure Cli Credential, make sure you already run `az login`
                    "credential": AzureCliCredential()
                }
        
        output_file_dir = asyncio.run(prepare_dataset(args.test_folder, args.prefix, args.is_bot))
        for output_file in output_file_dir.glob("*.jsonl"):
            run_results = []
            result = evaluate(
                data=output_file,
                evaluators={
                    "similarity": SimilarityEvaluator(model_config=model_config),
                    "groundedness": GroundednessEvaluator(model_config=model_config)
                },
                evaluation_name=args.evaluation_name if args.evaluation_name else os.path.splitext(os.path.basename(output_file))[0],
                # column mapping
                evaluator_config={
                    "similarity": {
                        "column_mapping": {
                            "query": "${data.query}",
                            "response": "${data.response}",
                            "context": "${data.context}",
                            "ground_truth": "${data.ground_truth}",
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
            print("✅ Evaluation completed. Results:", result)
            run_result = record_run_result(result)
            all_results[output_file.name] = run_result
    except Exception as e:
        print(f"❌ Error occurred: {str(e)}")
        import traceback
        traceback.print_exc()
    
    show_results(args, all_results)