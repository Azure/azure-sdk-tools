from azure.identity import DefaultAzureCredential
import concurrent.futures
import json
import logging
import os
import prompty
import pathlib
import prompty.azure_beta
import sys
import threading
from time import time
from typing import Literal, List

from ._sectioned_document import SectionedDocument
from ._search_manager import SearchManager
from ._models import ReviewResult, GeneralReviewResult

# Set up the logger at the module level
_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_PROMPTS_FOLDER = os.path.join(_PACKAGE_ROOT, "prompts")

# Create output folder for logs
output_folder = os.path.join(_PACKAGE_ROOT, "scratch", "output")
os.makedirs(output_folder, exist_ok=True)
log_file = os.path.join(output_folder, "error.log")

# Configure logger for immediate outputs (no buffering)
logging.basicConfig(
    filename=log_file,
    filemode="w",  # 'w' means write mode (overwrites existing file)
    level=logging.ERROR,
    format="%(asctime)s - %(levelname)s - %(message)s",
    force=True,  # Override any existing logger configuration
)

for handler in logging.root.handlers:
    if isinstance(handler, logging.FileHandler):
        handler.setLevel(logging.ERROR)

        # Instead, call flush() after each log message if needed
        # Or use this approach to make writes unbuffered:
        handler.formatter = logging.Formatter(
            "%(asctime)s - %(levelname)s - %(message)s"
        )

# Create a module-level logger that can be used anywhere in the file
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


