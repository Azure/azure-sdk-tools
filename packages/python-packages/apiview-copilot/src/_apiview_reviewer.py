from azure.identity import DefaultAzureCredential
import concurrent.futures
import json
import logging
import os
import prompty
import pathlib
import prompty.azure_beta
import signal
import sys
import threading
from time import time
from typing import Optional, List
import yaml

from ._diff import create_diff_with_line_numbers
from ._models import ReviewResult, Comment
from ._search_manager import SearchManager
from ._sectioned_document import SectionedDocument
from ._retry import retry_with_backoff


# Set up paths
_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_PROMPTS_FOLDER = os.path.join(_PACKAGE_ROOT, "prompts")

# Configure logger to write to project root error.log
log_file = os.path.join(_PACKAGE_ROOT, "error.log")

logging.basicConfig(
    filename=log_file,
    filemode="w",  # overwrite on each run
    level=logging.ERROR,
    format="%(asctime)s - %(levelname)s - %(message)s",
    force=True,
)

for handler in logging.root.handlers:
    if isinstance(handler, logging.FileHandler):
        handler.setLevel(logging.ERROR)
        handler.formatter = logging.Formatter("%(asctime)s - %(levelname)s - %(message)s")

# Create module-level logger
logger = logging.getLogger(__name__)

if "APPSETTING_WEBSITE_SITE_NAME" not in os.environ:
    # running on dev machine, loadenv
    import dotenv

    dotenv.load_dotenv()

CREDENTIAL = DefaultAzureCredential()

DEFAULT_USE_RAG = False


# create enum for the ReviewMode
class ApiViewReviewMode:
    FULL = "full"
    DIFF = "diff"


