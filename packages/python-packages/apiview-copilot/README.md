# APIView Copilot Reviewer

This tool is designed to produce automated reviews of APIView.

## Getting Started

The simplest way to get started:

1. Install this package with `pip install -r requirements.txt` or `pip install -r dev_requirements.txt` if you also need to run evaluations.
2. Create a `.env` file with the following contents to access the staging environment:
```
AZURE_APP_CONFIG_ENDPOINT="https://avc-appconfig-staging.azconfig.io"
ENVIRONMENT_NAME="staging"
OPENAI_ENDPOINT="https://azsdk-engsys-openai.openai.azure.com/"
```
3. Create one or more test files in plain-text for the language of choice. Store them in `scratch/apiviews/<lang>/`.
4. Generate a review using `avc review generate -l <LANG> -t <PATH_TO_TARGET_FILE> [-b <PATH_TO_BASE_FILE>] [--debug-log] [--remote]`.
5. Examine the output under `scratch/output/<LANG>/<TEST_FILE>.json`.

## Review Process and Stages

For each section of the APIView, the review process now consists of three distinct stages:

- **Guideline Stage:** Reviews the section against language-specific guidelines.
- **Context Stage:** Reviews the section using the full context (guidelines, examples, and memories) retrieved for that section.
- **Generic Stage:** Applies generic review rules and best practices.

## Creating Reviews

To synchronously create reviews locally:

```
avc review generate -l <LANG> -t <PATH_TO_TARGET_FILE> [-b <PATH_TO_BASE_FILE>] [--debug-log] [--remote]
```

- Use `--debug-log` to dump kept and discarded comments to files for debugging purposes. Only supported when calls are made locally.
- Use `--remote` to generate a review using the deployed Copilot app rather than making local calls.

To mimic how the web app generates reviews, use the following commands:
```
# start a review
avc review start-job -l <LANG> -t <PATH_TO_TARGET_FILE> [-b <PATH_TO_BASE_FILE>]

# get the job status from the output of start-job
avc review get-job --job-id <JOB_ID>
```

## Flask App in App Service

Commands available for working with the Flask app:

- `avc app deploy`: Deploy the Flask app to Azure App Service based on what App Configuration is set in your .env file.

## Running Evaluations

To run evaluations, see: [evals/README.md](./evals/README.md)

## Search Commands

Commands available for querying the search indexes:

- `avc search guidelines`: Search the guidelines for a query.
- `avc search examples`: Search the examples index for a query.
- `avc search kb`: This searches the examples and guidelines index for a query. It will resolve references and return a `Context` object that is filled into the prompt.

If you would like to search the knowledge base and see the output the way the LLM will see it, you can do the following:

`avc search kb --text "query" -l <LANG> --markdown > context.md`

This will dump the results to context.md which you can then view in VSCode with the preview editor.

## Database Commands

Commands for managing knowledge base items:

### Linking Items

```bash
avc db link -g <GUIDELINE_ID> -m <MEMORY_ID> [--reindex]
avc db link -g <GUIDELINE_ID> -e <EXAMPLE_ID> [--reindex]
avc db link -m <MEMORY_ID> -e <EXAMPLE_ID> [--reindex]
```

Links two knowledge base items by adding each other's ID to their related collections. Provide exactly two of `--guideline (-g)`, `--memory (-m)`, or `--example (-e)`. The operation is atomic — if the second update fails, the first is rolled back. Use `--reindex` to trigger a full search reindex after linking.

### Unlinking Items

```bash
avc db unlink -g <GUIDELINE_ID> -m <MEMORY_ID> [--reindex]
avc db unlink -g <GUIDELINE_ID> -e <EXAMPLE_ID> [--reindex]
avc db unlink -m <MEMORY_ID> -e <EXAMPLE_ID> [--reindex]
```

Removes the link between two knowledge base items. Same flags and atomicity guarantees as `db link`. Use `--reindex` to trigger a full search reindex after unlinking.

## Getting Comments from APIView

If you need to retrieve comments from APIView, you can use the following command:

`avc apiview get-comments --review-id <ID> [--environment "production"|"staging"]`

