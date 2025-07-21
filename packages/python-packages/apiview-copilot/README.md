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
4. Generate a review using `python cli.py review generate --language <lang> --target <path_to_target_file> [--base <path_to_base_file>] [--debug-log] [--remote]`.
5. Examine the output under `scratch/output/<lang>/<test_file>.json`.

## Review Process and Stages

For each section of the APIView, the review process now consists of three distinct stages:

- **Guideline Stage:** Reviews the section against language-specific guidelines.
- **Context Stage:** Reviews the section using the full context (guidelines, examples, and memories) retrieved for that section.
- **Generic Stage:** Applies generic review rules and best practices.

## Creating Reviews

```
python cli.py review generate --language <lang> --target <path_to_target_file> [--base <path_to_base_file>] [--debug-log] [--remote]
```

- Use `--debug-log` to dump kept and discarded comments to files for debugging purposes. Only supported when calls are made locally.
- Use `--remote` to generate a review using the deployed Copilot app rather than making local calls.

## Flask App in App Service

Commands available for working with the Flask app:

- `python cli.py app deploy`: Deploy the Flask app to Azure App Service.

## Running Evaluations

To run evaluations, see: [evals/README.md](./evals/README.md)

## Search Commands

Commands available for querying the search indexes:

- `python cli.py search guidelines`: Search the guidelines for a query.
- `python cli.py search examples`: Search the examples index for a query.
- `python cli.py search kb`: This searches the examples and guidelines index for a query. It will resolve references and return a `Context` object that is filled into the prompt.

If you would like to search the knowledge base and see the output the way the LLM will see it, you can do the following:

`python cli.py search kb --text "query" -l <lang> --markdown > context.md`

This will dump the results to context.md which you can then view in VSCode with the preview editor.

## Getting Comments from APIView

If you need to retrieve comments from APIView, you can use the following command:

`python cli.py apiview get-comments --review-id <ID> [--environment "production"|"staging"]`

This command retrieves comments from APIView for a specific review ID. You can specify the environment (production or staging) to get the comments from the appropriate APIView instance, but the default is production.

If you need RBAC permissions to access CosmosDB, you can run the following script:
`python scripts\apiview_permissions.py`

You must be logged in to the "Azure SDK Engineering System" subscription (`az login`) and have the necessary permissions
for this script to succeed.

## Reporting Metrics

To report metrics, you can use the following command:
`python cli.py report metrics -s <YYYY-MM-DD> -e <YYYY-MM-DD> [--markdown] [--environment "production"|"staging"]`

Specify the start and end dates for the metrics you want to report. The `--markdown` option will pass the results through an LLM to summarize the results in markdown. The `--environment` option allows you to specify whether to report metrics from the production or staging environment, with production being the default.

To dump the markdown results to file:
`python cli.py report metrics -s <YYYY-MM-DD> -e <YYYY-MM-DD> --markdown > metrics.md`

## Notes

On Windows CMD.exe, you can use `cli.bat` in lieu of `python cli.py` for all CLI commands.

## Documentation

For more information, visit the [API Documentation](https://apiviewuat.azurewebsites.net/swagger/index.html).
