"""Offline prompt-contract tests; model adherence needs behavioral evaluation."""

import ast
from pathlib import Path


_ROOT = Path(__file__).resolve().parents[1]
_INSTRUCTION = (_ROOT / "agents/chat_agent/instruction.md").read_text(encoding="utf-8")


def test_azure_typespec_modeling_prioritizes_emitter_evidence() -> None:
    assert "By default, omit `sources` from the initial search" in _INSTRUCTION
    assert "apply the Azure TypeSpec source-priority exception below" in _INSTRUCTION
    assert "Azure TypeSpec polymorphism or future-proof SDK shapes" in _INSTRUCTION
    assert 'set `sources=["typespec_azure_docs"]` on the initial' in _INSTRUCTION
    assert "howtos#generate-client-libraries#08types.mdx" in _INSTRUCTION
    assert "Consult `typespec_docs` only for basic language semantics or a named gap after the Azure lookup" in _INSTRUCTION
    assert "do not infer Azure emitter support from generic union docs or wiki summaries" in _INSTRUCTION


def test_azure_typespec_modeling_preserves_single_leaf_recommendation() -> None:
    assert "including a single current success variant and Rust SDKs" in _INSTRUCTION
    assert "`@discriminator` model hierarchies" in _INSTRUCTION
    assert "a base with a single current leaf when future variants are expected" in _INSTRUCTION
    assert "Do not recommend `@discriminated` unions as the current Azure client-emitter solution" in _INSTRUCTION
    assert "Verify language-specific output separately" in _INSTRUCTION


def test_response_status_is_not_a_payload_discriminator() -> None:
    assert "`200`/`204` response unions may map to SDK enums" in _INSTRUCTION
    assert "`@statusCode` is not a payload discriminator" in _INSTRUCTION


def test_source_parameter_guidance_allows_initial_azure_typespec_scope() -> None:
    module = ast.parse((_ROOT / "tools/knowledge_tools.py").read_text(encoding="utf-8"))
    tools_class = next(
        node for node in module.body
        if isinstance(node, ast.ClassDef) and node.name == "KnowledgeTools"
    )
    search = next(
        node for node in tools_class.body
        if isinstance(node, ast.AsyncFunctionDef) and node.name == "search_knowledge_base"
    )
    sources = next(arg for arg in search.args.kwonlyargs if arg.arg == "sources")
    description = ast.literal_eval(sources.annotation.slice.elts[1])

    assert "Usually omit this parameter" in description
    assert "For Azure TypeSpec polymorphism or future-proof SDK shapes" in description
    assert "initially use ['typespec_azure_docs']" in description
    assert "otherwise restrict sources only in a targeted follow-up" in description
