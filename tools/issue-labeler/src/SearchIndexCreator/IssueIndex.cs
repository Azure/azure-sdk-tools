// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;

namespace SearchIndexCreator
{
    public class IssueIndex
    {
        private readonly IConfiguration _config;

        public IssueIndex(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Sets up and runs the indexer.
        /// </summary>
        /// <param name="indexClient">The client to manage the Azure Search index.</param>
        /// <param name="indexerClient">The client to manage the Azure Search indexer.</param>
        /// <param name="openAIClient">The client to interact with Azure OpenAI.</param>
        public async Task SetupAndRunIndexer(SearchIndexClient indexClient, SearchIndexerClient indexerClient, AzureOpenAIClient openAIClient)
        {
            // Create an Index  
            Console.WriteLine("Creating/Updating the index...");
            var index = GetSampleIndex();
            await indexClient.CreateOrUpdateIndexAsync(index);
            Console.WriteLine("Index Created/Updated!");

            // Create a Data Source Connection  
            Console.WriteLine("Creating/Updating the data source connection...");
            var dataSource = new SearchIndexerDataSourceConnection(
                $"{_config["IssueIndexName"]}-blob",
                SearchIndexerDataSourceType.AzureBlob,
                connectionString: _config["BlobConnectionString"],
                container: new SearchIndexerDataContainer(_config["IssueBlobContainerName"]));
            indexerClient.CreateOrUpdateDataSourceConnection(dataSource);
            Console.WriteLine("Data Source Created/Updated!");

            // Create a Skillset specifically for chunking
            // Each issue has its associated comments attached to it. Not sure if this will change in the future making chunking potentially unnecessary.
            Console.WriteLine("Creating/Updating the skillset...");
            var skillset = new SearchIndexerSkillset($"{_config["IssueIndexName"]}-skillset", new List<SearchIndexerSkill>
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
                    MaximumPageLength = 10000,
                    PageOverlapLength = 500,
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
                    new SearchIndexerIndexProjectionSelector(_config["IssueIndexName"], parentKeyFieldName: "parent_id", sourceContext: "/document/pages/*", mappings: new[]
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
                        // Metadata is needed for updating the document (or atleast last modified not sure of the rest)
                        new InputFieldMappingEntry("metadata_storage_content_type")
                        {
                            Source = "/document/metadata_storage_content_type"
                        },
                        new InputFieldMappingEntry("metadata_storage_size")
                        {
                            Source = "/document/metadata_storage_size"
                        },
                        new InputFieldMappingEntry("metadata_storage_last_modified")
                        {
                            Source = "/document/metadata_storage_last_modified"
                        },
                        new InputFieldMappingEntry("metadata_storage_content_md5")
                        {
                            Source = "/document/metadata_storage_content_md5"
                        },
                        new InputFieldMappingEntry("metadata_storage_path")
                        {
                            Source = "/document/metadata_storage_path"
                        },
                        new InputFieldMappingEntry("metadata_storage_file_extension")
                        {
                            Source = "/document/metadata_storage_file_extension"
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

            // Create an Indexer  
            Console.WriteLine("Creating the indexer and running it...");
            var indexer = new SearchIndexer($"{_config["IssueIndexName"]}-indexer", dataSource.Name, _config["IssueIndexName"])
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
            };
            await indexerClient.CreateOrUpdateIndexerAsync(indexer).ConfigureAwait(false);
            Console.WriteLine("Indexer Created/Updated!");
        }

        /// <summary>
        /// Gets a sample search index with HNSW alorithm, built in vectorizer, semantic search turned on, compression set up, and all needed fields for issues.
        /// </summary>
        /// <returns>The sample search index.</returns>
        private SearchIndex GetSampleIndex()
        {
            const string vectorSearchHnswProfile = "issue-vector-profile";
            const string vectorSearchHnswConfig = "issueHnsw";
            const string vectorSearchVectorizer = "issueOpenAIVectorizer";
            const string semanticSearchConfig = "issue-semantic-config";
            const string binaryCompression = "issue-binary-compression";
            const int modelDimensions = 1536;// "Default" value

            SearchIndex searchIndex = new SearchIndex(_config["IssueIndexName"])
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
                    new SearchableField("Service"),
                    new SearchableField("Category"),
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
                    new SearchField("metadata_storage_content_type", SearchFieldDataType.String)
                    {
                        IsHidden = true,
                    },
                    new SearchField("metadata_storage_size", SearchFieldDataType.Int64)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    },
                    new SearchField("metadata_storage_last_modified", SearchFieldDataType.DateTimeOffset)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    },
                    new SearchField("metadata_storage_content_md5", SearchFieldDataType.String)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    },
                    new SearchField("metadata_storage_name", SearchFieldDataType.String)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    },
                    new SearchField("metadata_storage_path", SearchFieldDataType.String)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    },
                    new SearchField("metadata_storage_file_extension", SearchFieldDataType.String)
                    {
                        IsHidden = true,
                        IsSearchable = false
                    }
                }
            };

            return searchIndex;
        }
    }
}
