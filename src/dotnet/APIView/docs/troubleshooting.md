# APIView — FAQ

Common questions and answers for APIView users and the engineering team.

---

## Access

### Why can't I access APIView?

The most common reason is that your GitHub organization membership is set to **private**. APIView requires **PUBLIC** visibility. See [Getting Access](user-guide.md#getting-access) for the full requirements and links to change your membership visibility.

### I receive a 403 Forbidden Error page

To reduce the surface area for potential attacks, APIView is **only accessible from CorpNet**.

### APIView is not accessible (engineering team)

Possible causes: deployment in progress, Cosmos DB or Azure Storage Blob not accessible, or bad deployment.

1. Check the status of the `APIView` App Service in Azure Portal. If it's not running, check the [release pipeline](https://dev.azure.com/azure-sdk/internal/_release?_a=releases&view=mine&definitionId=73) for any in-progress deployments.
2. Check the `APIView` Application Insights resource for errors — Cosmos DB outages and startup exceptions will surface there.

---

## Uploads & Parsing

### Why is my manual revision failing?

The most common reason is the parser cannot generate a stub file. This is seen mostly for **Java** and **Python** when the uploaded package is not installable or not built according to parser expectations.

| Language | Fix |
|----------|-----|
| **Java** | Confirm you have uploaded the `sources.jar` |
| **Python** | Confirm the `.whl` is installable and properly built |

If the problem persists, check the [detailed instructions for manual uploads](https://eng.ms/docs/products/azure-developer-experience/support/apiview).

### Python sandboxing review not generating

If a Python wheel upload stays at "being generated" for more than 5 minutes:

1. Check the [Python sandboxing pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5102) for failures
2. Common causes: the uploaded wheel has import issues, or the DevOps pipeline queue is overloaded
3. Known limitation: pipeline failures are not reported back to the APIView UI

---

## CI & Revisions

### Why didn't my PR create an APIView revision?

Most common reasons:
- The PR did not change the API surface
- Only the version or changelog was updated

This is expected behavior — APIView only creates revisions when the API surface changes.

### My PR has no API changes, but the release is blocked

Possible cause:
- A **previous** API change already exists in `main`
- The latest automatic revision is still pending approval

The release correctly blocks — even if the most recent PR has no API changes.

---

## Releases & Approvals

### Why is APIView blocking my package release?

The release pipeline queries APIView to check whether the API surface has been approved. If the current API surface does not match any approved revision, the pipeline fails. See [ci-integration.md](ci-integration.md#release-enforcement-logic) for the full enforcement logic.

**To resolve:**
1. Reach out to the language architect and get your revision approved
2. Queue the pipeline again
3. If the architect is not available, send email to **adparchrescue@microsoft.com** for emergency approval

### Manually created revision is approved. Do I still need approval for the automatic revision?

No. APIView compares **API surfaces**, not revision types — a release will not be blocked as long as the API surface matches at least one previously approved revision. See [ci-integration.md](ci-integration.md#key-concept-api-surface-not-versions) for details.

### Can we override or disable the release check?

Yes, but **only after consulting the release owner and architect** for each language — an override can lead to release of breaking changes or unapproved APIs.

To disable:
1. In the pipeline run, click **Variables** under Advanced options
2. Click **Add variables**
3. Set **Name** to `Skip.CreateApiReview` and **Value** to `true`
4. Click **Create**, then click back

### Why is the prepare release script failing with key vault access permission error?

The prepare release script checks whether the API to be released is approved. This check requires secrets in Azure Key Vault. The script fails if you don't have reader permission.

By default, all members of the Azure SDK team and partners should have access. If you're getting permission errors, reach out to the engineering team.

### How to check release readiness

Use the **SDK Tools MCP server** and ask whether a package is ready for release. It checks:
- API approval status
- Namespace approval
- Provides relevant pipeline links

Example prompt:

> "Check if azure-storage-blob is ready for release."

This is the fastest and least error-prone way to diagnose release issues.

---

## Get Help

For additional troubleshooting guidance, see the [APIView Troubleshooting Guide](https://eng.ms/docs/products/azure-developer-experience/support/troubleshoot/apiview-troubleshoot).

For questions and support, use the **Azure SDK Engineering System Teams channel**.
