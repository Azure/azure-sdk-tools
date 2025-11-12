# Azure SDK AI Bot Evaluations

This directory contains evaluation testing for the Azure SDK AI bot.

## Prerequisites

- Python 3.9+
- Azure OpenAI (endpoint and key)
- Environment variables set up either in a `.env` file or in the system environment. Refer to the [env-variables](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/env-variables) file for required environment variables.

## Running Evaluations

### In DevOps pipeline

Offline evaluation runs can be triggered by the [tools - azure-sdk-ai-bot-evaluate-ci](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7918) pipeline. Results of the run can be found on the Evaluation tab in the Azure AI Foundry portal. The pipeline will automatically trigger on pull requests in the azure/azure-sdk-tools repository.

Online evaluation runs can be triggered by the [tools - sdk-ai-bot-online-evaluation](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7913) pipeline. Results of the run can be found on the Evaluation tab in the Azure AI Foundry portal. The pipeline is scheduled to trigger automatically to verify the performance of the SDK QA bot in the production environment.

### Locally

Running evaluations locally will execute evaluations on test files.

The main evaluation script is `evals_run.py`. Here are common ways to use it:

1. Run all tests for a specific language:
```bash
python evals_run.py
```

2. Run tests in a specific test folder:
```bash
python evals_run.py --test_folder specific_test_folder
```

3. Run tests for a specific scenario:

```bash
python evals_run.py --prefix specific_file_prefix
```


## Create New Evals

An evaluation test file should be written in JSONL format, and a file can contain multiple test cases. Each line in the JSONL file represents a distinct test case. A test case should be structured as follows:

```json
{
    "testcase": "unique_test_name",
    "query": "question_to_query",
    "ground_truth": "expected_answer",
    "context": "knowledge_text_for_query"
}
```

`testcase` is the name of the test case and should ideally describe what is being tested.

`query` is the question to query the bot.

`ground_truth` contains the expected answer.

`context` provides the knowledge text from RAG (Retrieval-Augmented Generation).

## Results and Baselines

- Test results are stored in `results/`
- After running evaluations, you can choose to establish a new baseline by answering `y` when the evaluations finish. If you answer `n`, the baseline will not be updated, but the results will be cached in `results/.log`. 

## Evaluation Metrics

Current measures:
- Groundedness (context adherence to query)
- Similarity to expected responses

Weights are applied to each metric to calculate the overall score.
