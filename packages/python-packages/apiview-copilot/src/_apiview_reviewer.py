# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for the APIView Copilot API review functionality.
"""

import concurrent.futures
import datetime
import json
import logging
import os
import signal
import sys
import threading
import uuid
from time import time
from typing import List, Optional

import prompty
import prompty.azure_beta
import yaml

from ._credential import get_credential, in_ci
from ._diff import create_diff_with_line_numbers
from ._models import Comment, ExistingComment, ReviewResult
from ._retry import retry_with_backoff
from ._search_manager import SearchManager
from ._sectioned_document import SectionedDocument
from ._settings import SettingsManager
from ._utils import get_language_pretty_name, get_prompt_path

# Set up package root for log and metadata paths
_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

# Configure logger to write to project root error.log and info.log
error_log_file = os.path.join(_PACKAGE_ROOT, "error.log")
info_log_file = os.path.join(_PACKAGE_ROOT, "info.log")

# Create handlers for error.log and info.log
error_handler = logging.FileHandler(error_log_file, mode="w")  # Overwrite on each run
error_handler.setLevel(logging.ERROR)  # Log only ERROR and higher levels
error_handler.setFormatter(logging.Formatter("%(asctime)s - %(levelname)s - %(message)s"))

info_handler = logging.FileHandler(info_log_file, mode="w")  # Overwrite on each run
info_handler.setLevel(logging.INFO)  # Log INFO and higher levels
info_handler.setFormatter(logging.Formatter("%(asctime)s - %(levelname)s - %(message)s"))

# Create a console handler for terminal output
console_handler = logging.StreamHandler()
console_handler.setLevel(logging.ERROR)  # Only log ERROR and higher to the terminal
console_handler.setFormatter(logging.Formatter("%(asctime)s - %(levelname)s - %(message)s"))

# Add handlers to the root logger
root_logger = logging.getLogger()
root_logger.setLevel(logging.DEBUG)  # Set the base level to DEBUG to capture all logs
root_logger.addHandler(error_handler)
root_logger.addHandler(info_handler)
root_logger.addHandler(console_handler)

# Create module-level logger
logger = logging.getLogger(__name__)


CREDENTIAL = get_credential()

SUPPORTED_LANGUAGES = [
    "android",
    "clang",
    "cpp",
    "dotnet",
    "golang",
    "ios",
    "java",
    "python",
    "rust",
    "typescript",
]


class ApiViewReviewMode:
    """Enumeration for APIView review modes."""

    FULL = "full"
    DIFF = "diff"


# pylint: disable=too-many-instance-attributes
class ApiViewReview:
    """Class representing an APIView review."""

    def __init__(
        self,
        target: str,
        base: Optional[str],
        *,
        language: str,
        outline: Optional[str] = None,
        comments: Optional[list] = None,
        include_general_guidelines: bool = False,
        debug_log: bool = False,
    ):
        self.job_id = str(uuid.uuid4())
        self.target = self._unescape(target)
        self.base = self._unescape(base) if base else None
        if self.base == "":
            self.base = None
        self.mode = ApiViewReviewMode.FULL if self.base is None else ApiViewReviewMode.DIFF
        self.language = language
        # lower threshold for Java because lines are unusually dense
        if self.language in ["java", "android"]:
            self.max_chunk_size = 450
        else:
            self.max_chunk_size = 500
        self.search = SearchManager(language=language)
        self.semantic_search_failed = False
        self.allowed_ids = [x.id for x in self.search.language_guidelines]
        self.results = ReviewResult()
        self.summary = None
        self.outline = outline
        self.existing_comments = (
            [ExistingComment(**self._normalize_comment_keys(data)) for data in comments] if comments else []
        )
        self.executor = concurrent.futures.ThreadPoolExecutor()
        self.filter_expression = f"language eq '{language}' and not (tags/any(t: t eq 'documentation' or t eq 'vague'))"
        if include_general_guidelines:
            self.filter_expression += " or language eq '' or language eq null"
        self.debug_log = debug_log
        self.settings = SettingsManager()
        self._isatty = sys.stdout.isatty()

        class JobLogger:
            """Logger wrapper to prepend job_id to all log messages."""

            def __init__(self, inner_logger, job_id):
                self._logger = inner_logger
                self._job_id = job_id

            def debug(self, msg, *args, **kwargs):
                """Log a debug message with job_id prefix."""
                self._logger.debug(f"[{self._job_id}] {msg}", *args, **kwargs)

            def info(self, msg, *args, **kwargs):
                """Log an info message with job_id prefix."""
                self._logger.info(f"[{self._job_id}] {msg}", *args, **kwargs)

            def warning(self, msg, *args, **kwargs):
                """Log a warning message with job_id prefix."""
                self._logger.warning(f"[{self._job_id}] {msg}", *args, **kwargs)

            def error(self, msg, *args, **kwargs):
                """Log an error message with job_id prefix."""
                self._logger.error(f"[{self._job_id}] {msg}", *args, **kwargs)

            def critical(self, msg, *args, **kwargs):
                """Log a critical message with job_id prefix."""
                self._logger.critical(f"[{self._job_id}] {msg}", *args, **kwargs)

            def exception(self, msg, *args, **kwargs):
                """Log an exception message with job_id prefix."""
                self._logger.exception(f"[{self._job_id}] {msg}", *args, **kwargs)

        self.logger = JobLogger(logger, self.job_id)

    def __del__(self):
        # Ensure the executor is properly shut down
        if hasattr(self, "executor"):
            self.executor.shutdown(wait=False)

    def _hash(self, obj) -> str:
        return str(hash(json.dumps(obj)))

    def _normalize_comment_keys(self, data):
        # Map alternative keys to the canonical ones
        if "author" in data:
            data["createdBy"] = data.pop("author")
        if "text" in data:
            data["commentText"] = data.pop("text")
        if "timestamp" in data:
            data["createdOn"] = data.pop("timestamp")
        return data

    def _print_message(self, msg: str = "", overwrite: bool = False):
        """
        Print messages, using carriage return for terminal, or newlines for non-terminal.
        Prepend job_id to non-tty output for cloud log traceability.
        """
        if self._isatty and overwrite:
            print(msg, end="\r", flush=True)
        elif msg:
            print(f"[{self.job_id}] {msg}", flush=True)
        else:
            print(f"[{self.job_id}]", flush=True)

    def _create_sectioned_document(self) -> SectionedDocument:
        """
        Creates a sectioned document from the target and base API views.
        """
        if self.mode == ApiViewReviewMode.FULL:
            # Create a sectioned document for the full API view
            numbered_lines = []
            for i, line in enumerate(self.target.splitlines()):
                numbered_lines.append(f"{i + 1}: {line}")
            return SectionedDocument(lines=numbered_lines, max_chunk_size=self.max_chunk_size)
        elif self.mode == ApiViewReviewMode.DIFF:
            # Create a sectioned document for the diff
            return SectionedDocument(
                lines=create_diff_with_line_numbers(old=self.base, new=self.target).splitlines(),
                max_chunk_size=self.max_chunk_size,
            )
        else:
            raise NotImplementedError(f"Review mode {self.mode} is not implemented.")

    def _execute_prompt_task(
        self, *, prompt_path: str, inputs: dict, task_name: str, status_idx: int, status_array: List[str]
    ) -> Optional[dict]:
        """Execute a single prompt task with percent progress tracking.

        Args:
            prompt_path (str): Path to the prompt file.
            inputs (dict): Dictionary of inputs for the prompt.
            task_name (str): Name of the task (e.g., "summary", "guideline").
            status_idx (int): Index in the status array to update.
            status_array (List[str]): Array tracking the status of all tasks.

        Returns:
            Optional[dict]: The result of the prompt execution, or None if an error occurred.
        """
        # Numeric percent progress update (status_array is just a placeholder for counting)
        total = len(status_array)
        completed = sum(1 for s in status_array if s)
        percent = int((completed / total) * 100) if total else 100
        self._print_message(f"Evaluating prompts... {percent}% complete", overwrite=True)

        try:
            # Run the prompt
            response = self._run_prompt(prompt_path, inputs)
            result = json.loads(response)

            # Mark this task as done (for numeric progress only)
            status_array[status_idx] = True
            completed = sum(1 for s in status_array if s)
            percent = int((completed / total) * 100) if total else 100
            self._print_message(f"Evaluating prompts... {percent}% complete", overwrite=True)
            return result

        except Exception as e:
            status_array[status_idx] = True  # Mark as done even on error
            completed = sum(1 for s in status_array if s)
            percent = int((completed / total) * 100) if total else 100
            self._print_message(f"Evaluating prompts... {percent}% complete", overwrite=True)
            self.logger.error(f"Error executing {task_name}: {str(e)}")
            return None

    def _generate_comments(self):
        """
        Generate comments for the API view by submitting jobs in parallel.
        """
        guideline_tag = "guideline"
        generic_tag = "generic"
        context_tag = "context"

        sectioned_doc = self._create_sectioned_document()

        sections_to_process = [(i, section) for i, section in enumerate(sectioned_doc)]

        # Select appropriate prompts based on mode
        if self.mode == ApiViewReviewMode.FULL:
            guideline_prompt_file = "guidelines_review.prompty"
            context_prompt_file = "context_review.prompty"
            generic_prompt_file = "generic_review.prompty"
        elif self.mode == ApiViewReviewMode.DIFF:
            guideline_prompt_file = "guidelines_diff_review.prompty"
            context_prompt_file = "context_diff_review.prompty"
            generic_prompt_file = "generic_diff_review.prompty"
        else:
            raise NotImplementedError(f"Review mode {self.mode} is not implemented.")

        # Set up progress tracking
        self._print_message("Processing sections: ", overwrite=True)

        total_prompts = len(sections_to_process) * 3  # 3 prompts per section
        prompt_status = [False] * total_prompts

        # Set up keyboard interrupt handler for more responsive cancellation (only in main thread)
        cancel_event = threading.Event()

        is_main_thread = threading.current_thread() == threading.main_thread()
        if is_main_thread:
            original_handler = signal.getsignal(signal.SIGINT)

            def keyboard_interrupt_handler():
                self._print_message("\n\nCancellation requested! Terminating process...")
                cancel_event.set()
                os._exit(1)

            signal.signal(signal.SIGINT, keyboard_interrupt_handler)

        # Retrieve guidelines as context for the guideline review phase
        guideline_context = self._retrieve_guidelines_as_context()
        guideline_context_string = guideline_context.to_markdown() if guideline_context else ""

        # Submit all jobs to the executor
        all_futures = {}

        # Guideline and generic tasks for each section
        for idx, (section_idx, section) in enumerate(sections_to_process):
            # First check if cancellation is requested
            if cancel_event.is_set():
                break

            # Guideline prompt
            guideline_key = f"{guideline_tag}_{section_idx}"
            all_futures[guideline_key] = self.executor.submit(
                self._execute_prompt_task,
                prompt_path=get_prompt_path(folder="api_review", filename=guideline_prompt_file),
                inputs={
                    "language": get_language_pretty_name(self.language),
                    "context": guideline_context_string,
                    "content": section.numbered(),
                },
                task_name=guideline_key,
                status_idx=idx * 3,
                status_array=prompt_status,
            )

            # Generic prompt
            generic_metadata = self._load_generic_metadata()
            generic_key = f"{generic_tag}_{section_idx}"
            all_futures[generic_key] = self.executor.submit(
                self._execute_prompt_task,
                prompt_path=get_prompt_path(folder="api_review", filename=generic_prompt_file),
                inputs={
                    "language": get_language_pretty_name(self.language),
                    "custom_rules": generic_metadata["custom_rules"],
                    "content": section.numbered(),
                },
                task_name=generic_key,
                status_idx=(idx * 3) + 1,
                status_array=prompt_status,
            )

            # Context prompt
            context_key = f"{context_tag}_{section_idx}"
            context = self._retrieve_context(str(section))
            context_string = context.to_markdown() if context else ""
            all_futures[context_key] = self.executor.submit(
                self._execute_prompt_task,
                prompt_path=get_prompt_path(folder="api_review", filename=context_prompt_file),
                inputs={
                    "language": get_language_pretty_name(self.language),
                    "context": context_string,
                    "content": section.numbered(),
                },
                task_name=context_key,
                status_idx=(idx * 3) + 2,
                status_array=prompt_status,
            )

        # Process results as they complete
        try:
            # Process each section's results
            section_results = {}

            for key, future in all_futures.items():
                try:
                    result = future.result()
                    if result:
                        section_type, section_idx = key.split("_")
                        section_idx = int(section_idx)

                        # Initialize section result if needed
                        if section_idx not in section_results:
                            section_results[section_idx] = {"comments": []}

                        # Add comments from this prompt
                        if "comments" in result:
                            # Tag comments with their source
                            for comment in result["comments"]:
                                comment["source"] = section_type
                                comment["is_generic"] = section_type == generic_tag
                            section_results[section_idx]["comments"].extend(result["comments"])
                except Exception as e:
                    self.logger.error(f"Error processing {key}: {str(e)}")

            self._print_message()  # Add newline after progress indicator

            # Merge results from all sections
            for section_idx, section_result in section_results.items():
                if section_result and section_result["comments"]:
                    comments = section_result["comments"]
                    section = sections_to_process[section_idx][1]
                    section_result = ReviewResult(comments=comments, allowed_ids=self.allowed_ids, section=section)
                    self.results.comments.extend(section_result.comments)
        except KeyboardInterrupt:
            self._print_message("\n\nCancellation requested! Terminating process...")
            cancel_event.set()
            os._exit(1)
        finally:
            # Restore original signal handler if it was set
            if is_main_thread:
                signal.signal(signal.SIGINT, original_handler)

    def _judge_generic_comments(self):
        """
        Judge generic comments by running the judge prompt on each comment, separating into keep
        and discard lists, reporting counts, and dumping to debug log if enabled.
        """
        judge_prompt_file = "filter_generic_comment.prompty"
        judge_prompt_path = get_prompt_path(folder="api_review", filename=judge_prompt_file)

        self._print_message("Reviewing generic comments...")

        # Submit each generic comment to the executor for parallel processing
        futures = {}
        for idx, comment in enumerate(self.results.comments):
            if comment.is_generic == True:
                continue
            search_result = self.search.search_all(query=comment.comment)
            context = self.search.build_context(search_result)
            context_text = context.to_markdown() if search_result else "EMPTY"
            futures[idx] = self.executor.submit(
                self._run_prompt,
                judge_prompt_path,
                inputs={
                    "content": comment.model_dump(),
                    "language": get_language_pretty_name(self.language),
                    "context": context_text,
                },
            )

        # Collect results as they complete, with % complete logging
        keep_results = []
        discard_results = []
        total = len(futures)
        for progress_idx, (idx, future) in enumerate(futures.items()):
            try:
                response = future.result()
                if not response or not response.strip():
                    self.logger.error(f"Error judging comment at index {idx}: Empty response from prompt.")
                    continue
                try:
                    response_json = json.loads(response)
                    response_json["idx"] = idx
                except Exception as je:
                    self.logger.error(
                        f"Error judging comment at index {idx}: Invalid JSON response: {repr(response)} | {str(je)}"
                    )
                    continue
                if response_json.get("action") == "DISCARD":
                    discard_results.append(response_json)
                elif response_json.get("action") == "KEEP":
                    keep_results.append(response_json)
                else:
                    # log an error but keep the comment to be safe
                    self.logger.error(
                        f"Error judging comment at index {idx}: Unknown action in response: {repr(response)}"
                    )
                    keep_results.append(response_json)
            except Exception as e:
                self.logger.error(f"Error judging comment at index {idx}: {str(e)}")
            percent = int(((progress_idx + 1) / total) * 100) if total else 100
            self._print_message(f"Judging comments... {percent}% complete", overwrite=True)
        self._print_message()  # Ensure the progress bar is visible before the summary

        # Report summary
        self._print_message(
            # pylint: disable=line-too-long
            f"Judging completed. Kept {len(keep_results)} generic comments. Discarded {len(discard_results)} generic comments."
        )

        # Debug log: dump keep_comments and discard_comments to files if enabled
        if self.debug_log:
            keep_comments = []
            discard_comments = []
            for result in keep_results + discard_results:
                # combine the comment and result info
                comment_dict = self.results.comments[result["idx"]].model_dump()
                comment = {**comment_dict, **result}
                if result.get("action") == "KEEP":
                    keep_comments.append(comment)
                elif result.get("action") == "DISCARD":
                    discard_comments.append(comment)
                else:
                    # should not happen, but log just in case
                    self.logger.error(f"Unexpected action in judged comment: {result}")
                    keep_comments.append(comment)
            ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
            debug_dir = os.path.join("scratch", "logs", self.language)
            os.makedirs(debug_dir, exist_ok=True)
            keep_path = os.path.join(debug_dir, f"debug_keep_generic_comments_{ts}.json")
            discard_path = os.path.join(debug_dir, f"debug_discard_generic_comments_{ts}.json")
            with open(keep_path, "w", encoding="utf-8") as f:
                json.dump(keep_comments, f, indent=2)
            with open(discard_path, "w", encoding="utf-8") as f:
                json.dump(discard_comments, f, indent=2)
            self.logger.debug(f"Kept generic comments written to {keep_path}")
            self.logger.debug(f"Discarded generic comments written to {discard_path}")

    def _deduplicate_comments(self):
        """
        Deduplicate comments based on line number and rule IDs.
        """
        comments = self.results.comments
        unique_comments = []
        batches = {}

        # First, collect all duplicate comments into batches to send to the LLM
        # and add any unique comments to the unique_comments list
        line_ids = set([x.line_no for x in comments])
        for line_id in line_ids:
            matches = [x for x in comments if x.line_no == line_id]
            if len(matches) == 1:
                unique_comments.append(matches[0])
                continue
            batches[line_id] = matches

        prompt_path = get_prompt_path(folder="api_review", filename="merge_comments.prompty")

        self._print_message("Deduplicating comments...")

        # Submit all batches to the executor for parallel processing
        futures = {}
        for line_no, batch in batches.items():
            # Collect all rule IDs for the batch
            all_guideline_ids = set()
            all_memory_ids = set()
            for comment in batch:
                all_guideline_ids.update(comment.guideline_ids)
                all_memory_ids.update(comment.memory_ids)

            # Prepare the context for the prompt
            search_results = self.search.search_all_by_id(list(all_guideline_ids.union(all_memory_ids)))
            context = self.search.build_context(search_results)

            # Submit the task to the executor
            futures[line_no] = self.executor.submit(
                self._run_prompt,
                prompt_path,
                {"comments": batch, "context": context},
            )

        # Process the results as they complete
        for line_no, future in futures.items():
            try:
                response = future.result()
                merge_results = json.loads(response)
                result_comments = merge_results.get("comments", [])
                if len(result_comments) != 1:
                    self.logger.error(f"Error merging comments for line {line_no}: {merge_results}")
                    continue
                merged_comment = result_comments[0]
                merged_comment["source"] = "merged"
                merged_comment_obj = Comment(**merged_comment)
                unique_comments.append(merged_comment_obj)
            except Exception as e:
                self.logger.error(f"Error processing deduplication for line {line_no}: {str(e)}")

        # Update the comments list with the unique comments
        self.results.comments = unique_comments

    def _filter_comments_with_metadata(self):
        """
        Run the filter prompt on the comments, processing each comment in parallel.
        """
        filter_prompt_file = "filter_comment_with_metadata.prompty"
        filter_prompt_path = get_prompt_path(folder="api_review", filename=filter_prompt_file)

        # Submit each comment to the executor for parallel processing
        futures = {}
        for idx, comment in enumerate(self.results.comments):
            futures[idx] = self.executor.submit(
                self._run_prompt,
                filter_prompt_path,
                inputs={
                    "content": comment.model_dump(),
                    "language": get_language_pretty_name(self.language),
                    "outline": self.outline,
                    "exceptions": self._load_filter_metadata().get("exceptions", "None"),
                },
            )

        # Collect results as they complete, with % complete logging
        keep_debug = []
        discard_debug = []
        comments_to_remove = []
        total = len(futures)
        for progress_idx, (idx, future) in enumerate(futures.items()):
            orig_comment = self.results.comments[idx]
            try:
                response = future.result()
                response_json = json.loads(response)
                action = response_json.get("action")
                if action == "KEEP":
                    keep_debug.append({**orig_comment.model_dump(), **response_json})
                elif action == "DISCARD":
                    discard_debug.append({**orig_comment.model_dump(), **response_json})
                    comments_to_remove.append(idx)
                else:
                    self.logger.warning(f"Unexpected action for line {orig_comment.line_no}: {repr(response)}")
                    keep_debug.append({**orig_comment.model_dump(), **response_json})
            except Exception as e:
                self.logger.error(f"Error filtering comment at index {idx}: {str(e)}")
            # Log % complete
            percent = int(((progress_idx + 1) / total) * 100) if total else 100
            self._print_message(f"Filtering comments... {percent}% complete", overwrite=True)
        self._print_message()  # Ensure the progress bar is visible before the summary

        # Summary message (final counts computed after removals)
        removed_count = len(set(comments_to_remove))
        initial_count = len(self.results.comments)
        final_count = initial_count - removed_count
        self._print_message(f"Filtering completed. Kept {final_count} comments. Discarded {removed_count} comments.")

        # Debug log: dump keep_comments and discard_comments to files if enabled
        if self.debug_log:
            ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
            debug_dir = os.path.join("scratch", "logs", self.language)
            os.makedirs(debug_dir, exist_ok=True)
            keep_path = os.path.join(debug_dir, f"debug_keep_comments_{ts}.json")
            discard_path = os.path.join(debug_dir, f"debug_discard_comments_{ts}.json")
            with open(keep_path, "w", encoding="utf-8") as f:
                json.dump(keep_debug, f, indent=2)
            with open(discard_path, "w", encoding="utf-8") as f:
                json.dump(discard_debug, f, indent=2)
            self.logger.debug(f"Kept comments written to {keep_path}")
            self.logger.debug(f"Discarded comments written to {discard_path}")

        # Remove comments that were marked for removal, preserving order of remaining comments
        if comments_to_remove:
            to_remove = set(comments_to_remove)
            self.results.comments = [comment for i, comment in enumerate(self.results.comments) if i not in to_remove]

    def _filter_preexisting_comments(self):
        """
        Check if there are any preexisting comments on the same line as new proposed comments. If so,
        resolve them with the LLM to either discard or update the proposed comment.
        """
        comments_to_remove = []
        # Prepare tasks for comments that have preexisting comments
        tasks = []
        indices = []
        for idx, comment in enumerate(self.results.comments):
            existing_comments = [e for e in self.existing_comments if e.line_no == comment.line_no]
            if not existing_comments:
                continue
            inputs = {
                "comment": comment.model_dump(),
                "existing": [e.model_dump() for e in existing_comments],
                "language": get_language_pretty_name(self.language),
            }
            prompt_path = get_prompt_path(folder="api_review", filename="filter_existing_comment.prompty")
            tasks.append((idx, comment, prompt_path, inputs))
            indices.append(idx)

        total = len(tasks)
        futures = {}
        for i, (idx, comment, prompt_path, inputs) in enumerate(tasks):
            futures[idx] = self.executor.submit(self._run_prompt, prompt_path, inputs)

        for i, (idx, comment, prompt_path, inputs) in enumerate(tasks):
            try:
                response = futures[idx].result()
                response_json = json.loads(response)
                action = response_json.get("action")
                comment = response_json.get("comment")
                if action == "KEEP":
                    comment.comment = comment
                elif action == "DISCARD":
                    comments_to_remove.append(idx)
                else:
                    self.logger.warning(f"Unexpected action for line {comment.line_no}: {repr(response)}")
                    comment.comment = comment
            except Exception as e:
                self.logger.error(f"Error filtering preexisting comments for line {comment.line_no}: {str(e)}")
                self.logger.warning(f"Keeping comment despite filtering error: {comment.comment}")
            percent = int(((i + 1) / total) * 100) if total else 100
            self._print_message(f"Filtering preexisting comments... {percent}% complete", overwrite=True)
        self._print_message()  # Ensure the progress bar is visible before the summary

        # remove comments that were marked for removal
        if not comments_to_remove:
            return
        initial_comment_count = len(self.results.comments)
        self.results.comments = [
            comment for idx, comment in enumerate(self.results.comments) if idx not in comments_to_remove
        ]
        final_comment_count = len(self.results.comments)
        self._print_message(
            # pylint: disable=line-too-long
            f"Filtered preexisting comments. KEEP: {final_comment_count}, DISCARD: {initial_comment_count - final_comment_count}."
        )

    def _run_prompt(self, prompt_path: str, inputs: dict, max_retries: int = 5) -> str:
        """
        Run a prompt with retry logic.

        Args:
            prompt_path: Path to the prompt file
            inputs: Dictionary of inputs for the prompt
            max_retries: Maximum number of retry attempts (default: 5)

        Returns:
            String result of the prompt execution

        Raises:
            Exception: If all retry attempts fail
        """

        def execute_prompt() -> str:
            if in_ci():
                configuration = {"api_key": self.settings.get("OPENAI_API_KEY")}
            else:
                configuration = {}

            return prompty.execute(prompt_path, inputs=inputs, configuration=configuration)

        def on_retry(exception, attempt, max_attempts):
            self.logger.warning(
                f"Error executing prompt {os.path.basename(prompt_path)}, "
                f"attempt {attempt+1}/{max_attempts}: {str(exception)}"
            )

        def on_failure(exception, attempt):
            self.logger.error(
                f"Failed to execute prompt {os.path.basename(prompt_path)} "
                f"after {attempt} attempts: {str(exception)}"
            )
            raise exception

        os.environ["OPENAI_ENDPOINT"] = self.settings.get("OPENAI_ENDPOINT")
        return retry_with_backoff(
            func=execute_prompt,
            max_retries=max_retries,
            retry_exceptions=(json.JSONDecodeError, Exception),
            on_retry=on_retry,
            on_failure=on_failure,
            logger=self.logger,
            description=f"prompt {os.path.basename(prompt_path)}",
        )

    # pylint: disable=too-many-locals
    def run(self) -> ReviewResult:
        """Execute the APIView review process."""
        try:
            self._print_message(f"Generating {get_language_pretty_name(self.language)} review {self.job_id}")
            self.logger.info(f"Generating review {self.job_id} for language={self.language}")
            overall_start_time = time()

            # Canary check: try authenticating against Search and CosmosDB before LLM calls
            canary_error = self._canary_check_search_and_cosmos()
            if canary_error:
                self._print_message(f"ERROR: {canary_error}")
                self.logger.error(f"Aborting review due to canary check failure: {canary_error}")
                raise RuntimeError(f"Aborting review: {canary_error}")

            # Track time for _generate_comments
            generate_start_time = time()
            self._generate_comments()
            generate_end_time = time()
            self._print_message(f"  Generated comments in {generate_end_time - generate_start_time:.2f} seconds.")

            # Run generic comments through a judge prompt
            judge_start_time = time()
            self._judge_generic_comments()
            judge_end_time = time()
            self._print_message(f"  Generic comments judged in {judge_end_time - judge_start_time:.2f} seconds.")

            # Track time for _deduplicate_comments
            deduplicate_start_time = time()
            initial_comment_count = len(self.results.comments)
            self._deduplicate_comments()
            merged_comment_count = len(self.results.comments)
            deduplicate_end_time = time()
            self._print_message(
                f"  Deduplication completed in {deduplicate_end_time - deduplicate_start_time:.2f} seconds."
            )
            self._print_message(f"  Initial comments: {initial_comment_count}, Merged comments: {merged_comment_count}")

            # Track time for _filter_comments
            filter_start_time = time()
            self._filter_comments_with_metadata()
            filter_end_time = time()
            self._print_message(f"  Filtering completed in {filter_end_time - filter_start_time:.2f} seconds.")

            # summary removed: no summary comment appended

            # Track time for _filter_preexisting_comments
            preexisting_start_time = time()
            self._filter_preexisting_comments()
            preexisting_end_time = time()
            self._print_message(
                f"  Preexisting comments filtered in {preexisting_end_time - preexisting_start_time:.2f} seconds."
            )

            results = self.results.sorted()

            overall_end_time = time()
            self._print_message(
                # pylint: disable=line-too-long
                f"Review {self.job_id} generated in {overall_end_time - overall_start_time:.2f} seconds. Found {len(results.comments)} comments"
            )

            if self.semantic_search_failed:
                self._print_message("WARN: Semantic search failed for some chunks (see error.log).")

            return results
        finally:
            # Don't close the executor here as it might be needed for future operations
            pass

    def _canary_check_search_and_cosmos(self) -> str | None:
        """
        Attempts a minimal search and CosmosDB access to verify authentication before LLM calls.
        Returns an error string if authentication fails, otherwise None.
        """
        try:
            try:
                # Use a real search result, even if empty
                _ = self.search.search_all(query="canary")
                _ = self.search.build_context([])
            except Exception as cosmos_exc:
                return f"CosmosDB authentication failed: {type(cosmos_exc).__name__}: {cosmos_exc}"
        except Exception as e:
            return f"Unexpected canary check error: {type(e).__name__}: {e}"
        return None

    def _retrieve_context(self, query: str) -> List[object] | None:
        """
        Given a code query, searches the unified index for relevant guidelines,
        memories and examples.
        """
        try:
            results = self.search.search_all(query=query)
            context = self.search.build_context(results.results)
            return context
        except Exception as e:
            logger.error("Error retrieving context: %s: %s", type(e).__name__, e, exc_info=True)
            return None

    def _retrieve_guidelines_as_context(self) -> List[object] | None:
        """
        Retrieves all guidelines for the current language as context.
        """
        try:
            guidelines = self.search.language_guidelines
            if not guidelines:
                return None
            context = self.search.build_context(self.search.language_guidelines.results)
            return context
        except Exception as e:
            logger.error("Error retrieving guidelines: %s: %s", type(e).__name__, e, exc_info=True)
            return None

    def _load_generic_metadata(self):
        """
        Load the generic metadata from the YAML file, or returns defaults if the file doesn't exist.
        """
        # Construct the path to the YAML file
        yaml_file = os.path.join(_PACKAGE_ROOT, "metadata", self.language, "guidance.yaml")

        # Return defaults if the file doesn't exist
        if not os.path.exists(yaml_file):
            return {"custom_rules": ""}

        # Load the YAML file
        with open(yaml_file, "r", encoding="utf-8") as f:
            yaml_data = yaml.safe_load(f)

        custom_rules_yaml = yaml_data.get("custom_rules", "")
        metadata = {
            "custom_rules": custom_rules_yaml,
        }
        return metadata

    def _load_filter_metadata(self):
        """
        Load the language-specific filter metadata from the YAML file, or returns
        defaults if the file doesn't exist.
        """
        # Construct the path to the YAML file
        yaml_file = os.path.join(_PACKAGE_ROOT, "metadata", self.language, "filter.yaml")

        # Return defaults if the file doesn't exist
        if not os.path.exists(yaml_file):
            return {"exceptions": "None"}

        # Load the YAML file
        with open(yaml_file, "r", encoding="utf-8") as f:
            yaml_data = yaml.safe_load(f)

        exceptions_yaml = yaml_data.get("exceptions", None)
        metadata = {
            "exceptions": exceptions_yaml or "None",
        }
        return metadata

    def _unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))

    def close(self):
        """Close resources used by this ApiViewReview instance."""
        if hasattr(self, "executor"):
            self.executor.shutdown(wait=True)
