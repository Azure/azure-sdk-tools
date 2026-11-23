"""Regression checks for document-aware Evolution Agent remediation contracts."""

from __future__ import annotations

import ast
import re
from pathlib import Path

from tools.knowledge_tools import KnowledgeTools

_ROOT = Path(__file__).resolve().parent.parent
_INSTRUCTION_PATH = _ROOT / "agents/chatbot_evolution_agent/instruction.md"
_INIT_PATH = _ROOT / "agents/chatbot_evolution_agent/init.py"


def _registered_knowledge_tools() -> set[str]:
    tree = ast.parse(_INIT_PATH.read_text(encoding="utf-8"))
    names: set[str] = set()
    for node in ast.walk(tree):
        if (
            isinstance(node, ast.Attribute)
            and isinstance(node.value, ast.Name)
            and node.value.id == "knowledge_tools"
        ):
            names.add(node.attr)
    return names


def test_instruction_kb_tools_match_registered_agent_tools() -> None:
    instruction = _INSTRUCTION_PATH.read_text(encoding="utf-8")
    registered = _registered_knowledge_tools()
    assert registered == {
        "list_knowledge_sources",
        "resolve_kb_source",
        "search_knowledge_base",
        "read_knowledge",
        "update_knowledge",
    }
    for name in registered:
        assert hasattr(KnowledgeTools, name)
        assert f"`{name}`" in instruction

    referenced_kb_tools = set(
        re.findall(r"`([a-z_]*(?:knowledge|kb_source)[a-z_]*)`", instruction)
    )
    assert referenced_kb_tools <= registered


def test_kb_remediation_enforces_doc_worthiness_and_generated_doc_policy() -> None:
    instruction = _INSTRUCTION_PATH.read_text(encoding="utf-8")
    taxonomy = instruction.split("### Classification taxonomy", 1)[1].split(
        "## KB remediation", 1
    )[0]
    kb_remediation = instruction.split("## KB remediation", 1)[1].split(
        "## Issue routing", 1
    )[0]

    assert "Doc-worthiness gate" in taxonomy
    for excluded in (
        "bugs",
        "transient infrastructure failures",
        "one-off",
        "unconfirmed thread opinions",
    ):
        assert excluded in taxonomy

    for tool_name in (
        "`list_knowledge_sources`",
        "`search_knowledge_base`",
        "`resolve_kb_source`",
        "`read_knowledge`",
    ):
        assert tool_name in kb_remediation

    assert "auto-generated" in kb_remediation
    assert (
        "Do not skip remediation or try to trace upstream generator code"
        in kb_remediation
    )
    assert "retrieval_mismatch" in kb_remediation
    assert "reasoning_gap" in kb_remediation


def test_issue_format_is_goal_oriented_and_preserves_tracking_marker() -> None:
    instruction = _INSTRUCTION_PATH.read_text(encoding="utf-8")
    issue_format = instruction.split("## Issue format", 1)[1].split(
        "## Output", 1
    )[0]

    assert (
        '<!-- chatbot-evolution source="<source>" '
        'classification="<classification>" scope="<scope>" -->'
    ) in issue_format
    assert "`[Teams Chatbot]: <concise summary>`" in issue_format
    assert "### Documentation goal" in issue_format
    assert "### Candidate validation reference" in issue_format
    assert "### Expected behavior" in issue_format
    assert "rather than copying verbatim" in issue_format
    assert "### Fixed document" not in instruction
