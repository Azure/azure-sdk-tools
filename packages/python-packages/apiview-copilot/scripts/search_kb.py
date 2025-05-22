import json
import os
import sys
from typing import Optional, List

# Add project root to sys.path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from src._search_manager import SearchManager
from cli import CustomJSONEncoder


def search_knowledge_base(
    query: str,
    language: str,
):
    """
    Queries the Search indexes and returns the resulting Cosmos DB
    objects, resolving all links between objects. This result represents
    what the AI reviewer would receive as context in RAG mode.
    """
    search = SearchManager(language=language)
    if path:
        with open(path, "r") as f:
            query = f.read()
    results = search.search_all(query=query)
    context = search.build_context(results)
    return context


if __name__ == "__main__":
    # extract the path and language from the command line arguments
    if len(sys.argv) < 3:
        print("Usage: python search_kb.py <path_or_query> <language> [<use_markdown>]")
        sys.exit(1)
    path = sys.argv[1]
    language = sys.argv[2]
    use_markdown = bool(sys.argv[3]) if len(sys.argv) > 3 else None
    # check if path is a file or string
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8") as file:
            query = file.read()
    else:
        query = path
    results = search_knowledge_base(query, language)
    if use_markdown:
        print(results.to_markdown())
    else:
        print(json.dumps(results, indent=2, cls=CustomJSONEncoder))
