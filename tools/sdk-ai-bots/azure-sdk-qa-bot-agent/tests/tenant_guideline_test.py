"""Offline tests for language-specific tenant guideline loading."""

import pytest

from config.tenant_config import (
    TenantID,
    get_tenant_config,
    load_tenant_qa_guideline,
)


def test_dotnet_tenant_loads_dedicated_guideline():
    config = get_tenant_config(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert config is not None
    assert config.qa_guideline_file == "tenants/language_dotnet.md"

    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert "# Tenant: Azure .NET SDK Assistant" in guideline
    assert "{{tenant_id}}" not in guideline
    assert "ApiCompat" in guideline
    assert "Verify that generation consumed any customization changes." in guideline
    assert "mgmt-review-comment-resolution/SKILL.md" in guideline
    assert "mitigate-breaking-changes/SKILL.md" in guideline
    assert "Skills do not replace reviewer approval" in guideline


def test_dotnet_guideline_links_skills_to_the_relevant_problem():
    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert guideline.index("1. Identify the problem:") < guideline.index(
        "2. Explain the fix:"
    ) < guideline.index("3. Recommend the next step:")
    assert guideline.index(
        "### Shared Reviewer Routing and Release Gates\n"
    ) < guideline.index("### Management-Plane SDK PR Review\n")
    management_review = guideline.split(
        "### Management-Plane SDK PR Review\n", 1
    )[1].split("### Generation and Spec Review Fixes\n", 1)[0]
    assert management_review.index(
        "#### 2. Read existing feedback before diagnosing independently"
    ) < management_review.index(
        "#### 3. Match the blocker to the fix and next action"
    ) < management_review.index(
        "#### 4. Recommend the relevant skill"
    ) < management_review.index("#### 5. Verify the fix before requesting re-review")
    assert "type/property renames or supported property-type changes" in management_review
    assert "mgmt-review-comment-resolution/SKILL.md" in management_review
    assert "preserving released contracts" in management_review
    assert "mitigate-breaking-changes/SKILL.md" in management_review
    assert guideline.count("mgmt-review-comment-resolution/SKILL.md") == 1
    assert guideline.count("mitigate-breaking-changes/SKILL.md") == 1
    assert "### Repository Skills for Service Teams" not in guideline
    assert "reply on the original review thread" not in management_review


@pytest.mark.parametrize(
    "guidance",
    [
        "submitted reviews (`get_reviews`)",
        "inline review threads (`get_review_comments`)",
        "PR conversation comments (`get_comments`)",
        "Prioritize current human reviewer decisions",
        "If results are truncated, paginated, or unavailable",
        "link directly to that comment",
        "Do not invent a mandatory bot-review-before-human-review gate",
        "manual intervention or a spec-owner decision",
        "Verify separate failures",
        "Verify that its documented scope applies",
        "Do not recommend either skill for approval-only requests",
        "regenerate the SDK using the spec commit containing the fixes",
        "so reviewers can verify the SDK fixes",
    ],
)
def test_dotnet_management_review_preserves_evidence_and_approval_guards(guidance):
    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    management_review = guideline.split(
        "### Management-Plane SDK PR Review\n", 1
    )[1].split("### Generation and Spec Review Fixes\n", 1)[0]
    assert guidance in management_review


@pytest.mark.parametrize(
    "guidance",
    [
        "Skills do not replace reviewer approval",
        "not generated files",
        "LocalSpecRepo",
        "explicit authorized owner approval",
        "without substituting suppressions or deleting unrelated baselines",
        "does not prove wire/runtime compatibility",
        "without explicit approval from its authorized maintainer",
    ],
)
def test_dotnet_guideline_retains_shared_safeguards(guidance):
    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert guidance in guideline


@pytest.mark.parametrize(
    ("tenant_id", "guideline_file"),
    [
        (TenantID.PYTHON_CHANNEL_QA_BOT, "tenants/language_python.md"),
        (TenantID.GOLANG_CHANNEL_QA_BOT, "tenants/language_channel.md"),
        (TenantID.JAVA_CHANNEL_QA_BOT, "tenants/language_channel.md"),
        (TenantID.JAVASCRIPT_CHANNEL_QA_BOT, "tenants/language_channel.md"),
    ],
)
def test_other_language_guidelines_are_unchanged(tenant_id, guideline_file):
    config = get_tenant_config(tenant_id)
    assert config is not None
    assert config.qa_guideline_file == guideline_file
    assert load_tenant_qa_guideline(tenant_id)
