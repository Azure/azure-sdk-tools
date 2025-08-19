# APIView Copilot Evaluations

This directory contains the evaluation testing for APIView Copilot.

## Prerequisites

- Python 3.9+
- Azure OpenAI (endpoint and key)
- Environment variables set up in a `.env` file as shown in the ApiView Copilot README.

## Running Evaluations

### In DevOps pipeline

Evals runs can be triggered by the [tools - apiview-copilot - tests](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7662&_a=summary) pipeline. Results of the run can be found on the Evaluation tab in the Azure AI Foundry portal for the `apiview-ai` project. 

### Locally

Running evaluations will run evals on test files for the language given and give the choice to record the baseline (aka write the results to `evals/results/language`). 

The main evaluation script is `run.py`. Here are the common ways to use it:

1. Run all tests for a specific language:
```bash
python run.py --language python
```

2. Run a specific test file:
```bash
python run.py --language python --test-file specific_tests.jsonl
```

or 

```bash
python run.py --language python --test-file tests/python/specific.jsonl
```

3. Change the number of evaluation runs (default is 3):
```bash
python run.py --language python --n 5
```

> Note: Due to variability in AI model responses, the number of runs can be increased to get a more stable result (the median of the results is chosen as the final result).


## Create New Evals

An eval test file should be written in JSONL, and a file can contain multiple test cases. Each line in the JSONL represents a distinct test case. A test case should be structured as follows:

```json
{
    "testcase": "unique_test_name",
    "query": "APIView text code to review",
    "response": {"status": "Error", "violations": [{...}, {...}]},
    "language": "python",
    "context": "guidelines context for violations present"
}
```

`testcase` is the name of the test case and ideally says something about what's being tested.

`query` is the APIview txt code to be reviewed.

`response` contains the expected JSON output from running the AI reviewer.

`context` provides the context that is relevant to the violations present in the code. This is automatically pulled from the static guidelines based on the expected violations present if the `eval create` command is used (described below).

`language` is the language of the APIview text code.

To add a new test case, the following workflow is recommended:

1. Use the "Copy review text" button in the APIview UI to copy the text code.
2. Apply the desired guideline violations that you want to test to the code.
3. Run the CLI to generate an expected output: `python cli.py review generate --language <language> --path <path-apiview-text> --model <model-name>`
4. Once happy with the expected output, you can write the new test case by running the following command:

```bash
python cli.py eval create --language python --apiview-path path/to/apiview.txt --expected-path path/to/expected.json --test-file path/to/test.jsonl --name testcase_name
```

## Editing Test Cases

You may want to edit a test case after it has been created. This can be done by running the `deconstruct` command, which will break down the test case into separate files for easier editing.

```bash
python cli.py eval deconstruct --language python --test-file path/to/test.jsonl --test-case testcase_name
```

This will create:
- `tests/python/testcase_name.txt` - containing the APIview txt code
- `tests/python/testcase_name.json` - containing the expected JSON results

Edit the test files accordingly and then add the test case back by running the `eval create` command again, this time adding the `--overwrite` argument.

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
