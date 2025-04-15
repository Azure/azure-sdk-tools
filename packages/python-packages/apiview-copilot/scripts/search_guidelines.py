import sys
import os
import json

# Add project root to sys.path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from src._search_manager import SearchManager
from cli import CustomJSONEncoder


def search_guidelines(path: str, language: str):
    """
    Search the guidelines-index for a query.

    Args:
        path (str): Path to the file containing the query, or a text query.
        language (str): Programming language of the query.
    Returns:
        list: List of search results.
    """
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8") as file:
            query = file.read()
    else:
        query = path

    search = SearchManager(language=language)
    results = search.search_guidelines(query)
    return results


if __name__ == "__main__":
    # extract the path and language from the command line arguments
    if len(sys.argv) < 3:
        print("Usage: python search_guidelines.py <path_or_query> <language>")
        sys.exit(1)
    path = sys.argv[1]
    language = sys.argv[2]
    results = search_guidelines(path, language)
    print(json.dumps(results, indent=2, cls=CustomJSONEncoder))
