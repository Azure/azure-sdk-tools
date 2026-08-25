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

## Weekly Triage

After each group of reviewed pull requests:

1. Count confirmed findings, false positives, duplicates, missed defects, and
   routing failures.
2. Sanitize confirmed misses and false positives into Vally fixtures without
   source code, credentials, customer data, or private repository context.
3. Run each new fixture at least three times to detect unstable behavior.
4. Update the skill only when the collected evidence demonstrates a recurring
   review problem.

Use these pilot measurements:

| Measurement | Calculation |
| --- | --- |
| Review precision | Confirmed findings / all non-duplicate findings |
| Duplicate rate | Duplicate findings / all findings |
| Missed-defect count | Confirmed defects absent from the Copilot review |
| Routing accuracy | Correct skill-routing decisions / reviewed pull requests |
| Stability | Runs producing the expected result / repeated fixture runs |

Pilot findings must not become merge-blocking checks. Reconsider enforcement
only after the skill demonstrates high precision, stable repeated results, and
low duplication across representative azsdk-cli pull requests.
