from __future__ import annotations

from pathlib import Path

from code_search.models import BuilderSettings, EmbeddingConfig
from code_search.plan import aggregate_repository_demands
from code_search.tenant_source import load_tenant_repository_configs


def test_agent_tenant_configuration_builds_one_plan_per_repository() -> None:
    settings = BuilderSettings(
        search_endpoint="https://example.search.windows.net",
        search_alias="alias",
        search_index_name="index",
        search_user_assigned_identity_resource_id="",
        storage_endpoint="https://example.blob.core.windows.net",
        storage_container="code-index",
        work_root=Path(".code-index"),
        max_file_bytes=2_097_152,
        embedding_concurrency=2,
        validation_timeout_seconds=180,
        embedding=EmbeddingConfig(
            endpoint="https://example.openai.azure.com",
            deployment="embedding",
            model="text-embedding-3-large",
            dimensions=3072,
            api_version="2024-02-01",
        ),
    )

    plan = aggregate_repository_demands(
        load_tenant_repository_configs(), settings
    )

    assert {item.git_url for item in plan} == {
        "https://github.com/Azure/typespec-azure.git",
        "https://github.com/microsoft/typespec.git",
    }
    assert all(item.path_prefixes == ("packages",) for item in plan)
    assert all(
        all(
            pattern.startswith("packages/") and not pattern.endswith(".md")
            for pattern in item.include_patterns
        )
        for item in plan
    )
