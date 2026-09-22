"""Tests for tenant skill and knowledge-source configuration."""

from pathlib import Path

from config.tenant_config import (
    SRC_AZURE_MCP_SERVER_DOCS,
    SRC_AZURE_SDK_DOCS_ENG,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from skills.tenant_skills import (
    build_skill_content,
    create_tenant_skills,
    get_skill_name_for_tenant,
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
    assert f"- {SRC_AZURE_SDK_DOCS_ENG}:" in content
    assert "Azure SDK engineering documentation" in content
    assert "onboarding, releases, engineering systems" in content
    assert "Azure MCP guidance" in content
    assert "[skill_guideline]" not in content
    assert "[skill_code_repositories]: none" in content


def test_typespec_skill_declares_repository_scope() -> None:
    content = build_skill_content(TenantID.TYPESPEC_CHANNEL_QA_BOT)

    assert "[skill_code_repositories]" in content
    assert "- Azure/typespec-azure/packages" in content
    assert "- microsoft/typespec/packages" in content


def test_repository_scope_is_limited_to_implementation_skills() -> None:
    enabled_tenants = {
        tenant_id
        for tenant_id in TenantID
        if (config := get_tenant_config(tenant_id)) is not None
        and config.code_repositories
    }

    assert enabled_tenants == {
        TenantID.PYTHON_CHANNEL_QA_BOT,
        TenantID.TYPESPEC_CHANNEL_QA_BOT,
        TenantID.TYPESPEC_EMITTER_QA_BOT,
        TenantID.AZURE_TYPESPEC_AUTHORING,
    }


def test_chat_agent_instruction_gates_repository_search() -> None:
    instruction = (
        Path(__file__).parents[1]
        / "agents"
        / "chat_agent"
        / "instruction.md"
    ).read_text(encoding="utf-8")

    assert (
        "only when the active skill declares `[skill_code_repositories]`"
        in instruction
    )
    assert "requires exact implementation evidence" in instruction
    assert "`file_access_grep` is required in the first retrieval batch" in instruction
    assert "read the most relevant file with `file_access_read` before composing" in instruction
    assert "Use at most two repository grep calls and two repository read calls" in instruction
    assert "never search an empty directory or a generic suffix" in instruction
    assert "Do not use repository search for policy, process, permissions" in instruction
    assert "do not add it merely to confirm" in instruction
    assert "Apply implementation evidence completely" in instruction


def test_implementation_skills_require_repository_verification() -> None:
    typespec_content = build_skill_content(TenantID.TYPESPEC_CHANNEL_QA_BOT)
    authoring_content = build_skill_content(TenantID.AZURE_TYPESPEC_AUTHORING)
    python_content = build_skill_content(TenantID.PYTHON_CHANNEL_QA_BOT)

    assert "verify the answer against the synchronized package" in typespec_content
    assert "standard, legacy, and routed operation declarations" in typespec_content
    assert "evaluate the new operation's required route and wire contract independently" in typespec_content
    assert "declaration files by semantic operation kind and behavior" in typespec_content
    assert "repository-defined trigger and the minimal supported fix" in typespec_content
    assert "verify the solution against the synchronized declaration" in authoring_content
    assert "greenfield standard contracts from brownfield" in authoring_content
    assert "evaluate the new operation's required route and wire contract independently" in authoring_content
    assert "Treat a sample as supporting evidence" in authoring_content
    assert "verify the behavior in the synchronized client-generator-core" in python_content


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


def test_chat_agent_allows_unattended_read_only_skill_loading() -> None:
    init_path = (
        Path(__file__).parents[1]
        / "agents"
        / "chat_agent"
        / "init.py"
    )
    agent_source = init_path.read_text(encoding="utf-8")

    assert "disable_load_skill_approval=True" in agent_source
    assert "disable_read_skill_resource_approval=True" in agent_source
    assert "disable_run_skill_script_approval=True" not in agent_source