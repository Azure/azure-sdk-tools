from azure.identity import DefaultAzureCredential
import concurrent.futures
import json
import logging
import os
import prompty
import prompty.azure_beta
import sys
import threading
from time import time
from typing import Literal, List

from ._sectioned_document import SectionedDocument, Section
from ._search_manager import SearchManager
from ._models import ReviewResult

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

    def get_response(
        self, apiview: str, *, chunk_input: bool = False, use_rag: bool = False
    ) -> ReviewResult:
        print("Generating review...")
        logger.info(
            f"Starting review with model: {self.model}, language: {self.language}, RAG: {use_rag}"
        )

        start_time = time()
        apiview = self.unescape(apiview)

        # chunk the document into sections if desired
        doc = SectionedDocument(apiview.splitlines(), chunk=True)
        chunks = list(doc)  # each chunk is a Section

        prompts = [
            "pizza_guideline_o3_mini",
            "noodle_parameter_guideline_o3_mini",
            # add more prompts here
        ]
        prompt_paths = [os.path.join(_PROMPTS_FOLDER, p + ".prompty") for p in prompts]

        final_results = ReviewResult(status="Success", violations=[])

        # ANSI status symbols
        PENDING = "░"
        PROCESSING = "▒"
        SUCCESS = "\033[32m█\033[0m"
        FAILURE = "\033[31m█\033[0m"

        # one slot per (chunk, prompt) pair
        total = len(chunks) * len(prompt_paths)
        status = [PENDING] * total
        print("Processing chunks/prompts: " + "".join(status), end="", flush=True)

        def run_task(chunk_idx: int, chunk: Section, prompt_idx: int, prompt_path: str):
            try:
                resp = prompty.execute(
                    prompt_path,
                    inputs={
                        "language": self.language,
                        "apiview": chunk.numbered(),  # numbered lines
                    },
                )
                data = json.loads(resp)
                return chunk_idx, prompt_idx, ReviewResult(**data), None
            except Exception as e:
                return chunk_idx, prompt_idx, None, e

        # schedule all (chunk, prompt) tasks
        with concurrent.futures.ThreadPoolExecutor() as executor:
            futures = {}
            index_map: List[tuple[int, int]] = []
            slot = 0
            for c_idx, chunk in enumerate(chunks):
                for p_idx, path in enumerate(prompt_paths):
                    fut = executor.submit(run_task, c_idx, chunk, p_idx, path)
                    futures[fut] = slot
                    index_map.append((c_idx, p_idx))
                    slot += 1

            # process results as they arrive
            for fut in concurrent.futures.as_completed(futures):
                slot = futures[fut]
                c_idx, p_idx = index_map[slot]

                status[slot] = PROCESSING
                print(
                    "\rProcessing chunks/prompts: " + "".join(status),
                    end="",
                    flush=True,
                )

                _, _, result, error = fut.result()
                if error:
                    status[slot] = FAILURE
                    logger.error(
                        f"Error in chunk {c_idx}, prompt '{prompts[p_idx]}': {error}"
                    )
                else:
                    status[slot] = SUCCESS
                    final_results.merge(result, section=chunks[c_idx])

                print(
                    "\rProcessing chunks/prompts: " + "".join(status),
                    end="",
                    flush=True,
                )

        print()  # newline after bar

        final_results.sort()
        duration = time() - start_time
        print(f"Review generated in {duration:.2f} seconds.")
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
