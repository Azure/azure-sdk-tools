"""Tests for tenant skill and knowledge-source configuration."""

import asyncio
import json
from pathlib import Path

import pytest
from agent_framework import FileSkillsSource

from config import tenant_config
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
    SRC_STATIC_CPEX_DOCS,
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
    get_skill_to_tenant_map,
)

_AGENT_ROOT = Path(__file__).parents[1]


def _read_pr_review_skill() -> str:
    return (
        _AGENT_ROOT / "skills" / "chat_agent" / "pr-review" / "SKILL.md"
    ).read_text(encoding="utf-8")


@pytest.mark.parametrize(
    "tenant_id",
    [TenantID.API_STEWARDSHIP_BOARD_QA_BOT, TenantID.API_SPEC_REVIEW_BOT],
)
@pytest.mark.parametrize("service_type", [None, "data-plane"])
def test_review_engineering_docs_keep_source_scope_without_title_restrictions(
    tenant_id: TenantID,
    service_type: str | None,
) -> None:
    from tools.knowledge_tools import _resolve_source_filters

    config = get_tenant_config(tenant_id)
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
    assert get_knowledge_source(SRC_AZURE_SDK_DOCS_ENG).get_link(
        "docs#develop#namespace-review.md"
    ) == (
        "https://eng.ms/docs/products/azure-developer-experience/develop/namespace-review"
    )


def test_api_spec_review_preserves_other_source_filters() -> None:
    config = get_tenant_config(TenantID.API_SPEC_REVIEW_BOT)

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


