# Evals for azure-mcp

This directory contains evaluation scripts for the Azure MCP project. This primarily tests tool call accuracy and ensures that the model will call the correct MCP tool with parameters to retrieve information about Azure resources.

The Tool Call Accuracy evaluator assesses how accurately an AI uses tools by examining:

- Relevance to the conversation
- Parameter correctness according to tool definitions
- Parameter value extraction from the conversation
- Potential usefulness of the tool call

## Prerequisites

- Python 3.9+
- Install dependencies:
  ```bash
  pip install -r requirements.txt
  ```
- Set the following environment variables (can be placed in a `.env` file):
  - `AZURE_OPENAI_ENDPOINT`
  - `AZURE_OPENAI_API_KEY`
  - `AZURE_SUBSCRIPTION_ID`
  - `AZURE_FOUNDRY_RESOURCE_GROUP`
  - `AZURE_FOUNDRY_PROJECT_NAME`

The latter 3 variables are used to connect to the Azure Foundry project where the evaluation results will be stored.

## Files

- `run.py`: Main entry point for running evaluation scripts.
- `data.jsonl`: Test cases for evaluation. These include the queries to the model and the expected tool calls.
- `followup.json`: Options for follow-up answers during evaluation. Sometimes a follow-up is required to get the model to call the mcp tool. For example, if the model is asked to "list all my resource groups", it may need a follow-up question to clarify under what subscription. The `followup.json` is a mapping provided to an additional model which will answer the follow-up question based on the original query and the model's response. The mapping may not contain real resources since we currently only test whether the model calls the tool correctly, not whether it returns the correct data.

## Running Evals

1. Ensure all prerequisites are met and environment variables are set.
2. From this directory, run:
   ```bash
   python run.py
   ```
   This will execute the evaluation using the provided data and configuration.

## Reviewing Results

Results are found in the Azure Foundry project. TODO add link.

## CI pipeline

- The script will automatically detect if it is running in CI and use Azure Pipelines credentials if available.
