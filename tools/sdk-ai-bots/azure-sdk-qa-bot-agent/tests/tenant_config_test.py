"""Tests for tenant skill and knowledge-source configuration."""

import asyncio
import json
from pathlib import Path
from typing import cast

import pytest
from agent_framework import (
    FileSkillsSource,
    SkillsSourceContext,  # pyright: ignore[reportAttributeAccessIssue]
)

from config import tenant_config
from config.tenant_config import (
    SRC_AZURE_MCP_SERVER_DOCS,
    SRC_AZURE_REST_API_SPECS_DOCS,
    SRC_AZURE_SDK_DOCS_ENG,
    SRC_AZURE_SDK_FOR_JS_DOCS,
    SRC_STATIC_CPEX_DOCS,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from skills.tenant_skills import (
    build_skill_content,
    create_tenant_skills,
    get_skill_name_for_tenant,
    get_skill_to_tenant_map,
)

_AGENT_ROOT = Path(__file__).parents[1]


def _read_pr_review_skill() -> str:
    return (
        _AGENT_ROOT / "skills" / "chat_agent" / "pr-review" / "SKILL.md"
    ).read_text(encoding="utf-8")


@pytest.mark.parametrize(
    "tenant_id",
    [TenantID.API_SPEC_REVIEW_BOT],
)
@pytest.mark.parametrize("service_type", [None, "data-plane"])
def test_review_engineering_docs_keep_source_scope_without_title_restrictions(
    tenant_id: TenantID,
    service_type: str | None,
) -> None:
    from tools.knowledge_tools import _resolve_source_filters

    config = get_tenant_config(tenant_id)
    assert config is not None
    filters = _resolve_source_filters(
        [source.name for source in config.sources],
        tenant_id.value,
        service_type,
    )

    expected = f"context_id eq '{SRC_AZURE_SDK_DOCS_ENG}'"
    if service_type:
        expected += f" and (service_type eq '{service_type}' or service_type eq null)"
    assert filters[SRC_AZURE_SDK_DOCS_ENG] == expected
    assert set(filters) == {source.name for source in config.sources}
    source = get_knowledge_source(SRC_AZURE_SDK_DOCS_ENG)
    assert source is not None
    assert source.get_link(
        "docs#develop#namespace-review.md"
    ) == (
        "https://eng.ms/docs/products/azure-developer-experience/develop/namespace-review"
    )


def test_api_spec_review_preserves_other_source_filters() -> None:
    config = get_tenant_config(TenantID.API_SPEC_REVIEW_BOT)
    assert config is not None

    assert config.source_filter == {
        SRC_STATIC_CPEX_DOCS: "search.ismatch('/.*breaking.*/', 'title', 'full', 'any')",
    }


def test_chat_agent_exposes_github_actions_log_retrieval() -> None:
    from tools.github_mcp_tools import _GITHUB_READONLY_TOOLS

    assert "get_job_logs" in _GITHUB_READONLY_TOOLS
    assert "actions_get_job_logs" not in _GITHUB_READONLY_TOOLS


def test_shared_retrieval_uses_bounded_batch_and_latency_constraints() -> None:
    """Guard hard limits; model adherence is evaluated with the Foundry scenarios."""
    instruction = (
        _AGENT_ROOT / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "8 tool calls" in instruction
    assert "3 dependent follow-up rounds" in instruction
    assert "`load_skill` ALONE" in instruction


def test_pr_review_preserves_status_and_log_tool_contracts() -> None:
    pr_review = _read_pr_review_skill()

    for method in ("pull_request_read", "get_status", "get_check_runs", "get_job_logs"):
        assert f"`{method}`" in pr_review
    assert "`return_content=true`" in pr_review


def test_pr_review_preserves_required_access_links() -> None:
    pr_review = _read_pr_review_skill()

    assert "https://aka.ms/azsdk/access" in pr_review
    assert (
        "https://docs.github.com/en/account-and-profile/how-tos/"
        "organization-membership/publicizing-or-hiding-organization-membership"
    ) in pr_review


def test_review_filter_changes_do_not_broaden_typespec_tenant() -> None:
    config = get_tenant_config(TenantID.TYPESPEC_CHANNEL_QA_BOT)
    assert config is not None
    assert config.source_filter[SRC_AZURE_SDK_DOCS_ENG] == (
        "search.ismatch('design*', 'title')"
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
    assert sources[SRC_AZURE_REST_API_SPECS_DOCS].get_link(
        "documentation#validation#sdk-breaking-changes.md"
    ) == (
        "https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/validation/sdk-breaking-changes.md"
    )
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
    guide = next(item for item in specs["paths"] if item["path"] == "/documentation")
    assert guide["folder"] == SRC_AZURE_REST_API_SPECS_DOCS
    assert guide["relativeByRepoPath"] is True
    assert guide["metadata"]["scope"] == "branded"


@pytest.mark.parametrize(
    "tenant_id",
    [
        TenantID.TYPESPEC_CHANNEL_QA_BOT,
        TenantID.AZURE_TYPESPEC_AUTHORING,
        TenantID.API_SPEC_REVIEW_BOT,
        TenantID.PYTHON_CHANNEL_QA_BOT,
        TenantID.DOTNET_CHANNEL_QA_BOT,
        TenantID.GOLANG_CHANNEL_QA_BOT,
        TenantID.JAVA_CHANNEL_QA_BOT,
        TenantID.JAVASCRIPT_CHANNEL_QA_BOT,
    ],
)
def test_specs_tenants_use_repository_docs_instead_of_wiki(tenant_id: TenantID) -> None:
    config = get_tenant_config(tenant_id)
    assert config is not None
    names = [source.name for source in config.sources]
    assert SRC_AZURE_REST_API_SPECS_DOCS in names
    assert len(names) == len(set(names))
    assert "azure_rest_api_specs_wiki" not in names
    assert "azure_rest_api_specs_wiki" not in config.source_filter
    assert get_knowledge_source("azure_rest_api_specs_wiki") is None
    assert SRC_AZURE_REST_API_SPECS_DOCS not in config.source_filter


@pytest.mark.parametrize(
    "title",
    [
        "documentation#contributing#api-spec-review-faq.md",
        "documentation#specification-guidelines#azure-service-versioning-guide.md",
        "documentation#validation#typespec-validation.md",
    ],
)
def test_migrated_specs_guides_resolve_to_repository_links(title: str) -> None:
    source = get_knowledge_source(SRC_AZURE_REST_API_SPECS_DOCS)
    assert source is not None
    assert source.get_link(title) == (
        "https://github.com/Azure/azure-rest-api-specs/blob/main/"
        + title.replace("#", "/")
    )


def test_tenant_skills_are_partitioned_by_agent() -> None:
    sdk_skills = create_tenant_skills("azure-sdk-chat-agent")
    mcp_skills = create_tenant_skills("azure-mcp-server-agent")

    assert len(sdk_skills) > 1
    assert len(mcp_skills) == 1
    assert mcp_skills[0].frontmatter.name == "azure-mcp-server"


def test_chat_agent_pr_review_skill_is_discoverable_and_registered(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    skills = asyncio.run(
        FileSkillsSource(
            skill_paths=_AGENT_ROOT / "skills" / "chat_agent"
        ).get_skills(  # pyright: ignore[reportCallIssue]
            cast(SkillsSourceContext, None)  # pyright: ignore[reportCallIssue]
        )
    )
    assert [skill.frontmatter.name for skill in skills] == ["pr-review"]
    assert "pr-review" not in get_skill_to_tenant_map()

    init = (
        _AGENT_ROOT / "agents" / "chat_agent" / "init.py"
    ).read_text(encoding="utf-8")
    assert "FileSkillsSource(skill_paths=skills_dir)" in init
    assert "AggregatingSkillsSource" in init
    assert "skills_dir = _get_chat_skill_dir()" in init

    from agents.chat_agent import init as chat_agent_init

    monkeypatch.setattr(
        chat_agent_init.tenant_skills,
        "__file__",
        str(Path("/app/skills/tenant_skills.py")),
    )
    assert chat_agent_init._get_chat_skill_dir() == Path("/app/skills/chat_agent")


def test_pr_review_skill_does_not_change_tenant_routing() -> None:
    from openai.types.responses.response_function_tool_call import (
        ResponseFunctionToolCall,
    )

    from services.chat_service import ChatService

    def load_skill_call(skill_name: str) -> ResponseFunctionToolCall:
        return ResponseFunctionToolCall(
            id=f"fc_{skill_name}",
            call_id=f"call_{skill_name}",
            name="load_skill",
            arguments=json.dumps({"skill_name": skill_name}),
            type="function_call",
            status="completed",
        )

    assert ChatService._extract_routed_tenant([load_skill_call("pr-review")]) is None
    assert ChatService._extract_routed_tenant(
        [load_skill_call("api-spec-review")]
    ) == TenantID.API_SPEC_REVIEW_BOT


def test_azure_mcp_server_skill_contains_routing_metadata() -> None:
    content = build_skill_content(TenantID.AZURE_MCP_SERVER)

    assert get_skill_name_for_tenant(TenantID.AZURE_MCP_SERVER) == "azure-mcp-server"
    assert f"[skill_tenant_id]: {TenantID.AZURE_MCP_SERVER.value}" in content
    assert f"- {SRC_AZURE_MCP_SERVER_DOCS}:" in content
    assert f"- {SRC_AZURE_SDK_DOCS_ENG}:" in content
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






def test_tenant_skills_do_not_route_permission_help_to_onboarding() -> None:
    for skill in create_tenant_skills("azure-sdk-chat-agent"):
        if skill.frontmatter.name in ("api-spec-review", "sdk-onboarding"):
            description = skill.frontmatter.description.lower()
            assert "repository access" not in description
            assert "permission" not in description
    for tenant_id in (
        TenantID.API_SPEC_REVIEW_BOT,
        TenantID.AZURE_SDK_ONBOARDING,
    ):
        config = get_tenant_config(tenant_id)
        assert config is not None
        assert not any(
            ("access" in exclusion.lower() or "permission" in exclusion.lower())
            and "onboarding" in exclusion.lower()
            for exclusion in config.exclusions
        )
        assert not any("permission" in topic.lower() for topic in config.topics)




def test_api_spec_review_tenant_delegates_workflow_diagnosis_to_shared_guidance() -> None:
    prompt = (
        Path(__file__).parents[1] / "prompts" / "tenants" / "api_spec_review.md"
    ).read_text(encoding="utf-8")

    assert "use the `pr-review` skill" in prompt
    assert "`spec-pr-review`" not in prompt
    assert "`spec-validation`" not in prompt
    assert "`api-breaking-changes`" not in prompt


def test_azure_mcp_server_pr_review_help_skill_is_discoverable() -> None:
    root = Path(__file__).parents[1]
    skills = asyncio.run(
        FileSkillsSource(
            skill_paths=root / "skills" / "azure_mcp_server"
        ).get_skills(  # pyright: ignore[reportCallIssue]
            cast(SkillsSourceContext, None)  # pyright: ignore[reportCallIssue]
        )
    )
    instruction = (
        root / "agents" / "azure_mcp_server_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert [skill.frontmatter.name for skill in skills] == ["pr-review-help"]
    assert "`pr-review-help`" in instruction


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