def test_stewardship_tenant_uses_shared_agent_and_maintained_sources() -> None:
    config = get_tenant_config(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert config is not None
    assert config.agent.name == "azure-sdk-chat-agent"
    assert config.agent.name_config_key == "AI_FOUNDRY_AGENT_NAME"
    assert config.display_name == "API Stewardship Board - General"
    assert config.skill_name == "api-stewardship-board"
    assert config.qa_guideline_files == ["tenants/api_stewardship_board.md"]
    assert config.enable_routing
    assert not config.enable_wiki_cross_document_pages
    assert (
        "Internal API modeling, documentation/SDK exposure, and service-side access boundaries"
    ) in config.topics
    assert (
        "API deprecation, retirement, affected versions, and customer-notification requirements"
    ) in config.topics
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
    assert SRC_AZURE_SDK_DOCS_ENG not in config.source_filter


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
    assert skill.frontmatter.description == (
        "Data-plane API design and API Stewardship Board review guidance."
    )
    assert "Give evidence-backed advice, not Board decisions" in content
    assert "Use `typespec` for detailed authoring" not in content
    assert "`api-spec-review` for validator-specific diagnosis" not in content
    assert "`sdk-onboarding` for release-plan lifecycle" not in content
    assert "repair infrastructure or prescribe resource-specific permission changes" in content
    assert "Use maintained Board guidance" in content
    assert "async reviews, materials, lead times, Office Hours and support" in content


def test_stewardship_specialist_routes_are_injected_from_tenant_config() -> None:
    from services.chat_service import ChatService

    config = get_tenant_config(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    assert config is not None
    message = ChatService()._build_tenant_system_message(
        TenantID.API_STEWARDSHIP_BOARD_QA_BOT
    )
    scope, skill_content = message.split(
        "\n[skill] name=api-stewardship-board ", 1
    )
    assert "[tenant_scope]" in scope
    for exclusion in (
        "Detailed TypeSpec authoring or emitter failures — use the TypeSpec skill",
        "Detailed spec PR check investigation — use the API Spec Review skill",
        "Breaking-change exception requests — use the API Spec Review skill for the applicable authorized-reviewer process",
        "Release-plan lifecycle — use the SDK Onboarding skill",
    ):
        assert exclusion in config.exclusions
        assert f"- {exclusion}" in scope
        assert exclusion not in skill_content


def test_stewardship_guideline_follows_a_request_to_answer_workflow() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    guideline = content.split("[skill_guideline]\n", 1)[1]
    assert [line for line in guideline.splitlines() if line.startswith("## ")] == [
        "## Scope",
        "## 1. Understand the request",
        "## 2. Gather evidence",
        "## 3. Apply the relevant policy",
        "## 4. Recommend the next action",
    ]
    context = guideline.split("## 1. Understand the request\n", 1)[1].split("\n## ", 1)[0]
    evidence = guideline.split("## 2. Gather evidence\n", 1)[1].split("\n## ", 1)[0]
    policy = guideline.split("## 3. Apply the relevant policy\n", 1)[1].split("\n## ", 1)[0]
    answer = guideline.split("## 4. Recommend the next action\n", 1)[1]

    assert "latest confirmed thread resolution" in context
    assert "distinguish proposals from decisions and preserve withdrawals" in context
    assert "Load `pr-review`" in evidence
    assert "Retrieve maintained guidance for the unresolved question" in evidence
    assert "As applicable:" in policy
    assert (
        "REST signoff, breaking-change exceptions, SDK/API review and package-name approval separate"
    ) in policy
    assert "live `.github/protected-labels.yml`" in policy
    assert "SDK/documentation visibility from service authorization" in policy
    assert "preview classification from policy and availability, not AFEC alone" in policy
    assert "retirement, verify affected versions, applicable notice requirements and approval evidence" in policy
    assert "do not generalize prior approvals" in policy
    assert "next useful action from current evidence, rather than restart completed review work" in answer
    assert "Use maintained Board guidance" in answer
    assert "Do not send users back to this channel to repeat their question" in answer


def test_stewardship_pr_guidance_delegates_shared_rules_and_preserves_board_policy() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "Load `pr-review`" in content
    assert "verify blockers against live current-head state" in content
    assert "omit review checklists and repeated conclusions or links" in content
    assert "load `pr-review` before other tools" in instruction
    assert _read_pr_review_skill().startswith("---\nname: pr-review\n")
    assert "## Answer Style" not in content
    assert "up to 3 short bullets" not in content
    assert "Brevity limits the answer, not the investigation" not in content
    assert "All your tools are read-only" in instruction
    assert "Design findings, Board signoff and merge readiness are distinct signals" not in content
    assert "next useful action from current evidence, rather than restart completed review work" in content
    assert "guidance for the unresolved question identified from the PR or request" in content
    assert "live `.github/protected-labels.yml`" in content
    assert "approval does not transfer to another PR" in content
    assert "published-version edits from new versions, preview from GA" in content
    assert "REST from SDK compatibility" in content
    assert "Do not send users back to this channel to repeat their question" in content
    guideline = content.split("[skill_guideline]", 1)[1]
    assert len(guideline.split()) <= 300


def test_stewardship_check_support_uses_shared_failure_diagnosis() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert "check diagnosis" in content
    assert "**Check failures**" not in content
    pr_review = _read_pr_review_skill()
    assert "Read the failed check's logs/output" in pr_review
    assert "then search the KB using the error and check context" in pr_review
    assert "Propose an evidence-backed fix or concrete next step" in pr_review
    assert "state any evidence gaps" in pr_review
    assert "`get_comments`" in pr_review


def test_stewardship_blocker_diagnosis_uses_shared_merge_summary_policy() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    pr_review = _read_pr_review_skill()

    assert '"Next Steps to Merge"' not in content
    assert "Load `pr-review`" in content
    assert '"Next Steps to Merge"' in pr_review
    assert "current-head evidence" in pr_review
    assert "failed or missing required checks are blockers" in pr_review
    assert "optional failures are informational" in pr_review
    assert "unknown requirement status remain evidence gaps" in pr_review


def test_stewardship_skill_requires_explicit_service_type_for_scoped_search() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)

    assert 'service_type="data-plane"' in content
    assert 'service_type="management-plane"' in content
    assert "including the initial search" in content
    assert "leave it unset when ambiguous or general" in content
    assert "`wiki_search` has no service-type filter" in content


def test_stewardship_scope_keeps_arm_referral_separate_from_search_details() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    scope = content.split("## Scope\n", 1)[1].split("\n## ", 1)[0]

    assert len(scope.split()) <= 75
    assert "data-plane API design" in scope
    assert "Direct ARM/management-plane reviews" in scope
    assert "search_knowledge_base" not in scope


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
    assert "ARM Office Hours" not in skill.frontmatter.description


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


def test_chat_agent_pr_review_skill_is_discoverable_and_registered(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    skills = asyncio.run(
        FileSkillsSource(_AGENT_ROOT / "skills" / "chat_agent").get_skills()
    )
    assert [skill.frontmatter.name for skill in skills] == ["pr-review"]
    assert "pr-review" not in get_skill_to_tenant_map()

    init = (
        _AGENT_ROOT / "agents" / "chat_agent" / "init.py"
    ).read_text(encoding="utf-8")
    assert "FileSkillsSource(skills_dir).get_skills()" in init
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
        FileSkillsSource(root / "skills" / "azure_mcp_server").get_skills()
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