# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------


"""
Module for handling @mention requests in APIView Copilot.
Refactored to use modular workflow registry and base class pattern.
"""

import json

import prompty
from src._utils import get_prompt_path
from src.mention.open_parser_issue_workflow import OpenParserIssueWorkflow
from src.mention.update_kb_workflow import UpdateKnowledgeBaseWorkflow

# Add any new workflows here
WORKFLOW_REGISTRY = {
    "update_kb": UpdateKnowledgeBaseWorkflow,
    "open_parser_issue": OpenParserIssueWorkflow,
}


def _parse_conversation_action(
    *, language: str, code: str, package_name: str, trigger_comment: str, other_comments: list[str]
):
    prompty_file = "parse_conversation_action.prompty"
    prompt_path = get_prompt_path(folder="mention", filename=prompty_file)
    inputs = {
        "language": language,
        "code": code,
        "package_name": package_name,
        "trigger_comment": trigger_comment,
        "other_comments": other_comments,
    }
    raw_results = prompty.execute(prompt_path, inputs=inputs)
    try:
        results = json.loads(raw_results)
        return results
    except Exception:
        return {}


def _run_workflow(workflow_name, **kwargs):
    workflow_cls = WORKFLOW_REGISTRY.get(workflow_name)
    if not workflow_cls:
        raise ValueError(f"Unknown workflow: {workflow_name}")
    workflow = workflow_cls(**kwargs)
    return workflow.run()


def handle_mention_request(*, comments: list[str], language: str, package_name: str, code: str) -> str:
    """
    Central entry point for @mention requests. Parses the action and dispatches to the appropriate workflow.
    """
    if not comments:
        return "No comments provided."

    trigger_comment = comments[-1]  # Get the last comment to trigger the mention
    other_comments = comments[:-1]  # All other comments
    action_results = _parse_conversation_action(
        language=language,
        code=code,
        package_name=package_name,
        trigger_comment=trigger_comment,
        other_comments=other_comments,
    )
    action = action_results.get("action")
    reasoning = action_results.get("reasoning", "")
    if action == "no_action":
        return f"No action required: {reasoning}"
    if action in WORKFLOW_REGISTRY:
        return _run_workflow(
            action,
            language=language,
            code=code,
            package_name=package_name,
            trigger_comment=trigger_comment,
            other_comments=other_comments,
            reasoning=reasoning,
        )
    return f"Unknown or unsupported action: {action}"
