// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using OpenAI;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using SearchIndexCreator.RepositoryIndexConfigs;

namespace SearchIndexCreator
{
    public class IssueTriageContentIndex
    {
        private readonly IConfiguration _config;
        private readonly string _embeddingModelName;
        private readonly int _vectorDimensions;
        private readonly IRepositoryIndexConfig _repoConfig;

        public IssueTriageContentIndex(IConfiguration config)
        {
            _config = config;
            _embeddingModelName = _config["EmbeddingModelName"];
            _vectorDimensions = _embeddingModelName?.Equals("text-embedding-3-large", StringComparison.OrdinalIgnoreCase) == true
                ? 3072
                : 1536;
            _repoConfig = RepositoryIndexConfigFactory.Create(_config["repo"]);
        }

        /// <summary>
        /// Sets up and runs the indexer.
        /// </summary>
        /// <param name="indexClient">The client to manage the Azure Search index.</param>
        /// <param name="indexerClient">The client to manage the Azure Search indexer.</param>
        /// <param name="openAIClient">The client to interact with OpenAI.</param>
        public async Task SetupAndRunIndexer(SearchIndexClient indexClient, SearchIndexerClient indexerClient, OpenAIClient openAIClient)
        {
            try
            {
                // Create an Index  
                Console.WriteLine("Creating/Updating the index...");
                var index = GetSampleIndex();
                await indexClient.CreateOrUpdateIndexAsync(index);
                Console.WriteLine($"Index '{index.Name}' created/updated successfully.");

                //Create a data source
                Console.WriteLine("Creating/Updating the data source...");
                var dataSource = GetDataSource();
                await indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);
                Console.WriteLine("Data Source Created/Updated!");

                //Create a skillset
                Console.WriteLine("Creating/Updating the skillset...");

                var skillset = GetSkillset(_repoConfig);

                Console.WriteLine($"Using {_repoConfig.DisplayName} skillset...");
                await indexerClient.CreateOrUpdateSkillsetAsync(skillset).ConfigureAwait(false);
                Console.WriteLine("Skillset Created/Updated!");

                // Create or update the indexer
                Console.WriteLine("Creating/Updating the indexer...");
                var indexer = GetSearchIndexer(dataSource, skillset);
                await indexerClient.CreateOrUpdateIndexerAsync(indexer);
                Console.WriteLine("Indexer Created/Updated!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during index setup: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets a sample search index with HNSW alorithm, built in vectorizer, semantic search turned on, compression set up, and all needed fields for issues.
        /// </summary>
        /// <returns>The sample search index.</returns>
        private SearchIndex GetSampleIndex()
        {
            var indexName = _config["IndexName"];

            var index = new SearchIndex(indexName)
            {
                SemanticSearch = CreateSemanticSearch(),
                VectorSearch = CreateVectorSearch(),
                Fields = CreateFields()

            };

            return index;
        }

        private VectorSearch CreateVectorSearch()
        {
            const string vectorSearchHnswProfile = "issue-vector-profile";
            const string vectorSearchHnswConfig = "issueHnsw";
            const string vectorSearchVectorizer = "issueOpenAIVectorizer";
            const string vectorSearchExhaustiveKnnConfig = "documentExhaustiveKnn";
            const string binaryCompression = "issue-binary-compression";

            return new VectorSearch
            {
                Profiles =
                    {
                        new VectorSearchProfile(vectorSearchHnswProfile, vectorSearchHnswConfig)
                        {
                            VectorizerName = vectorSearchVectorizer,
                            CompressionName = binaryCompression
                        },
                    },
                Algorithms =
                    {
                        new HnswAlgorithmConfiguration(vectorSearchHnswConfig),
                        new ExhaustiveKnnAlgorithmConfiguration(vectorSearchExhaustiveKnnConfig)
                    },
                Vectorizers =
                    {
                        new AzureOpenAIVectorizer(vectorSearchVectorizer)
                        {
                            Parameters = new AzureOpenAIVectorizerParameters()
                            {
                                ResourceUri = new Uri(_config["OpenAIEndpoint"]),
                                DeploymentName = _embeddingModelName,
                                ModelName = _embeddingModelName
                            }
                        }
                    },
                Compressions =
                    {
                        new BinaryQuantizationCompression(binaryCompression)
                    }
            };
        }

        private SemanticSearch CreateSemanticSearch()
        {
            const string semanticSearchConfig = "issue-semantic-config";
            return new SemanticSearch
            {
                Configurations =
                    {
                        new SemanticConfiguration(semanticSearchConfig, new()
                        {
                            TitleField = new SemanticField(fieldName: "Title"),
                            ContentFields =
                            {
                                new SemanticField(fieldName: "Chunk")
                            },
                            KeywordsFields =
                            {
                                new SemanticField(fieldName: "Service"),
                                new SemanticField(fieldName: "Category")
                            },
                        })
                    },
                DefaultConfigurationName = semanticSearchConfig,
            };
        }

        private IList<SearchField> CreateFields()
        {
            const string vectorSearchHnswProfile = "issue-vector-profile";

            return new List<SearchField>
            {
                    new SearchableField("ChunkId")
                    {
                        IsKey = true,
                        IsFilterable = false,
                        IsSortable = true,
                        IsFacetable = false,
                        AnalyzerName = LexicalAnalyzerName.Keyword
                    },
                    new SearchableField("ParentId")
                    {
                        IsFilterable = true,
                        IsSortable = false,
                        IsFacetable = false
                    },
                    new SearchableField("Chunk"),
                    new SearchField("TextVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = _vectorDimensions,
                        VectorSearchProfileName = vectorSearchHnswProfile
                    },
                    new SearchField("Id", SearchFieldDataType.String)
                    {
                        IsSearchable = false
                    },
                    new SearchableField("Title"),
                    new SearchableField("Service")
                    {
                        IsFilterable = true
                    },
                    new SearchableField("Category")
                    {
                        IsFilterable = true
                    },
                    new SearchableField("Server")
                    {
                        IsFilterable = true
                    },
                    new SearchableField("Tool")
                    {
                        IsFilterable = true
                    },
                    new SearchField("Author", SearchFieldDataType.String)
                    {
                        IsSearchable = false
                    },
                    new SearchField("Repository", SearchFieldDataType.String)
                    {
                        IsSearchable = false
                    },
                    new SearchField("CreatedAt", SearchFieldDataType.DateTimeOffset)
                    {
                        IsSearchable = false
                    },
                    new SearchField("Url", SearchFieldDataType.String)
                    {
                        IsSearchable = false
                    },
                    new SearchField("CodeOwner", SearchFieldDataType.Int32)
                    {
                        IsSearchable = false,
                        IsSortable = false,
                        IsFilterable = true
                    },
                    new SearchField("MetadataStorageLastModified", SearchFieldDataType.DateTimeOffset)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    },
                    new SearchableField("DocumentType")
                    {
                        IsFilterable = true,
                        IsFacetable = true
                    }
                };
        }

        private SearchIndexerDataSourceConnection GetDataSource()
        {
            return new SearchIndexerDataSourceConnection(
                $@"{_config["ContainerName"]}-blob-datasource",
                SearchIndexerDataSourceType.AzureBlob,
                connectionString: _config["BlobConnectionString"], // "Connection string" indicating to use managed identity
                container: new SearchIndexerDataContainer($@"{_config["ContainerName"]}-blob")
            )
            {
                DataChangeDetectionPolicy = new HighWaterMarkChangeDetectionPolicy("metadata_storage_last_modified"),
                DataDeletionDetectionPolicy = new NativeBlobSoftDeleteDeletionDetectionPolicy(),
                IndexerPermissionOptions = new List<IndexerPermissionOption>()

            };
        }

        private SearchIndexerSkillset GetSkillset(IRepositoryIndexConfig config)
        {
            var mappings = new List<InputFieldMappingEntry>
            {
                new InputFieldMappingEntry("TextVector") { Source = "/document/pages/*/TextVector" },
                new InputFieldMappingEntry("Chunk") { Source = "/document/pages/*" },
                new InputFieldMappingEntry("Id") { Source = "/document/Id" },
                new InputFieldMappingEntry("Title") { Source = "/document/Title" },
                new InputFieldMappingEntry("Author") { Source = "/document/Author" },
                new InputFieldMappingEntry("Repository") { Source = "/document/Repository" },
                new InputFieldMappingEntry("CreatedAt") { Source = "/document/CreatedAt" },
                new InputFieldMappingEntry("Url") { Source = "/document/Url" },
                new InputFieldMappingEntry("CodeOwner") { Source = "/document/CodeOwner" },
                new InputFieldMappingEntry("DocumentType") { Source = "/document/DocumentType" },
                new InputFieldMappingEntry("MetadataStorageLastModified") { Source = "/document/MetadataStorageLastModified" }
            };

            // Add repo-specific field mappings
            mappings.AddRange(config.GetCustomFieldMappings());

            return new SearchIndexerSkillset(
                $"{_config["ContainerName"]}-skillset",
                new List<SearchIndexerSkill>
                {
            new SplitSkill(
                inputs: new List<InputFieldMappingEntry>
                {
                    new InputFieldMappingEntry("text") { Source = "/document/Body" }
                },
                outputs: new List<OutputFieldMappingEntry>
                {
                    new OutputFieldMappingEntry("textItems") { TargetName = "pages" }
                })
            {
                Context = "/document",
                TextSplitMode = TextSplitMode.Pages,
                MaximumPageLength = config.MaxPageLength,
                PageOverlapLength = config.PageOverlapLength
            },
            new AzureOpenAIEmbeddingSkill(
                inputs: new List<InputFieldMappingEntry>
                {
                    new InputFieldMappingEntry("text") { Source = "/document/pages/*" }
                },
                outputs: new List<OutputFieldMappingEntry>
                {
                    new OutputFieldMappingEntry("embedding") { TargetName = "TextVector" }
                })
            {
                Context = "/document/pages/*",
                ResourceUri = new Uri(_config["OpenAIEndpoint"]),
                ModelName = _embeddingModelName,
                DeploymentName = _embeddingModelName
            }
                })
            {
                IndexProjection = new SearchIndexerIndexProjection(new[]
                {
                    new SearchIndexerIndexProjectionSelector(
                        targetIndexName: _config["IndexName"],
                        parentKeyFieldName: "ParentId",
                        sourceContext: "/document/pages/*",
                        mappings: mappings.ToArray())
                })
                {
                    Parameters = new SearchIndexerIndexProjectionsParameters
                    {
                        ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
                    }
                }
            };
        }

        private SearchIndexer GetSearchIndexer(SearchIndexerDataSourceConnection dataSource, SearchIndexerSkillset skillset)
        {
            return new SearchIndexer(
                name: $@"{_config["ContainerName"]}-indexer",
                dataSourceName: dataSource.Name,
                targetIndexName: _config["IndexName"])
            {
                Description = "Indexer to chunk documents, generate embeddings, and add to the index",
                Parameters = new IndexingParameters()
                {
                    IndexingParametersConfiguration = new IndexingParametersConfiguration()
                    {
                        DataToExtract = BlobIndexerDataToExtract.ContentAndMetadata,
                        ParsingMode = BlobIndexerParsingMode.Json
                    }
                },
                SkillsetName = skillset.Name,
                Schedule = new IndexingSchedule(TimeSpan.FromDays(1))

            };
        }
    }
}
