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

1. Run a test workflow file:
```bash
# APIView workflow
python run.py --language python --test-file workflows/apiview.yaml

# Mention action workflow
python run.py --language python --test-file workflows/mention-action.yaml
```

2. Change the number of evaluation runs (default is 1):
```bash
python run.py --language python --test-file workflows/filter-comment-metadata.yaml --n 5
```

> Note: Due to variability in AI model responses, the number of runs can be increased to get a more stable result (the median of the results is chosen as the final result).

## Workflow Types

- **APIView Review**: Tests the main code review functionality
- **Prompt Workflows**: Tests specific prompts in isolation

The evaluation framework supports different types of workflows

## Prompt Workflows
For testing specific prompts with structured inputs and outputs. Examples:
- `filter-comment-metadata.yaml` - Tests comment filtering based on exceptions and context
- `mention-action.yaml` - Tests conversation action parsing  
- `thread-resolution-action.yaml` - Tests thread resolution actions

Workflow YAML structure:
```yaml
name: workflow-name
kind: prompt
```

## Workflow vs Test File Relationship

- **Workflow YAML files** (`workflows/`) define how to run evaluations and which prompts to test
- **Test JSONL files** (`tests/python/`) contain the actual test case data  
- **Prompty files** (`prompts/`) contain the prompts being evaluated

The workflow YAML orchestrates the relationship between these components.

evals/
├── workflows/          # YAML files defining what to test
├── tests/python/       # JSONL files with test cases
├── results/            # Test outputs
prompts/                # Prompty files being tested


## Create New Evals

For each test, the structure varies based on the specific workflow. For example, filter-comment-metadata tests use:

```json
{
    "testcase": "filter_missing_async_client",
    "language": "Python", 
    "exceptions": "1. DO NOT make comments...\n2. DO NOT comment...",
    "outline": "## namespace azure.widget\n- WidgetClient\n...",
    "content": "{\"line_no\": 4, \"bad_code\": \"...\", \"comment\": \"...\"}",
    "response": "{\"action\": \"DISCARD\", \"rationale\": \"...\"}"
}
```

The fields should match the parameters expected by the target function in `_custom.py`.

`testcase` is the name of the test case and ideally says something about what's being tested.

`language` is the programming language being tested (e.g., "Python").

`exceptions` contains the list of exception rules that should not be violated when making comments.

`outline` provides the API structure outline that shows what elements exist in the codebase.

`content` is the JSON string containing the proposed comment to be evaluated (with fields like line_no, bad_code, comment, etc.).

`response` contains the expected JSON output with the action (KEEP/DISCARD) and rationale.

### Creating APIView Review Tests

To add a new APIView review test case, the following workflow is recommended:

1. Use the "Copy review text" button in the APIview UI to copy the text code.
2. Apply the desired guideline violations that you want to test to the code.
3. Run the CLI to generate an expected output: `python cli.py review generate --language <language> --path <path-apiview-text> --model <model-name>`
4. Once happy with the expected output, you can write the new test case by running the following command:

```bash
python cli.py eval create --language python --apiview-path path/to/apiview.txt --expected-path path/to/expected.json --test-file path/to/test.jsonl --name testcase_name
```

### Creating Workflow Tests

For workflow-based evaluations:

1. First create your test case data in the appropriate JSONL format for your workflow
2. Ensure your workflow YAML points to the correct test file and prompty
3. Create a target function for your workflow in `_custom.py`:

```python
def _your_workflow_name(param1: str, param2: str, ...):
    """Target function for your workflow."""
    prompty_path = Path(__file__).parent.parent / "prompts" / "your_folder" / "your_prompt.prompty"
    prompty_kwargs = {
        "param1": param1,
        "param2": param2,
        # Map all parameters from JSONL test data
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}
```

4. Register your function in the `workflow_targets` dictionary in the `PromptWorkflowEvaluator.target_function` property:

```python
workflow_targets = {
    "your-workflow-name": _your_workflow_name,
    # existing workflows...
}
```

5. Test the workflow using: `python run.py --test-file workflows/your-workflow.yaml`

**Pattern Notes:**
- Function name should start with `_`
- Parameters must match the fields in your JSONL test data
- Use `prompty.execute()` to run the prompt with mapped parameters

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