class ApiViewReview:

    def __init__(self, *, language: str, model: str = DEFAULT_MODEL):
        self.language = language
        self.model = model
        self.search = SearchManager(language=language)
        self.output_parser = ReviewResult
        self.semantic_search_failed = False
        if model not in supported_models:
            raise ValueError(
                f"Model {model} not supported. Supported models are: {', '.join(supported_models)}"
            )

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

    def get_general_review_response(self, apiview: str) -> GeneralReviewResult:
        lines = self.unescape(apiview).splitlines()
        start_line_no = 0
        numbered_lines = []
        for i, line in enumerate(lines):
            numbered_lines.append(f"{start_line_no + i + 1:4d}: {line}")
        apiview = "\n".join(numbered_lines)

        prompt_path = pathlib.Path(_PROMPTS_FOLDER) / f"review_apiview_{self.language}.prompty"
        judge_path = pathlib.Path(_PROMPTS_FOLDER) / f"review_apiview_{self.language}_judge.prompty"

        response = prompty.execute(
            prompt_path,
            inputs={
                "language": self.language,
                "apiview": apiview,
            }
        )
        initial_review = json.loads(response)

        response = prompty.execute(
            judge_path,
            inputs={
                "language": self.language,
                "apiview": apiview,
                "review_results": initial_review,
                "guidelines": self.search.retrieve_static_guidelines(
                    self.language, include_general_guidelines=False
                ),
            }
        )
        final_review = json.loads(response)
        return GeneralReviewResult(**final_review)

    def get_response(
        self, apiview: str, *, chunk_input: bool = False, use_rag: bool = False
    ) -> ReviewResult:
        print(f"Generating review...")

        logger.info(
            f"Starting review with model: {self.model}, language: {self.language}, RAG: {use_rag}"
        )

        start_time = time()
        apiview = self.unescape(apiview)
        if not use_rag:
            guidelines = self.search.retrieve_static_guidelines(
                self.language, include_general_guidelines=True
            )

        # Prepare the document
        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=chunk_input)
        final_results = ReviewResult(status="Success", violations=[])

        # Skip header if multiple sections
        chunks_to_process = []
        for i, chunk in enumerate(chunked_apiview):
            if i == 0 and len(chunked_apiview.sections) > 1:
                # the first chunk is the header, so skip it
                continue
            chunks_to_process.append((i, chunk))

        # Define status characters with colors
        PENDING = "░"
        PROCESSING = "▒"
        SUCCESS = "\033[32m█\033[0m"  # Green square
        FAILURE = "\033[31m█\033[0m"  # Red square
        RED_TEXT = "\033[31m"  # Red text
        RESET_COLOR = "\033[0m"  # Reset to default text color

        # Print initial progress bar
        print("Processing chunks: ", end="", flush=True)
        chunk_status = [PENDING] * len(chunks_to_process)

        # select the appropriate prompty file
        prompty_type = model_map[self.model]
        prompt_file = f"review_apiview_{prompty_type}.prompty".replace("-", "_")
        prompt_path = os.path.join(_PROMPTS_FOLDER, prompt_file)
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

        # Register the handler for SIGINT (Ctrl+C)
        import signal

        original_handler = signal.getsignal(signal.SIGINT)
        signal.signal(signal.SIGINT, keyboard_interrupt_handler)

        try:
            # Define a function to process a single chunk and update progress
            def process_chunk(chunk_info):
                # Check for cancellation
                if cancel_event.is_set():
                    return chunk_info[1], None

                i, chunk = chunk_info
                chunk_idx = chunks_to_process.index((i, chunk))
                max_retries = 5

                for j in range(max_retries):
                    # Check for cancellation again
                    if cancel_event.is_set():
                        return chunk, None

                    chunk_status[chunk_idx] = PROCESSING
                    print(
                        "\r" + "Processing chunks: " + "".join(chunk_status),
                        end="",
                        flush=True,
                    )

                    try:
                        if use_rag:
                            context = self._retrieve_and_resolve_guidelines(str(chunk))
                            if context is None:
                                logger.warning(
                                    f"Failed to retrieve guidelines for chunk {i}, using static guidelines instead."
                                )
                                self.semantic_search_failed = True
                                context = self.search.retrieve_static_guidelines(
                                    self.language, include_general_guidelines=True
                                )
                                context_string = json.dumps(context)
                            else:
                                context_string = context.to_markdown()
                        else:
                            context = guidelines
                            context_string = json.dumps(context)

                        response = prompty.execute(
                            prompt_path,
                            inputs={
                                "language": self.language,
                                "context": context_string,
                                "apiview": chunk.numbered(),
                            },
                        )
                        json_response = json.loads(response)
                        chunk_status[chunk_idx] = SUCCESS  # Green for success
                        print(
                            "\r" + "Processing chunks: " + "".join(chunk_status),
                            end="",
                            flush=True,
                        )
                        return chunk, json_response
                    except json.JSONDecodeError as e:
                        # handle JSON errors
                        error_msg = f"JSON decode error in chunk {i}, attempt {j+1}/{max_retries}: {str(e)}"
                        logger.error(error_msg)

                        if j == max_retries - 1:
                            chunk_status[chunk_idx] = FAILURE
                            print(
                                "\r" + "Processing chunks: " + "".join(chunk_status),
                                end="",
                                flush=True,
                            )
                            return chunk, None
                    except Exception as e:
                        # Catch all other exceptions
                        error_msg = f"Error processing chunk {i}, attempt {j+1}/{max_retries}: {str(e)}"
                        logger.error(error_msg)

                        if j == max_retries - 1:
                            chunk_status[chunk_idx] = FAILURE
                            print(
                                "\r" + "Processing chunks: " + "".join(chunk_status),
                                end="",
                                flush=True,
                            )
                            return chunk, None

                # If we get here, we've exhausted all retries
                logger.error(
                    f"Failed to process chunk {i} after {max_retries} attempts"
                )
                chunk_status[chunk_idx] = FAILURE
                print(
                    "\r" + "Processing chunks: " + "".join(chunk_status),
                    end="",
                    flush=True,
                )
                return chunk, None

            # Process chunks in parallel using ThreadPoolExecutor
            with concurrent.futures.ThreadPoolExecutor() as executor:
                # Submit all tasks
                future_to_chunk = {
                    executor.submit(process_chunk, chunk_info): chunk_info
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
                            chunk_status[chunk_idx] = FAILURE
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
                final_results.merge(chunk_result, section=chunk)

        final_results.sort()
        end_time = time()
        print(f"Review generated in {end_time - start_time:.2f} seconds.")
        if bad_chunks:
            print(
                f"{RED_TEXT}WARN: {len(bad_chunks)}/{len(chunks_to_process)} chunks had errors (see error.log){RESET_COLOR}"
            )
        if self.semantic_search_failed:
            print(
                f"{RED_TEXT}WARN: Semantic search failed for some chunks (see error.log).{RESET_COLOR}"
            )

        return final_results

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

    def unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))
