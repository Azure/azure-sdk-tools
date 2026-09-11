from __future__ import annotations

from dataclasses import dataclass

import pytest

from code_repository_sync.repositories import aggregate_repository_configs


@dataclass(frozen=True)
class _Config:
    git_url: str = "https://github.com/Azure/typespec-azure.git"
    git_ref: str = "refs/heads/main"
    path_prefixes: tuple[str, ...] = ("packages",)
    include_patterns: tuple[str, ...] = ("packages/**/*.ts",)
    exclude: tuple[str, ...] = ("packages/generated/**",)


def test_deduplicates_identical_url_ref_declarations() -> None:
    repositories = aggregate_repository_configs(
        [("one", _Config()), ("two", _Config())]
    )

    assert len(repositories) == 1
    assert repositories[0].namespace == "Azure/typespec-azure"


def test_rejects_conflicting_selection_for_same_url_ref() -> None:
    with pytest.raises(ValueError, match="Conflicting repository selection"):
        aggregate_repository_configs(
            [
                ("one", _Config()),
                ("two", _Config(include_patterns=("packages/**/*.tsp",))),
            ]
        )


def test_rejects_multiple_refs_for_stable_blob_namespace() -> None:
    with pytest.raises(ValueError, match="blob namespace"):
        aggregate_repository_configs(
            [
                ("one", _Config()),
                ("two", _Config(git_ref="refs/heads/feature")),
            ]
        )


def test_rejects_traversal_in_repository_config() -> None:
    with pytest.raises(ValueError, match="unsafe repository path prefix"):
        aggregate_repository_configs(
            [("one", _Config(path_prefixes=("../packages",)))]
        )


@pytest.mark.parametrize(
    "config",
    [
        _Config(git_url="https://example.com/Azure/typespec-azure.git"),
        _Config(path_prefixes=("/packages",)),
        _Config(path_prefixes=(r"C:\packages",)),
    ],
)
def test_rejects_non_github_or_absolute_repository_config(
    config: _Config,
) -> None:
    with pytest.raises(ValueError):
        aggregate_repository_configs([("one", config)])
