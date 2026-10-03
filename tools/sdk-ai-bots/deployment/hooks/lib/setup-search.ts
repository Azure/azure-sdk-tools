import { execFileSync } from "node:child_process";

const STABLE_API_VERSION = "2026-04-01";
const AGENTIC_API_VERSION = "2026-08-01-preview";

type JsonObject = Record<string, unknown>;

export type SearchResourceAction = "create" | "update" | "unchanged";

export interface SearchResourceDefinition {
  collection: string;
  apiVersion: string;
  name: string;
  body: JsonObject;
}

export interface SearchResourceResult {
  collection: string;
  name: string;
  action: SearchResourceAction;
}

export interface SearchResourcePlanOptions {
  subscriptionId: string;
  resourceGroup: string;
  searchServiceName: string;
  storageAccountName: string;
  aiResourceName: string;
  env?: NodeJS.ProcessEnv;
}

export interface ReconcileSearchResourcesOptions extends SearchResourcePlanOptions {
  dryRun?: boolean;
  fetchImpl?: typeof fetch;
  searchAdminKey?: string;
  getSearchAdminKey?: (options: SearchResourcePlanOptions) => string;
  log?: (message: string) => void;
}

function configured(env: NodeJS.ProcessEnv, key: string, fallback: string): string {
  return env[key]?.trim() || fallback;
}

function sourceDataFields(): JsonObject[] {
  return [
    "chunk",
    "chunk_id",
    "context_id",
    "header_1",
    "header_2",
    "header_3",
    "ordinal_position",
    "scope",
    "service_type",
    "text_vector",
    "title",
  ].map((name) => ({ name }));
}

function semanticConfiguration(name: string): JsonObject {
  return {
    name,
    prioritizedFields: {
      titleField: { fieldName: "title" },
      prioritizedContentFields: [{ fieldName: "chunk" }],
      prioritizedKeywordsFields: [
        { fieldName: "header_1" },
        { fieldName: "header_2" },
        { fieldName: "header_3" },
      ],
    },
    rankingOrder: "BoostedRerankerScore",
  };
}

function indexDefinition(
  indexName: string,
  synonymMapName: string,
  aiEndpoint: string,
  embeddingModel: string,
): JsonObject {
  const synonymMaps = [synonymMapName];
  return {
    name: indexName,
    defaultScoringProfile: "search-knowledge",
    fields: [
      {
        name: "chunk_id",
        type: "Edm.String",
        key: true,
        searchable: true,
        filterable: true,
        retrievable: true,
        sortable: true,
        facetable: false,
        analyzer: "keyword",
      },
      {
        name: "parent_id",
        type: "Edm.String",
        searchable: false,
        filterable: true,
        retrievable: true,
        sortable: false,
        facetable: false,
      },
      {
        name: "chunk",
        type: "Edm.String",
        searchable: true,
        filterable: false,
        retrievable: true,
        sortable: false,
        facetable: false,
        synonymMaps,
      },
      {
        name: "title",
        type: "Edm.String",
        searchable: true,
        filterable: true,
        retrievable: true,
        sortable: false,
        facetable: false,
        synonymMaps,
      },
      ...["header_1", "header_2", "header_3"].map((name) => ({
        name,
        type: "Edm.String",
        searchable: true,
        filterable: true,
        retrievable: true,
        sortable: false,
        facetable: false,
        synonymMaps,
      })),
      {
        name: "text_vector",
        type: "Collection(Edm.Single)",
        searchable: true,
        filterable: false,
        retrievable: true,
        sortable: false,
        facetable: false,
        dimensions: 1536,
        vectorSearchProfile: "azure-sdk-knowledge-text-profile",
      },
      {
        name: "ordinal_position",
        type: "Edm.Int32",
        searchable: false,
        filterable: true,
        retrievable: true,
        sortable: true,
        facetable: true,
      },
      {
        name: "context_id",
        type: "Edm.String",
        searchable: true,
        filterable: true,
        retrievable: true,
        sortable: false,
        facetable: false,
      },
      ...["scope", "service_type"].map((name) => ({
        name,
        type: "Edm.String",
        searchable: true,
        filterable: true,
        retrievable: true,
        sortable: false,
        facetable: false,
        analyzer: "standard.lucene",
      })),
      {
        name: "page_type",
        type: "Edm.String",
        searchable: false,
        filterable: true,
        retrievable: true,
        sortable: true,
        facetable: true,
      },
      {
        name: "chunk_refs_str",
        type: "Edm.String",
        searchable: false,
        filterable: false,
        retrievable: true,
        sortable: false,
        facetable: false,
      },
    ],
    scoringProfiles: [
      {
        name: "search-knowledge",
        functionAggregation: "sum",
        functions: [],
        text: {
          weights: {
            chunk: 40,
            header_1: 20,
            header_2: 10,
            header_3: 10,
            title: 20,
          },
        },
      },
    ],
    semantic: {
      defaultConfiguration: "azure-sdk-knowledge-semantic-configuration",
      configurations: [
        semanticConfiguration("azure-sdk-knowledge-semantic-configuration"),
        semanticConfiguration("default-semantic-configuration"),
      ],
    },
    vectorSearch: {
      algorithms: [
        {
          name: "azure-sdk-knowledge-algorithm",
          kind: "hnsw",
          hnswParameters: {
            metric: "cosine",
            m: 4,
            efConstruction: 400,
            efSearch: 500,
          },
        },
      ],
      profiles: [
        {
          name: "azure-sdk-knowledge-text-profile",
          algorithm: "azure-sdk-knowledge-algorithm",
          vectorizer: "azure-sdk-knowledge-text-vectorizer",
        },
      ],
      vectorizers: [
        {
          name: "azure-sdk-knowledge-text-vectorizer",
          kind: "azureOpenAI",
          azureOpenAIParameters: {
            resourceUri: aiEndpoint,
            deploymentId: embeddingModel,
            modelName: embeddingModel,
          },
        },
      ],
    },
  };
}

