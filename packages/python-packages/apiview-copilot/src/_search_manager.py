from azure.cosmos import CosmosClient
from azure.search.documents import SearchClient, SearchItemPaged
from azure.search.documents.models import (
    VectorizableTextQuery,
    QueryType,
    QueryAnswerType,
    QueryAnswerResult,
    QueryCaptionType,
    QueryCaptionResult,
)
from azure.identity import DefaultAzureCredential

from collections import deque
import json
import os
import prompty
import prompty.azure
from typing import List, Dict


if "APPSETTING_WEBSITE_SITE_NAME" not in os.environ:
    # running on dev machine, loadenv
    import dotenv

    dotenv.load_dotenv()

# Cosmos DB
COSMOS_ACC_NAME = os.environ.get("AZURE_COSMOS_ACC_NAME")
COSMOS_DB_NAME = os.environ.get("AZURE_COSMOS_DB_NAME")
COSMOS_ENDPOINT = f"https://{COSMOS_ACC_NAME}.documents.azure.com:443/"

# Azure AI Search
AZURE_SEARCH_NAME = os.environ.get("AZURE_SEARCH_NAME")
SEARCH_ENDPOINT = f"https://{AZURE_SEARCH_NAME}.search.windows.net"

CREDENTIAL = DefaultAzureCredential()

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_GUIDELINES_FOLDER = os.path.join(_PACKAGE_ROOT, "guidelines")
_PROMPTS_FOLDER = os.path.join(_PACKAGE_ROOT, "prompts")


class SearchItem:
    """
    Represents a single search result item.
    """

    def __init__(self, result: Dict):
        self.id = result.get("id")
        self.text = result.get("chunk")
        self.lang = result.get("lang")
        self.title = result.get("title")
        self.score = result.get("@search.score")
        self.reranker_score = result.get("@search.reranker_score")
        self.captions = []
        for caption in result.get("@search.captions", []):
            self.captions.append(SearchCaption(caption))


class SearchAnswer:
    """
    Represents a single answer from the search results.
    """

    def __init__(self, result: QueryAnswerResult):
        self.text = result.text
        self.score = result.score
        self.highlights = result.highlights


class SearchCaption:
    """
    Represents a single caption from the search results.
    """

    def __init__(self, result: Dict):
        self.text = result.text
        self.highlights = result.highlights


class SearchResult:
    """
    Represents the search results.
    """

    def __init__(self, search_results: SearchItemPaged[Dict]):
        result_list = list(search_results)
        self.results = []
        self.answers = []
        for result in result_list:
            self.results.append(SearchItem(result))
        for answer in search_results.get_answers():
            self.answers.append(SearchAnswer(answer))


