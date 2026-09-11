from __future__ import annotations

from pathlib import Path
import stat

import pytest

from code_repository_sync import sync
from code_repository_sync.models import (
    RepositoryConfig,
    RepositorySnapshot,
    SelectedFile,
    SubmoduleSnapshot,
)


def _repository() -> RepositoryConfig:
    return RepositoryConfig(
        git_url="https://github.com/Azure/typespec-azure.git",
        git_ref="refs/heads/main",
        owner="Azure",
        repository="typespec-azure",
        path_prefixes=("packages",),
        include_patterns=("packages/**/*.tsp",),
        exclude_patterns=(),
    )


def test_prepares_manifest_with_repository_and_submodule_commits(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    submodule = SubmoduleSnapshot(
        path="core",
        git_url="https://github.com/microsoft/typespec.git",
        commit="b" * 40,
    )
    monkeypatch.setattr(
        sync,
        "checkout_repository",
        lambda _repository, destination: RepositorySnapshot(
            root=destination,
            commit="a" * 40,
            submodules=(submodule,),
        ),
    )
    monkeypatch.setattr(
        sync,
        "select_source_files",
        lambda _root, _repository, **kwargs: (
            []
            if not kwargs.get("output_prefix")
            else [SelectedFile("core/packages/main.tsp", b"model Widget {}")]
        ),
    )

    files, manifests = sync._prepare_repositories(
        [_repository()],
        tmp_path,
        max_file_bytes=1024,
    )

    assert [file.path for file in files] == [
        "Azure/typespec-azure/core/packages/main.tsp"
    ]
    assert manifests == [
        {
            "name": "Azure/typespec-azure",
            "git_url": "https://github.com/Azure/typespec-azure.git",
            "git_ref": "refs/heads/main",
            "commit_sha": "a" * 40,
            "submodules": [
                {
                    "path": "core",
                    "git_url": "https://github.com/microsoft/typespec.git",
                    "commit_sha": "b" * 40,
                }
            ],
        }
    ]


def test_rejects_empty_repository_selection(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(
        sync,
        "checkout_repository",
        lambda _repository, destination: RepositorySnapshot(
            root=destination,
            commit="a" * 40,
            submodules=(),
        ),
    )
    monkeypatch.setattr(
        sync,
        "select_source_files",
        lambda *_args, **_kwargs: [],
    )

    with pytest.raises(RuntimeError, match="produced no files"):
        sync._prepare_repositories(
            [_repository()],
            tmp_path,
            max_file_bytes=1024,
        )


def test_remove_run_root_clears_readonly_files(tmp_path: Path) -> None:
    work_root = tmp_path / "work"
    run_root = work_root / "run"
    readonly = run_root / "objects" / "object"
    readonly.parent.mkdir(parents=True)
    readonly.write_text("git object", encoding="utf-8")
    readonly.chmod(stat.S_IREAD)

    sync._remove_run_root(run_root, work_root)

    assert not work_root.exists()
