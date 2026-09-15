# Azure SDK Copilot Code Review Pilots

## azsdk-cli

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

## Release CSV package names (Azure/azure-sdk)

The naming pilot applies only to new packages or changed `ServiceName` and
`DisplayName` values in `_data/releases/latest/*-packages.csv`. Routine version
bumps, row moves, and pre-existing unknown names receive no naming comments.
Use the [package naming review guidance](references/package-names.md) to suggest
word spacing and acronym/product casing, or request owner confirmation when
the name is unknown. Suggestions are not authoritative or auto-applied.

This skill is repository-local: editing it in azure-sdk-tools does not install
it in Azure/azure-sdk. The companion website change supplies path-specific
`.github/instructions/release-csv-names.instructions.md` guidance and narrows
that repository's blanket prohibition on package-index review comments. Keep
those instructions aligned with this skill's naming guidance. The website's
existing automatic Copilot review rule requests reviews; this pilot adds no
second review-request workflow and does not change the azsdk-cli workflow.

Before rollout, verify with repository maintainers that custom instructions
are enabled, the naming guidance is attributed in an actual generated CSV PR,
and automated merges cannot bypass existing required human/code-owner reviews.
If an open package-index PR is updated with new naming changes after its first
review, request a re-review manually: the existing rule does not review every
push. Do not disable human approvals or auto-apply Copilot's suggestions.

Pilot validation should cover a new unknown row, a supported spacing/acronym
correction, an ambiguous name that needs owner input, and a version-only update
with no naming feedback. Classify useful/incorrect suggestions and missed
names using the process above. Check that the following discovery/sync run
preserves approved CSV names and does not create replacement incorrect Epics.
