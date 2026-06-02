"""Daily knowledge sync orchestrator.

Implements the main pipeline:
1. Clone/update documentation repositories
2. Preprocess spector cases and TypeSpec libraries
3. Process markdown files (frontmatter extraction, content normalization)
4. Detect changes via content hashing
5. Upload changed files to Azure Blob Storage
6. Clean up expired blobs

Note: AI Search indexing is handled by GraphRAG (configured with
azure_ai_search vector store). This module only manages the raw
document blob storage that GraphRAG reads from.
"""

from __future__ import annotations

import base64
import logging
import os
import re
import shutil
import subprocess
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

from src.services.configuration_loader import (
    ConfigurationLoader,
    DocumentationSource,
    Metadata,
    RepositoryConfig,
)
from src.services.metadata_resolver import MetadataResolver
from src.services.spector_processor import SpectorCaseProcessor
from src.services.storage_service import BlobService
from src.services.typespec_processor import TypeSpecProcessor

logger = logging.getLogger(__name__)


@dataclass
class ProcessedFile:
    filename: str
    content: str
    blob_path: str
    is_valid: bool
    metadata: Metadata | None = None


@dataclass
class ProcessResult:
    total_processed: int = 0
    changed_documents: int = 0
    unchanged_documents: int = 0
    changed_files: list[ProcessedFile] = field(default_factory=list)
    metadata_changed_files: list[ProcessedFile] = field(default_factory=list)
    unchanged_files: list[ProcessedFile] = field(default_factory=list)


@dataclass
class SyncResult:
    """Result of the daily sync — used to drive incremental GraphRAG."""

    changed_blob_paths: list[str] = field(default_factory=list)
    deleted_blob_paths: list[str] = field(default_factory=list)


# --- Main entry point ---


async def process_daily_sync_knowledge() -> SyncResult:
    """Run the complete daily knowledge sync pipeline.

    Returns:
        SyncResult with changed and deleted blob paths for incremental GraphRAG.
    """
    working_dir = os.path.join(tempfile.gettempdir(), "daily-sync-work")
    docs_dir = os.path.join(working_dir, "docs")
    temp_docs_dir = os.path.join(working_dir, "temp_docs")

    blob_service = BlobService()
    sync_result = SyncResult()

    try:
        # Clean and create work directories
        if os.path.exists(working_dir):
            shutil.rmtree(working_dir, ignore_errors=True)
        os.makedirs(docs_dir, exist_ok=True)

        # Step 1: Clone repositories
        logger.info("Setting up documentation repositories...")
        _setup_repositories(docs_dir)

        # Step 2: Preprocess spector cases
        logger.info("Preprocessing spector cases...")
        await SpectorCaseProcessor.process_spector_cases(docs_dir)

        # Step 3: Process TypeSpec libraries
        logger.info("Processing typespec-azure-resource-manager library...")
        try:
            TypeSpecProcessor(
                docs_dir, "typespec-azure/packages/typespec-azure-resource-manager/lib"
            ).process_typespec_libraries()
        except Exception as e:
            logger.error("Error processing typespec library: %s", e)

        # Step 4: Process documentation sources
        logger.info("Loading documentation source config...")
        doc_sources = ConfigurationLoader.get_documentation_sources()

        logger.info("Loading existing blob metadata for change detection...")
        existing_blobs = blob_service.list_blobs()

        all_changed: list[ProcessedFile] = []
        all_metadata_changed: list[ProcessedFile] = []
        all_unchanged: list[ProcessedFile] = []

        for source in doc_sources:
            source_dir = os.path.join(working_dir, source.path)
            target_dir = os.path.join(temp_docs_dir, source.folder)

            if not os.path.exists(source_dir):
                logger.warning("Source directory not found: %s", source_dir)
                continue

            os.makedirs(target_dir, exist_ok=True)

            # Create release notes index
            try:
                _create_release_notes_index(source, source_dir, target_dir)
            except Exception as e:
                logger.error("Error creating release notes index: %s", e)

            # Process files
            result = _process_source_directory(
                source_dir, source, target_dir, existing_blobs, blob_service
            )
            all_changed.extend(result.changed_files)
            all_metadata_changed.extend(result.metadata_changed_files)
            all_unchanged.extend(result.unchanged_files)

        logger.info(
            "Processing completed: %d changed, %d metadata-changed, %d unchanged",
            len(all_changed),
            len(all_metadata_changed),
            len(all_unchanged),
        )

        # Step 5: Upload changed files to blob storage
        _upload_files(blob_service, all_changed + all_metadata_changed)

        # Step 6: Clean up expired blobs
        deleted_paths = _cleanup_expired_blobs(
            blob_service, all_changed + all_unchanged + all_metadata_changed
        )

        # Build sync result for incremental GraphRAG
        sync_result.changed_blob_paths = [f.blob_path for f in all_changed if f.is_valid]
        sync_result.deleted_blob_paths = deleted_paths

        logger.info("Daily sync knowledge processing completed")

    finally:
        if os.path.exists(working_dir):
            shutil.rmtree(working_dir, ignore_errors=True)

    return sync_result


