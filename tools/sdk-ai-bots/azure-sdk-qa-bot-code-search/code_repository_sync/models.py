from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True, slots=True)
class SyncSettings:
    storage_endpoint: str
    storage_container: str
    blob_prefix: str
    work_root: Path
    max_file_bytes: int
    storage_concurrency: int


@dataclass(frozen=True, slots=True)
class RepositoryConfig:
    git_url: str
    git_ref: str
    owner: str
    repository: str
    path_prefixes: tuple[str, ...]
    include_patterns: tuple[str, ...]
    exclude_patterns: tuple[str, ...]

    @property
    def namespace(self) -> str:
        return f"{self.owner}/{self.repository}"


@dataclass(frozen=True, slots=True)
class SubmoduleSnapshot:
    path: str
    git_url: str
    commit: str

    def to_manifest(self) -> dict[str, str]:
        return {
            "path": self.path,
            "git_url": self.git_url,
            "commit_sha": self.commit,
        }


@dataclass(frozen=True, slots=True)
class RepositorySnapshot:
    root: Path
    commit: str
    submodules: tuple[SubmoduleSnapshot, ...]


@dataclass(frozen=True, slots=True)
class SelectedFile:
    path: str
    content: bytes
