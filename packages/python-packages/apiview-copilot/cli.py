# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=too-many-lines

"""
Command line interface for APIView Copilot.
"""

import asyncio
import json
import logging
import os
import pathlib
import sys
import time
from collections import OrderedDict
from datetime import date
from typing import List, Optional

import colorama
import requests
import yaml
from azure.core.exceptions import ClientAuthenticationError
from colorama import Fore, Style
from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from knack.help_files import helps
from knack.util import CLIError
from src._apiview import (
    ApiViewClient,
)
from src._apiview import get_active_reviews as _get_active_reviews
from src._apiview import get_ai_comment_feedback as _get_ai_comment_feedback
from src._apiview import (
    _APIVIEW_COMMENT_SELECT_FIELDS,
    get_apiview_cosmos_client,
    get_approvers,
    get_comment_with_context,
    get_comments_in_date_range,
    get_created_revisions,
    get_opened_revisions,
    get_thread_start_dates,
    resolve_package,
)
from src._apiview_reviewer import SUPPORTED_LANGUAGES, ApiViewReview
from src._database_manager import ContainerNames, DatabaseManager
from src._garbage_collector import GarbageCollector
from src._apiview_metrics import (
    DEFAULT_OUTPUT_PATH as DEFAULT_VERSION_TRENDS_OUTPUT_PATH,
)
from src._apiview_metrics import (
    DEFAULT_COMPLIANCE_OUTPUT_PATH,
)
from src._apiview_metrics import (
    build_compliance_reports,
    build_version_reports,
    generate_compliance_chart,
    generate_version_chart,
    print_compliance_report,
    print_version_report,
)
from src._comment_bucket_trends import (
    DEFAULT_OUTPUT_PATH as DEFAULT_COMMENT_BUCKET_OUTPUT_PATH,
    DEFAULT_GENERIC_OUTPUT_PATH,
    DEFAULT_GUIDELINE_OUTPUT_PATH,
)
from src._comment_bucket_trends import (
    build_language_comment_bucket_reports,
    generate_comment_bucket_chart,
    print_comment_bucket_report,
)
from src._mention import handle_mention_request
from src._metrics import get_metrics_report
from src._models import APIViewComment
from src._prompt_runner import run_prompt
from src._search_manager import SearchManager
from src._settings import SettingsManager
from src._thread_resolution import handle_thread_resolution_request
from src._utils import get_language_pretty_name, to_iso8601
from src.agent._agent import get_readonly_agent, get_readwrite_agent, invoke_agent

colorama.init(autoreset=True)

BLUE = Fore.BLUE
GREEN = Fore.GREEN
RESET = Style.RESET_ALL
BOLD = Style.BRIGHT

# Bold and color for prompts
BOLD_GREEN = BOLD + GREEN
BOLD_BLUE = BOLD + BLUE


helps[
    "review"
] = """
    type: group
    short-summary: Commands for creating and managing APIView reviews.
"""

helps[
    "agent"
] = """
    type: group
    short-summary: Commands for interacting with the agent.
"""

helps[
    "apiview"
] = """
    type: group
    short-summary: Commands for querying APIView data.
"""

helps[
    "test"
] = """
    type: group
    short-summary: Commands for development and testing.
"""

helps[
    "ops"
] = """
    type: group
    short-summary: Commands for deployment and infrastructure.
"""

helps[
    "kb"
] = """
    type: group
    short-summary: Commands for interacting with the knowledge base.
"""

helps[
    "db"
] = """
    type: group
    short-summary: Commands for managing the database.
"""

helps[
    "report"
] = """
    type: group
    short-summary: Commands for analytics, auditing, and reporting.
"""

# COMMANDS


def prompt_test(path: str = None, workers: int = 4):
    """Run a prompt file with its sample inputs, or smoke-test all prompts if no path is given."""
    from src._prompt_runner import _execute_prompt_template

    if path:
        prompty_path = pathlib.Path(path)
        if not prompty_path.exists():
            print(f"Error: File '{path}' does not exist.")
            sys.exit(1)
        if prompty_path.suffix != ".prompty":
            print(f"Error: File '{path}' is not a .prompty file.")
            sys.exit(1)

        print(f"Executing prompt: {path}")
        print("-" * 60)
        result = _execute_prompt_template(str(prompty_path))
        print(result)
        return

    # No path given — smoke-test all prompts
    import glob
    from concurrent.futures import ThreadPoolExecutor, as_completed

    from src._retry import retry_with_backoff

    prompts_dir = pathlib.Path(__file__).parent / "prompts"
    prompty_files = sorted(glob.glob(str(prompts_dir / "**" / "*.prompty"), recursive=True))

    if not prompty_files:
        print("No .prompty files found.")
        return

    BOLD = "\033[1m"
    RED = "\033[91m"
    GREEN = "\033[92m"
    YELLOW = "\033[93m"
    RESET = "\033[0m"

    print(f"{'=' * 60}")
    print("prompt smoke test")
    print(f"{'=' * 60}")
    print(f"{YELLOW}NOTE: A passing result means the prompt executed without errors.")
    print(f"It does NOT verify that the prompt produces correct or intended output.")
    print(f"Use 'avc test prompt -p <file>' to inspect individual outputs.{RESET}")
    print()
    print(f"{BOLD}collected {len(prompty_files)} prompt files{RESET}")
    print()

    # Pre-initialize SettingsManager singleton on the main thread to avoid
    # a race during first-time initialization when running prompts in parallel.
    SettingsManager()

    results = []

    def _run_one(filepath: str) -> tuple[str, bool, str]:
        rel = pathlib.Path(filepath).relative_to(pathlib.Path(__file__).parent)
        try:
            retry_with_backoff(lambda: _execute_prompt_template(filepath), description=str(rel))
            return (str(rel), True, "")
        except Exception as exc:
            return (str(rel), False, str(exc))

    with ThreadPoolExecutor(max_workers=workers) as executor:
        future_to_file = {executor.submit(_run_one, f): f for f in prompty_files}
        for future in as_completed(future_to_file):
            rel_path, passed, error = future.result()
            results.append((rel_path, passed, error))
            status = f"{GREEN}PASS{RESET}" if passed else f"{RED}FAIL{RESET}"
            print(f"  {status} {rel_path}")

    # Sort results for consistent display in summary
    results.sort(key=lambda r: r[0])
    passed = [r for r in results if r[1]]
    failed = [r for r in results if not r[1]]

    # Summary
    print()
    if failed:
        print(f"{RED}{BOLD}{'=' * 60}")
        print("FAILURES")
        print(f"{'=' * 60}{RESET}")
        for rel_path, _, error in failed:
            print(f"  {RED}{rel_path}{RESET}")
            print(f"    {error}")
            print()

    print(f"{'=' * 60}")
    print("smoke test summary")
    print(f"{'=' * 60}")
    summary_parts = []
    if passed:
        summary_parts.append(f"{GREEN}{len(passed)} passed{RESET}")
    if failed:
        summary_parts.append(f"{RED}{len(failed)} failed{RESET}")
    print(f"{', '.join(summary_parts)}, {len(results)} total")

    if failed:
        sys.exit(1)


def _local_review(
    language: str,
    target: str,
    base: str = None,
    outline: str = None,
    existing_comments: str = None,
    debug_log: bool = False,
):
    """
    Generates a review using the locally installed code.
    """
    target_path = pathlib.Path(target)
    if base is not None:
        target_name = target_path.stem
        base_name = pathlib.Path(base).stem
        # find the common prefix
        common_prefix = os.path.commonprefix([target_name, base_name])
        # strip the common prefix from both names
        target_name = target_name[len(common_prefix) :]
        base_name = base_name[len(common_prefix) :]

    with target_path.open("r", encoding="utf-8") as f:
        target_apiview = f.read()
    if base:
        with pathlib.Path(base).open("r", encoding="utf-8") as f:
            base_apiview = f.read()
    else:
        base_apiview = None

    outline_text = None
    if outline:
        with pathlib.Path(outline).open("r", encoding="utf-8") as f:
            outline_text = f.read()

    comments_obj = None
    if existing_comments:
        with pathlib.Path(existing_comments).open("r", encoding="utf-8") as f:
            comments_obj = json.load(f)

    try:
        reviewer = ApiViewReview(
            target=target_apiview,
            base=base_apiview,
            language=language,
            outline=outline_text,
            comments=comments_obj,
            write_output=True,
            write_debug_logs=debug_log,
        )
    except ValueError as e:
        raise CLIError(str(e)) from e
    reviewer.run()
    reviewer.close()


def run_evals(
    test_paths: list[str] = None,
    num_runs: int = 1,
    save: bool = False,
    use_recording: bool = False,
    style: str = "compact",
    environment: str = "production",
):
    """
    Runs the specified test case(s).
    """
    if test_paths is None:
        test_paths = []
    from evals._discovery import discover_targets
    from evals._runner import EvaluationRunner

    targets = discover_targets(test_paths)
    runner = EvaluationRunner(
        num_runs=num_runs, use_recording=use_recording, verbose=(style == "verbose"), environment=environment
    )
    try:
        results = runner.run(targets)
        if save:
            report = runner.generate_report(results)
            for doc in report:
                db = DatabaseManager.get_instance(environment=environment)
                try:
                    db.evals.upsert(doc["id"], data=doc)
                except Exception as exc:
                    print(f"Error saving eval document to database: {exc}")
                    raise exc

        runner.show_results(results)
    finally:
        runner.cleanup()


def deploy_flask_app():
    """Command to deploy the Flask app."""
    # pylint: disable=import-outside-toplevel
    from scripts.deploy_app import deploy_app_to_azure

    deploy_app_to_azure()