# --- Repository setup ---


def _setup_repositories(docs_dir: str) -> None:
    """Clone/checkout all configured repositories."""
    # Configure git HTTP/1.1
    try:
        subprocess.run(
            ["git", "config", "--global", "http.version", "HTTP/1.1"],
            capture_output=True,
        )
    except Exception:
        pass

    _setup_ssh_config()
    repos = ConfigurationLoader.get_repository_configs()

    for repo in repos:
        try:
            logger.info("Setting up %s...", repo.name)
            repo_path = os.path.join(docs_dir, repo.path)
            clone_url = _get_authenticated_url(repo)

            if repo.auth_type == "local":
                # Copy from local path
                for folder in repo.sparse_checkout or []:
                    src = os.path.join(clone_url, folder)
                    dst = os.path.join(repo_path, folder)
                    if os.path.exists(src):
                        shutil.copytree(src, dst, dirs_exist_ok=True)
            else:
                os.makedirs(docs_dir, exist_ok=True)
                if repo.sparse_checkout:
                    subprocess.run(
                        ["git", "clone", "--filter=blob:none", "--sparse", clone_url, repo.path],
                        cwd=docs_dir,
                        capture_output=True,
                        check=True,
                        env=os.environ,
                    )
                    subprocess.run(
                        ["git", "config", "core.sparseCheckout", "true"],
                        cwd=repo_path,
                        capture_output=True,
                        check=True,
                    )
                    sparse_file = os.path.join(repo_path, ".git/info/sparse-checkout")
                    Path(sparse_file).write_text("\n".join(repo.sparse_checkout))
                    subprocess.run(
                        ["git", "checkout", repo.branch],
                        cwd=repo_path,
                        capture_output=True,
                        check=True,
                        env=os.environ,
                    )
                else:
                    subprocess.run(
                        ["git", "clone", clone_url, repo.path],
                        cwd=docs_dir,
                        capture_output=True,
                        check=True,
                        env=os.environ,
                    )

            logger.info("%s setup completed", repo.name)
        except Exception as e:
            logger.error("Error setting up %s: %s", repo.name, e)
            continue


def _get_authenticated_url(repo: RepositoryConfig) -> str:
    """Get URL with embedded credentials based on auth type."""
    if repo.auth_type == "public":
        return repo.url
    if repo.auth_type == "token":
        if not repo.token:
            raise RuntimeError(f"Token missing for {repo.name}")
        return repo.url.replace("https://", f"https://x-access-token:{repo.token}@")
    if repo.auth_type == "ssh":
        return repo.url
    if repo.auth_type == "local":
        return repo.local_path or repo.url
    return repo.url


def _setup_ssh_config() -> None:
    """Set up SSH keys and config for git operations."""
    ssh_key = os.environ.get("SSH_PRIVATE_KEY")
    if not ssh_key:
        return

    home = os.environ.get("HOME", os.path.expanduser("~"))
    ssh_dir = os.path.join(home, ".ssh")
    os.makedirs(ssh_dir, mode=0o700, exist_ok=True)

    key_path = os.path.join(ssh_dir, "id_ed25519")
    decoded_key = base64.b64decode(ssh_key).decode("utf-8")
    Path(key_path).write_text(decoded_key)
    os.chmod(key_path, 0o600)

    config_path = os.path.join(ssh_dir, "config")
    ssh_config = f"""Host github-microsoft
    HostName github.com
    User git
    IdentityFile {key_path}
    IdentitiesOnly yes
    StrictHostKeyChecking no
    UserKnownHostsFile /dev/null

Host github.com
    HostName github.com
    User git
    StrictHostKeyChecking no
    UserKnownHostsFile /dev/null
"""
    Path(config_path).write_text(ssh_config)
    os.chmod(config_path, 0o644)

    os.environ["GIT_SSH_COMMAND"] = f'ssh -F "{config_path}" -o StrictHostKeyChecking=no'
    logger.info("SSH configuration setup completed")


# --- File processing ---


