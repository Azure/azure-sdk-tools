# Spec Review Process

Common issues and solutions for API spec review process

## PR bot adds WaitForARMFeedback label even when CI checks fail

This is intentional behavior — the label is added when the PR enters the ARM review queue regardless of CI status. Open a draft PR first and only mark it "ready for review" after all required checks pass, to avoid wasting reviewer time. Reviewers who see failing checks will manually change the label to `ARMChangesRequested`.

Note: Automatic label switching for failed checks is on the backlog and will be implemented once the labeling system migration to GitHub Actions is complete.