This command retrieves comments from APIView for a specific review ID. You can specify the environment (production or staging) to get the comments from the appropriate APIView instance, but the default is production.

### Resolving Package Information

`avc apiview resolve-package --package <PACKAGE_DESCRIPTION> --language <LANGUAGE> [--version <VERSION>] [--environment "production"|"staging"]`

This command resolves package information from a package description and language. It uses a multi-stage matching strategy (exact match → LLM fallback):
1. **Exact match**: Searches for packages that exactly match the description (case-insensitive)
2. **LLM-powered matching (fallback)**: If no exact match is found, retrieves all packages for the language and uses an LLM to find the best semantic match

Returns:
- The actual package name
- The review ID (can be used with ApiViewClient to get revisions)
- Language and version information

Note: To get the actual revision content, use the returned `review_id` with the `ApiViewClient.get_revision_text()` method.

Examples:
```bash
# Get latest revision for azure-core in Python
avc apiview resolve-package --package azure-core --language python

# Get specific version
avc apiview resolve-package --package azure-storage-blob --language python --version 12.19.0

# Use natural language descriptions - LLM will find the best match
avc apiview resolve-package --package "storage blobs" --language python
avc apiview resolve-package --package "cosmos database" --language python
```

If you need RBAC permissions to access CosmosDB, you can run the following script:
`python scripts\apiview_permissions.py`

You must be logged in to the "Azure SDK Engineering System" subscription (`az login`) and have the necessary permissions for this script to succeed.

## Reporting Metrics

Report is now available at [PowerBI](https://msit.powerbi.com/groups/3e17dcb0-4257-4a30-b843-77f47f1d4121/reports/d8fdff73-ac33-49dd-873a-3948d7cb3c48?ctid=72f988bf-86f1-41af-91ab-2d7cd011db47&pbi_source=linkShare)

Underneath, we use a script to generate the metrics. You can use the following command:
```bash
avc metrics report -s <YYYY-MM-DD> -e <YYYY-MM-DD> [--markdown] [--environment "production"|"staging"] [--charts] [--exclude <LANG1> <LANG2> ...]
```

Options:
- `-s/--start-date`: Start date for the metrics report (YYYY-MM-DD)
- `-e/--end-date`: End date for the metrics report (YYYY-MM-DD)
- `--markdown`: Pass the results through an LLM to summarize the results in markdown
- `--environment`: Specify whether to report metrics from the production or staging environment (default: production)
- `--charts`: Generate PNG charts from the metrics and save to `scratch/charts/`
- `-x/--exclude`: Languages to exclude from the report (e.g., `--exclude Java Go`)

To dump the markdown results to file:
```bash
avc metrics report -s <YYYY-MM-DD> -e <YYYY-MM-DD> --markdown > metrics.md
```

To generate charts:
```bash
avc metrics report -s 2026-01-01 -e 2026-01-31 --charts
```

This generates four PNG charts in `scratch/charts/`:
- **adoption.png**: Stacked bar chart showing Copilot vs non-Copilot reviews per language
- **comment_quality.png**: Stacked percent bar chart showing AI comment quality categories per language
- **human_copilot_split.png**: Human vs AI comments for reviews with Copilot
- **human_comments_comparison.png**: Side-by-side comparison of human comments with vs without Copilot

### Comment Quality Categories

The `comment_quality` metrics track AI comment outcomes with the following mutually exclusive categories (which sum to `ai_comment_count`):

| Category | Description |
|----------|-------------|
| `good` / `good_count` | AI comments that were upvoted |
| `implicit_good` / `implicit_good_count` | AI comments marked resolved but not voted on |
| `neutral` / `neutral_count` | AI comments in unapproved revisions with no action |
| `implicit_bad` / `implicit_bad_count` | AI comments in approved revisions with no action (not resolved, not voted) |
| `bad` / `bad_count` | AI comments that were downvoted (any downvote trumps upvotes) |
| `deleted` / `deleted_count` | AI comments that were deleted |

## Notes

On Windows CMD.exe, use `avc.bat` in lieu of `avc` for all CLI commands.

## Documentation

For more information, visit the [API Documentation](https://apiviewuat.azurewebsites.net/swagger/index.html).
