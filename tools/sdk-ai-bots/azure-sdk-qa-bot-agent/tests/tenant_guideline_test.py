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
    assert TenantID.DOTNET_CHANNEL_QA_BOT.value in guideline
    assert "{{tenant_id}}" not in guideline
    assert "ApiCompat" in guideline
    assert "NullableResponse<T>" in guideline
    assert "mgmt-review-comment-resolution/SKILL.md" in guideline
    assert "mitigate-breaking-changes/SKILL.md" in guideline
    assert "Skills do not replace reviewer approval" in guideline


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
