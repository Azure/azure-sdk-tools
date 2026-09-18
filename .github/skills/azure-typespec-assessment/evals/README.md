# Azure TypeSpec assessment routing eval

`assessment.eval.yaml` contains four skill-invocation checks:

- two prompts that must invoke `azure-typespec-assessment`;
- one authoring prompt that must invoke `azure-typespec-author`;
- one SDK generation prompt that must invoke
  `azsdk-common-generate-sdk-locally`.

The eval intentionally contains no assessment replay data, benchmark fixtures,
or custom scripts.

From `.github\skills`, run:

```powershell
vally lint azure-typespec-assessment --strict
vally eval -e azure-typespec-assessment\evals\assessment.eval.yaml `
  --skill-dir . --workers 1 --max-retries 0 `
  --output jsonl --output-dir <output-directory>
```
