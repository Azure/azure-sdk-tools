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
    assert "Follow the shared **PR Review Workflow**" in evidence
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

    assert "Follow the shared **PR Review Workflow**" in content
    assert "## PR Review Workflow" in instruction
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
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "check diagnosis" in content
    assert "**Check failures**" not in content
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    assert "Read the failed check's logs/output" in pr_review
    assert "then search the KB using the error and check context" in pr_review
    assert "Propose an evidence-backed fix or concrete next step" in pr_review
    assert "state any evidence gaps" in pr_review
    other_tools = instruction.split("## Other Tools\n", 1)[1].split("\n## ", 1)[0]
    assert "**Check failures**" not in other_tools
    assert "`get_comments`" in pr_review
    assert "Recovery depends on the cause" in pr_review
    assert "inspect the returned `html_url` with `web_fetch`" in pr_review


def test_chat_agent_exposes_github_actions_log_retrieval() -> None:
    from tools.github_mcp_tools import _GITHUB_READONLY_TOOLS

    assert "get_job_logs" in _GITHUB_READONLY_TOOLS
    assert "actions_get_job_logs" not in _GITHUB_READONLY_TOOLS


def test_stewardship_blocker_diagnosis_uses_shared_merge_summary_policy() -> None:
    content = build_skill_content(TenantID.API_STEWARDSHIP_BOARD_QA_BOT)
    root = Path(__file__).parents[1]
    instruction = (root / "agents" / "chat_agent" / "instruction.md").read_text(
        encoding="utf-8"
    )
    spec_prompt = (root / "prompts" / "tenants" / "api_spec_review.md").read_text(
        encoding="utf-8"
    )

    assert '"Next Steps to Merge"' not in content
    assert 'use "Next Steps to Merge" as a summary, not complete policy' in instruction
    assert "Verify current-head required checks" in instruction
    assert "conflicts and available branch policies" in instruction
    assert "Do not assume red optional checks block merge or omitted checks are nonblocking" in instruction
    assert "follow the shared **PR Review Workflow**" in spec_prompt
    assert "single source of truth for merge blockers" not in instruction
    assert "complete, authoritative list of merge blockers" not in spec_prompt


def test_shared_retrieval_respects_pr_workflow_dependencies() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    steps = (
        "**Read the PR and statuses.**",
        "**Checks stuck, untriggered, pending or awaiting approval:**",
        "**Merge blockers:**",
        "**Failed checks in blockers:**",
        "**Review existing feedback.**",
        "**Understand the changes.**",
        "**Review approval:**",
    )
    positions = [pr_review.index(f"{index}. {step}") for index, step in enumerate(steps, start=1)]
    assert positions == sorted(positions)
    assert "Follow relevant steps by priority, not as a checklist" in pr_review
    assert "continue only for unresolved needs and stop once answered" in pr_review
    assert "wait for both before later calls" in pr_review
    assert "ALL needed tools in a single parallel batch" in instruction
    assert "at most 8 tool calls per turn total" in instruction


def test_shared_retrieval_uses_original_batch_and_latency_constraints() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    constraints = instruction.split("## Constraints\n", 1)[1].split("\n## ", 1)[0]
    rules = {line.split(".", 1)[0]: line for line in constraints.splitlines() if line[:1].isdigit()}

    retrieval = instruction.split("## Retrieval (Evidence-First ReAct)\n", 1)[1].split("\n## ", 1)[0]
    assert "Open once, in parallel" in retrieval
    assert "start with `search_knowledge_base` and `wiki_search` together" in retrieval
    assert "current question or evidence gap, preserving the user's goal and constraints" in retrieval
    assert "Let context drive retrieval" not in retrieval
    assert "Retry only for an explicit gap" in retrieval
    assert "one targeted follow-up for a named missing fact" in retrieval
    assert "Follow evidence gaps" not in retrieval
    assert "Default retrieval is one parallel batch" in rules["2"]
    assert "Do not repeat a broad retrieval after the first batch" in rules["2"]
    assert "ALL needed tools in a single parallel batch" in rules["5"]
    assert "call `load_skill` ALONE first" in rules["5"]
    assert "Latency budget" in rules["6"]
    assert "Match depth to the user's need and current evidence" in instruction
    assert "defer available diagnosis to a follow-up" in instruction
    assert "summarize first, then let the user choose what to explore" not in instruction
    assert "non-PR" not in instruction
    assert "Batch independent calls; sequence dependent calls" not in constraints
    assert "A third turn is allowed only" in rules["6"]
    assert "at most 8 tool calls per turn total" in rules["1"]
    assert "wait for both before later calls" in pr_review
    assert "only to resolve remaining review questions" in pr_review


