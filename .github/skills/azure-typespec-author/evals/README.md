# Authoring Routing Evals

This hermetic suite checks authoring boundaries, including delegation of naming-only `client.tsp` requests to `azsdk-common-typespec-naming`. It mounts skills only and is independent of the [live TypeSpec benchmarks](../evaluate/README.md).

Naming-rule and customization-tool cases live with the shared naming skill, not as copies in the authoring skill. Authoring/assessment handoffs are tested by `evals/workflows/mock/typespec-naming-handoffs.eval.yaml` at the repository root.

From `.github/skills`, using the repository's Vally 0.14 installation:

```powershell
vally lint azure-typespec-author --strict
vally lint -e azure-typespec-author\evals\eval.yaml --strict
vally eval -e azure-typespec-author\evals\eval.yaml --workers 1 --output jsonl --output-dir ..\..\artifacts\naming-evals
```

The eval explicitly mounts relevant skills; do not add `--skill-dir` pointing at all repository skills. Run serially if the local Copilot executor reports `Cannot set session filesystem provider while sessions are active`. The shared skill-eval pipeline discovers this suite.