function dataSourceDefinition(
  name: string,
  description: string,
  storageResourceId: string,
  containerName: string,
): JsonObject {
  return {
    name,
    description,
    type: "azureblob",
    credentials: {
      connectionString: `ResourceId=${storageResourceId};`,
    },
    container: { name: containerName },
    dataDeletionDetectionPolicy: {
      "@odata.type": "#Microsoft.Azure.Search.SoftDeleteColumnDeletionDetectionPolicy",
      softDeleteColumnName: "IsDeleted",
      softDeleteMarkerValue: "true",
    },
  };
}

function embeddingSkill(aiEndpoint: string, embeddingModel: string): JsonObject {
  return {
    "@odata.type": "#Microsoft.Skills.Text.AzureOpenAIEmbeddingSkill",
    name: "#2",
    context: "/document/pages/*",
    resourceUri: aiEndpoint,
    deploymentId: embeddingModel,
    modelName: embeddingModel,
    dimensions: 1536,
    inputs: [{ name: "text", source: "/document/pages/*" }],
    outputs: [{ name: "embedding", targetName: "text_vector" }],
  };
}

function splitSkill(description?: string): JsonObject {
  return {
    "@odata.type": "#Microsoft.Skills.Text.SplitSkill",
    name: "#1",
    ...(description ? { description } : {}),
    context: "/document",
    defaultLanguageCode: "en",
    textSplitMode: "pages",
    maximumPageLength: 2000,
    pageOverlapLength: 500,
    inputs: [{ name: "text", source: "/document/content" }],
    outputs: [{ name: "textItems", targetName: "pages" }],
  };
}

function projectionMapping(name: string, source: string): JsonObject {
  return { name, source };
}

function skillsetDefinition(
  name: string,
  description: string,
  indexName: string,
  aiEndpoint: string,
  embeddingModel: string,
  mappings: JsonObject[],
): JsonObject {
  return {
    name,
    description,
    skills: [splitSkill(name === "azure-sdk-knowledge-skillset" ? "Split skill to chunk documents" : undefined), embeddingSkill(aiEndpoint, embeddingModel)],
    indexProjections: {
      selectors: [
        {
          targetIndexName: indexName,
          parentKeyFieldName: "parent_id",
          sourceContext: "/document/pages/*",
          mappings,
        },
      ],
      parameters: { projectionMode: "skipIndexingParentDocuments" },
    },
  };
}

