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
4. Generate a review using `python cli.py review generate --language <lang> --target <path_to_target_file> [--base <path_to_base_file>]`.
5. Examine the output under `scratch/output/<lang>/<test_file>.json`.

## Context Modes: RAG and Static

APIView Copilot supports two context modes for generating reviews:

- **RAG (Retrieval-Augmented Generation)**: Uses Azure AI Search and CosmosDB to retrieve relevant guidelines, examples, and memories for each code section. This is the default mode.
- **Static**: Uses only the static guidelines bundled with the tool, without any retrieval from external services.

To use RAG mode (default), ensure your `.env` file includes:

```
AZURE_OPENAI_ENDPOINT="..." # The Azure OpenAI endpoint URL
AZURE_SEARCH_NAME="..."     # The name of the Azure AI Search resource. Required for RAG
AZURE_COSMOS_ACC_NAME="..." # The name of the CosmosDB account. Required for RAG
AZURE_COSMOS_DB_NAME="..."  # The name of the CosmosDB database. Required for RAG
```

To use static mode, only `AZURE_OPENAI_ENDPOINT` is required.

### Example: Generating a Review (RAG mode)

```
python cli.py review generate --language <lang> --target <path_to_target_file> [--base <path_to_base_file>]
```

### Example: Generating a Review (Static mode)

```
python cli.py review generate --language <lang> --mode static --target <path_to_target_file> [--base <path_to_base_file>]
```

If you omit `--mode`, RAG is used by default.

## Creating Reviews with Development Code

- `cli.bat review local`: Generate a review using the development code. You can add `--mode static` or `--mode rag` to control the context mode.

## Flask App in App Service

Commands available for working with the Flask app:

- `cli.bat app deploy`: Deploy the Flask app to Azure App Service.
- `cli.bat review remote`: Generate a review using the Flask app in Azure App Service. You can use `--mode static` or `--mode rag` as with local reviews.

## Running Evaluations

To run evaluations, see: [evals/README.md](./evals/README.md)

## Search Commands

Commands available for querying the search indexes:

- `cli.bat search guidelines`: Search the guidelines for a query.
- `cli.bat search examples`: Search the examples index for a query.
- `cli.bat search kb`: This searches the examples and guidelines index for a query. It will resolve references and return a `Context` object that is filled into the prompt.

If you would like to search the knowledge base and see the output the way the LLM will see it, you can do the following:

`cli.bat search kb --text "query" -l <lang> --markdown > context.md`

This will dump the results to context.md which you can then view in VSCode with the preview editor.

## Documentation

For more information, visit the [API Documentation](https://apiviewuat.azurewebsites.net/swagger/index.html).
