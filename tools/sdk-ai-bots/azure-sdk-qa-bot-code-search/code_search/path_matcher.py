from __future__ import annotations

from pathlib import Path, PurePath

import pathspec
from cocoindex.resources.file import PatternFilePathMatcher


class RepositoryPathMatcher:
    def __init__(
        self,
        root: Path,
        included_patterns: tuple[str, ...],
        excluded_patterns: tuple[str, ...],
        max_file_bytes: int,
    ) -> None:
        self._root = root
        self._patterns = PatternFilePathMatcher(
            included_patterns=list(included_patterns),
            excluded_patterns=list(excluded_patterns),
        )
        gitignore = root / ".gitignore"
        lines = (
            gitignore.read_text(encoding="utf-8", errors="replace").splitlines()
            if gitignore.is_file()
            else []
        )
        self._gitignore = pathspec.PathSpec.from_lines("gitwildmatch", lines)
        self._max_file_bytes = max_file_bytes

    def is_dir_included(self, path: PurePath) -> bool:
        value = path.as_posix().rstrip("/") + "/"
        return self._patterns.is_dir_included(path) and not self._gitignore.match_file(
            value
        )

    def is_file_included(self, path: PurePath) -> bool:
        if not self._patterns.is_file_included(path):
            return False
        value = path.as_posix()
        if self._gitignore.match_file(value):
            return False
        absolute = self._root / Path(*path.parts)
        try:
            if absolute.stat().st_size > self._max_file_bytes:
                return False
            with absolute.open("rb") as stream:
                prefix = stream.read(8192)
        except OSError:
            return False
        return b"\0" not in prefix
