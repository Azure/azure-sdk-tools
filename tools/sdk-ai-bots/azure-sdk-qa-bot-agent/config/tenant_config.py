"""Tenant configuration for the Azure SDK QA Bot Agent.

Each tenant defines a lightweight config with:
  - Knowledge sources (KnowledgeSource objects with name, description, filter).
  - A tenant-specific QA guideline loaded from prompts/tenants/.
  - Whether tenant routing is enabled.

The knowledge source registry lives here so that both the tenant config and
the search tool can look up sources by name.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path

from models.knowledge import KnowledgeSource

# ---------------------------------------------------------------------------
# Tenant IDs
# ---------------------------------------------------------------------------

class TenantID(str, Enum):
    AZURE_SDK_QA_BOT = "azure_sdk_qa_bot"
    PYTHON_CHANNEL_QA_BOT = "python_channel_qa_bot"
    DOTNET_CHANNEL_QA_BOT = "dotnet_channel_qa_bot"
    GOLANG_CHANNEL_QA_BOT = "golang_channel_qa_bot"
    JAVA_CHANNEL_QA_BOT = "java_channel_qa_bot"
    JAVASCRIPT_CHANNEL_QA_BOT = "javascript_channel_qa_bot"
    GENERAL_QA_BOT = "general_qa_bot"
    AZURE_SDK_ONBOARDING = "azure_sdk_onboarding"
    AZURE_TYPESPEC_AUTHORING = "azure_typespec_authoring"
    API_SPEC_REVIEW_BOT = "api_spec_review_bot"


# ---------------------------------------------------------------------------
# Knowledge source name constants
# ---------------------------------------------------------------------------

# -- TypeSpec --
SRC_TYPESPEC_DOCS = "typespec_docs"
SRC_TYPESPEC_AZURE_DOCS = "typespec_azure_docs"
SRC_TYPESPEC_AZURE_HTTP_SPECS = "typespec_azure_http_specs"
SRC_TYPESPEC_HTTP_SPECS = "typespec_http_specs"
SRC_STATIC_TYPESPEC_QA = "static_typespec_qa"
SRC_STATIC_TYPESPEC_MIGRATION_DOCS = "static_typespec_migration_docs"
SRC_STATIC_TYPESPEC_TO_SWAGGER_MAPPING = "static_typespec_to_swagger_mapping"
SRC_TYPESPEC_AZURE_RESOURCE_MANAGER_LIB = "typespec-azure-resource-manager-lib"

# -- Azure Guidelines & Standards --
SRC_AZURE_API_GUIDELINES = "azure_api_guidelines"
SRC_AZURE_RESOURCE_MANAGER_RPC = "azure_resource_manager_rpc"
SRC_AZURE_REST_API_SPECS_WIKI = "azure_rest_api_specs_wiki"
SRC_AZURE_REST_API_SPECS_DOCS = "azure_rest_api_specs_docs"
SRC_AZURE_OPENAPI_DIFF_DOCS = "azure_openapi_diff_docs"

# -- SDK language docs --
SRC_AZURE_SDK_FOR_PYTHON_DOCS = "azure_sdk_for_python_docs"
SRC_AZURE_SDK_FOR_PYTHON_WIKI = "azure_sdk_for_python_wiki"
SRC_AZURE_SDK_FOR_NET_DOCS = "azure_sdk_for_net_docs"
SRC_AZURE_SDK_FOR_GO_DOCS = "azure_sdk_for_go_docs"
SRC_AZURE_SDK_FOR_JAVA_DOCS = "azure_sdk_for_java_docs"
SRC_AZURE_SDK_FOR_JAVA_WIKI = "azure_sdk_for_java_wiki"
SRC_AZURE_SDK_FOR_JS_DOCS = "azure_sdk_for_js_docs"
SRC_AZURE_SDK_FOR_JS_WIKI = "azure_sdk_for_js_wiki"
SRC_AUTOREST_JAVA_DOCS = "autorest_java_docs"

# -- Cross-language SDK resources --
SRC_AZURE_SDK_GUIDELINES = "azure-sdk-guidelines"
SRC_AZURE_SDK_DOCS_ENG = "azure-sdk-docs-eng"
SRC_AZURE_SDK_INTERNAL_WIKI = "azure-sdk-internal-wiki"

# -- General Azure & review resources --
SRC_STATIC_AZURE_DOCS = "static_azure_docs"
SRC_STATIC_API_SPEC_VIEW_QA = "static_api_spec_view_qa"


# ---------------------------------------------------------------------------
# Global knowledge source registry
# ---------------------------------------------------------------------------
# Every knowledge source used by any tenant is registered here **once**.
# Tenants reference sources by name, optionally overriding the default filter.

KNOWLEDGE_SOURCE_REGISTRY: dict[str, KnowledgeSource] = {}


def _register(*sources: KnowledgeSource) -> None:
    for src in sources:
        KNOWLEDGE_SOURCE_REGISTRY[src.name] = src


_register(
    # -- TypeSpec --
    KnowledgeSource(
        name=SRC_TYPESPEC_DOCS,
        description="Core TypeSpec language documentation covering fundamental syntax, semantics, and usage patterns.",
    ),
    KnowledgeSource(
        name=SRC_TYPESPEC_AZURE_DOCS,
        description="Azure-specific TypeSpec documentation, patterns, and templates for management and data-plane services.",
    ),
    KnowledgeSource(
        name=SRC_TYPESPEC_AZURE_HTTP_SPECS,
        description="All Azure TypeSpec scenarios that should be supported by client & service generators.",
    ),
    KnowledgeSource(
        name=SRC_TYPESPEC_HTTP_SPECS,
        description="All scenarios that should be supported by client & service generators.",
    ),
    KnowledgeSource(
        name=SRC_STATIC_TYPESPEC_QA,
        description="Historical Q&A repository with expert TypeSpec solutions for Azure scenarios.",
    ),
    KnowledgeSource(
        name=SRC_STATIC_TYPESPEC_MIGRATION_DOCS,
        description="TypeSpec migration guides and conversion patterns from OpenAPI/Swagger.",
    ),
    KnowledgeSource(
        name=SRC_STATIC_TYPESPEC_TO_SWAGGER_MAPPING,
        description="Mapping between TypeSpec constructs and Swagger/OpenAPI equivalents.",
    ),
    KnowledgeSource(
        name=SRC_TYPESPEC_AZURE_RESOURCE_MANAGER_LIB,
        description="TypeSpec Azure Resource Manager library documentation covering ARM-specific decorators and templates.",
    ),

    # -- Azure Guidelines & Standards --
    KnowledgeSource(
        name=SRC_AZURE_API_GUIDELINES,
        description="Comprehensive Azure REST API guidelines, OpenAPI standards, and development best practices for data-plane APIs.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_RESOURCE_MANAGER_RPC,
        description="Azure Resource Manager (ARM) RPC specs including RBAC, tags, templates, and ARM compliance requirements.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_REST_API_SPECS_WIKI,
        description="Guidelines for Azure REST API specifications using Swagger or TypeSpec, including PR review process.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_REST_API_SPECS_DOCS,
        description="Azure REST API specification documentation covering spec structure and conventions.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_OPENAPI_DIFF_DOCS,
        description="OpenAPI diff documentation for detecting and managing breaking changes in API specifications.",
    ),

    # -- SDK language docs --
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_PYTHON_DOCS,
        description="Azure SDK for Python documentation covering installation, usage patterns, and API reference.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_PYTHON_WIKI,
        description="Azure SDK for Python wiki with guides, troubleshooting, and development best practices.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_NET_DOCS,
        description="Azure SDK for .NET documentation covering installation, usage patterns, and API reference.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_GO_DOCS,
        description="Azure SDK for Go documentation covering installation, usage patterns, and API reference.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_JAVA_DOCS,
        description="Azure SDK for Java documentation covering installation, usage patterns, and API reference.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_JAVA_WIKI,
        description="Azure SDK for Java wiki with guides, troubleshooting, and development best practices.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_JS_DOCS,
        description="Azure SDK for JavaScript/TypeScript documentation covering installation, usage patterns, and API reference.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_FOR_JS_WIKI,
        description="Azure SDK for JavaScript/TypeScript wiki with guides, troubleshooting, and development best practices.",
    ),
    KnowledgeSource(
        name=SRC_AUTOREST_JAVA_DOCS,
        description="AutoRest Java code generator documentation for Azure SDK Java generation from OpenAPI specs.",
    ),

    # -- Cross-language SDK resources --
    KnowledgeSource(
        name=SRC_AZURE_SDK_GUIDELINES,
        description="Cross-language Azure SDK design guidelines and best practices for all supported languages.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_DOCS_ENG,
        description="Azure SDK engineering documentation covering onboarding, release processes, and engineering systems.",
    ),
    KnowledgeSource(
        name=SRC_AZURE_SDK_INTERNAL_WIKI,
        description="Internal Azure SDK wiki with team-specific guidance and operational knowledge.",
    ),

    # -- General Azure & review resources --
    KnowledgeSource(
        name=SRC_STATIC_AZURE_DOCS,
        description="Static Azure documentation and reference materials for general Azure services.",
    ),
    KnowledgeSource(
        name=SRC_STATIC_API_SPEC_VIEW_QA,
        description="Historical Q&A for API specification review covering common validation errors and fixes.",
    ),
)


def get_knowledge_source(name: str) -> KnowledgeSource | None:
    """Look up a registered knowledge source by name."""
    return KNOWLEDGE_SOURCE_REGISTRY.get(name)


# ---------------------------------------------------------------------------
# Tenant config data class
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class TenantConfig:
    """Lightweight per-tenant configuration.

    ``sources`` is an ordered list of :class:`KnowledgeSource` objects
    available to this tenant.  Each source carries its own description and
    default filter; tenants can override a source's filter via
    ``source_filter_overrides``.
    """

    sources: list[KnowledgeSource] = field(default_factory=list)
    source_filter_overrides: dict[str, str] = field(default_factory=dict)
    qa_guideline_file: str = ""
    enable_routing: bool = False

    def get_source_filter(self, source_name: str) -> str:
        """Return the effective filter for *source_name*.

        Checks tenant-level overrides first, then falls back to the
        source's default filter.
        """
        if source_name in self.source_filter_overrides:
            return self.source_filter_overrides[source_name]
        src = get_knowledge_source(source_name)
        return src.filter if src else ""


# ---------------------------------------------------------------------------
# Helper to build source lists from registry names
# ---------------------------------------------------------------------------

def _sources(*names: str) -> list[KnowledgeSource]:
    """Resolve a list of source names to KnowledgeSource objects."""
    result = []
    for n in names:
        src = KNOWLEDGE_SOURCE_REGISTRY.get(n)
        if src is None:
            raise ValueError(f"Unknown knowledge source: {n!r}")
        result.append(src)
    return result


# ---------------------------------------------------------------------------
# Shared source lists (mirrors Go backend)
# ---------------------------------------------------------------------------

_TYPESPEC_SOURCES = _sources(
    SRC_AZURE_RESOURCE_MANAGER_RPC,
    SRC_AZURE_API_GUIDELINES,
    SRC_TYPESPEC_AZURE_DOCS,
    SRC_STATIC_TYPESPEC_QA,
    SRC_TYPESPEC_AZURE_HTTP_SPECS,
    SRC_TYPESPEC_DOCS,
    SRC_AZURE_REST_API_SPECS_WIKI,
    SRC_STATIC_TYPESPEC_MIGRATION_DOCS,
    SRC_TYPESPEC_HTTP_SPECS,
    SRC_STATIC_AZURE_DOCS,
    SRC_STATIC_TYPESPEC_TO_SWAGGER_MAPPING,
)

_AZURE_TYPESPEC_AUTHORING_SOURCES = _sources(
    SRC_AZURE_API_GUIDELINES,
    SRC_AZURE_RESOURCE_MANAGER_RPC,
    SRC_TYPESPEC_AZURE_DOCS,
    SRC_STATIC_TYPESPEC_QA,
    SRC_TYPESPEC_AZURE_HTTP_SPECS,
    SRC_TYPESPEC_DOCS,
    SRC_AZURE_REST_API_SPECS_WIKI,
    SRC_STATIC_TYPESPEC_MIGRATION_DOCS,
    SRC_TYPESPEC_HTTP_SPECS,
    SRC_STATIC_AZURE_DOCS,
    SRC_TYPESPEC_AZURE_RESOURCE_MANAGER_LIB,
)

# ---------------------------------------------------------------------------
# Tenant config map
# ---------------------------------------------------------------------------

_TENANT_CONFIG_MAP: dict[TenantID, TenantConfig] = {
    TenantID.PYTHON_CHANNEL_QA_BOT: TenantConfig(
        sources=_sources(
            SRC_AZURE_SDK_FOR_PYTHON_DOCS,
            SRC_AZURE_SDK_FOR_PYTHON_WIKI,
            SRC_AZURE_SDK_GUIDELINES,
            SRC_AZURE_SDK_DOCS_ENG,
            SRC_AZURE_SDK_INTERNAL_WIKI,
            SRC_TYPESPEC_AZURE_DOCS,
            SRC_AZURE_REST_API_SPECS_WIKI,
        ),
        source_filter_overrides={
            SRC_AZURE_SDK_GUIDELINES: "search.ismatch('python_*', 'title')",
            SRC_TYPESPEC_AZURE_DOCS: "search.ismatch('typespec-python*', 'title') or search.ismatch('generate*', 'title')",
            SRC_AZURE_REST_API_SPECS_WIKI: "search.ismatch('SDK*', 'title')",
        },
        qa_guideline_file="tenants/language_python.md",
    ),
    TenantID.DOTNET_CHANNEL_QA_BOT: TenantConfig(
        sources=_sources(
            SRC_AZURE_SDK_FOR_NET_DOCS,
            SRC_AZURE_SDK_GUIDELINES,
            SRC_AZURE_SDK_DOCS_ENG,
            SRC_TYPESPEC_AZURE_DOCS,
            SRC_AZURE_REST_API_SPECS_WIKI,
        ),
        source_filter_overrides={
            SRC_AZURE_SDK_GUIDELINES: "search.ismatch('dotnet_*', 'title')",
            SRC_TYPESPEC_AZURE_DOCS: "search.ismatch('typespec-csharp*', 'title') or search.ismatch('generate*', 'title')",
            SRC_AZURE_REST_API_SPECS_WIKI: "search.ismatch('SDK*', 'title')",
        },
        qa_guideline_file="tenants/language_channel.md",
    ),
    TenantID.GOLANG_CHANNEL_QA_BOT: TenantConfig(
        sources=_sources(
            SRC_AZURE_SDK_FOR_GO_DOCS,
            SRC_AZURE_SDK_GUIDELINES,
            SRC_AZURE_SDK_DOCS_ENG,
            SRC_TYPESPEC_AZURE_DOCS,
            SRC_AZURE_REST_API_SPECS_WIKI,
        ),
        source_filter_overrides={
            SRC_AZURE_SDK_GUIDELINES: "search.ismatch('golang_*', 'title')",
            SRC_TYPESPEC_AZURE_DOCS: "search.ismatch('typespec-go*', 'title') or search.ismatch('generate*', 'title')",
            SRC_AZURE_REST_API_SPECS_WIKI: "search.ismatch('SDK*', 'title')",
        },
        qa_guideline_file="tenants/language_channel.md",
    ),
    TenantID.JAVA_CHANNEL_QA_BOT: TenantConfig(
        sources=_sources(
            SRC_AZURE_SDK_FOR_JAVA_DOCS,
            SRC_AZURE_SDK_FOR_JAVA_WIKI,
            SRC_AZURE_SDK_GUIDELINES,
            SRC_AUTOREST_JAVA_DOCS,
            SRC_AZURE_SDK_DOCS_ENG,
            SRC_TYPESPEC_AZURE_DOCS,
            SRC_AZURE_REST_API_SPECS_WIKI,
        ),
        source_filter_overrides={
            SRC_AZURE_SDK_GUIDELINES: "search.ismatch('java_*', 'title')",
            SRC_TYPESPEC_AZURE_DOCS: "search.ismatch('typespec-java*', 'title') or search.ismatch('generate*', 'title')",
            SRC_AZURE_REST_API_SPECS_WIKI: "search.ismatch('SDK*', 'title')",
        },
        qa_guideline_file="tenants/language_channel.md",
    ),
    TenantID.JAVASCRIPT_CHANNEL_QA_BOT: TenantConfig(
        sources=_sources(
            SRC_AZURE_SDK_FOR_JS_DOCS,
            SRC_AZURE_SDK_FOR_JS_WIKI,
            SRC_AZURE_SDK_GUIDELINES,
            SRC_AZURE_SDK_DOCS_ENG,
            SRC_TYPESPEC_AZURE_DOCS,
            SRC_AZURE_REST_API_SPECS_WIKI,
        ),
        source_filter_overrides={
            SRC_AZURE_SDK_GUIDELINES: "search.ismatch('typescript_*', 'title')",
            SRC_TYPESPEC_AZURE_DOCS: "search.ismatch('typespec-ts*', 'title') or search.ismatch('generate*', 'title')",
            SRC_AZURE_REST_API_SPECS_WIKI: "search.ismatch('SDK*', 'title')",
        },
        qa_guideline_file="tenants/language_channel.md",
    ),
    TenantID.AZURE_SDK_QA_BOT: TenantConfig(
        sources=[*_TYPESPEC_SOURCES, *_sources(SRC_AZURE_SDK_DOCS_ENG)],
        source_filter_overrides={
            SRC_AZURE_SDK_DOCS_ENG: "search.ismatch('design*', 'title')",
        },
        qa_guideline_file="tenants/typespec.md",
        enable_routing=True,
    ),
    TenantID.AZURE_SDK_ONBOARDING: TenantConfig(
        sources=_sources(SRC_AZURE_SDK_DOCS_ENG),
        qa_guideline_file="tenants/azure_sdk_onboarding.md",
    ),
    TenantID.AZURE_TYPESPEC_AUTHORING: TenantConfig(
        sources=_AZURE_TYPESPEC_AUTHORING_SOURCES,
        qa_guideline_file="tenants/azure_typespec_authoring.md",
    ),
    TenantID.API_SPEC_REVIEW_BOT: TenantConfig(
        sources=_sources(
            SRC_STATIC_AZURE_DOCS,
            SRC_STATIC_API_SPEC_VIEW_QA,
            SRC_AZURE_REST_API_SPECS_WIKI,
            SRC_AZURE_REST_API_SPECS_DOCS,
            SRC_AZURE_OPENAPI_DIFF_DOCS,
            SRC_AZURE_SDK_DOCS_ENG,
        ),
        source_filter_overrides={
            SRC_AZURE_SDK_DOCS_ENG: "search.ismatch('design*', 'title')",
        },
        qa_guideline_file="tenants/api_spec_review.md",
        enable_routing=True,
    ),
    TenantID.GENERAL_QA_BOT: TenantConfig(
        qa_guideline_file="tenants/general.md",
        enable_routing=True,
    ),
}


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

_PROMPTS_DIR = Path(__file__).resolve().parent.parent / "prompts"


def get_tenant_config(tenant_id: str) -> TenantConfig | None:
    """Return the config for *tenant_id*, or ``None`` if unknown."""
    try:
        tid = TenantID(tenant_id)
    except ValueError:
        return None
    return _TENANT_CONFIG_MAP.get(tid)


def get_all_tenant_ids() -> list[str]:
    """Return all registered tenant ID strings."""
    return [t.value for t in _TENANT_CONFIG_MAP]


def get_tenant_sources_display(tenant_id: str) -> list[dict[str, str]]:
    """Return the knowledge sources for a tenant as a list of {name, description} dicts.

    This is the format sent to the LLM so it can decide which sources to query.
    """
    config = get_tenant_config(tenant_id)
    if config is None:
        return []
    return [src.to_display_dict() for src in config.sources]


def load_tenant_qa_guideline(tenant_id: str) -> str:
    """Load the tenant-specific QA guideline markdown from prompts/.

    Returns an empty string if the tenant or file is not found.
    """
    config = get_tenant_config(tenant_id)
    if config is None or not config.qa_guideline_file:
        return ""
    guideline_path = _PROMPTS_DIR / config.qa_guideline_file
    if not guideline_path.exists():
        return ""
    return guideline_path.read_text(encoding="utf-8").strip()
