from __future__ import annotations

from pathlib import Path

from code_repository_sync.repositories import aggregate_repository_configs
from code_repository_sync.tenant_source import load_tenant_repository_configs


def test_loads_sibling_repository_declarations_without_agent_dependencies() -> None:
    path = (
        Path(__file__).resolve().parents[2]
        / "azure-sdk-qa-bot-agent"
        / "config"
        / "tenant_config.py"
    )

    raw = load_tenant_repository_configs(path)
    repositories = aggregate_repository_configs(raw)

    assert [
        (repository.namespace, repository.git_ref)
        for repository in repositories
    ] == [
        ("Azure/typespec-azure", "refs/heads/main"),
        ("microsoft/typespec", "refs/heads/main"),
    ]
