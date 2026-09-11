"""Models for tenant-scoped code search."""

from __future__ import annotations

from pydantic import BaseModel, Field


class GitRepositoryRef(BaseModel):
    git_url: str
    git_ref: str


class CodeReference(BaseModel):
    git_url: str
    git_ref: str
    commit_sha: str
    indexed_at: str
    path: str
    start_line: int
    end_line: int
    language: str
    artifact_type: str
    symbol_name: str | None = None
    symbol_kind: str | None = None
    content: str
    link: str
    score: float


class UnavailableRepository(BaseModel):
    repository: GitRepositoryRef
    reason: str


class SearchIndexedCodeResult(BaseModel):
    results: list[CodeReference] = Field(default_factory=list)
    unavailable_repositories: list[UnavailableRepository] = Field(default_factory=list)
