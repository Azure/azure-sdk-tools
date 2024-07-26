import os
from dotenv import load_dotenv

load_dotenv()

INCREMENTAL_EMBEDDING = os.environ.get("INCREMENTAL_EMBEDDING", "False").lower() == "true"

METADATA_PATH = os.environ.get("METADATA_PATH", "./data/metadata.json")
DOCUMENT_PATH = os.environ.get("DOCUMENT_PATH", "./data/documents") # documents folder
CHUNK_PATH = os.environ.get("CHUNK_PATH")
RAG_CHUNK_PATH = os.environ.get("RAG_CHUNK_PATH", "./data/rag_chunks.json")
HEADING_LEVEL = int(os.environ.get("HEADING_LEVEL", "4"))
LARGER_CHUNK_SIZE = int(os.environ.get("LARGER_CHUNK_SIZE", "1000"))
SMALLER_CHUNK_SIZE = int(os.environ.get("SMALLER_CHUNK_SIZE", "200"))
OVERLAP_SIZE = int(os.environ.get("OVERLAP_SIZE", "10"))

AZURE_OPENAI_KEY = os.environ.get("AZURE_OPENAI_API_KEY")
AZURE_OPENAI_ENDPOINT = os.environ.get("AZURE_OPENAI_ENDPOINT")
AZURE_OPENAI_VERSION = os.environ.get("AZURE_OPENAI_VERSION", "2023-05-15")
AZURE_OPENAI_EMBEDDING_MODEL = os.environ.get("AZURE_OPENAI_EMBEDDING_MODEL", "text-embedding-ada-002")

AZURE_SEARCH_KEY = os.environ.get("AZURE_SEARCH_KEY")
AZURE_SEARCH_ENDPOINT = os.environ.get("AZURE_SEARCH_ENDPOINT")
AZURE_SEARCH_INDEX_NAME = os.environ.get("AZURE_SEARCH_INDEX_NAME")