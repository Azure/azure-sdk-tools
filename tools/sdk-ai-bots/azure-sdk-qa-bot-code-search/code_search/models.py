from __future__ import annotations

from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True, slots=True)
class EmbeddingConfig:
    endpoint: str
    deployment: str
    model: str
    dimensions: int
    api_version: str


@dataclass(frozen=True, slots=True)
class BuilderSettings:
    search_endpoint: str
    search_alias: str
    search_index_name: str
    search_user_assigned_identity_resource_id: str
    storage_endpoint: str
    storage_container: str
    work_root: Path
    max_file_bytes: int
    embedding_concurrency: int
    validation_timeout_seconds: int
    embedding: EmbeddingConfig


@dataclass(frozen=True, slots=True)
class TenantRepositoryDemand:
    tenant_id: str
    git_url: str
    git_ref: str
    path_prefixes: tuple[str, ...]
    include_patterns: tuple[str, ...]
    exclude: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class RepositoryIndexConfig:
    git_url: str
    git_ref: str
    repository_key: str
    path_prefixes: tuple[str, ...]
    include_patterns: tuple[str, ...]
    exclude: tuple[str, ...]
    tenant_path_prefixes: tuple[tuple[str, tuple[str, ...]], ...]
    max_file_bytes: int
    index_identity: str

    def tenant_bindings(self) -> dict[str, tuple[str, ...]]:
        return dict(self.tenant_path_prefixes)


@dataclass(frozen=True, slots=True)
class PreparedChunk:
    logical_id: str
    path: str
    path_prefixes: tuple[str, ...]
    language: str
    artifact_type: str
    content: str
    content_hash: str
    start_line: int
    end_line: int
    symbol_name: str | None
    symbol_kind: str | None
    identifiers: tuple[str, ...]
    parser: str
    parser_failure: str | None


@dataclass(frozen=True, slots=True)
class CodeSearchDocument:
    chunk_id: str
    git_url: str
    git_ref: str
    valid_from_generation: int
    valid_to_generation: int
    path: str
    path_prefixes: tuple[str, ...]
    language: str
    artifact_type: str
    content: str
    content_vector: tuple[float, ...]
    content_hash: str
    start_line: int
    end_line: int
    symbol_name: str | None
    symbol_kind: str | None
    identifiers: tuple[str, ...]
    parser: str

    def target_identity(self) -> tuple[Any, ...]:
        return (
            self.git_url,
            self.git_ref,
            self.path,
            self.path_prefixes,
            self.language,
            self.artifact_type,
            self.content,
            self.content_vector,
            self.content_hash,
            self.start_line,
            self.end_line,
            self.symbol_name,
            self.symbol_kind,
            self.identifiers,
            self.parser,
        )

    def to_search_document(self) -> dict[str, Any]:
        value = asdict(self)
        value["path_prefixes"] = list(self.path_prefixes)
        value["content_vector"] = list(self.content_vector)
        value["identifiers"] = list(self.identifiers)
        return value


@dataclass(frozen=True, slots=True)
class ParserFailure:
    path: str
    message: str

    def to_dict(self) -> dict[str, str]:
        return asdict(self)


@dataclass(frozen=True, slots=True)
class GenerationManifest:
    schema_version: int
    git_url: str
    git_ref: str
    previous_commit_sha: str
    resolved_commit_sha: str
    tree_sha: str
    generation: int
    indexed_at: str
    cocoindex_version: str
    code_index_schema_version: int
    search_index_alias: str
    search_index_name: str
    embedding_provider: str
    embedding_model: str
    embedding_deployment: str
    embedding_dimensions: int
    chunker_version: str
    index_identity: str
    files: int
    chunks: int
    languages: dict[str, int]
    parser_failures: tuple[ParserFailure, ...]
    documents_added: int
    documents_updated: int
    documents_closed: int
    git_cache_sha256: str
    state_database_sha256: str

    def to_dict(self) -> dict[str, Any]:
        value = asdict(self)
        value["parser_failures"] = [failure.to_dict() for failure in self.parser_failures]
        return value
