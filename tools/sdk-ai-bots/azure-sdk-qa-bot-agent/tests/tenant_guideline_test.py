"""Offline tests for shared and language-specific tenant guideline loading."""

import pytest

from config import tenant_config
from config.tenant_config import (
    TenantConfig,
    TenantID,
    get_tenant_config,
    load_tenant_qa_guideline,
)


def test_dotnet_tenant_loads_shared_guideline_before_supplement():
    config = get_tenant_config(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert config is not None
    assert config.qa_guideline_files == [
        "tenants/language_channel.md",
        "tenants/language_dotnet.md",
    ]

    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert guideline.count("# Tenant: Azure SDK Language Channel Assistant") == 1
    assert guideline.count("# .NET-Specific Answer Guidelines") == 1
    assert guideline.index("### SDK Lifecycle") < guideline.index(
        "# .NET-Specific Answer Guidelines"
    )
    assert "{{tenant_id}}" not in guideline
    assert f"operating in the {TenantID.DOTNET_CHANNEL_QA_BOT.value}" in guideline
    assert "ApiCompat" in guideline


def test_dotnet_guideline_links_skills_to_the_relevant_problem():
    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    management_review = guideline.split(
        "### Management-Plane SDK PR Review\n", 1
    )[1].split("### Generation and Spec Review Fixes\n", 1)[0]
    assert management_review.index(
        "**API naming/review feedback:**"
    ) < management_review.index(
        "#### Relevant Management SDK Skills"
    ) < management_review.index("#### Verify the SDK Fix Before Re-review")
    assert "type/property renames or supported property-type changes" in management_review
    assert "mgmt-review-comment-resolution/SKILL.md" in management_review
    assert "preserving released contracts" in management_review
    assert "mitigate-breaking-changes/SKILL.md" in management_review
    assert guideline.count("mgmt-review-comment-resolution/SKILL.md") == 1
    assert guideline.count("mitigate-breaking-changes/SKILL.md") == 1
    assert "Verify that its documented scope applies" in management_review
    assert "Do not recommend either skill for approval-only requests" in management_review
    assert "if the spec fix is not merged yet and the SDK does not yet reflect the fixes" in management_review
    assert "Remind the author to regenerate the SDK from the spec commit containing those fixes" in management_review
    assert "so reviewers can verify the updated output" in management_review
    assert "reply on the original review thread" not in management_review


@pytest.mark.parametrize(
    "routing",
    [
        "For management-plane GA API approval, contact **Arthur Ma (`arthurma`)**.",
        (
            "For data-plane GA API approval, contact **Michael Nash (`micnash`)** "
            "or **Josh Love (`jolov`)**."
        ),
    ],
)
def test_dotnet_guideline_routes_ga_approval_by_package_plane(routing):
    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    release_guidance = guideline.split(
        "### .NET Review and Release Requirements\n", 1
    )[1].split("### Management-Plane SDK PR Review\n", 1)[0]
    assert routing in release_guidance
    assert "`Azure.ResourceManager.*` indicates management-plane" in release_guidance
    assert "do not assume every other package is data-plane" in release_guidance
    assert "ask the author before selecting a contact" in release_guidance
    assert "route each to the contact for its plane" in release_guidance
    assert "package/version, APIView revision link, and failed build link" in release_guidance
    assert "not unrelated publishing failures" in release_guidance
    assert "do not claim approval was requested or granted without evidence" in release_guidance


@pytest.mark.parametrize(
    "heading",
    [
        "### SDK Lifecycle",
        "### Context to Identify",
    ],
)
def test_shared_sections_are_not_duplicated_in_dotnet_supplement(heading):
    common = (
        tenant_config._PROMPTS_DIR / "tenants/language_channel.md"
    ).read_text(encoding="utf-8")
    supplement = (
        tenant_config._PROMPTS_DIR / "tenants/language_dotnet.md"
    ).read_text(encoding="utf-8")
    assert heading in common
    assert heading not in supplement
    guideline = load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert guideline.count(heading) == 1


@pytest.mark.parametrize(
    "guidance",
    [
        "management API review, data-plane review",
        ".NET APIView/owner approvals",
        "LocalSpecRepo",
        "dotnet build /t:GenerateCode",
        "@azure-typespec/http-client-csharp` (data plane)",
        "@azure-typespec/http-client-csharp-mgmt` (management)",
        "api-version` in `tspconfig.yaml",
        "explicit authorized owner approval",
        "without substituting suppressions or deleting unrelated baselines",
        "does not prove wire/runtime compatibility",
        "recording/API-version mismatches",
        (
            "After changing .NET generation inputs or customizations, rerun the "
            "applicable generation workflow and inspect the output before diagnosing "
            "why a change did not take effect."
        ),
    ],
)
def test_dotnet_supplement_retains_specific_guidance(guidance):
    supplement = (
        tenant_config._PROMPTS_DIR / "tenants/language_dotnet.md"
    ).read_text(encoding="utf-8")
    assert guidance in supplement
    assert guidance in load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)