class ApiViewReview:

    # Define status characters with colors
    PENDING = "░"
    PROCESSING = "▒"
    SUCCESS = "\033[32m█\033[0m"  # Green square
    FAILURE = "\033[31m█\033[0m"  # Red square
    RED_TEXT = "\033[31m"  # Red text
    RESET_COLOR = "\033[0m"  # Reset to default text color

    def __init__(
        self,
        target: str,
        base: Optional[str],
        *,
        language: str,
        use_rag: bool = DEFAULT_USE_RAG,
    ):
        self.target = self._unescape(target)
        self.base = self._unescape(base) if base else None
        if self.base == "":
            self.base = None
        self.mode = ApiViewReviewMode.FULL if self.base is None else ApiViewReviewMode.DIFF
        self.language = language
        self.use_rag = use_rag
        self.search = SearchManager(language=language)
        self.semantic_search_failed = False
        static_guideline_ids = [x["id"] for x in self.search.static_guidelines]
        self.results = ReviewResult(guideline_ids=static_guideline_ids, comments=[])
        self.summary = None
        self.outline = None
        self.executor = concurrent.futures.ThreadPoolExecutor()

    def __del__(self):
        # Ensure the executor is properly shut down
        if hasattr(self, "executor"):
            self.executor.shutdown(wait=False)

    def _hash(self, obj) -> str:
        return str(hash(json.dumps(obj)))

    def _ensure_env_vars(self, vars: List[str]):
        """
        Ensures that the given environment variables are set.
        """
        missing = []
        for var in vars:
            if os.getenv(var) is None:
                missing.append(var)
        if missing:
            raise ValueError(f"Environment variables not set: {', '.join(missing)}")

    def _create_sectioned_document(self) -> SectionedDocument:
        """
        Creates a sectioned document from the target and base API views.
        """
        if self.mode == ApiViewReviewMode.FULL:
            # Create a sectioned document for the full API view
            numbered_lines = []
            for i, line in enumerate(self.target.splitlines()):
                numbered_lines.append(f"{i + 1}: {line}")
            return SectionedDocument(lines=numbered_lines)
        elif self.mode == ApiViewReviewMode.DIFF:
            # Create a sectioned document for the diff
            return SectionedDocument(lines=create_diff_with_line_numbers(old=self.base, new=self.target).splitlines())
        else:
            raise NotImplementedError(f"Review mode {self.mode} is not implemented.")

    def _execute_prompt_task(
        self, *, prompt_path: str, inputs: dict, task_name: str, status_idx: int, status_array: List[str]
    ) -> Optional[dict]:
        """Execute a single prompt task with status tracking.

        Args:
            prompt_path (str): Path to the prompt file.
            inputs (dict): Dictionary of inputs for the prompt.
            task_name (str): Name of the task (e.g., "summary", "guideline").
            status_idx (int): Index in the status array to update.
            status_array (List[str]): Array tracking the status of all tasks.

        Returns:
            Optional[dict]: The result of the prompt execution, or None if an error occurred.
        """
        status_array[status_idx] = self.PROCESSING
        print("\r" + "Evaluating prompts: " + "".join(status_array), end="", flush=True)

        try:
            # Run the prompt
            response = self._run_prompt(prompt_path, inputs)

            # Process result based on task type
            if task_name == "summary" or task_name == "outline":
                result = response  # Just return the text
            else:
                # Parse JSON for guideline/generic tasks
                result = json.loads(response)

            # Update status and return result
            status_array[status_idx] = self.SUCCESS
            print("\r" + "Evaluating prompts: " + "".join(status_array), end="", flush=True)
            return result

        except Exception as e:
            status_array[status_idx] = self.FAILURE
            print("\r" + "Evaluating prompts: " + "".join(status_array), end="", flush=True)
            logger.error(f"Error executing {task_name}: {str(e)}")
            return None

    def _generate_comments(self):
        """
        Generate comments for the API view by submitting jobs in parallel.
        """
        summary_tag = "summary"
        guideline_tag = "guideline"
        generic_tag = "generic"
        outline_tag = "outline"

        sectioned_doc = self._create_sectioned_document()

        sections_to_process = [(i, section) for i, section in enumerate(sectioned_doc)]

        # Select appropriate prompts based on mode
        if self.mode == ApiViewReviewMode.FULL:
            guideline_prompt_file = "guidelines_review.prompty"
            generic_prompt_file = "generic_review.prompty"
            summary_prompt_file = "summarize_api.prompty"
            summary_content = self.target
        elif self.mode == ApiViewReviewMode.DIFF:
            guideline_prompt_file = "guidelines_diff_review.prompty"
            generic_prompt_file = "generic_diff_review.prompty"
            summary_prompt_file = "summarize_diff.prompty"
            summary_content = create_diff_with_line_numbers(old=self.base, new=self.target)
        else:
            raise NotImplementedError(f"Review mode {self.mode} is not implemented.")

        # Outline prompt is always based on self.target
        outline_prompt_file = "generate_outline.prompty"
        outline_content = self.target

        # Set up progress tracking
        print("Processing sections: ", end="", flush=True)
        total_prompts = 1 + (len(sections_to_process) * 2) + 1  # 1 for summary, 1 for outline, 2 for each section
        prompt_status = [self.PENDING] * total_prompts

        # Set up keyboard interrupt handler for more responsive cancellation
        cancel_event = threading.Event()
        original_handler = signal.getsignal(signal.SIGINT)

        def keyboard_interrupt_handler(signal, frame):
            print("\n\nCancellation requested! Terminating process...")
            cancel_event.set()
            os._exit(1)

        signal.signal(signal.SIGINT, keyboard_interrupt_handler)

        # Submit all jobs to the executor
        all_futures = {}

        # 1. Summary task
        all_futures[summary_tag] = self.executor.submit(
            self._execute_prompt_task,
            prompt_path=os.path.join(_PROMPTS_FOLDER, summary_prompt_file),
            inputs={
                "language": self._get_language_pretty_name(),
                "content": summary_content,
            },
            task_name=summary_tag,
            status_idx=0,
            status_array=prompt_status,
        )

        # 2. Outline task (always based on self.target)
        all_futures[outline_tag] = self.executor.submit(
            self._execute_prompt_task,
            prompt_path=os.path.join(_PROMPTS_FOLDER, outline_prompt_file),
            inputs={
                "content": outline_content,
            },
            task_name=outline_tag,
            status_idx=1,
            status_array=prompt_status,
        )

        # 3. Guideline and generic tasks for each section
        for idx, (section_idx, section) in enumerate(sections_to_process):
            # First check if cancellation is requested
            if cancel_event.is_set():
                break

            # Prepare context for guideline tasks
            if self.use_rag:
                context = self._retrieve_and_resolve_guidelines(str(section))
                if context:
                    context_string = context.to_markdown()
                else:
                    logger.warning(
                        f"Failed to retrieve guidelines for section {section_idx}, using static guidelines instead."
                    )
                    self.semantic_search_failed = True
                    context_string = json.dumps(self.search.static_guidelines)
            else:
                context_string = json.dumps(self.search.static_guidelines)

            # Guideline prompt
            guideline_key = f"{guideline_tag}_{section_idx}"
            all_futures[guideline_key] = self.executor.submit(
                self._execute_prompt_task,
                prompt_path=os.path.join(_PROMPTS_FOLDER, guideline_prompt_file),
                inputs={
                    "language": self._get_language_pretty_name(),
                    "context": context_string,
                    "content": section.numbered(),
                },
                task_name=guideline_key,
                status_idx=(idx * 2) + 2,
                status_array=prompt_status,
            )

            # Generic prompt
            generic_metadata = self._load_generic_metadata()
            generic_key = f"{generic_tag}_{section_idx}"
            all_futures[generic_key] = self.executor.submit(
                self._execute_prompt_task,
                prompt_path=os.path.join(_PROMPTS_FOLDER, generic_prompt_file),
                inputs={
                    "language": self._get_language_pretty_name(),
                    "custom_rules": generic_metadata["custom_rules"],
                    "content": section.numbered(),
                },
                task_name=generic_key,
                status_idx=(idx * 2) + 3,
                status_array=prompt_status,
            )

        # Process results as they complete
        try:
            # Process summary result
            summary_response = all_futures[summary_tag].result()
            if summary_response:
                self.summary = Comment(
                    rule_ids=[],
                    line_no=1,
                    bad_code="",
                    suggestion=None,
                    comment=summary_response,
                    source="summary",
                )

            # Process outline result
            outline_response = all_futures[outline_tag].result()
            if outline_response:
                self.outline = outline_response

            # Process each section's results
            section_results = {}

            for key, future in all_futures.items():
                if key in {summary_tag, outline_tag}:
                    continue  # Already processed
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
                            section_results[section_idx]["comments"].extend(result["comments"])
                except Exception as e:
                    logger.error(f"Error processing {key}: {str(e)}")

            print()  # Add newline after progress indicator

            # Merge results from all sections
            for section_idx, section_result in section_results.items():
                if section_result and section_result["comments"]:
                    section = sections_to_process[section_idx][1]
                    section_result = ReviewResult(**section_result)
                    self.results.merge(section_result, section=section)
        except KeyboardInterrupt:
            print("\n\nCancellation requested! Terminating process...")
            cancel_event.set()
            os._exit(1)
        finally:
            # Restore original signal handler
            signal.signal(signal.SIGINT, original_handler)

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

        prompt_path = os.path.join(_PROMPTS_FOLDER, "merge_comments.prompty")

        print(f"Deduplicating comments...")

        # Submit all batches to the executor for parallel processing
        futures = {}
        for line_no, batch in batches.items():
            # Collect all rule IDs for the batch
            all_rule_ids = set()
            for comment in batch:
                all_rule_ids.update(comment.rule_ids)

            # Prepare the context for the prompt
            context = self.search.guidelines_for_ids(all_rule_ids)

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
                    logger.error(f"Error merging comments for line {line_no}: {merge_results}")
                    continue
                merged_comment = result_comments[0]
                merged_comment["source"] = "merged"
                merged_comment_obj = Comment(**merged_comment)
                unique_comments.append(merged_comment_obj)
            except Exception as e:
                logger.error(f"Error processing deduplication for line {line_no}: {str(e)}")

        # Update the comments list with the unique comments
        self.results.comments = unique_comments

    def _filter_comments(self):
        """
        Run the filter prompt on the comments, processing each comment in parallel.
        """
        filter_prompt_file = "final_comment_filter_single.prompty"
        filter_prompt_path = os.path.join(_PROMPTS_FOLDER, filter_prompt_file)

        print(f"Filtering comments...")

        # Submit each comment to the executor for parallel processing
        futures = {}
        for idx, comment in enumerate(self.results.comments):
            futures[idx] = self.executor.submit(
                self._run_prompt,
                filter_prompt_path,
                inputs={
                    "content": comment.model_dump(),
                    "language": self._get_language_pretty_name(),
                    "outline": self.outline,
                },
            )

        # Collect results as they complete
        keep_comments = []
        discard_comments = []
        for idx, future in futures.items():
            try:
                response = future.result()
                response_json = json.loads(response)
                if response_json.get("status") == "KEEP":
                    keep_comments.append(response_json)
                else:
                    discard_comments.append(response_json)
            except Exception as e:
                logger.error(f"Error filtering comment at index {idx}: {str(e)}")

        # Update the results with the filtered comments
        print(f"Filtering completed. Kept {len(keep_comments)} comments. Discarded {len(discard_comments)} comments.")
        self.results.comments = [Comment(**comment) for comment in keep_comments]

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
            return prompty.execute(prompt_path, inputs=inputs)

        def on_retry(exception, attempt, max_attempts):
            logger.warning(
                f"Error executing prompt {os.path.basename(prompt_path)}, "
                f"attempt {attempt+1}/{max_attempts}: {str(exception)}"
            )

        def on_failure(exception, attempt):
            logger.error(
                f"Failed to execute prompt {os.path.basename(prompt_path)} "
                f"after {attempt} attempts: {str(exception)}"
            )
            raise exception

        return retry_with_backoff(
            func=execute_prompt,
            max_retries=max_retries,
            retry_exceptions=(json.JSONDecodeError, Exception),
            on_retry=on_retry,
            on_failure=on_failure,
            logger=logger,
            description=f"prompt {os.path.basename(prompt_path)}",
        )

    def run(self) -> ReviewResult:
        try:
            print(f"Generating {self._get_language_pretty_name()} review...")
            overall_start_time = time()

            # Track time for _generate_comments
            generate_start_time = time()
            self._generate_comments()
            generate_end_time = time()
            print(f"  Generated comments in {generate_end_time - generate_start_time:.2f} seconds.")

            # Track time for _deduplicate_comments
            deduplicate_start_time = time()
            self._deduplicate_comments()
            deduplicate_end_time = time()
            print(f"  Deduplication completed in {deduplicate_end_time - deduplicate_start_time:.2f} seconds.")

            # Track time for _filter_comments
            filter_start_time = time()
            self._filter_comments()
            filter_end_time = time()
            print(f"  Filtering completed in {filter_end_time - filter_start_time:.2f} seconds.")

            # Add the summary to the results
            if self.summary:
                self.results.comments.append(self.summary)
            results = self.results.sorted()

            overall_end_time = time()
            print(f"Review generated in {overall_end_time - overall_start_time:.2f} seconds.")

            if self.semantic_search_failed:
                print(f"{self.RED_TEXT}WARN: Semantic search failed for some chunks (see error.log).{self.RESET_COLOR}")

            return results
        finally:
            # Don't close the executor here as it might be needed for future operations
            pass

    def _get_language_pretty_name(self) -> str:
        """
        Returns a pretty name for the language.
        """
        language_pretty_names = {
            "android": "Android",
            "cpp": "C++",
            "dotnet": "C#",
            "golang": "Go",
            "ios": "Swift",
            "java": "Java",
            "python": "Python",
            "typescript": "TypeScript",
        }
        return language_pretty_names.get(self.language, self.language.capitalize())

    def _retrieve_and_resolve_guidelines(self, query: str) -> List[object] | None:
        try:
            """
            Given a code query, searches the examples index for relevant examples
            and the guidelines index for relevant guidelines based on a structural
            description of the code. Then, it resolves the two sets of results.
            """
            self._ensure_env_vars(["AZURE_SEARCH_NAME"])

            # search the examples index directly with the code snippet
            example_results = self.search.search_examples(query)

            # use a prompt to convert the code snippet to text
            # then do a hybrid search of the guidelines index against this description
            prompt = os.path.join(_PROMPTS_FOLDER, "code_to_text.prompty")
            response = self._run_prompt(prompt, inputs={"question": query})
            guideline_results = self.search.search_guidelines(response)

            context = self.search.build_context(guideline_results, example_results)
            return context
        except Exception as e:
            # Log search errors
            logger.error(f"Error retrieving guidelines: {str(e)}")
            # Return empty context as fallback
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
        with open(yaml_file, "r") as f:
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
            return {"exceptions": "None", "sample": ""}

        # Load the YAML file
        with open(yaml_file, "r") as f:
            yaml_data = yaml.safe_load(f)

        sample_yaml = yaml_data.get("sample", None)
        exceptions_yaml = yaml_data.get("exceptions", None)
        metadata = {
            "sample": "",
            "exceptions": exceptions_yaml or "None",
        }
        # format the sample string if there's a value
        if sample_yaml:
            metadata[
                "sample"
            ] = f"""
            sample:
              {sample_yaml}
            """
        return metadata

    def _unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))

    def close(self):
        """Close resources used by this ApiViewReview instance."""
        if hasattr(self, "executor"):
            self.executor.shutdown(wait=True)
