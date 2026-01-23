# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for ingesting guidelines from the azure-sdk repository.

This module provides functionality to:
1. Detect changes in the azure-sdk repo guidelines using git commit comparison
2. Parse guidelines from markdown files
3. Compute content hashes for efficient change detection
4. Sync guidelines to Cosmos DB, only updating records where content differs
"""

from __future__ import annotations

import hashlib
import logging
import re
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Optional

import httpx
from src._database_manager import DatabaseManager
from src._models import Guideline
from src._search_manager import SearchManager
from src._settings import SettingsManager

logger = logging.getLogger(__name__)

# Azure SDK repository details
AZURE_SDK_OWNER = "Azure"
AZURE_SDK_REPO = "azure-sdk"
GUIDELINES_PATH_PREFIX = "docs/"

# App Configuration key for tracking sync state
LAST_SYNCED_COMMIT_SHA_KEY = "guidelines:lastSyncedCommitSha"

# Language mappings from folder names to language identifiers
LANGUAGE_FOLDER_MAP = {
    "python": "python",
    "java": "java",
    "dotnet": "dotnet",
    "typescript": "typescript",
    "golang": "golang",
    "cpp": "cpp",
    "rust": "rust",
    "ios": "ios",
    "android": "android",
    "clang": "clang",
    "general": None,  # Language-agnostic guidelines
}


@dataclass
class ParsedGuideline:
    """Represents a guideline parsed from markdown."""

    id: str
    title: str
    content: str
    language: Optional[str]
    anchor: str
    source_file_path: str


@dataclass
class SyncResult:
    """Result of a guideline sync operation."""

    created: list[str] = field(default_factory=list)
    updated: list[str] = field(default_factory=list)
    deleted: list[str] = field(default_factory=list)
    unchanged: list[str] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)

    @property
    def total_processed(self) -> int:
        return len(self.created) + len(self.updated) + len(self.deleted) + len(self.unchanged)

    def summary(self) -> str:
        return (
            f"Sync complete: {len(self.created)} created, {len(self.updated)} updated, "
            f"{len(self.deleted)} deleted, {len(self.unchanged)} unchanged, {len(self.errors)} errors"
        )


class GuidelineIngestor:
    """
    Handles ingestion of guidelines from the azure-sdk repository into Cosmos DB.

    Uses git commit comparison to detect changes efficiently, only processing
    files that have been modified since the last sync.
    """

    _instance: "GuidelineIngestor" = None

    @classmethod
    def get_instance(cls, force_new: bool = False) -> "GuidelineIngestor":
        """Returns a singleton instance of GuidelineIngestor."""
        if cls._instance is None or force_new:
            cls._instance = cls()
        return cls._instance

    def __init__(self):
        self._settings = SettingsManager()
        self._db = DatabaseManager.get_instance()
        self._timeout = 30
        self._client = httpx.Client(
            timeout=self._timeout,
            headers={
                "Accept": "application/vnd.github+json",
                "User-Agent": "APIView-Copilot/1.0",
            },
        )
        # Use GitHub token if available for higher rate limits
        github_token = self._settings.get("github_pat")
        if github_token:
            self._client.headers["Authorization"] = f"token {github_token}"

    def get_last_synced_commit_sha(self) -> Optional[str]:
        """Get the last synced commit SHA from App Configuration."""
        return self._settings.get(LAST_SYNCED_COMMIT_SHA_KEY)

    def set_last_synced_commit_sha(self, sha: str) -> None:
        """Update the last synced commit SHA in App Configuration."""
        self._settings.set(LAST_SYNCED_COMMIT_SHA_KEY, sha)

    def get_current_head_sha(self, branch: str = "main") -> str:
        """Get the current HEAD commit SHA from the azure-sdk repository."""
        url = f"https://api.github.com/repos/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/commits/{branch}"
        resp = self._client.get(url)
        resp.raise_for_status()
        return resp.json()["sha"]

    def get_changed_files(self, base_sha: str, head_sha: str) -> list[str]:
        """
        Get list of changed markdown files in the docs/ folder between two commits.

        Uses GitHub's compare API to efficiently determine which files changed.
        """
        url = f"https://api.github.com/repos/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/compare/{base_sha}...{head_sha}"
        resp = self._client.get(url)
        resp.raise_for_status()
        data = resp.json()

        changed_files = []
        for file_info in data.get("files", []):
            filename = file_info["filename"]
            # Only process markdown files in the docs/ folder
            if filename.startswith(GUIDELINES_PATH_PREFIX) and filename.endswith(".md"):
                changed_files.append(filename)

        return changed_files

    def get_all_guideline_files(self) -> list[str]:
        """
        Get all markdown files in the docs/ folder.

        Used for initial full sync or when --force is specified.
        """
        url = f"https://api.github.com/repos/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/git/trees/main?recursive=1"
        resp = self._client.get(url)
        resp.raise_for_status()
        tree = resp.json().get("tree", [])

        files = []
        for item in tree:
            if (
                item["type"] == "blob"
                and item["path"].startswith(GUIDELINES_PATH_PREFIX)
                and item["path"].endswith(".md")
            ):
                files.append(item["path"])

        return files

    def fetch_file_content(self, file_path: str, commit_sha: str) -> str:
        """Fetch the raw content of a file at a specific commit."""
        url = f"https://raw.githubusercontent.com/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/{commit_sha}/{file_path}"
        resp = self._client.get(url)
        resp.raise_for_status()
        return resp.text

    @staticmethod
    def compute_content_hash(content: str) -> str:
        """
        Compute SHA-256 hash of normalized content.

        Normalization:
        - Strip leading/trailing whitespace
        - Normalize line endings to LF
        - Collapse multiple blank lines to single blank line
        """
        normalized = content.strip()
        normalized = normalized.replace("\r\n", "\n").replace("\r", "\n")
        normalized = re.sub(r"\n{3,}", "\n\n", normalized)
        return hashlib.sha256(normalized.encode("utf-8")).hexdigest()

    def parse_guidelines_from_file(self, file_path: str, content: str) -> list[ParsedGuideline]:
        """
        Parse individual guidelines from a markdown file.

        Guidelines are identified by headers with anchors in the format:
        ## Header Text {#anchor-id}

        Each guideline's content extends from its header to the next header of same or higher level.
        """
        guidelines = []

        # Determine language from file path
        # e.g., "docs/python/design.md" -> "python"
        parts = file_path.split("/")
        language = None
        if len(parts) >= 2:
            folder = parts[1]  # e.g., "python", "java", "general"
            language = LANGUAGE_FOLDER_MAP.get(folder)

        # Extract filename without extension for ID prefix
        filename = parts[-1].replace(".md", "") if parts else "unknown"

        # Pattern to match headers with explicit anchors: ## Text {#anchor}
        # Also match DO/DO NOT/SHOULD/SHOULD NOT/MAY/MUST patterns which are common in guidelines
        header_pattern = re.compile(r"^(#{1,6})\s+(.+?)\s*\{#([a-zA-Z0-9_-]+)\}\s*$", re.MULTILINE)

        matches = list(header_pattern.finditer(content))

        for i, match in enumerate(matches):
            header_level = len(match.group(1))
            title = match.group(2).strip()
            anchor = match.group(3)

            # Content starts after this header and ends at the next header of same or higher level
            start_pos = match.end()
            end_pos = len(content)

            # Find the next header of same or higher level
            for next_match in matches[i + 1 :]:
                next_level = len(next_match.group(1))
                if next_level <= header_level:
                    end_pos = next_match.start()
                    break

            guideline_content = content[start_pos:end_pos].strip()

            # Skip empty guidelines
            if not guideline_content:
                continue

            # Create ID in format: {filename}=html={anchor}
            # This matches the existing ID format used in the search index
            guideline_id = f"{language}_{filename}=html={anchor}" if language else f"{filename}=html={anchor}"

            guidelines.append(
                ParsedGuideline(
                    id=guideline_id,
                    title=title,
                    content=guideline_content,
                    language=language,
                    anchor=anchor,
                    source_file_path=file_path,
                )
            )

        return guidelines

    def sync_guidelines(
        self,
        *,
        dry_run: bool = False,
        force: bool = False,
        language_filter: Optional[str] = None,
    ) -> SyncResult:
        """
        Synchronize guidelines from the azure-sdk repository to Cosmos DB.

        Args:
            dry_run: If True, report changes without writing to the database.
            force: If True, ignore the last synced SHA and process all files.
            language_filter: If specified, only process guidelines for this language.

        Returns:
            SyncResult with details of what was created, updated, deleted, or unchanged.
        """
        result = SyncResult()

        # Get current HEAD and last synced SHA
        current_sha = self.get_current_head_sha()
        last_sha = None if force else self.get_last_synced_commit_sha()

        logger.info(f"Current HEAD: {current_sha}")
        logger.info(f"Last synced: {last_sha or '(none - full sync)'}")

        # Determine which files to process
        if last_sha and last_sha != current_sha:
            files_to_process = self.get_changed_files(last_sha, current_sha)
            logger.info(f"Incremental sync: {len(files_to_process)} files changed")
        else:
            files_to_process = self.get_all_guideline_files()
            logger.info(f"Full sync: {len(files_to_process)} files to process")

        # Filter by language if specified
        if language_filter:
            files_to_process = [
                f
                for f in files_to_process
                if f.startswith(f"{GUIDELINES_PATH_PREFIX}{language_filter}/")
                or f.startswith(f"{GUIDELINES_PATH_PREFIX}general/")
            ]
            logger.info(f"After language filter: {len(files_to_process)} files")

        if not files_to_process:
            logger.info("No files to process")
            if not dry_run:
                self.set_last_synced_commit_sha(current_sha)
            return result

        # Track all guideline IDs we've seen from changed files (for deletion detection)
        seen_ids_by_file: dict[str, set[str]] = {}

        # Process each file
        for file_path in files_to_process:
            try:
                logger.info(f"Processing: {file_path}")
                content = self.fetch_file_content(file_path, current_sha)
                parsed_guidelines = self.parse_guidelines_from_file(file_path, content)

                seen_ids_by_file[file_path] = set()

                for parsed in parsed_guidelines:
                    seen_ids_by_file[file_path].add(parsed.id)

                    # Compute hash of the new content
                    new_hash = self.compute_content_hash(parsed.content)

                    # Check if guideline exists and compare hash
                    try:
                        existing = self._db.guidelines.get(parsed.id)
                        existing_hash = existing.get("content_hash")

                        if existing_hash == new_hash:
                            result.unchanged.append(parsed.id)
                            continue

                        # Content changed - update
                        if not dry_run:
                            guideline = Guideline(
                                id=parsed.id,
                                title=parsed.title,
                                content=parsed.content,
                                language=parsed.language,
                                content_hash=new_hash,
                                source_file_path=parsed.source_file_path,
                                source_commit_sha=current_sha,
                                last_synced_at=datetime.now(timezone.utc),
                                # Preserve existing relationships
                                related_guidelines=existing.get("related_guidelines", []),
                                related_examples=existing.get("related_examples", []),
                                related_memories=existing.get("related_memories", []),
                                tags=existing.get("tags"),
                            )
                            self._db.guidelines.upsert(parsed.id, data=guideline, run_indexer=False)
                        result.updated.append(parsed.id)

                    except Exception:
                        # Guideline doesn't exist - create
                        if not dry_run:
                            guideline = Guideline(
                                id=parsed.id,
                                title=parsed.title,
                                content=parsed.content,
                                language=parsed.language,
                                content_hash=new_hash,
                                source_file_path=parsed.source_file_path,
                                source_commit_sha=current_sha,
                                last_synced_at=datetime.now(timezone.utc),
                            )
                            self._db.guidelines.create(parsed.id, data=guideline, run_indexer=False)
                        result.created.append(parsed.id)

            except Exception as e:
                logger.error(f"Error processing {file_path}: {e}")
                result.errors.append(f"{file_path}: {e}")

        # Handle deletions: find guidelines that were in changed files but are no longer present
        for file_path, seen_ids in seen_ids_by_file.items():
            try:
                # Query existing guidelines from this file
                query = f"SELECT c.id FROM c WHERE c.source_file_path = '{file_path}' AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)"
                existing_items = list(
                    self._db.guidelines.client.query_items(query=query, enable_cross_partition_query=True)
                )

                for item in existing_items:
                    if item["id"] not in seen_ids:
                        if not dry_run:
                            self._db.guidelines.delete(item["id"], run_indexer=False)
                        result.deleted.append(item["id"])

            except Exception as e:
                logger.error(f"Error handling deletions for {file_path}: {e}")
                result.errors.append(f"deletion check {file_path}: {e}")

        # Run indexer once at the end (if not dry run)
        if not dry_run and (result.created or result.updated or result.deleted):
            logger.info("Running search indexer...")
            SearchManager.run_indexers(["guidelines"])

        # Update the last synced SHA
        if not dry_run:
            self.set_last_synced_commit_sha(current_sha)

        logger.info(result.summary())
        return result
