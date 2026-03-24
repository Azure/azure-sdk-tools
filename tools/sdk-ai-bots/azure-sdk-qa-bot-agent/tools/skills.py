"""Skill definitions for the Azure SDK QA Bot Agent.

Generates agent_framework Skills from the existing tenant configuration.
Each tenant becomes a Skill with:
  - description: advertised in the system prompt (~100 tokens each)
  - content: the full QA guideline + tenant_id + knowledge source names
    so the agent can self-route without an LLM call.
"""

from __future__ import annotations

import logging

from agent_framework import Skill

from config.tenant_config import (
    TenantID,
    get_tenant_config,
    load_tenant_qa_guideline,
)

logger = logging.getLogger(__name__)

# Map tenant IDs to skill names (kebab-case)
_TENANT_SKILL_MAP: dict[TenantID, str] = {
    TenantID.API_SPEC_REVIEW_BOT: "api-spec-review",
    TenantID.AZURE_SDK_ONBOARDING: "sdk-onboarding",
    TenantID.AZURE_TYPESPEC_AUTHORING: "typespec-authoring",
    TenantID.AZURE_SDK_QA_BOT: "typespec",
    TenantID.PYTHON_CHANNEL_QA_BOT: "python-sdk",
    TenantID.DOTNET_CHANNEL_QA_BOT: "dotnet-sdk",
    TenantID.JAVA_CHANNEL_QA_BOT: "java-sdk",
    TenantID.JAVASCRIPT_CHANNEL_QA_BOT: "javascript-sdk",
    TenantID.GOLANG_CHANNEL_QA_BOT: "go-sdk",
    TenantID.GENERAL_QA_BOT: "general-azure-sdk",
}

# Short descriptions for system-prompt advertisement
_SKILL_DESCRIPTIONS: dict[TenantID, str] = {
    TenantID.API_SPEC_REVIEW_BOT: (
        "Azure REST API specification PR review: validation errors, LintDiff, "
        "Avocado, breaking changes, merge process in azure-rest-api-specs repos."
    ),
    TenantID.AZURE_SDK_ONBOARDING: (
        "Azure SDK onboarding process: service onboarding phases, release planner, "
        "SDK lifecycle, permissions, AzSDK agent usage."
    ),
    TenantID.AZURE_TYPESPEC_AUTHORING: (
        "Advanced TypeSpec authoring: ARM and data-plane API design, Azure Templates, "
        "decorators, code generation, RPC compliance."
    ),
    TenantID.AZURE_SDK_QA_BOT: (
        "TypeSpec language: syntax, decorators, patterns, Azure extensions, "
        "migration from OpenAPI, validation, tspconfig."
    ),
    TenantID.PYTHON_CHANNEL_QA_BOT: (
        "Azure Python SDK: code generation, tsp-client, validation, testing, "
        "release processes, runtime usage."
    ),
    TenantID.DOTNET_CHANNEL_QA_BOT: (
        "Azure .NET SDK: code generation, validation, testing, release, "
        "pipeline troubleshooting."
    ),
    TenantID.JAVA_CHANNEL_QA_BOT: (
        "Azure Java SDK: code generation, AutoRest Java, validation, testing, "
        "release, pipeline troubleshooting."
    ),
    TenantID.JAVASCRIPT_CHANNEL_QA_BOT: (
        "Azure JavaScript/TypeScript SDK: code generation, validation, testing, "
        "release, pipeline troubleshooting."
    ),
    TenantID.GOLANG_CHANNEL_QA_BOT: (
        "Azure Go SDK: code generation, validation, testing, release, "
        "pipeline troubleshooting."
    ),
    TenantID.GENERAL_QA_BOT: (
        "General Azure SDK guidance: cross-language questions, API design, "
        "topics spanning multiple domains."
    ),
}


def _build_skill_content(tenant_id: TenantID) -> str:
    """Build skill content combining tenant_id, knowledge sources, and guideline."""
    config = get_tenant_config(tenant_id.value)
    if config is None:
        return ""

    parts: list[str] = []

    # Tenant ID for search_knowledge_base
    parts.append(f"[skill_tenant_id]: {tenant_id.value}")

    # Knowledge sources
    if config.sources:
        parts.append("\n[skill_knowledge_sources]")
        for src in config.sources:
            parts.append(f"- {src.name}: {src.description}")

    # Full guideline
    guideline = load_tenant_qa_guideline(tenant_id.value)
    if guideline:
        parts.append(f"\n[skill_guideline]\n{guideline}")

    return "\n".join(parts)


def create_tenant_skills() -> list[Skill]:
    """Create a Skill for each tenant in the configuration."""
    skills: list[Skill] = []
    for tenant_id, skill_name in _TENANT_SKILL_MAP.items():
        description = _SKILL_DESCRIPTIONS.get(tenant_id, "")
        content = _build_skill_content(tenant_id)
        if not content:
            logger.warning("Skipping skill %s: no content", skill_name)
            continue
        skills.append(
            Skill(
                name=skill_name,
                description=description,
                content=content,
            )
        )
        logger.info("Created skill: %s (tenant=%s)", skill_name, tenant_id.value)
    return skills