def group_apiview_comments(comments_path: str):
    """
    Groups similar comments in an APIView comments JSON file.
    """
    from src._comment_grouper import CommentGrouper
    from src._models import Comment

    comments = []
    if os.path.exists(comments_path):
        with open(comments_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    else:
        print(f"Comments file {comments_path} does not exist.")
        return
    comments_data = data.get("comments", [])
    comments = [Comment(**comment) for comment in comments_data]
    grouper = CommentGrouper(comments=comments)
    grouped_comments = grouper.group()
    return grouped_comments


def generate_review(
    language: str,
    target: str,
    base: Optional[str] = None,
    outline: Optional[str] = None,
    existing_comments: Optional[str] = None,
    remote: bool = False,
    debug_log: bool = False,
):
    """
    Generates a review synchronously.
    """
    if remote:
        elapsed = 0
        job_info = review_job_start(
            language=language,
            target=target,
            base=base,
            outline=outline,
            existing_comments=existing_comments,
        )
        job_id = job_info.get("job_id") if job_info else None
        print(f"Started review job {job_id}...")
        if not job_id:
            print("Error: Could not extract job_id from review_job_start output.")
            print(job_info)
            return
        for _ in range(1800):  # up to 30 minutes
            status_info = review_job_get(job_id, remote=True)
            status = status_info.get("status") if status_info else None
            if not status_info:
                print(f"Error: Could not get status for job {job_id}")
                return
            if status == "Success":
                print(json.dumps(status_info, indent=2))
                return
            elif status == "Error":
                print(f"Review job failed: {json.dumps(status_info, indent=2)}")
                return
            time.sleep(30)
            elapsed += 30
            print(f"  Status: {status}. Elapsed time: {elapsed / 60.0:.1f} min")
        print("Timed out waiting for review job to complete.")
    else:
        return _local_review(
            language=language,
            target=target,
            base=base,
            outline=outline,
            existing_comments=existing_comments,
            debug_log=debug_log,
        )


def review_job_start(
    language: str,
    target: str,
    base: Optional[str] = None,
    outline: Optional[str] = None,
    existing_comments: Optional[str] = None,
):
    """Start an API review job."""

    with open(target, "r", encoding="utf-8") as f:
        target_content = f.read()
    if base:
        with open(base, "r", encoding="utf-8") as f:
            base_content = f.read()
    else:
        base_content = None

    outline_text = None
    if outline:
        with open(outline, "r", encoding="utf-8") as f:
            outline_text = f.read()

    comments_obj = None
    if existing_comments:
        with open(existing_comments, "r", encoding="utf-8") as f:
            comments_obj = json.load(f)

    payload = {
        "language": language,
        "target": target_content,
    }
    if base_content is not None:
        payload["base"] = base_content
    if outline_text is not None:
        payload["outline"] = outline_text
    if comments_obj is not None:
        payload["comments"] = comments_obj

    settings = SettingsManager()
    base_url = settings.get("WEBAPP_ENDPOINT")
    api_endpoint = f"{base_url}/api-review/start"

    resp = requests.post(api_endpoint, json=payload, headers=_build_auth_header(), timeout=60)
    if resp.status_code == 202:
        return resp.json()
    else:
        print(f"Error: {resp.status_code} {resp.text}")


def review_job_get(job_id: str, remote: bool = False):
    """Get the status/result of an API review job."""
    if remote:
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        api_endpoint = f"{base_url}/api-review"
        url = f"{api_endpoint.rstrip('/')}/{job_id}"

        headers = _build_auth_header()
        resp = requests.get(url, headers=headers, timeout=10)
        if resp.status_code == 200:
            return resp.json()
        else:
            print(f"Error: {resp.status_code} {resp.text}")
    else:
        db = DatabaseManager.get_instance()
        try:
            job = db.review_jobs.get(job_id)
            return job
        except Exception as e:
            print(f"Error: Job '{job_id}' not found: {e}")


def get_all_guidelines(language: str, markdown: bool = False):
    """
    Retrieve all guidelines for a specific language. Returns a context
    object which can be printed as JSON or Markdown.
    """
    search = SearchManager(language=language)
    context = search.build_context(search.language_guidelines.results)
    if markdown:
        md = context.to_markdown()
        print(md)
    else:
        print(json.dumps(context, indent=2, cls=CustomJSONEncoder))


def run_pytest(args: str = None):
    """Run unit tests with pytest."""
    import shlex
    import subprocess

    cmd = [sys.executable, "-m", "pytest", "tests"]
    if args:
        cmd.extend(shlex.split(args))
    result = subprocess.run(cmd, cwd=pathlib.Path(__file__).parent)
    sys.exit(result.returncode)


def extract_document_section(apiview_path: str, size: int, index: int = 1):
    """
    Extracts a section of a document for testing purposes.

    apiview_path: Path to the document file.
    size: Size of the section to extract.
    index: Index of the section to extract (default is 1).
    """
    from src._sectioned_document import SectionedDocument

    if not os.path.exists(apiview_path):
        raise ValueError(f"File {apiview_path} does not exist.")
    with open(apiview_path, "r", encoding="utf-8") as f:
        content = f.read()
    sectioned = SectionedDocument(lines=content.splitlines(), max_chunk_size=size)
    try:
        section = sectioned.sections[index - 1]
        print(str(section))
    except IndexError:
        print(f"Error: Index {index} out of range. Document has {len(sectioned.sections)} sections.")


def search_knowledge_base(
    language: Optional[str] = None,
    text: Optional[str] = None,
    path: Optional[str] = None,
    markdown: bool = False,
    ids: Optional[List[str]] = None,
):
    """
    Queries the Search indexes and returns the resulting Cosmos DB
    objects, resolving all links between objects. This result represents
    what the AI reviewer would receive as context in RAG mode when used
    with the Markdown flag.
    """
    # ensure that if ids is provided, no other parameters are provided
    if ids:
        if language or text or path or markdown:
            raise ValueError("When using `--ids`, do not provide any other parameters.")
        search = SearchManager()
        results = search.search_all_by_id(ids)
        print(json.dumps([result.__dict__ for result in results], indent=2, cls=CustomJSONEncoder))
        return
    elif not language:
        raise ValueError("`--language` is required when `--ids` is not provided.")

    if (path and text) or (not path and not text):
        raise ValueError("Provide one of `--path` or `--text`.")
    search = SearchManager(language=language)
    query = text
    if path:
        with open(path, "r", encoding="utf-8") as f:
            query = f.read()
    results = search.search_all(query=query)
    context = search.build_context(results.results)
    if markdown:
        md = context.to_markdown()
        print(md)
    else:
        print(json.dumps(context, indent=2, cls=CustomJSONEncoder))


def reindex_search(containers: Optional[list[str]] = None):
    """
    Trigger a reindex of the Azure Search index for the ArchAgent Knowledge Base.
    If no container is specified, reindex all containers.
    """
    containers = containers or ContainerNames.data_containers()
    return SearchManager.run_indexers(container_names=containers)


def check_links_kb(language: Optional[str] = None, fix: Optional[str] = None):
    """Audit bidirectional links between memories, guidelines, and examples. Report broken or one-way links.

    With --fix all|broken|oneway, repairs the selected category of issues.
    """
    from src._utils import guideline_id_from_db, guideline_id_to_db

    db = DatabaseManager.get_instance()

    # ── 1. Load all items from the three KB containers ──────────────────
    def _query_all(container, language_filter=None):
        if language_filter:
            query = "SELECT * FROM c WHERE (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false) AND c.language = @lang"
            params = [{"name": "@lang", "value": language_filter}]
        else:
            query = "SELECT * FROM c WHERE NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false"
            params = None
        return list(container.client.query_items(query=query, parameters=params, enable_cross_partition_query=True))

    print("Loading knowledge base items...")
    guidelines = _query_all(db.guidelines, language)
    examples = _query_all(db.examples, language)
    memories = _query_all(db.memories, language)
    print(f"  guidelines: {len(guidelines)}, examples: {len(examples)}, memories: {len(memories)}")

    # Build lookup indices  (id → raw dict)
    # For guidelines, IDs are stored in DB format (= delimiters)
    guideline_index = {item["id"]: item for item in guidelines}
    example_index = {item["id"]: item for item in examples}
    memory_index = {item["id"]: item for item in memories}

    # Also build a set of all known IDs for quick existence checks
    all_guideline_ids = set(guideline_index.keys())
    all_example_ids = set(example_index.keys())
    all_memory_ids = set(memory_index.keys())

    # ── 2. Define relationship pairs to check ───────────────────────────
    # Each tuple: (container_name, items_dict, field_name, target_container_name, target_ids_set,
    #              reverse_field_name, target_items_dict, is_guideline_link)
    #
    # Bidirectional pairs:
    #   guideline.related_memories  ↔  memory.related_guidelines
    #   guideline.related_examples  ↔  example.guideline_ids
    #   guideline.related_guidelines ↔ guideline.related_guidelines  (symmetric)
    #   memory.related_examples     ↔  example.memory_ids
    #   memory.related_memories     ↔  memory.related_memories        (symmetric)

    issues_dangling = []  # references to non-existent items
    issues_one_way = []  # A→B but B does not reference A back

    def _check_links(source_type, source_items, field, target_type, target_ids_set, target_items, reverse_field):
        """Check one direction of a relationship pair."""
        for item in source_items.values():
            source_id = item["id"]
            refs = item.get(field, [])
            for ref_id in refs:
                # Dangling reference?
                if ref_id not in target_ids_set:
                    issues_dangling.append(
                        {
                            "source_type": source_type,
                            "source_id": guideline_id_from_db(source_id) if source_type == "guideline" else source_id,
                            "field": field,
                            "target_type": target_type,
                            "missing_id": guideline_id_from_db(ref_id) if target_type == "guideline" else ref_id,
                            # Store raw IDs for fix operations
                            "_raw_source_id": source_id,
                            "_raw_ref_id": ref_id,
                            "_source_container": source_type,
                        }
                    )
                    continue

                # One-way link? Check reverse direction
                target_item = target_items[ref_id]
                reverse_refs = target_item.get(reverse_field, [])
                # For the reverse link, the source_id must appear in the target's reverse field
                # Guideline IDs are already in DB format in the dict
                if source_id not in reverse_refs:
                    issues_one_way.append(
                        {
                            "source_type": source_type,
                            "source_id": guideline_id_from_db(source_id) if source_type == "guideline" else source_id,
                            "field": field,
                            "target_type": target_type,
                            "target_id": guideline_id_from_db(ref_id) if target_type == "guideline" else ref_id,
                            "reverse_field": reverse_field,
                            # Store raw DB IDs for fix operations
                            "_raw_source_id": source_id,
                            "_raw_target_id": ref_id,
                            "_target_container": target_type,
                        }
                    )

    # Check all relationship directions
    _check_links(
        "guideline", guideline_index, "related_memories", "memory", all_memory_ids, memory_index, "related_guidelines"
    )
    _check_links(
        "guideline", guideline_index, "related_examples", "example", all_example_ids, example_index, "guideline_ids"
    )
    _check_links(
        "guideline",
        guideline_index,
        "related_guidelines",
        "guideline",
        all_guideline_ids,
        guideline_index,
        "related_guidelines",
    )
    _check_links(
        "memory",
        memory_index,
        "related_guidelines",
        "guideline",
        all_guideline_ids,
        guideline_index,
        "related_memories",
    )
    _check_links("memory", memory_index, "related_examples", "example", all_example_ids, example_index, "memory_ids")
    _check_links("memory", memory_index, "related_memories", "memory", all_memory_ids, memory_index, "related_memories")
    _check_links(
        "example", example_index, "guideline_ids", "guideline", all_guideline_ids, guideline_index, "related_examples"
    )
    _check_links("example", example_index, "memory_ids", "memory", all_memory_ids, memory_index, "related_examples")

    # ── 3. Deduplicate symmetric one-way issues ─────────────────────────
    # For symmetric relationships (guideline↔guideline, memory↔memory) we may
    # report A→B missing and B→A missing as separate issues.  Keep both — the
    # fix logic is idempotent and each represents a distinct missing back-ref.

    # ── 4. Report ────────────────────────────────────────────────────────
    total_issues = len(issues_dangling) + len(issues_one_way)
    if total_issues == 0:
        print(f"\n{GREEN}All links are healthy. No issues found.{RESET}")
        return

    print(f"\n{BOLD}Link audit results:{RESET}")
    if issues_dangling:
        print(f"\n  {Fore.RED}Dangling references ({len(issues_dangling)}):{RESET}")
        for issue in issues_dangling:
            print(
                f"    {issue['source_type']} '{issue['source_id']}' .{issue['field']}"
                f" -> {issue['target_type']} '{issue['missing_id']}' (NOT FOUND)"
            )

    if issues_one_way:
        print(f"\n  {Fore.YELLOW}One-way links ({len(issues_one_way)}):{RESET}")
        for issue in issues_one_way:
            print(
                f"    {issue['source_type']} '{issue['source_id']}' .{issue['field']}"
                f" -> {issue['target_type']} '{issue['target_id']}'"
                f" (missing reverse in .{issue['reverse_field']})"
            )

    print(f"\n  Total: {len(issues_dangling)} dangling, {len(issues_one_way)} one-way")

    # ── 5. Fix (if requested) ───────────────────────────────────────────
    if not fix:
        if issues_one_way or issues_dangling:
            print(f"\n  Run with {BOLD}--fix all|broken|oneway{RESET} to repair.")
        return

    fix_broken = fix in ("all", "broken")
    fix_oneway = fix in ("all", "oneway")

    containers_map = {
        "guideline": db.guidelines,
        "memory": db.memories,
        "example": db.examples,
    }
    fixed = 0
    errors = 0
    modified_containers = set()

    # ── 5a. Remove dangling references ──────────────────────────────────
    #   For guideline ID fields, first check if the ref is in web format
    #   (.html#) and converting to DB format (=html=) yields a valid,
    #   non-duplicate ID.  If so, heal the format instead of deleting.
    if fix_broken and issues_dangling:
        print(f"\n{BOLD}Fixing {len(issues_dangling)} dangling reference(s)...{RESET}")
        guideline_fields = {"related_guidelines", "guideline_ids"}
        # Group operations by (source_container, source_id) to batch per item
        # Each op is ("remove", field, ref_id) or ("replace", field, old_id, new_id)
        ops = {}  # (source_type, raw_source_id) -> [op, ...]
        for issue in issues_dangling:
            key = (issue["_source_container"], issue["_raw_source_id"])
            field = issue["field"]
            ref_id = issue["_raw_ref_id"]
            if field in guideline_fields and ".html#" in ref_id:
                converted = guideline_id_to_db(ref_id)
                if converted in all_guideline_ids:
                    # Check whether the converted ID is already in the source item's array
                    source_item = {
                        "guideline": guideline_index,
                        "memory": memory_index,
                        "example": example_index,
                    }[
                        issue["_source_container"]
                    ].get(issue["_raw_source_id"], {})
                    existing_refs = source_item.get(field, [])
                    if converted not in existing_refs:
                        ops.setdefault(key, []).append(("replace", field, ref_id, converted))
                        continue
            ops.setdefault(key, []).append(("remove", field, ref_id))

        for (source_type, source_id), item_ops in ops.items():
            container = containers_map[source_type]
            try:
                item = container.get(source_id)
            except Exception as e:
                print(f"  {Fore.RED}Error retrieving {source_type} '{source_id}': {e}{RESET}")
                errors += 1
                continue

            changed = False
            for op in item_ops:
                arr = item.setdefault(op[1], [])
                if op[0] == "replace":
                    _, field, old_id, new_id = op
                    if old_id in arr:
                        idx = arr.index(old_id)
                        arr[idx] = new_id
                        changed = True
                        display_src = guideline_id_from_db(source_id) if source_type == "guideline" else source_id
                        print(
                            f"  {GREEN}Healed {source_type} '{display_src}' .{field}: "
                            f"'{guideline_id_from_db(old_id)}' -> '{guideline_id_from_db(new_id)}'{RESET}"
                        )
                else:  # remove
                    _, field, ref_id = op
                    if ref_id in arr:
                        arr.remove(ref_id)
                        changed = True

            if changed:
                try:
                    container.client.upsert_item(item)
                    display_id = guideline_id_from_db(source_id) if source_type == "guideline" else source_id
                    removals_count = sum(1 for o in item_ops if o[0] == "remove")
                    heals_count = sum(1 for o in item_ops if o[0] == "replace")
                    parts = []
                    if heals_count:
                        parts.append(f"{heals_count} healed")
                    if removals_count:
                        parts.append(f"{removals_count} removed")
                    print(f"  {GREEN}Updated {source_type} '{display_id}' ({', '.join(parts)}){RESET}")
                    fixed += 1
                    modified_containers.add(source_type)
                except Exception as e:
                    display_id = guideline_id_from_db(source_id) if source_type == "guideline" else source_id
                    print(f"  {Fore.RED}Error updating {source_type} '{display_id}': {e}{RESET}")
                    errors += 1

    # ── 5b. Add missing back-references for one-way links ───────────────
    if fix_oneway and issues_one_way:
        print(f"\n{BOLD}Fixing {len(issues_one_way)} one-way link(s)...{RESET}")
        # Collect all needed updates: target_container -> target_id -> (reverse_field, id_to_add)
        updates = {}
        for issue in issues_one_way:
            target_type = issue["_target_container"]
            target_id = issue["_raw_target_id"]
            reverse_field = issue["reverse_field"]
            source_id = issue["_raw_source_id"]
            key = (target_type, target_id)
            updates.setdefault(key, []).append((reverse_field, source_id))

        for (target_type, target_id), adds in updates.items():
            container = containers_map[target_type]
            try:
                item = container.get(target_id)
            except Exception as e:
                print(f"  {Fore.RED}Error retrieving {target_type} '{target_id}': {e}{RESET}")
                errors += 1
                continue

            changed = False
            for reverse_field, source_id in adds:
                refs = item.setdefault(reverse_field, [])
                if source_id not in refs:
                    refs.append(source_id)
                    changed = True

            if changed:
                try:
                    container.client.upsert_item(item)
                    display_id = guideline_id_from_db(target_id) if target_type == "guideline" else target_id
                    print(f"  {GREEN}Fixed {target_type} '{display_id}'{RESET}")
                    fixed += 1
                    modified_containers.add(target_type)
                except Exception as e:
                    display_id = guideline_id_from_db(target_id) if target_type == "guideline" else target_id
                    print(f"  {Fore.RED}Error updating {target_type} '{display_id}': {e}{RESET}")
                    errors += 1

    # Trigger indexers for modified containers
    if modified_containers:
        _try_run_indexers([(t, containers_map[t]) for t in modified_containers])

    print(f"\n  Fixed: {fixed}, Errors: {errors}")


def consolidate_memories_kb(
    kind: str,
    ids: List[str],
    apply: bool = False,
):
    """Find and merge duplicate memories linked to the specified items.

    Requires --kind (guideline, example, or memory) and --ids (one or more IDs).
    By default, runs in dry-run mode. Pass --apply to execute.
    """
    from src._memory_utils import apply_consolidation, find_consolidation_candidates
    from src._utils import guideline_id_from_db

    print(f"Scanning memory clusters for {len(ids)} {kind}(s)...")
    actions = find_consolidation_candidates(kind=kind, ids=ids)

    if not actions:
        print(f"\n{GREEN}No duplicate memories found.{RESET}")
        return

    # Report findings
    total_groups = sum(len(a["groups"]) for a in actions)
    total_redundant = sum(len(g["memory_ids"]) - 1 for a in actions for g in a["groups"])
    print(
        f"\nFound {total_groups} merge group(s) across {len(actions)} parent(s), "
        f"affecting {total_redundant} redundant memory(ies):\n"
    )

    for action in actions:
        parent_id = action["parent_id"]
        if action["parent_type"] == "guideline":
            parent_id = guideline_id_from_db(parent_id)
        print(f"  {BOLD}{action['parent_type']}: {action['parent_title']}{RESET}")
        print(f"    ID: {parent_id}")
        for i, group in enumerate(action["groups"], 1):
            print(f"\n    Group {i}: merge {len(group['memory_ids'])} memories")
            print(f"      Reason: {group['reason']}")
            print(f"      Merged title: {group['merged_title']}")
            for mid in group["memory_ids"]:
                print(f"        - {mid}")
        print()

    if not apply:
        print(f"Run with {BOLD}--apply{RESET} to execute the consolidation.")
        return

    print(f"{BOLD}Applying consolidation...{RESET}")
    result = apply_consolidation(actions)
    print(f"\n{GREEN}Consolidation complete.{RESET}")
    print(f"  Merged: {result['merged']} survivor(s) updated")
    print(f"  Deleted: {result['deleted']} redundant memory(ies)")
    if result["errors"]:
        print(f"  {Fore.RED}Errors ({len(result['errors'])}):{RESET}")
        for err in result["errors"]:
            print(f"    - {err}")


def review_summarize(language: str, target: str, base: str = None, remote: bool = False):
    """
    Summarize an API or a diff of two APIs.
    """
    if remote:
        payload = {"language": language, "target": target}
        if base:
            payload["base"] = base
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        api_endpoint = f"{base_url}/api-review/summarize"

        response = requests.post(api_endpoint, json=payload, headers=_build_auth_header(), timeout=60)
        if response.status_code == 200:
            summary = response.json().get("summary")
            print(summary)
        else:
            print(f"Error: {response.status_code} - {response.text}")
    else:
        from src._diff import create_diff_with_line_numbers

        with open(target, "r", encoding="utf-8") as f:
            target_content = f.read()

        pretty_language = get_language_pretty_name(language)

        if base:
            with open(base, "r", encoding="utf-8") as f:
                base_content = f.read()
            content = create_diff_with_line_numbers(old=base_content, new=target_content)
            summary = run_prompt(
                folder="summarize", filename="summarize_diff", inputs={"language": pretty_language, "content": content}
            )
        else:
            summary = run_prompt(
                folder="summarize",
                filename="summarize_api",
                inputs={"language": pretty_language, "content": target_content},
            )

        print(summary)


def handle_agent_chat(
    thread_id: Optional[str] = None, remote: bool = False, quiet: bool = False, readonly: bool = False
):
    """
    Start or continue an interactive chat session with the agent.

    Args:
        thread_id: Optional thread ID to continue a previous conversation
        remote: Whether to use remote API or local agent
        quiet: If True, suppress error messages during tool execution (agent will retry automatically)
        readonly: If True, force readonly mode even if user has write permissions
    """

    async def async_input(prompt: str) -> str:
        # Run input() in a thread to avoid blocking the event loop
        return await asyncio.to_thread(input, prompt)

    async def chat():
        # Suppress Azure SDK error logs if quiet mode is enabled
        if quiet:
            logging.getLogger("azure").setLevel(logging.CRITICAL)

        print(f"{BOLD}Interactive API Review Agent Chat. Type 'exit' to quit.\n{RESET}")
        messages = []
        current_thread_id = thread_id
        if remote:
            settings = SettingsManager()
            base_url = settings.get("WEBAPP_ENDPOINT")
            api_endpoint = f"{base_url}/agent/chat"
            session = requests.Session()
            # Inline _build_auth_header in requests below
            while True:
                try:
                    user_input = await async_input(f"{BOLD_GREEN}You:{RESET} ")
                except (EOFError, KeyboardInterrupt):
                    print(f"\n{BOLD}Exiting chat.{RESET}")
                    if current_thread_id:
                        print(
                            # pylint: disable=line-too-long
                            f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                        )
                    break
                if user_input.strip().lower() in {"exit", "quit"}:
                    print(f"\n{BOLD}Exiting chat.{RESET}")
                    if current_thread_id:
                        print(
                            # pylint: disable=line-too-long
                            f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                        )
                    break
                if not user_input.strip():
                    continue
                try:
                    payload = {"userInput": user_input}
                    if current_thread_id:
                        payload["threadId"] = current_thread_id
                    resp = session.post(api_endpoint, json=payload, headers=_build_auth_header(), timeout=60)
                    if resp.status_code == 200:
                        data = resp.json()
                        response = data.get("response", "")
                        thread_id_out = data.get("threadId", current_thread_id)
                        print(f"{BOLD_BLUE}Agent:{RESET} {response}\n")
                        current_thread_id = thread_id_out
                    else:
                        print(f"Error: {resp.status_code} - {resp.text}")
                except Exception as e:
                    print(f"Error: {e}")
        else:
            # Local mode: select read-only vs read-write based on the caller's AAD token roles
            token = _try_get_access_token()
            claims = _get_unverified_token_claims(token) if token else {}

            # Force readonly if flag is set, otherwise use permissions-based selection
            if readonly:
                agent_cm = get_readonly_agent
            else:
                agent_cm = get_readwrite_agent if _claims_is_writer(claims) else get_readonly_agent

            mode = "readwrite" if agent_cm is get_readwrite_agent else "readonly"
            print(f"{BOLD}Local agent mode:{RESET} {mode}\n")

            with agent_cm() as (client, agent_id):
                while True:
                    try:
                        user_input = await async_input(f"{BOLD_GREEN}You:{RESET} ")
                    except (EOFError, KeyboardInterrupt):
                        print(f"\n{BOLD}Exiting chat.{RESET}")
                        if current_thread_id:
                            print(
                                # pylint: disable=line-too-long
                                f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                            )
                        break
                    if user_input.strip().lower() in {"exit", "quit"}:
                        print(f"\n{BOLD}Exiting chat.{RESET}")
                        if current_thread_id:
                            print(
                                # pylint: disable=line-too-long
                                f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                            )
                        break
                    if not user_input.strip():
                        continue
                    try:
                        print(f"{BLUE}Processing...{RESET}", end="", flush=True)
                        response, thread_id_out, messages = await invoke_agent(
                            client=client,
                            agent_id=agent_id,
                            user_input=user_input,
                            thread_id=current_thread_id,
                            messages=messages,
                        )
                        print(f"\r{' ' * 20}\r", end="", flush=True)  # Clear "Processing..." line
                        print(f"{BOLD_BLUE}Agent:{RESET} {response}\n")
                        current_thread_id = thread_id_out
                    except Exception as e:
                        print(f"\r{' ' * 20}\r", end="", flush=True)  # Clear "Processing..." line
                        print(f"Error: {e}")

    asyncio.run(chat())


def handle_agent_mention(
    comments_path: str = None,
    fetch_comment_id: str = None,
    remote: bool = False,
    dry_run: bool = False,
    source_comment_id: str = None,
):
    """
    Handles @mention requests from the agent.

    Can be invoked in two ways:
    1. With --comments-path: Load comments from a JSON file
    2. With --fetch-comment-id: Fetch a comment from the database and manufacture a feedback conversation

    At least one of --comments-path or --fetch-comment-id must be provided.
    """
    if not comments_path and not fetch_comment_id:
        print("Error: Either --comments-path or --fetch-comment-id must be provided.")
        return

    if comments_path and fetch_comment_id:
        print("Error: Only one of --comments-path or --fetch-comment-id can be provided, not both.")
        return

    comments = []
    language = None
    package_name = None
    code = None

    if fetch_comment_id:
        source_comment_id = fetch_comment_id
        environment = os.getenv("ENVIRONMENT_NAME")
        if not environment:
            print("Error: ENVIRONMENT_NAME environment variable is not set. Please set it in your .env file.")
            return
        # Fetch the comment from the database and manufacture a conversation
        result = get_comment_with_context(fetch_comment_id, environment=environment)
        if not result:
            print(f"Comment with ID '{fetch_comment_id}' not found in {environment} environment.")
            return

        language = result.get("language")
        package_name = result.get("package_name")
        code = result.get("code")
        feedback_text = result.get("feedback_text")
        original_comment = result.get("comment", {})
        original_text = original_comment.get("CommentText", "")

        if not language:
            print(f"Could not determine language for comment '{fetch_comment_id}'.")
            return

        if not feedback_text or feedback_text == "No feedback entries found.":
            print(f"No feedback associated with comment '{fetch_comment_id}'. Nothing to process.")
            return

        # Manufacture a conversation matching the ApiViewAgentComment dict shape
        # that the production C# caller sends.
        comments = [
            {
                "lineNo": 0,
                "createdOn": original_comment.get("CreatedOn", ""),
                "createdBy": original_comment.get("CreatedBy", "APIView Copilot"),
                "commentText": original_text,
                "upvotes": (
                    len(original_comment.get("Upvotes", []))
                    if isinstance(original_comment.get("Upvotes"), list)
                    else original_comment.get("Upvotes", 0)
                ),
                "downvotes": (
                    len(original_comment.get("Downvotes", []))
                    if isinstance(original_comment.get("Downvotes"), list)
                    else original_comment.get("Downvotes", 0)
                ),
                "isResolved": original_comment.get("IsResolved", False),
                "severity": original_comment.get("Severity", ""),
                "threadId": original_comment.get("ThreadId", ""),
            },
            {
                "lineNo": 0,
                "createdOn": "",
                "createdBy": "Reviewer",
                "commentText": feedback_text,
                "upvotes": 0,
                "downvotes": 0,
                "isResolved": False,
                "severity": "",
                "threadId": original_comment.get("ThreadId", ""),
            },
        ]

        print(f"Processing feedback for comment '{fetch_comment_id}':")
        print(f"  Language: {language}")
        print(f"  Package: {package_name}")
        print(f"  Original comment: {original_text[:100]}...")
        print(f"  Feedback: {feedback_text}")
        print()

    else:
        # Load comments from the comments_path
        if os.path.exists(comments_path):
            with open(comments_path, "r", encoding="utf-8") as f:
                data = json.load(f)
        else:
            print(f"Comments file {comments_path} does not exist.")
            return
        comments = data.get("comments", [])
        language = data.get("language", None)
        package_name = data.get("package_name", None)
        code = data.get("code", None)
        source_comment_id = data.get("source_comment_id", None) or source_comment_id

    # Resolve language to canonical and pretty forms
    try:
        apiview_language, pretty_language = resolve_language(language)
    except ValueError:
        print(f"Unsupported language `{language}`")
        return

    if dry_run:
        payload = {
            "comments": comments,
            "language": pretty_language,
            "packageName": package_name,
            "code": code,
            "sourceCommentId": source_comment_id,
        }
        print(f"{BOLD_BLUE}=== DRY RUN: Mention Agent Payload ==={RESET}")
        print(json.dumps(payload, indent=2))
        return

    if remote:
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        api_endpoint = f"{base_url}/api-review/mention"
        try:
            resp = requests.post(
                api_endpoint,
                json={
                    "comments": comments,
                    "language": language,
                    "packageName": package_name,
                    "code": code,
                    "sourceCommentId": source_comment_id,
                },
                headers=_build_auth_header(),
                timeout=60,
            )
            data = resp.json()
            if resp.status_code == 200:
                print(f"{BOLD_BLUE}Agent response:{RESET}\n{data.get('response', '')}\n")
            else:
                print(f"Error: {resp.status_code} - {data}")
        except Exception as e:
            print(f"Error: {e}")
    else:
        return handle_mention_request(
            comments=comments,
            language=pretty_language,
            package_name=package_name,
            code=code,
            source_comment_id=source_comment_id,
        )


def handle_agent_thread_resolution(comments_path: str, remote: bool = False):
    """
    Handles requests to update the knowledge base when a conversation is resolved.
    """
    # load comments from the comments_path
    comments = []
    if os.path.exists(comments_path):
        with open(comments_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    else:
        print(f"Comments file {comments_path} does not exist.")
        return
    comments = data.get("comments", [])
    language = data.get("language", None)
    package_name = data.get("package_name", None)
    code = data.get("code", None)
    source_comment_id = data.get("source_comment_id", None)
    # Resolve language to canonical and pretty forms
    try:
        apiview_language, pretty_language = resolve_language(language)
    except ValueError:
        print(f"Unsupported language `{language}`")
        return

    if remote:
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        api_endpoint = f"{base_url}/api-review/resolve"
        try:
            resp = requests.post(
                api_endpoint,
                json={
                    "comments": comments,
                    "language": language,
                    "packageName": package_name,
                    "code": code,
                    "sourceCommentId": source_comment_id,
                },
                headers=_build_auth_header(),
                timeout=60,
            )
            data = resp.json()
            if resp.status_code == 200:
                print(f"{BOLD_BLUE}Agent response:{RESET}\n{data.get('response', '')}\n")
            else:
                print(f"Error: {resp.status_code} - {data}")
        except Exception as e:
            print(f"Error: {e}")
    else:
        return handle_thread_resolution_request(
            comments=comments,
            language=pretty_language,
            package_name=package_name,
            code=code,
            source_comment_id=source_comment_id,
        )


def db_get(container_name: str, id: str):
    """Retrieve an item from the database."""
    db = DatabaseManager.get_instance()
    container = db.get_container_client(container_name)
    try:
        item = container.get(id)
        print(json.dumps(item, indent=2))
    except Exception as e:
        print(f"Error retrieving item: {e}")


def db_delete(container_name: str, id: str):
    """Soft-delete an item from the database.

    Automatically removes back-links from related items. Orphaned examples
    (no remaining ``memory_ids`` or ``guideline_ids``) are soft-deleted.
    Orphaned memories and guidelines are always retained.
    """
    db = DatabaseManager.get_instance()
    container = db.get_container_client(container_name)

    # Map container names to item types
    container_type = {
        "guidelines": "guideline",
        "memories": "memory",
        "examples": "example",
    }.get(container_name)

    try:
        item = container.get(id)
    except Exception as e:
        print(f"Error retrieving item: {e}")
        return

    if container_type:
        _cascade_unlink(db, item, container_type)

    try:
        container.delete(id)
        print(f"Item {id} soft-deleted from container {container_name}.")
    except Exception as e:
        print(f"Error deleting item: {e}")


# Mapping: (source_type) -> list of (field_on_source, target_type, field_on_target)
_RELATION_MAP = {
    "guideline": [
        ("related_memories", "memory", "related_guidelines"),
        ("related_examples", "example", "guideline_ids"),
        ("related_guidelines", "guideline", "related_guidelines"),
    ],
    "memory": [
        ("related_guidelines", "guideline", "related_memories"),
        ("related_examples", "example", "memory_ids"),
        ("related_memories", "memory", "related_memories"),
    ],
    "example": [
        ("guideline_ids", "guideline", "related_examples"),
        ("memory_ids", "memory", "related_examples"),
    ],
}

_TYPE_CONTAINERS = {
    "guideline": "guidelines",
    "memory": "memories",
    "example": "examples",
}


def _get_container(db, item_type: str):
    return getattr(db, _TYPE_CONTAINERS[item_type])


def _cascade_unlink(db, item: dict, item_type: str):
    """Remove back-links from all related items. Soft-delete orphaned examples."""
    item_id = item["id"]

    for source_field, target_type, target_field in _RELATION_MAP.get(item_type, []):
        target_container = _get_container(db, target_type)

        for target_id in item.get(source_field, []):
            try:
                raw_target = target_container.get(target_id)
                refs = raw_target.get(target_field, [])
                if item_id in refs:
                    refs.remove(item_id)

                # Check if the target is now orphaned and should be deleted
                if (
                    target_type == "example"
                    and not raw_target.get("memory_ids", [])
                    and not raw_target.get("guideline_ids", [])
                ):
                    target_container.delete(target_id, run_indexer=False)
                    print(f"  Soft-deleted orphaned example {target_id}")
                else:
                    target_container.client.upsert_item(raw_target)
                    print(f"  Unlinked {target_type} {target_id}")
            except Exception as e:
                print(f"  Warning: failed to clean {target_type} {target_id}: {e}")


def _try_run_indexers(containers: list[tuple[str, object]]):
    """Best-effort trigger of search indexers for the given (label, container) pairs."""
    for label, container in containers:
        try:
            container.run_indexer()
        except Exception as e:
            print(f"Warning: Failed to trigger indexer for {label}: {e}. Run `avc search reindex` manually.")


def db_link(guideline: str = None, memory: str = None, example: str = None, reindex: bool = False):
    """Link two knowledge base items by adding each other's ID to their related collections."""
    # Validate exactly two of the three are provided
    provided = [(k, v) for k, v in [("guideline", guideline), ("memory", memory), ("example", example)] if v]
    if len(provided) != 2:
        print("Error: Provide exactly two of --guideline (-g), --memory (-m), --example (-e).")
        return

    db = DatabaseManager.get_instance()
    type_a, id_a = provided[0]
    type_b, id_b = provided[1]

    try:
        result = db.link_and_save(type_a, id_a, type_b, id_b, run_indexer=False)
    except Exception as e:
        print(f"Error: {e}")
        return

    if not result["changed"]:
        print(f"{type_a} '{result['stored_id_a']}' and {type_b} '{result['stored_id_b']}' are already linked.")
        return

    containers = {"guideline": db.guidelines, "memory": db.memories, "example": db.examples}
    _try_run_indexers([(type_a, containers[type_a]), (type_b, containers[type_b])])

    print(f"Linked {type_a} '{result['stored_id_a']}' <-> {type_b} '{result['stored_id_b']}'.")
    print(f"  {type_a}.{result['field_a']} += '{result['stored_id_b']}'")
    print(f"  {type_b}.{result['field_b']} += '{result['stored_id_a']}'")

    if reindex:
        print("Reindexing...")
        reindex_search()


def db_unlink(guideline: str = None, memory: str = None, example: str = None, reindex: bool = False):
    """Remove the link between two knowledge base items."""
    provided = [(k, v) for k, v in [("guideline", guideline), ("memory", memory), ("example", example)] if v]
    if len(provided) != 2:
        print("Error: Provide exactly two of --guideline (-g), --memory (-m), --example (-e).")
        return

    db = DatabaseManager.get_instance()
    type_a, id_a = provided[0]
    type_b, id_b = provided[1]

    containers = {
        "guideline": db.guidelines,
        "memory": db.memories,
        "example": db.examples,
    }
    container_a = containers[type_a]
    container_b = containers[type_b]

    try:
        item_a = container_a.get(id_a)
    except Exception as e:
        print(f"Error retrieving {type_a} '{id_a}': {e}")
        return

    try:
        item_b = container_b.get(id_b)
    except Exception as e:
        print(f"Error retrieving {type_b} '{id_b}': {e}")
        return

    original_a = json.loads(json.dumps(item_a))

    relation_fields = {
        ("guideline", "memory"): ("related_memories", "related_guidelines"),
        ("guideline", "example"): ("related_examples", "guideline_ids"),
        ("memory", "example"): ("related_examples", "memory_ids"),
        ("memory", "guideline"): ("related_guidelines", "related_memories"),
        ("example", "guideline"): ("guideline_ids", "related_examples"),
        ("example", "memory"): ("memory_ids", "related_examples"),
    }
    field_a, field_b = relation_fields[(type_a, type_b)]

    stored_id_a = item_a["id"]
    stored_id_b = item_b["id"]

    a_changed = False
    if stored_id_b in item_a.get(field_a, []):
        item_a[field_a].remove(stored_id_b)
        a_changed = True

    b_changed = False
    if stored_id_a in item_b.get(field_b, []):
        item_b[field_b].remove(stored_id_a)
        b_changed = True

    if not a_changed and not b_changed:
        print(f"{type_a} '{stored_id_a}' and {type_b} '{stored_id_b}' are not linked.")
        return

    try:
        container_a.client.upsert_item(item_a)
    except Exception as e:
        print(f"Error updating {type_a} '{stored_id_a}': {e}")
        return

    try:
        container_b.client.upsert_item(item_b)
    except Exception as e:
        print(f"Error updating {type_b} '{stored_id_b}': {e}. Rolling back {type_a} '{stored_id_a}'...")
        try:
            container_a.client.upsert_item(original_a)
            print("Rollback successful.")
        except Exception as rollback_err:
            print(f"CRITICAL: Rollback failed for {type_a} '{stored_id_a}': {rollback_err}")
        return

    _try_run_indexers([(type_a, container_a), (type_b, container_b)])

    print(f"Unlinked {type_a} '{stored_id_a}' <-> {type_b} '{stored_id_b}'.")
    if a_changed:
        print(f"  {type_a}.{field_a} -= '{stored_id_b}'")
    if b_changed:
        print(f"  {type_b}.{field_b} -= '{stored_id_a}'")

    if reindex:
        print("Reindexing...")
        reindex_search()


def db_purge(containers: Optional[list[str]] = None, run_indexer: bool = False):
    """Purge soft-deleted items from the database. Use --verbose for per-item output."""
    verbose = logging.getLogger().level <= logging.DEBUG
    gc = GarbageCollector()
    containers = containers or ContainerNames.data_containers()
    for container_name in containers:
        try:
            start_count = gc.get_item_count(container_name)
            if run_indexer:
                gc.run_indexer_and_purge(container_name, verbose=verbose)
            else:
                gc.purge_items(container_name, verbose=verbose)
            final_count = gc.get_item_count(container_name)
            if start_count - final_count:
                print(
                    f"Soft-deleted items purged from container {container_name}. {start_count - final_count} items removed."
                )
            else:
                print(f"No soft-deleted items to purge from container {container_name}.")
        except Exception as e:
            print(f"Error purging container: {e}")


def get_apiview_comments(revision_id: str, environment: str = "production") -> dict:
    """
    Retrieves comments for a specific APIView revision and returns them grouped by line number and
    sorted by createdOn time.
    """
    apiview = ApiViewClient(environment=environment)
    comments = asyncio.run(apiview.get_review_comments(revision_id=revision_id))
    conversations = {}
    if comments:
        for comment in comments:
            line_no = comment.get("lineNo")
            if line_no in conversations:
                conversations[line_no].append(comment)
            else:
                conversations[line_no] = [comment]
    for line_no, comments in conversations.items():
        # sort comments by created_on time
        comments.sort(key=lambda x: x.get("createdOn", 0))
    return conversations


def get_active_reviews(
    start_date: str,
    end_date: str,
    language: Optional[str] = None,
    environment: str = "production",
    summary: bool = False,
    approved_only: bool = False,
) -> list | None:
    """
    Retrieves active APIView reviews in the specified environment during the specified period.
    If --language is omitted, returns results for all languages.
    Use --approved-only to show only approved revisions (matching the metrics chart definition).
    """
    reviews, _ = _get_active_reviews(start_date, end_date, environment=environment)

    if language:
        _, pretty_language = resolve_language(language)
        pretty_language = pretty_language.lower()
        filtered = [r for r in reviews if r.language.lower() == pretty_language]
    else:
        pretty_language = None
        filtered = reviews

    if approved_only:
        # Filter to only revisions that have been approved during the period.
        # This matches the "active_review_count" definition used in the metrics charts.
        approved_filtered = []
        for r in filtered:
            approved_revs = [rev for rev in r.revisions if rev.approval is not None]
            if approved_revs:
                from copy import copy

                r_copy = copy(r)
                r_copy.revisions = approved_revs
                approved_filtered.append(r_copy)
        filtered = approved_filtered

    if summary:
        # Output summary format as a table: {package-name} {package-version} {APPROVED|unapproved}
        summary_data = []
        for r in filtered:
            for rev in r.revisions:
                status = "APPROVED" if rev.approval else "unapproved"
                copilot_status = "YES" if rev.has_copilot_review else "no"
                # Extract just the date from approval timestamp (YYYY-MM-DD)
                approval_date = rev.approval[:10] if rev.approval else "n/a"
                version_type = rev.version_type
                row = {
                    "name": r.name or "unknown",
                    "version": rev.package_version or "unknown",
                    "status": status,
                    "copilot": copilot_status,
                    "approval_date": approval_date,
                    "version_type": version_type,
                }
                if not pretty_language:
                    row["language"] = r.language
                summary_data.append(row)

        # Calculate column widths for proper alignment
        if summary_data:
            max_name_len = max(len(item["name"]) for item in summary_data)
            max_version_len = max(len(item["version"]) for item in summary_data)
            max_status_len = max(len(item["status"]) for item in summary_data)
            max_copilot_len = max(len(item["copilot"]) for item in summary_data)
            max_type_len = max(len(item["version_type"]) for item in summary_data)

            if not pretty_language:
                max_lang_len = max(len(item["language"]) for item in summary_data)
                # Print header with LANGUAGE column
                print(
                    f"{'LANGUAGE':<{max_lang_len}}\t{'PACKAGE':<{max_name_len}}\t{'VERSION':<{max_version_len}}\t{'STATUS':<{max_status_len}}\t{'COPILOT':<{max_copilot_len}}\t{'TYPE':<{max_type_len}}\tAPPROVED"
                )
                print(
                    "-"
                    * (
                        max_lang_len
                        + max_name_len
                        + max_version_len
                        + max_status_len
                        + max_copilot_len
                        + max_type_len
                        + 60
                    )
                )

                for item in summary_data:
                    print(
                        f"{item['language']:<{max_lang_len}}\t{item['name']:<{max_name_len}}\t{item['version']:<{max_version_len}}\t{item['status']:<{max_status_len}}\t{item['copilot']:<{max_copilot_len}}\t{item['version_type']:<{max_type_len}}\t{item['approval_date']}"
                    )
            else:
                # Print header without LANGUAGE column
                print(
                    f"{'PACKAGE':<{max_name_len}}\t{'VERSION':<{max_version_len}}\t{'STATUS':<{max_status_len}}\t{'COPILOT':<{max_copilot_len}}\t{'TYPE':<{max_type_len}}\tAPPROVED"
                )
                print("-" * (max_name_len + max_version_len + max_status_len + max_copilot_len + max_type_len + 50))

                for item in summary_data:
                    print(
                        f"{item['name']:<{max_name_len}}\t{item['version']:<{max_version_len}}\t{item['status']:<{max_status_len}}\t{item['copilot']:<{max_copilot_len}}\t{item['version_type']:<{max_type_len}}\t{item['approval_date']}"
                    )

        filter_label = " approved" if approved_only else ""
        language_label = f" in {pretty_language}" if pretty_language else ""
        print(
            f"\nFound {len(summary_data)}{filter_label} package versions{language_label} between {start_date} and {end_date}."
        )
        # Don't return anything in summary mode to avoid duplicate output
        return None
    else:
        # Output detailed JSON format
        filtered_dicts = []
        for r in filtered:
            d = {
                "review_id": r.review_id,
                "name": r.name,
                "revisions": [
                    {
                        "revision_ids": rev.revision_ids,
                        "package_version": rev.package_version,
                        "approval": rev.approval,
                        "has_copilot_review": rev.has_copilot_review,
                        "version_type": rev.version_type,
                    }
                    for rev in r.revisions
                ],
            }
            if not pretty_language:
                d["language"] = r.language
            filtered_dicts.append(d)
        language_label = f" in {pretty_language}" if pretty_language else ""
        print(f"Found {len(filtered_dicts)} reviews{language_label} between {start_date} and {end_date}.")
        return filtered_dicts


def resolve_package_info(
    package_query: str, language: str, version: str = None, environment: str = "production", remote: bool = False
):
    """
    Resolves package information from a package query and language.
    Returns the package name, review ID, and revision ID.
    """
    if remote:
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        api_endpoint = f"{base_url}/api-review/resolve-package"
        payload = {
            "packageQuery": package_query,
            "language": language,
            "environment": environment,
        }
        if version:
            payload["version"] = version
        try:
            resp = requests.post(api_endpoint, json=payload, headers=_build_auth_header(), timeout=60)
            if resp.status_code == 200:
                print(json.dumps(resp.json(), indent=2))
            else:
                print(f"Error: {resp.status_code} - {resp.text}")
        except Exception as e:
            print(f"Error: {e}")
    else:
        result = resolve_package(
            package_query=package_query, language=language, version=version, environment=environment
        )

        if result:
            if "error" in result:
                print(f"Error: {result.get('error')} - Language: {result.get('language')}")
            else:
                print(json.dumps(result, indent=2))
        else:
            print(f"No package found matching '{package_query}' for language '{language}'")


def list_created_revisions(
    start_date: str,
    end_date: str,
    environment: str = "production",
    exclude: list = None,
) -> None:
    """List the number of APIRevisions created in APIView in the given date window, by language and type."""
    data = get_created_revisions(start_date, end_date, environment=environment, exclude_languages=exclude)
    _print_revision_table(data, empty_msg="No revisions found in the specified date range.")


def list_opened_revisions(
    start_date: str,
    end_date: str,
    environment: str = "production",
    exclude: list = None,
    created_in_window: bool = False,
) -> None:
    """List revisions that were actually opened/viewed in APIView, by language and type.

    Queries Application Insights for reviews that had page views, then enriches
    with revision metadata from Cosmos DB.
    """
    data = get_opened_revisions(start_date, end_date, environment=environment, exclude_languages=exclude, created_in_window=created_in_window)
    _print_revision_table(data, empty_msg="No opened revisions found in the specified date range.")


def _print_revision_table(data: dict, *, empty_msg: str = "No revisions found.") -> None:
    """Shared table printer for revision breakdown commands."""
    by_language = data["by_language"]
    totals_by_type = data["totals_by_type"]
    total = data["total"]

    if not by_language:
        print(empty_msg)
        return

    # Collect all type names across languages
    all_types = sorted(totals_by_type.keys())

    # Build rows: one per language, plus a totals row
    rows = []
    for lang in sorted(by_language.keys()):
        counts = by_language[lang]
        lang_total = sum(counts.values())
        row = {"Language": lang}
        for t in all_types:
            val = counts.get(t, 0)
            col_total = totals_by_type.get(t, 0)
            pct = (val / col_total * 100) if col_total else 0
            row[t] = f"{val} ({pct:.0f}%)"
        lang_pct = (lang_total / total * 100) if total else 0
        row["Total"] = f"{lang_total} ({lang_pct:.0f}%)"
        rows.append(row)

    # Totals row
    totals_row = {"Language": "TOTAL"}
    for t in all_types:
        val = totals_by_type.get(t, 0)
        pct = (val / total * 100) if total else 0
        totals_row[t] = f"{val} ({pct:.0f}%)"
    totals_row["Total"] = str(total)

    # Compute column widths from display strings
    columns = ["Language"] + all_types + ["Total"]
    col_widths = {}
    for col in columns:
        col_widths[col] = max(len(col), max(len(str(r[col])) for r in rows + [totals_row]))

    # Print header
    header = "  ".join(f"{col:<{col_widths[col]}}" for col in columns)
    print(header)
    print("-" * len(header))

    # Print rows
    for row in rows:
        print("  ".join(f"{row[col]:<{col_widths[col]}}" for col in columns))

    # Print totals
    print("-" * len(header))
    print("  ".join(f"{totals_row[col]:<{col_widths[col]}}" for col in columns))


def report_metrics(
    start_date: str,
    end_date: str,
    environment: str = "production",
    save: bool = False,
    charts: bool = False,
    exclude: list = None,
) -> None:
    """Generate a report of APIView metrics between two dates."""
    report = get_metrics_report(start_date, end_date, environment, save, charts, exclude)
    sys.stdout.buffer.write(json.dumps(report, indent=2, ensure_ascii=False, default=str).encode("utf-8"))
    sys.stdout.buffer.write(b"\n")


def report_comment_bucket_trends(
    months: int = 6,
    languages: Optional[list[str]] = None,
    exclude_human: bool = False,
    neutral: bool = False,
    environment: str = "production",
    end_date: Optional[str] = None,
) -> None:
    """Generate a comment-bucket trend chart for the month window ending on end_date."""
    parsed_end_date = None
    if end_date:
        try:
            parsed_end_date = date.fromisoformat(end_date)
        except ValueError as exc:
            raise CLIError("Invalid --end-date value. Use YYYY-MM-DD format.") from exc

    normalized_languages = None
    if languages:
        normalized_languages = [resolve_language(language)[1] for language in languages]

    include_human = not exclude_human

    reports = build_language_comment_bucket_reports(
        languages=normalized_languages,
        months=months,
        end_date=parsed_end_date,
        include_human=include_human,
        include_neutral=neutral,
        environment=environment,
    )
    saved_path = generate_comment_bucket_chart(
        reports,
        output_path=DEFAULT_COMMENT_BUCKET_OUTPUT_PATH,
        include_human=include_human,
        include_neutral=neutral,
        raw=False,
        environment=environment,
    )
    print_comment_bucket_report(
        reports,
        saved_path,
        include_human=include_human,
        include_neutral=neutral,
        environment=environment,
    )

    # Generate breakout charts for generic vs guideline-backed AI comments (no human)
    generic_reports = build_language_comment_bucket_reports(
        languages=normalized_languages,
        months=months,
        end_date=parsed_end_date,
        include_human=False,
        include_neutral=neutral,
        generic_filter=True,
        environment=environment,
    )
    generate_comment_bucket_chart(
        generic_reports,
        output_path=DEFAULT_GENERIC_OUTPUT_PATH,
        include_human=False,
        include_neutral=neutral,
        raw=False,
        environment=environment,
        title_prefix="Generic AI Comments by Language",
    )

    guideline_reports = build_language_comment_bucket_reports(
        languages=normalized_languages,
        months=months,
        end_date=parsed_end_date,
        include_human=False,
        include_neutral=neutral,
        generic_filter=False,
        environment=environment,
    )
    generate_comment_bucket_chart(
        guideline_reports,
        output_path=DEFAULT_GUIDELINE_OUTPUT_PATH,
        include_human=False,
        include_neutral=neutral,
        raw=False,
        environment=environment,
        title_prefix="Guideline AI Comments by Language",
    )


def report_apiview_metrics(
    months: int = 6,
    languages: Optional[list[str]] = None,
    environment: str = "production",
    end_date: Optional[str] = None,
    chart: bool = False,
    summary: bool = False,
) -> None:
    """Generate APIView platform metrics (versioned-revision tracking and cross-language compliance)."""
    parsed_end_date = None
    if end_date:
        try:
            parsed_end_date = date.fromisoformat(end_date)
        except ValueError as exc:
            raise CLIError("Invalid --end-date value. Use YYYY-MM-DD format.") from exc

    normalized_languages = None
    if languages:
        normalized_languages = [resolve_language(language)[1] for language in languages]

    version_reports = build_version_reports(
        languages=normalized_languages,
        months=months,
        end_date=parsed_end_date,
        environment=environment,
    )

    compliance_reports = build_compliance_reports(
        languages=normalized_languages,
        months=months,
        end_date=parsed_end_date,
        environment=environment,
    )

    version_chart_path = None
    compliance_chart_path = None
    if chart:
        version_chart_path = generate_version_chart(
            version_reports,
            output_path=DEFAULT_VERSION_TRENDS_OUTPUT_PATH,
            environment=environment,
        )
        compliance_chart_path = generate_compliance_chart(
            compliance_reports,
            output_path=DEFAULT_COMPLIANCE_OUTPUT_PATH,
            environment=environment,
        )

    output = {"versions": version_reports, "compliance": compliance_reports}
    sys.stdout.buffer.write(json.dumps(output, indent=2, ensure_ascii=False, default=str).encode("utf-8"))
    sys.stdout.buffer.write(b"\n")

    if summary:
        print_version_report(version_reports, version_chart_path, environment=environment, file=sys.stderr)
        print_compliance_report(compliance_reports, compliance_chart_path, environment=environment, file=sys.stderr)


def grant_permissions(assignee_id: str = None):
    """
    Grants permissions for running AVC locally.
    """
    from src._permissions import (
        PrincipalType,
        assign_cosmosdb_roles,
        assign_keyvault_access,
        assign_rbac_roles,
        get_current_user_object_id,
    )

    if not assignee_id:
        assignee_id = get_current_user_object_id()

    if not assignee_id:
        raise ValueError("Error: Could not determine the current user ID. Provide `--assignee-id` or run `az login`.")

    subscription_id = "a18897a6-7e44-457d-9260-f2854c0aca42"

    # grant permissions for App Configuration Data Reader
    for rg_name in ["apiview-copilot", "apiview-copilot-staging"]:
        assign_rbac_roles(
            roles=["App Configuration Data Reader"],
            principal_id=assignee_id,
            principal_type=PrincipalType.USER,
            subscription_id=subscription_id,
            rg_name=rg_name,
        )

        # grant permissions for Search Index Data Reader
        assign_rbac_roles(
            roles=["Search Index Data Reader"],
            principal_id=assignee_id,
            principal_type=PrincipalType.USER,
            subscription_id=subscription_id,
            rg_name=rg_name,
        )

        # grant CosmosDB permissions
        cosmos_name = "avc-cosmos" if rg_name == "apiview-copilot" else "avc-cosmos-staging"
        assign_cosmosdb_roles(
            principal_id=assignee_id,
            principal_type=PrincipalType.USER,
            subscription_id=subscription_id,
            rg_name=rg_name,
            role_kind="readWrite",
            cosmos_account_name=cosmos_name,
        )

        # grant KeyVault access
        keyvault_name = "avc-vault" if rg_name == "apiview-copilot" else "avc-vault-staging"
        assign_keyvault_access(
            principal_id=assignee_id,
            subscription_id=subscription_id,
            rg_name=rg_name,
            vault_name=keyvault_name,
            tenant_id="72f988bf-86f1-41af-91ab-2d7cd011db47",
        )

    rg_name = "azsdk-engsys-ai"
    # grant permissions for Cognitive Services OpenAI User on the OpenAI resource
    assign_rbac_roles(
        roles=["Cognitive Services OpenAI User"],
        principal_id=assignee_id,
        principal_type=PrincipalType.USER,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-openai",
    )

    # grant permissions for Azure AI User on the Foundry resource
    assign_rbac_roles(
        roles=["Azure AI User"],
        principal_id=assignee_id,
        principal_type=PrincipalType.USER,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-ai",
    )
    print("✅ Permissions granted. Please run `az logout` and then `az login` to refresh your access.")


def revoke_permissions(assignee_id: str = None):
    """
    Revokes permissions for running AVC locally.
    """
    from azure.mgmt.resource import ManagementLockClient
    from src._credential import get_credential
    from src._permissions import (
        get_current_user_object_id,
        revoke_cosmosdb_roles,
        revoke_keyvault_access,
        revoke_rbac_roles,
    )

    if not assignee_id:
        assignee_id = get_current_user_object_id()

    if not assignee_id:
        raise ValueError("Error: Could not determine the current user ID. Provide `--assignee-id` or run `az login`.")

    subscription_id = "a18897a6-7e44-457d-9260-f2854c0aca42"
    credential = get_credential()
    lock_client = ManagementLockClient(credential, subscription_id)

    # temporarily delete the delete locks
    for rg_name in ["apiview-copilot", "apiview-copilot-staging", "azsdk-engsys-ai"]:
        locks = lock_client.management_locks.list_at_resource_group_level(rg_name)
        for lock in locks:
            if lock.level == "CanNotDelete":
                lock_client.management_locks.delete_at_resource_group_level(rg_name, lock.name)
                print(f"✅ Removed 'CanNotDelete' lock '{lock.name}' from resource group '{rg_name}'...")

    for rg_name in ["apiview-copilot", "apiview-copilot-staging"]:
        revoke_rbac_roles(
            roles=[
                "App Configuration Data Reader",
                "Search Index Data Reader",
                "DocumentDB Account Contributor",
            ],
            principal_id=assignee_id,
            subscription_id=subscription_id,
            rg_name=rg_name,
        )
        cosmos_name = "avc-cosmos" if rg_name == "apiview-copilot" else "avc-cosmos-staging"
        revoke_cosmosdb_roles(
            principal_id=assignee_id,
            subscription_id=subscription_id,
            rg_name=rg_name,
            cosmos_account_name=cosmos_name,
        )

        keyvault_name = "avc-vault" if rg_name == "apiview-copilot" else "avc-vault-staging"
        revoke_keyvault_access(
            principal_id=assignee_id, subscription_id=subscription_id, rg_name=rg_name, vault_name=keyvault_name
        )

    rg_name = "azsdk-engsys-ai"
    revoke_rbac_roles(
        roles=["Cognitive Services OpenAI User"],
        principal_id=assignee_id,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-openai",
    )
    revoke_rbac_roles(
        roles=["Azure AI User"],
        principal_id=assignee_id,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-ai",
    )

    # recreate the deleted locks
    for rg_name in ["apiview-copilot", "apiview-copilot-staging", "azsdk-engsys-ai"]:
        lock_name = f"lock-{rg_name}"
        lock_client.management_locks.create_or_update_at_resource_group_level(
            rg_name,
            lock_name=lock_name,
            parameters={
                "level": "CanNotDelete",
            },
        )
        print(f"✅ Re-created 'CanNotDelete' lock for resource group '{rg_name}'...")


def check_health(include_auth: bool = False):
    """
    Checks that the APIView Copilot service is healthy and accessible.
    """
    settings = SettingsManager()
    base_url = settings.get("WEBAPP_ENDPOINT")
    headers = []
    if include_auth:
        headers = _build_auth_header()
        api_endpoint = f"{base_url}/auth-test"
    else:
        api_endpoint = f"{base_url}/health-test"
    try:
        resp = requests.get(api_endpoint, headers=headers, timeout=10)
        if resp.status_code == 200:
            print("✅ APIView Copilot service is healthy.")
        else:
            print(f"❌ Service health check failed: {resp.status_code} - {resp.text}")
    except Exception as e:
        print(f"❌ Service health check error: {e}")


# ---------------------------------------------------------------------------
# Unified language resolution
# ---------------------------------------------------------------------------
# Build a single case-insensitive lookup that maps every known alias, pretty
# name, and canonical name to a (canonical, pretty) tuple.

_LANGUAGE_ALIAS_TABLE: dict[str, tuple[str, str]] = {}


def _build_language_alias_table():
    """Populate ``_LANGUAGE_ALIAS_TABLE`` once."""
    if _LANGUAGE_ALIAS_TABLE:
        return
    # Extra aliases beyond what SUPPORTED_LANGUAGES and get_language_pretty_name provide
    extra_aliases: dict[str, str] = {
        "csharp": "dotnet",
        "c#": "dotnet",
        "c++": "cpp",
        "go": "golang",
        "swift": "ios",
        "c": "clang",
        "javascript": "typescript",
    }
    for canonical in SUPPORTED_LANGUAGES:
        pretty = get_language_pretty_name(canonical)
        entry = (canonical, pretty)
        _LANGUAGE_ALIAS_TABLE[canonical.lower()] = entry
        _LANGUAGE_ALIAS_TABLE[pretty.lower()] = entry
    for alias, canonical in extra_aliases.items():
        pretty = get_language_pretty_name(canonical)
        _LANGUAGE_ALIAS_TABLE[alias.lower()] = (canonical, pretty)


def resolve_language(lang: str) -> tuple[str, str]:
    """Resolve any language string to ``(canonical, pretty)`` or raise ``ValueError``.

    Accepts canonical names (``"golang"``), pretty names (``"Go"``), and common
    aliases (``"csharp"``, ``"c#"``, ``"c++"``).  Case-insensitive.

    Returns:
        Tuple of ``(canonical, pretty)`` — e.g. ``("golang", "Go")``.

    Raises:
        ValueError: If the language string is not recognized.
    """
    _build_language_alias_table()
    if not lang:
        raise ValueError("Language must not be empty.")
    entry = _LANGUAGE_ALIAS_TABLE.get(lang.lower())
    if not entry:
        raise ValueError(
            f"Unsupported language `{lang}`. "
            f"Accepted values: {', '.join(sorted({v[1] for v in _LANGUAGE_ALIAS_TABLE.values()}))}"
        )
    return entry


def resolve_language_to_canonical(lang: str) -> str:
    """Knack ``type=`` converter: returns the canonical language name."""
    return resolve_language(lang)[0]


def get_architect_comments(
    start_date: str,
    end_date: str,
    language: Optional[str] = None,
    environment: str = "production",
    output_format: str = "json",
    all_commenters: bool = False,
    include_replies: bool = False,
):
    """
    Retrieve human architect review comments for a date range.
    Returns comments from language board approvers, excluding Diagnostic and AI-generated comments.
    If --language is omitted, returns results for all languages.
    If --all-commenters is set, includes comments from all users (not just approvers).
    By default, only the first comment in each thread (which has a Severity) is returned.
    Use --include-replies to also include reply comments.
    """
    raw_comments = get_comments_in_date_range(start_date, end_date, environment=environment)
    filtered = [c for c in raw_comments if c.get("CommentSource") != "AIGenerated"]

    allowed_commenters = None
    if not all_commenters:
        if language:
            pretty_language = resolve_language(language)[1]
            allowed_commenters = get_approvers(language=pretty_language, environment=environment)
        else:
            allowed_commenters = get_approvers(environment=environment)

    # Look up the language for each comment's review
    review_lang_map: dict[str, str] = {}
    review_ids = set(c.get("ReviewId") for c in filtered if c.get("ReviewId"))
    if review_ids:
        reviews_container = get_apiview_cosmos_client(container_name="Reviews", environment=environment)
        params = []
        clauses = []
        for i, rid in enumerate(review_ids):
            param_name = f"@id_{i}"
            clauses.append(f"c.id = {param_name}")
            params.append({"name": param_name, "value": rid})
        review_query = f"SELECT c.id, c.Language FROM c WHERE ({' OR '.join(clauses)})"
        review_results = list(
            reviews_container.query_items(
                query=review_query, parameters=params, enable_cross_partition_query=True
            )
        )
        review_lang_map = {r["id"]: get_language_pretty_name(r.get("Language", "")) for r in review_results}

    # Filter by language if specified
    if language:
        target_language = resolve_language(language)[1].lower()
        filtered = [c for c in filtered if review_lang_map.get(c.get("ReviewId"), "").lower() == target_language]

    # Compute ISO bounds and true thread start dates (via Cosmos) for both branches.
    start_iso = to_iso8601(start_date)
    end_iso = to_iso8601(end_date, end_of_day=True)
    thread_starts = get_thread_start_dates(filtered, environment=environment)
    started_in_window = {
        key for key, min_created in thread_starts.items() if start_iso <= min_created <= end_iso
    }

    # By default, exclude replies — keep only the thread-starting comment for threads
    # that actually *started* in the date window (not merely replied to).
    if not include_replies:
        # Keep only comments belonging to threads that started in the window
        filtered = [
            c
            for c in filtered
            if (c.get("ThreadId") or c.get("ElementId")) in started_in_window
        ]

        # Keep only the first (earliest) comment per thread
        seen_threads = {}
        for c in filtered:
            thread_key = c.get("ThreadId") or c.get("ElementId")
            if thread_key is None:
                # No ThreadId or ElementId — treat as standalone
                seen_threads[c.get("id")] = c
            elif thread_key not in seen_threads:
                seen_threads[thread_key] = c
            else:
                existing = seen_threads[thread_key]
                if existing.get("CreatedOn", "") > c.get("CreatedOn", ""):
                    seen_threads[thread_key] = c
        filtered = list(seen_threads.values())

        # When filtering to approvers, exclude threads whose starter is not an approver.
        if allowed_commenters is not None:
            filtered = [c for c in filtered if c.get("CreatedBy") in allowed_commenters]
    else:
        # When including replies, identify threads started by an approver and include
        # *all* comments in those threads (not just approver-authored ones).
        # First, restrict to threads that actually started in the date window.
        filtered = [
            c
            for c in filtered
            if (c.get("ThreadId") or c.get("ElementId")) in started_in_window
        ]

        if allowed_commenters is not None:
            # Find who authored the earliest comment per thread
            thread_starters: dict[str, str] = {}
            thread_earliest_in_window: dict[str, str] = {}
            for c in filtered:
                thread_key = c.get("ThreadId") or c.get("ElementId")
                if thread_key is None:
                    continue
                created = c.get("CreatedOn", "")
                if thread_key not in thread_earliest_in_window or created < thread_earliest_in_window[thread_key]:
                    thread_earliest_in_window[thread_key] = created
                    thread_starters[thread_key] = c.get("CreatedBy", "")

            approver_threads = {
                k for k, author in thread_starters.items() if author in allowed_commenters
            }
            filtered = [
                c
                for c in filtered
                if (c.get("ThreadId") or c.get("ElementId")) in approver_threads
            ]

    comments = [APIViewComment(**c) for c in filtered]

    results = [
        {
            **{k: v for k, v in comment.model_dump(by_alias=True, mode="json").items() if k in _APIVIEW_COMMENT_SELECT_FIELDS},
            "Language": review_lang_map.get(comment.review_id, ""),
        }
        for comment in comments
    ]
    if output_format == "yaml":
        print(yaml.dump(results, default_flow_style=False, allow_unicode=True, sort_keys=False))
    else:
        print(json.dumps(results, indent=2))


def _build_auth_header():
    """
    Helper to build Authorization header with Bearer token for WEBAPP_ENDPOINT requests.
    """
    from src._credential import get_credential

    credential = get_credential()
    settings = SettingsManager()
    app_id = settings.get("APP_ID")
    scope = f"api://{app_id}/.default"
    try:
        token = credential.get_token(scope)
    except ClientAuthenticationError as e:
        logging.error("Authentication failed: %s", e)
        print("\nERROR: You are not logged in to Azure. Please run 'az login' and try again.\n")
        sys.exit(1)
    return {"Authorization": f"Bearer {token.token}"}


def _try_get_access_token() -> Optional[str]:
    """Best-effort token acquisition; returns None if not logged in."""
    from src._credential import get_credential

    credential = get_credential()
    settings = SettingsManager()
    app_id = settings.get("APP_ID")
    scope = f"api://{app_id}/.default"
    try:
        token = credential.get_token(scope)
    except ClientAuthenticationError:
        return None
    return token.token


def _get_unverified_token_claims(token: str) -> dict:
    """Decode JWT claims without verifying signature (used only for role selection UX)."""
    if not token:
        return {}
    import jwt

    try:
        return jwt.decode(
            token,
            options={"verify_signature": False, "verify_aud": False, "verify_iss": False, "verify_exp": False},
        )
    except Exception:
        return {}


def _claims_is_writer(claims: dict) -> bool:
    roles = claims.get("roles", [])
    if isinstance(roles, str):
        roles = roles.split()
    token_roles = set(roles)
    return ("Write" in token_roles) or ("App.Write" in token_roles)


def get_feedback(
    start_date: str,
    end_date: str,
    language: Optional[str] = None,
    exclude: Optional[list[str]] = None,
    environment: str = "production",
    output_format: str = "json",
):
    """
    Retrieve AI comment feedback from APIView between start_date and end_date.
    If --language is omitted, returns feedback for all languages.
    """
    results = _get_ai_comment_feedback(
        language=language,
        start_date=start_date,
        end_date=end_date,
        exclude=exclude,
        environment=environment,
    )
    if output_format == "yaml":
        print(yaml.dump(results, default_flow_style=False, allow_unicode=True, sort_keys=False))
    else:
        print(json.dumps(results, indent=2))


def get_memories(
    start_date: str,
    end_date: str,
    language: Optional[str] = None,
    environment: str = "production",
    output_format: str = "json",
):
    """
    Retrieve memories created between start_date and end_date.
    If --language is omitted, returns memories for all languages.
    Uses the Cosmos DB _ts (Unix epoch) field for date filtering.
    """
    from datetime import datetime, timezone

    start_ts = int(datetime.strptime(start_date, "%Y-%m-%d").replace(tzinfo=timezone.utc).timestamp())
    end_ts = int(
        datetime.strptime(end_date, "%Y-%m-%d").replace(hour=23, minute=59, second=59, tzinfo=timezone.utc).timestamp()
    )

    db_manager = DatabaseManager.get_instance(environment=environment)
    query = "SELECT * FROM c WHERE c._ts >= @start_ts AND c._ts <= @end_ts AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)"
    parameters = [
        {"name": "@start_ts", "value": start_ts},
        {"name": "@end_ts", "value": end_ts},
    ]

    if language:
        # Build a set of known aliases so we match regardless of how the language
        # was stored (e.g. "csharp", "dotnet", "C#", "c#").
        aliases = {language.lower()}
        try:
            canonical, pretty = resolve_language(language)
            aliases.add(canonical.lower())
            aliases.add(pretty.lower())
        except ValueError:
            pass
        # Also cover common Cosmos-stored forms via the alias table
        _build_language_alias_table()
        for alias_key, (canon, _pretty) in _LANGUAGE_ALIAS_TABLE.items():
            if canon.lower() in aliases or alias_key in aliases:
                aliases.add(alias_key)
                aliases.add(canon.lower())
                aliases.add(_pretty.lower())
        alias_clauses = []
        for i, alias in enumerate(sorted(aliases)):
            param_name = f"@lang_{i}"
            alias_clauses.append(param_name)
            parameters.append({"name": param_name, "value": alias})
        query += f" AND LOWER(c.language) IN ({', '.join(alias_clauses)})"

    results = list(
        db_manager.memories.client.query_items(
            query=query,
            parameters=parameters,
            enable_cross_partition_query=True,
        )
    )

    # Strip Cosmos DB internal fields for cleaner output
    for item in results:
        for key in ["_rid", "_self", "_etag", "_attachments"]:
            item.pop(key, None)
        # Convert _ts to human-readable date
        if "_ts" in item:
            item["created_at"] = datetime.fromtimestamp(item["_ts"], tz=timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")

    if output_format == "yaml":
        print(yaml.dump(results, default_flow_style=False, allow_unicode=True, sort_keys=False))
    else:
        print(json.dumps(results, indent=2))


class CliCommandsLoader(CLICommandsLoader):
    """Loader for CLI commands related to APIView and review management."""

    # COMMAND REGISTRATION

    def load_command_table(self, args):
        with CommandGroup(self, "apiview", "__main__#{}") as g:
            g.command("get-comments", "get_apiview_comments")
            g.command("resolve-package", "resolve_package_info")
            g.command("list-created-revisions", "list_created_revisions")
            g.command("list-opened-revisions", "list_opened_revisions")
        with CommandGroup(self, "review", "__main__#{}") as g:
            g.command("generate", "generate_review")
            g.command("start-job", "review_job_start")
            g.command("get-job", "review_job_get")
            g.command("summarize", "review_summarize")
            g.command("group-comments", "group_apiview_comments")
        with CommandGroup(self, "agent", "__main__#{}") as g:
            g.command("mention", "handle_agent_mention")
            g.command("chat", "handle_agent_chat")
            g.command("resolve-thread", "handle_agent_thread_resolution")
        with CommandGroup(self, "test", "__main__#{}") as g:
            g.command("eval", "run_evals")
            g.command("extract-section", "extract_document_section")
            g.command("prompt", "prompt_test")
            g.command("pytest", "run_pytest")
        with CommandGroup(self, "ops", "__main__#{}") as g:
            g.command("deploy", "deploy_flask_app")
            g.command("check", "check_health")
            g.command("grant", "grant_permissions")
            g.command("revoke", "revoke_permissions")
        with CommandGroup(self, "kb", "__main__#{}") as g:
            g.command("search", "search_knowledge_base")
            g.command("reindex", "reindex_search")
            g.command("all-guidelines", "get_all_guidelines")
            g.command("check-links", "check_links_kb")
            g.command("consolidate-memories", "consolidate_memories_kb")
        with CommandGroup(self, "db", "__main__#{}") as g:
            g.command("get", "db_get")
            g.command("delete", "db_delete")
            g.command("purge", "db_purge")
            g.command("link", "db_link")
            g.command("unlink", "db_unlink")
        with CommandGroup(self, "report", "__main__#{}") as g:
            g.command("metrics", "report_metrics")
            g.command("quality-trends", "report_comment_bucket_trends")
            g.command("active-reviews", "get_active_reviews")
            g.command("feedback", "get_feedback")
            g.command("memory", "get_memories")
            g.command("architect-comments", "get_architect_comments")
            g.command("apiview-metrics", "report_apiview_metrics")
        return OrderedDict(self.command_table)

    # ARGUMENT REGISTRATION

    def load_arguments(self, command):
        with ArgumentsContext(self, "") as ac:
            ac.argument(
                "language",
                type=resolve_language_to_canonical,
                help="The language (e.g., python, Go, C#, dotnet, typescript).",
                options_list=("--language", "-l"),
            )
            ac.argument(
                "remote",
                action="store_true",
                help="Use the remote API review service instead of local processing.",
            )
            ac.argument(
                "start_date",
                type=str,
                help="The start date (YYYY-MM-DD).",
                options_list=["--start-date", "-s"],
            )
            ac.argument(
                "end_date",
                type=str,
                help="The end date (YYYY-MM-DD).",
                options_list=["--end-date", "-e"],
            )
            ac.argument(
                "environment",
                type=str,
                help="The APIView environment. Defaults to 'production'.",
                options_list=["--environment"],
                default="production",
                choices=["production", "staging"],
            )
            ac.argument(
                "comments_path",
                type=str,
                help="Path to a JSON file containing comments.",
                options_list=["--comments-path", "-c"],
                default=None,
            )

        with ArgumentsContext(self, "review") as ac:
            ac.argument(
                "target",
                type=str,
                help="The path to the APIView file to review.",
                options_list=("--target", "-t"),
            )
            ac.argument(
                "base",
                type=str,
                # pylint: disable=line-too-long
                help="The path to the base APIView file to compare against. If omitted, copilot will review the entire target APIView.",
                options_list=("--base", "-b"),
            )
            ac.argument(
                "outline",
                type=str,
                help="Path to a plain text file containing the outline text.",
                options_list=["--outline"],
                default=None,
            )
            ac.argument(
                "existing_comments",
                type=str,
                help="Path to a JSON file containing existing comments.",
                default=None,
            )
        with ArgumentsContext(self, "review generate") as ac:
            ac.argument(
                "debug_log",
                options_list=["--debug-log"],
                action="store_true",
                help="Enable debug logging for the review process. Outputs to `scratch/logs/<LANG>` directory.",
            )
        with ArgumentsContext(self, "test extract-section") as ac:
            ac.argument("size", type=int, help="The size of the section to extract.")
            ac.argument(
                "index",
                type=int,
                help="The index of the section to extract (default is 1).",
                default=1,
                options_list=["--index", "-i"],
            )
        with ArgumentsContext(self, "test eval") as ac:
            ac.argument(
                "num_runs", type=int, options_list=["--num-runs", "-n"], help="Number of times to run the test case."
            )
            ac.argument(
                "test_paths",
                type=str,
                nargs="*",
                options_list=["--test-paths", "-p"],
                default=[],
                help="The full paths to the folder(s) containing the test files. Must have a `test-config.yaml` file. If omitted, runs all workflows.",
            )
            ac.argument(
                "use_recording",
                options_list=["--use-recording"],
                action="store_true",
                help="Use recordings instead of executing LLM calls to speed up runs. If recordings are not available, LLM calls will be made and saved as recordings.",
            )
            ac.argument(
                "style",
                options_list=["--style"],
                type=str,
                choices=["compact", "verbose"],
                default="compact",
                help="Choose whether to show only failing and partial test cases (compact) or to also show passing ones (verbose)",
            )
            ac.argument(
                "save",
                action="store_true",
                help="Save the results to CosmosDB metrics.",
            )

        with ArgumentsContext(self, "kb") as ac:
            ac.argument(
                "path",
                type=str,
                help="The path to the file containing query text or code.",
                options_list=["--path"],
            )
            ac.argument(
                "text",
                type=str,
                help="The text query to search.",
            )
            ac.argument(
                "markdown",
                help="Render output as markdown instead of JSON.",
            )
            ac.argument(
                "ids",
                type=str,
                nargs="+",
                help="The IDs to retrieve.",
                options_list=["--ids"],
            )
        with ArgumentsContext(self, "kb reindex") as ac:
            ac.argument(
                "containers",
                type=str,
                nargs="*",
                help="The names of the containers to reindex. If not provided, all containers will be reindexed.",
                options_list=["--containers", "-c"],
                choices=ContainerNames.data_containers(),
            )
        with ArgumentsContext(self, "kb check-links") as ac:
            ac.argument(
                "fix",
                type=str,
                choices=["all", "broken", "oneway"],
                default=None,
                help="Repair issues: 'all' fixes everything, 'broken' removes dangling refs, 'oneway' adds missing back-refs.",
                options_list=["--fix"],
            )
        with ArgumentsContext(self, "kb consolidate-memories") as ac:
            ac.argument(
                "kind",
                type=str,
                choices=["guideline", "example", "memory"],
                help="The type of item whose related memories should be consolidated.",
                options_list=["--kind", "-k"],
            )
            ac.argument(
                "ids",
                type=str,
                nargs="+",
                help="One or more IDs of the specified kind to consolidate.",
                options_list=["--ids"],
            )
            ac.argument(
                "apply",
                action="store_true",
                help="Apply the consolidation. Without this flag, runs in dry-run mode and only reports what would be merged.",
            )
        with ArgumentsContext(self, "review get-job") as ac:
            ac.argument(
                "job_id",
                type=str,
                help="The job ID to poll.",
                options_list=["--job-id"],
            )
            ac.argument(
                "remote",
                action="store_true",
                help="Query the remote API service instead of the local database.",
            )
        with ArgumentsContext(self, "review summarize") as ac:
            ac.argument(
                "target", type=str, help="The path to the APIView file to summarize.", options_list=("--target", "-t")
            )
            ac.argument(
                "base",
                type=str,
                help="The path to the base APIView file for diff summarization.",
                options_list=["--base", "-b"],
            )
        with ArgumentsContext(self, "agent chat") as ac:
            ac.argument(
                "thread_id",
                type=str,
                help="The thread ID to continue the discussion. If not provided, a new thread will be created.",
                options_list=["--thread-id", "-t"],
            )
            ac.argument(
                "quiet",
                action="store_true",
                help="Suppress error messages during tool execution. The agent will retry automatically.",
                options_list=["--quiet", "-q"],
            )
            ac.argument(
                "readonly",
                action="store_true",
                help="Force readonly mode, even if you have write permissions.",
                options_list=["--readonly", "-r"],
            )
        with ArgumentsContext(self, "agent mention") as ac:
            ac.argument(
                "fetch_comment_id",
                type=str,
                help="Fetch a comment from the APIView database to process feedback. Also used as --source-comment-id for audit traceability.",
                options_list=["--fetch-comment-id"],
                default=None,
            )
            ac.argument(
                "dry_run",
                help="Print the payload that would be sent to the mention agent without executing it.",
                options_list=["--dry-run"],
                action="store_true",
            )
            ac.argument(
                "source_comment_id",
                type=str,
                help="The APIView comment ID that triggered this request, recorded on any memories created for audit traceability. Automatically set when --fetch-comment-id is used.",
                options_list=["--source-comment-id"],
                default=None,
            )
        with ArgumentsContext(self, "db") as ac:
            ac.argument(
                "container_name",
                type=str.lower,
                help="The name of the Cosmos DB container",
                choices=ContainerNames.values(),
                options_list=["--container-name", "-c"],
            )
            ac.argument(
                "id",
                type=str,
                help="The id of the item.",
                options_list=["--id", "-i"],
            )
        with ArgumentsContext(self, "db link") as ac:
            ac.argument(
                "guideline",
                type=str,
                help="The ID of the guideline to link.",
                options_list=["--guideline", "-g"],
                default=None,
            )
            ac.argument(
                "memory",
                type=str,
                help="The ID of the memory to link.",
                options_list=["--memory", "-m"],
                default=None,
            )
            ac.argument(
                "example",
                type=str,
                help="The ID of the example to link.",
                options_list=["--example", "-e"],
                default=None,
            )
            ac.argument(
                "reindex",
                action="store_true",
                help="Reindex the search indexes after linking.",
                options_list=["--reindex"],
            )
        with ArgumentsContext(self, "db unlink") as ac:
            ac.argument(
                "guideline",
                type=str,
                help="The ID of the guideline to unlink.",
                options_list=["--guideline", "-g"],
                default=None,
            )
            ac.argument(
                "memory",
                type=str,
                help="The ID of the memory to unlink.",
                options_list=["--memory", "-m"],
                default=None,
            )
            ac.argument(
                "example",
                type=str,
                help="The ID of the example to unlink.",
                options_list=["--example", "-e"],
                default=None,
            )
            ac.argument(
                "reindex",
                action="store_true",
                help="Reindex the search indexes after unlinking.",
                options_list=["--reindex"],
            )
        with ArgumentsContext(self, "db purge") as ac:
            ac.argument(
                "containers",
                type=str,
                nargs="*",
                help="The names of the containers to purge. If not provided, all containers will be purged.",
                options_list=["--containers", "-c"],
                choices=ContainerNames.data_containers(),
            )
            ac.argument(
                "run_indexer",
                help="Whether to run the search indexer before purging.",
                options_list=["--run-indexer"],
                action="store_true",
            )
        with ArgumentsContext(self, "apiview") as ac:
            ac.argument(
                "revision_id",
                type=str,
                help="The revision ID of the APIView to retrieve comments for.",
                options_list=["--revision-id", "-r"],
            )
            ac.argument(
                "language",
                type=resolve_language_to_canonical,
                help="Language to filter comments (case-insensitive, e.g., python, Go, C#).",
                options_list=("--language", "-l"),
            )
        with ArgumentsContext(self, "report active-reviews") as ac:
            ac.argument(
                "summary",
                action="store_true",
                help="Output summary format (package-name package-version APPROVED/UNAPPROVED) instead of detailed JSON.",
            )
            ac.argument(
                "approved_only",
                action="store_true",
                help="Show only approved revisions (matching the metrics chart definition).",
            )
        with ArgumentsContext(self, "apiview resolve-package") as ac:
            ac.argument(
                "package_query",
                type=str,
                help="The package name or description to search for.",
                options_list=["--package", "-p"],
            )
            ac.argument(
                "language",
                type=resolve_language_to_canonical,
                help="The language of the package (e.g., python, Go, C#).",
                options_list=["--language", "-l"],
            )
            ac.argument(
                "version",
                type=str,
                help="Optional version to filter by. If not provided, gets the latest revision.",
                options_list=["--version", "-v"],
                default=None,
            )
        with ArgumentsContext(self, "apiview list-created-revisions") as ac:
            ac.argument(
                "exclude",
                type=str,
                nargs="*",
                help="Languages to exclude (e.g., --exclude Java Go).",
                options_list=["--exclude"],
                default=None,
            )
        with ArgumentsContext(self, "apiview list-opened-revisions") as ac:
            ac.argument(
                "exclude",
                type=str,
                nargs="*",
                help="Languages to exclude (e.g., --exclude Java Go).",
                options_list=["--exclude"],
                default=None,
            )
            ac.argument(
                "created_in_window",
                action="store_true",
                help="Only count revisions created within the date window (default: count all revisions for viewed reviews).",
                options_list=["--created-in-window"],
                default=False,
            )
        with ArgumentsContext(self, "test prompt") as ac:
            ac.argument(
                "path",
                type=str,
                options_list=["--path", "-p"],
                help="Path to a .prompty file to test. If omitted, smoke-tests all prompts.",
            )
            ac.argument(
                "workers",
                type=int,
                options_list=["--workers", "-w"],
                default=4,
                help="Number of parallel workers when testing all prompts (default: 4).",
            )
        with ArgumentsContext(self, "test pytest") as ac:
            ac.argument(
                "args",
                type=str,
                options_list=["--args", "-a"],
                help="Additional arguments to pass to pytest (e.g. '-k test_name -v').",
            )
        with ArgumentsContext(self, "report metrics") as ac:
            ac.argument(
                "environment",
                type=str,
                help="The APIView environment from which to calculate the metrics report. Defaults to 'production'.",
                options_list=["--environment"],
                default="production",
                choices=["production", "staging"],
            )
            ac.argument(
                "charts",
                action="store_true",
                help="Generate PNG charts from the metrics and save to output/charts/.",
            )
            ac.argument(
                "exclude",
                type=str,
                nargs="*",
                help="Languages to exclude from the report (e.g., --exclude Java Go).",
                options_list=["--exclude"],
                default=None,
            )
            ac.argument(
                "save",
                action="store_true",
                help="Save the metrics report to CosmosDB.",
            )
        with ArgumentsContext(self, "report quality-trends") as ac:
            ac.argument(
                "months",
                type=int,
                options_list=["--months"],
                default=6,
                help="Number of calendar months to look back from the end date. Defaults to 6.",
            )
            ac.argument(
                "end_date",
                type=str,
                options_list=["--end-date", "-e"],
                default=None,
                help="Inclusive query end date in YYYY-MM-DD format. Defaults to today.",
            )
            ac.argument(
                "languages",
                type=str,
                nargs="+",
                options_list=["--languages"],
                default=None,
                help="Languages to include. Defaults to Python, C#, Java, and JavaScript.",
            )
            ac.argument(
                "exclude_human",
                action="store_true",
                options_list=["--exclude-human"],
                help="Exclude human comments from the chart.",
            )
            ac.argument(
                "neutral",
                action="store_true",
                options_list=["--neutral"],
                help="Include neutral AI comments as a separate bucket.",
            )
        with ArgumentsContext(self, "report apiview-metrics") as ac:
            ac.argument(
                "months",
                type=int,
                options_list=["--months"],
                default=6,
                help="Number of calendar months to look back from the end date. Defaults to 6.",
            )
            ac.argument(
                "end_date",
                type=str,
                options_list=["--end-date", "-e"],
                default=None,
                help="Inclusive query end date in YYYY-MM-DD format. Defaults to today.",
            )
            ac.argument(
                "languages",
                type=str,
                nargs="+",
                options_list=["--languages"],
                default=None,
                help="Languages to include. Defaults to Python, C#, Java, JavaScript, and Go.",
            )
            ac.argument(
                "chart",
                action="store_true",
                options_list=["--chart"],
                help="Generate a PNG trend chart and save to output/charts/.",
            )
            ac.argument(
                "summary",
                action="store_true",
                options_list=["--summary"],
                help="Print human-readable summary tables to stderr after the JSON output.",
            )
        with ArgumentsContext(self, "ops check") as ac:
            ac.argument(
                "include_auth",
                action="store_true",
                help="Include authentication in the health check.",
            )
        with ArgumentsContext(self, "ops") as ac:
            ac.argument(
                "assignee_id",
                type=str,
                help="The user ID of the assignee. If not provided, defaults to the current user.",
                options_list=["--assignee-id", "-a"],
                default=None,
            )
        with ArgumentsContext(self, "report") as ac:
            ac.argument(
                "language",
                type=resolve_language_to_canonical,
                help="Filter by language (e.g., python, Go, C#). If omitted, returns results for all languages.",
                options_list=("--language", "-l"),
                default=None,
            )
        with ArgumentsContext(self, "report feedback") as ac:
            ac.argument(
                "exclude",
                type=str,
                nargs="*",
                help="Feedback types to exclude. Can be 'good', 'bad', or 'delete'.",
                options_list=["--exclude"],
                choices=["good", "bad", "delete"],
            )
            ac.argument(
                "output_format",
                type=str,
                help="Output format. Defaults to 'json'.",
                options_list=["--format", "-f"],
                default="json",
                choices=["json", "yaml"],
            )
        with ArgumentsContext(self, "report memory") as ac:
            ac.argument(
                "language",
                type=str,
                help="The language to filter by (e.g., python, csharp, C#, typescript).",
                options_list=["--language", "-l"],
            )
            ac.argument(
                "output_format",
                type=str,
                help="Output format. Defaults to 'json'.",
                options_list=["--format", "-f"],
                default="json",
                choices=["json", "yaml"],
            )
        with ArgumentsContext(self, "report architect-comments") as ac:
            ac.argument(
                "output_format",
                type=str,
                help="Output format. Defaults to 'json'.",
                options_list=["--format", "-f"],
                default="json",
                choices=["json", "yaml"],
            )
            ac.argument(
                "all_commenters",
                action="store_true",
                help="Include comments from all users, not just language board approvers.",
                options_list=["--all-commenters"],
                default=False,
            )
            ac.argument(
                "include_replies",
                action="store_true",
                help="Include reply comments. By default, only the first comment in each thread is returned.",
                options_list=["--include-replies"],
                default=False,
            )
        super(CliCommandsLoader, self).load_arguments(command)


def run_cli():
    """Run the CLI application."""
    # Pre-set ENVIRONMENT_NAME from --environment so that all downstream code
    # (SettingsManager, DatabaseManager, SearchManager, etc.) can resolve the
    # environment without every function explicitly threading the parameter.
    # The --environment flag is registered globally with a default of "production".
    # If --environment is supplied, it always wins over the env var.
    env = None
    args = sys.argv[1:]
    for idx, arg in enumerate(args):
        if arg == "--environment":
            if idx + 1 < len(args):
                env = args[idx + 1]
            break
        if arg.startswith("--environment="):
            env = arg.split("=", 1)[1]
            break
    if env:
        os.environ["ENVIRONMENT_NAME"] = env
    elif not os.environ.get("ENVIRONMENT_NAME"):
        os.environ["ENVIRONMENT_NAME"] = "production"
    cli = CLI(cli_name="avc", commands_loader_cls=CliCommandsLoader)
    exit_code = cli.invoke(sys.argv[1:])
    sys.exit(exit_code)


class CustomJSONEncoder(json.JSONEncoder):
    """Custom JSON encoder to handle objects with `to_dict` or `__dict__` methods."""

    def default(self, o):
        # If the object has a `to_dict` method, use it
        if hasattr(o, "to_dict"):
            return o.to_dict()
        # If the object has a `__dict__` attribute, use it
        elif hasattr(o, "__dict__"):
            return o.__dict__
        # Otherwise, use the default serialization
        return super().default(o)


if __name__ == "__main__":
    run_cli()
