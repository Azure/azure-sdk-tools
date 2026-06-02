"""Knowledge configuration loader.

Parses knowledge-config.json and provides typed access to repository
configurations and documentation source definitions.
"""

from __future__ import annotations

import json
import logging
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Literal

logger = logging.getLogger(__name__)

_CONFIG_PATH = Path(__file__).resolve().parent.parent.parent / "config" / "knowledge-config.json"


# --- Data Models ---


@dataclass
class Metadata:
    scope: Literal["branded", "unbranded"] | None = None
    service_type: Literal["data-plane", "management-plane"] | None = None


@dataclass
class Override:
    pattern: str
    metadata: Metadata


@dataclass
class DocumentationPath:
    name: str
    description: str
    path: str | None = None
    folder: str | None = None
    file_name_lower_case: bool = False
    ignored_paths: list[str] = field(default_factory=list)
    relative_by_repo_path: bool = False
    is_generated: bool = False
    metadata: Metadata | None = None
    overrides: list[Override] = field(default_factory=list)


@dataclass
class Repository:
    url: str
    branch: str
    auth_type: Literal["public", "ssh", "token", "local"]
    path: str | None = None
    ssh_host: str | None = None
    token_env_var: str | None = None
    local_path_env: str | None = None


@dataclass
class Source:
    repository: Repository
    paths: list[DocumentationPath]


@dataclass
class KnowledgeConfig:
    version: str
    sources: list[Source]
    description: str | None = None


# --- Legacy flattened formats used by the orchestrator ---


@dataclass
class DocumentationSource:
    """Flattened representation of a documentation path with resolved filesystem path."""
    path: str
    folder: str
    file_name_lower_case: bool = False
    ignored_paths: list[str] = field(default_factory=list)
    is_generated: bool = False
    metadata: Metadata | None = None
    overrides: list[Override] = field(default_factory=list)


@dataclass
class RepositoryConfig:
    """Flattened repository config with resolved auth details."""
    name: str
    url: str
    path: str
    branch: str
    sparse_checkout: list[str] | None = None
    auth_type: str = "public"
    ssh_host: str | None = None
    token: str | None = None
    local_path: str | None = None


# --- Loader ---


class ConfigurationLoader:
    """Loads and transforms knowledge-config.json."""

    _config: KnowledgeConfig | None = None
    _config_path: Path = _CONFIG_PATH

    @classmethod
    def load_config(cls) -> KnowledgeConfig:
        if cls._config is not None:
            return cls._config

        config_content = cls._config_path.read_text(encoding="utf-8")
        raw = json.loads(config_content)
        cls._config = cls._parse_config(raw)
        logger.info(
            "Loaded config version %s with %d sources",
            cls._config.version,
            len(cls._config.sources),
        )
        return cls._config

    @classmethod
    def get_documentation_sources(cls) -> list[DocumentationSource]:
        """Transform config into legacy DocumentationSource list."""
        config = cls.load_config()
        sources: list[DocumentationSource] = []

        for source in config.sources:
            repo_path = source.repository.path or cls._get_repo_path_from_url(
                source.repository.url
            )

            for doc_path in source.paths:
                if doc_path.relative_by_repo_path or doc_path.path is None:
                    fs_path = f"docs/{repo_path}"
                else:
                    fs_path = f"docs/{repo_path}/{doc_path.path}"

                sources.append(
                    DocumentationSource(
                        path=fs_path,
                        folder=doc_path.folder or doc_path.name,
                        file_name_lower_case=doc_path.file_name_lower_case,
                        ignored_paths=doc_path.ignored_paths,
                        is_generated=doc_path.is_generated,
                        metadata=doc_path.metadata,
                        overrides=doc_path.overrides,
                    )
                )

        return sources

    @classmethod
    def get_repository_configs(cls) -> list[RepositoryConfig]:
        """Transform config into legacy RepositoryConfig list."""
        config = cls.load_config()
        repositories: list[RepositoryConfig] = []

        for source in config.sources:
            repo = source.repository
            repo_path = repo.path or cls._get_repo_path_from_url(repo.url)
            sparse_checkout = cls._calculate_sparse_checkout(source.paths)

            repositories.append(
                RepositoryConfig(
                    name=cls._get_repo_name_from_url(repo.url),
                    url=repo.url,
                    path=repo_path,
                    branch=repo.branch,
                    sparse_checkout=sparse_checkout or None,
                    auth_type=repo.auth_type,
                    ssh_host=repo.ssh_host,
                    token=(
                        os.environ.get(repo.token_env_var, "")
                        if repo.token_env_var
                        else None
                    ),
                    local_path=(
                        os.environ.get(repo.local_path_env, "")
                        if repo.local_path_env
                        else None
                    ),
                )
            )

        return repositories

    @classmethod
    def reload_config(cls) -> KnowledgeConfig:
        cls._config = None
        return cls.load_config()

    @classmethod
    def set_config_path(cls, path: Path) -> None:
        cls._config_path = path
        cls._config = None

    # --- Private helpers ---

    @classmethod
    def _parse_config(cls, raw: dict) -> KnowledgeConfig:
        sources = []
        for src in raw.get("sources", []):
            repo_raw = src["repository"]
            repository = Repository(
                url=repo_raw["url"],
                branch=repo_raw["branch"],
                auth_type=repo_raw.get("authType", "public"),
                path=repo_raw.get("path"),
                ssh_host=repo_raw.get("sshHost"),
                token_env_var=repo_raw.get("tokenEnvVar"),
                local_path_env=repo_raw.get("localPathEnv"),
            )

            paths = []
            for p in src.get("paths", []):
                metadata = None
                if "metadata" in p:
                    metadata = Metadata(
                        scope=p["metadata"].get("scope"),
                        service_type=p["metadata"].get("service_type"),
                    )

                overrides = []
                for o in p.get("overrides", []):
                    overrides.append(
                        Override(
                            pattern=o["pattern"],
                            metadata=Metadata(
                                scope=o["metadata"].get("scope"),
                                service_type=o["metadata"].get("service_type"),
                            ),
                        )
                    )

                paths.append(
                    DocumentationPath(
                        name=p.get("name", p.get("folder", "")),
                        description=p.get("description", ""),
                        path=p.get("path"),
                        folder=p.get("folder"),
                        file_name_lower_case=p.get("fileNameLowerCase", False),
                        ignored_paths=p.get("ignoredPaths", []),
                        relative_by_repo_path=p.get("relativeByRepoPath", False),
                        is_generated=p.get("isGenerated", False),
                        metadata=metadata,
                        overrides=overrides,
                    )
                )

            sources.append(Source(repository=repository, paths=paths))

        return KnowledgeConfig(
            version=raw.get("version", "1.0"),
            sources=sources,
            description=raw.get("description"),
        )

    @staticmethod
    def _get_repo_name_from_url(url: str) -> str:
        patterns = [
            r"/([^/]+)\.git$",
            r"/([^/]+)\.wiki\.git$",
            r"/([^/]+)$",
            r"_git/([^/]+)$",
        ]
        for pattern in patterns:
            m = re.search(pattern, url)
            if m:
                return m.group(1)
        segments = url.rstrip("/").split("/")
        return segments[-1] if segments else "unknown-repo"

    @staticmethod
    def _get_repo_path_from_url(url: str) -> str:
        return ConfigurationLoader._get_repo_name_from_url(url)

    @staticmethod
    def _calculate_sparse_checkout(paths: list[DocumentationPath]) -> list[str]:
        return [p.path for p in paths if p.path]
