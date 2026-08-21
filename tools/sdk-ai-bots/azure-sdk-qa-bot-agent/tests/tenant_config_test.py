"""Tests for tenant skill and knowledge-source configuration."""

from pathlib import Path

from config.tenant_config import (
    SRC_AZURE_MCP_SERVER_DOCS,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from skills.tenant_skills import (
    build_skill_content,
    create_tenant_skills,
    get_skill_name_for_tenant,
)


def test_azure_mcp_server_tenant_uses_curated_source() -> None:
    config = get_tenant_config(TenantID.AZURE_MCP_SERVER)

    assert config is not None
    assert config.skill_name == "azure-mcp-server"
    assert [source.name for source in config.sources] == [SRC_AZURE_MCP_SERVER_DOCS]
    assert config.agent.name == "azure-mcp-server-agent"
    assert config.agent.name_config_key == "AZURE_MCP_SERVER_AGENT_NAME"


def test_tenants_use_azure_sdk_agent_by_default() -> None:
    config = get_tenant_config(TenantID.TYPESPEC_CHANNEL_QA_BOT)

    assert config is not None
    assert config.agent.name == "azure-sdk-chat-agent"
    assert config.agent.name_config_key == "AI_FOUNDRY_AGENT_NAME"


def test_tenant_skills_are_partitioned_by_agent() -> None:
    sdk_skills = create_tenant_skills("azure-sdk-chat-agent")
    mcp_skills = create_tenant_skills("azure-mcp-server-agent")

    assert len(sdk_skills) > 1
    assert len(mcp_skills) == 1
    assert mcp_skills[0].frontmatter.name == "azure-mcp-server"


def test_azure_mcp_server_skill_contains_routing_metadata() -> None:
    content = build_skill_content(TenantID.AZURE_MCP_SERVER)

    assert get_skill_name_for_tenant(TenantID.AZURE_MCP_SERVER) == "azure-mcp-server"
    assert f"[skill_tenant_id]: {TenantID.AZURE_MCP_SERVER.value}" in content
    assert f"- {SRC_AZURE_MCP_SERVER_DOCS}:" in content
    assert "[skill_guideline]" not in content


def test_azure_mcp_server_source_resolves_repository_paths() -> None:
    source = get_knowledge_source(SRC_AZURE_MCP_SERVER_DOCS)

    assert source is not None
    assert "team Q&A" in source.description
    assert "onboarding and merge practices" in source.description
    assert source.get_link("servers/Azure.Mcp.Server/README.md") == (
        "https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/README.md"
    )
    assert source.get_link("docs/Authentication.md") == (
        "https://github.com/microsoft/mcp/blob/main/docs/Authentication.md"
    )


def test_azure_mcp_server_instruction_selects_sources_by_question() -> None:
    instruction_path = (
        Path(__file__).parents[1]
        / "agents"
        / "azure_mcp_server_agent"
        / "instruction.md"
    )
    instruction = instruction_path.read_text(encoding="utf-8")

    assert "Use Microsoft Learn for public setup" in instruction
    assert "Use GitHub MCP for source code" in instruction
    assert "raw.githubusercontent.com" in instruction
    assert "rather than `web_fetch`" in instruction
    assert "Use the Azure MCP knowledge source for team guidance" in instruction
    assert "Preserve concrete distinctions and requirements" in instruction
    assert "answer from it" in instruction
    assert "not to broaden the scope or introduce unsupported requirements" in instruction
    assert "only details that materially affect the recommendation or next action" in instruction
    assert "instead of reproducing exhaustive checklists" in instruction
    assert "Lead with a direct answer in 1–3 sentences" in instruction
    assert "Prefer short bullets with one idea each" in instruction
    assert "under roughly 150 words unless the user asks for detail" in instruction
    assert "For broad or multi-part questions" in instruction
    assert "For under-specified questions, give a short answer first" in instruction