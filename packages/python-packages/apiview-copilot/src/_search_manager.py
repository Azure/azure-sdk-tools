from collections import deque
import copy
import os
from typing import List, Dict, Optional

from azure.cosmos import CosmosClient
from azure.search.documents import SearchClient, SearchItemPaged
from azure.search.documents.models import (
    VectorizableTextQuery,
    QueryType,
    QueryAnswerType,
    QueryAnswerResult,
    QueryCaptionType,
    SemanticErrorMode,
)

from src._credential import get_credential
from src._models import Guideline, Example, Memory


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

CREDENTIAL = get_credential()

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_GUIDELINES_FOLDER = os.path.join(_PACKAGE_ROOT, "guidelines")


class SearchItem:
    """
    Represents a single search result item.
    """

    def __init__(self, result: Dict):
        self.id = result.get("id")
        self.kind = result.get("kind")
        self.title = result.get("title")
        self.content = result.get("chunk")
        self.language = result.get("language")
        self.service = result.get("service")
        self.is_exception = result.get("is_exception") or False
        self.example_type = result.get("example_type")
        self.source = result.get("source")
        self.score = result.get("@search.score", None)
        self.reranker_score = result.get("@search.reranker_score", None)
        self.captions = []
        captions = result.get("@search.captions", None)
        for caption in captions or []:
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
        answers = search_results.get_answers()
        if answers:
            for answer in answers:
                self.answers.append(SearchAnswer(answer))

    def __len__(self):
        return len(self.results)

    def __iter__(self):
        return iter(self.results)


class Context:
    """
    Represents the resolved context of a search with objects
    from the CosmosDB database.
    """

    items: List["ContextItem"]

    def __init__(
        self,
        *,
        guidelines: Dict[str, object] = None,
        memories: Dict[str, object] = None,
        examples: Dict[str, object] = None,
        scores: Optional[dict] = None,
    ):
        self.items = []
        scores = scores or {}
        # Propagate scores from examples to guidelines/memories if needed
        # First, collect example scores
        example_scores = {ex_id: scores.get(ex_id) for ex_id in (examples or {})}

        # For guidelines, use their own score, or the max score of their related examples
        for guideline in guidelines.values():
            score = scores.get(getattr(guideline, "id", None))
            related_exs = getattr(guideline, "related_examples", [])
            related_scores = [
                example_scores.get(ex_id) for ex_id in related_exs if example_scores.get(ex_id) is not None
            ]
            if related_scores:
                max_related = max(related_scores)
                if score is None or (max_related is not None and max_related > score):
                    score = max_related
            item = ContextItem(guideline, examples=examples, score=score)
            self.items.append(item)

        # For memories, use their own score, or the max score of their related examples
        for memory in memories.values():
            score = scores.get(getattr(memory, "id", None))
            related_exs = getattr(memory, "related_examples", [])
            related_scores = [
                example_scores.get(ex_id) for ex_id in related_exs if example_scores.get(ex_id) is not None
            ]
            if related_scores:
                max_related = max(related_scores)
                if score is None or (max_related is not None and max_related > score):
                    score = max_related
            item = ContextItem(memory, examples=examples, score=score)
            self.items.append(item)
        self._normalize_scores()
        # Only sort items with a valid normalized_score, put None scores at the end
        self.items.sort(
            key=lambda x: (
                x.normalized_score is not None,
                x.normalized_score if x.normalized_score is not None else float("-inf"),
            ),
            reverse=True,
        )

    def __iter__(self):
        """
        Returns an iterator over the context items.
        """
        for item in self.items:
            yield item

    def __len__(self):
        """
        Returns the number of items in the context.
        """
        return len(self.items)

    def __repr__(self):
        return f"Context(items={len(self.items)}"

    def _normalize_scores(self):
        """
        Normalizes the scores using Z-score normalization, then shifts/scales so the mean is 50.
        """
        scores = [item.score for item in self.items if item.score is not None]
        if not scores:
            return
        mean = sum(scores) / len(scores)
        std = (sum((s - mean) ** 2 for s in scores) / len(scores)) ** 0.5 if len(scores) > 1 else 1.0

        # Z-score normalization, then shift so mean is 50
        for item in self.items:
            if item.score is not None:
                z = (item.score - mean) / std if std > 0 else 0.0
                item.normalized_score = round(z * 10 + 50, 1)  # 1 stddev = 10 points
            else:
                item.normalized_score = None

    def to_markdown(self) -> str:
        """
        Converts the context to a markdown string.
        """
        markdown = ""
        for item in self.items:
            markdown += f"\n{item.to_markdown()}"
        return markdown