def test_shared_verdict_checks_take_precedence_over_answer_style() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    answer_rules = instruction.split("## Answer Rules\n", 1)[1].split("\n## ", 1)[0]

    assert "Check applicability before a verdict" in answer_rules
    assert "takes precedence over direct-answer style" in answer_rules
    assert "pair each case-specific verdict with the source passage and request facts" in answer_rules
    assert "check relevant exceptions before declaring an outcome required or forbidden" in answer_rules
    assert "A Wiki conclusion needs supporting source passages covering the same case" in answer_rules
    assert "violating a requirement does not establish an alternative classification" in answer_rules
    assert "Treat an unmatched prerequisite as unresolved, not an assumption" in answer_rules
    assert "give conditional guidance and ask for that fact" in answer_rules
    assert "Do not transfer rules between different lifecycles, branches, or SDKs" in answer_rules
    assert "keep the case-specific verdict conditional" in answer_rules
    assert "Lead with the strongest conclusion the evidence establishes" in answer_rules
    assert "If applicability is unresolved, state that condition first instead of assuming it" in answer_rules
    retrieval = instruction.split("## Retrieval (Evidence-First ReAct)\n", 1)[1].split("\n## ", 1)[0]
    assert "For a short question, include its exact wording in an opening query" in retrieval
    assert "Do not introduce unstated constraints into search queries" in retrieval


def test_shared_skill_selection_follows_new_evidence_without_user_handoff() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    skills = instruction.split("## Skills & Tenant Context\n", 1)[1].split("\n## ", 1)[0]

    assert "Reassess the active skill when evidence changes the problem to solve" in skills
    assert "load a better-matched skill and continue investigating" in skills
    assert "rather than referring the user elsewhere" in skills
    assert "load that tenant's skill and follow its guidance" in skills
    assert "Authoring tenant lock" in skills
    assert "use ONLY the preloaded `typespec-authoring` skill" in skills


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


def test_merge_and_workflow_guidance_is_in_pr_review_workflow() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    other_tools = instruction.split("## Other Tools\n", 1)[1].split("\n## ", 1)[0]
    for rule in ("**Merge blockers:**", "**Checks stuck, untriggered, pending or awaiting approval:**"):
        assert pr_review.count(rule) == 1
        assert rule not in other_tools
        assert instruction.count(rule) == 1
    assert "**Failed checks in blockers:**" in pr_review
    assert "Assess state/draft, labels, checks and review/signoff evidence" in pr_review
    assert "**Review approval:**" in pr_review
    assert "required checks/reviews and other merge conditions hold" in pr_review
    assert "verified requested reviewers/code owners from the PR or matching `CODEOWNERS` entries" in pr_review


def test_chat_agent_explains_workflow_action_required_evidence() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "**Checks stuck, untriggered, pending or awaiting approval:**" in instruction
    assert "Read the PR and `get_status` via `pull_request_read`" in instruction
    assert "wait for both before later calls" in instruction
    assert "Run details (`actions_get` for Actions) establish the workflow state" in instruction
    assert "For completed `action_required` runs or an unexplained approval report" in instruction
    assert "inspect the returned `html_url` with `web_fetch`" in instruction
    assert "the API may omit the reason" in instruction
    assert "A failed status alone does not establish a code defect" in instruction
    gate = instruction.split("**Checks stuck, untriggered, pending or awaiting approval:**", 1)[1].split("**Merge blockers:**", 1)[0]
    assert gate.index("For completed `action_required` runs") < gate.index("Recovery depends on the cause")
    assert "Recovery depends on the cause" in instruction
    assert "Use GitHub MCP for GitHub content" in instruction
    assert "**Only exception:** fetch a completed/action-required workflow run" in instruction
    assert "not unrelated GitHub pages" in instruction
    assert "Never call `web_fetch` on `github.com` URLs" not in instruction