def _process_source_directory(
    source_dir: str,
    source: DocumentationSource,
    target_dir: str,
    existing_blobs: dict,
    blob_service: BlobService,
) -> ProcessResult:
    """Process all markdown files in a source directory."""
    result = ProcessResult()

    def process_single(full_path: str, file_source_dir: str) -> None:
        result.total_processed += 1
        processed = _process_markdown_file(full_path, source, file_source_dir)
        if not processed.is_valid:
            return

        # Build metadata for comparison
        blob_metadata = None
        if processed.metadata:
            blob_metadata = {"scope": processed.metadata.scope}
            if processed.metadata.service_type:
                blob_metadata["service_type"] = processed.metadata.service_type

        content_changed = blob_service.has_content_changed(
            processed.blob_path, processed.content, existing_blobs
        )
        metadata_changed = blob_service.has_metadata_changed(
            processed.blob_path, blob_metadata, existing_blobs
        )

        if content_changed:
            result.changed_documents += 1
            result.changed_files.append(processed)
            Path(os.path.join(target_dir, processed.filename)).write_text(
                processed.content, encoding="utf-8"
            )
        elif metadata_changed:
            result.metadata_changed_files.append(processed)
        else:
            result.unchanged_documents += 1
            result.unchanged_files.append(processed)

    # Check if source is a file
    if os.path.isfile(source_dir):
        process_single(source_dir, os.path.dirname(source_dir))
        return result

    # Walk directory
    for dirpath, _, filenames in os.walk(source_dir):
        for fname in filenames:
            if not (fname.endswith(".md") or fname.endswith(".mdx")):
                continue
            full_path = os.path.join(dirpath, fname)
            rel_path = os.path.relpath(full_path, source_dir)

            # Skip ignored paths
            if source.ignored_paths:
                if source.is_generated:
                    if any(fname.startswith(p.replace("/", "#").replace("\\", "#")) for p in source.ignored_paths):
                        continue
                else:
                    if any(rel_path.startswith(p) for p in source.ignored_paths):
                        continue

            # Skip reference files and release notes
            if rel_path.startswith("reference") or fname.startswith("release-"):
                continue

            process_single(full_path, source_dir)

    return result


def _process_markdown_file(
    file_path: str,
    source: DocumentationSource,
    source_dir: str,
) -> ProcessedFile:
    """Process a single markdown file."""
    content = Path(file_path).read_text(encoding="utf-8")
    converted = convert_markdown(content)

    if not converted["filename"]:
        rel_path = os.path.relpath(file_path, source_dir)
        converted["filename"] = rel_path.replace(os.sep, "#").replace("/", "#")
        if source.folder == "azure-sdk-guidelines":
            return ProcessedFile(filename="", content="", blob_path="", is_valid=False)
        if source.is_generated:
            converted["filename"] = re.sub(r"^generated#", "", converted["filename"])

    filename = converted["filename"]
    if source.file_name_lower_case:
        filename = filename.lower().replace(" ", "-")

    blob_path = f"{source.folder}/{filename}"

    # Resolve metadata
    metadata = None
    if source.metadata:
        rel_path = os.path.relpath(file_path, source_dir)
        metadata = MetadataResolver.resolve_metadata(
            rel_path, source.metadata, source.overrides or None
        )

    return ProcessedFile(
        filename=converted["filename"],
        content=converted["content"],
        blob_path=blob_path,
        is_valid=True,
        metadata=metadata,
    )


# --- Content processing functions ---


def preprocess_content(content: str) -> str:
    """Fix Azure AI Search markdown parser issues.

    1. Replace # at start of lines in code blocks with // (prevents header detection)
    2. Escape ``` code block delimiters
    """
    # Fix 1: Replace # in code blocks
    def fix_code_block(m: re.Match) -> str:
        lang = m.group(1)
        code = m.group(2)
        transformed = re.sub(r"^#(\s*)", r"//\1", code, flags=re.MULTILINE)
        return f"```{lang}\n{transformed}```"

    result = re.sub(r"```(\w+)\s*\n([\s\S]*?)```", fix_code_block, content)

    # Fix 2: Escape code block delimiters
    result = re.sub(r"```(\w*)", r"\\`\\`\\`\1", result)

    return result


def convert_markdown(content: str) -> dict[str, str]:
    """Convert markdown: extract frontmatter, normalize content."""
    title = ""
    filename = ""
    found_title = False
    in_frontmatter = False
    first_content_line = True

    content = preprocess_content(content)
    lines = content.split("\n")
    content_lines: list[str] = []

    for line in lines:
        if line.strip() == "---":
            if not in_frontmatter:
                in_frontmatter = True
                continue
            else:
                in_frontmatter = False
                continue

        if in_frontmatter:
            if line.startswith("title:"):
                title = line[6:].strip().strip("\"'")
                found_title = True
            if line.startswith("permalink:"):
                filename = line[10:].strip().strip("\"'")
            continue

        if first_content_line:
            if found_title:
                content_lines.append(f"# {title}")
                content_lines.append("")
            first_content_line = False

        content_lines.append(line)

    return {"filename": filename, "content": "\n".join(content_lines)}


