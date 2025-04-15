# APIView Copilot Reviewer

This tool is designed to produce automated reviews of APIView.

## Getting Started

The simplest way to get started with the project would be to follow these steps:

1. Install this package with `pip install -r requirements.txt` or `pip install -r dev_requirements.txt` if you also need to run evaluations.
2. Create a `.env` file with the following contents:

```
AZURE_OPENAI_ENDPOINT="..." # The Azure OpenAI endpoint URL
```

3. Create one or more test files in plain-text for the language of choice. Store them in `scratch/apiviews/<lang>/`.
4. Generate a review using `python cli.py review generate --language <lang> --path <path_to_test_file> --model <model> [--chunk-input]`.
5. Examine the output under `scratch/output/<lang>/<test_file>.json`.

## Generating Reviews with RAG

To utilize the RAG (retrieval-augmented generation) capabilities of APIView Copilot, you will need to set up a few additional things:

1. Install this package with `pip install -r requirements.txt` or `pip install -r dev_requirements.txt` if you also need to run evaluations.
2. Create a `.env` file with the following contents:

```
AZURE_OPENAI_ENDPOINT="..." # The Azure OpenAI endpoint URL
AZURE_SEARCH_NAME="..."     # The name of the Azure AI Search resource. Required only for RAG
AZURE_COSMOS_ACC_NAME="..." # The name of the CosmosDB account. Required only for RAG
AZURE_COSMOS_DB_NAME="..."  # The name of the CosmosDB database. Required only for RAG
```

3. Create one or more test files in plain-text for the language of choice. Store them in `scratch/apiviews/<lang>/`.
4. Generate a review using `python cli.py review generate --language <lang> --path <path_to_test_file> --model <model> --use-rag [--chunk-input]`.
5. Examine the output under `scratch/output/<lang>/<test_file>.json`.

## Creating Reviews with Development Code

- `cli.bat review local`: Generate a review using the development code. This will still make networking calls.

## Flask App in App Service

Commands available for working with the Flask app:

- `cli.bat app deploy`: Deploy the Flask app to Azure App Service.
- `cli.bat review remote`: Generate a review using the Flask app in Azure App Service.

## Running Evaluations

To run evaluations, see: [evals/README.md](./evals/README.md)

## Documentation

https://apiviewuat.azurewebsites.net/swagger/index.html
