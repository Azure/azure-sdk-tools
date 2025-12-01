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

The main evaluation script is `run.py`. Here are the common ways to use it:

1. **Run all evaluations** (discovers all workflows automatically):
```bash
python run.py
```

2. **Run specific workflow**:
```bash
python run.py --test-paths tests/mention_action
```

3. **Run a single test file**:
```bash
python run.py --test-paths tests/filter_existing_comment/discard_azure_sdk_repeat_comment.yaml
```

4. **Change number of evaluation runs** (default is 1):
```bash
python run.py --num-runs 5 --test-paths tests/filter_comment_metadata
```

5. **Use recordings** to speed up runs without making LLM calls:
```bash
python run.py --use-recording
```

> Note: Due to variability in AI model responses, the number of runs can be increased to get a more stable result (the median of the results is chosen as the final result).

## Evaluation Recordings

The `--use-recording` flag records LLM responses to speed up repeated runs and reduce API costs.

### How to Use

```bash
# First run: makes LLM calls and saves responses to record
python run.py --use-recording --test-paths tests/mention_action

# Subsequent runs: uses recorded responses (no LLM calls)
python run.py --use-recording --test-paths tests/mention_action
```

### How It Works

- **Recordings location**: `evals/cache/` (automatically created, gitignored)
- **Fresh vs recorded**: If a test's recording exists, it's loaded; otherwise LLM is called
- **Modifying tests**: If you change a test file, delete its recording in `evals/cache/` to force a fresh LLM call on next run, or don't use `--use-recording` to always get fresh results.

### Benefits

- Speed up debugging iterations (no waiting for LLM calls)
- Reduce Azure OpenAI API costs during development
- Get consistent results for unchanged tests

## Workflow Structure

```
evals/
├── tests/                      # All test workflows
│   ├── <workflow_name>/        # Each workflow has its own directory
│   │   ├── test-config.yaml    # Workflow configuration (name, kind)
│   │   └── *.yaml               # Individual test case files
│   ├── mention_action/
│   ├── filter_comment_metadata/
│   ├── deduplicate_parser_issue/
│   └── ...
├── cache/                      # Recorded LLM responses (when using --use-recording)
├── run.py                      # Main entry point
└── _custom.py                  # Target functions for workflows
prompts/                        # Prompty files being tested (in parent dir)
```

### Test File Organization

- **`test-config.yaml`**: Defines the workflow name and kind. Kind will usually be `prompt` as `apiview` kind is deprecated.
- **Individual test files**: Each `.yaml` file contains one test case
- **Discovery**: The runner automatically finds all tests in workflow directories

Examples:
- `filter-comment-metadata` - Tests comment filtering based on exceptions and context
- `mention-action` - Tests conversation action parsing  
- `thread-resolution-action` - Tests thread resolution actions


## Create New Evals

### Test File Format

Each test case is stored in its own YAML file. The runner automatically discovers and loads all test files in a workflow directory.

**Example test file** (`tests/mention_action/update_kb_no_guideline.yaml`):
```yaml
testcase: mention_action_update_kb_no_guideline
language: python
package_name: azure.widgets
code: "class WidgetObject:"
other_comments: |
  [
    {
      "CreatedBy": "azure-sdk",
      "CommentText": "This name is unnecessarily verbose.\n\nSuggestion: `Widget`.",
      "Downvotes": ["tjprescott"],
      "CreatedOn": "2025-03-17T17:48:25.920445-04:00"
    },
    {
      "CreatedBy": "noodle",
      "CommentText": "We discussed it internally and want to keep it as is because we used that name in the JS SDK and we want to keep them the same.",
      "CreatedOn": "2025-03-18T13:15:19.1494832-04:00"
    },
    {
      "CreatedBy": "tjprescott",
      "CommentText": "@noodle, sorry, that's not a valid reason. If you wanted the names to be consistent you should have had them reviewed at the same time. The suffix `Object` adds no useful information and just results in a longer name.",
      "CreatedOn": "2025-03-19T17:48:25.920445-04:00"
    }
  ]
trigger_comment: |
  {
    "CreatedBy": "tjprescott",
    "CommentText": "@azure-sdk, your comment is correct, but your suggestion was bad because it was actually more verbose! The name should be `Widget`.",
    "CreatedOn": "2025-03-19T17:48:25.920445-04:00"
  }
response:
  action: update_kb
  reasoning: |
    The architect indicates that while the copilot's comment about verbosity was correct, the suggestion itself was flawed and more verbose than needed.
    This suggests the copilot's suggestion logic needs improvement and should be recorded in the Knowledge Base.
```

**Required fields:**
- `testcase`: Unique identifier for the test (used for recording and reporting)
- `response`: Expected output (can be YAML object or JSON string)
- Other fields: Must match parameters in your target function in `_custom.py`

