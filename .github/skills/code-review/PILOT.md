# azsdk-cli Copilot Code Review Pilot

This pilot automatically requests a non-blocking GitHub Copilot review when a
pull request changes `tools/azsdk-cli/**`. Copilot submits a comment review; it
does not approve the pull request, request changes, satisfy required approvals,
or block merging.

The workflow requests one review when an eligible pull request is opened,
reopened, or moved out of draft. It does not request another review for every
push during the pilot.

## Classify Review Results

Maintainers classify each Copilot finding using its review comment:

1. React with `:+1:` when the finding is correct, useful, and supported by the
   changed code.
2. React with `:-1:` when the finding is incorrect or unsupported.
3. Reply with `duplicate: <check>` when an analyzer, compiler, linter, or CI
   check already owns the finding.
4. Record a missed defect in the pilot tracking issue with the pull request,
   changed line, triggering scenario, and expected finding.
5. Record a routing failure when the `code-review` skill is attributed to an
   unrelated change or is not attributed to an azsdk-cli review where its rules
   were relevant.

Comments and replies are evidence for maintainers; Copilot does not process
replies to its review comments.
