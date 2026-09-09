"""Set up the dedicated Azure AI Search datasource, skillset, and indexer."""

from __future__ import annotations

import logging

import requests
from azure.identity import DefaultAzureCredential

from .config import get as cfg, load_sync as load_config, require

logger = logging.getLogger(__name__)

API_VERSION = "2024-11-01-preview"  # user-assigned identity fields need a preview version
DATASOURCE = "azure-sdk-knowledge-wiki-datasource"
SKILLSET = "azure-sdk-knowledge-wiki-skillset"
INDEXER = "azure-sdk-knowledge-wiki-indexer"

# The shared index was created with ada-002 vectors, so the wiki skillset must
# embed with the same model regardless of what the build pipeline uses.
EMBEDDING_DEPLOYMENT = "text-embedding-ada-002"


def _ua_identity() -> dict:
    return {
        "@odata.type": "#Microsoft.Azure.Search.DataUserAssignedIdentity",
        "userAssignedIdentity": require("SEARCH_USER_ASSIGNED_IDENTITY_RESOURCE_ID"),
    }


def _put(base: str, token: str, kind: str, name: str, body: dict) -> None:
    url = f"{base}/{kind}/{name}?api-version={API_VERSION}"
    resp = requests.put(
        url,
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"},
        json=body,
        timeout=60,
    )
    if not resp.ok:
        # The status alone says nothing about which property was rejected.
        raise RuntimeError(f"{kind} {name!r} failed ({resp.status_code}): {resp.text}")
    logger.info("%s %r upserted", kind, name)


def datasource_body() -> dict:
    return {
        "name": DATASOURCE,
        "type": "azureblob",
        "credentials": {"connectionString": f"ResourceId={require('STORAGE_ACCOUNT_RESOURCE_ID')};"},
        "container": {"name": cfg("STORAGE_WIKI_OUTPUT_CONTAINER", "wiki")},
        "identity": _ua_identity(),
        "dataDeletionDetectionPolicy": {
            "@odata.type": "#Microsoft.Azure.Search.SoftDeleteColumnDeletionDetectionPolicy",
            "softDeleteColumnName": "IsDeleted",
            "softDeleteMarkerValue": "true",
        },
    }


def skillset_body() -> dict:
    index = cfg("AI_SEARCH_INDEX", "azure-sdk-knowledge")
    return {
        "name": SKILLSET,
        "description": "Chunk + embed LLM wiki pages; project into the shared KB index with page_type.",
        "skills": [
            {
                "@odata.type": "#Microsoft.Skills.Text.SplitSkill",
                "name": "#1",
                "context": "/document",
                "defaultLanguageCode": "en",
                "textSplitMode": "pages",
                # Search matches focused overlapping chunks, then wiki reads
                # expand the selected hit to the complete page.
                "maximumPageLength": 2000,
                "pageOverlapLength": 500,
                "inputs": [{"name": "text", "source": "/document/content"}],
                "outputs": [{"name": "textItems", "targetName": "pages"}],
            },
            {
                "@odata.type": "#Microsoft.Skills.Text.AzureOpenAIEmbeddingSkill",
                "name": "#2",
                "context": "/document/pages/*",
                "resourceUri": require("AZURE_OPENAI_ENDPOINT"),
                "deploymentId": EMBEDDING_DEPLOYMENT,
                "dimensions": 1536,
                "modelName": EMBEDDING_DEPLOYMENT,
                "authIdentity": _ua_identity(),
                "inputs": [{"name": "text", "source": "/document/pages/*"}],
                "outputs": [{"name": "embedding", "targetName": "text_vector"}],
            },
        ],
        "indexProjections": {
            "selectors": [
                {
                    "targetIndexName": index,
                    "parentKeyFieldName": "parent_id",
                    "sourceContext": "/document/pages/*",
                    "mappings": [
                        {"name": "text_vector", "source": "/document/pages/*/text_vector"},
                        {"name": "chunk", "source": "/document/pages/*"},
                        {"name": "title", "source": "/document/title"},
                        {"name": "context_id", "source": "/document/context_id"},
                        {"name": "header_1", "source": "/document/title"},
                        {"name": "page_type", "source": "/document/page_type"},
                        {"name": "chunk_refs_str", "source": "/document/chunk_refs"},
                    ],
                }
            ],
            "parameters": {"projectionMode": "skipIndexingParentDocuments"},
        },
    }


def indexer_body() -> dict:
    return {
        "name": INDEXER,
        "dataSourceName": DATASOURCE,
        "skillsetName": SKILLSET,
        "targetIndexName": cfg("AI_SEARCH_INDEX", "azure-sdk-knowledge"),
        "schedule": {"interval": "P1D"},
        "parameters": {
            "configuration": {
                "dataToExtract": "contentAndMetadata",
                "indexedFileNameExtensions": ".md",
            }
        },
        "fieldMappings": [
            {"sourceFieldName": "title", "targetFieldName": "title"},
            {"sourceFieldName": "context_id", "targetFieldName": "context_id"},
            {"sourceFieldName": "page_type", "targetFieldName": "page_type"},
        ],
        "outputFieldMappings": [],
    }


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    credential = DefaultAzureCredential()
    load_config(credential)
    base = require("AI_SEARCH_BASE_URL").rstrip("/")
    token = credential.get_token("https://search.azure.com/.default").token
    _put(base, token, "datasources", DATASOURCE, datasource_body())
    _put(base, token, "skillsets", SKILLSET, skillset_body())
    _put(base, token, "indexers", INDEXER, indexer_body())
    logger.info("wiki indexer resources are set up; it runs daily and on creation.")


if __name__ == "__main__":
    main()
