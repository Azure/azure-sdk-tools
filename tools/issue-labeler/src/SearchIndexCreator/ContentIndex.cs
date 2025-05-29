// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;

namespace SearchIndexCreator
{
    public class ContentIndex
    {
        private readonly IConfiguration _config;

        public ContentIndex(IConfiguration config)
        {
            _config = config;
        }

        public async Task SetupAndRunIndexer(SearchIndexClient indexClient, SearchIndexerClient indexerClient, AzureOpenAIClient openAIClient)
        {
            // Create an Index  
            Console.WriteLine("Creating/Updating the index...");
            var index = GetSampleIndex();
            await indexClient.CreateOrUpdateIndexAsync(index);
            Console.WriteLine($"Index '{index.Name}' created/updated successfully.");

            //Create a data source
            Console.WriteLine("Creating/Updating the data source...");
            var dataSource = new SearchIndexerDataSourceConnection(
                "search-content-blob",
                SearchIndexerDataSourceType.AzureBlob,
                connectionString: _config["BlobConnectionString"], // "Connection string" indicating to use managed identity
                container: new SearchIndexerDataContainer("search-content-blob")
            )
            {
                DataChangeDetectionPolicy = new HighWaterMarkChangeDetectionPolicy("metadata_storage_last_modified"),
                DataDeletionDetectionPolicy = new NativeBlobSoftDeleteDeletionDetectionPolicy(),
                IndexerPermissionOptions = new List<IndexerPermissionOption>()
            };
            try
            {
                indexerClient.CreateOrUpdateDataSourceConnection(dataSource);
                Console.WriteLine("Data Source Created/Updated!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating/updating data source: {ex.Message}");
            }
            

            //Create a skillset
            Console.WriteLine("Creating/Updating the skillset...");
            var skillset = new SearchIndexerSkillset("search-content-skillset", new List<SearchIndexerSkill>
            {  
                // Add required skills here    
                new SplitSkill(
                    new List<InputFieldMappingEntry>
                    {
                        new InputFieldMappingEntry("text") { Source = "/document/Body" }
                    },
                    new List<OutputFieldMappingEntry>
                    {
                        new OutputFieldMappingEntry("textItems") { TargetName = "pages" }
                    })
                {
                    Context = "/document",
                    TextSplitMode = TextSplitMode.Pages,
                    // 10k because token limits are so high but want to experiment with lower chunking.
                    MaximumPageLength = 1000,
                    PageOverlapLength = 100,
                },
                new AzureOpenAIEmbeddingSkill(
                    new List<InputFieldMappingEntry>
                    {
                        new InputFieldMappingEntry("text") { Source = "/document/pages/*" }
                    },
                    new List<OutputFieldMappingEntry>
                    {
                        new OutputFieldMappingEntry("embedding") { TargetName = "text_vector" }
                    }
                )
                {
                    Context = "/document/pages/*",
                    ResourceUri = new Uri(_config["OpenAIEndpoint"]),
                    ModelName = _config["EmbeddingModelName"],
                    DeploymentName = _config["EmbeddingModelName"]
                }
            })
            {
                IndexProjection = new SearchIndexerIndexProjection(new[]
                {
                    new SearchIndexerIndexProjectionSelector("search-content-index", parentKeyFieldName: "parent_id", sourceContext: "/document/pages/*", mappings: new[]
                    {
                        new InputFieldMappingEntry("text_vector")
                        {
                            Source = "/document/pages/*/text_vector"
                        },
                        new InputFieldMappingEntry("chunk")
                        {
                            Source = "/document/pages/*"
                        },
                        new InputFieldMappingEntry("Id")
                        {
                            Source = "/document/Id"
                        },
                        new InputFieldMappingEntry("Title")
                        {
                            Source = "/document/Title"
                        },
                        new InputFieldMappingEntry("Service")
                        {
                            Source = "/document/Service"
                        },
                        new InputFieldMappingEntry("Category")
                        {
                            Source = "/document/Category"
                        },
                        new InputFieldMappingEntry("Author")
                        {
                            Source = "/document/Author"
                        },
                        new InputFieldMappingEntry("Repository")
                        {
                            Source = "/document/Repository"
                        },
                        new InputFieldMappingEntry("CreatedAt")
                        {
                            Source = "/document/CreatedAt"
                        },
                        new InputFieldMappingEntry("Url")
                        {
                            Source = "/document/Url"
                        },
                        new InputFieldMappingEntry("CodeOwner")
                        {
                            Source = "/document/CodeOwner"
                        },
                        new InputFieldMappingEntry("DocumentType")
                        {
                            Source = "/document/DocumentType"
                        },
                        // Metadata is needed for updating the document (or atleast last modified not sure of the rest)
                        new InputFieldMappingEntry("metadata_storage_last_modified")
                        {
                            Source = "/document/metadata_storage_last_modified"
                        }
                    })
                })
                {
                    Parameters = new SearchIndexerIndexProjectionsParameters
                    {
                        ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
                    }
                }
            };
            await indexerClient.CreateOrUpdateSkillsetAsync(skillset).ConfigureAwait(false);
            Console.WriteLine("Skillset Created/Updated!");
            

            // Create or update the indexer
            Console.WriteLine("Creating/Updating the indexer...");
            var indexer = new SearchIndexer(
                name: "search-content-indexer",
                dataSourceName: dataSource.Name,
                targetIndexName: "search-content-index")
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
            indexerClient.CreateOrUpdateIndexer(indexer);
            Console.WriteLine("Indexer Created/Updated!");
        }

