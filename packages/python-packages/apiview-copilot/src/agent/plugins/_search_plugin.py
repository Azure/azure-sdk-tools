from semantic_kernel.functions import kernel_function
from src._search_manager import SearchManager


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
        context = search.build_context(results)
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
        context = search.build_context(results)
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
        context = search.build_context(results)
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
        context = search.build_context(results)
        return context.to_markdown()
