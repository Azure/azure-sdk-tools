"""Metadata resolver — resolves scope and service_type for documents.

Uses glob pattern matching to apply file-level overrides on top of
path-level default metadata.
"""

from __future__ import annotations

import logging

from wcmatch import fnmatch

from src.services.configuration_loader import Metadata, Override

logger = logging.getLogger(__name__)


class MetadataResolver:
    """Resolves hierarchical metadata with file overrides."""

    @staticmethod
    def resolve_metadata(
        relative_path: str,
        path_metadata: Metadata | None,
        overrides: list[Override] | None = None,
    ) -> Metadata | None:
        """Resolve metadata for a file using path defaults and overrides.

        Args:
            relative_path: Relative path from source root
            path_metadata: Default metadata from DocumentationPath
            overrides: Per-file override rules

        Returns:
            Resolved Metadata, or None if no metadata applies
        """
        if path_metadata is None:
            return None

        # Start with path-level defaults
        resolved = Metadata(
            scope=path_metadata.scope,
            service_type=path_metadata.service_type,
        )

        # Apply overrides (last match wins for each field)
        if overrides:
            for override in overrides:
                if MetadataResolver._match_pattern(relative_path, override.pattern):
                    if override.metadata.scope:
                        resolved.scope = override.metadata.scope
                    if override.metadata.service_type:
                        resolved.service_type = override.metadata.service_type

        return resolved

    @staticmethod
    def validate_metadata(metadata: Metadata) -> bool:
        """Validate metadata structure."""
        if metadata.scope not in ("branded", "unbranded"):
            return False
        if metadata.service_type and metadata.service_type not in (
            "data-plane",
            "management-plane",
        ):
            return False
        if metadata.scope == "unbranded" and metadata.service_type:
            logger.warning("service_type is set for unbranded content and will be ignored")
        return True

    @staticmethod
    def _match_pattern(file_path: str, pattern: str) -> bool:
        """Match a file path against a glob pattern."""
        # Normalize path separators
        normalized_path = file_path.replace("\\", "/")
        normalized_pattern = pattern.replace("\\", "/")

        return fnmatch.fnmatch(
            normalized_path,
            normalized_pattern,
            flags=fnmatch.DOTMATCH | fnmatch.IGNORECASE | fnmatch.GLOBSTAR,
        )
