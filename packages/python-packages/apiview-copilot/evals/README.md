# APIView Copilot Evaluations

This directory contains the evaluation testing for APIView Copilot.

## Prerequisites

- Python 3.9+
- Azure OpenAI (endpoint and key)
- Environment variables set up in a `.env` file:
  ```
  AZURE_OPENAI_ENDPOINT=<your-endpoint>
  AZURE_OPENAI_API_KEY=<your-key>
  ```

> Note: the API key is needed for AI-assisted evaluations. The service does not support Entra ID yet.

## Running Evaluations

The main evaluation script is `run.py`. Here are the common ways to use it:

1. Run all tests for a specific language:
```bash
python run.py --language python
```

2. Run a specific test file:
```bash
python run.py --language python --test-file specific_tests.jsonl
```

3. Change the number of evaluation runs (default is 3):
```bash
python run.py --language python --n 5
```

## Adding New Test Cases

A test should be written in JSONL, and a file can contain multiple test cases. Each test case should be structured as follows:

```json
{
    "testcase": "unique_test_name",
    "query": "APIView text code to review",
    "response": {"status": "Error", "violations": [{...}, {...}]},
    "language": "python",
    "context": "guidelines context for violations present"
}
```

`query` is the entire APIview txt code to be reviewed, `response` contains the expected output, and `context` provides the guidelines context that is relevant to the violations present in the code.

To add a new test case, the following workflow is recommended:

1. Use the "Copy review text" button in the APIview UI to copy the text code.
2. Apply the desired guideline violations that you want to test to the code.
3. Run the CLI to generate an expected output: `python cli.py review generate --language <language> --path <path-apiview-text> --model <model-name>`
4. Once happy with the expected output, you can write the new test case by running the following command:

```bash
python construct_testcase.py --language python --apiview-path apiview_txt --expected-path expected.json --file-path test.jsonl --name test_name
```

## Editing Test Cases

To break down a test case into separate files for easier editing:

```bash
python deconstruct_testcase.py --language python --test-file tests/python/test.jsonl --test-case test_name
```

This will create:
- `tests/python/test_name.txt` - containing the APIview txt code
- `tests/python/test_name.json` - containing the expected JSON results

Edit the test files accordingly and then add the test case back by running the construct_testcase.py script again, adding the `--overwrite` argument.

## Results and Baselines

- Test results are stored in `results/<language>/`
- Overall coverage is calculated when all tests are ran and stored in `results/<language>/coverage.json`
- After running evals, you can choose to establish a new baseline by answering `y` after the evals finish.

## Evaluation Metrics

Current measures:
- Exact matches (right rule, right line)
- Fuzzy matches (right rule, wrong line (but close))
- False positives
- Groundedness (adherence to guidelines)
- Similarity to expected responses

Weights are applied to each metric to calculate the overall score.