def test_chat_agent_prioritizes_resolution_and_diagnoses_pending_pr_checks() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    gate = pr_review.split("**Checks stuck, untriggered, pending or awaiting approval:**", 1)[1].split("**Merge blockers:**", 1)[0]
    failed_blockers = pr_review.split("**Failed checks in blockers:**", 1)[1].split("**Review existing feedback.**", 1)[0]
    answer_rules = instruction.split("## Answer Rules\n", 1)[1].split("\n## ", 1)[0]

    assert "Help resolve the user's problem with evidence-backed diagnosis and concrete next steps" in answer_rules
    assert "not just an issue list or a handoff" in answer_rules
    assert "If an SDK lifecycle problem cannot be resolved here" in answer_rules
    assert "consider the Azure SDK Tools Agent as a fallback when it can help" in answer_rules
    assert "explain the specific action and remaining gap" in answer_rules
    assert "always recommend the Azure SDK Tools Agent" not in answer_rules
    assert "Pending checks may instead be queued/running or untriggered" in gate
    assert "Recovery depends on the cause" in gate
    assert "Run details (`actions_get` for Actions) establish the workflow state" in gate
    assert "KB guidance" not in gate
    assert "address access-related stuck or awaiting-approval checks" in gate
    assert "reopening/retriggering addresses untriggered checks when repository guidance supports it" in gate
    diagnosis_steps = (
        "Read the failed check's logs/output",
        "then search the KB using the error and check context",
        "Propose an evidence-backed fix or concrete next step",
    )
    positions = [failed_blockers.index(step) for step in diagnosis_steps]
    assert positions == sorted(positions)
    assert "`get_job_logs` with `return_content=true` for Actions" in failed_blockers
    assert "not just its status or a download URL" in failed_blockers
    assert "to identify the specific error" in failed_blockers
    assert "Ask for logs only after tool retrieval fails, stating the limitation" in failed_blockers


def test_workflow_permission_guidance_is_shared_across_tenants() -> None:
    root = Path(__file__).parents[1]
    instruction = (root / "agents" / "chat_agent" / "instruction.md").read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    gate = pr_review.split("**Checks stuck, untriggered, pending or awaiting approval:**", 1)[1].split("**Merge blockers:**", 1)[0]
    assert "**Workflow permissions**" not in pr_review
    assert "self-service [access requests](https://aka.ms/azsdk/access)" in gate
    assert (
        "[Public Azure membership](https://docs.github.com/en/account-and-profile/how-tos/"
        "organization-membership/publicizing-or-hiding-organization-membership)"
    ) in gate
    assert "continue only for unresolved needs and stop once answered" in pr_review
    assert "if the user's write access is missing or unknown" not in pr_review
    assert "verify repository/CI-trigger permissions" not in pr_review
    assert "For approval gates, ask a maintainer to approve the run instead" not in pr_review
    prompt = (root / "prompts" / "tenants" / "api_spec_review.md").read_text(encoding="utf-8")
    assert "When a pipeline appears stuck or a check is not triggered" not in prompt


