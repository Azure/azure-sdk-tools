"""Tests for tenant skill and knowledge-source configuration."""

import asyncio
import json
from pathlib import Path

import pytest
from agent_framework import FileSkillsSource

from config import tenant_config
from config.tenant_config import (
    SRC_AZURE_MCP_SERVER_DOCS,
    SRC_AZURE_REST_API_SPECS_DOCS,
    SRC_AZURE_REST_API_SPECS_WIKI,
    SRC_AZURE_SDK_DOCS_ENG,
    SRC_AZURE_SDK_FOR_JS_DOCS,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from skills.tenant_skills import (
    build_skill_content,
    create_tenant_skills,
    get_skill_name_for_tenant,
)


@pytest.mark.parametrize(
    ("tenant_id", "extra_sources", "guideline_language", "emitter_language"),
    [
        (
            TenantID.PYTHON_CHANNEL_QA_BOT,
            [
                tenant_config.SRC_AZURE_SDK_FOR_PYTHON_DOCS,
            ],
            "python",
            "python",
        ),
        (
            TenantID.DOTNET_CHANNEL_QA_BOT,
            [
                tenant_config.SRC_AZURE_SDK_FOR_NET_DOCS,
                tenant_config.SRC_TYPESPEC_CSHARP_EMITTER_DOCS,
            ],
            "dotnet",
            "csharp",
        ),
        (
            TenantID.GOLANG_CHANNEL_QA_BOT,
            [
                tenant_config.SRC_AZURE_SDK_FOR_GO_DOCS,
            ],
            "golang",
            "go",
        ),
        (
            TenantID.JAVA_CHANNEL_QA_BOT,
            [
                tenant_config.SRC_AZURE_SDK_FOR_JAVA_DOCS,
                tenant_config.SRC_AZURE_SDK_FOR_JAVA_WIKI,
                tenant_config.SRC_AUTOREST_JAVA_DOCS,
            ],
            "java",
            "java",
        ),
        (
            TenantID.JAVASCRIPT_CHANNEL_QA_BOT,
            [
                tenant_config.SRC_AZURE_SDK_FOR_JS_DOCS,
            ],
            "typescript",
            "ts",
        ),
    ],
)
def test_language_tenants_include_shared_sources_and_preserve_filters(
    tenant_id, extra_sources, guideline_language, emitter_language
) -> None:
    config = get_tenant_config(tenant_id)
    assert config is not None
    common_sources = [
        tenant_config.SRC_AZURE_SDK_GUIDELINES,
        SRC_AZURE_SDK_DOCS_ENG,
        tenant_config.SRC_AZURE_SDK_INTERNAL_WIKI,
        tenant_config.SRC_TYPESPEC_AZURE_DOCS,
        SRC_AZURE_REST_API_SPECS_WIKI,
        SRC_AZURE_REST_API_SPECS_DOCS,
    ]
    assert [source.name for source in tenant_config._SDK_SOURCES] == common_sources
    names = [source.name for source in config.sources]
    assert len(names) == len(set(names))
    assert set(names) == set(common_sources + extra_sources)
    assert config.sources is not tenant_config._SDK_SOURCES
    assert config.source_filter == {
        tenant_config.SRC_AZURE_SDK_GUIDELINES: (
            f"search.ismatch('{guideline_language}', 'title')"
        ),
        tenant_config.SRC_TYPESPEC_AZURE_DOCS: (
            f"search.ismatch('typespec-{emitter_language}', 'title') "
            "or search.ismatch('generate*', 'title')"
        ),
        SRC_AZURE_REST_API_SPECS_WIKI: "search.ismatch('SDK', 'title')",
    }


def test_javascript_tenant_uses_repository_docs_instead_of_wiki() -> None:
    config = get_tenant_config(TenantID.JAVASCRIPT_CHANNEL_QA_BOT)

    assert config is not None
    assert SRC_AZURE_SDK_FOR_JS_DOCS in [source.name for source in config.sources]
    assert "azure_sdk_for_js_wiki" not in [source.name for source in config.sources]
    assert get_knowledge_source("azure_sdk_for_js_wiki") is None


def test_javascript_contributing_guide_resolves_to_repository_root() -> None:
    source = get_knowledge_source(SRC_AZURE_SDK_FOR_JS_DOCS)

    assert source is not None
    assert source.get_link("CONTRIBUTING.md") == (
        "https://github.com/Azure/azure-sdk-for-js/blob/main/CONTRIBUTING.md"
    )


def test_azure_mcp_server_tenant_uses_curated_sources() -> None:
    config = get_tenant_config(TenantID.AZURE_MCP_SERVER)

    assert config is not None
    assert config.skill_name == "azure-mcp-server"
    assert [source.name for source in config.sources] == [
        SRC_AZURE_MCP_SERVER_DOCS,
        SRC_AZURE_SDK_DOCS_ENG,
    ]
    assert config.source_filter[SRC_AZURE_SDK_DOCS_ENG] == (
        "search.ismatch('mcp*', 'title')"
    )
    assert config.agent.name == "azure-mcp-server-agent"
    assert config.agent.name_config_key == "AZURE_MCP_SERVER_AGENT_NAME"


def test_tenants_use_azure_sdk_agent_by_default() -> None:
    config = get_tenant_config(TenantID.TYPESPEC_CHANNEL_QA_BOT)

    assert config is not None
    assert config.agent.name == "azure-sdk-chat-agent"
    assert config.agent.name_config_key == "AI_FOUNDRY_AGENT_NAME"


def test_go_tenant_includes_sdk_breaking_change_guides() -> None:
    config = get_tenant_config(TenantID.GOLANG_CHANNEL_QA_BOT)
    assert config is not None
    sources = {source.name: source for source in config.sources}
    assert sources[SRC_AZURE_REST_API_SPECS_WIKI].get_link("SDK-Breaking-Changes.md") == (
        "https://github.com/Azure/azure-rest-api-specs/wiki/SDK-Breaking-Changes"
    )
    assert config.source_filter[SRC_AZURE_REST_API_SPECS_WIKI] == "search.ismatch('SDK', 'title')"
    assert sources[SRC_AZURE_REST_API_SPECS_DOCS].get_link("documentation/ci-fix.md") == (
        "https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/ci-fix.md"
    )
    assert SRC_AZURE_REST_API_SPECS_DOCS not in config.source_filter

    sync_path = (
        Path(__file__).parents[2] / "azure-sdk-qa-bot-knowledge-sync"
        / "config" / "knowledge-config.json"
    )
    sync_config = json.loads(sync_path.read_text(encoding="utf-8"))
    specs = next(
        source for source in sync_config["sources"]
        if source["repository"]["url"] == "https://github.com/Azure/azure-rest-api-specs.git"
    )
    guide = next(item for item in specs["paths"] if item["path"] == "/documentation/ci-fix.md")
    assert guide["folder"] == SRC_AZURE_REST_API_SPECS_DOCS
    assert guide["relativeByRepoPath"] is True
    assert guide["metadata"]["scope"] == "branded"


def test_tenant_skills_are_partitioned_by_agent() -> None:
    sdk_skills = create_tenant_skills("azure-sdk-chat-agent")
    mcp_skills = create_tenant_skills("azure-mcp-server-agent")

    assert len(sdk_skills) > 1
    assert len(mcp_skills) == 1
    assert mcp_skills[0].frontmatter.name == "azure-mcp-server"


def test_protected_arm_label_authorization_routes_to_onboarding() -> None:
    skills = {
        skill.frontmatter.name: skill
        for skill in create_tenant_skills("azure-sdk-chat-agent")
    }
    review = skills["api-spec-review"].frontmatter.description
    onboarding = skills["sdk-onboarding"].frontmatter.description
    assert "protected ARM label authorization" in review
    assert "use sdk-onboarding instead" in review
    assert "protected ARM label authorization" in onboarding
    assert "ARMSignedOff" in onboarding
    assert "suppression approval" in onboarding
    assert "validation errors" in review
    assert "breaking changes" in review


def test_chat_instruction_routes_label_permissions_despite_pr_context() -> None:
    instruction = (
        Path(__file__).resolve().parents[1]
        / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    assert "load `sdk-onboarding`, not `api-spec-review`" in instruction
    assert "even when the question includes a PR or merge blocker" in instruction
    assert "`ARMSignedOff` or suppression approval" in instruction
    assert "**Authoring tenant lock (overrides rules below)**" in instruction


def test_azure_mcp_server_skill_contains_routing_metadata() -> None:
    content = build_skill_content(TenantID.AZURE_MCP_SERVER)

    assert get_skill_name_for_tenant(TenantID.AZURE_MCP_SERVER) == "azure-mcp-server"
    assert f"[skill_tenant_id]: {TenantID.AZURE_MCP_SERVER.value}" in content
    assert f"- {SRC_AZURE_MCP_SERVER_DOCS}:" in content
    assert f"- {SRC_AZURE_SDK_DOCS_ENG}:" in content
    assert "Azure SDK engineering documentation" in content
    assert "onboarding, releases, engineering systems" in content
    assert "Azure MCP guidance" in content
    assert "[skill_guideline]" not in content


def test_dotnet_skill_composes_common_and_specific_guidelines_once() -> None:
    content = build_skill_content(TenantID.DOTNET_CHANNEL_QA_BOT)

    assert content.count("[skill_guideline]") == 1
    assert content.count("# Tenant: Azure SDK Language Channel Assistant") == 1
    assert content.count("# .NET-Specific Answer Guidelines") == 1
    assert content.index("### SDK Lifecycle") < content.index(
        "# .NET-Specific Answer Guidelines"
    )
    assert "{{tenant_id}}" not in content
    assert "mgmt-review-comment-resolution/SKILL.md" in content


def test_azure_mcp_server_source_resolves_repository_paths() -> None:
    source = get_knowledge_source(SRC_AZURE_MCP_SERVER_DOCS)

    assert source is not None
    assert source.get_link("servers/Azure.Mcp.Server/README.md") == (
        "https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/README.md"
    )
    assert source.get_link("docs/Authentication.md") == (
        "https://github.com/microsoft/mcp/blob/main/docs/Authentication.md"
    )
    title = "servers#Azure.Mcp.Server#docs#new-command.md"
    assert source.get_link(title) == (
        "https://github.com/microsoft/mcp/blob/main/"
        "servers/Azure.Mcp.Server/docs/new-command.md"
    )

    sync_path = (
        Path(__file__).parents[2] / "azure-sdk-qa-bot-knowledge-sync"
        / "config" / "knowledge-config.json"
    )
    sync_config = json.loads(sync_path.read_text(encoding="utf-8"))
    mcp_source = next(
        entry for entry in sync_config["sources"]
        if entry["repository"]["url"] == "https://github.com/microsoft/mcp.git"
    )
    guide = next(
        entry for entry in mcp_source["paths"]
        if entry["path"] == "/servers/Azure.Mcp.Server/docs/new-command.md"
    )
    assert guide["folder"] == source.name
    assert guide["relativeByRepoPath"] is True


def test_internal_mcp_docs_resolve_to_eng_ms_paths() -> None:
    source = get_knowledge_source(SRC_AZURE_SDK_DOCS_ENG)

    assert source is not None
    assert source.get_link("docs#mcp.md") == (
        "https://eng.ms/docs/products/azure-developer-experience/mcp"
    )
    assert source.get_link("docs#mcp#getting-started.md") == (
        "https://eng.ms/docs/products/azure-developer-experience/mcp/getting-started"
    )


def test_azure_mcp_server_instruction_selects_sources_by_question() -> None:
    instruction_path = (
        Path(__file__).parents[1]
        / "agents"
        / "azure_mcp_server_agent"
        / "instruction.md"
    )
    instruction = instruction_path.read_text(encoding="utf-8")

    assert "Use Microsoft Learn for public product documentation" in instruction
    assert "Use GitHub MCP for current source code" in instruction
    assert "Do not use `web_fetch` for GitHub content" in instruction
    assert "Use the Azure MCP knowledge source for internal guidance" in instruction
    assert "Prefer specific evidence about the user's case" in instruction
    assert "only details that materially affect the recommendation or next action" in instruction
    assert "instead of reproducing exhaustive checklists" in instruction
    assert "Lead with a direct answer in 1–3 sentences" in instruction
    assert "Prefer short bullets with one idea each" in instruction
    assert "under roughly 150 words unless the user asks for detail" in instruction
    assert "For broad or multi-part questions" in instruction
    assert "For under-specified questions, answer what the evidence establishes" in instruction
    assert "use `wiki_search` when a synthesized view would help" in instruction
    assert "Use the tenant context supplied by the preloaded skill" in instruction
    assert "tenant_id=azure_mcp_server" not in instruction
    assert "Resolve important evidence gaps before answering" in instruction
    assert "Do not invent unsupported details or links" in instruction


def test_azure_mcp_server_instruction_preserves_decision_criteria() -> None:
    """Guard instruction content, not the model's behavioral compliance."""
    instruction = (
        Path(__file__).parents[1]
        / "agents"
        / "azure_mcp_server_agent"
        / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "Preserve the user's stated goal when reformulating searches" in instruction
    assert "retrieve their definitions before drawing conclusions" in instruction
    assert "sources leave the mapping ambiguous" in instruction
    assert "A limitation in one area does not negate a documented benefit in another" in instruction
    assert "Do not turn a caveat into a prohibition or mandatory requirement" in instruction
    assert "Label additional precautions as recommendations" in instruction
    assert "preserving its conditions and exceptions" in instruction
    assert "do not give a definitive recommendation that depends on an unresolved assumption" in instruction


def test_azure_mcp_server_instruction_limits_retrieval_rounds() -> None:
    """Guard latency guidance; model adherence needs behavioral evaluation."""
    instruction = (
        Path(__file__).parents[1]
        / "agents"
        / "azure_mcp_server_agent"
        / "instruction.md"
    ).read_text(encoding="utf-8")

    assert 'Start with `search_mode="quick"` for both' in instruction
    assert "Batch independent, necessary tool calls in parallel" in instruction
    assert "Wiki results already include source chunks" in instruction
    assert "Aim for one retrieval round followed by the answer" in instruction
    assert "make one targeted follow-up instead of repeating a broad search" in instruction
    assert 'Escalate to `search_mode="deep"` only when quick retrieval leaves a specific evidence gap' in instruction
    assert "Do not escalate merely to confirm an already supported answer" in instruction
    assert "Never repeat a tool call with identical arguments" in instruction


def test_azure_mcp_server_pr_review_help_skill_is_discoverable() -> None:
    root = Path(__file__).parents[1]
    skills = asyncio.run(
        FileSkillsSource(root / "skills" / "azure_mcp_server").get_skills()
    )
    instruction = (
        root / "agents" / "azure_mcp_server_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert [skill.frontmatter.name for skill in skills] == ["pr-review-help"]
    assert "onboarding buddy" in skills[0].frontmatter.description
    assert "load the `pr-review-help` skill" in instruction


def test_azure_mcp_server_agent_registers_wiki_search() -> None:
    init_path = (
        Path(__file__).parents[1]
        / "agents"
        / "azure_mcp_server_agent"
        / "init.py"
    )
    agent_source = init_path.read_text(encoding="utf-8")

    assert "knowledge_tools.search_knowledge_base" in agent_source
    assert "knowledge_tools.wiki_search" in agent_source