@pytest.mark.parametrize(
    ("tenant_id", "supplement_file"),
    [
        (TenantID.PYTHON_CHANNEL_QA_BOT, "tenants/language_python.md"),
        (TenantID.DOTNET_CHANNEL_QA_BOT, "tenants/language_dotnet.md"),
        (TenantID.GOLANG_CHANNEL_QA_BOT, ""),
        (TenantID.JAVA_CHANNEL_QA_BOT, ""),
        (TenantID.JAVASCRIPT_CHANNEL_QA_BOT, ""),
    ],
)
def test_language_tenants_load_common_guidance_once(tenant_id, supplement_file):
    config = get_tenant_config(tenant_id)
    assert config is not None
    expected_files = ["tenants/language_channel.md"]
    if supplement_file:
        expected_files.append(supplement_file)
    assert config.qa_guideline_files == expected_files

    expected = "\n\n".join(
        (tenant_config._PROMPTS_DIR / filename).read_text(encoding="utf-8").strip()
        for filename in expected_files
    )
    assert load_tenant_qa_guideline(tenant_id) == expected.replace(
        "{{tenant_id}}", tenant_id.value
    )


def test_composition_preserves_order_and_substitutes_tenant_in_all_files(
    monkeypatch, tmp_path
):
    monkeypatch.setattr(tenant_config, "_PROMPTS_DIR", tmp_path)
    monkeypatch.setitem(
        tenant_config._TENANT_CONFIG_MAP,
        TenantID.DOTNET_CHANNEL_QA_BOT,
        TenantConfig(
            qa_guideline_files=["common.md", "dotnet.md", "extra.md"],
        ),
    )
    (tmp_path / "common.md").write_text(
        "  Common {{tenant_id}}\n", encoding="utf-8"
    )
    (tmp_path / "dotnet.md").write_text(
        "\nSpecific {{tenant_id}}  ", encoding="utf-8"
    )
    (tmp_path / "extra.md").write_text(
        "  Extra {{tenant_id}}\n", encoding="utf-8"
    )
    tenant_id = TenantID.DOTNET_CHANNEL_QA_BOT
    assert load_tenant_qa_guideline(tenant_id) == (
        f"Common {tenant_id.value}\n\nSpecific {tenant_id.value}\n\nExtra {tenant_id.value}"
    )


@pytest.mark.parametrize("missing_file", ["common.md", "dotnet.md", "extra.md"])
def test_composed_guideline_does_not_silently_skip_missing_files(
    monkeypatch, tmp_path, missing_file
):
    monkeypatch.setattr(tenant_config, "_PROMPTS_DIR", tmp_path)
    monkeypatch.setitem(
        tenant_config._TENANT_CONFIG_MAP,
        TenantID.DOTNET_CHANNEL_QA_BOT,
        TenantConfig(
            qa_guideline_files=["common.md", "dotnet.md", "extra.md"],
        ),
    )
    for name in ("common.md", "dotnet.md", "extra.md"):
        if name != missing_file:
            (tmp_path / name).write_text("# Guideline", encoding="utf-8")
    with pytest.raises(FileNotFoundError) as exc:
        load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)
    assert exc.value.filename == str(tmp_path / missing_file)


def test_single_file_missing_guideline_raises(monkeypatch, tmp_path):
    monkeypatch.setattr(tenant_config, "_PROMPTS_DIR", tmp_path)
    monkeypatch.setitem(
        tenant_config._TENANT_CONFIG_MAP,
        TenantID.DOTNET_CHANNEL_QA_BOT,
        TenantConfig(qa_guideline_files=["missing.md"]),
    )
    with pytest.raises(FileNotFoundError):
        load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT)


def test_empty_guideline_list_loads_no_guidance(monkeypatch):
    monkeypatch.setitem(
        tenant_config._TENANT_CONFIG_MAP,
        TenantID.DOTNET_CHANNEL_QA_BOT,
        TenantConfig(),
    )
    assert load_tenant_qa_guideline(TenantID.DOTNET_CHANNEL_QA_BOT) == ""


def test_guideline_lists_are_not_shared_between_configs():
    first = TenantConfig()
    second = TenantConfig()
    first.qa_guideline_files.append("common.md")
    assert second.qa_guideline_files == []


@pytest.mark.parametrize(
    "tenant_id", [TenantID.TYPESPEC_CHANNEL_QA_BOT, TenantID.AZURE_MCP_SERVER]
)
def test_non_language_tenants_do_not_receive_language_guidance(tenant_id):
    config = get_tenant_config(tenant_id)
    assert config is not None
    assert "# Tenant: Azure SDK Language Channel Assistant" not in (
        load_tenant_qa_guideline(tenant_id)
    )
