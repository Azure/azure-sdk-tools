"""Verify earlier spec PR gates before giving SDK breaking-change advice."""

import asyncio
import logging
import re

from models.chat import AdditionalInfoType, ChatRequest
from tools.github_mcp_tools import get_spec_pull_request_labels

logger = logging.getLogger(__name__)

_SPEC_PR = re.compile(
    r"""https://github\.com/azure/(azure-rest-api-specs(?:-pr)?)/pull/"""
    r"""([1-9][0-9]{0,9})(?=$|[/#?\s<>)\]"'.,])""",
    re.IGNORECASE,
)
_SDK_LABEL = re.compile(r"\bbreakingchange-[a-z0-9]+-sdk\b", re.IGNORECASE)
_SDK = re.compile(r"\bsdks?\b", re.IGNORECASE)
_BREAKING_CHANGE = re.compile(r"\bbreaking[\s-]+changes?\b", re.IGNORECASE)
_ARM = re.compile(r"\b(?:arm|resource[\s-]+manager|management[\s-]+plane)\b", re.IGNORECASE)
_SPEC_GATES = (
    ("breakingchangereviewrequired", "breakingchange-approved-", "BreakingChangeReviewRequired"),
    ("versioningreviewrequired", "versioning-approved-", "VersioningReviewRequired"),
)
_ARM_LABELS = {
    "resource-manager", "armreview", "armsignedoff", "waitforarmfeedback",
    "armchangesrequested", "notreadyforarmreview", "armmodelingreviewrequired",
}


async def sdk_breaking_change_review_hold(req: ChatRequest) -> str | None:
    """Return a direct hold response, or let the agent answer as usual."""
    text = req.message.content
    has_sdk_label = bool(_SDK_LABEL.search(text))
    if not (has_sdk_label or (_SDK.search(text) and _BREAKING_CHANGE.search(text))):
        return None

    links = [text]
    for info in req.additional_infos or []:
        if info.type == AdditionalInfoType.Link:
            links.extend((info.content, info.link or ""))
    prs = {(repo.lower(), int(number)) for repo, number in _SPEC_PR.findall("\n".join(links))}
    if not prs:
        return None
    if len(prs) > 1:
        return "Which Azure spec PR should I check before advising on SDK breaking-change approval?"

    repo, number = next(iter(prs))
    pr = f"[spec PR #{number}](https://github.com/Azure/{repo}/pull/{number})"
    try:
        labels = await asyncio.wait_for(get_spec_pull_request_labels(repo, number), timeout=10)
    except Exception as exc:
        logger.warning("Could not verify spec PR review labels (%s)", type(exc).__name__)
        return (
            f"I couldn't verify the current review gates for {pr}. Check **Next Steps to Merge** "
            "on the PR and confirm the applicable REST API breaking-change review and, for ARM "
            "specs, ARM signoff before acting on the SDK breaking-change label."
        )

    is_arm = bool(labels & _ARM_LABELS or any(_SDK_LABEL.search(label) for label in labels))
    if not is_arm and "data-plane" in labels:
        return None
    is_arm = is_arm or bool(_ARM.search(text)) or has_sdk_label

    pending_spec_gates = [
        f"`{display}`" for required, approval, display in _SPEC_GATES
        if required in labels and not any(label.startswith(approval) for label in labels)
    ]
    premature = f"SDK breaking-change approval is premature for {pr}."
    if pending_spec_gates:
        first = (
            f"{premature} Complete the [REST API breaking-change or versioning review]"
            f"(https://aka.ms/brch) first ({', '.join(pending_spec_gates)})."
        )
        if is_arm:
            first += " Then confirm ARM signoff (`ARMSignedOff`) before requesting SDK review."
        return first
    if "notreadyforarmreview" in labels or "armmodelingreviewrequired" in labels:
        return (
            f"{premature} Resolve the ARM review blockers listed in **Next Steps to Merge**, "
            "then obtain ARM signoff (`ARMSignedOff`)."
        )
    if "armsignedoff" in labels:
        return None
    if is_arm:
        return (
            f"{premature} ARM signoff (`ARMSignedOff`) is not present. "
            "Complete ARM review and obtain signoff before requesting SDK review. "
            "The SDK label alone does not establish whether the SDK change is accepted."
        )
    return (
        f"I couldn't verify whether ARM review is required for {pr}. Check **Next Steps to Merge** "
        "and confirm the applicable REST review and ARM signoff before requesting SDK review."
    )