function indexerDefinition(
  name: string,
  dataSourceName: string,
  skillsetName: string,
  indexName: string,
  interval: string,
  configuration: JsonObject,
  fieldMappings: JsonObject[],
): JsonObject {
  return {
    name,
    dataSourceName,
    targetIndexName: indexName,
    skillsetName,
    schedule: { interval },
    parameters: { configuration },
    fieldMappings,
    outputFieldMappings: [],
  };
}

export function buildSearchResourcePlan(options: SearchResourcePlanOptions): SearchResourceDefinition[] {
  const env = options.env ?? process.env;
  const indexName = configured(env, "AI_SEARCH_INDEX", "azure-sdk-knowledge");
  const indexerName = configured(env, "AI_SEARCH_INDEXER", "azure-sdk-knowledge-indexer");
  const knowledgeSourceName = configured(
    env,
    "AI_SEARCH_KNOWLEDGE_SOURCE",
    "azure-sdk-knowledge-source",
  );
  const knowledgeBaseName = configured(
    env,
    "AI_SEARCH_KNOWLEDGE_BASE",
    "azure-sdk-knowledgebase",
  );
  const primaryDataSourceName = configured(
    env,
    "AI_SEARCH_DATA_SOURCE",
    "azuresdkqabot-search-datasource",
  );
  const primarySkillsetName = configured(
    env,
    "AI_SEARCH_SKILLSET",
    "azure-sdk-knowledge-skillset",
  );
  const wikiDataSourceName = configured(
    env,
    "AI_SEARCH_WIKI_DATA_SOURCE",
    "azure-sdk-knowledge-wiki-datasource",
  );
  const wikiSkillsetName = configured(
    env,
    "AI_SEARCH_WIKI_SKILLSET",
    "azure-sdk-knowledge-wiki-skillset",
  );
  const wikiIndexerName = configured(
    env,
    "AI_SEARCH_WIKI_INDEXER",
    "azure-sdk-knowledge-wiki-indexer",
  );
  const synonymMapName = configured(
    env,
    "AI_SEARCH_SYNONYM_MAP",
    "pr-approval-synonyms",
  );
  const embeddingModel = configured(
    env,
    "AI_SEARCH_EMBEDDING_MODEL",
    "text-embedding-3-small",
  );
  const knowledgeModel = configured(env, "AI_SEARCH_KNOWLEDGE_MODEL", "gpt-5-mini");
  const aiEndpoint = `https://${options.aiResourceName}.openai.azure.com`;
  const storageResourceId =
    `/subscriptions/${options.subscriptionId}/resourceGroups/${options.resourceGroup}` +
    `/providers/Microsoft.Storage/storageAccounts/${options.storageAccountName}`;

  const primaryMappings = [
    projectionMapping("text_vector", "/document/pages/*/text_vector"),
    projectionMapping("chunk", "/document/pages/*"),
    projectionMapping("title", "/document/title"),
    projectionMapping("context_id", "/document/context_id"),
    projectionMapping("header_1", "/document/sections/h1"),
    projectionMapping("header_2", "/document/sections/h2"),
    projectionMapping("header_3", "/document/sections/h3"),
    projectionMapping("ordinal_position", "/document/ordinal_position"),
    projectionMapping("scope", "/document/scope"),
    projectionMapping("service_type", "/document/service_type"),
  ];
  const wikiMappings = [
    projectionMapping("text_vector", "/document/pages/*/text_vector"),
    projectionMapping("chunk", "/document/pages/*"),
    projectionMapping("title", "/document/title"),
    projectionMapping("context_id", "/document/context_id"),
    projectionMapping("header_1", "/document/title"),
    projectionMapping("page_type", "/document/page_type"),
    projectionMapping("chunk_refs_str", "/document/chunk_refs"),
  ];

  return [
    {
      collection: "synonymmaps",
      apiVersion: STABLE_API_VERSION,
      name: synonymMapName,
      body: {
        name: synonymMapName,
        format: "solr",
        synonyms: "auto-approval,auto-signoff,automatic approval,automatic signoff",
      },
    },
    {
      collection: "indexes",
      apiVersion: STABLE_API_VERSION,
      name: indexName,
      body: indexDefinition(indexName, synonymMapName, aiEndpoint, embeddingModel),
    },
    {
      collection: "datasources",
      apiVersion: STABLE_API_VERSION,
      name: primaryDataSourceName,
      body: dataSourceDefinition(
        primaryDataSourceName,
        `Managed by sdk-ai-bots deployment; storage=${storageResourceId}; container=knowledge`,
        storageResourceId,
        "knowledge",
      ),
    },
    {
      collection: "datasources",
      apiVersion: STABLE_API_VERSION,
      name: wikiDataSourceName,
      body: dataSourceDefinition(
        wikiDataSourceName,
        `Managed by sdk-ai-bots deployment; storage=${storageResourceId}; container=wiki`,
        storageResourceId,
        "wiki",
      ),
    },
    {
      collection: "skillsets",
      apiVersion: STABLE_API_VERSION,
      name: primarySkillsetName,
      body: skillsetDefinition(
        primarySkillsetName,
        "Skillset to chunk documents and generate embeddings",
        indexName,
        aiEndpoint,
        embeddingModel,
        primaryMappings,
      ),
    },
    {
      collection: "skillsets",
      apiVersion: STABLE_API_VERSION,
      name: wikiSkillsetName,
      body: skillsetDefinition(
        wikiSkillsetName,
        "Chunk + embed LLM wiki pages; project into the shared KB index with page_type.",
        indexName,
        aiEndpoint,
        embeddingModel,
        wikiMappings,
      ),
    },
    {
      collection: "indexers",
      apiVersion: STABLE_API_VERSION,
      name: indexerName,
      body: indexerDefinition(
        indexerName,
        primaryDataSourceName,
        primarySkillsetName,
        indexName,
        "PT1H",
        {
          allowSkillsetToReadFileData: false,
          dataToExtract: "contentAndMetadata",
          markdownHeaderDepth: "h3",
          markdownParsingSubmode: "oneToMany",
          parsingMode: "markdown",
        },
        [
          { sourceFieldName: "metadata_storage_name", targetFieldName: "title" },
          {
            sourceFieldName: "metadata_storage_path",
            targetFieldName: "context_id",
            mappingFunction: {
              name: "extractTokenAtPosition",
              parameters: { delimiter: "/", position: 4 },
            },
          },
        ],
      ),
    },
    {
      collection: "indexers",
      apiVersion: STABLE_API_VERSION,
      name: wikiIndexerName,
      body: indexerDefinition(
        wikiIndexerName,
        wikiDataSourceName,
        wikiSkillsetName,
        indexName,
        "P1D",
        {
          dataToExtract: "contentAndMetadata",
          indexedFileNameExtensions: ".md",
        },
        [
          { sourceFieldName: "title", targetFieldName: "title" },
          { sourceFieldName: "context_id", targetFieldName: "context_id" },
          { sourceFieldName: "page_type", targetFieldName: "page_type" },
        ],
      ),
    },
    {
      collection: "knowledgesources",
      apiVersion: AGENTIC_API_VERSION,
      name: knowledgeSourceName,
      body: {
        name: knowledgeSourceName,
        description: "",
        kind: "searchIndex",
        searchIndexParameters: {
          searchIndexName: indexName,
          searchFields: [],
          sourceDataFields: sourceDataFields(),
        },
      },
    },
    {
      collection: "knowledgebases",
      apiVersion: AGENTIC_API_VERSION,
      name: knowledgeBaseName,
      body: {
        name: knowledgeBaseName,
        description: "",
        retrievalInstructions: "",
        outputMode: "extractiveData",
        knowledgeSources: [{ name: knowledgeSourceName }],
        models: [
          {
            kind: "azureOpenAI",
            azureOpenAIParameters: {
              resourceUri: aiEndpoint,
              deploymentId: knowledgeModel,
              modelName: knowledgeModel,
            },
          },
        ],
        retrievalReasoningEffort: { kind: "minimal" },
      },
    },
  ];
}

