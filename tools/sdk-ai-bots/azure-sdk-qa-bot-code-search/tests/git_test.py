from __future__ import annotations

from collections.abc import Sequence
from pathlib import Path

import pytest

from code_repository_sync.git import (
    ParsedSubmoduleStatus,
    checkout_repository,
    parse_submodule_status,
    validate_submodules,
)
from code_repository_sync.models import RepositoryConfig

_CORE_COMMIT = "a" * 40
_NESTED_COMMIT = "b" * 40


def test_checkout_uses_fresh_depth_one_fetch_and_recursive_submodules(
    tmp_path: Path,
) -> None:
    destination = tmp_path / "checkout"
    commands: list[tuple[str, ...]] = []

    def run(args: Sequence[str], _cwd: Path | None) -> str:
        command = tuple(args)
        commands.append(command)
        if args[:3] == ("git", "rev-parse", "FETCH_HEAD^{commit}"):
            return _CORE_COMMIT
        if args[:3] == ("git", "rev-parse", "HEAD"):
            return _CORE_COMMIT
        if args[:3] == ("git", "rev-list", "--count"):
            return "1"
        return ""

    snapshot = checkout_repository(
        RepositoryConfig(
            git_url="https://github.com/Azure/typespec-azure.git",
            git_ref="refs/heads/main",
            owner="Azure",
            repository="typespec-azure",
            path_prefixes=("packages",),
            include_patterns=("packages/**/*.ts",),
            exclude_patterns=(),
        ),
        destination,
        runner=run,
    )

    assert snapshot.commit == _CORE_COMMIT
    assert (
        "git",
        "fetch",
        "--quiet",
        "--force",
        "--no-tags",
        "--depth=1",
        "origin",
        "refs/heads/main",
    ) in commands
    assert (
        "git",
        "submodule",
        "update",
        "--init",
        "--recursive",
        "--depth=1",
        "--recommend-shallow",
    ) in commands


def test_parses_recursive_submodule_status() -> None:
    statuses = parse_submodule_status(
        f" {_CORE_COMMIT} core (heads/main)\n"
        f" {_NESTED_COMMIT} core/vendor/nested\n"
    )

    assert statuses == (
        ParsedSubmoduleStatus(" ", _CORE_COMMIT, "core"),
        ParsedSubmoduleStatus(" ", _NESTED_COMMIT, "core/vendor/nested"),
    )


def test_rejects_absolute_submodule_path() -> None:
    with pytest.raises(RuntimeError, match="Unsafe submodule path"):
        parse_submodule_status(f" {_CORE_COMMIT} /core\n")


@pytest.mark.parametrize("marker", ["-", "+", "U"])
def test_rejects_uninitialized_or_mismatched_submodule(
    tmp_path: Path, marker: str
) -> None:
    (tmp_path / "core").mkdir()
    status = ParsedSubmoduleStatus(marker, _CORE_COMMIT, "core")

    with pytest.raises(RuntimeError, match="Submodule 'core'"):
        validate_submodules(tmp_path, (status,), runner=lambda _args, _cwd: "")


def test_validates_recursive_submodules_at_gitlink_commits(
    tmp_path: Path,
) -> None:
    core = tmp_path / "core"
    nested = core / "vendor" / "nested"
    nested.mkdir(parents=True)
    calls: list[tuple[tuple[str, ...], Path | None]] = []

    def run(args: Sequence[str], cwd: Path | None) -> str:
        command = tuple(args)
        calls.append((command, cwd))
        if args[:3] == ("git", "rev-parse", "HEAD"):
            return _NESTED_COMMIT if cwd == nested else _CORE_COMMIT
        if args[:3] == ("git", "rev-list", "--count"):
            return "1"
        if args[:3] == ("git", "ls-tree", "HEAD"):
            commit = _NESTED_COMMIT if cwd == core else _CORE_COMMIT
            return f"160000 commit {commit}\t{args[-1]}"
        if args[:4] == ("git", "remote", "get-url", "origin"):
            return "https://github.com/microsoft/typespec.git"
        raise AssertionError(args)

    snapshots = validate_submodules(
        tmp_path,
        (
            ParsedSubmoduleStatus(" ", _CORE_COMMIT, "core"),
            ParsedSubmoduleStatus(" ", _NESTED_COMMIT, "core/vendor/nested"),
        ),
        runner=run,
    )

    assert [(item.path, item.commit) for item in snapshots] == [
        ("core", _CORE_COMMIT),
        ("core/vendor/nested", _NESTED_COMMIT),
    ]
    assert snapshots[0].to_manifest() == {
        "path": "core",
        "git_url": "https://github.com/microsoft/typespec.git",
        "commit_sha": _CORE_COMMIT,
    }
    assert any(
        args[-1] == "vendor/nested" and cwd == core for args, cwd in calls
    )
