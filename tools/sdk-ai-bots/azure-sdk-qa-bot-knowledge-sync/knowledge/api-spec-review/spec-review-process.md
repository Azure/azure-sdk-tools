# Spec Review Process

Common issues and solutions for API spec review process

## PR bot adds WaitForARMFeedback label even when CI checks fail

This is intentional behavior — the label is added when the PR enters the ARM review queue regardless of CI status. Open a draft PR first and only mark it "ready for review" after all required checks pass, to avoid wasting reviewer time. Reviewers who see failing checks will manually change the label to `ARMChangesRequested`.

Note: Automatic label switching for failed checks is on the backlog and will be implemented once the labeling system migration to GitHub Actions is complete.

## PR pipelines not starting or waiting for a long time

If required pipeline checks (e.g., `SDK Validation Status`, `Swagger PrettierCheck`) are stuck at "Waiting for status to be reported" and never start, this is almost always a **permissions issue** with the PR author's Azure GitHub organization membership.

To fix this, the PR author needs to:

1. **Make their Azure org membership public** — go to https://github.com/orgs/Azure/people, find your account, and change visibility from "Private" to "Public".
2. **Join the Azure SDK Partners group** — request access at https://aka.ms/azsdk/access.

Once both steps are complete, re-run or create a new PR and the pipelines should trigger normally. This affects both `azure-rest-api-specs` and `azure-rest-api-specs-pr` repos.