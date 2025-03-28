from azure.search.documents import SearchClient
from azure.identity import DefaultAzureCredential

import dotenv
import json
import os
import prompty
import prompty.azure
import sys
from typing import Literal, List

from ._sectioned_document import SectionedDocument, Section
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

    def __init__(self, *, language: str, model: Literal["gpt-4o-mini", "o3-mini"], use_rag: bool = True, log_prompts: bool = False,):
        self.language = language
        self.model = model
        self.output_parser = GuidelinesResult
        self.log_prompts = log_prompts
        if self.log_prompts is None:
            self.log_prompts = os.getenv("APIVIEW_LOG_PROMPTS", "false").lower() == "true"
        self.use_rag = use_rag
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
        if not self.use_rag:
            guidelines = self._retrieve_static_guidelines(self.language, include_general_guidelines=True)
        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=chunk_input)
        final_results = GuidelinesResult(status="Success", violations=[])
        for i, chunk in enumerate(chunked_apiview):
            if self.use_rag:
                # use the Azure OpenAI service to get guidelines
                guidelines = self._retrieve_guidelines_from_search(chunk)
            # select the appropriate prompty file and run it
            prompt_file = f"review_apiview_{self.model}.prompty".replace("-", "_")
            prompt_path = os.path.join(_PROMPTS_FOLDER, prompt_file)
            response = prompty.execute(prompt_path, inputs={
                "language": self.language,
                "context": json.dumps(guidelines),
                "apiview": chunk.numbered(),
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

    def _retrieve_static_guidelines(self, language, include_general_guidelines: bool = False) -> List[object]:
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
    
    def _search_guidelines(self, chunk: Section) -> List[object]:
        credential = DefaultAzureCredential()
        search_name = os.getenv("AZURE_SEARCH_NAME")
        search_endpoint = f"https://{search_name}.search.windows.net"
        client = SearchClient(endpoint=search_endpoint, index_name="guidelines-index", credential=credential)
        return list(client.search(search_text=str(chunk), top=10, filter=f"language eq '{self.language}' or language eq ''"))

    def _search_examples(self, chunk: Section) -> List[object]:
        credential = DefaultAzureCredential()
        search_name = os.getenv("AZURE_SEARCH_NAME")
        search_endpoint = f"https://{search_name}.search.windows.net"
        client = SearchClient(endpoint=search_endpoint, index_name="examples-index", credential=credential)
        return list(client.search(search_text=str(chunk), top=10, filter=f"language eq '{self.language}' or language eq ''"))

    def _retrieve_guidelines_from_search(self, chunk: Section) -> List[object]:
        """
        Retrieves the guidelines for the given language from Azure AI Search service.
        """
        if os.getenv("AZURE_SEARCH_NAME") is None:
            raise ValueError("AZURE_SEARCH_NAME environment variable not set")
        
        # search the examples index directly with the code snippet
        example_results = self._search_examples(chunk)
        example_scores = [x["@search.score"] for x in example_results]
        
        # use a prompt to convert the code snippet to text
        # then do a hybrid search of the guidelines index against this description
        prompt = os.path.join(_PROMPTS_FOLDER, "code_to_text.prompty")
        response = prompty.execute(prompt, inputs={"question": str(chunk)})
        guideline_results = self._search_guidelines(response)
        guideline_scores = [x["@search.score"] for x in guideline_results]

        # TODO: Now we need to resolve all of the stuff
        test = "gest"
        return []


