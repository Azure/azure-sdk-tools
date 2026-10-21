"""Tests for tenant skill and knowledge-source configuration."""

import asyncio
import json
from pathlib import Path

from agent_framework import FileSkillsSource
import pytest

from config.tenant_config import (
    SRC_AZURE_API_GUIDELINES,
    SRC_AZURE_AVOCADO_DOCS,
    SRC_AZURE_MCP_SERVER_DOCS,
    SRC_AZURE_OPENAPI_DIFF_DOCS,
    SRC_AZURE_REST_API_SPECS_DOCS,
    SRC_AZURE_REST_API_SPECS_WIKI,
    SRC_AZURE_SDK_DOCS_ENG,
    SRC_AZURE_SDK_FOR_JS_DOCS,
    SRC_AZURE_SDK_GUIDELINES,
    SRC_TYPESPEC_AZURE_DOCS,
    SRC_TYPESPEC_DOCS,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from skills.tenant_skills import (
    build_skill_content,
    create_tenant_skills,
    get_skill_name_for_tenant,
)


def test_stewardship_tenant_uses_shared_agent_and_maintained_sources() -> None:
    config = get_tenant_config(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert config is not None
    assert config.agent.name == "azure-sdk-chat-agent"
    assert config.agent.name_config_key == "AI_FOUNDRY_AGENT_NAME"
    assert config.display_name == "API Stewardship Board - General"
    assert config.skill_name == "api-stewardship-board"
    assert config.enable_routing
    assert not config.enable_wiki_cross_document_pages
    assert [source.name for source in config.sources] == [
        SRC_AZURE_API_GUIDELINES,
        SRC_TYPESPEC_DOCS,
        SRC_TYPESPEC_AZURE_DOCS,
        SRC_AZURE_SDK_GUIDELINES,
        SRC_AZURE_SDK_DOCS_ENG,
        SRC_AZURE_REST_API_SPECS_WIKI,
        SRC_AZURE_REST_API_SPECS_DOCS,
        SRC_AZURE_OPENAPI_DIFF_DOCS,
        SRC_AZURE_AVOCADO_DOCS,
    ]
    assert config.source_filter[SRC_AZURE_SDK_DOCS_ENG] == (
        "search.ismatch('design* OR plan* OR support* OR faqs*', 'title', 'full', 'any')"
    )


def test_stewardship_skill_contains_tenant_sources_and_guideline() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    skills = create_tenant_skills("azure-sdk-chat-agent")
    skill = next(s for s in skills if s.frontmatter.name == "api-stewardship-board")

    assert get_skill_name_for_tenant(TenantID.API_STEWARDSHIP_BOARD_QA_BOT) == (
        "api-stewardship-board"
    )
    assert "[skill_tenant_id]: api_stewardship_board_qa_bot" in content
    assert f"- {SRC_AZURE_AVOCADO_DOCS}:" in content
    assert "[skill_guideline]" in content
    assert "Advisory only" in skill.frontmatter.description
    assert "Never approve changes" in content
    assert "repair infrastructure or prescribe permission changes" in content
    assert "Use current guidance" in content
    assert "not historical schedules or workarounds" in content


def test_stewardship_pr_guidance_is_focused_and_separates_review_status() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert "Lead with the finding and next action" in content
    assert "a short paragraph or up to 3 short bullets" in content
    assert "omit speculative checklists, repeated evidence, and optional offers" in content
    assert "one concise point, without nested lists or extended rationale" in content
    assert "short descriptive link titles, not full document headings" in content
    assert "Avoid repeating the recommendation in a closing next-step section" in content
    assert "100 words" not in content
    assert "60 words" not in content
    assert "Separate design findings, Board signoff and merge readiness" in content
    steps = [
        "### 1. Identify the Request",
        "### 2. Inspect the Current PR",
        "### 3. Assess the Design or Compatibility Question",
        "### 4. Resolve Review-Process or Tooling Questions",
        "### 5. Report the Finding and Necessary Next Action",
    ]
    positions = [content.index(step) for step in steps]
    assert positions == sorted(positions)
    assert "discussion comments (`get_comments`)" in content
    assert "Green checks and signoff do not prove design correctness" in content
    assert "Brevity limits the answer, not the investigation" in content
    guideline = content.split("[skill_guideline]", 1)[1]
    assert len(guideline.split()) <= 500


def test_stewardship_check_support_prioritizes_reported_failure() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert "For check failures, lead with the reported check" in content
    assert "linked comments" in content
    assert "Passing checks do not establish that the user's problem is resolved" in content
    assert "do not invent an automation diagnosis" in content
    assert "Inspect linked error evidence before asking" in content
    assert "approval does not transfer to another PR" in content
    assert "Keep signoff secondary to check failures" in content


def test_stewardship_blocker_diagnosis_does_not_depend_on_merge_summary() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert "not the sole source of blockers" in content
    assert "independently inspect current-head" in content
    assert "branch-policy evidence" in content
    assert "whose blocking status is unverified" in content
    assert "single source of truth for merge blockers" not in content
    assert "complete, authoritative list of merge blockers" not in content


def test_stewardship_filter_does_not_broaden_existing_review_tenants() -> None:
    for tenant_id in (TenantID.API_SPEC_REVIEW_BOT, TenantID.TYPESPEC_CHANNEL_QA_BOT):
        config = get_tenant_config(tenant_id)
        assert config is not None
        assert config.source_filter[SRC_AZURE_SDK_DOCS_ENG] == (
            "search.ismatch('design*', 'title')"
        )


def test_stewardship_skill_requires_explicit_service_type_for_scoped_search() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert 'service_type="data-plane"' in content
    assert 'service_type="management-plane"' in content
    assert "including the initial search" in content
    assert "leave it unset when ambiguous or general" in content
    assert "`wiki_search` has no service-type filter" in content


def test_stewardship_request_identification_stays_focused() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    identify = content.split("### 1. Identify the Request\n", 1)[1].split(
        "### 2. Inspect the Current PR", 1
    )[0]

    assert len(identify.split()) <= 75
    assert "service type (data plane or management plane)" in identify
    assert "search_knowledge_base" not in identify


def test_stewardship_routes_arm_reviews_to_arm_office_hours() -> None:
    config = get_tenant_config(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    assert config is not None
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    skill = next(
        s for s in create_tenant_skills("azure-sdk-chat-agent")
        if s.frontmatter.name == "api-stewardship-board"
    )
    arm_office_hours = "https://armwiki.azurewebsites.net/introduction/office_hours.html"

    assert "Data-plane API design" in config.scope
    assert any(arm_office_hours in exclusion for exclusion in config.exclusions)
    assert f"[ARM Office Hours]({arm_office_hours})" in content
    assert "stop this Board review workflow" in content
    assert "Direct ARM/management-plane reviews to ARM Office Hours" in skill.frontmatter.description


@pytest.mark.parametrize(
    ("source_name", "title", "expected_url"),
    [
        (
            SRC_AZURE_AVOCADO_DOCS,
            "README.md",
            "https://github.com/Azure/avocado/blob/main/README.md",
        ),
        (
            SRC_AZURE_REST_API_SPECS_DOCS,
            ".github#workflows#src#package-name-approval#PACKAGE-NAME-REVIEW-PROCESS.md",
            "https://github.com/Azure/azure-rest-api-specs/blob/main/"
            ".github/workflows/src/package-name-approval/PACKAGE-NAME-REVIEW-PROCESS.md",
        ),
        (
            SRC_AZURE_REST_API_SPECS_DOCS,
            "documentation#ci-fix.md",
            "https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/ci-fix.md",
        ),
        (
            SRC_AZURE_SDK_DOCS_ENG,
            "docs#support#support.md",
            "https://eng.ms/docs/products/azure-developer-experience/support/support",
        ),
    ],
)
def test_stewardship_document_citations(
    source_name: str, title: str, expected_url: str
) -> None:
    source = get_knowledge_source(source_name)

    assert source is not None
    assert source.get_link(title) == expected_url


def test_stewardship_sources_have_matching_sync_configuration() -> None:
    config_path = (
        Path(__file__).parents[2]
        / "azure-sdk-qa-bot-knowledge-sync"
        / "config"
        / "knowledge-config.json"
    )
    config = json.loads(config_path.read_text(encoding="utf-8"))
    sources = config["sources"]
    avocado = next(
        s for s in sources if s["repository"]["url"] == "https://github.com/Azure/avocado.git"
    )
    assert avocado["repository"]["branch"] == "main"
    assert avocado["paths"][0]["path"] == "/README.md"
    assert avocado["paths"][0]["folder"] == SRC_AZURE_AVOCADO_DOCS
    specs = next(
        s
        for s in sources
        if s["repository"]["url"] == "https://github.com/Azure/azure-rest-api-specs.git"
    )
    assert {p["path"] for p in specs["paths"]} == {
        "/documentation/BreakingChange-Oad-Rules-Mapping.md",
        "/eng/tools/typespec-suppressions/README.md",
        "/.github/arm-leases/README.md",
        "/.github/workflows/src/package-name-approval/PACKAGE-NAME-REVIEW-PROCESS.md",
        "/documentation/ci-fix.md",
    }
    assert all(p["folder"] == SRC_AZURE_REST_API_SPECS_DOCS for p in specs["paths"])


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