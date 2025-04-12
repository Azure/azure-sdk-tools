import sys
import os
import json
from src._search_manager import SearchManager


def search_examples(path: str, language: str):
    """
    Search the examples-index for a query.

    Args:
        path (str): Path to the file containing the query code.
        language (str): Programming language of the query code.
    Returns:
        list: List of search results.
    """
    # Load the query
    with open(path, "r", encoding="utf-8") as file:
        query = file.read()

    search = SearchManager(language=language)
    return search.search_examples(query)


if __name__ == "__main__":
    # extract the path and language from the command line arguments
    if len(sys.argv) < 3:
        print("Usage: python search_examples.py <path> <language>")
        sys.exit(1)
    path = sys.argv[1]
    language = sys.argv[2]
    if not os.path.exists(path):
        print(f"File not found: {path}")
        sys.exit(1)

    results = search_examples(path, language)
    print(json.dumps(results, indent=2))
