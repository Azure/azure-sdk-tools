# Customization Classifier Skill Tests

Automated test suite for the customization classifier skill. Uses real Claude API calls (no mocking).

## Prerequisites

```bash
pip install anthropic pytest azure-identity python-dotenv
```

## Environment Setup

Create a `.env` file at the repo root (`azure-sdk-tools/.env`):

```bash
# Azure AI Services endpoint (uses Azure CLI / Entra ID auth)
ANTHROPIC_ENDPOINT="https://your-ai-services.services.ai.azure.com/anthropic/"
ANTHROPIC_MODEL="claude-sonnet-4-5"

# Optional: API key (if not using identity auth)
# ANTHROPIC_API_KEY="your-key-here"
```

**Authentication options:**
1. **Identity auth (recommended)**: Set `ANTHROPIC_ENDPOINT` only. Uses Azure CLI credentials.
2. **API key auth**: Set both `ANTHROPIC_ENDPOINT` and `ANTHROPIC_API_KEY`.
3. **Direct Anthropic**: Set `ANTHROPIC_API_KEY` only (no endpoint).

Ensure you're logged in with Azure CLI:
```bash
az login
```

## Validation Approach

The test suite validates two aspects:

### 1. Classification Correctness (Primary)
Deterministic check that the classifier returns the expected classification (PHASE_A, SUCCESS, or FAILURE).

### 2. Reasoning Quality (Secondary)
For PHASE_A responses, validates that reasoning references specific TypeSpec decorators (`@clientName`, `@access`, `@client`, etc.) from `customizing-client-tsp.md`. This ensures the classifier is grounding its recommendations in the reference documentation.

## Running Tests

### Quick run (direct execution)
```bash
cd .github/skills/customization-classifier/tests
python test_classifier.py
```

### With pytest (CI-friendly)
```bash
pytest test_classifier.py -v
```

### Run specific test
```bash
pytest test_classifier.py -v -k "basic_rename"
```

## Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| basic_rename_phase_a | Rename request | PHASE_A |
| build_error_type_not_found | Missing type error | PHASE_A |
| hide_operation_phase_a | Hide operation | PHASE_A |
| client_restructure_phase_a | Client restructuring | PHASE_A |
| no_typespec_solution_polling | Polling customization | FAILURE |
| stall_detection_same_error_twice | Same error twice | FAILURE |
| max_iterations_exceeded | Iteration > 4 | FAILURE |
| build_success | Build passed | SUCCESS |
| missing_customization_files_python | No _patch.py | FAILURE |
| missing_customization_files_java | No Customization.java | FAILURE |
| analyzer_error_naming_violation | .NET analyzer error | PHASE_A |
| complex_convenience_method | Complex request | FAILURE |

## Adding New Test Cases

Edit `test_cases.json`:

```json
{
  "id": "my_new_test",
  "description": "Description of what this tests",
  "input": {
    "service": "ServiceName",
    "language": "python",
    "request": "The customization request or error message"
  },
  "expected": {
    "classification": "PHASE_A"
  }
}
```

For PHASE_A tests, the reasoning is automatically validated for grounding in the reference document (decorator references).

## CI Integration

Add to GitHub Actions workflow:

```yaml
- name: Test Customization Classifier Skill
  env:
    ANTHROPIC_ENDPOINT: ${{ secrets.ANTHROPIC_ENDPOINT }}
    ANTHROPIC_MODEL: claude-sonnet-4-5
    # For identity auth in CI, use workload identity federation
    # Or set ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
  run: |
    pip install anthropic pytest azure-identity python-dotenv
    pytest .github/skills/customization-classifier/tests/test_classifier.py -v
```

## Model Configuration

Default model from `.env`: `ANTHROPIC_MODEL`

To override in code:
```python
runner = SkillTestRunner(model="claude-opus-4-5")
```