The `testcase` field is used for:
- Identifying tests in output
- Caching results (when using `--use-recording`)
- Organizing test results

**Another example** (`tests/filter_comment_metadata/filter_comment_metadata.yaml`):

```yaml
testcase: filter_missing_async_client
language: Python
exceptions: |
  1. DO NOT make comments that don't actually identify a problem
  2. DO NOT comment on the `send_request` method
  ...
outline: |
  ## namespace azure.widget
  - WidgetClient
    - get
    - create
    - update
    - delete
    - list

  ## namespace azure.widget.aio
  - WidgetClient
    - get
    - create
    - update
    - delete
    - list
content: |
  {
    "line_no": 4,
    "bad_code": "class azure.widget.WidgetClient():",
    "suggestion": "",
    "comment": "You must have an async client named `WidgetClient` in the azure.widget.aio namespace.",
    "source": "guideline"
  }
response:
  action: DISCARD
  rationale: The comment asserts that an async client named 'WidgetClient' is missing in the azure.widget.aio namespace, but the outline clearly shows that WidgetClient exists in that namespace.
```

All fields must match the parameters expected by the target function in `_custom.py`.

### Creating Tests

1. **Create a workflow directory** in `evals/tests/`:
```bash
mkdir evals/tests/your_workflow_name
```

2. **Create `test-config.yaml`**:
```yaml
name: your-workflow-name 
kind: prompt
```

3. **Create test case files** (one YAML file per test case):
```yaml
testcase: descriptive_test_name
param1: value1
param2: value2
response: '{"expected": "output"}'
```

4. **Create a target function** in `evals/_custom.py`:

```python
def _your_workflow_name(testcase: str, param1: str, param2: str, response: str):
    """Target function for your-workflow-name."""
    prompty_path = Path(__file__).parent.parent / "prompts" / "your_folder" / "your_prompt.prompty"
    prompty_kwargs = {
        "param1": param1,
        "param2": param2,
        # Map parameters to prompty (exclude testcase and response as they're for evaluation framework)
    }
    result = prompty.execute(prompty_path, inputs=prompty_kwargs)
    return {"actual": result}
```

5. **Add your target function** to the registry in `evals/_custom.py` under an evaluator's `target_function` callable.
```python
# Under PromptyEvaluator, PromptySummaryEvaluator, or similar
@property
def target_function(self) -> callable:
    workflow_targets = {
        "mention_action": _mention_action_workflow,
        "thread_resolution_action": _thread_resolution_action_workflow,
        "filter_comment_metadata": _filter_comment_metadata,
        "filter_existing_comment": _filter_existing_comment,
        "deduplicate_parser_issue": _deduplicate_parser_issue,
        "deduplicate_guidelines_issue": _deduplicate_guidelines_issue,
        # Add more workflows as needed
    }
```

**Important:** All test file fields must match the target function parameters exactly.

5. **Run your workflow**:
```bash
python run.py --test-paths tests/your_workflow_name
```

**Pattern Notes:**
- Function name starts with `_` and uses underscores
- Parameters must match test file fields exactly
- Use `prompty.execute()` to run the prompt with mapped parameters

## Editing Test Cases

To edit a test case, simply open the YAML file directly in your editor and make changes:

```bash
# Open and edit the test file
code evals/tests/mention_action/update_kb_no_guideline.yaml

# Run the test to verify your changes
python run.py --test-paths tests/mention_action/update_kb_no_guideline.yaml
```

The YAML format makes it easy to update test inputs, expected responses, and other parameters without using additional tools.
Remember, if you change fields in the test file, they must be consistent with the target function parameters in `_custom.py`.

## Results and Baselines

- Test results are displayed in the console after each run
- Results include overall accuracy score and per-test results with visual indicators (✅/❌)

## Evaluation Metrics

Evaluations measure the correctness and quality of prompt outputs:

### For Action-Based Workflows
(e.g., `mention_action`, `thread_resolution_action`, `filter_comment_metadata`)

- **Action Correctness**: Whether the model selected the correct action (e.g., `update_kb`, `DISCARD`, `KEEP`)
- **Rationale Similarity**: Semantic similarity score (0-100%) between the expected and actual reasoning, using Azure AI's `SimilarityEvaluator`
- **Final Score**: 
  - If action is correct: similarity score (0-100%)
  - If action is incorrect: 0%

### For Summarization Workflows
(e.g., `mention_summarize`)

- **Similarity Score**: Semantic similarity between expected and actual summaries (0-100%)
- **Success Threshold**: Scores above 70% are considered successful

### Overall Scoring

For each workflow run:
- Each test case is scored individually (0-100%)
- Overall accuracy is the average of all test case scores
- When multiple runs are performed (`--num-runs > 1`), the **median** run by accuracy is selected as the final result
