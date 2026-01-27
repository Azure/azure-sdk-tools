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
import logging
import re
from collections import Counter
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Dict, List, Optional, Tuple

import httpx
from bs4 import BeautifulSoup

try:
    import markdown_it

    MARKDOWN_IT = markdown_it.MarkdownIt()
except ImportError:
    MARKDOWN_IT = None

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
LAST_SYNCED_COMMIT_SHA_KEY = "guidelines:last_synced_commit_sha"

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
    errors: list[str] = field(default_factory=list)

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

    def summary(self) -> str:
        return (
            f"Sync complete: "
            f"Guidelines: {len(self.guidelines_created)} created, {len(self.guidelines_updated)} updated, "
            f"{len(self.guidelines_deleted)} deleted, {len(self.guidelines_unchanged)} unchanged | "
            f"Examples: {len(self.examples_created)} created, {len(self.examples_updated)} updated, "
            f"{len(self.examples_deleted)} deleted, {len(self.examples_unchanged)} unchanged | "
            f"{len(self.errors)} errors"
        )


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

    def get_all_guideline_files(self, commit_sha: str = "main") -> list[str]:
        """
        Get all guideline markdown files in the docs/ folder.

        Used for initial full sync or when --force is specified.
        Only returns files that match FILES_TO_PARSE.

        Args:
            commit_sha: The commit SHA or branch to fetch files from. Defaults to 'main'.
        """
        url = f"https://api.github.com/repos/{AZURE_SDK_OWNER}/{AZURE_SDK_REPO}/git/trees/{commit_sha}?recursive=1"
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
                # Check if the filename (without path) is in our list of files to parse
                base_name = item["path"].split("/")[-1]
                if base_name in FILES_TO_PARSE:
                    files.append(item["path"])

        return files

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
            text = f"{text[:index]}{link.text} ({link['href']}) {text[len(link.text) + 1 + index:]}"
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
        if MARKDOWN_IT is None:
            logger.warning("markdown-it-py not installed, falling back to regex-based parsing")
            return self._parse_markdown_fallback(file_path, content)

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

    def _parse_markdown_fallback(self, file_path: str, content: str) -> List[ParsedGuideline]:
        """
        Fallback regex-based parsing when markdown-it is not available.

        This uses the header anchor pattern: ## Header Text {#anchor-id}
        """
        guidelines = []

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

        # Extract filename without extension for ID prefix
        filename = parts[-1].replace(".md", "") if parts else "unknown"

        # Pattern to match headers with explicit anchors: ## Text {#anchor}
        header_pattern = re.compile(r"^(#{1,6})\s+(.+?)\s*\{#([a-zA-Z0-9_-]+)\}\s*$", re.MULTILINE)

        matches = list(header_pattern.finditer(content))

        for i, match in enumerate(matches):
            header_level = len(match.group(1))
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

            # Create ID in format: {language}_{filename}=html={anchor}
            guideline_id = f"{language}_{filename}=html={anchor}" if language else f"{filename}=html={anchor}"

            guidelines.append(
                ParsedGuideline(
                    id=guideline_id,
                    text=guideline_content,
                    language=language,
                    source_file_path=file_path,
                )
            )

        return guidelines

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
        pattern = re.compile(r"^[A-Za-z0-9_=\\-]{1,1024}$")
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
        force: bool = False,
        use_llm: bool = False,
        base_sha: Optional[str] = None,
        target_sha: Optional[str] = None,
    ) -> SyncResult:
        """
        Synchronize guidelines from the azure-sdk repository to Cosmos DB.

        Args:
            dry_run: If True, report changes without writing to the database.
            force: If True, ignore the last synced SHA and process all files.
            use_llm: If True, use LLM to parse guidelines into structured format with examples.
            base_sha: If provided, use this SHA as the baseline instead of the last synced SHA from AppConfig.
            target_sha: If provided, use this SHA as the target instead of the latest on main.

        Returns:
            SyncResult with details of what was created, updated, deleted, or unchanged.
        """
        result = SyncResult()

        # Get target SHA (use provided value or fetch current HEAD)
        if target_sha:
            current_sha = target_sha
            print(f"Using provided target SHA: {current_sha}")
        else:
            print("Fetching current HEAD SHA from main branch...")
            current_sha = self.get_current_head_sha()
            print(f"Current HEAD SHA: {current_sha}")

        # Get base SHA (use provided value, or AppConfig value unless force is True)
        if force:
            last_sha = None
            print("Force flag set - will perform full sync (ignoring base SHA)")
        elif base_sha:
            last_sha = base_sha
            print(f"Using provided base SHA: {last_sha}")
        else:
            print("Fetching last synced SHA from App Configuration...")
            last_sha = self.get_last_synced_commit_sha()
            if last_sha:
                print(f"Last synced SHA from AppConfig: {last_sha}")
            else:
                print("No previous sync found in AppConfig - will perform full sync")

        # Determine which files to process
        is_incremental = last_sha and last_sha != current_sha
        if is_incremental:
            print(f"Comparing commits: {last_sha[:8]}...{current_sha[:8]}")
            files_to_process = self.get_changed_files(last_sha, current_sha)
            print(f"Incremental sync: {len(files_to_process)} files changed")
            if files_to_process:
                for f in files_to_process:
                    print(f"  Changed: {f}")
        else:
            print(f"Fetching all guideline files from SHA: {current_sha[:8]}")
            files_to_process = self.get_all_guideline_files(current_sha)
            print(f"Full sync: {len(files_to_process)} files to process")

        if not files_to_process:
            print("No files to process")
            if not dry_run:
                self.set_last_synced_commit_sha(current_sha)
            return result

        # For incremental sync, we need to parse BOTH base and target to find actual differences
        if is_incremental:
            return self._sync_incremental(files_to_process, last_sha, current_sha, dry_run, use_llm, result)
        else:
            return self._sync_full(files_to_process, current_sha, dry_run, use_llm, result)

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
            except Exception as e:
                # File might not exist at this SHA (e.g., newly added file)
                if "404" not in str(e):
                    logger.error(f"Error parsing {file_path} at {commit_sha[:8]}: {e}")
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
        use_llm: bool,
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

        for gid in all_ids:
            in_base = gid in base_map
            in_target = gid in target_map

            if in_target and not in_base:
                # New guideline
                result.guidelines_created.append(gid)
                print(f"  Created: {gid}")
                if not dry_run:
                    self._upsert_guideline(target_map[gid][0], target_map[gid][1], target_sha)

            elif in_base and not in_target:
                # Deleted guideline
                result.guidelines_deleted.append(gid)
                print(f"  Deleted: {gid}")
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
                    # Verify guideline exists in database
                    try:
                        existing = self._db.guidelines.get(gid)
                        print(f"    Found in DB: {existing.get('id')} - {existing.get('title', '(no title)')[:60]}")
                    except Exception:
                        print(f"    WARNING: Not found in database - will be created")
                    if not dry_run:
                        self._upsert_guideline(target_map[gid][0], target_hash, target_sha)

        # Run indexer once at the end (if not dry run)
        if not dry_run and (result.guidelines_created or result.guidelines_updated or result.guidelines_deleted):
            print("Running search indexer...")
            SearchManager.run_indexers(["guidelines"])

        # Update the last synced SHA
        if not dry_run:
            self.set_last_synced_commit_sha(target_sha)

        return result

    def _sync_full(
        self,
        files_to_process: List[str],
        target_sha: str,
        dry_run: bool,
        use_llm: bool,
        result: SyncResult,
    ) -> SyncResult:
        """Perform full sync by comparing target SHA against database."""
        # Step 1: Preprocess all files and collect raw guidelines
        print(f"\nPreprocessing {len(files_to_process)} files...")
        all_parsed_guidelines: List[ParsedGuideline] = []

        for file_path in files_to_process:
            try:
                print(f"  Processing: {file_path}")
                content = self.fetch_file_content(file_path, target_sha)
                parsed = self.parse_markdown_file(file_path, content)
                all_parsed_guidelines.extend(parsed)
            except Exception as e:
                logger.error(f"Error preprocessing {file_path}: {e}")
                result.errors.append(f"preprocess {file_path}: {e}")

        print(f"Extracted {len(all_parsed_guidelines)} raw guidelines from {len(files_to_process)} files")

        # Validate and deduplicate
        malformed_ids = self.check_id_format(all_parsed_guidelines)
        if malformed_ids:
            print(f"Warning: Found {len(malformed_ids)} malformed IDs: {malformed_ids[:5]}...")

        # Filter out guidelines without valid IDs
        all_parsed_guidelines = [g for g in all_parsed_guidelines if g.id]

        duplicates_result = self.filter_duplicates(all_parsed_guidelines)
        good_guidelines = duplicates_result["good"]
        bad_guidelines = duplicates_result["bad"]

        if bad_guidelines:
            print(f"Warning: Filtered out {len(bad_guidelines)} duplicate guidelines with conflicting content")

        print(f"After validation: {len(good_guidelines)} valid guidelines\n")

        # Step 2: Optionally use LLM to enrich guidelines with examples
        if use_llm:
            print("LLM parsing is enabled but not yet implemented - using raw guidelines")

        # Step 3: Sync to database
        print("Syncing to database...")
        seen_guideline_ids: set[str] = set()
        files_with_guidelines: set[str] = set()

        for parsed in good_guidelines:
            seen_guideline_ids.add(parsed.id)
            files_with_guidelines.add(parsed.source_file_path)

            # Compute hash of the new content
            new_hash = self.compute_content_hash(parsed.text)

            # Check if guideline exists and compare hash
            try:
                existing = self._db.guidelines.get(parsed.id)
                existing_hash = existing.get("content_hash")

                if existing_hash == new_hash:
                    result.guidelines_unchanged.append(parsed.id)
                    continue

                # Content changed - update
                result.guidelines_updated.append(parsed.id)
                if not dry_run:
                    self._upsert_guideline(parsed, new_hash, target_sha, existing)

            except Exception:
                # Guideline doesn't exist - create
                result.guidelines_created.append(parsed.id)
                if not dry_run:
                    self._upsert_guideline(parsed, new_hash, target_sha)

        # Handle deletions: find guidelines that were in processed files but are no longer present
        for file_path in files_with_guidelines:
            try:
                query = f"SELECT c.id FROM c WHERE c.source_file_path = '{file_path}' AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)"
                existing_items = list(
                    self._db.guidelines.client.query_items(query=query, enable_cross_partition_query=True)
                )

                for item in existing_items:
                    if item["id"] not in seen_guideline_ids:
                        if not dry_run:
                            self._db.guidelines.delete(item["id"], run_indexer=False)
                        result.guidelines_deleted.append(item["id"])

            except Exception as e:
                logger.error(f"Error handling deletions for {file_path}: {e}")
                result.errors.append(f"deletion check {file_path}: {e}")

        # Run indexer once at the end (if not dry run)
        if not dry_run and (result.guidelines_created or result.guidelines_updated or result.guidelines_deleted):
            print("Running search indexer...")
            SearchManager.run_indexers(["guidelines"])

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
    ) -> None:
        """Create or update a guideline in the database."""
        # Extract a title from the text
        title_lines = parsed.text.strip().split("\n")
        title = title_lines[0][:100] if title_lines else "Untitled Guideline"
        title = re.sub(r"(DO|YOU SHOULD|YOU MAY|DO NOT|YOU SHOULD NOT)\s*", "", title).strip()

        guideline = Guideline(
            id=parsed.id,
            title=title,
            content=parsed.text,
            language=parsed.language,
            content_hash=content_hash,
            source_file_path=parsed.source_file_path,
            source_commit_sha=commit_sha,
            last_synced_at=datetime.now(timezone.utc),
            related_guidelines=existing.get("related_guidelines", []) if existing else [],
            related_examples=existing.get("related_examples", []) if existing else [],
            related_memories=existing.get("related_memories", []) if existing else [],
            tags=existing.get("tags") if existing else None,
        )
        self._db.guidelines.upsert(parsed.id, data=guideline, run_indexer=False)
