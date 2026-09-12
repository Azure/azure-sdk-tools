# Azure TypeSpec Assessment

Use `azure-typespec-assessment` with your coding agent to review TypeSpec
changes and generate an assessment report without modifying your source.
Make sure the skill is available to your agent.

## 1. Assess local code

Open your TypeSpec repository and ask:

```text
Use azure-typespec-assessment to assess the current TypeSpec changes in the Microsoft.ServiceNetworking/ServiceNetworking specification.
```

Replace the specification name with the one you are working on. Local assessment
is the primary experience; you do not need to open a PR first.

If you omit the baseline, the agent asks you to confirm `origin/main` or provide
another ref or commit ID before starting. You can specify a commit ID directly
to skip the question:

```text
Use azure-typespec-assessment to assess the current TypeSpec changes in the Microsoft.ServiceNetworking/ServiceNetworking specification against commit <commit-id>.
```

## 2. Assess a PR

Provide the PR URL:

```text
Use azure-typespec-assessment to assess the TypeSpec changes for PR https://github.com/Azure/azure-rest-api-specs/pull/44988.
```

## Results

Open the generated `assessment.html` to view the findings. Structured results
are available in `assessment.json`.

[Browse reports for 12 existing PRs](https://wonderful-coast-0b5cc5a00.3.azurestaticapps.net)
or read the [high-level design spec](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/docs/specs/typespec-assessment.spec.md)
and [detailed design](design.md).
