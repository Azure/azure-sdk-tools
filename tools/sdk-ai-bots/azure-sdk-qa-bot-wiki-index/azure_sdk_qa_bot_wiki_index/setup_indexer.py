"""Set up the dedicated Azure AI Search datasource, skillset, and indexer."""

from __future__ import annotations

import logging
import os

import requests
from azure.identity import DefaultAzureCredential

logger = logging.getLogger(__name__)

API_VERSION = "2024-07-01"
DATASOURCE = "azure-sdk-knowledge-wiki-datasource"
SKILLSET = "azure-sdk-knowledge-wiki-skillset"
INDEXER = "azure-sdk-knowledge-wiki-indexer"

_DEFAULTS = {
    "AI_SEARCH_BASE_URL": "https://azuresdkqabot-dev-search.search.windows.net",
    "AI_SEARCH_INDEX": "azure-sdk-knowledge",
    "STORAGE_ACCOUNT_RESOURCE_ID": (
        "/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/"
        "azure-sdk-qa-bot-dev/providers/Microsoft.Storage/storageAccounts/"
        "azuresdkqabotdevstorage"
    ),
    "STORAGE_WIKI_OUTPUT_CONTAINER": "wiki",
    "AZURE_OPENAI_ENDPOINT": "https://azuresdkqabot-dev-ai-resource.openai.azure.com",
    "WIKI_EMBEDDING_DEPLOYMENT": "text-embedding-ada-002",
    "SEARCH_USER_ASSIGNED_IDENTITY_RESOURCE_ID": (
        "/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourcegroups/"
        "azure-sdk-qa-bot-dev/providers/Microsoft.ManagedIdentity/"
        "userAssignedIdentities/azuresdkqabot-dev-identity"
    ),
}


def _env(name: str) -> str:
    return os.environ.get(name, _DEFAULTS[name])


def _ua_identity() -> dict:
    return {
        "@odata.type": "#Microsoft.Azure.Search.DataUserAssignedIdentity",
        "userAssignedIdentity": _env("SEARCH_USER_ASSIGNED_IDENTITY_RESOURCE_ID"),
    }


def _put(base: str, token: str, kind: str, name: str, body: dict) -> None:
    url = f"{base}/{kind}/{name}?api-version={API_VERSION}"
    resp = requests.put(
        url,
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"},
        json=body,
        timeout=60,
    )
    resp.raise_for_status()
    logger.info("%s %r upserted", kind, name)


def datasource_body() -> dict:
    return {
        "name": DATASOURCE,
        "type": "azureblob",
        "credentials": {"connectionString": f"ResourceId={_env('STORAGE_ACCOUNT_RESOURCE_ID')};"},
        "container": {"name": _env("STORAGE_WIKI_OUTPUT_CONTAINER")},
        "identity": _ua_identity(),
        "dataDeletionDetectionPolicy": {
            "@odata.type": "#Microsoft.Azure.Search.SoftDeleteColumnDeletionDetectionPolicy",
            "softDeleteColumnName": "is_deleted",
            "softDeleteMarkerValue": "true",
        },
    }


def skillset_body() -> dict:
    index = _env("AI_SEARCH_INDEX")
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
                "maximumPageLength": 2000,
                "pageOverlapLength": 500,
                "inputs": [{"name": "text", "source": "/document/content"}],
                "outputs": [{"name": "textItems", "targetName": "pages"}],
            },
            {
                "@odata.type": "#Microsoft.Skills.Text.AzureOpenAIEmbeddingSkill",
                "name": "#2",
                "context": "/document/pages/*",
                "resourceUri": _env("AZURE_OPENAI_ENDPOINT"),
                "deploymentId": _env("WIKI_EMBEDDING_DEPLOYMENT"),
                "dimensions": 1536,
                "modelName": _env("WIKI_EMBEDDING_DEPLOYMENT"),
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
        "targetIndexName": _env("AI_SEARCH_INDEX"),
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
    base = _env("AI_SEARCH_BASE_URL").rstrip("/")
    token = DefaultAzureCredential().get_token("https://search.azure.com/.default").token
    _put(base, token, "datasources", DATASOURCE, datasource_body())
    _put(base, token, "skillsets", SKILLSET, skillset_body())
    _put(base, token, "indexers", INDEXER, indexer_body())
    logger.info("wiki indexer resources are set up; it runs daily and on creation.")


if __name__ == "__main__":
    main()
