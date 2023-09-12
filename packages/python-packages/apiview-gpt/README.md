# APIView GPT Reviewer 

This GPT-enabled tool is designed to produce automated reviews of APIView.

## Parsing Guidelines
To generate the guidelines in JSON format:
1. Install this package with `pip install .` or `pip install -e .` for an editable install.
2. Create a `.env` file with the following contents:
```
AZURE_SDK_REPO_PATH="{path to the Azure SDK repo which contains the guidelines}/azure-sdk"
REST_API_GUIDELINES_PATH="{path to the REST API guidelines repo}/api-guidelines"
```
3. Run `python parse_guidelines.py`. Guidlines will be overwritten in the `guidelines` folder.

## Generating a Review
To generate a review in JSON format:
1. Install this package with `pip install .` or `pip install -e .` for an editable install.
2. Create a `.env` file with the following contents:
```
OPENAI_API_BASE="" # The OpenAI endpoint URL
OPENAI_API_KEY="" # The OpenAI API key
APIVIEW_GPT_SERVICE_URL=https://apiview-gpt.azurewebsites.net
```
3. Run `python cli.py review generate --language <LANG> --path <PATH>`
4. Output will be stored in `scratch\output\<INPUT_FILENAME>.json`.

## Working With Semantic Documents

APIView GPT uses semantic search to ground the ChatGPT request. To interact with the semantic database:
1. Add the following to your `.env` file:
```
APIVIEW_API_KEY="" # The APIView API key
``` 
2. Run `python cli.py vector --help` to see available commands.

### Adding Semantic Documents

To add new semantic documents, create a JSON file with the following format:
```
{
    "language": "python",                           # can use any language APIView supports
    "badCode": "def foo():\n    pass\n",            # must be a single line
    "goodCode": "def foo():\n    return None\n",    # Optional. Must be a single line
    "comment": "You should always have a return",   # Optional. Free text comment about the bad code pattern
    "guidelineIds": [
        "python_design.html#python-return-statements"  # Optional. List of guideline IDs that this document is related to
    ]
}
```
The JSON file you can create should be a JSON array of one or more of the above document objects. Then run:
`python cli.py vector create --path <PATH_TO_JSON>`

The command will iterate through the JSON file and create a semantic document for each object.

### Searching Semantic Documents

Generating a review searches the semantic document database prior to sending the request to OpenAI. To search the semantic database directly (as in, for testing),
run the following command:
`python cli.py vector search --language <LANGUAGE> --path <PATH_TO_CODE>`

Here code is a text file of the APIView you want to evaluate. 

## Documentation

https://apiviewuat.azurewebsites.net/swagger/index.html
