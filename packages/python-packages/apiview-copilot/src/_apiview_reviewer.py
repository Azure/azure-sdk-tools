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

model_map = {
    "gpt-4o-mini": "regular",
    "gpt-4.1-nano": "regular",
    "gpt-4o": "regular",
    "gpt-4.1": "regular",
    "o3": "reasoning",
    "o3-mini": "reasoning",
    "o4-mini": "reasoning",
}

supported_models = [x for x in model_map.keys()]

DEFAULT_MODEL = "o3-mini"
DEFAULT_USE_RAG = False


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
        *,
        language: str,
        model: str = DEFAULT_MODEL,
        use_rag: bool = DEFAULT_USE_RAG,
    ):
        if model not in supported_models:
            raise ValueError(f"Model {model} not supported. Supported models are: {', '.join(supported_models)}")
        self.language = language
        self.model = model
        self.use_rag = use_rag
        self.search = SearchManager(language=language)
        self.semantic_search_failed = False

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

    def get_response(self, *, target: str, base: Optional[str] = None, diff: Optional[str] = None) -> ReviewResult:
        print(f"Generating review...")

        logger.info(f"Starting review with model: {self.model}, language: {self.language}")

        start_time = time()
        target = self.unescape(target)
        static_guidelines = self.search.static_guidelines
        static_guideline_ids = [x["id"] for x in static_guidelines]

        # Prepare the document
        chunked_target = SectionedDocument(lines=target.splitlines())
        combined_results = ReviewResult(guideline_ids=static_guideline_ids, comments=[])

        # Skip header if multiple sections
        chunks_to_process = []
        for i, chunk in enumerate(chunked_target):
            chunks_to_process.append((i, chunk))

        # Print initial progress bar
        print("Processing chunks: ", end="", flush=True)
        chunk_status = [self.PENDING] * len(chunks_to_process)

        prompty_type = model_map[self.model]

        guideline_prompt_file = f"guidelines_review_{prompty_type}.prompty".replace("-", "_")
        generic_prompt_file = f"generic_review.prompty"

        # set the model name in the env var so we don't need a prompty file per model
        os.environ["PROMPTY_MODEL_DEPLOYMENT"] = self.model

        # Flag to indicate cancellation
        cancel_event = threading.Event()

        # Set up keyboard interrupt handler for more responsive cancellation
        def keyboard_interrupt_handler(signal, frame):
            print("\n\nCancellation requested! Terminating process...")
            cancel_event.set()
            # Exit immediately without further processing
            os._exit(1)  # Force immediate exit

        original_handler = signal.getsignal(signal.SIGINT)
        signal.signal(signal.SIGINT, keyboard_interrupt_handler)

        try:
            # Process chunks in parallel using ThreadPoolExecutor
            with concurrent.futures.ThreadPoolExecutor() as executor:
                # Submit all tasks
                future_to_chunk = {
                    executor.submit(
                        self._process_chunk_with_retry,
                        chunk_info,
                        static_guidelines,
                        cancel_event,
                        guideline_prompt_file,
                        generic_prompt_file,
                        chunk_status,
                    ): chunk_info
                    for chunk_info in chunks_to_process
                }

                # Process results as they complete - silently log errors without terminal output
                results = []
                try:
                    for future in concurrent.futures.as_completed(future_to_chunk):
                        chunk_info = future_to_chunk[future]
                        try:
                            result = future.result()
                            results.append(result)
                        except Exception as e:
                            i, chunk = chunk_info
                            chunk_idx = chunks_to_process.index((i, chunk))
                            chunk_status[chunk_idx] = self.FAILURE
                            print(
                                "\r" + "Processing chunks: " + "".join(chunk_status),
                                end="",
                                flush=True,
                            )
                            logger.error(f"Error processing chunk {i}: {str(e)}")
                            results.append((chunk, None))
                except KeyboardInterrupt:
                    # This should not be reached as our signal handler will catch it,
                    # but just in case the signal handler isn't active
                    print("\n\nCancellation requested! Terminating process...")
                    sys.exit(1)  # Force exit without further processing

                print()  # Add newline after progress bar is complete

        except KeyboardInterrupt:
            # This should not be reached as our signal handler will catch it,
            # but just in case the signal handler isn't active
            print("\n\nCancellation requested! Terminating process...")
            os._exit(1)  # Force exit without further processing

        bad_chunks = [chunk for chunk, chunk_result in results if chunk_result is None]

        # Merge results from completed chunks
        for chunk, chunk_response in results:
            if chunk_response is not None:
                chunk_result = ReviewResult(**chunk_response)
                combined_results.merge(chunk_result, section=chunk)

        combined_results = self._deduplicate_comments(combined_results)
        combined_results.sort()

        # Pass combined results through the filter function with retry logic
        filter_prompt_file = "final_comments_filter.prompty"
        filter_prompt_path = os.path.join(_PROMPTS_FOLDER, filter_prompt_file)

        # load the language-specific yaml file for the filter
        filter_metadata = self._load_filter_metadata()
        print(f"Filtering results...")

        response_json, filter_success = self._execute_filter_prompt(
            combined_results, filter_prompt_path, filter_metadata
        )

        # If filter succeeded, extract the "KEEP" comments, otherwise use all comments
        if filter_success:
            keep_json = [x for x in response_json["comments"] if x["status"] == "KEEP"]
            discard_json = [x for x in response_json["comments"] if x["status"] == "REMOVE"]
        else:
            # Just include all comments if filtering failed
            keep_json = [c.model_dump() for c in combined_results.comments]
            discard_json = []

        final_results = ReviewResult(**{"comments": keep_json})

        end_time = time()
        print(f"Review generated in {end_time - start_time:.2f} seconds.")
        if bad_chunks:
            print(
                f"{self.RED_TEXT}WARN: {len(bad_chunks)}/{len(chunks_to_process)} chunks had errors (see error.log){self.RESET_COLOR}"
            )
        if self.semantic_search_failed:
            print(f"{self.RED_TEXT}WARN: Semantic search failed for some chunks (see error.log).{self.RESET_COLOR}")

        return final_results

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
            "typescript": "JavaScript",
        }
        return language_pretty_names.get(self.language, self.language.capitalize())

    def _retrieve_and_resolve_guidelines(self, query: str) -> List[object] | None:
        try:
            """
            Given a code query, searches the examples index for relevant examples
            and the guidelines index for relevant guidelines based on a structual
            description of the code. Then, it resolves the two sets of results.
            """
            self._ensure_env_vars(["AZURE_SEARCH_NAME"])

            # search the examples index directly with the code snippet
            example_results = self.search.search_examples(query)

            # use a prompt to convert the code snippet to text
            # then do a hybrid search of the guidelines index against this description
            prompt = os.path.join(_PROMPTS_FOLDER, "code_to_text.prompty")
            response = prompty.execute(prompt, inputs={"question": query})
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

    def unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))

    def _process_chunk_with_retry(
        self, chunk_info, static_guidelines, cancel_event, guideline_prompt_file, generic_prompt_file, chunk_status
    ):
        """Process a chunk with retry logic."""
        i, chunk = chunk_info
        chunk_idx = i

        # Check for cancellation
        if cancel_event.is_set():
            return chunk, None

        # Update progress indicator
        chunk_status[chunk_idx] = self.PROCESSING
        print("\r" + "Processing chunks: " + "".join(chunk_status), end="", flush=True)

        def execute_chunk():
            # Build the context string
            if self.use_rag:
                context = self._retrieve_and_resolve_guidelines(str(chunk))
                if context:
                    context_string = context.to_markdown()
                else:
                    logger.warning(f"Failed to retrieve guidelines for chunk {i}, using static guidelines instead.")
                    self.semantic_search_failed = True
                    context_string = json.dumps(static_guidelines)
            else:
                context_string = json.dumps(static_guidelines)

            # Execute prompts in parallel and merge results
            return self._run_parallel_prompts(
                chunk, context_string, guideline_prompt_file, generic_prompt_file, i, 0, max_retries
            )

        def on_retry(exception, attempt, max_attempts):
            # Keep status as PROCESSING during retries
            chunk_status[chunk_idx] = self.PROCESSING
            print("\r" + "Processing chunks: " + "".join(chunk_status), end="", flush=True)

        def on_failure(exception, attempt):
            # Mark as failed on final failure
            chunk_status[chunk_idx] = self.FAILURE
            print("\r" + "Processing chunks: " + "".join(chunk_status), end="", flush=True)
            return None

        max_retries = 5
        result = retry_with_backoff(
            func=execute_chunk,
            max_retries=max_retries,
            retry_exceptions=(json.JSONDecodeError, Exception),
            on_retry=on_retry,
            on_failure=on_failure,
            logger=logger,
            description=f"chunk {i}",
        )

        # Update progress indicator on success if we got a result
        if result is not None:
            chunk_status[chunk_idx] = self.SUCCESS
            print("\r" + "Processing chunks: " + "".join(chunk_status), end="", flush=True)

        return chunk, result

    def _run_parallel_prompts(
        self,
        chunk,
        context_string,
        guideline_prompt_file,
        generic_prompt_file,
        chunk_idx,
        attempt,
        max_retries,
    ):
        """
        Run both guideline and general prompts in parallel for a chunk.

        This method:
        1. Executes both prompts concurrently
        2. Combines their results
        3. Handles errors per prompt

        Returns a merged JSON response with comments from both prompts.
        """
        guideline_tag = "guideline"
        generic_tag = "generic"

        # Run both prompts in parallel for this chunk
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as sub_executor:
            # Set up futures for both prompts
            futures = {
                guideline_tag: sub_executor.submit(
                    prompty.execute,
                    os.path.join(_PROMPTS_FOLDER, guideline_prompt_file),
                    inputs={
                        "language": self._get_language_pretty_name(),
                        "context": context_string,
                        "apiview": chunk.numbered(),
                    },
                )
            }

            generic_metadata = self._load_generic_metadata()
            futures[generic_tag] = sub_executor.submit(
                prompty.execute,
                os.path.join(_PROMPTS_FOLDER, generic_prompt_file),
                inputs={
                    "language": self._get_language_pretty_name(),
                    "apiview": chunk.numbered(),
                    "custom_rules": generic_metadata["custom_rules"],
                },
            )

            # Collect results from all prompts
            results = {}
            for key, future in futures.items():
                try:
                    # Get the raw result text
                    result_text = future.result()

                    # Try to parse as JSON - if this fails, it will be caught and become a retryable error
                    values = json.loads(result_text)

                    # Only proceed if JSON parsing succeeded
                    # Tag each comment with the source prompt tag
                    for item in values.get("comments", []):
                        item["source"] = key
                    results[key] = values
                except json.JSONDecodeError as e:
                    # Log the specific JSON error and re-raise it
                    # This will be caught by the _process_chunk method's retry logic
                    logger.error(
                        f"JSON decode error in {key} prompt for chunk {chunk_idx}, attempt {attempt+1}/{max_retries}: {str(e)}"
                    )
                    # Re-raise to trigger retry in the parent method
                    raise
                except Exception as e:
                    # For non-JSON errors, log but continue with empty results
                    logger.error(
                        f"Error in {key} prompt for chunk {chunk_idx}, attempt {attempt+1}/{max_retries}: {str(e)}"
                    )
                    results[key] = {"comments": []}

        # Merge the guideline_response and general_response into a single result
        json_response = results.get(guideline_tag, {"comments": []})
        json_response["comments"].extend(results.get(generic_tag, {}).get("comments", []))

        return json_response

    def _execute_filter_prompt(self, combined_results, filter_prompt_path, filter_metadata):
        """Execute the filter prompt with retry logic."""

        def run_filter():
            return json.loads(
                prompty.execute(
                    filter_prompt_path,
                    inputs={
                        "comments": combined_results,
                        "language": self._get_language_pretty_name(),
                        "sample": filter_metadata["sample"],
                        "exceptions": filter_metadata["exceptions"],
                    },
                )
            )

        def on_final_failure(exception, attempt):
            print(
                f"{self.RED_TEXT}WARN: Filter prompt failed after {max_filter_retries} attempts, using unfiltered results.{self.RESET_COLOR}"
            )
            return {
                "comments": [{"original_comment": c.model_dump(), "status": "KEEP"} for c in combined_results.comments]
            }

        max_filter_retries = 5
        return (
            retry_with_backoff(
                func=run_filter,
                max_retries=max_filter_retries,
                retry_exceptions=(json.JSONDecodeError, Exception),
                on_failure=on_final_failure,
                logger=logger,
                description="filter prompt",
            ),
            True,
        )

    def _deduplicate_comments(self, results: ReviewResult) -> ReviewResult:
        """
        Deduplicate comments based on line number and rule IDs.
        """
        comments = results.comments
        unique_comments = []
        batches = {}

        # first collect all duplicate comments into batches to send to the LLM and
        # add any unique comments to the unique_comments list
        line_ids = set([x.line_no for x in comments])
        for line_id in line_ids:
            matches = [x for x in comments if x.line_no == line_id]
            if len(matches) == 1:
                unique_comments.append(matches[0])
                continue
            batches[line_id] = matches

        prompt_path = os.path.join(_PROMPTS_FOLDER, "merge_comments.prompty")

        print(f"Deduplicating comments...")
        # now send the batches to the LLM
        # TODO: These should be processed in parallel
        for line_no, batch in batches.items():
            # need to get all the rule_ids for the comments in this batch
            all_rule_ids = set()
            for comment in batch:
                all_rule_ids.update(comment.rule_ids)

            context = self.search.guidelines_for_ids(all_rule_ids)

            response = prompty.execute(prompt_path, inputs={"comments": batch, "context": context})
            merge_results = json.loads(response)
            result_comments = merge_results.get("comments", [])
            if len(result_comments) != 1:
                logger.error(f"Error merging comments for line {line_no}: {merge_results}")
                continue
            merged_comment = result_comments[0]
            merged_comment["source"] = "merged"
            merged_comment_obj = Comment(**merged_comment)
            unique_comments.append(merged_comment_obj)

        # update the comments list with the unique comments
        results.comments = unique_comments
        return results
