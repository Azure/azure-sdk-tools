import os
import asyncio
from datetime import datetime
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    Evaluation,
    InputDataset,
    EvaluatorConfiguration,
    ConnectionType,
    EvaluatorIds,
)
from azure.ai.evaluation import evaluate
from azure.ai.evaluation import (
    SimilarityEvaluator,
    QAEvaluator,
    AzureOpenAIModelConfiguration,
)
import re
import json
import aiohttp
import asyncio
import time
import os
from pathlib import Path
from typing import List, Dict, Any, Tuple, Optional
from dotenv import load_dotenv
load_dotenv()

"""Parser for data folder format with # Title, ## Question/Answer sections"""
async def parse_data(file_path: str) -> List[Tuple[str, str]]:
    qa_pairs = []
    current_title = None
    current_question = []
    current_answer = []
    in_question_section = False
    in_answer_section = False
    
    with open(file_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    # Process file by sections (each section starts with # Title)
    sections = []
    current_section = []
    
    for line in lines:
        if line.strip().startswith('# ') and current_section:
            sections.append(current_section)
            current_section = []
        current_section.append(line)
        
    if current_section:
        sections.append(current_section)
        
    # Process each section
    for section in sections:
        current_title = None
        current_question = []
        current_answer = []
        in_question_section = False
        in_answer_section = False
        
        for line in section:
            line = line.strip()
            
            # Extract title (starts with single #)
            if line.startswith('# ') and not current_title:
                current_title = line[2:].strip()
                continue
            
            # Detect Question and Answer sections
            if line.startswith('## question') | line.startswith('## Question'):
                in_question_section = True
                in_answer_section = False
                continue
            elif line.startswith('## answer') | line.startswith('## Answer'):
                in_question_section = False
                in_answer_section = True
                continue
            
            # Collect question and answer content
            if in_question_section and line and not line.startswith('#'):
                current_question.append(line)
            elif in_answer_section and line and not line.startswith('#'):
                current_answer.append(line)
        
        # Combine title and question
        if current_title:
            full_question = current_title
            if current_question:
                question_text = '\n'.join(current_question).strip()
                full_question = f"title: {current_title}\n\nquestion: {question_text}"
            
            answer_text = '\n'.join(current_answer).strip()
            
            if full_question and answer_text:
                qa_pairs.append((full_question, answer_text))
        
    return qa_pairs

async def call_completion_api(question: str) -> Dict[str, Any]:
    """Call the completion API endpoint."""
    api_url = "http://localhost:8088/completion"
    headers = {
        "Content-Type": "application/json; charset=utf8",
        "X-API-Key": "xK9d#mP2$vR4nL7@jF5hQ8*wC3tY6bN9eZ2^mA4uW8gB"
    }
    payload = {
        "tenant_id": "azure_sdk_qa_bot",
        "message": {
            "role": "user",
            "content": question
        },
        "with_preprocess": True,
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(api_url, json=payload, headers=headers) as resp:
            if resp.status == 200:
                return await resp.json()
            else:
                raise Exception(f"API request failed with status {resp.status}")

async def process_qa_pair(question: str, ground_truth: str) -> Dict[str, Any]:
    """Process a single Q&A pair and generate test case."""
    start_time = time.time()
    
    try:
        api_response = await call_completion_api(question)
        latency = time.time() - start_time
        
        return {
            "query": question,
            "ground_truth": ground_truth,
            "response": api_response.get("answer", ""),
            "context": "",
            "latency": latency,
            "response_length": len(api_response.get("answer", ""))
        }
    except Exception as e:
        print(f"Error processing question '{question}': {str(e)}")
        return None

async def process_file(input_file: str, output_file: str) -> None:
    """Process a single input file"""
    print(f"Processing file: {input_file}")
    try:
        qa_pairs = await parse_data(input_file)
        total_pairs = len(qa_pairs)
        print(f"Found {total_pairs} Q&A pairs in {input_file}")
        
        for idx, (question, answer) in enumerate(qa_pairs, 1):
            print(f"Processing Q&A pair {idx}/{total_pairs} ({idx/total_pairs*100:.1f}%)...")
            result = await process_qa_pair(question, answer)
            if result:
                with open("output/"+output_file, 'a', encoding='utf-8') as f:
                    f.write(json.dumps(result, ensure_ascii=False) + '\n')
                print(f"✓ Successfully processed and saved Q&A pair {idx}/{total_pairs}")
            else:
                print(f"✗ Failed to process Q&A pair {idx}/{total_pairs}")
            
    except Exception as e:
        print(f"Error processing file {input_file}: {str(e)}")

async def prepare_dataset(file_prefix: str = None) -> str:
    """
    Process markdown files in the data directory and generate Q&A pairs.
    
    Args:
        file_prefix: Optional prefix to filter which markdown files to process.
                    If provided, only files starting with this prefix will be processed.
    """
    data_dir = Path("data")
    output_dir = Path("output")
    output_dir.mkdir(exist_ok=True)
    
    if not data_dir.exists():
        return
    
    current_date = datetime.now().strftime("%Y_%m_%d")
    output_file = f"collected_qa_{current_date}.jsonl"

    
    # Process markdown files in the folder, optionally filtered by prefix
    glob_pattern = f"{file_prefix}*.md" if file_prefix else "*.md"
    matching_files = list(data_dir.glob(glob_pattern))
    
    if matching_files:
        print(f"Found {len(matching_files)} files matching prefix '{file_prefix}' in {data_dir}/")
        for file_path in matching_files:
            await process_file(str(file_path), str(output_file))
    elif file_prefix:
        print(f"No files found matching prefix '{file_prefix}' in {data_dir}/")
        
    print("Processing complete. Results written to output directory.")
    return output_file

async def create_cloud_evaluation_task(filename: str):
    """
    Create an evaluation task for the Azure SDK QA bot.
    This function sets up the evaluation environment, including the dataset and evaluators.
    """

    # Required environment variables
    project_endpoint = os.environ["PROJECT_ENDPOINT"]
    model_endpoint = os.environ["AZURE_ENDPOINT"]
    model_api_key = os.environ["AZURE_API_KEY"]
    model_deployment_name = os.environ["AZURE_DEPLOYMENT_NAME"]

    # Optional – reuse an existing dataset
    dataset_name    = filename.strip().rsplit('.', 1)[0]  # Remove file extension
    dataset_version = "0.0.2"

    # Create the project client (Foundry project and credentials)
    project_client = AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential(),
    )

    # Try to get existing dataset first, if not found then upload
    try:
        # Try to get existing dataset
        existing_dataset = project_client.datasets.get(
            name=dataset_name,
            version=dataset_version,
        )
        data_id = existing_dataset.id
        print(f"✅ Found existing dataset: {dataset_name} v{dataset_version}")
    except Exception as e:
        # Dataset doesn't exist, upload a new one
        print(f"⚠️  Dataset {dataset_name} v{dataset_version} not found. Uploading new dataset...")
        data_id = project_client.datasets.upload_file(
            name=dataset_name,
            version=dataset_version,
            file_path="./output/" + filename,
        ).id
        print(f"✅ Successfully uploaded new dataset: {dataset_name} v{dataset_version}")


    # Create an evaluation with the dataset and evaluators specified
    current_date = datetime.now().strftime("%Y_%m_%d")
    evaluation_name = f"evaluation_channel_collected_qa_{current_date}"
    evaluation = Evaluation(
        display_name=evaluation_name,
        description="Evaluation of dataset",
        data=InputDataset(id=data_id),
        evaluators={
            "qa": EvaluatorConfiguration(
                id=EvaluatorIds.QA.value,
                init_params={
                    "deployment_name": model_deployment_name,
                },
                data_mapping={
                    "query": "${data.query}",
                    "response": "${data.response}",
                    "context": "${data.context}",
                    "ground_truth": "${data.ground_truth}",
                }
            )
        }
    )

    # Run the evaluation 
    evaluation_response = project_client.evaluations.create(
        evaluation,
        headers={
            "model-endpoint": model_endpoint,
            "api-key": model_api_key,
        },
    )

    print("✅ Created evaluation job. ID:", evaluation_response)

async def create_local_evaluation_task(filename: str):
    """
    Create an evaluation task for the Azure SDK QA bot.
    This function sets up the evaluation environment, including the dataset and evaluators.
    """

    # Required environment variables
    project_endpoint = os.environ["PROJECT_ENDPOINT"]
    # :param type: The type of the model configuration. Should be 'azure_openai' for AzureOpenAIModelConfiguration
    # :type type: NotRequired[Literal["azure_openai"]]
    # :param azure_deployment: Name of Azure OpenAI deployment to make requests to
    # :type azure_deployment: str
    # :param azure_endpoint: Endpoint of Azure OpenAI resource to make requests to
    # :type azure_endpoint: str
    # :param api_key: API key of Azure OpenAI resource
    # :type api_key: str
    # :param api_version: API version to use in request to Azure OpenAI deployment. Optional.
    # :type api_version: NotRequired[str]
    model_config = AzureOpenAIModelConfiguration(
        azure_deployment=os.environ.get("AZURE_DEPLOYMENT_NAME"),
        azure_endpoint=os.environ.get("AZURE_ENDPOINT"),
        api_key=os.environ.get("AZURE_API_KEY"),
        api_version=os.environ.get("AZURE_API_VERSION"),
    )

    result = evaluate(
        data="./output/" + filename, # provide your data here
        evaluators={
            "qa": QAEvaluator(
               model_config=model_config
            )
        },
        # column mapping
        evaluator_config={
            "qa": {
                "column_mapping": {
                    "query": "${data.query}",
                    "response": "${data.response}",
                    "context": "${data.context}",
                    "ground_truth": "${data.ground_truth}",
                } 
            }
        },
        # Optionally provide your Azure AI Foundry project information to track your evaluation results in your project portal
        azure_ai_project = project_endpoint,
        # Optionally provide an output path to dump a json of metric summary, row level data and metric and Azure AI project URL
        output_path="./evalresults.json"
    )
    print("✅ Evaluation completed. Results:", result)


if __name__ == "__main__":
    # import argparse
    
    # parser = argparse.ArgumentParser(description="Process Q&A pairs from markdown files.")
    # parser.add_argument("--prefix", type=str, help="Process only files starting with this prefix")
    # args = parser.parse_args()

    # output_file = asyncio.run(prepare_dataset(args.prefix))
    # if output_file:
    #     asyncio.run(create_evaluation_task(output_file))
    # else:
    #     print("No files processed. Exiting.")
    asyncio.run(create_local_evaluation_task("collected_qa_2025_06_30.jsonl"))