class ContextItem:
    """
    Represents a single item in the context.
    """

    def __init__(self, item, *, examples: Dict[str, object], score=None):
        self.id = self._process_id(item.id)
        self.content = getattr(item, "content", getattr(item, "text", None))
        self.language = getattr(item, "language", None)
        self.title = getattr(item, "title", None)
        self.service = getattr(item, "service", None)
        self.is_exception = getattr(item, "is_exception", None)
        self.examples = []
        self.score = score
        for ex_id in getattr(item, "related_examples", []):
            example = copy.deepcopy(examples.get(ex_id)) if examples else None
            if example is not None:
                # use the example's score if it's higher
                if hasattr(example, "score") and example.score and (self.score is None or example.score > self.score):
                    self.score = example.score
                if hasattr(example, "id"):
                    del example.id
                if hasattr(example, "guideline_ids"):
                    del example.guideline_ids
                self.examples.append(example)
            else:
                print(f"WARNING: Example {ex_id} not found for guideline {item.id}. Skipping.")

    def _process_id(self, id: str) -> str:
        """
        Processes the ID to convert the Search-compatible values with web-compatible ones.
        """
        return id.replace("=html=", ".html#")

    def _metadata_markdown(self) -> str:
        """
        Converts the metadata to a markdown string.
        """
        markdown = f"> **id:** {self.id}<br>"
        if self.normalized_score is not None:
            markdown += f"**score:** {int(round(self.normalized_score))}<br>"
        if self.is_exception:
            markdown += f"> **exception:** {self.is_exception}<br>"
        markdown += "\n"
        return markdown

    def to_markdown(self) -> str:
        """
        Converts the context item to a markdown string.
        """

        markdown = self._metadata_markdown()

        markdown += f"## {self.title}\n\n"

        if self.is_exception:
            markdown += f"**THIS IS AN EXCEPTION TO ESTABLISHED GUIDELINES**\n\n"

        markdown += f"{self.content}\n\n"

        if self.examples:
            # collect good and bad examples separately
            good_examples = []
            bad_examples = []
            for example in self.examples:
                if example.example_type == "good":
                    good_examples.append(example)
                else:
                    bad_examples.append(example)

            if good_examples:
                markdown += "### GOOD Examples\n\n"
                for example in good_examples:
                    markdown += f"```python\n{example.content}\n```\n\n"

            if bad_examples:
                markdown += "### BAD Examples\n\n"
                for example in bad_examples:
                    markdown += f"```python\n{example.content}\n```\n\n"
        return markdown


