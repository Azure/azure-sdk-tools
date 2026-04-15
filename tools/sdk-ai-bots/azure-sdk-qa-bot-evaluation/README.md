# Azure SDK AI Bot Evaluations

This directory contains evaluation testing for the Azure SDK AI bot.

## Prerequisites

- Python 3.9+
- Azure OpenAI (endpoint and key)
- Environment variables set up either in a `.env` file or in the system environment. Refer to the [env-variables](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/env-variables) file for required environment variables.

## Running Evaluations

### In DevOps pipeline

Offline evaluation runs can be triggered by the [tools - azure-sdk-ai-bot-evaluate-ci](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7918) pipeline. Results of the run can be found on the Evaluation tab in the Azure AI Foundry portal. The pipeline will automatically trigger on pull requests in the azure/azure-sdk-tools repository. The evaluation result will be published as artifact 'evaluation-results'

Online evaluation runs can be triggered by the [tools - sdk-ai-bot-online-evaluation](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7913) pipeline. Results of the run can be found on the Evaluation tab in the Azure AI Foundry portal. The pipeline is scheduled to trigger automatically to verify the performance of the SDK QA bot in the production environment.

#### How to resolve evaluation CI pipeline failures

When the offline evaluation pipeline [tools - azure-sdk-ai-bot-evaluate-ci](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7918) fails in a PR, the PR is blocked and cannot be approved until the failure is properly triaged.

**PR owner responsibilities**
When the evaluation CI fails, the PR owner must:
1. Download the 'evaluation-results' artifact from the pipeline run.
2. Review each failed case recorded in <category>-failed-cases-YYYY-MM-DD-HH-SS.json.
3. Determine whether each failed case is caused by changes introduced in the PR:
   1. If caused by the PR, fix the issue and update the PR.
   2. If not caused by the PR, provide a clear explanation and supporting context.
4. Add a PR comment confirming that:
   1. All evaluation failures have been reviewed.
   2. Each failure has been triaged with a clear reason or resolution.

**Reviewer responsibilities**

After the PR owner has completed the triage and documented the results in the PR comments, the reviewer may proceed with approving the PR.

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
    "expected_knowledges": [
        {
            "title": "sample_knowledge_title",
            "link": "https://example.com/knowledge"
        }
    ],
    "expected_references": [
        {
            "title": "sample_reference_title",
            "link": "https://example.com/reference"
        }
    ]
}
```

`testcase` is the name of the test case and should ideally describe what is being tested.

`query` is the question to query the bot.

`ground_truth` contains the expected answer.

`expected_knowledges` provides the expected knowledge list. Each knowledge is {"title": string, "link": string} object.

`expected_references` provides the expected reference list. Each reference is {"title": string, "link": string} object.

## Results and Baselines

- Test results are stored in `results/`
- After running evaluations, you can choose to establish a new baseline by answering `y` when the evaluations finish. If you answer `n`, the baseline will not be updated, but the results will be cached in `results/.log`. 

## Evaluation Metrics

Current measures:
- Groundedness (context adherence to query)
- Similarity to expected responses

Weights are applied to each metric to calculate the overall score.

## Pre-commit Hooks

Pre-commit hooks are enabled to ensure code quality. There are two hooks : mypy, pyright enabled. After cloning, install the hooks by running:
```bash
pre-commit install
```
To run all checks manually, execute:
```bash
pre-commit run --all-files
```

