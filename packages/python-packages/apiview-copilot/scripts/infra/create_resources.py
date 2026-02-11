# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=too-many-lines, redefined-outer-name, reimported
import os
import re
import sys
from typing import List, Literal, Optional
from uuid import uuid4

try:
    from _variables import Variables
except ImportError:
    # for CI or when run as a module
    from scripts.infra._variables import Variables

from azure.appconfiguration import (
    AzureAppConfigurationClient,
    ConfigurationSetting,
    SecretReferenceConfigurationSetting,
)
from azure.core.exceptions import ResourceExistsError, ResourceNotFoundError
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from azure.mgmt.appconfiguration import AppConfigurationManagementClient
from azure.mgmt.appconfiguration.models import (
    ConfigurationStore,
    DataPlaneProxyProperties,
)
from azure.mgmt.appconfiguration.models import Sku as AppConfigurationSku
from azure.mgmt.authorization import AuthorizationManagementClient
from azure.mgmt.authorization.models import (
    PrincipalType,
    RoleAssignmentCreateParameters,
)
from azure.mgmt.cognitiveservices import CognitiveServicesManagementClient
from azure.mgmt.cognitiveservices.models import (
    Account,
    AccountProperties,
    Deployment,
    DeploymentModel,
)
from azure.mgmt.cognitiveservices.models import Sku as CognitiveSku
from azure.mgmt.cognitiveservices.models import Sku as ResourceSku
from azure.mgmt.cosmosdb import CosmosDBManagementClient
from azure.mgmt.cosmosdb.models import (
    Capability,
    ConsistencyPolicy,
    ContainerPartitionKey,
    DatabaseAccountCreateUpdateParameters,
    Location,
    SqlContainerCreateUpdateParameters,
    SqlContainerResource,
    SqlDatabaseCreateUpdateParameters,
    SqlDatabaseResource,
    SqlRoleAssignmentCreateUpdateParameters,
)
from azure.mgmt.keyvault import KeyVaultManagementClient
from azure.mgmt.keyvault.models import AccessPolicyEntry, Permissions
from azure.mgmt.keyvault.models import Sku as KeyVaultSku
from azure.mgmt.keyvault.models import Vault, VaultProperties
from azure.mgmt.resource import ResourceManagementClient
from azure.mgmt.resource.resources.models import ResourceGroup
from azure.mgmt.search import SearchManagementClient
from azure.mgmt.search.models import SearchService
from azure.mgmt.search.models import Sku as SearchSku
from azure.mgmt.web import WebSiteManagementClient
from azure.mgmt.web.models import AppServicePlan, Site, SiteConfig, SkuDescription
from azure.search.documents.indexes import SearchIndexClient, SearchIndexerClient
from azure.search.documents.indexes.models import (
    AzureOpenAIEmbeddingSkill,
    AzureOpenAIVectorizer,
    AzureOpenAIVectorizerParameters,
    DefaultCognitiveServicesAccount,
    FieldMapping,
    FieldMappingFunction,
    HnswAlgorithmConfiguration,
    HnswParameters,
    IndexProjectionMode,
    InputFieldMappingEntry,
    LexicalAnalyzerName,
    OutputFieldMappingEntry,
    SearchField,
)
from azure.search.documents.indexes.models import SearchFieldDataType as SFDT
from azure.search.documents.indexes.models import (
    SearchIndex,
    SearchIndexer,
    SearchIndexerDataContainer,
    SearchIndexerDataSourceConnection,
    SearchIndexerDataSourceType,
    SearchIndexerIndexProjection,
    SearchIndexerIndexProjectionSelector,
    SearchIndexerIndexProjectionsParameters,
    SearchIndexerSkillset,
    SemanticConfiguration,
    SemanticField,
    SemanticPrioritizedFields,
    SemanticSearch,
    SplitSkill,
    TextSplitMode,
    VectorSearch,
    VectorSearchProfile,
)

# pyright: reportCallIssue=false
# pyright: reportArgumentType=false
# pyright: reportReturnType=false
# pyright: reportOptionalSubscript=false

credential = DefaultAzureCredential(exclude_visual_studio_code_credential=True)


def check_credential():
    """Verify the Azure credential is valid."""
    try:
        credential.get_token("https://management.azure.com/.default")
    except Exception:
        print("Unable to get token. Please run `az login` and try again.")
        sys.exit(1)


def create_resource_group(v: Variables):
    """Create the resource group or reuse the existing one."""
    try:
        client = ResourceManagementClient(credential, v.subscription_id)
        exists = client.resource_groups.check_existence(v.rg_name)
        if exists:
            print(f"✅ Using existing resource group: {v.rg_name}")
            return
        else:
            print(f"Creating resource group {v.rg_name}...")
            client.resource_groups.create_or_update(
                v.rg_name,
                ResourceGroup(
                    location=v.rg_location,
                    tags={"DoNotDelete": "true", "Owner": "mariari"},
                ),
            )
            print(f"✅ Created resource group: {v.rg_name}")
    except Exception as e:
        print(f"❌ An error occurred: {e}")
        sys.exit(1)


def create_cosmosdb_account(v: Variables):
    """Create the Azure CosmosDB account and assign the necessary roles."""
    try:
        client = CosmosDBManagementClient(credential, v.subscription_id)
        try:
            client.database_accounts.get(v.rg_name, v.cosmos_account_name)
            print(f"✅ Using existing CosmosDB account: {v.cosmos_account_name}")
        except ResourceNotFoundError:
            # Create the resource
            consistency_policy = ConsistencyPolicy(
                default_consistency_level="BoundedStaleness",
                max_interval_in_seconds=300,
                max_staleness_prefix=100000,
            )
            account_params = DatabaseAccountCreateUpdateParameters(
                location=v.rg_location,
                locations=[Location(location_name=v.rg_location)],
                kind="GlobalDocumentDB",
                database_account_offer_type="Standard",
                capabilities=[Capability(name="EnableServerless")],
                consistency_policy=consistency_policy,
                disable_local_auth=True,  # Ensures compliance with security policy
            )
            print(f"\nCreating CosmosDB account {v.cosmos_account_name}...")
            client.database_accounts.begin_create_or_update(v.rg_name, v.cosmos_account_name, account_params).result()
            print(f"✅ Created CosmosDB account: {v.cosmos_account_name}")
    except Exception as e:
        print(f"❌ An error occurred: {e}")
        sys.exit(1)


