# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for ingesting guidelines from the azure-sdk repository.

This module provides functionality to:
1. Detect changes in the azure-sdk repo guidelines using git commit comparison
2. Parse guidelines from markdown files using BeautifulSoup (similar to original archagent-ai process)
3. Extract examples from guidelines using LLM parsing
4. Compute content hashes for efficient change detection
5. Sync guidelines and examples to Cosmos DB, only updating records where content differs

The ingestion process follows the original archagent-ai approach:
- Step 1: Preprocess markdown files to extract guidelines with Jekyll-style requirement tags
- Step 2: Use LLM to parse guidelines into structured format with examples
"""

from __future__ import annotations

import hashlib
import json
import logging
import re
from collections import Counter
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Dict, List, Optional, Tuple

import httpx
import markdown_it
from bs4 import BeautifulSoup

MARKDOWN_IT = markdown_it.MarkdownIt()

from src._database_manager import DatabaseManager
from src._models import Example, ExampleType, Guideline, Memory
from src._prompt_runner import run_prompt
from src._search_manager import SearchManager
from src._settings import SettingsManager
from src._utils import guideline_id_to_db

logger = logging.getLogger(__name__)

# Azure SDK repository details
AZURE_SDK_OWNER = "Azure"
AZURE_SDK_REPO = "azure-sdk"
GUIDELINES_PATH_PREFIX = "docs/"

# App Configuration key for tracking sync state
LAST_SYNCED_COMMIT_SHA_KEY = "guidelines:last_synced_commit_sha"

# Batch size for LLM parsing (matches original archagent-ai process)
LLM_BATCH_SIZE = 10

# Files to parse from the azure-sdk repo (matches original process)
FILES_TO_PARSE = [
    "design.md",
    "implementation.md",
    "introduction.md",
    "azurecore.md",
    "compatibility.md",
    "documentation.md",
    "spring.md",
]

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

# ============================================================================
# Jekyll/Markdown Tag Patterns (from original archagent-ai preprocess_guidelines.py)
# ============================================================================

# Requirement tag patterns with IDs
MAY_PATTERN = r'{% include requirement/MAY\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
MAY_REPLACE = "YOU MAY"
MUST_DO_PATTERN = r'{% include requirement/MUST\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
MUST_NO_ID_PATTERN = r"{% include requirement/MUST %}"
MUST_DO_REPLACE = "DO"
MUST_NOT_PATTERN = r'{% include requirement/MUSTNOT\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
MUST_NOT_REPLACE = "DO NOT"
SHOULD_PATTERN = r'{% include requirement/SHOULD\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
SHOULD_NO_ID_PATTERN = r"{% include requirement/SHOULD %}"
SHOULD_REPLACE = "YOU SHOULD"
SHOULD_NOT_PATTERN = r'{% include requirement/SHOULDNOT\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
SHOULD_NOT_REPLACE = "YOU SHOULD NOT"

# Include patterns
INCLUDE_PATTERN = r"{%\s*(include|include_relative)\s*([^\s%}]+)\s*%}"
INCLUDE_NOTE_PATTERN = r'{% include note.html content=\\?"([^\\]+)\\?" %}'
INCLUDE_NOTE_REPLACE = r"**NOTE:** \1"
INCLUDE_DRAFT_PATTERN = r'{% include draft.html content=\\?"([^\\]+)\\?" %}'
INCLUDE_DRAFT_REPLACE = r"**DRAFT:** \1"
INCLUDE_IMPORTANT_PATTERN = r'{% include important.html content=\\?"([^\\]+)\\?" %}'
INCLUDE_IMPORTANT_REPLACE = r"**IMPORTANT:** \1"

# Icon pattern (to strip)
ICON_PATTERN = r"^:[a-z_]+: "
ICON_REPLACE = ""


@dataclass
class ParsedGuideline:
    """Represents a guideline parsed from markdown (intermediate format)."""

    id: str
    text: str
    language: Optional[str]
    source_file_path: str


@dataclass
class ParsedExample:
    """Represents an example parsed from a guideline."""

    id: str
    title: str
    content: str
    example_type: str  # "good" or "bad"
    guideline_ids: List[str]
    language: Optional[str]
    source_file_path: str


@dataclass
class SyncDetail:
    """Before/after detail for a single changed item."""

    id: str
    kind: str  # "guideline" or "example"
    action: str  # "created", "updated", or "deleted"
    before: Optional[str]
    after: Optional[str]


@dataclass
class SyncResult:
    """Result of a guideline sync operation."""

    guidelines_created: list[str] = field(default_factory=list)
    guidelines_updated: list[str] = field(default_factory=list)
    guidelines_deleted: list[str] = field(default_factory=list)
    guidelines_unchanged: list[str] = field(default_factory=list)
    examples_created: list[str] = field(default_factory=list)
    examples_updated: list[str] = field(default_factory=list)
    examples_deleted: list[str] = field(default_factory=list)
    examples_unchanged: list[str] = field(default_factory=list)
    memories_absorbed: list[str] = field(default_factory=list)
    memories_retained: list[str] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    details: list[SyncDetail] = field(default_factory=list)

    @property
    def total_guidelines(self) -> int:
        return (
            len(self.guidelines_created)
            + len(self.guidelines_updated)
            + len(self.guidelines_deleted)
            + len(self.guidelines_unchanged)
        )

    @property
    def total_examples(self) -> int:
        return (
            len(self.examples_created)
            + len(self.examples_updated)
            + len(self.examples_deleted)
            + len(self.examples_unchanged)
        )

    @property
    def total_memories(self) -> int:
        return len(self.memories_absorbed) + len(self.memories_retained)

    def summary(self) -> str:
        parts = [
            "Sync complete: ",
            f"Guidelines: {len(self.guidelines_created)} created, {len(self.guidelines_updated)} updated, "
            f"{len(self.guidelines_deleted)} deleted, {len(self.guidelines_unchanged)} unchanged | ",
            f"Examples: {len(self.examples_created)} created, {len(self.examples_updated)} updated, "
            f"{len(self.examples_deleted)} deleted, {len(self.examples_unchanged)} unchanged",
        ]
        if self.memories_absorbed or self.memories_retained:
            parts.append(
                f" | Memories: {len(self.memories_absorbed)} absorbed, {len(self.memories_retained)} retained"
            )
        parts.append(f" | {len(self.errors)} errors")
        return "".join(parts)


class GuidelineIngestor:
    """
    Handles ingestion of guidelines from the azure-sdk repository into Cosmos DB.

    Follows the original archagent-ai process:
    1. Preprocess markdown files using BeautifulSoup to extract guidelines with Jekyll-style requirement tags
    2. Optionally use LLM to parse guidelines into structured format with examples
    3. Sync to Cosmos DB with change detection via content hashing

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

    # ========================================================================
    # Git/GitHub Operations
    # ========================================================================

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
        Only returns files that match FILES_TO_PARSE.
        """
        url = f"https://api.github.com/repos/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/compare/{base_sha}...{head_sha}"
        resp = self._client.get(url)
        resp.raise_for_status()
        data = resp.json()

        changed_files = []
        for file_info in data.get("files", []):
            filename = file_info["filename"]
            # Only process specific markdown files in the docs/ folder
            if filename.startswith(GUIDELINES_PATH_PREFIX) and filename.endswith(".md"):
                # Check if the filename (without path) is in our list of files to parse
                base_name = filename.split("/")[-1]
                if base_name in FILES_TO_PARSE:
                    changed_files.append(filename)

        return changed_files

    @staticmethod
    def _file_matches_language_folders(file_path: str, allowed_folders: set) -> bool:
        """Return True if *file_path* belongs to one of the *allowed_folders* under ``docs/``."""
        parts = file_path.replace("\\", "/").split("/")
        try:
            docs_idx = parts.index("docs")
            if docs_idx + 1 < len(parts):
                return parts[docs_idx + 1] in allowed_folders
        except ValueError:
            pass
        return False

    def fetch_file_content(self, file_path: str, commit_sha: str) -> str:
        """Fetch the raw content of a file at a specific commit."""
        url = f"https://raw.githubusercontent.com/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/{commit_sha}/{file_path}"
        resp = self._client.get(url)
        resp.raise_for_status()
        return resp.text

    # ========================================================================
    # Markdown Preprocessing (BeautifulSoup-based, from archagent-ai)
    # ========================================================================

    @staticmethod
    def _extract_id_from_inline(item) -> Optional[str]:
        """Extract the id from inline text like {% include requirement/MUST id="some-id" %}."""
        text = item.text if hasattr(item, "text") else str(item)
        id_match = re.search(r'id="([a-zA-Z0-9_-]+)"', text)
        if id_match:
            return id_match.group(1)
        try:
            # Try to get id from anchor element
            id_val = item.next_element.attrs.get("name") if hasattr(item, "next_element") else None
            return id_val
        except (AttributeError, TypeError):
            return None

    @staticmethod
    def _split_tags(item, file_path: str) -> Tuple[str, Optional[str]]:
        """
        Split the tag from the ID and replace Jekyll requirement tags with readable text.

        Returns tuple of (processed_text, guideline_id).
        """
        text = item.text if hasattr(item, "text") else str(item)
        guideline_id = GuidelineIngestor._extract_id_from_inline(item)

        # Replace Jekyll requirement tags with readable text
        text = re.sub(MAY_PATTERN, MAY_REPLACE, text)
        text = re.sub(MUST_DO_PATTERN, MUST_DO_REPLACE, text)
        text = re.sub(MUST_NO_ID_PATTERN, MUST_DO_REPLACE, text)
        text = re.sub(MUST_NOT_PATTERN, MUST_NOT_REPLACE, text)
        text = re.sub(SHOULD_PATTERN, SHOULD_REPLACE, text)
        text = re.sub(SHOULD_NO_ID_PATTERN, SHOULD_REPLACE, text)
        text = re.sub(SHOULD_NOT_PATTERN, SHOULD_NOT_REPLACE, text)
        text = re.sub(ICON_PATTERN, ICON_REPLACE, text)
        text = re.sub(INCLUDE_NOTE_PATTERN, INCLUDE_NOTE_REPLACE, text)
        text = re.sub(INCLUDE_IMPORTANT_PATTERN, INCLUDE_IMPORTANT_REPLACE, text)
        text = re.sub(INCLUDE_DRAFT_PATTERN, INCLUDE_DRAFT_REPLACE, text)

        # Build the guideline ID in format: {language}_{filename}=html={id}
        # e.g., python_design=html=some-id
        segments = file_path.replace("\\", "/").split("/")
        try:
            docs_idx = segments.index("docs")
            relevant_segments = segments[docs_idx + 1 :]
        except ValueError:
            relevant_segments = segments

        prefix = "_".join(relevant_segments).replace(".md", "=html")
        full_id = f"{prefix}={guideline_id}" if guideline_id else None

        return text, full_id

    @staticmethod
    def _add_links(text: str, item) -> str:
        """Find any links associated with the text and add them in format: text (link)."""
        if not hasattr(item, "find_all"):
            return text

        links = [link for link in item.find_all("a") if link.get("href", "").startswith("http")]
        if not links:
            return text

        for link in links:
            index = text.find(link.text)
            if index == -1:
                continue
            text = f"{text[:index]}{link.text} ({link['href']}){text[index + len(link.text):]}"
        return text

    @staticmethod
    def _convert_code_tag_to_markdown(html: str) -> str:
        """Convert HTML code tag to markdown code block."""
        code_tag_pattern = r'<code class="language-(.+)">([\s\S]*?)</code>'
        match = re.search(code_tag_pattern, html)
        if match:
            language = match[1]
            code = match[2]
            return f"```{language}\n{code}\n```"
        return html

    def parse_markdown_file(self, file_path: str, content: str) -> List[ParsedGuideline]:
        """
        Parse a markdown file to extract guidelines using BeautifulSoup.

        This follows the original archagent-ai preprocessing approach:
        - Renders markdown to HTML using markdown-it
        - Uses BeautifulSoup to parse HTML structure
        - Extracts guidelines from list items and paragraphs with requirement IDs
        """
        # Determine language from file path
        parts = file_path.replace("\\", "/").split("/")
        language = None
        if len(parts) >= 2:
            try:
                docs_idx = parts.index("docs")
                if docs_idx + 1 < len(parts):
                    folder = parts[docs_idx + 1]
                    language = LANGUAGE_FOLDER_MAP.get(folder)
            except ValueError:
                pass

        entries: List[ParsedGuideline] = []

        # Render markdown to HTML
        html = MARKDOWN_IT.render(content)
        soup = BeautifulSoup(html, features="html.parser")

        for item in soup.find_all():
            if item.name == "p":
                text, guideline_id = self._split_tags(item, file_path)
                text = self._add_links(text, item)

                if guideline_id:
                    entries.append(
                        ParsedGuideline(
                            id=guideline_id,
                            text=text,
                            language=language,
                            source_file_path=file_path,
                        )
                    )
                else:
                    # Try to append orphan paragraphs to the previous guideline
                    if entries:
                        entries[-1].text += "\n\n" + text

            elif item.name == "pre":
                # Handle code blocks
                raw_html = "".join(str(tag) for tag in item.contents)
                markdown_text = self._convert_code_tag_to_markdown(raw_html)
                if entries:
                    entries[-1].text += "\n\n" + markdown_text

            elif item.name in ["ol", "ul"]:
                # Handle list items (common location for requirement IDs)
                list_items = item.find_all("li")
                for li in list_items:
                    item_text, guideline_id = self._split_tags(li, file_path)
                    item_text = self._add_links(item_text, li)

                    if guideline_id:
                        entries.append(
                            ParsedGuideline(
                                id=guideline_id,
                                text=item_text,
                                language=language,
                                source_file_path=file_path,
                            )
                        )
                    else:
                        # Append to previous guideline
                        if entries:
                            entries[-1].text += "\n" + item_text

        return entries

    # ========================================================================
    # Content Hashing and Validation
    # ========================================================================

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

    @staticmethod
    def check_id_format(items: List[ParsedGuideline]) -> List[str]:
        """
        Ensures ids are compatible with Azure AI Search.
        Returns a list of malformed ids.
        """
        malformed = []
        pattern = re.compile(r"^[A-Za-z0-9_=-]{1,1024}$")
        for item in items:
            if not item.id:
                malformed.append(f"(missing id for text: {item.text[:50]}...)")
            elif not bool(pattern.fullmatch(item.id)):
                malformed.append(item.id)
        return malformed

    @staticmethod
    def filter_duplicates(items: List[ParsedGuideline]) -> Dict[str, List[ParsedGuideline]]:
        """
        Filter out duplicate guidelines.

        Returns dict with 'good' (valid unique items) and 'bad' (duplicates with different content).
        """
        filtered = []
        bad_filtered = []
        counter = Counter(x.id for x in items)
        duplicates = [x for x, count in counter.items() if count > 1]
        bad_ids = set()
        copied_ids = set()

        # Determine which ids are exact copies vs improperly reused
        for dup_id in duplicates:
            matches = [x for x in items if x.id == dup_id]
            match_set = set(x.text for x in matches)
            if len(match_set) == 1:
                copied_ids.add(dup_id)
            else:
                bad_ids.add(dup_id)

        # Filter out bad ids and all but one of copied ids
        final_ids = set()
        for item in items:
            item_id = item.id
            if item_id in bad_ids:
                bad_filtered.append(item)
            elif item_id in copied_ids and item_id in final_ids:
                continue  # Skip duplicate copies
            else:
                filtered.append(item)
                final_ids.add(item_id)

        return {"good": filtered, "bad": bad_filtered}

    # ========================================================================
    # Main Sync Method
    # ========================================================================

    def sync_guidelines(
        self,
        *,
        dry_run: bool = False,
        details: bool = False,
        base_sha: str,
        target_sha: str,
        languages: Optional[List[str]] = None,
    ) -> SyncResult:
        """
        Synchronize guidelines from the azure-sdk repository to Cosmos DB.

        Args:
            dry_run: If True, report changes without writing to the database.
            details: If True, include before/after content for each changed item.
            base_sha: The baseline commit SHA to compare against.
            target_sha: The target commit SHA to sync to.
            languages: If provided, only process guideline files for these languages (canonical names).

        Returns:
            SyncResult with details of what was created, updated, deleted, or unchanged.
        """
        result = SyncResult()

        current_sha = target_sha
        print(f"Target SHA: {current_sha}")
        last_sha = base_sha
        print(f"Base SHA: {last_sha}")

        # Validate SHA relationship
        if last_sha == current_sha:
            raise ValueError(f"base_sha and target_sha are identical ({current_sha[:8]}). Nothing to sync.")

        # Verify base is an ancestor of target using the compare API
        compare_url = f"https://api.github.com/repos/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/compare/{last_sha}...{current_sha}"
        compare_resp = self._client.get(compare_url)
        compare_resp.raise_for_status()
        compare_data = compare_resp.json()
        compare_status = compare_data.get("status")
        if compare_status == "behind":
            raise ValueError(
                f"base_sha ({last_sha[:8]}) is ahead of target_sha ({current_sha[:8]}). "
                "base_sha must be an ancestor of target_sha."
            )
        if compare_status == "identical":
            raise ValueError(f"base_sha and target_sha resolve to identical trees. Nothing to sync.")

        # Determine which files changed
        print(f"Comparing commits: {last_sha[:8]}...{current_sha[:8]}")
        files_to_process = self.get_changed_files(last_sha, current_sha)
        print(f"Incremental sync: {len(files_to_process)} files changed")
        if files_to_process:
            for f in files_to_process:
                print(f"  Changed: {f}")

        # Apply language filter if specified
        if languages:
            # Build the set of allowed folder names from LANGUAGE_FOLDER_MAP
            allowed_folders = set()
            for folder, lang in LANGUAGE_FOLDER_MAP.items():
                if lang in languages:
                    allowed_folders.add(folder)
            # Also include "general" since language-agnostic guidelines apply to all
            allowed_folders.add("general")

            before_count = len(files_to_process)
            files_to_process = [
                f for f in files_to_process
                if self._file_matches_language_folders(f, allowed_folders)
            ]
            print(f"Language filter ({', '.join(languages)}): {before_count} -> {len(files_to_process)} files")

        if not files_to_process:
            print("No files to process")
            if not dry_run:
                self.set_last_synced_commit_sha(current_sha)
            return result

        return self._sync_incremental(files_to_process, last_sha, current_sha, dry_run, details, result)

    def _parse_files_at_sha(
        self, files: List[str], commit_sha: str, label: str
    ) -> Tuple[List[ParsedGuideline], List[str]]:
        """Parse guideline files at a specific commit SHA."""
        print(f"\nParsing {len(files)} files from {label} ({commit_sha[:8]})...")
        all_guidelines: List[ParsedGuideline] = []
        errors: List[str] = []

        for file_path in files:
            try:
                content = self.fetch_file_content(file_path, commit_sha)
                parsed = self.parse_markdown_file(file_path, content)
                all_guidelines.extend(parsed)
            except httpx.HTTPStatusError as e:
                # File might not exist at this SHA (e.g., newly added file checked against base)
                if e.response.status_code == 404:
                    continue
                logger.error("Error fetching %s at %s: %s", file_path, commit_sha[:8], e)
                errors.append(f"parse {file_path}: {e}")
            except Exception as e:
                logger.error("Error parsing %s at %s: %s", file_path, commit_sha[:8], e)
                errors.append(f"parse {file_path}: {e}")

        # Filter out guidelines without valid IDs
        all_guidelines = [g for g in all_guidelines if g.id]
        duplicates_result = self.filter_duplicates(all_guidelines)
        good_guidelines = duplicates_result["good"]

        print(f"  Found {len(good_guidelines)} valid guidelines")
        return good_guidelines, errors

    def _sync_incremental(
        self,
        files_to_process: List[str],
        base_sha: str,
        target_sha: str,
        dry_run: bool,
        details: bool,
        result: SyncResult,
    ) -> SyncResult:
        """Perform incremental sync by comparing guidelines between two SHAs."""
        # Parse guidelines from both base and target SHAs
        base_guidelines, base_errors = self._parse_files_at_sha(files_to_process, base_sha, "base")
        target_guidelines, target_errors = self._parse_files_at_sha(files_to_process, target_sha, "target")

        result.errors.extend(base_errors)
        result.errors.extend(target_errors)

        # Build lookup maps: id -> (guideline, content_hash)
        base_map: Dict[str, Tuple[ParsedGuideline, str]] = {}
        for g in base_guidelines:
            base_map[g.id] = (g, self.compute_content_hash(g.text))

        target_map: Dict[str, Tuple[ParsedGuideline, str]] = {}
        for g in target_guidelines:
            target_map[g.id] = (g, self.compute_content_hash(g.text))

        # Compare: find created, updated, deleted, unchanged
        all_ids = set(base_map.keys()) | set(target_map.keys())

        print(f"\nComparing {len(all_ids)} unique guideline IDs...")

        # First pass: identify which guidelines changed
        changed_guidelines: List[ParsedGuideline] = []
        changed_guideline_ids: set[str] = set()

        for gid in all_ids:
            in_base = gid in base_map
            in_target = gid in target_map

            if in_target and not in_base:
                changed_guidelines.append(target_map[gid][0])
                changed_guideline_ids.add(gid)
            elif in_base and in_target:
                if base_map[gid][1] != target_map[gid][1]:
                    changed_guidelines.append(target_map[gid][0])
                    changed_guideline_ids.add(gid)

        # Run LLM enrichment on changed guidelines only
        enriched_map: Dict[str, dict] = {}
        all_examples: List[ParsedExample] = []
        if changed_guidelines:
            enriched_list, all_examples = self._parse_guidelines_with_llm(changed_guidelines, result)
            for item in enriched_list:
                enriched_map[item["id"]] = item

        # Second pass: apply changes
        for gid in all_ids:
            in_base = gid in base_map
            in_target = gid in target_map
            enriched = enriched_map.get(gid)

            if in_target and not in_base:
                # New guideline
                result.guidelines_created.append(gid)
                print(f"  Created: {gid}")
                if details:
                    after_content = enriched.get("content", target_map[gid][0].text) if enriched else target_map[gid][0].text
                    result.details.append(SyncDetail(id=gid, kind="guideline", action="created", before=None, after=after_content))
                if not dry_run:
                    self._upsert_guideline(target_map[gid][0], target_map[gid][1], target_sha, enriched=enriched)

            elif in_base and not in_target:
                # Deleted guideline
                result.guidelines_deleted.append(gid)
                print(f"  Deleted: {gid}")
                if details:
                    result.details.append(SyncDetail(id=gid, kind="guideline", action="deleted", before=base_map[gid][0].text, after=None))
                if not dry_run:
                    try:
                        self._db.guidelines.delete(gid, run_indexer=False)
                    except Exception:
                        pass  # Might not exist in DB

            else:
                # Exists in both - compare hashes
                base_hash = base_map[gid][1]
                target_hash = target_map[gid][1]

                if base_hash == target_hash:
                    result.guidelines_unchanged.append(gid)
                else:
                    result.guidelines_updated.append(gid)
                    print(f"  Updated: {gid}")
                    if details:
                        after_content = enriched.get("content", target_map[gid][0].text) if enriched else target_map[gid][0].text
                        result.details.append(SyncDetail(id=gid, kind="guideline", action="updated", before=base_map[gid][0].text, after=after_content))
                    # Verify guideline exists in database
                    try:
                        existing = self._db.guidelines.get(gid)
                        print(f"    Found in DB: {existing.get('id')} - {existing.get('title', '(no title)')[:60]}")
                    except Exception:
                        print("    WARNING: Not found in database - will be created")
                    if not dry_run:
                        self._upsert_guideline(target_map[gid][0], target_hash, target_sha, enriched=enriched)

        # Run indexer once at the end (if not dry run)
        if not dry_run and (result.guidelines_created or result.guidelines_updated or result.guidelines_deleted):
            print("Running search indexer...")
            SearchManager.run_indexers(["guidelines"])

        # Sync examples (include deleted guideline IDs so their examples get cleaned up)
        example_sync_ids = changed_guideline_ids | set(result.guidelines_deleted)
        if all_examples or result.guidelines_deleted:
            self._sync_examples(all_examples, target_sha, example_sync_ids, dry_run, details, result)

        # Reconcile memories for changed guidelines
        changed_set = set(result.guidelines_created) | set(result.guidelines_updated)
        self._reconcile_memories(changed_set, enriched_map, dry_run, result)

        # Update the last synced SHA
        if not dry_run:
            self.set_last_synced_commit_sha(target_sha)

        return result

    def _upsert_guideline(
        self,
        parsed: ParsedGuideline,
        content_hash: str,
        commit_sha: str,
        existing: Optional[dict] = None,
        enriched: Optional[dict] = None,
    ) -> None:
        """Create or update a guideline in the database.

        Args:
            parsed: The raw parsed guideline.
            content_hash: SHA-256 hash of normalized content.
            commit_sha: Git commit SHA.
            existing: Existing DB record (for preserving relationship fields).
            enriched: LLM-enriched guideline dict (with title, tags, related_guidelines).
        """
        # Use LLM-enriched title/tags/content if available, otherwise derive from raw text
        if enriched:
            title = enriched.get("title", "")[:100] or "Untitled Guideline"
            content = enriched.get("content") or parsed.text
            tags = enriched.get("tags")
            related_guidelines = enriched.get("related_guidelines", [])
            # Merge with existing related_examples — LLM examples are synced separately
            related_examples = existing.get("related_examples", []) if existing else []
        else:
            title_lines = parsed.text.strip().split("\n")
            title = title_lines[0][:100] if title_lines else "Untitled Guideline"
            title = re.sub(r"(DO|YOU SHOULD|YOU MAY|DO NOT|YOU SHOULD NOT)\s*", "", title).strip()
            content = parsed.text
            tags = existing.get("tags") if existing else None
            related_guidelines = existing.get("related_guidelines", []) if existing else []
            related_examples = existing.get("related_examples", []) if existing else []

        guideline = Guideline(
            id=parsed.id,
            title=title,
            content=content,
            language=parsed.language,
            content_hash=content_hash,
            source_file_path=parsed.source_file_path,
            source_commit_sha=commit_sha,
            last_synced_at=datetime.now(timezone.utc),
            related_guidelines=related_guidelines,
            related_examples=related_examples,
            related_memories=existing.get("related_memories", []) if existing else [],
            tags=tags,
        )
        self._db.guidelines.upsert(parsed.id, data=guideline, run_indexer=False)

    # ========================================================================
    # LLM-based Example Extraction
    # ========================================================================

    def _parse_guidelines_with_llm(
        self,
        guidelines: List[ParsedGuideline],
        result: SyncResult,
    ) -> Tuple[List[dict], List[ParsedExample]]:
        """
        Use LLM to enrich guidelines and extract code examples.

        Follows the original archagent-ai Step 2 process:
        - Sends batches of guidelines to the LLM
        - LLM returns enriched guidelines (with title, tags, clarity) and extracted examples
        - Examples are code blocks separated into good/bad classifications

        Args:
            guidelines: Raw parsed guidelines from Step 1.
            result: SyncResult to append errors to.

        Returns:
            Tuple of (enriched_guidelines_dicts, parsed_examples).
        """
        enriched_guidelines: List[dict] = []
        all_examples: List[ParsedExample] = []

        batch_count = (len(guidelines) + LLM_BATCH_SIZE - 1) // LLM_BATCH_SIZE
        print(f"\nParsing {len(guidelines)} guidelines with LLM in {batch_count} batches...")

        for batch_idx in range(batch_count):
            start = batch_idx * LLM_BATCH_SIZE
            end = min(start + LLM_BATCH_SIZE, len(guidelines))
            batch = guidelines[start:end]

            # Convert batch to JSON input format (matches original archagent-ai)
            batch_input = [{"id": g.id, "text": g.text} for g in batch]

            try:
                result_str = run_prompt(
                    folder="other",
                    filename="parse_guidelines",
                    inputs={"question": json.dumps(batch_input)},
                    max_retries=3,
                    logger=logger,
                )
                parsed_result = json.loads(result_str)
                items = parsed_result.get("items", [])

                for item in items:
                    enriched_guidelines.append(item)

                    # Extract examples from the guideline's related_examples
                    guideline_id = item.get("id", "")
                    for ex in item.get("related_examples", []):
                        # Find source file path from the original parsed guideline
                        source_file = ""
                        lang = None
                        for g in batch:
                            if g.id == guideline_id:
                                source_file = g.source_file_path
                                lang = g.language
                                break

                        all_examples.append(
                            ParsedExample(
                                id=ex.get("id", ""),
                                title=ex.get("title", ""),
                                content=ex.get("content", ""),
                                example_type=ex.get("example_type", "good"),
                                guideline_ids=ex.get("guideline_ids", [guideline_id]),
                                language=lang or ex.get("language"),
                                source_file_path=source_file,
                            )
                        )

                print(f"  Batch {batch_idx + 1}/{batch_count}: {len(items)} guidelines, "
                      f"{sum(len(i.get('related_examples', [])) for i in items)} examples")

            except Exception as e:
                logger.error("LLM parsing failed for batch %d: %s", batch_idx + 1, e)
                result.errors.append(f"LLM batch {batch_idx + 1}: {e}")
                # Fall through — guidelines without LLM enrichment will use raw data

        print(f"LLM parsing complete: {len(enriched_guidelines)} guidelines enriched, "
              f"{len(all_examples)} examples extracted")
        return enriched_guidelines, all_examples

    def _sync_examples(
        self,
        examples: List[ParsedExample],
        commit_sha: str,
        guideline_ids_to_process: set[str],
        dry_run: bool,
        details: bool,
        result: SyncResult,
    ) -> None:
        """
        Sync extracted examples to the database.

        Creates/updates/deletes Example records and updates the parent
        Guideline.related_examples lists.

        Args:
            examples: Parsed examples from LLM extraction.
            commit_sha: Target commit SHA for source tracking.
            guideline_ids_to_process: Set of guideline IDs that were processed
                (used to scope deletion checks).
            dry_run: If True, report changes without modifying the database.
            result: SyncResult to record outcomes.
        """
        if not examples:
            return

        print(f"\nSyncing {len(examples)} examples...")
        seen_example_ids: set[str] = set()

        # Group examples by parent guideline for relationship updates
        guideline_example_map: Dict[str, List[str]] = {}

        for parsed_ex in examples:
            if not parsed_ex.id:
                continue
            seen_example_ids.add(parsed_ex.id)

            # Track guideline -> example relationships
            for gid in parsed_ex.guideline_ids:
                guideline_example_map.setdefault(gid, []).append(parsed_ex.id)

            new_hash = self.compute_content_hash(parsed_ex.content)

            try:
                existing = self._db.examples.get(parsed_ex.id)
                existing_hash = existing.get("content_hash")

                if existing_hash == new_hash:
                    result.examples_unchanged.append(parsed_ex.id)
                    continue

                result.examples_updated.append(parsed_ex.id)
                if details:
                    result.details.append(SyncDetail(id=parsed_ex.id, kind="example", action="updated", before=existing.get("content", ""), after=parsed_ex.content))
                if not dry_run:
                    self._upsert_example(parsed_ex, new_hash, commit_sha, existing)
            except Exception:
                result.examples_created.append(parsed_ex.id)
                if details:
                    result.details.append(SyncDetail(id=parsed_ex.id, kind="example", action="created", before=None, after=parsed_ex.content))
                if not dry_run:
                    self._upsert_example(parsed_ex, new_hash, commit_sha)

        # Handle deletions: find examples linked to processed guidelines
        # that are no longer in the extracted set
        for gid in guideline_ids_to_process:
            try:
                existing = self._db.guidelines.get(gid)
                old_example_ids = existing.get("related_examples", [])
                for ex_id in old_example_ids:
                    if ex_id not in seen_example_ids:
                        result.examples_deleted.append(ex_id)
                        if details:
                            try:
                                ex_item = self._db.examples.get(ex_id)
                                result.details.append(SyncDetail(id=ex_id, kind="example", action="deleted", before=ex_item.get("content", ""), after=None))
                            except Exception:
                                result.details.append(SyncDetail(id=ex_id, kind="example", action="deleted", before=None, after=None))
                        if not dry_run:
                            try:
                                self._db.examples.delete(ex_id, run_indexer=False)
                            except Exception:
                                pass
            except Exception:
                pass

        # Update guideline related_examples fields
        if not dry_run:
            for gid in guideline_ids_to_process:
                ex_ids = guideline_example_map.get(gid, [])
                try:
                    existing = self._db.guidelines.get(gid)
                    if existing.get("related_examples", []) == ex_ids:
                        continue
                    existing["related_examples"] = ex_ids
                    guideline = Guideline(**{k: v for k, v in existing.items() if k != "kind" and k != "isDeleted"})
                    self._db.guidelines.upsert(gid, data=guideline, run_indexer=False)
                except Exception as e:
                    logger.error("Failed to update related_examples for %s: %s", gid, e)

        # Run examples indexer
        if not dry_run and (result.examples_created or result.examples_updated or result.examples_deleted):
            print("Running examples search indexer...")
            SearchManager.run_indexers(["examples"])

    def _upsert_example(
        self,
        parsed: ParsedExample,
        content_hash: str,
        commit_sha: str,
        existing: Optional[dict] = None,
    ) -> None:
        """Create or update an example in the database."""
        example = Example(
            id=parsed.id,
            title=parsed.title,
            content=parsed.content,
            example_type=ExampleType(parsed.example_type),
            language=parsed.language,
            guideline_ids=parsed.guideline_ids,
            content_hash=content_hash,
            source_file_path=parsed.source_file_path,
            source_commit_sha=commit_sha,
            last_synced_at=datetime.now(timezone.utc),
            service=existing.get("service") if existing else None,
            is_exception=existing.get("is_exception", False) if existing else False,
            tags=existing.get("tags") if existing else None,
            memory_ids=existing.get("memory_ids", []) if existing else [],
        )
        self._db.examples.upsert(parsed.id, data=example, run_indexer=False)

    # ========================================================================
    # Memory Reconciliation
    # ========================================================================

    def _reconcile_memories(
        self,
        changed_guideline_ids: set[str],
        enriched_map: Dict[str, dict],
        dry_run: bool,
        result: SyncResult,
    ) -> None:
        """
        Reconcile memories against updated guidelines.

        For each changed guideline that has related memories, asks the LLM
        whether those memories are now redundant (absorbed by the guideline).
        Absorbed memories are unlinked and soft-deleted if orphaned.

        Args:
            changed_guideline_ids: IDs of guidelines that were created or updated.
            enriched_map: LLM-enriched guideline dicts keyed by ID.
            dry_run: If True, report changes without modifying the database.
            result: SyncResult to record outcomes.
        """
        if not changed_guideline_ids:
            return

        # Collect guidelines that have related memories
        guidelines_with_memories: List[dict] = []
        for gid in changed_guideline_ids:
            try:
                db_guideline = self._db.guidelines.get(gid)
                related_mems = db_guideline.get("related_memories", [])
                if related_mems:
                    guidelines_with_memories.append(db_guideline)
            except Exception:
                continue

        if not guidelines_with_memories:
            return

        total_memories = sum(len(g.get("related_memories", [])) for g in guidelines_with_memories)
        prefix = "[DRY RUN] " if dry_run else ""
        print(f"\n{prefix}Reconciling memories for {len(guidelines_with_memories)} guidelines "
              f"({total_memories} memories to check)...")

        any_deletions = False
        guidelines_needing_reindex = False

        for db_guideline in guidelines_with_memories:
            gid = db_guideline["id"]
            memory_ids = db_guideline.get("related_memories", [])

            # Fetch all related memories, skip already-deleted ones
            memories: List[dict] = []
            for mid in memory_ids:
                try:
                    mem = self._db.memories.get(mid)
                    if not mem.get("isDeleted", False):
                        memories.append(mem)
                except Exception:
                    logger.warning("Memory %s referenced by guideline %s not found, skipping", mid, gid)

            if not memories:
                continue

            # Build the guideline content for the LLM — prefer enriched version
            enriched = enriched_map.get(gid)
            guideline_content = {
                "id": gid,
                "title": enriched.get("title", "") if enriched else db_guideline.get("title", ""),
                "content": enriched.get("content", "") if enriched else db_guideline.get("content", ""),
            }

            memory_inputs = [
                {"id": m["id"], "title": m.get("title", ""), "content": m.get("content", "")}
                for m in memories
            ]

            prompt_input = json.dumps({"guideline": guideline_content, "memories": memory_inputs})

            print(f"  Guideline: {gid} — checking {len(memories)} memories")

            try:
                result_str = run_prompt(
                    folder="other",
                    filename="reconcile_memories",
                    inputs={"question": prompt_input},
                    max_retries=3,
                    logger=logger,
                )
                parsed_result = json.loads(result_str)
                absorbed_ids = set(parsed_result.get("absorbed_memory_ids", []))
                reasoning = parsed_result.get("reasoning", {})
            except Exception as e:
                logger.error("Memory reconciliation failed for guideline %s: %s", gid, e)
                result.errors.append(f"reconcile memories for {gid}: {e}")
                # On failure, retain all memories
                for m in memories:
                    result.memories_retained.append(m["id"])
                continue

            for mem in memories:
                mid = mem["id"]
                if mid in absorbed_ids:
                    reason = reasoning.get(mid, "no reason provided")
                    print(f"    {prefix}Absorbed: {mid} — {reason}")
                    result.memories_absorbed.append(mid)

                    if not dry_run:
                        self._absorb_memory(db_guideline, mem)
                        any_deletions = True
                        guidelines_needing_reindex = True
                else:
                    reason = reasoning.get(mid, "")
                    if reason:
                        print(f"    Retained: {mid} — {reason}")
                    else:
                        print(f"    Retained: {mid}")
                    result.memories_retained.append(mid)

        # Run indexers if any memories were modified/deleted
        if not dry_run and any_deletions:
            print(f"{prefix}Running memories search indexer...")
            SearchManager.run_indexers(["memories"])
        if not dry_run and guidelines_needing_reindex:
            print(f"{prefix}Running guidelines search indexer...")
            SearchManager.run_indexers(["guidelines"])

    def _absorb_memory(self, guideline: dict, memory: dict) -> None:
        """
        Absorb a memory into its parent guideline.

        Removes bidirectional links between the guideline and memory,
        cleans up any example cross-links, and soft-deletes the memory
        if it becomes orphaned (no remaining relationships).

        Args:
            guideline: The guideline DB record (dict).
            memory: The memory DB record (dict).
        """
        gid = guideline["id"]
        mid = memory["id"]

        # 1. Remove memory from guideline's related_memories
        related_memories = guideline.get("related_memories", [])
        if mid in related_memories:
            related_memories.remove(mid)
            guideline["related_memories"] = related_memories
            cleaned = {k: v for k, v in guideline.items() if k not in ("kind", "isDeleted")}
            g = Guideline(**cleaned)
            self._db.guidelines.upsert(gid, data=g, run_indexer=False)

        # 2. Remove guideline from memory's related_guidelines
        #    Normalize all entries to DB-safe format before comparing, in case
        #    some were stored in web format (foo.html#bar) by older code paths.
        mem_guidelines = memory.get("related_guidelines", [])
        normalized = [guideline_id_to_db(g) for g in mem_guidelines]
        if gid in normalized:
            normalized.remove(gid)
            memory["related_guidelines"] = normalized

        # 3. Clean up example cross-links (memory.related_examples <-> example.memory_ids)
        mem_examples = memory.get("related_examples", [])
        for ex_id in mem_examples:
            try:
                ex = self._db.examples.get(ex_id)
                ex_memory_ids = ex.get("memory_ids", [])
                if mid in ex_memory_ids:
                    ex_memory_ids.remove(mid)
                    ex["memory_ids"] = ex_memory_ids
                    cleaned_ex = {k: v for k, v in ex.items() if k not in ("kind", "isDeleted")}
                    e = Example(**cleaned_ex)
                    self._db.examples.upsert(ex_id, data=e, run_indexer=False)
            except Exception as e:
                logger.warning("Failed to clean example %s cross-link for memory %s: %s", ex_id, mid, e)
        memory["related_examples"] = []

        # 4. Clean up memory-to-memory cross-links
        mem_memories = memory.get("related_memories", [])
        for sibling_mid in mem_memories:
            try:
                sibling = self._db.memories.get(sibling_mid)
                sibling_related = sibling.get("related_memories", [])
                if mid in sibling_related:
                    sibling_related.remove(mid)
                    sibling["related_memories"] = sibling_related
                    cleaned_sib = {k: v for k, v in sibling.items() if k not in ("kind", "isDeleted")}
                    s = Memory(**cleaned_sib)
                    self._db.memories.upsert(sibling_mid, data=s, run_indexer=False)
            except Exception as e:
                logger.warning("Failed to clean sibling memory %s cross-link for %s: %s", sibling_mid, mid, e)

        # 5. Check if memory is orphaned (no remaining relationships)
        is_orphaned = (
            not memory.get("related_guidelines", [])
            and not memory.get("related_examples", [])
            and not memory.get("related_memories", [])
        )

        if is_orphaned:
            print(f"      Soft-deleting orphaned memory: {mid}")
            self._db.memories.delete(mid, run_indexer=False)
        else:
            # Memory still has other relationships — update it but don't delete
            remaining = []
            if memory.get("related_guidelines"):
                remaining.append(f"{len(memory['related_guidelines'])} guidelines")
            if memory.get("related_memories"):
                remaining.append(f"{len(memory['related_memories'])} memories")
            print(f"      Memory {mid} unlinked but retained (still linked to {', '.join(remaining)})")
            cleaned_mem = {k: v for k, v in memory.items() if k not in ("kind", "isDeleted")}
            m = Memory(**cleaned_mem)
            self._db.memories.upsert(mid, data=m, run_indexer=False)
