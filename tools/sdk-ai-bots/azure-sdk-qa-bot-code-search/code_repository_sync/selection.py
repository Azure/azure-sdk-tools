from __future__ import annotations

import logging
import os
import stat
from pathlib import Path, PurePosixPath, PureWindowsPath

import pathspec

from .models import RepositoryConfig, SelectedFile

logger = logging.getLogger(__name__)


def select_source_files(
    root: Path,
    config: RepositoryConfig,
    *,
    output_prefix: str = "",
    excluded_roots: tuple[str, ...] = (),
    max_file_bytes: int,
) -> list[SelectedFile]:
    """Select configured UTF-8 regular files without following symlinks."""
    if root.is_symlink() or not root.is_dir():
        raise ValueError(f"repository root must be a regular directory: {root}")

    normalized_output_prefix = _normalize_relative_path(
        output_prefix, allow_empty=True
    )
    normalized_excluded = {
        _normalize_relative_path(value) for value in excluded_roots
    }
    include = pathspec.PathSpec.from_lines(
        "gitwildmatch", config.include_patterns
    )
    exclude = pathspec.PathSpec.from_lines(
        "gitwildmatch", config.exclude_patterns
    )
    selected: list[SelectedFile] = []

    for current, directory_names, file_names in os.walk(
        root, topdown=True, followlinks=False
    ):
        current_path = Path(current)
        current_relative = current_path.relative_to(root).as_posix()
        if current_relative == ".":
            current_relative = ""

        kept_directories: list[str] = []
        for name in sorted(directory_names):
            relative = _join(current_relative, name)
            candidate = current_path / name
            if name == ".git" or relative in normalized_excluded:
                continue
            if candidate.is_symlink():
                if _could_contain_selected_file(relative, config.path_prefixes):
                    raise ValueError(
                        f"repository selection rejects symlink directory: {relative}"
                    )
                continue
            kept_directories.append(name)
        directory_names[:] = kept_directories

        for name in sorted(file_names):
            relative = _join(current_relative, name)
            if relative == ".git" or not _matches(
                relative, config, include, exclude
            ):
                continue
            candidate = current_path / name
            if candidate.is_symlink():
                raise ValueError(
                    f"repository selection rejects symlink file: {relative}"
                )
            metadata = candidate.stat(follow_symlinks=False)
            if not stat.S_ISREG(metadata.st_mode):
                raise ValueError(
                    f"repository selection requires a regular file: {relative}"
                )
            if metadata.st_size > max_file_bytes:
                logger.info(
                    "skipping %s because it exceeds %d bytes",
                    relative,
                    max_file_bytes,
                )
                continue
            content = candidate.read_bytes()
            try:
                content.decode("utf-8")
            except UnicodeDecodeError:
                logger.warning("skipping non-UTF-8 source file %s", relative)
                continue
            output_path = _join(normalized_output_prefix, relative)
            selected.append(SelectedFile(path=output_path, content=content))

    return sorted(selected, key=lambda item: item.path)


def _matches(
    relative: str,
    config: RepositoryConfig,
    include: pathspec.PathSpec,
    exclude: pathspec.PathSpec,
) -> bool:
    in_prefix = any(
        not prefix
        or relative == prefix
        or relative.startswith(f"{prefix}/")
        for prefix in config.path_prefixes
    )
    return in_prefix and include.match_file(relative) and not exclude.match_file(
        relative
    )


def _could_contain_selected_file(
    directory: str, path_prefixes: tuple[str, ...]
) -> bool:
    return any(
        not prefix
        or directory == prefix
        or directory.startswith(f"{prefix}/")
        or prefix.startswith(f"{directory}/")
        for prefix in path_prefixes
    )


def _normalize_relative_path(value: str, *, allow_empty: bool = False) -> str:
    raw = value.replace("\\", "/").strip()
    if raw.startswith("/") or PureWindowsPath(raw).is_absolute():
        raise ValueError(f"unsafe repository-relative path: {value!r}")
    raw = raw.rstrip("/")
    if not raw and allow_empty:
        return ""
    path = PurePosixPath(raw)
    if not raw or path.is_absolute() or ".." in path.parts or "." in path.parts:
        raise ValueError(f"unsafe repository-relative path: {value!r}")
    return path.as_posix()


def _join(parent: str, child: str) -> str:
    return f"{parent}/{child}" if parent else child
