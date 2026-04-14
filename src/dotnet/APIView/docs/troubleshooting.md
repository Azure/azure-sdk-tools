# APIView — Troubleshooting (User FAQ)

Common issues and solutions for APIView users. For engineering team troubleshooting, see [operations.md](operations.md#troubleshooting-engineering-team).

---

## Access Issues

### Why can't I access APIView?

> **Important:** Your membership in the Microsoft or Azure organization must be set to **PUBLIC** visibility. Private memberships cannot be verified by APIView.

The most common reason for access denial is that your GitHub organization membership is set to private. Change it to public:

- Microsoft: https://github.com/orgs/Microsoft/people
- Azure: https://github.com/orgs/Azure/people

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

### Why is APIView blocking my package release?

Release builds submit the package to APIView to compare against the last approved revision. If there is a **difference in API surface**, a new revision is created and the **pipeline fails**. If there is no difference, the approval status is returned and the pipeline succeeds.

**To resolve:**
1. Reach out to the language architect and get your revision approved
2. Queue the pipeline again
3. If the architect is not available, send email to **adparchrescue@microsoft.com** for emergency approval

### Manually created revision is approved. Do I still need approval for the automatic revision?

No. A GA package release will **not** be blocked as long as the package API surface matches **at least one** previously approved revision, regardless of revision type.

### Why is the prepare release script failing with key vault access permission error?

The prepare release script checks whether the API to be released is approved. This check requires secrets in Azure Key Vault. The script fails if you don't have reader permission.

By default, all members of the Azure SDK team and partners should have access. If you're getting permission errors, reach out to the engineering team.

---

## Get Help

For questions and support, use the **Azure SDK Engineering System Teams channel**.
