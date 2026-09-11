from __future__ import annotations

from pathlib import Path

import pytest

from code_repository_sync.models import RepositoryConfig
from code_repository_sync.selection import select_source_files


def _config() -> RepositoryConfig:
    return RepositoryConfig(
        git_url="https://github.com/Azure/typespec-azure.git",
        git_ref="refs/heads/main",
        owner="Azure",
        repository="typespec-azure",
        path_prefixes=("packages",),
        include_patterns=(
            "packages/**/*.tsp",
            "packages/**/*.ts",
        ),
        exclude_patterns=("packages/generated/**",),
    )


def test_selects_utf8_sources_with_limits_and_excludes(tmp_path: Path) -> None:
    _write(tmp_path / "packages/root.ts", "root")
    _write(tmp_path / "packages/nested/main.tsp", "model A {}")
    _write(tmp_path / "packages/generated/skip.ts", "generated")
    _write(tmp_path / "docs/skip.ts", "docs")
    _write(tmp_path / "packages/large.ts", "123456")
    (tmp_path / "packages/binary.ts").write_bytes(b"\xff")

    files = select_source_files(
        tmp_path,
        _config(),
        max_file_bytes=5,
    )

    assert [(file.path, file.content) for file in files] == [
        ("packages/root.ts", b"root")
    ]


def test_applies_patterns_relative_to_submodule_and_prefixes_output(
    tmp_path: Path,
) -> None:
    _write(tmp_path / "packages/compiler/src/main.ts", "source")

    files = select_source_files(
        tmp_path,
        _config(),
        output_prefix="core",
        max_file_bytes=100,
    )

    assert [file.path for file in files] == [
        "core/packages/compiler/src/main.ts"
    ]


def test_rejects_selected_symlink(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    link = tmp_path / "packages/link.ts"
    _write(link, "source")
    path_type = type(link)
    original = path_type.is_symlink
    monkeypatch.setattr(
        path_type,
        "is_symlink",
        lambda path: path == link or original(path),
    )

    with pytest.raises(ValueError, match="symlink file"):
        select_source_files(tmp_path, _config(), max_file_bytes=100)


def test_rejects_absolute_output_prefix(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="unsafe repository-relative path"):
        select_source_files(
            tmp_path,
            _config(),
            output_prefix="/core",
            max_file_bytes=100,
        )


def _write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
