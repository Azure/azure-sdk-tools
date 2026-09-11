from __future__ import annotations

from azure.core import MatchConditions
from azure.core.credentials import TokenCredential
from azure.core.exceptions import ResourceNotFoundError
from azure.search.documents.indexes import SearchIndexClient
from azure.search.documents.indexes.models import (
    AzureOpenAIVectorizer,
    AzureOpenAIVectorizerParameters,
    HnswAlgorithmConfiguration,
    SearchAlias,
    SearchField,
    SearchIndex,
    SearchIndexerDataUserAssignedIdentity,
    SearchableField,
    SemanticConfiguration,
    SemanticField,
    SemanticPrioritizedFields,
    SemanticSearch,
    SimpleField,
    VectorSearch,
    VectorSearchProfile,
)

from .models import BuilderSettings

VECTOR_ALGORITHM = "code-hnsw"
VECTOR_PROFILE = "code-vector-profile"
VECTORIZER = "code-azure-openai"
SEMANTIC_CONFIGURATION = "code-semantic"


def ensure_index(settings: BuilderSettings, credential: TokenCredential) -> None:
    client = SearchIndexClient(settings.search_endpoint, credential)
    try:
        client.create_or_update_index(_index_definition(settings))
        alias = SearchAlias(
            name=settings.search_alias,
            indexes=[settings.search_index_name],
        )
        try:
            existing = client.get_alias(settings.search_alias)
        except ResourceNotFoundError:
            client.create_or_update_alias(alias)
            return
        if existing.indexes == [settings.search_index_name]:
            return
        alias.e_tag = existing.e_tag
        client.create_or_update_alias(
            alias, match_condition=MatchConditions.IfNotModified
        )
    finally:
        client.close()


def _index_definition(settings: BuilderSettings) -> SearchIndex:
    identity = (
        SearchIndexerDataUserAssignedIdentity(
            resource_id=settings.search_user_assigned_identity_resource_id
        )
        if settings.search_user_assigned_identity_resource_id
        else None
    )
    fields = [
        SimpleField(
            name="chunk_id",
            type="Edm.String",
            key=True,
            filterable=True,
        ),
        SimpleField(
            name="git_url",
            type="Edm.String",
            filterable=True,
            facetable=True,
        ),
        SimpleField(
            name="git_ref",
            type="Edm.String",
            filterable=True,
            facetable=True,
        ),
        SimpleField(
            name="valid_from_generation",
            type="Edm.Int64",
            filterable=True,
            sortable=True,
        ),
        SimpleField(
            name="valid_to_generation",
            type="Edm.Int64",
            filterable=True,
            sortable=True,
        ),
        SearchableField(name="path", filterable=True),
        SearchField(
            name="path_prefixes",
            type="Collection(Edm.String)",
            filterable=True,
        ),
        SimpleField(
            name="language",
            type="Edm.String",
            filterable=True,
            facetable=True,
        ),
        SimpleField(
            name="artifact_type",
            type="Edm.String",
            filterable=True,
            facetable=True,
        ),
        SearchableField(name="content"),
        SearchField(
            name="content_vector",
            type="Collection(Edm.Single)",
            searchable=True,
            vector_search_dimensions=settings.embedding.dimensions,
            vector_search_profile_name=VECTOR_PROFILE,
        ),
        SimpleField(
            name="content_hash",
            type="Edm.String",
            filterable=True,
        ),
        SimpleField(
            name="start_line",
            type="Edm.Int32",
            filterable=True,
        ),
        SimpleField(
            name="end_line",
            type="Edm.Int32",
            filterable=True,
        ),
        SearchableField(name="symbol_name", filterable=True),
        SearchableField(name="symbol_kind", filterable=True, facetable=True),
        SearchField(
            name="identifiers",
            type="Collection(Edm.String)",
            searchable=True,
            filterable=True,
        ),
        SimpleField(
            name="parser",
            type="Edm.String",
            filterable=True,
            facetable=True,
        ),
    ]
    return SearchIndex(
        name=settings.search_index_name,
        fields=fields,
        vector_search=VectorSearch(
            algorithms=[HnswAlgorithmConfiguration(name=VECTOR_ALGORITHM)],
            profiles=[
                VectorSearchProfile(
                    name=VECTOR_PROFILE,
                    algorithm_configuration_name=VECTOR_ALGORITHM,
                    vectorizer_name=VECTORIZER,
                )
            ],
            vectorizers=[
                AzureOpenAIVectorizer(
                    vectorizer_name=VECTORIZER,
                    parameters=AzureOpenAIVectorizerParameters(
                        resource_url=settings.embedding.endpoint,
                        deployment_name=settings.embedding.deployment,
                        model_name=settings.embedding.model,
                        auth_identity=identity,
                    ),
                )
            ],
        ),
        semantic_search=SemanticSearch(
            default_configuration_name=SEMANTIC_CONFIGURATION,
            configurations=[
                SemanticConfiguration(
                    name=SEMANTIC_CONFIGURATION,
                    prioritized_fields=SemanticPrioritizedFields(
                        title_field=SemanticField(field_name="path"),
                        content_fields=[SemanticField(field_name="content")],
                        keywords_fields=[
                            SemanticField(field_name="symbol_name"),
                            SemanticField(field_name="symbol_kind"),
                            SemanticField(field_name="identifiers"),
                        ],
                    ),
                )
            ],
        ),
    )