def assign_cosmosdb_permissions(v: Variables, principal_id: str, principal_type: PrincipalType):
    """Assigns the necessary control and data plane roles to the logged-in user."""
    # assign CosmosDB management and data plane roles to myself
    assign_rbac_roles(
        v,
        roles=["Cosmos DB Operator"],
        principal_id=principal_id,
        principal_type=principal_type,
    )
    _assign_cosmosdb_builtin_roles(v, kind="readWrite", principal_id=principal_id)


def assign_rbac_roles(
    v: Variables, *, roles: List[str], principal_id: str, principal_type: PrincipalType, scope: Optional[str] = None
):
    """Assigns arbitrary RBAC roles to the logged-in user."""
    client = AuthorizationManagementClient(credential, v.subscription_id)
    for role_name in roles:
        try:
            role_definitions = list(
                client.role_definitions.list(
                    f"/subscriptions/{v.subscription_id}",
                    filter=f"roleName eq '{role_name}'",
                )
            )

            if not role_definitions:
                raise KeyError(f"Role '{role_name}' not found!")

            role_definition_id = role_definitions[0].id
            role_scope = scope or f"/subscriptions/{v.subscription_id}/resourceGroups/{v.rg_name}"

            # Assign role
            assignment_id = str(uuid4())
            params = RoleAssignmentCreateParameters(
                principal_id=principal_id,
                role_definition_id=role_definition_id,
                principal_type=principal_type,
            )
            client.role_assignments.create(scope=role_scope, role_assignment_name=assignment_id, parameters=params)
            print(f"✅ Assigned '{role_name}' to {principal_id}.")
        except ResourceExistsError:
            print(f"✅ RBAC role '{role_name}' already assigned to {principal_id}.")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def _assign_cosmosdb_builtin_roles(v: Variables, *, kind: Literal["readOnly", "readWrite"], principal_id: str):
    """Assigns special built-in roles for Cosmos DB."""
    cosmos_mgmt_client = CosmosDBManagementClient(credential, v.subscription_id)
    cosmos_account = cosmos_mgmt_client.database_accounts.get(v.rg_name, v.cosmos_account_name)
    role_name = None
    role_id = None
    # Assign well-known Role ID for "Cosmos DB Built-in Data Contributor"
    if kind == "readWrite":
        role_id = f"{cosmos_account.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
        role_name = "Data Contributor (built-in)"
    elif kind == "readOnly":
        role_id = f"{cosmos_account.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000001"
        role_name = "Data Reader (built-in)"
    if not role_name or not role_id:
        return
    try:
        results = list(cosmos_mgmt_client.sql_resources.list_sql_role_assignments(v.rg_name, v.cosmos_account_name))
        for result in results:
            if result.role_definition_id == role_id and result.principal_id == principal_id:
                print(f"✅ Cosmos role '{role_name}' role already assigned to {principal_id}.")
                return
        # Otherwise we must assign the role
        role_assignment_params = SqlRoleAssignmentCreateUpdateParameters(
            role_definition_id=role_id,
            principal_id=principal_id,
            scope=cosmos_account.id,  # Assign at account level
        )
        role_assignment_id = str(uuid4())
        cosmos_mgmt_client.sql_resources.begin_create_update_sql_role_assignment(
            role_assignment_id,
            v.rg_name,
            v.cosmos_account_name,
            role_assignment_params,
        ).result()
        print(f"✅ Assigned Cosmos role '{role_name}' role to {principal_id}.")
    except ResourceExistsError:
        print(f"✅ 'Cosmos role {role_name}' role already assigned to {principal_id}.")
    except Exception as e:
        print(f"❌ An error occurred: {e}")
        sys.exit(1)


def create_cosmosdb_database(v: Variables):
    """Create the CosmosDB database."""
    try:
        credential.get_token("https://cosmos.azure.com/.default")
        client = CosmosDBManagementClient(credential, v.subscription_id)
        try:
            client.sql_resources.get_sql_database(v.rg_name, v.cosmos_account_name, v.cosmos_db_name)
            print(f"✅ Using existing CosmosDB database: {v.cosmos_db_name}")
        except ResourceNotFoundError:
            print(f"\nCreating CosmosDB database {v.cosmos_db_name}...")
            client.sql_resources.begin_create_update_sql_database(
                v.rg_name,
                v.cosmos_account_name,
                v.cosmos_db_name,
                SqlDatabaseCreateUpdateParameters(resource=SqlDatabaseResource(id=v.cosmos_db_name)),
            ).result()
            print(f"✅ Created CosmosDB database: {v.cosmos_db_name}")
    except Exception as e:
        print(f"❌ An error occurred: {e}")
        sys.exit(1)


def create_cosmosdb_containers(v: Variables):
    """Create the CosmosDB containers."""
    containers = ["guidelines", "examples", "memories", "metrics", "evals", "review-jobs"]
    client = CosmosDBManagementClient(credential, v.subscription_id)
    try:
        for container in containers:
            try:
                client.sql_resources.get_sql_container(
                    v.rg_name,
                    v.cosmos_account_name,
                    v.cosmos_db_name,
                    container,
                )
                print(f"✅ Container already exists: {container}")
            except ResourceNotFoundError:
                print(f"Creating DB container {container}...")
                client.sql_resources.begin_create_update_sql_container(
                    v.rg_name,
                    v.cosmos_account_name,
                    v.cosmos_db_name,
                    container,
                    SqlContainerCreateUpdateParameters(
                        resource=SqlContainerResource(
                            id=container,
                            partition_key=ContainerPartitionKey(paths=["/id"]),
                        )
                    ),
                ).result()
                print(f"✅ Created container: {container}")
    except Exception as e:
        print(f"❌ An error occurred: {e}")
        sys.exit(1)


