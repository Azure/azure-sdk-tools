# APIView Copilot Reviewer

This tool is designed to produce automated reviews of APIView.

## Getting Started

The simplest way to get started with the project would be to follow these steps:

1. Install this package with `pip install -r requirements.txt` or `pip install -r dev_requirements.txt` if you also need to run evaluations.
2. Create a `.env` file with the following contents:

```
AZURE_OPENAI_ENDPOINT="..." # The Azure OpenAI endpoint URL
AZURE_SEARCH_NAME="..."     # The name of the Azure AI Search resource
AZURE_COSMOS_ACC_NAME="..." # The name of the CosmosDB account
AZURE_COSMOS_DB_NAME="..."  # The name of the CosmosDB database
```

3. Create one or more test files in plain-text for the language of choice. Store them in `scratch/apiviews/<lang>/`.
4. Generate a review using `python cli.py review generate --language <lang> --target <path_to_target_file> [--base <path_to_base_file>] [--debug-log]`.
5. Examine the output under `scratch/output/<lang>/<test_file>.json`.

## Review Process and Stages

For each section of the APIView, the review process now consists of three distinct stages:

- **Guideline Stage:** Reviews the section against language-specific guidelines retrieved from Azure Search and CosmosDB.
- **Context Stage:** Reviews the section using the full context (guidelines, examples, and memories) retrieved for that section.
- **Generic Stage:** Applies generic review rules and best practices.

All context and guidelines are retrieved dynamically from Azure Search and CosmosDB. There is no static guideline mode.

### Example: Generating a Review

```
python cli.py review generate --language <lang> --target <path_to_target_file> [--base <path_to_base_file>] [--debug-log]
```

- Use `--debug-log` to dump kept and discarded comments to files for debugging purposes.

## Creating Reviews with Development Code

- `cli.bat review local`: Generate a review using the development code. Supports the `--debug-log` option.

## Flask App in App Service

Commands available for working with the Flask app:

- `cli.bat app deploy`: Deploy the Flask app to Azure App Service.
- `cli.bat review remote`: Generate a review using the Flask app in Azure App Service.

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
