import dotenv
import json
import os
import prompty
import prompty.azure
import sys
from typing import Literal

from ._sectioned_document import SectionedDocument
from ._models import GuidelinesResult

if "APPSETTING_WEBSITE_SITE_NAME" not in os.environ:
    # running on dev machine, loadenv
    import dotenv
    dotenv.load_dotenv()

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_GUIDELINES_FOLDER = os.path.join(_PACKAGE_ROOT, "guidelines")
_PROMPTS_FOLDER = os.path.join(_PACKAGE_ROOT, "prompts")

DEFAULT_MODEL = "o3-mini"


class ApiViewReview:

    def __init__(self, *, language: str, model: Literal["gpt-4o-mini", "o3-mini"] = DEFAULT_MODEL, log_prompts: bool = False,):
        self.language = language
        self.model = model
        self.output_parser = GuidelinesResult
        self.log_prompts = log_prompts
        if self.log_prompts is None:
            self.log_prompts = os.getenv("APIVIEW_LOG_PROMPTS", "false").lower() == "true"
        if log_prompts:
            # remove the folder if it exists
            base_path = os.path.join(_PACKAGE_ROOT, "scratch", "prompts")
            if os.path.exists(base_path):
                import shutil
                shutil.rmtree(base_path)
            os.makedirs(base_path)

    def _hash(self, obj) -> str:
        return str(hash(json.dumps(obj)))

    def get_response(self, apiview: str, *, chunk_input: bool = False) -> GuidelinesResult:
        apiview = self.unescape(apiview)
        guidelines = self._retrieve_static_guidelines(self.language, include_general_guidelines=True)
        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=chunk_input)
        final_results = GuidelinesResult(status="Success", violations=[])
        for i, chunk in enumerate(chunked_apiview):
            # select the appropriate prompty file and run it
            prompt_file = f"review_apiview_{self.model}.prompty".replace("-", "_")
            prompt_path = os.path.join(_PROMPTS_FOLDER, prompt_file)
            response = prompty.execute(prompt_path, inputs={
                "language": self.language,
                "context": json.dumps(guidelines),
                "apiview": str(chunk),
            })
            try:
                json_object = json.loads(response)
                chunk_result = GuidelinesResult(**json_object)
                final_results.merge(chunk_result, section=chunk)
            except json.JSONDecodeError:
                print(f"WARNING: Failed to decode JSON for chunk {i}: {response}")
                continue
        final_results.validate(guidelines=guidelines)
        final_results.sort()
        return final_results

    def unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))

    def _retrieve_static_guidelines(self, language, include_general_guidelines: bool = False):
        """
        Retrieves the guidelines for the given language, optional with general guidelines.
        This method retrieves guidelines statically from the file system. It does not
        query any Azure service.
        """
        general_guidelines = []
        if include_general_guidelines:
            general_guidelines_path = os.path.join(_GUIDELINES_FOLDER, "general")
            for filename in os.listdir(general_guidelines_path):
                with open(os.path.join(general_guidelines_path, filename), "r") as f:
                    items = json.loads(f.read())
                    general_guidelines.extend(items)

        language_guidelines = []
        language_guidelines_path = os.path.join(_GUIDELINES_FOLDER, language)
        for filename in os.listdir(language_guidelines_path):
            with open(os.path.join(language_guidelines_path, filename), "r") as f:
                items = json.loads(f.read())
                language_guidelines.extend(items)
        return general_guidelines + language_guidelines
