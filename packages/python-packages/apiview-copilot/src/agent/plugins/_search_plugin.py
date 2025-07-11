import os
from semantic_kernel.functions import kernel_function

from azure.search.documents.indexes import SearchIndexerClient

from src._credential import get_credential
from src._search_manager import SearchManager
from src._database_manager import ContainerNames


class SearchPlugin:

    @kernel_function(
        description="Search for Guidelines in the ArchAgent Knowledge Base by programming language (e.g. python, csharp, etc.)."
    )
    async def search_guidelines(self, query: str, language: str):
        """
        Search for APIView comments in the ArchAgent Knowledge Base.
        Args:
            query (str): The search query.
            language (str): The programming language to filter results.
        """
        search = SearchManager(language=language)
        results = search.search_guidelines(query)
        context = search.build_context(results.results)
        return context.to_markdown()

    @kernel_function(
        description="Search for Examples in the ArchAgent Knowledge Base by programming language (e.g. python, csharp, etc.)."
    )
    async def search_examples(self, query: str, language: str):
        """
        Search for APIView comments in the ArchAgent Knowledge Base.
        Args:
            query (str): The search query.
            language (str): The programming language to filter results.
        """
        search = SearchManager(language=language)
        results = search.search_examples(query)
        context = search.build_context(results.results)
        return context.to_markdown()

    @kernel_function(
        description="Search for APIView comments in the ArchAgent Knowledge Base by programming language (e.g. python, csharp, etc.)."
    )
    async def search_api_view_comments(self, query: str, language: str):
        """
        Search for APIView comments in the ArchAgent Knowledge Base.
        Args:
            query (str): The search query.
            language (str): The programming language to filter results.
        """
        search = SearchManager(language=language)
        results = search.search_api_view_comments(query)
        context = search.build_context(results.results)
        return context.to_markdown()

    @kernel_function(
        description="Search the ArchAgent Knowledge Base for any content by programming language (e.g. python, csharp, etc.)."
    )
    async def search_any(self, query: str, language: str):
        """
        Search for APIView comments in the ArchAgent Knowledge Base.
        Args:
            query (str): The search query.
            language (str): The programming language to filter results.
        """
        search = SearchManager(language=language)
        results = search.search_all(query)
        context = search.build_context(results.results)
        return context.to_markdown()

    @kernel_function(
        description="Trigger a reindex of a specific Azure Search indexer for the ArchAgent Knowledge Base."
    )
    async def run_indexer(self, container_name: str):
        f"""
        Trigger a reindex of the Azure Search index for the ArchAgent Knowledge Base.
        Args:
            container_name (str): The name of the container to reindex. The only valid container names are: {', '.join([c.value for c in ContainerNames])}
        """
        if container_name not in [c.value for c in ContainerNames] or container_name == "review-jobs":
            return
        indexer_name = f"{container_name}-indexer"
        search_service = os.environ.get("AZURE_SEARCH_NAME")
        if not search_service:
            raise RuntimeError("AZURE_SEARCH_NAME not set in environment.")
        endpoint = f"https://{search_service}.search.windows.net"
        client = SearchIndexerClient(endpoint=endpoint, credential=get_credential())
        status = client.get_indexer_status(indexer_name)
        if status.status in ["inProgress"]:
            return {
                "status": "inProgress",
                "message": f"Indexer '{indexer_name}' is already running. Skipping invocation.",
            }
        client.run_indexer(indexer_name)
        return {
            "status": "inProgress",
            "message": f"Indexer '{indexer_name}' is now running.",
        }

    @kernel_function(description="Trigger a reindex of all Azure Search indexers for the ArchAgent Knowledge Base.")
    async def run_all_indexers(self):
        """
        Trigger a reindex of all Azure Search indexers for the ArchAgent Knowledge Base.
        """
        results = {}
        for container_name in [c.value for c in ContainerNames]:
            if container_name == "review-jobs":
                continue
            results[container_name] = await self.run_indexer(container_name)
        return results