class SearchManager:

    def __init__(self, *, language: str, include_general_guidelines: bool = False):
        self.language = language
        self.filter_expression = f"language eq '{language}'"
        if include_general_guidelines:
            self.filter_expression += " or language eq '' or language eq null"
        self._ensure_env_vars(["AZURE_SEARCH_NAME"])
        self.client = SearchClient(endpoint=SEARCH_ENDPOINT, index_name="archagent-index", credential=CREDENTIAL)
        self.language_guidelines = self._fetch_language_guidelines(language)

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

    def _fetch_language_guidelines(self, language: str) -> SearchResult:
        """
        Fetch all language-specific guidelines (kind='guidelines') from Azure Search,
        excluding those tagged 'vague' or 'documentation'.
        """
        filter_expr = (
            f"kind eq 'guidelines' and language eq '{language}'"
            " and not tags/any(t: t eq 'vague')"
            " and not tags/any(t: t eq 'documentation')"
        )
        return SearchResult(
            self.client.search(search_text="*", filter=filter_expr, query_type=QueryType.SIMPLE, top=1000)
        )

    def _search(self, query: str, *, filter: str, top: int = 20) -> SearchResult:
        """
        Internal method to perform a search on the Azure Search index.
        This method is used by the public search methods to perform the actual search.
        """
        self._ensure_env_vars(["AZURE_SEARCH_NAME"])
        result = self.client.search(
            search_text=query,
            filter=filter,
            semantic_configuration_name="semantic-search-config",
            semantic_error_mode=SemanticErrorMode.FAIL,
            query_type=QueryType.SEMANTIC,
            query_caption=QueryCaptionType.EXTRACTIVE,
            query_answer=QueryAnswerType.EXTRACTIVE,
            top=top,
            vector_queries=[VectorizableTextQuery(text=query, fields="text_vector")],
        )
        return SearchResult(result)

    def search_guidelines(self, query: str, *, top: int = 20) -> SearchResult:
        filter = "kind eq 'guidelines'"
        if self.filter_expression:
            filter = f"({filter}) and ({self.filter_expression})"
        return self._search(query, filter=filter, top=top)

    def search_examples(self, query: str, *, top: int = 20) -> SearchResult:
        filter = "kind eq 'examples'"
        if self.filter_expression:
            filter = f"({filter}) and ({self.filter_expression})"
        return self._search(query, filter=filter, top=top)

    def search_api_view_comments(self, query: str, *, top: int = 20) -> SearchResult:
        filter = "kind eq 'memories'"
        if self.filter_expression:
            filter = f"({filter}) and ({self.filter_expression})"
        return self._search(query, filter=filter, top=top)

    def search_all(self, query: str, *, top: int = 20) -> SearchResult:
        """
        Searches the unified index for the given query and returns the results as a SearchResult object.
        """
        return self._search(query, filter=self.filter_expression, top=top)

    def guidelines_for_ids(self, ids: List[str]) -> List[object]:
        """
        Retrieves the guidelines for the given IDs from CosmosDB.
        """
        ids = list(ids)  # Ensure ids is subscriptable
        self._ensure_env_vars(["AZURE_COSMOS_ACC_NAME", "AZURE_COSMOS_DB_NAME"])
        client = CosmosClient(COSMOS_ENDPOINT, credential=CREDENTIAL)
        database = client.get_database_client(COSMOS_DB_NAME)
        guidelines_container = database.get_container_client("guidelines")
        batch_size = 50
        results = []
        for i in range(0, len(ids), batch_size):
            batch = ids[i : i + batch_size]
            placeholders = ",".join([f"@id{j}" for j in range(len(batch))])
            query = f"SELECT * FROM c WHERE c.id IN ({placeholders})"
            parameters = [{"name": f"@id{j}", "value": value} for j, value in enumerate(batch)]
            items = list(
                guidelines_container.query_items(
                    query=query,
                    parameters=parameters,
                    enable_cross_partition_query=True,
                )
            )
            results.extend([Guideline.model_validate(r) for r in items])
        return results

    def build_context(self, items: List[SearchItem]) -> Context:
        """
        Given a set of items (guidelines, examples, memories), resolve the knowledge graph by traversing
        all related links (related_examples, related_memories, guideline_ids, memory_ids, etc.) using
        breadth-first traversal. Ensures the final context contains all linked guidelines, examples, and memories.
        """
        self._ensure_env_vars(["AZURE_COSMOS_ACC_NAME", "AZURE_COSMOS_DB_NAME"])
        client = CosmosClient(COSMOS_ENDPOINT, credential=CREDENTIAL)
        database = client.get_database_client(COSMOS_DB_NAME)
        guidelines_container = database.get_container_client("guidelines")
        examples_container = database.get_container_client("examples")
        memories_container = database.get_container_client("memories")

        # Partition input items by kind using SearchItem attributes
        guidelines = {item.id: item for item in items.results if item.kind == "guidelines"}
        examples = {item.id: item for item in items.results if item.kind == "examples"}
        memories = {item.id: item for item in items.results if item.kind == "memories"}
        # Save scores for each id
        scores = {item.id: item.score for item in items.results if hasattr(item, "score")}

        # Track seen IDs to avoid cycles
        seen_guideline_ids = set()
        seen_example_ids = set()
        seen_memory_ids = set()

        # Queues for BFT
        guideline_queue = deque(guidelines.keys())
        example_queue = deque(examples.keys())
        memory_queue = deque(memories.keys())

        batch_size = 50

        def batch_query(container, id_list):
            results = []
            for i in range(0, len(id_list), batch_size):
                batch = id_list[i : i + batch_size]
                placeholders = ",".join([f"@id{i}" for i in range(len(batch))])
                query = f"SELECT * FROM c WHERE c.id IN ({placeholders})"
                parameters = [{"name": f"@id{i}", "value": value} for i, value in enumerate(batch)]
                results.extend(
                    list(
                        container.query_items(
                            query=query,
                            parameters=parameters,
                            enable_cross_partition_query=True,
                        )
                    )
                )
            # Determine which model to use based on the container
            if container.container_link.endswith("/guidelines"):
                return [Guideline.model_validate(r) for r in results]
            elif container.container_link.endswith("/examples"):
                return [Example.model_validate(r) for r in results]
            elif container.container_link.endswith("/memories"):
                return [Memory.model_validate(r) for r in results]
            else:
                return results

        # BFT across all three entity types
        while guideline_queue or example_queue or memory_queue:
            # Process guidelines
            if guideline_queue:
                batch_ids = [guideline_queue.popleft() for _ in range(min(batch_size, len(guideline_queue)))]
                new_guidelines = batch_query(guidelines_container, batch_ids)
                for guideline in new_guidelines:
                    gid = guideline.id
                    if gid in seen_guideline_ids:
                        continue
                    seen_guideline_ids.add(gid)
                    guidelines[gid] = guideline

                    # Queue up related examples and memories
                    for ex_id in getattr(guideline, "related_examples", []) or []:
                        if ex_id not in seen_example_ids:
                            example_queue.append(ex_id)
                    for mem_id in getattr(guideline, "related_memories", []) or []:
                        if mem_id not in seen_memory_ids:
                            memory_queue.append(mem_id)

            # Process examples
            if example_queue:
                batch_ids = [example_queue.popleft() for _ in range(min(batch_size, len(example_queue)))]
                new_examples = batch_query(examples_container, batch_ids)
                for example in new_examples:
                    ex_id = example.id
                    if ex_id in seen_example_ids:
                        continue
                    seen_example_ids.add(ex_id)
                    examples[ex_id] = example

                    # Queue up related guidelines and memories
                    for gid in getattr(example, "guideline_ids", []) or []:
                        if gid not in seen_guideline_ids:
                            guideline_queue.append(gid)
                    for mem_id in getattr(example, "memory_ids", []) or []:
                        if mem_id not in seen_memory_ids:
                            memory_queue.append(mem_id)

            # Process memories
            if memory_queue:
                batch_ids = [memory_queue.popleft() for _ in range(min(batch_size, len(memory_queue)))]
                new_memories = batch_query(memories_container, batch_ids)
                for memory in new_memories:
                    mem_id = memory.id
                    if mem_id in seen_memory_ids:
                        continue
                    seen_memory_ids.add(mem_id)
                    memories[mem_id] = memory

                    # Queue up related guidelines and examples
                    for gid in getattr(memory, "related_guidelines", []) or []:
                        if gid not in seen_guideline_ids:
                            guideline_queue.append(gid)
                    for ex_id in getattr(memory, "related_examples", []) or []:
                        if ex_id not in seen_example_ids:
                            example_queue.append(ex_id)

        context = Context(guidelines=guidelines, examples=examples, memories=memories, scores=scores)
        return context