def test_tenant_skills_do_not_route_permission_help_to_onboarding() -> None:
    for skill in create_tenant_skills("azure-sdk-chat-agent"):
        if skill.frontmatter.name in ("api-spec-review", "sdk-onboarding"):
            description = skill.frontmatter.description.lower()
            assert "repository access" not in description
            assert "permission" not in description
    for tenant_id in (
        TenantID.API_SPEC_REVIEW_BOT,
        TenantID.API_STEWARDSHIP_BOARD_QA_BOT,
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


def test_pr_review_response_matches_customer_question_with_useful_context() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    answer_rules = instruction.split("## Answer Rules\n", 1)[1].split("\n## ", 1)[0]
    normalized = " ".join(pr_review.split())
    assert "The customer needs the cause and remedy, not the investigation trail" in normalized
    assert "naming a red check or ruling out approval is only triage" in normalized
    assert "Continue to the specific error and supported remedy, or an evidenced retrieval limit" in normalized
    assert "Passed checks, routine run metadata and summary-gate mechanics belong only when requested or needed to act" in normalized
    assert "State the conclusion once; do not repeat it or the same links in a recap" in answer_rules
    assert "Every actionable step must include a clickable URL inline" in answer_rules
    assert "using each destination once" not in answer_rules
    assert "Bullet points over paragraphs. One idea per bullet." in answer_rules
    assert "Use bullets only when they make necessary findings or actions easier to scan" not in answer_rules
    assert "Keep possibility, rejection reason, and recommendation distinct" in answer_rules
    assert "Cover the adjacent variant" in answer_rules
    assert "not every fact retrieved" not in answer_rules
    assert "answer all three separately" in answer_rules
    assert "Completeness precedes brevity" in answer_rules
    assert "When the answer depends on an exact rule" in answer_rules
    assert "When the evidence states an exact rule" not in answer_rules
    assert "Help move the PR forward from its current state, not produce a standard review report" in normalized
    assert "when needed for the user's request and preflight findings" in normalized
    assert "stop once answered" in normalized
    assert "When approval is next, mention up to 3 verified requested reviewers/code owners" in normalized
    assert "Never invent handles" in pr_review


def test_pr_preflight_uses_status_and_labels_to_select_next_steps() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    preflight = pr_review.split("1. **Read the PR and statuses.**", 1)[1].split("\n2.", 1)[0]
    changes = pr_review.split("6. **Understand the changes.**", 1)[1].split("\n7.", 1)[0]

    assert "Assess state/draft, labels, checks and review/signoff evidence" in preflight
    assert "to choose relevant next steps and depth" in preflight
    assert "Interpret labels using repository guidance" in preflight
    assert "prioritize unresolved work while honoring the user's request" in preflight
    assert "Use returned run links" in preflight
    assert "investigate the reported check before expanding the check inventory" in preflight
    assert "when needed for the user's request and preflight findings" in changes
    assert "For verified signoff" not in pr_review
    assert "neither verified signoff nor blockers" not in pr_review


def test_pr_review_triages_failures_and_preserves_explicit_code_review() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    assert "Read the failed check's logs/output" in pr_review
    assert "then search the KB using the error and check context" in pr_review
    assert "Propose an evidence-backed fix or concrete next step" in pr_review
    assert "Read code only when needed for diagnosis" in pr_review
    preflight = pr_review.split("1. **Read the PR and statuses.**", 1)[1].split("\n2.", 1)[0]
    assert "Assess state/draft, labels, checks and review/signoff evidence" in preflight
    assert "to choose relevant next steps and depth" in preflight
    assert "honoring the user's request" in preflight
    assert "Draft state alone does not require a deep code review; still diagnose relevant check failures" not in pr_review
    assert "Explicit fresh code/design review proceeds even on draft/red-check PRs" in pr_review
    assert "when needed for the user's request and preflight findings" in pr_review
    assert "Read relevant `get_reviews`, `get_review_comments` and `get_comments`" in pr_review
    assert "follow the relevant **PR Review Workflow** steps; skip irrelevant steps" in instruction
    assert "read the PR, its failing check runs, and their logs before answering" not in instruction


def test_shared_reference_rules_append_source_links() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    formatting = instruction.split("## Formatting & References\n", 1)[1].split("\n## ", 1)[0]
    assert "Append a References section at the end with links from knowledge base or web search results" in formatting
    assert "Cite sources inline with short titles" not in formatting
    assert "without repeating inline links" not in formatting
    assert "**References**" in formatting


def test_pr_review_assesses_purpose_and_changed_file_evidence() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    normalized = " ".join(pr_review.split())
    assert "Read the description and relevant diffs" in normalized
    assert "Start `get_files` at `perPage=1`; page only as needed" in normalized
    assert "Assess changes alongside checks and feedback; state coverage gaps" in normalized


def test_latest_copilot_overall_review_selection_requires_paginated_evidence() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    normalized = " ".join(pr_review.split())
    assert "For `copilot-pull-request-reviewer[bot]`, select the latest overall review" in normalized
    assert "by `submitted_at` across necessary pages" in normalized
    assert "order/one page is insufficient" in normalized
    assert 'Qualify incomplete retrieval as "latest retrieved."' in normalized
    assert "Keep human reviews and unresolved inline findings regardless of age" in normalized


def test_pr_review_reads_existing_reviews_and_compares_inline_feedback_with_current_diff() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    feedback = pr_review.split("**Review existing feedback.**", 1)[1].split("**Understand the changes.**", 1)[0]
    normalized = " ".join(feedback.split())
    assert "Read relevant `get_reviews`, `get_review_comments` and `get_comments`" in normalized
    assert "compare replies/current diffs to identify unresolved findings" in normalized
    assert "select the latest overall review" in normalized


def test_chat_agent_cross_checks_spec_merge_summary_with_live_evidence() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "**Merge blockers:**" in instruction
    assert '"Next Steps to Merge" as a summary, not complete policy' in instruction
    assert "`azure-rest-api-specs` / `azure-rest-api-specs-pr`" in instruction
    assert "current-head required checks" in instruction
    assert "Do not assume red optional checks block merge or omitted checks are nonblocking" in instruction
    assert "Separate execution gates, merge checks and reviews" in instruction
    assert "qualify missing evidence" in instruction
    assert "single source of truth for merge blockers" not in instruction


def test_api_spec_review_tenant_delegates_workflow_diagnosis_to_shared_guidance() -> None:
    prompt = (
        Path(__file__).parents[1] / "prompts" / "tenants" / "api_spec_review.md"
    ).read_text(encoding="utf-8")

    assert "follow the shared **PR Review Workflow**" in prompt
    assert "complete, authoritative list of merge blockers" not in prompt
    assert "it is NOT a blocker" not in prompt


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