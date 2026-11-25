"""Provider-neutral deterministic issue status helpers."""

from __future__ import annotations

from models.feedback import AzureDevOpsIssueReference, parse_issue_reference
from tools.ado_mcp_tools import get_ado_work_item_state, reopen_ado_work_item
from tools.github_mcp_tools import get_github_issue_state, reopen_github_issue


async def get_issue_state(issue_url: str) -> str:
    """Return ``open`` or ``closed`` for a supported issue URL."""
    reference = parse_issue_reference(issue_url)
    if isinstance(reference, AzureDevOpsIssueReference):
        return await get_ado_work_item_state(issue_url)
    return await get_github_issue_state(issue_url)


async def reopen_issue(issue_url: str) -> None:
    """Reopen a supported GitHub issue or Azure Boards work item."""
    reference = parse_issue_reference(issue_url)
    if isinstance(reference, AzureDevOpsIssueReference):
        await reopen_ado_work_item(issue_url)
        return
    await reopen_github_issue(issue_url)