def extract_date_from_filename(file_path: str) -> str:
    """Extract date from filename in format release-YYYY-MM-DD.md."""
    m = re.search(r"release-(\d{4}-\d{2}-\d{2})", os.path.basename(file_path))
    return m.group(1) if m else "1970-01-01"


def extract_release_info(content: str) -> dict[str, str]:
    """Extract title, releaseDate, version from frontmatter."""
    info = {"title": "", "releaseDate": "", "version": ""}
    m = re.match(r"^---\s*\n([\s\S]*?)\n---\s*", content)
    if not m:
        return info
    for line in m.group(1).split("\n"):
        line = line.strip()
        if line.startswith("title:"):
            info["title"] = line[6:].strip().strip("\"'")
        elif line.startswith("releaseDate:"):
            info["releaseDate"] = line[12:].strip()
        elif line.startswith("version:"):
            info["version"] = line[8:].strip().strip("\"'")
    return info


def extract_sections(content: str) -> str:
    """Extract and downgrade headers for release note sections."""
    # Remove frontmatter
    result = re.sub(r"^---\s*\n[\s\S]*?\n---\s*\n", "", content)
    # Remove caution blocks
    result = re.sub(r":::caution[\s\S]*?:::\s*", "", result)
    # Downgrade headers
    result = re.sub(r"^(#+)\s+(.+)$", r"#\1 \2", result, flags=re.MULTILINE)
    return result.strip()


# --- Upload and cleanup ---


def _upload_files(blob_service: BlobService, files: list[ProcessedFile]) -> None:
    """Upload changed/metadata-changed files to blob storage."""
    count = 0
    for f in files:
        if not f.is_valid:
            continue
        metadata = None
        if f.metadata:
            metadata = {"scope": f.metadata.scope}
            if f.metadata.service_type:
                metadata["service_type"] = f.metadata.service_type
        blob_service.put_blob(f.blob_path, f.content, metadata)
        count += 1
    logger.info("Uploaded %d files to blob storage", count)


def _cleanup_expired_blobs(blob_service: BlobService, current_files: list[ProcessedFile]) -> list[str]:
    """Remove blobs that are no longer in the current file set.

    Returns:
        List of deleted blob paths.
    """
    blobs = blob_service.list_blobs()
    current_paths = {f.blob_path for f in current_files if f.is_valid}
    deleted: list[str] = []

    for blob_path in blobs:
        if blob_path.startswith("static_"):
            continue
        if blob_path not in current_paths:
            try:
                blob_service.delete_blob(blob_path)
                deleted.append(blob_path)
            except Exception as e:
                logger.warning("Failed to delete blob %s: %s", blob_path, e)

    logger.info("Cleaned up %d expired blobs", len(deleted))
    return deleted


def _create_release_notes_index(
    source: DocumentationSource, source_dir: str, target_dir: str
) -> None:
    """Create an index file with the 10 most recent release notes."""
    release_dir = os.path.join(source_dir, "release-notes")
    if not os.path.isdir(release_dir):
        return

    # Find release note files
    release_files: list[str] = []
    for dirpath, _, filenames in os.walk(release_dir):
        for fname in filenames:
            if re.match(r"release-\d{4}-\d{2}-\d{2}\.(md|mdx)$", fname):
                release_files.append(os.path.join(dirpath, fname))

    # Sort by date descending
    release_files.sort(key=lambda f: extract_date_from_filename(f), reverse=True)
    recent = release_files[:10]

    # Build index content
    content = f"# {source.folder} - Recent Version Release Notes\n"
    content += f"This contains latest release version and changes of {source.folder}\n\n"

    for file_path in recent:
        try:
            file_content = Path(file_path).read_text(encoding="utf-8")
            rel_path = os.path.relpath(file_path, source_dir)

            # Build release link
            if source.folder == "typespec_docs":
                link = f"https://typespec.io/docs/{rel_path}"
            elif source.folder == "typespec_azure_docs":
                link = f"https://azure.github.io/typespec-azure/docs/{rel_path}"
            else:
                link = ""
            link = re.sub(r"\.(md|mdx)$", "", link)

            info = extract_release_info(file_content)
            header = f"## [version-{info['title']}-{info['releaseDate']}"
            if info["version"]:
                header += f" (v{info['version']})"
            header += f"]({link})\n"

            sections = extract_sections(file_content)
            content += header + sections + "\n"
        except Exception as e:
            logger.warning("Error reading release note %s: %s", file_path, e)

    index_path = os.path.join(target_dir, "version-release-notes-index.md")
    Path(index_path).write_text(content, encoding="utf-8")
    logger.info("Created release notes index for %s", source.folder)
