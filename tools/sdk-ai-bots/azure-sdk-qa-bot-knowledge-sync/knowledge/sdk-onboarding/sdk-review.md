# SDK Review

Common issues and solutions for SDK review processes and requirements.

## Data plane API specs require API Stewardship Board review

**Scenario**: You have a data plane TypeSpec PR in the azure-rest-api-specs repository and want to know if you need review from the TypeSpec team before merging.

**Requirement**: All data plane API specs must be reviewed by the **API Stewardship Board**. You do not need a dedicated "TypeSpec team review" as a prerequisite to merge, but you do need an API Stewardship review.

**Process**:

1. **Create a release plan**: Before you can schedule a review, you must create a release plan.

**What is a Release Plan?**

A release plan is a guided workflow that tracks an upcoming REST API and SDK release. It's created in the Release Planner tool and connects to Service Tree to get details about your service.

The release plan consists of milestones and tasks:
- **API Readiness milestone**: Includes tasks like scheduling reviews, fixing validation issues, and obtaining sign-offs
- **SDK Release milestone**: Covers SDK generation, testing, release, and approvals

2. **Schedule the API Stewardship Board review**: Once your release plan is created and your PR has all checks passing, schedule the review.

3. **TypeSpec Discussion for Help**: If you need help authoring/fixing TypeSpec (e.g., failing TypeSpecValidation/tsv, emitter issues, modeling questions), you can ask in the TypeSpec Discussions channel or file an issue in the typespec-azure repo. This is for assistance, not a mandatory merge gate.

**What You Don't Need**:
- ❌ Dedicated TypeSpec team review as a merge prerequisite
- ❌ TypeSpec team approval to merge

**What You Do Need**:
- ✅ API Stewardship Board review (scheduled via release plan)
- ✅ All required CI checks passing
- ✅ At least one approval from someone with write access
- ✅ "Automated merging requirements met" check is green

**How to Create a Release Plan**:

A release plan is created in Release Planner and provides:
- A guided workflow specific to your service scenario
- Automated checks for when to schedule reviews, fix validation issues, request sign-offs
- Integration with your spec PR to track progress
- Guidance on SDK generation, testing, and release

The major milestones are:
1. **API Readiness**: REST API spec review, validation, and CPEX sign-off
2. **SDK Release**: SDK generation, testing, release, and approval

**Key Points**:
- Data plane specs require API Stewardship Board review, not TypeSpec team review
- Create a release plan first to track the review process
- TypeSpec Discussion/issues are for technical help, not formal review
- Follow the "Next Steps to Merge" guidance in your PR
- Ensure CI is passing before requesting review

