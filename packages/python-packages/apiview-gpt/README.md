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
```
3. Run `python generate_review.py`. Right now it will just use `text.txt` as the input.
4. Output will be stored in `output.json`.
