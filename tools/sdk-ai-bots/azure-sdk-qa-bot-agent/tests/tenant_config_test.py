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
    assert "Data-plane API design and API Stewardship Board review guidance" in skill.frontmatter.description
    assert "REST/SDK compatibility" in skill.frontmatter.description
    assert "long-running operations" in skill.frontmatter.description
    assert "Advisory only" not in skill.frontmatter.description
    assert "Use typespec" not in skill.frontmatter.description
    assert "Give evidence-backed advice, not Board decisions" in content
    assert "Use `typespec` for detailed authoring" in content
    assert "`api-spec-review` for check investigation" in content
    assert "`sdk-onboarding` for access or release-plan lifecycle" in content
    assert "repair infrastructure or prescribe permission changes" in content
    assert "Use maintained Board guidance" in content
    assert "async reviews, materials, lead times, Office Hours and support" in content


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
    assert "Prefer short bullets with one idea each" in instruction
    assert "Use short descriptive link titles" in instruction
    assert "do not repeat the recommendation in a closing next-step section" in instruction
    assert "All your tools are read-only" in instruction
    assert "Separate design findings, Board signoff and merge readiness" in content
    assert "Green checks and signoff do not prove design correctness" in content
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
    assert "Start with the reported check" in instruction
    assert "inspect linked error evidence before asking" in instruction
    assert "Passing checks do not establish that the user's problem is resolved" in instruction
    assert "Do not invent an automation diagnosis" in instruction
    assert "verified fix or specific support handoff" in instruction
    assert "Read linked comments and relevant discussion (`get_comments`)" in instruction
    assert "This may signal a permission or maintainer-approval gate" in instruction
    assert "the API status alone does not establish the cause" in instruction
    assert "your next call must be `web_fetch` on the run's returned `html_url`" in instruction


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
    assert 'use "Next Steps to Merge" from `pull_request_read` as a summary' in instruction
    assert "verify current-head required checks" in instruction
    assert "conflicts and available branch-policy evidence independently" in instruction
    assert "An unlisted check is not necessarily nonblocking" in instruction
    assert "a red optional check is not necessarily blocking" in instruction
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
        "**Action-required workflows:**",
        "**Validation failures:**",
        "**Merge blockers:**",
        "**Review existing feedback.**",
        "**Understand the changes.**",
        "**Review approval:**",
    )
    positions = [pr_review.index(f"{index}. {step}") for index, step in enumerate(steps, start=1)]
    assert positions == sorted(positions)
    assert "Use these steps in priority order; skip irrelevant steps" in pr_review
    assert "do not batch later retrieval with this preflight" in pr_review
    assert "**Batch independent calls, not dependent steps.**" in instruction
    assert "Wait for a result before calls that depend on its evidence" in instruction
    assert "do not skip necessary investigation to meet the latency target" in instruction
    assert "**ALL needed tools in a single parallel batch**" not in instruction
    assert "A third turn is allowed only" not in instruction
    assert "at most 8 tool calls per turn total" in instruction
    assert "GitHub MCP is read-only" in pr_review


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
    scope = content.split("## Scope\n", 1)[1].split("## Board Review Guidance", 1)[0]

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


def test_merge_and_workflow_guidance_is_in_pr_review_workflow() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    other_tools = instruction.split("## Other Tools\n", 1)[1].split("\n## ", 1)[0]
    for rule in ("**Merge blockers:**", "**Action-required workflows:**"):
        assert pr_review.count(rule) == 1
        assert rule not in other_tools
        assert instruction.count(rule) == 1
    assert "**Validation failures:**" in pr_review
    assert "Draft state alone does not require a deep code review" in pr_review
    assert "**Review approval:**" in pr_review
    assert "required checks, reviews and merge conditions are satisfied" in pr_review
    assert "GitHub MCP is read-only" in pr_review
    assert "Only name people you actually found" in pr_review


def test_chat_agent_fetches_explicit_workflow_action_required_reason() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "**Action-required workflows:**" in instruction
    assert "read the PR and `get_status` first" in instruction
    assert "Wait for both results before later calls" in instruction
    assert "do not batch later retrieval with this preflight" in instruction
    assert "If a failing status links to an Actions run" in instruction
    assert 'inspect one linked run with `actions_get(method="get_workflow_run")`' in instruction
    assert "before any changed-file, diff, review, comment, or overflow-retry calls" in instruction
    assert "reserve calls for this diagnosis" in instruction
    assert 'If `status="completed"` and `conclusion="action_required"`' in instruction
    assert "your next call must be `web_fetch` on the run's returned `html_url`" in instruction
    assert "before inspecting more runs" in instruction
    assert "This may signal a permission or maintainer-approval gate" in instruction
    assert "the API status alone does not establish the cause" in instruction
    assert "Use GitHub MCP for GitHub content" in instruction
    assert "**Only exception:** fetch a completed/action-required workflow run" in instruction
    assert "not unrelated GitHub pages" in instruction
    assert "Never call `web_fetch` on `github.com` URLs" not in instruction


