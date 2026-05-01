"""Generate report-issue preview to scratch/report_issue_preview.md"""
import os
import sys

os.environ["ENVIRONMENT_NAME"] = "staging"
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app import _generate_report_issue, _get_report_issue_labels, ReportIssueRequest, CommentContextRequest

# Scenario 1: General Issue
req1 = ReportIssueRequest(
    mode="general",
    description=(
        "When I navigate to the diff view for a review with more than 500 API definitions, "
        "the page freezes for 10+ seconds and shows a blank white screen. This happens in "
        "Chrome and Edge. The review is azure-storage-blob 12.20.0 vs 12.19.0. Other "
        "reviewers on my team also experience this."
    ),
    reviewLink="https://apiview.dev/Assemblies/Review/azure-storage-blob-12.20.0",
)
r1 = _generate_report_issue(req1)
l1 = _get_report_issue_labels(req1)

# Scenario 2: Comment Issue (AVC)
ctx = CommentContextRequest(
    commentSource="copilot",
    commentText="Consider making this method synchronous since it does not perform any I/O operations.",
    language="python",
    codeSnippet="async def list_blobs(self, container: str, prefix: str = None) -> AsyncIterator[BlobProperties]:",
    elementId="BlobServiceClient.list_blobs",
)
req2 = ReportIssueRequest(
    mode="comment",
    description=(
        "This AVC suggestion is incorrect. The list_blobs method DOES perform I/O - "
        "it makes HTTP calls to Azure Storage to list blobs. Making it synchronous would "
        "block the event loop and break all async callers."
    ),
    commentContext=ctx,
    reviewLink="https://apiview.dev/Assemblies/Review/azure-storage-blob-12.20.0",
)
r2 = _generate_report_issue(req2)
l2 = _get_report_issue_labels(req2)

# Write markdown preview
with open("scratch/report_issue_preview.md", "w", encoding="utf-8") as f:
    for name, r, labels in [
        ("SCENARIO 1: General Issue", r1, l1),
        ("SCENARIO 2: Comment Issue (AVC)", r2, l2),
    ]:
        f.write(f"# {name}\n\n")
        f.write(f"**Title:** {r['title']}\n\n")
        f.write(f"**Labels:** {labels}\n\n")
        f.write("---\n\n")
        f.write(r["body"])
        f.write("\n\n---\n\n")

print("Done: scratch/report_issue_preview.md")
