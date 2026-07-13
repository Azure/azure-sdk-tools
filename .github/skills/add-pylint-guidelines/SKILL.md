---
name: add-pylint-guidelines
description: Create, update, or test custom rules in `azure-pylint-guidelines-checker`. Use when working on implementing/testing the pylint checker.
---

## Adding Pylint Guidelines

### Prerequisites
1. Activate a virtual environment before installing dependencies
2. Install the dev requirements at `tools/pylint-extensions/azure-pylint-guidelines-checker/dev_requirements.txt`

### Implementing Guidelines

1. Understand the existing pylint plugin structure and how guidelines are implemented. Review the current implementation in `pylint_guidelines_checker.py` to see how existing rules are defined.
2. Define the new guideline you want to add. Clearly specify what coding practice you want to enforce and under what conditions it should be flagged.
3. Write a test file in the `tests` directory to cover the new guideline, covering edge cases and using TDD principles. Every guideline should have corresponding tests that verify both that violations are correctly flagged and that compliant code does not trigger false positives.
4. Implement the new guideline in the `pylint_guidelines_checker.py` file following existing patterns:
   - Add a new method to check for the specific coding pattern.
   - Register the new message ID and description for the guideline.
   - Using the appropriate AST node classes to analyze the code structure.
   - Update the rule number range in the comment at the top of the file.
5. Add a test case in `test_pylint_custom_plugins.py` to verify that the new guideline is correctly identified and that valid code does not trigger false positives.
6. If the rule is potentially ambiguous (e.g., it has edge cases, nuanced conditions, or may be unclear to users about what constitutes a violation), add an entry to `tools/pylint-extensions/azure-pylint-guidelines-checker/code_examples.md` with:
   - A section header matching the rule name (e.g., `## <rule-name>`).
   - A brief explanation of what the rule enforces and when it fires.
   - A "violation" code snippet showing code that triggers the rule.
   - A "compliant" code snippet showing the correct alternative.
7. Update `tools/pylint-extensions/azure-pylint-guidelines-checker/README.md` to document the new rule, including its rule ID, a short description, and any relevant usage notes.
8. Update the changelog to document newly added guidelines

### Testing
1. Run the tests using `pytest` to ensure your new guideline is correctly implemented.
```
cd tools/pylint-extensions/azure-pylint-guidelines-checker
<ensure a venv is activated>
pip install -r dev_requirements.txt
python -m pytest tests/test_pylint_custom_plugins.py::<Test_Name> -v
```
2. Run pylint directly on the test files to see the new guideline in action and verify that it flags the intended issues without false positives.
```
python -m pylint --load-plugins=pylint_guidelines_checker --disable=all --enable=<guideline-name> tests/test_files/<test_file>.py
```