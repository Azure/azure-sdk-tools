# SDK Review

Common issues and solutions for SDK review processes and requirements.

## Data plane API specs require API Stewardship Board review

All data plane API specs must be reviewed by the **API Stewardship Board** before merging. A dedicated TypeSpec team review is not required.

**Process**: Create a release plan (via Release Planner) first, then schedule the API Stewardship Board review once all CI checks pass. The TypeSpec Discussion channel is for technical help only, not a formal review gate.

To merge you need: all required CI checks passing, at least one approval from someone with write access, and the "Automated merging requirements met" check green.