function defaultGetSearchAdminKey(options: SearchResourcePlanOptions): string {
  return execFileSync(
    "az",
    [
      "search",
      "admin-key",
      "show",
      "--subscription",
      options.subscriptionId,
      "--resource-group",
      options.resourceGroup,
      "--service-name",
      options.searchServiceName,
      "--query",
      "primaryKey",
      "--output",
      "tsv",
    ],
    { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
  ).trim();
}

function comparableBody(resource: SearchResourceDefinition): JsonObject {
  if (resource.collection !== "datasources") return resource.body;
  const { credentials: _credentials, ...withoutCredentials } = resource.body;
  return withoutCredentials;
}

function isDesiredSubset(desired: unknown, current: unknown): boolean {
  if (Array.isArray(desired)) {
    return (
      Array.isArray(current) &&
      desired.length === current.length &&
      desired.every((value, index) => isDesiredSubset(value, current[index]))
    );
  }
  if (desired !== null && typeof desired === "object") {
    if (current === null || typeof current !== "object" || Array.isArray(current)) return false;
    return Object.entries(desired).every(([key, value]) =>
      isDesiredSubset(value, (current as JsonObject)[key]),
    );
  }
  return Object.is(desired, current);
}

function resourceUrl(
  endpoint: string,
  resource: SearchResourceDefinition,
): string {
  return `${endpoint}/${resource.collection}/${encodeURIComponent(resource.name)}?api-version=${resource.apiVersion}`;
}

async function readCurrent(
  fetchImpl: typeof fetch,
  url: string,
  headers: Record<string, string>,
): Promise<JsonObject | null> {
  const response = await fetchImpl(url, { method: "GET", headers });
  if (response.status === 404) return null;
  if (!response.ok) {
    throw new Error(`Azure AI Search GET failed (${response.status} ${response.statusText}) for ${url}`);
  }
  return (await response.json()) as JsonObject;
}

async function putResource(
  fetchImpl: typeof fetch,
  url: string,
  headers: Record<string, string>,
  body: JsonObject,
): Promise<JsonObject> {
  const response = await fetchImpl(url, {
    method: "PUT",
    headers,
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const details = (await response.text()).trim();
    throw new Error(
      `Azure AI Search PUT failed (${response.status} ${response.statusText}) for ${url}` +
        (details ? `: ${details}` : ""),
    );
  }
  return (await response.json()) as JsonObject;
}

export async function reconcileSearchResources(
  options: ReconcileSearchResourcesOptions,
): Promise<SearchResourceResult[]> {
  const dryRun = options.dryRun ?? true;
  const fetchImpl = options.fetchImpl ?? fetch;
  const log = options.log ?? ((message: string) => console.log(`[setup-search] ${message}`));
  const adminKey =
    options.searchAdminKey ??
    (options.getSearchAdminKey ?? defaultGetSearchAdminKey)(options);
  if (!adminKey) throw new Error("Azure AI Search admin key is empty.");

  const endpoint = `https://${options.searchServiceName}.search.windows.net`;
  const headers = {
    "api-key": adminKey,
    "Content-Type": "application/json",
  };
  const resources = buildSearchResourcePlan(options);
  const results: SearchResourceResult[] = [];

  log(`${dryRun ? "Dry-run" : "Applying"} ${resources.length} Search data-plane resources on ${options.searchServiceName}.`);
  for (const resource of resources) {
    const url = resourceUrl(endpoint, resource);
    const current = await readCurrent(fetchImpl, url, headers);
    const action: SearchResourceAction =
      current === null
        ? "create"
        : isDesiredSubset(comparableBody(resource), current)
          ? "unchanged"
          : "update";

    results.push({ collection: resource.collection, name: resource.name, action });
    log(`  ${action.padEnd(9)} ${resource.collection}/${resource.name}`);
    if (dryRun || action === "unchanged") continue;

    const updated = await putResource(fetchImpl, url, headers, resource.body);
    if (!isDesiredSubset(comparableBody(resource), updated)) {
      throw new Error(`Azure AI Search did not return the desired definition for ${resource.collection}/${resource.name}.`);
    }
  }

  return results;
}