def create_cognitive_services(v: Variables):
    """Create the Azure Cognitive Services resource."""
    client = CognitiveServicesManagementClient(credential, v.subscription_id)
    try:
        client.accounts.get(v.rg_name, v.cognitive_services_name)
        print(f"✅ Using existing Cognitive Services resource: {v.cognitive_services_name}")
    except ResourceNotFoundError:
        cognitive_service = Account(
            location=v.rg_location,
            sku=CognitiveSku(name="S0"),
            kind="CognitiveServices",
            identity={"type": "SystemAssigned"},
            properties=AccountProperties(
                disable_local_auth=True,  # Ensures compliance with security policy
            ),
        )
        try:
            client.accounts.begin_create(v.rg_name, v.cognitive_services_name, cognitive_service).result()
            print(f"✅ Created Cognitive Services resource: {v.cognitive_services_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_azure_openai(v: Variables):
    """Creates an Azure OpenAI resource."""
    client = CognitiveServicesManagementClient(credential, v.subscription_id)
    try:
        resource = client.accounts.get(v.ai_rg, v.openai_name)
        print(f"✅ Using existing OpenAI resource: {v.openai_name}")
        return resource
    except ResourceNotFoundError:
        openai_resource = Account(
            location=v.rg_location,
            sku=CognitiveSku(name="S0"),
            kind="OpenAI",
            identity={"type": "SystemAssigned"},
            properties=AccountProperties(
                custom_sub_domain_name=v.openai_name,
                public_network_access="Enabled",
                network_acls={
                    "defaultAction": "Allow",
                    "virtualNetworkRules": [],
                    "ipRules": [],
                },
                disable_local_auth=False,  # Ensures compliance with security policy
            ),
        )
        try:
            resource = client.accounts.begin_create(v.ai_rg, v.openai_name, openai_resource).result()
            print(f"✅ Created OpenAI resource: {v.openai_name}")
            return resource
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_azure_openai_deployments(v: Variables):
    """Create the necessary model deployments in Azure OpenAI"""
    client = CognitiveServicesManagementClient(credential, v.subscription_id)
    models_to_deploy = [
        (v.openai_embedding_model, "GlobalStandard", "1", 500),
        ("gpt-4.1", "GlobalStandard", "2025-04-14", 500),
        ("gpt-4.1-nano", "GlobalStandard", "2025-04-14", 500),
        ("o3-mini", "GlobalStandard", "2025-01-31", 100),
        ("gpt-5", "GlobalStandard", "2025-08-07", 100),
        ("gpt-5-mini", "GlobalStandard", "2025-08-07", 100),
    ]
    for model, sku, version, capacity in models_to_deploy:
        try:
            client.deployments.get(v.ai_rg, v.openai_name, model)
            print(f"✅ Using existing OpenAI model deployment: {model}")
        except ResourceNotFoundError:
            deployment = Deployment(
                properties={"model": DeploymentModel(format="OpenAI", name=model, version=version)},
                sku=ResourceSku(name=sku, capacity=capacity),
            )
            client.deployments.begin_create_or_update(v.ai_rg, v.openai_name, model, deployment).result()
            print(f"✅ Created OpenAI model deployment: {model}")


def create_azure_search_service(v: Variables):
    """Create the Azure AI Search service."""
    # create Azure AI Search service
    client = SearchManagementClient(credential, v.subscription_id)
    try:
        resource = client.services.get(v.rg_name, v.search_name)
        print(f"✅ Using existing Azure AI Search resource: {v.search_name}")
        return resource
    except ResourceNotFoundError:
        search_service = SearchService(
            location=v.rg_location,
            sku=SearchSku(name="standard"),
            disable_local_auth=True,  # Ensures compliance with security policy
            identity={"type": "SystemAssigned"},
            semantic_search="standard",
        )
        resource = client.services.begin_create_or_update(v.rg_name, v.search_name, search_service).result()
        print(f"Created Azure AI Search resource: {v.search_name}")
        return resource


def _get_vector_search_config(v: Variables, *, profile_name: str) -> VectorSearch:
    """Create the Azure AI Search vector search configuration."""
    algorithm_name = "avc-vector-search"
    algorithm_profile_name = profile_name
    vectorizer_name = "avc-vectorizer"
    vector_search = VectorSearch(
        algorithms=[
            HnswAlgorithmConfiguration(
                name=algorithm_name,
                parameters=HnswParameters(metric="cosine", m=4, ef_construction=400, ef_search=500),
            )
        ],
        profiles=[
            VectorSearchProfile(
                name=algorithm_profile_name,
                algorithm_configuration_name=algorithm_name,
                vectorizer_name=vectorizer_name,
            )
        ],
        vectorizers=[
            AzureOpenAIVectorizer(
                vectorizer_name=vectorizer_name,
                parameters=AzureOpenAIVectorizerParameters(
                    resource_url=v.openai_endpoint,
                    deployment_name=v.openai_embedding_model,
                    model_name=v.openai_embedding_model,
                ),
            )
        ],
    )
    return vector_search


def _get_semantic_search_config(
    *,
    config_name: str,
    content_fields: List[str],
    title_field: str | None,
    keyword_fields: List[str],
) -> SemanticConfiguration:
    semantic_config = SemanticConfiguration(
        name=config_name,
        prioritized_fields=SemanticPrioritizedFields(
            keywords_fields=[SemanticField(field_name=x) for x in keyword_fields],
            content_fields=[SemanticField(field_name=x) for x in content_fields],
        ),
    )
    if title_field:
        semantic_config.prioritized_fields.title_field = SemanticField(field_name=title_field)
    return SemanticSearch(configurations=[semantic_config])


def configure_search_identity(v: Variables, resource: SearchService):
    principal_id = (
        resource.identity.principal_id  # pyright: ignore[reportOptionalMemberAccess]
    )  # pyright: ignore[reportOptionalMemberAccess]
    assign_rbac_roles(
        v,
        roles=[
            "Cosmos DB Account Reader Role",
            "Cognitive Services Contributor",
            "Cognitive Services User",
        ],
        principal_id=principal_id,
        principal_type=PrincipalType.SERVICE_PRINCIPAL,
    )
    assign_rbac_roles(
        v,
        roles=["Cognitive Services OpenAI User"],
        principal_id=principal_id,
        principal_type=PrincipalType.SERVICE_PRINCIPAL,
        scope=v.openai_id,
    )
    if v.assignee_object_id:
        assign_rbac_roles(
            v,
            roles=["Search Index Data Reader"],
            principal_id=v.assignee_object_id,
            principal_type=PrincipalType.USER,
        )
    _assign_cosmosdb_builtin_roles(v, kind="readOnly", principal_id=principal_id)


def _get_skillset_definition(
    v: Variables,
    *,
    name: Literal["examples", "guidelines", "memories"],
    field_mappings: List[FieldMapping],
) -> SearchIndexerSkillset:
    # Create the split skill
    split_skill = SplitSkill(
        name="#1-split-skill",
        description="Split text into chunks for embedding",
        context="/document",
        default_language_code="en",
        maximum_page_length=2000,
        page_overlap_length=500,
        maximum_pages_to_take=0,
        inputs=[InputFieldMappingEntry(name="text", source="/document/content")],
        outputs=[OutputFieldMappingEntry(name="textItems", target_name="pages")],
        text_split_mode=TextSplitMode.PAGES,
    )

    # Create the embedding skill
    embedding_skill = AzureOpenAIEmbeddingSkill(
        name="#2-embedding-skill",
        context="/document/pages/*",
        inputs=[InputFieldMappingEntry(name="text", source="/document/pages/*")],
        outputs=[OutputFieldMappingEntry(name="embedding", target_name="text_vector")],
        resource_url=v.openai_endpoint,
        dimensions=v.openai_embedding_dimensions,
        deployment_name=v.openai_embedding_model,
        model_name=v.openai_embedding_model,
    )

    # Create the index projection
    index_projections = SearchIndexerIndexProjection(
        selectors=[
            SearchIndexerIndexProjectionSelector(
                target_index_name=v.search_index_name,
                parent_key_field_name="parent_id",
                source_context="/document/pages/*",
                mappings=field_mappings,
            )
        ],
        parameters=SearchIndexerIndexProjectionsParameters(
            projection_mode=IndexProjectionMode.SKIP_INDEXING_PARENT_DOCUMENTS
        ),
    )

    return SearchIndexerSkillset(
        name=f"{name}-skillset",
        skills=[split_skill, embedding_skill],
        description="Chunk and vectorize documents",
        cognitive_services_account=DefaultCognitiveServicesAccount(),
        index_projection=index_projections,
    )


def create_azure_search_skillsets(v: Variables):
    client = SearchIndexerClient(endpoint=v.search_endpoint, credential=credential)
    guidelines_skillset_name = "guidelines-skillset"
    try:
        client.get_skillset(guidelines_skillset_name)
        print(f"✅ Using existing Azure AI Search skillset: {guidelines_skillset_name}")
    except ResourceNotFoundError:
        guidelines_field_mappings = [
            InputFieldMappingEntry(name="text_vector", source="/document/pages/*/text_vector"),
            InputFieldMappingEntry(name="chunk", source="/document/pages/*"),
            InputFieldMappingEntry(name="id", source="/document/id"),
            InputFieldMappingEntry(name="title", source="/document/title"),
            InputFieldMappingEntry(name="kind", source="/document/kind"),
            InputFieldMappingEntry(name="language", source="/document/language"),
            InputFieldMappingEntry(name="source", source="/document/source"),
            InputFieldMappingEntry(name="tags", source="/document/tags"),
        ]
        guidelines_skillset = _get_skillset_definition(v, name="guidelines", field_mappings=guidelines_field_mappings)
        try:
            client.create_skillset(guidelines_skillset)
            print(f"✅ Created Azure AI Search skillset: {guidelines_skillset_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)

    examples_skillset_name = "examples-skillset"
    try:
        client.get_skillset(examples_skillset_name)
        print(f"✅ Using existing Azure AI Search skillset: {examples_skillset_name}")
    except ResourceNotFoundError:
        examples_field_mappings = [
            InputFieldMappingEntry(name="text_vector", source="/document/pages/*/text_vector"),
            InputFieldMappingEntry(name="chunk", source="/document/pages/*"),
            InputFieldMappingEntry(name="id", source="/document/id"),
            InputFieldMappingEntry(name="title", source="/document/title"),
            InputFieldMappingEntry(name="kind", source="/document/kind"),
            InputFieldMappingEntry(name="language", source="/document/language"),
            InputFieldMappingEntry(name="service", source="/document/service"),
            InputFieldMappingEntry(name="example_type", source="/document/example_type"),
            InputFieldMappingEntry(name="source", source="/document/source"),
            InputFieldMappingEntry(name="is_exception", source="/document/is_exception"),
            InputFieldMappingEntry(name="tags", source="/document/tags"),
        ]
        examples_skillset = _get_skillset_definition(v, name="examples", field_mappings=examples_field_mappings)
        try:
            client.create_skillset(examples_skillset)
            print(f"✅ Created Azure AI Search skillset: {examples_skillset_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)

    memory_skillset_name = "memories-skillset"
    try:
        client.get_skillset(memory_skillset_name)
        print(f"✅ Using existing Azure AI Search skillset: {memory_skillset_name}")
    except ResourceNotFoundError:
        memory_field_mappings = [
            InputFieldMappingEntry(name="text_vector", source="/document/pages/*/text_vector"),
            InputFieldMappingEntry(name="chunk", source="/document/pages/*"),
            InputFieldMappingEntry(name="id", source="/document/id"),
            InputFieldMappingEntry(name="title", source="/document/title"),
            InputFieldMappingEntry(name="kind", source="/document/kind"),
            InputFieldMappingEntry(name="language", source="/document/language"),
            InputFieldMappingEntry(name="service", source="/document/service"),
            InputFieldMappingEntry(name="source", source="/document/source"),
            InputFieldMappingEntry(name="is_exception", source="/document/is_exception"),
            InputFieldMappingEntry(name="tags", source="/document/tags"),
        ]
        memory_skillset = _get_skillset_definition(v, name="memories", field_mappings=memory_field_mappings)
        try:
            client.create_skillset(memory_skillset)
            print(f"✅ Created Azure AI Search skillset: {memory_skillset_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_cosmos_to_search_data_sources(v: Variables):
    try:
        cosmos_client = CosmosDBManagementClient(credential, v.subscription_id)
        cosmos_connection_string = (
            cosmos_client.database_accounts.list_connection_strings(v.rg_name, v.cosmos_account_name)
            .connection_strings[0]
            .connection_string
        )
        resource_id = cosmos_client.database_accounts.get(v.rg_name, v.cosmos_account_name).id
        # must restructure the connection string to work with system-managed identity
        # remove the AccountKey from the connection string via regex
        cosmos_connection_string = re.sub(r"AccountKey=[^;]*;", "", cosmos_connection_string)
        # replace AccountEndpoint with the ResourceId
        cosmos_connection_string = re.sub(
            r"AccountEndpoint=[^;]*;",
            f"ResourceId={resource_id};",
            cosmos_connection_string,
        )
        cosmos_connection_string = f"{cosmos_connection_string}Database={v.cosmos_db_name};IdentityAuthType=AccessToken"
    except Exception as e:
        print(f"❌ An error occurred getting CosmosDB connection string: {e}")
        sys.exit(1)

    client = SearchIndexerClient(endpoint=v.search_endpoint, credential=credential)
    try:
        _ = client.get_data_source_connection("guidelines-ds")
        print("✅ Using existing Azure AI Search data source: guidelines-ds")
    except ResourceNotFoundError:
        try:
            guidelines_ds = SearchIndexerDataSourceConnection(
                name="guidelines-ds",
                type=SearchIndexerDataSourceType.COSMOS_DB,
                connection_string=cosmos_connection_string,
                container=SearchIndexerDataContainer(name="guidelines"),
                data_change_detection_policy={
                    "@odata.type": "#Microsoft.Azure.Search.HighWaterMarkChangeDetectionPolicy",
                    "highWaterMarkColumnName": "_ts",
                },
                data_deletion_detection_policy={
                    "@odata.type": "#Microsoft.Azure.Search.SoftDeleteColumnDeletionDetectionPolicy",
                    "softDeleteColumnName": "isDeleted",
                    "softDeleteMarkerValue": "true",
                },
            )
            client.create_data_source_connection(guidelines_ds)
            print("✅ Created Azure AI Search data source: guidelines-ds")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)

    try:
        _ = client.get_data_source_connection("examples-ds")
        print("✅ Using existing Azure AI Search data source: examples-ds")
    except ResourceNotFoundError:
        try:
            examples_ds = SearchIndexerDataSourceConnection(
                name="examples-ds",
                type=SearchIndexerDataSourceType.COSMOS_DB,
                connection_string=cosmos_connection_string,
                container=SearchIndexerDataContainer(name="examples"),
                data_change_detection_policy={
                    "@odata.type": "#Microsoft.Azure.Search.HighWaterMarkChangeDetectionPolicy",
                    "highWaterMarkColumnName": "_ts",
                },
                data_deletion_detection_policy={
                    "@odata.type": "#Microsoft.Azure.Search.SoftDeleteColumnDeletionDetectionPolicy",
                    "softDeleteColumnName": "isDeleted",
                    "softDeleteMarkerValue": "true",
                },
            )
            client.create_data_source_connection(examples_ds)
            print("✅ Created Azure AI Search data source: examples-ds")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)

    try:
        _ = client.get_data_source_connection("memories-ds")
        print("✅ Using existing Azure AI Search data source: memories-ds")
    except ResourceNotFoundError:
        try:
            memories_ds = SearchIndexerDataSourceConnection(
                name="memories-ds",
                type=SearchIndexerDataSourceType.COSMOS_DB,
                connection_string=cosmos_connection_string,
                container=SearchIndexerDataContainer(name="memories"),
                data_change_detection_policy={
                    "@odata.type": "#Microsoft.Azure.Search.HighWaterMarkChangeDetectionPolicy",
                    "highWaterMarkColumnName": "_ts",
                },
                data_deletion_detection_policy={
                    "@odata.type": "#Microsoft.Azure.Search.SoftDeleteColumnDeletionDetectionPolicy",
                    "softDeleteColumnName": "isDeleted",
                    "softDeleteMarkerValue": "true",
                },
            )
            client.create_data_source_connection(memories_ds)
            print("✅ Created Azure AI Search data source: memories-ds")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_unified_search_index(v: Variables):
    """Create the Azure AI Search index for unified search."""
    client = SearchIndexClient(endpoint=v.search_endpoint, credential=credential)
    index_name = v.search_index_name
    try:
        _ = client.get_index(index_name)
        print(f"✅ Using existing Azure AI Search index: {index_name}")
    except ResourceNotFoundError:
        fields = [
            # identifiers and links
            SearchField(
                name="chunk_id",
                type=SFDT.String,
                key=True,
                filterable=True,
                searchable=True,
                stored=True,
                sortable=True,
                facetable=False,
                analyzer_name=LexicalAnalyzerName.KEYWORD,
            ),
            SearchField(
                name="parent_id",
                type=SFDT.String,
                filterable=True,
                searchable=False,
                stored=True,
                sortable=False,
                facetable=True,
            ),
            # core text
            SearchField(
                name="chunk",
                type=SFDT.String,
                searchable=True,
                stored=True,
                filterable=False,
                sortable=False,
                facetable=False,
                analyzer_name=LexicalAnalyzerName.EN_MICROSOFT,
            ),
            # vectorized text
            SearchField(
                name="text_vector",
                type=SFDT.Collection(SFDT.Single),
                searchable=True,
                stored=True,
                hidden=True,
                vector_search_dimensions=v.openai_embedding_dimensions,
                vector_search_profile_name=v.vectorizer_profile_name,
            ),
            # GUID or document ID
            SearchField(
                name="id",
                type=SFDT.String,
                searchable=False,
                stored=True,
                filterable=True,
                sortable=True,
                facetable=True,
            ),
            # human-readable title for semantic search
            SearchField(
                name="title",
                type=SFDT.String,
                searchable=True,
                stored=True,
                filterable=False,
                sortable=True,
                facetable=False,
                analyzer_name=LexicalAnalyzerName.EN_MICROSOFT,
            ),
            # metadata fields
            SearchField(
                name="kind",
                type=SFDT.String,
                searchable=False,
                filterable=True,
                stored=True,
                facetable=True,
            ),
            SearchField(
                name="language",
                type=SFDT.String,
                searchable=False,
                filterable=True,
                stored=True,
                facetable=True,
            ),
            SearchField(
                name="example_type",
                type=SFDT.String,
                searchable=False,
                filterable=True,
                stored=True,
                facetable=True,
            ),
            SearchField(
                name="service",
                type=SFDT.String,
                searchable=False,
                filterable=True,
                stored=True,
                facetable=True,
            ),
            SearchField(
                name="source",
                type=SFDT.String,
                searchable=False,
                filterable=True,
                stored=True,
                facetable=True,
            ),
            SearchField(
                name="is_exception",
                type=SFDT.Boolean,
                searchable=False,
                filterable=True,
                stored=True,
                facetable=True,
            ),
            SearchField(
                name="tags",
                type=SFDT.Collection(SFDT.String),
                searchable=True,
                filterable=True,
                facetable=True,
                stored=True,
                sortable=False,
                analyzer_name=LexicalAnalyzerName.EN_MICROSOFT,
            ),
            SearchField(
                name="action_taken",
                type=SFDT.String,
                searchable=True,
                filterable=True,
                facetable=True,
                stored=True,
                sortable=False,
            ),
            SearchField(
                name="isDeleted",
                type=SFDT.Boolean,
                searchable=False,
                filterable=True,
                stored=True,
                facetable=True,
            ),
        ]
        index = SearchIndex(
            name=index_name,
            fields=fields,
            vector_search=_get_vector_search_config(v, profile_name=v.vectorizer_profile_name),
            semantic_search=_get_semantic_search_config(
                config_name="semantic-search-config",
                content_fields=["chunk"],
                title_field="title",
                keyword_fields=[
                    "kind",
                    "id",
                    "language",
                    "service",
                    "example_type",
                    "source",
                    "tags",
                ],
            ),
        )
        try:
            client.create_index(index)
            print(f"✅ Created Azure AI Search index: {index_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_azure_search_indexers(v: Variables):
    client = SearchIndexerClient(endpoint=v.search_endpoint, credential=credential)

    guidelines_indexer_name = "guidelines-indexer"
    try:
        client.get_indexer(guidelines_indexer_name)
        print(f"✅ Using existing Azure AI Search indexer: {guidelines_indexer_name}")
    except ResourceNotFoundError:
        guidelines_indexer = SearchIndexer(
            name=guidelines_indexer_name,
            description="Indexer for guidelines",
            data_source_name="guidelines-ds",
            target_index_name=v.search_index_name,
            schedule={"interval": "P1D"},
            skillset_name="guidelines-skillset",
            parameters={
                "batchSize": 50,
                "maxFailedItems": 1000,
                "maxFailedItemsPerBatch": 25,
            },
            field_mappings=[
                # maps the internal "rid" field to the "chunk_id" field in the index
                FieldMapping(
                    source_field_name="rid",
                    target_field_name="chunk_id",
                    mapping_function=FieldMappingFunction(name="base64Encode"),
                )
            ],
        )
        try:
            client.create_indexer(guidelines_indexer)
            print(f"✅ Created Azure AI Search indexer: {guidelines_indexer_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)

    examples_indexer_name = "examples-indexer"
    try:
        client.get_indexer(examples_indexer_name)
        print(f"✅ Using existing Azure AI Search indexer: {examples_indexer_name}")
    except ResourceNotFoundError:
        examples_indexer = SearchIndexer(
            name="examples-indexer",
            data_source_name="examples-ds",
            target_index_name=v.search_index_name,
            description="Indexer for examples",
            skillset_name="examples-skillset",
            parameters={
                "batchSize": 50,
                "maxFailedItems": 1000,
                "maxFailedItemsPerBatch": 25,
            },
            field_mappings=[
                # maps the internal "rid" field to the "chunk_id" field in the index
                FieldMapping(
                    source_field_name="rid",
                    target_field_name="chunk_id",
                    mapping_function=FieldMappingFunction(name="base64Encode"),
                )
            ],
        )
        try:
            client.create_indexer(examples_indexer)
            print(f"✅ Created Azure AI Search indexer: {examples_indexer_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)

    memories_indexer_name = "memories-indexer"
    try:
        client.get_indexer(memories_indexer_name)
        print(f"✅ Using existing Azure AI Search indexer: {memories_indexer_name}")
    except ResourceNotFoundError:
        memories_indexer = SearchIndexer(
            name="memories-indexer",
            data_source_name="memories-ds",
            target_index_name=v.search_index_name,
            description="Indexer for memories",
            skillset_name="memories-skillset",
            parameters={
                "batchSize": 50,
                "maxFailedItems": 1000,
                "maxFailedItemsPerBatch": 25,
            },
            field_mappings=[
                FieldMapping(
                    source_field_name="rid",
                    target_field_name="chunk_id",
                    mapping_function=FieldMappingFunction(name="base64Encode"),
                )
            ],
        )
        try:
            client.create_indexer(memories_indexer)
            print(f"✅ Created Azure AI Search indexer: {memories_indexer_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_keyvault(v: Variables):
    """Create the Key Vault."""
    client = KeyVaultManagementClient(credential, v.subscription_id)
    try:
        client.vaults.get(v.rg_name, v.keyvault_name)
        print(f"✅ Using existing Key Vault: {v.keyvault_name}")
    except ResourceNotFoundError:
        try:
            access_policies = []
            if v.assignee_object_id:
                access_policies.append(
                    AccessPolicyEntry(
                        tenant_id=v.tenant_id,
                        object_id=v.assignee_object_id,
                        permissions=Permissions(
                            keys=["get", "list"],
                            secrets=["get", "list", "set"],
                            certificates=["get", "list"],
                        ),
                    )
                )
            client.vaults.begin_create_or_update(
                v.rg_name,
                v.keyvault_name,
                Vault(
                    location=v.rg_location,
                    properties=VaultProperties(
                        tenant_id=v.tenant_id, sku=KeyVaultSku(name="standard"), access_policies=access_policies
                    ),
                ),
            ).result()
            print(f"✅ Created Key Vault: {v.keyvault_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_app_configuration(v: Variables):
    """Create the App Configuration."""
    client = AppConfigurationManagementClient(credential, v.subscription_id)
    try:
        client.configuration_stores.get(v.rg_name, v.app_configuration_name)
        print(f"✅ Using existing App Configuration: {v.app_configuration_name}")
    except ResourceNotFoundError:
        try:
            client.configuration_stores.begin_create(
                v.rg_name,
                v.app_configuration_name,
                ConfigurationStore(
                    location=v.rg_location,
                    sku=AppConfigurationSku(name="standard"),
                    disable_local_auth=True,
                    data_plane_proxy=DataPlaneProxyProperties(authentication_mode="Pass-through"),
                ),
            ).result()
            print(f"✅ Created App Configuration: {v.app_configuration_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def populate_settings(v: Variables):
    secrets_client = SecretClient(vault_url=v.keyvault_endpoint, credential=credential)

    # retrieve the API Key for the OpenAI resource and store it as a KeyVault secret
    mgmt_client = CognitiveServicesManagementClient(credential, v.subscription_id)
    try:
        secret_name = "AZURE-OPENAI-API-KEY"
        secret_uri = f"https://{v.keyvault_name}.vault.azure.net/secrets/{secret_name}"
        secrets_client.set_secret(
            secret_name,
            mgmt_client.accounts.list_keys(v.ai_rg, v.openai_name).key1,
        )
    except Exception as e:
        print(f"❌ An error occurred: {e}")
        sys.exit(1)
    # pylint: disable=protected-access
    label = "staging" if v.is_staging else "production"
    appconfig_client = AzureAppConfigurationClient(base_url=v.app_configuration_endpoint, credential=credential)

    # allow list of setting keys we want to persist in AppConfiguration
    settings_for_app_config = set(
        [
            "cosmos_db_name",
            "cosmos_endpoint",
            "foundry_endpoint",
            "foundry_kernel_model",
            "foundry_api_version",
            "foundry_project",
            "openai_endpoint",
            "rg_name",
            "search_endpoint",
            "search_index_name",
            "subscription_id",
            "tenant_id",
            "webapp_endpoint",
            "webapp_name",
            "evals_subscription",
            "evals_rg",
            "evals_project_name",
        ]
    )

    # set the regular config items
    for key, value in v.__dict__.items():
        key = key.strip().lower()
        if key not in settings_for_app_config:
            continue
        setting = ConfigurationSetting(key=key, label=label, value=value)
        appconfig_client.set_configuration_setting(setting)

    # now set the keyvault secret config item
    setting = SecretReferenceConfigurationSetting(key="openai_api_key", label=label, secret_id=secret_uri)
    appconfig_client.set_configuration_setting(setting)
    print("✅ App Configuration and KeyVault settings updated.")


def create_app_service_plan(v: Variables):
    """Create the App Service Plan App."""
    client = WebSiteManagementClient(credential, v.subscription_id)
    try:
        client.app_service_plans.get(v.rg_name, v.app_service_plan_name)
        print(f"✅ Using existing App Service Plan: {v.app_service_plan_name}")
    except ResourceNotFoundError:
        try:
            client.app_service_plans.begin_create_or_update(
                v.rg_name,
                v.app_service_plan_name,
                AppServicePlan(
                    location=v.rg_location,
                    sku=SkuDescription(name="P1v3", tier="Premium", size="V3", family="P"),
                    reserved=True,
                ),
            ).result()
            print(f"✅ Created App Service Plan: {v.app_service_plan_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_webapp(v: Variables) -> Site:
    """Create the Web App."""
    client = WebSiteManagementClient(credential, v.subscription_id)

    try:
        # Check if the web app already exists
        webapp = client.web_apps.get(v.rg_name, v.webapp_name)
        print(f"✅ Using existing Web App: {v.webapp_name}")
        return webapp
    except ResourceNotFoundError:
        try:
            # Create the web app
            print(f"Creating Web App {v.webapp_name}...")
            env_v = {
                "SCM_DO_BUILD_DURING_DEPLOYMENT": "true",
                "WEBSITE_ENABLE_SYNC_UPDATE_SITE": "true",
                "ENVIRONMENT_NAME": "staging" if v.is_staging else "production",
                "AZURE_APP_CONFIG_ENDPOINT": v.app_configuration_endpoint,
            }
            client.web_apps.begin_create_or_update(
                v.rg_name,
                v.webapp_name,
                Site(
                    location=v.rg_location,
                    server_farm_id=v.app_service_plan_name,
                    https_only=True,
                    reserved=True,
                    site_config=SiteConfig(
                        http20_enabled=False,
                        always_on=True,
                        linux_fx_version="PYTHON|3.12",
                        public_network_access="Enabled",
                        minimum_elastic_instance_count=1,
                        app_settings=[{"name": name, "value": value} for name, value in env_v.items()],
                    ),
                    identity={"type": "SystemAssigned"},
                ),
            ).result()
            print(f"✅ Created Web App: {v.webapp_name}")
            # Re-fetch the webapp to get updated managed identity info
            return client.web_apps.get(v.rg_name, v.webapp_name)
        except Exception as e:
            print(f"❌ An error occurred while creating the Web App: {e}")
            sys.exit(1)


def create_azure_ai_foundry(v: Variables):
    """Create the Azure AI Foundry resource."""
    client = CognitiveServicesManagementClient(credential, v.subscription_id)
    try:
        resource = client.accounts.get(v.ai_rg, v.foundry_account_name)
        print(f"✅ Using existing Azure AI Foundry resource: {v.foundry_account_name}")
        return resource
    except ResourceNotFoundError:
        foundry_resource = Account(
            location=v.rg_location,
            sku=CognitiveSku(name="S0"),
            kind="AIServices",
            identity={"type": "SystemAssigned"},
            properties={
                "disableLocalAuth": True,  # Ensures compliance with security policy
                "allowProjectManagement": True,
                "customSubDomainName": v.foundry_account_name,
            },
        )
        try:
            resource = client.accounts.begin_create(v.ai_rg, v.foundry_account_name, foundry_resource).result()
            print(f"✅ Created Azure AI Foundry resource: {v.foundry_account_name}")
            return resource
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def create_azure_ai_foundry_deployments(v: Variables):
    """Create the necessary model deployments in Azure AI Foundry"""
    client = CognitiveServicesManagementClient(credential, v.subscription_id)
    models_to_deploy = [
        ("gpt-5", "GlobalStandard", "2025-08-07", 100),
        ("gpt-5-mini", "GlobalStandard", "2025-08-07", 100),
    ]
    for model, sku, version, capacity in models_to_deploy:
        try:
            client.deployments.get(v.ai_rg, v.foundry_account_name, model)
            print(f"✅ Using existing Azure AI Foundry model deployment: {model}")
        except ResourceNotFoundError:
            deployment = Deployment(
                properties={"model": DeploymentModel(format="OpenAI", name=model, version=version)},
                sku=ResourceSku(name=sku, capacity=capacity),
            )
            client.deployments.begin_create_or_update(v.ai_rg, v.foundry_account_name, model, deployment).result()
            print(f"✅ Created Azure AI Foundry model deployment: {model}")


def create_azure_ai_foundry_project(v: Variables):
    client = CognitiveServicesManagementClient(credential, v.subscription_id)
    try:
        client.projects.get(v.ai_rg, v.foundry_account_name, v.foundry_project_name)
        print(f"✅ Using existing Azure AI Foundry Project: {v.foundry_project_name}")
    except ResourceNotFoundError:
        try:
            client.projects.begin_create(
                resource_group_name=v.ai_rg,
                account_name=v.foundry_account_name,
                project_name=v.foundry_project_name,
                project={
                    "location": v.rg_location,
                    "identity": {"type": "SystemAssigned"},
                    "properties": {},
                },
            ).result()
            print(f"✅ Created Azure AI Foundry Project: {v.foundry_project_name}")
        except Exception as e:
            print(f"❌ An error occurred: {e}")
            sys.exit(1)


def check_assignee(v):
    if not v.assignee_object_id:
        print(
            "⚠️ ASSIGNEE_OBJECT_ID is not set. You will be unable to directly use the resources created. Run the command: `az ad signed-in-user show` to get your object ID."
        )


if __name__ == "__main__":
    env_name = os.getenv("ENVIRONMENT_NAME")
    if env_name not in ("production", "staging"):
        print("❌ ENVIRONMENT_NAME environment variable must be set to 'production' or 'staging'")
        sys.exit(1)
    print(f"✅ Creating resources for ENVIRONMENT_NAME: {env_name}")
    v = Variables(is_staging=env_name == "staging")
    check_credential()
    check_assignee(v)
    create_resource_group(v)

    create_cosmosdb_account(v)
    if v.assignee_object_id:
        assign_cosmosdb_permissions(v, principal_id=v.assignee_object_id, principal_type=PrincipalType.USER)
    create_cosmosdb_database(v)
    create_cosmosdb_containers(v)

    create_cognitive_services(v)
    openai_resource = create_azure_openai(v)
    create_azure_openai_deployments(v)
    if v.assignee_object_id:
        assign_rbac_roles(
            v,
            roles=["Cognitive Services OpenAI User"],
            principal_id=v.assignee_object_id,
            principal_type=PrincipalType.USER,
            scope=openai_resource.id,
        )
    ai_foundry_resource = create_azure_ai_foundry(v)
    create_azure_ai_foundry_deployments(v)
    create_azure_ai_foundry_project(v)
    if v.assignee_object_id:
        assign_rbac_roles(
            v,
            roles=[
                "Azure AI User",
            ],
            principal_id=v.assignee_object_id,
            principal_type=PrincipalType.USER,
            scope=ai_foundry_resource.id,
        )

    search_service = create_azure_search_service(v)
    configure_search_identity(v, search_service)
    create_unified_search_index(v)
    create_cosmos_to_search_data_sources(v)
    create_azure_search_skillsets(v)
    create_azure_search_indexers(v)

    create_keyvault(v)
    create_app_configuration(v)
    if v.assignee_object_id:
        assign_rbac_roles(
            v,
            roles=["App Configuration Data Owner"],
            principal_id=v.assignee_object_id,
            principal_type=PrincipalType.USER,
        )
    populate_settings(v)

    create_app_service_plan(v)
    webapp = create_webapp(v)
    webapp_identity = webapp.identity.principal_id  # pyright: ignore[reportOptionalMemberAccess]
    assign_cosmosdb_permissions(v, principal_id=webapp_identity, principal_type=PrincipalType.SERVICE_PRINCIPAL)
    assign_rbac_roles(
        v,
        roles=[
            "App Configuration Data Owner",
            "Search Index Data Reader",
        ],
        principal_id=webapp_identity,
        principal_type=PrincipalType.SERVICE_PRINCIPAL,
    )

    assign_rbac_roles(
        v,
        roles=[
            "Cognitive Services OpenAI User",
        ],
        principal_id=webapp_identity,
        principal_type=PrincipalType.SERVICE_PRINCIPAL,
        scope=openai_resource.id,
    )
    assign_rbac_roles(
        v,
        roles=["Azure AI User"],
        principal_id=webapp_identity,
        principal_type=PrincipalType.SERVICE_PRINCIPAL,
        scope=ai_foundry_resource.id,
    )
