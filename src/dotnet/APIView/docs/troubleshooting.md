# APIView — Troubleshooting (User FAQ)

Common issues and solutions for APIView users. For engineering team troubleshooting, see [operations.md](operations.md#troubleshooting-engineering-team).

---

## Access Issues

### Why can't I access APIView?

The most common reason is that your GitHub organization membership is set to **private**. APIView requires **PUBLIC** visibility. See [Getting Access](user-guide.md#getting-access) for the full requirements and links to change your membership visibility.

### I receive a 403 Forbidden Error page

To reduce the surface area for potential attacks, APIView is **only accessible from CorpNet**.

---

## Reviews and Revisions

### I don't see my manually created reviews from older APIView

As of December 7th, APIView shows only **one review per package**. All previously separate reviews are now added as revisions within a single review. To find them:

1. Click on the review link for your package
2. Select **Manual** in the Revision Type dropdown
3. All previously created manual API revisions will appear

### Why do I see so many comments that were not present earlier?

As of December 7th, APIView shows **all comments ever created across multiple revisions** for a package in a single review.

### My pull request has links to previously generated API revisions. Are those links still valid?

Yes. Previously generated links are still valid — APIView automatically redirects to the newly created review for the package.

---

## Upload Failures

### Why is my manual revision failing?

The most common reason is the parser cannot generate a stub file. This is seen mostly for **Java** and **Python** when the uploaded package is not installable or not built according to parser expectations.

| Language | Fix |
|----------|-----|
| **Java** | Confirm you have uploaded the `sources.jar` |
| **Python** | Confirm the `.whl` is installable and properly built |

If the problem persists, check the [detailed instructions for manual uploads](https://eng.ms/docs/products/azure-developer-experience/support/apiview).

---

## Release Blocking

> For details on when and why releases are blocked, see [ci-integration.md](ci-integration.md#release-enforcement-logic) and [release_approval.md](release_approval.md#6-release-gating-cicd-integration).

### Why is APIView blocking my package release?

The release pipeline queries APIView to check whether the API surface has been approved. If the current API surface does not match any approved revision, the pipeline fails. See [ci-integration.md](ci-integration.md#release-enforcement-logic) for the full enforcement logic.

**To resolve:**
1. Reach out to the language architect and get your revision approved
2. Queue the pipeline again
3. If the architect is not available, send email to **adparchrescue@microsoft.com** for emergency approval

### Manually created revision is approved. Do I still need approval for the automatic revision?

No. APIView compares **API surfaces**, not revision types — a release will not be blocked as long as the API surface matches at least one previously approved revision. See [ci-integration.md](ci-integration.md#key-concept-api-surface-not-versions) for details.

### Why is the prepare release script failing with key vault access permission error?

The prepare release script checks whether the API to be released is approved. This check requires secrets in Azure Key Vault. The script fails if you don't have reader permission.

By default, all members of the Azure SDK team and partners should have access. If you're getting permission errors, reach out to the engineering team.

---

## Get Help

For questions and support, use the **Azure SDK Engineering System Teams channel**.
