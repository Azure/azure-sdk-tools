from langchain.embeddings import OpenAIEmbeddings
from langchain.vectorstores.azuresearch import AzureSearch
from azure.search.documents.indexes.models import (
    SearchField,
    SearchFieldDataType,
    SimpleField,
    HnswAlgorithmConfiguration,
    VectorSearch
)
from azure.core.credentials import AzureKeyCredential
from azure.search.documents import SearchClient
import base64
import json
import os

from settings.settings import *
from splitting.chunk import RagChunk
from splitting.utils import rag_chunk_to_json

class Embedding:
    def __init__(self):

        embeddings: OpenAIEmbeddings = OpenAIEmbeddings(
            openai_api_type="azure",
            openai_api_base=AZURE_OPENAI_ENDPOINT,
            openai_api_key=AZURE_OPENAI_KEY,
            openai_api_version=AZURE_OPENAI_VERSION,
            deployment=AZURE_OPENAI_EMBEDDING_MODEL, 
            chunk_size=1)
        embedding_function = embeddings.embed_query
        fields = [
            SimpleField(
                name="Id",
                type=SearchFieldDataType.String,
                key=True,
            ),
            SearchField(
                name="Embedding",
                type=SearchFieldDataType.Collection(SearchFieldDataType.Single),
                searchable=True,
                vector_search_dimensions=len(embedding_function("Text")),
                vector_search_profile_name="myHnswProfile"
            ),
            SearchField(
                name="Text",
                type=SearchFieldDataType.String,
                filterable=True,
                facetable=True,
            ),
            SimpleField(
                name="Description",
                type=SearchFieldDataType.String,
                filterable=True,
                facetable=True,
            ),
            SimpleField(
                name="AdditionalMetadata",
                type=SearchFieldDataType.String,
                filterable=True,
                facetable=True,
            ),
            SimpleField(
                name="ExternalSourceName",
                type=SearchFieldDataType.String,
                filterable=True,
                facetable=True,
            ),
            SimpleField(
                name="IsReference",
                type=SearchFieldDataType.Boolean,
                filterable=True,
                facetable=True,
            )
        ]
        algorithm_configuration = HnswAlgorithmConfiguration(
            name="searchAlgorithm",
            kind="hnsw",
            parameters={
                "metric": "cosine",
                }
        )
        search_profile = {
            "name": "myHnswProfile",
            "algorithm_configuration_name": "searchAlgorithm",
        }
        vector_search: VectorSearch = VectorSearch(
            algorithms=[algorithm_configuration],
            profiles=[search_profile]
        )
        azure_search: AzureSearch = AzureSearch(
            azure_search_endpoint=AZURE_SEARCH_ENDPOINT,
            azure_search_key=AZURE_SEARCH_KEY,
            index_name=AZURE_SEARCH_INDEX_NAME,
            embedding_function=embedding_function,
            fields=fields,
            vector_search=vector_search
        )
        azure_search_client: SearchClient = SearchClient(
            endpoint=AZURE_SEARCH_ENDPOINT,
            index_name=AZURE_SEARCH_INDEX_NAME,
            credential=AzureKeyCredential(AZURE_SEARCH_KEY)
        )

        self.index_name = AZURE_SEARCH_INDEX_NAME
        self.azure_search = azure_search
        self.azure_search_client = azure_search_client

    def _encode(cls, id: str):
        return base64.b64encode(id.encode("ascii")).decode()

    def add_rag_chunks(self, rag_chunks: list[RagChunk]):
        texts = [rag_chunk.rag_text for rag_chunk in rag_chunks]
        metadatas = [{
            "Id": self._encode(rag_chunk.id),
            "Text": rag_chunk.rag_text,
            "Description": "",
            "AdditionalMetadata": json.dumps(rag_chunk_to_json(rag_chunk)),
            "ExternalSourceName": "",
            "IsReference": False
        } for rag_chunk in rag_chunks]
        print("Adding {} rag chunks to index {}...".format(len(texts), self.index_name))
        self.azure_search.add_texts(texts, metadatas)

    def add_incremental_rag_chunks(self, rag_chunks_to_delete: set[str], rag_chunks: list[RagChunk]):
        documents_to_delete = [{"Id": self._encode(rag_chunk_id)} for rag_chunk_id in rag_chunks_to_delete]
        print("Deleting {} rag chunks from index {}...".format(len(documents_to_delete), self.index_name))
        self.azure_search_client.delete_documents(documents=documents_to_delete)

        self.add_rag_chunks(rag_chunks)