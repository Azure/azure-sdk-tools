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

## Create Reviews Help Text

```text
    --language -l [Required] : The language of the APIView file.
    --model -m    [Required] : The model to use for the review.  Allowed values: gpt-4o-mini,
                               o3-mini.
    --path        [Required] : The path to the APIView file.
    --chunk-input            : Chunk the input into smaller sections (currently, by class).
    --log-prompts            : Log each prompt in ascending order in the `scratch/propmts` folder.
    --use-rag                : Use RAG pattern to generate the review.
```

## Running Evaluations

To run evaluations, see: [evals/README.md](./evals/README.md)

## Documentation

https://apiviewuat.azurewebsites.net/swagger/index.html