class SearchManager:

    def __init__(self, *, language: str):
        self.language = language
        self.filter_expression = f"lang eq '{language}' or lang eq '' or lang eq null"

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

    def retrieve_static_guidelines(
        self, language, include_general_guidelines: bool = False
    ) -> List[object]:
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

    def search_guidelines(self, query: str) -> SearchResult:
        """
        Searches the guidelines index for the given query and
        returns the results as a SearchResult object.
        """
        self._ensure_env_vars(["AZURE_SEARCH_NAME"])
        client = SearchClient(
            endpoint=SEARCH_ENDPOINT,
            index_name="guidelines-index",
            credential=CREDENTIAL,
        )
        result = client.search(
            search_text=query,
            filter=self.filter_expression,
            semantic_configuration_name="archagent-semantic-search-guidelines",
            query_type=QueryType.SEMANTIC,
            query_caption=QueryCaptionType.EXTRACTIVE,
            query_answer=QueryAnswerType.EXTRACTIVE,
            top=10,
            vector_queries=[VectorizableTextQuery(text=query, fields="text_vector")],
        )
        return SearchResult(result)

    def search_examples(self, query: str) -> SearchResult:
        """
        Searches the examples index for the given query and
        returns the results as a SearchResult object.
        """
        self._ensure_env_vars(["AZURE_SEARCH_NAME"])
        client = SearchClient(
            endpoint=SEARCH_ENDPOINT, index_name="examples-index", credential=CREDENTIAL
        )
        result = client.search(
            search_text=query,
            top=10,
            filter=self.filter_expression,
            semantic_configuration_name="archagent-semantic-search-examples",
            query_type=QueryType.SEMANTIC,
            query_caption=QueryCaptionType.EXTRACTIVE,
            query_answer=QueryAnswerType.EXTRACTIVE,
            query_answer_count=3,
            vector_queries=[VectorizableTextQuery(text=query, fields="text_vector")],
        )
        return SearchResult(result)

    def retrieve_and_resolve_guidelines(self, query: str) -> List[object]:
        """
        Given a code query, searches the examples index for relevant examples
        and the guidelines index for relevant guidelines based on a structual
        description of the code. Then, it resolves the two sets of results.
        """
        self._ensure_env_vars(["AZURE_SEARCH_NAME"])

        # search the examples index directly with the code snippet
        example_results = self.search_examples(query)

        # use a prompt to convert the code snippet to text
        # then do a hybrid search of the guidelines index against this description
        prompt = os.path.join(_PROMPTS_FOLDER, "code_to_text.prompty")
        response = prompty.execute(prompt, inputs={"question": query})
        guideline_results = self.search_guidelines(response)

        context = self._retrieve_and_resolve_context(guideline_results, example_results)
        return context

    def _retrieve_and_resolve_context(
        self, guideline_results: List[object], example_results: List[object]
    ) -> List[object]:
        self._ensure_env_vars(["AZURE_COSMOS_ACC_NAME", "AZURE_COSMOS_DB_NAME"])
        client = CosmosClient(COSMOS_ENDPOINT, credential=CREDENTIAL)
        database = client.get_database_client(COSMOS_DB_NAME)
        guidelines_container = database.get_container_client("guidelines")
        examples_container = database.get_container_client("examples")

        # initial ids from the search queries
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
                batch = id_list[i : i + batch_size]
                placeholders = ",".join([f"@id{i}" for i in range(len(batch))])
                query = f"SELECT * FROM c WHERE c.id IN ({placeholders})"
                parameters = [
                    {"name": f"@id{i}", "value": value} for i, value in enumerate(batch)
                ]
                results.extend(
                    list(
                        container.query_items(
                            query=query,
                            parameters=parameters,
                            enable_cross_partition_query=True,
                        )
                    )
                )
            return results

        while queue:
            batch_ids = list(
                set(
                    [
                        queue.popleft()
                        for _ in range(min(batch_size, len(queue)))
                        if _ not in seen_guideline_ids
                    ]
                )
            )
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
                for rel in guideline.get("related_guidelines") or []:
                    if rel not in seen_guideline_ids:
                        queue.append(rel)

                # now do the same for examples
                for ex in guideline.get("related_examples") or []:
                    try:
                        if ex not in seen_example_ids:
                            seen_example_ids.add(ex)
                            final_examples[ex] = None
                    except TypeError:
                        # FIXME: This shouldn't happen once the data integrity is cleaned up
                        print(
                            f"WARNING: Examples for guideline {gid} is not a string! Skipping."
                        )
                        continue

            # now resolve all examples
            example_ids_to_lookup = [
                eid for eid, val in final_examples.items() if val is None
            ]
            examples = batch_query(examples_container, example_ids_to_lookup)

            for ex in examples:
                ex_id = ex["id"]
                final_examples[ex_id] = ex

                # queue up more related guidelines from the example
                for gid in ex.get("guideline_ids", []):
                    if gid not in seen_guideline_ids:
                        queue.append(gid)

        # flatten the results to just the values
        final_guidelines = [v for v in final_guidelines.values() if v is not None]
        final_examples = [v for v in final_examples.values() if v is not None]

        # remove irrelevant guideline fields
        remove_guideline_fields = [
            "category",
            "_rid",
            "_self",
            "_etag",
            "_attachments",
            "_ts",
            "related_guidelines",
            "related_examples",
        ]
        for guideline in final_guidelines:
            for field in remove_guideline_fields:
                if field in guideline:
                    del guideline[field]

        # remove irrelevant example fields
        remove_example_fields = [
            "_rid",
            "_self",
            "_etag",
            "_attachments",
            "_ts",
            "guideline_ids",
        ]
        for example in final_examples:
            for field in remove_example_fields:
                if field in example:
                    del example[field]

        return {
            "guidelines": final_guidelines,
            "examples": final_examples,
        }
