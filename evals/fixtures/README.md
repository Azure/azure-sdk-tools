# Scenario fixtures

Scenario fixtures live here, one folder per scenario name (matching the `name:`
field in the corresponding `evals/*.eval.yaml`).

## Referencing fixtures from an eval

Reference them from the eval via an `environment.files` block:

```yaml
environment:
  files:
    - src: ../../fixtures/<scenario-name>/<file>      # evals/tools/*.eval.yaml (2 levels up)
      dest: <path inside the workspace>
    - src: ../../../fixtures/<scenario-name>/<file>   # evals/workflow-scenarios/{mock,live}/* (3 levels up)
      dest: <path inside the workspace>
```

`src` paths resolve relative to the eval file, so adjust the number of `../`
segments if the eval moves or is nested deeper.