def test_workflow_permission_guidance_is_shared_across_tenants() -> None:
    root = Path(__file__).parents[1]
    instruction = (root / "agents" / "chat_agent" / "instruction.md").read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    gate = pr_review.split("**Action-required workflows:**", 1)[1].split("**Validation failures:**", 1)[0]
    assert "**Workflow permissions**" not in pr_review
    assert "For permission issues or workflow approval gates in Azure SDK repositories" in gate
    assert "direct users to [request access](https://aka.ms/azsdk/access)" in gate
    assert "set their Azure organization membership to [Public]" in gate
    assert (
        "[Public](https://docs.github.com/en/account-and-profile/how-tos/"
        "organization-membership/publicizing-or-hiding-organization-membership)"
    ) in gate
    assert "give the unblocking action and stop" in gate
    assert "unless code/design review or another independent facet was explicitly requested" in gate
    assert "if the user's write access is missing or unknown" not in pr_review
    assert "verify repository/CI-trigger permissions" not in pr_review
    assert "For approval gates, ask a maintainer to approve the run instead" not in pr_review
    prompt = (root / "prompts" / "tenants" / "api_spec_review.md").read_text(encoding="utf-8")
    assert "When a pipeline appears stuck or a check is not triggered" not in prompt


def test_pr_review_response_matches_customer_question_with_useful_context() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    normalized = " ".join(pr_review.split())
    assert "Tailor the answer to the question" in normalized
    assert "Prioritize actionable findings and material review gaps" in normalized
    assert "rather than listing everything investigated" in normalized
    assert "For general PR help, diagnose blockers rather than automatically reviewing code" in normalized
    assert "stop when the request is answered" in normalized
    assert "When review approval is the next action, identify requested reviewers / code owners" in normalized
    assert "do not invent code fixes for a workflow approval gate" in normalized
    assert "Only name people you actually found" in pr_review
    assert "GitHub MCP is read-only" in pr_review


def test_pr_review_triages_failures_and_preserves_explicit_code_review() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    assert "Diagnose actual failures from logs or check output" in pr_review
    assert "red status alone does not establish a code defect" in pr_review
    assert "Read only the code needed to resolve the failure, not a broad code review" in pr_review
    assert "Draft state alone does not require a deep code review; still diagnose relevant check failures" in pr_review
    assert "For an explicit code/API-design review, continue even if the PR is draft or checks are red" in pr_review
    assert "When code/design review is requested, or no blocker already answers a general review request" in pr_review
    assert "When relevant to the question, read overall reviews" in pr_review
    assert "follow the relevant **PR Review Workflow** steps; skip irrelevant steps" in instruction
    assert "read the PR, its failing check runs, and their logs before answering" not in instruction


def test_chat_agent_cites_inline_without_requiring_duplicate_references() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    formatting = instruction.split("## Formatting & References\n", 1)[1].split("\n## ", 1)[0]
    assert "Cite sources inline" in formatting
    assert "add other references only when useful, without repeating links" in formatting
    assert "Append a References section" not in formatting
    assert "**References**" not in formatting


def test_pr_review_assesses_purpose_and_changed_file_evidence() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    normalized = " ".join(pr_review.split())
    assert "read the PR description and relevant diffs before forming recommendations" in normalized
    assert "Start `get_files` with `perPage=1`" in normalized
    assert "fetch additional pages only as needed" in normalized
    assert "Assess changes alongside checks and existing feedback" in normalized
    assert "state review coverage gaps" in normalized


def test_latest_copilot_overall_review_selection_requires_paginated_evidence() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    normalized = " ".join(pr_review.split())
    assert "For `copilot-pull-request-reviewer[bot]`, consider only the latest overall review" in normalized
    assert "by `submitted_at` after checking the necessary native pages" in normalized
    assert "response order or one page does not establish the latest review" in normalized
    assert "If retrieval is incomplete, qualify it as the latest retrieved review" in normalized
    assert "Keep human reviews and unresolved inline findings in scope regardless of age" in normalized


def test_pr_review_reads_existing_reviews_and_compares_inline_feedback_with_current_diff() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")
    pr_review = instruction.split("## PR Review Workflow\n", 1)[1].split("\n## ", 1)[0]
    feedback = pr_review.split("**Review existing feedback.**", 1)[1].split("**Understand the changes.**", 1)[0]
    normalized = " ".join(feedback.split())
    assert "read overall reviews (`get_reviews`) and relevant inline threads (`get_review_comments`)" in normalized
    assert "Compare findings with replies and the current diff" in normalized
    assert "distinguish outstanding feedback from already-addressed points" in normalized
    assert "consider only the latest overall review" in normalized


def test_chat_agent_cross_checks_spec_merge_summary_with_live_evidence() -> None:
    instruction = (
        Path(__file__).parents[1] / "agents" / "chat_agent" / "instruction.md"
    ).read_text(encoding="utf-8")

    assert "**Merge blockers:**" in instruction
    assert '"Next Steps to Merge" from `pull_request_read` as a summary' in instruction
    assert "`azure-rest-api-specs` / `azure-rest-api-specs-pr`" in instruction
    assert "current-head required checks" in instruction
    assert "An unlisted check is not necessarily nonblocking" in instruction
    assert "a red optional check is not necessarily blocking" in instruction
    assert "Distinguish workflow execution gates" in instruction
    assert "State uncertainty when requirements or evidence are unclear" in instruction
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