        private SearchIndex GetSampleIndex()
        {
            // Create a sample index with fields
            const string vectorSearchHnswProfile = "issue-vector-profile";
            const string vectorSearchHnswConfig = "issueHnsw";
            const string vectorSearchVectorizer = "issueOpenAIVectorizer";
            const string vectorSearchExhaustiveKnnConfig = "documentExhaustiveKnn";
            const string binaryCompression = "issue-binary-compression";
            const string semanticSearchConfig = "issue-semantic-config";
            const int modelDimensions = 1536;

            var indexName = "search-content-index";
            var index = new SearchIndex(indexName)
            {
                VectorSearch = new()
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
                                DeploymentName = _config["EmbeddingModelName"],
                                ModelName = _config["EmbeddingModelName"]
                            }
                        }
                    },
                    Compressions =
                    {
                        new BinaryQuantizationCompression(binaryCompression)
                    }
                },
                SemanticSearch = new()
                {
                    Configurations =
                    {
                        new SemanticConfiguration(semanticSearchConfig, new()
                        {
                            TitleField = new SemanticField(fieldName: "Title"),
                            ContentFields =
                            {
                                new SemanticField(fieldName: "chunk")
                            },
                            KeywordsFields =
                            {
                                new SemanticField(fieldName: "Service"),
                                new SemanticField(fieldName: "Category")
                            },
                        })
                    },
                },
                Fields =
                {
                    new SearchableField("chunk_id")
                    {
                        IsKey = true,
                        IsFilterable = false,
                        IsSortable = true,
                        IsFacetable = false,
                        AnalyzerName = LexicalAnalyzerName.Keyword
                    },
                    new SearchableField("parent_id")
                    {
                        IsFilterable = true,
                        IsSortable = false,
                        IsFacetable = false
                    },
                    new SearchableField("chunk"),
                    new SearchField("text_vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = modelDimensions,
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
                    // 0 = false, 1 = true
                    // Used numbers to use the magnitude boosting function
                    new SearchField("CodeOwner", SearchFieldDataType.Int32)
                    {
                        IsSearchable = false,
                        IsSortable = false,
                        IsFilterable = true
                    },
                    new SearchField("metadata_storage_last_modified", SearchFieldDataType.DateTimeOffset)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    },
                    new SearchableField("DocumentType")
                    {
                        IsFilterable = true,
                        IsFacetable = true
                    }
                }
            };
            return index;
        }
    }
}
