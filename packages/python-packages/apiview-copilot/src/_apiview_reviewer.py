from azure.identity import DefaultAzureCredential

from collections import deque
import json
import os
import prompty
import prompty.azure
from time import time
from typing import Literal, List

from ._sectioned_document import SectionedDocument
from ._search_manager import SearchManager
from ._models import ReviewResult

if "APPSETTING_WEBSITE_SITE_NAME" not in os.environ:
    # running on dev machine, loadenv
    import dotenv

    dotenv.load_dotenv()

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_PROMPTS_FOLDER = os.path.join(_PACKAGE_ROOT, "prompts")

CREDENTIAL = DefaultAzureCredential()


class ApiViewReview:

    def __init__(self, *, language: str, model: Literal["gpt-4o-mini", "o3-mini"]):
        self.language = language
        self.model = model
        self.search = SearchManager(language=language)
        self.output_parser = ReviewResult

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
        print(f"Generating review...")
        start_time = time()
        apiview = self.unescape(apiview)
        if not use_rag:
            guidelines = self.search.retrieve_static_guidelines(
                self.language, include_general_guidelines=True
            )
            guideline_ids = [g["id"] for g in guidelines]
        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=chunk_input)
        final_results = ReviewResult(status="Success", violations=[])
        max_retries = 5
        for i, chunk in enumerate(chunked_apiview):
            for j in range(max_retries):
                print(
                    f"Processing chunk {i + 1}/{len(chunked_apiview)}... (Attempt {j + 1}/{max_retries})"
                )
                if i == 0 and len(chunked_apiview.sections) > 1:
                    # the first chunk is the header, so skip it
                    continue

                if use_rag:
                    # use the Azure OpenAI service to get guidelines
                    context = self._retrieve_and_resolve_guidelines(str(chunk))
                    guideline_ids = [g.id for g in context]
                # select the appropriate prompty file and run it
                prompt_file = f"review_apiview_{self.model}.prompty".replace("-", "_")
                prompt_path = os.path.join(_PROMPTS_FOLDER, prompt_file)
                response = prompty.execute(
                    prompt_path,
                    inputs={
                        "language": self.language,
                        "context": (
                            context.to_markdown() if use_rag else json.dumps(guidelines)
                        ),
                        "apiview": chunk.numbered(),
                    },
                )
                try:
                    json_object = json.loads(response)
                    chunk_result = ReviewResult(**json_object)
                    final_results.merge(chunk_result, section=chunk)
                    break
                except json.JSONDecodeError:
                    if j == max_retries - 1:
                        print(
                            f"WARNING: Failed to decode JSON for chunk {i}: {response}"
                        )
                        break
                    else:
                        print(
                            f"WARNING: Failed to decode JSON for chunk {i}: {response}. Retrying..."
                        )
                        continue
        final_results.validate(guideline_ids=guideline_ids)
        final_results.sort()
        end_time = time()
        print(f"Review generated in {end_time - start_time:.2f} seconds.")
        return final_results

    def _retrieve_and_resolve_guidelines(self, query: str) -> List[object]:
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

    def unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))
