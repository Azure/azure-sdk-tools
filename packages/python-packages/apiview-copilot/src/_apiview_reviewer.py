from azure.search.documents import SearchClient
from azure.search.documents.models import VectorizableTextQuery, QueryType, QueryCaptionType
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

    def __init__(self, *, language: str, model: Literal["gpt-4o-mini", "o3-mini"], log_prompts: bool = False,):
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

    def get_response(self, apiview: str, *, chunk_input: bool = False, use_rag: bool = False) -> GuidelinesResult:
        print(f"Generating review...")
        start_time = time()
        apiview = self.unescape(apiview)
        if not use_rag:
            guidelines = self._retrieve_static_guidelines(self.language, include_general_guidelines=True)
        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=chunk_input)
        final_results = GuidelinesResult(status="Success", violations=[])
        max_retries = 5
        for i, chunk in enumerate(chunked_apiview):
            for j in range(max_retries):
                print(f"Processing chunk {i + 1}/{len(chunked_apiview)}... ({j + 1}/{max_retries})")
                if i == 0 and len(chunked_apiview.sections) > 1:
                    # the first chunk is the header, so skip it
                    continue

                if use_rag:
                    # use the Azure OpenAI service to get guidelines
                    context = self._retrieve_guidelines_from_search(chunk)
                    guidelines = context["guidelines"]
                # select the appropriate prompty file and run it
                prompt_file = f"review_apiview_{self.model}.prompty".replace("-", "_")
                prompt_path = os.path.join(_PROMPTS_FOLDER, prompt_file)
                response = prompty.execute(prompt_path, inputs={
                    "language": self.language,
                    "context": json.dumps(context) if use_rag else json.dumps(guidelines),
                    "apiview": chunk.numbered(),
                })
                try:
                    json_object = json.loads(response)
                    chunk_result = GuidelinesResult(**json_object)
                    final_results.merge(chunk_result, section=chunk)
                    break
                except json.JSONDecodeError:
                    if j == max_retries - 1:
                        print(f"WARNING: Failed to decode JSON for chunk {i}: {response}")
                        break
                    else:
                        print(f"WARNING: Failed to decode JSON for chunk {i}: {response}. Retrying...")
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
        result = list(client.search(
            search_text=query,
            top=10,
            filter=self._get_filter_expression(),
            semantic_configuration_name="archagent-semantic-search-guidelines",
            query_type=QueryType.SEMANTIC,
            query_caption=QueryCaptionType.EXTRACTIVE,
            vector_queries=[
                VectorizableTextQuery(
                    text=query,
                    fields="text_vector"                    
                )
            ]))
        return result

    def _search_examples(self, chunk: Section) -> List[object]:
        credential = DefaultAzureCredential()
        search_name = os.getenv("AZURE_SEARCH_NAME")
        search_endpoint = f"https://{search_name}.search.windows.net"
        client = SearchClient(endpoint=search_endpoint, index_name="examples-index", credential=credential)
        query = str(chunk)
        return list(client.search(
            search_text=query,
            top=10,
            filter=self._get_filter_expression(),
            semantic_configuration_name="archagent-semantic-search-examples",
            query_type=QueryType.SEMANTIC,
            query_caption=QueryCaptionType.EXTRACTIVE,
            vector_queries=[
                VectorizableTextQuery(
                    text=query,
                    fields="text_vector"
                )
            ]))

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


        # intial ids from the search queries
        starting_example_ids = list(set([x["id"] for x in example_results]))
        starting_guideline_ids = list(set([x["id"] for x in guideline_results]))

        # track processed IDs to avoid loops
        seen_guideline_ids = set()
        seen_example_ids = set()

        # track the final results
        final_guidelines = {}
        final_examples = {}
        for ex in starting_example_ids:
            final_examples[ex] = None

        # queue for BFS traversal
        queue = deque(starting_guideline_ids)
        batch_size = 50

        def batch_query(container: CosmosClient, id_list: List[str]) -> List[object]:
            """
            Helper function to batch query the container.
            """
            results = []
            for i in range(0, len(id_list), batch_size):
                batch = id_list[i:i + batch_size]
                placeholders = ",".join([f"@id{i}" for i in range(len(batch))])
                query  = f"SELECT * FROM c WHERE c.id IN ({placeholders})"
                parameters = [{"name": f"@id{i}", "value": value} for i, value in enumerate(batch)]
                results.extend(list(container.query_items(
                    query=query,
                    parameters=parameters,
                    enable_cross_partition_query=True
                )))
            return results

        while queue:
            batch_ids = list(set([queue.popleft() for _ in range(min(batch_size, len(queue))) if _ not in seen_guideline_ids]))
            if not batch_ids:
                continue

            guidelines = batch_query(guidelines_container, batch_ids)
            for guideline in guidelines:
                gid = guideline["id"]
                if gid in seen_guideline_ids:
                    continue

                seen_guideline_ids.add(gid)
                final_guidelines[gid] = guideline

                # queue up related guidelines
                for rel in guideline.get("related_guidelines", []):
                    if rel not in seen_guideline_ids:
                        queue.append(rel)

                # now do the same for examples
                for ex in guideline.get("related_examples", []):
                    try:
                        if ex not in seen_example_ids:
                            seen_example_ids.add(ex)
                            final_examples[ex] = None
                    except TypeError:
                        # FIXME: This shouldn't happen once the data integrity is cleaned up
                        print(f"WARNING: Examples for guideline {gid} is not a string! Skipping.")
                        continue

            # now resolve all examples
            example_ids_to_lookup = [eid for eid, val in final_examples.items() if val is None]
            examples = batch_query(examples_container, example_ids_to_lookup)

            for ex in examples:
                exid = ex["id"]
                final_examples[exid] = ex

                # queue up more related guidelines from the example
                for gid in ex.get("guideline_ids", []):
                    if gid not in seen_guideline_ids:
                        queue.append(gid)

        # flatten the results to just the values
        final_guidelines = [v for v in final_guidelines.values() if v is not None]
        final_examples = [v for v in final_examples.values() if v is not None]

        # remove irrelevant guideline fields
        remove_guideline_fields = ["category", "_rid", "_self", "_etag", "_attachments", "_ts", "related_guidelines", "related_examples"]
        for guideline in final_guidelines:
            for field in remove_guideline_fields:
                if field in guideline:
                    del guideline[field]

        # remove irrelevant example fields
        remove_example_fields = ["_rid", "_self", "_etag", "_attachments", "_ts", "guideline_ids"]
        for example in final_examples:
            for field in remove_example_fields:
                if field in example:
                    del example[field]

        return {
            "guidelines": final_guidelines,
            "examples": final_examples,
        }
