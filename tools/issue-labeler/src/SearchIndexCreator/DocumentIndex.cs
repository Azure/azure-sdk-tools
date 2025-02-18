// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Azure.AI.OpenAI;

namespace SearchIndexCreator
{
    public class DocumentIndex
    {
        private readonly IConfiguration _config;

        public DocumentIndex(IConfiguration config)
        {
            _config = config;
        }


        /// <summary>
        /// Sets up and runs the indexer.
        /// </summary>
        /// <param name="indexClient">The client to manage the search index.</param>
        /// <param name="indexerClient">The client to manage the search indexer.</param>
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
                $"{_config["DocumentIndexName"]}-blob",
                SearchIndexerDataSourceType.AzureBlob,
                connectionString: _config["BlobConnectionString"],
                container: new SearchIndexerDataContainer($"{_config["DocumentIndexName"]}-blob"))
            {
                DataChangeDetectionPolicy = new HighWaterMarkChangeDetectionPolicy("metadata_storage_last_modified"),
                DataDeletionDetectionPolicy = new NativeBlobSoftDeleteDeletionDetectionPolicy()
            };
            indexerClient.CreateOrUpdateDataSourceConnection(dataSource);
            Console.WriteLine("Data Source Created/Updated!");

            // Create a Skillset  
            Console.WriteLine("Creating/Updating the skillset...");
            var skillset = new SearchIndexerSkillset($"{_config["DocumentIndexName"]}-skillset", new List<SearchIndexerSkill>
            {  
                // Add required skills here    
                new SplitSkill(
                    new List<InputFieldMappingEntry>
                    {
                        new InputFieldMappingEntry("text") { Source = "/document/Content" }
                    },
                    new List<OutputFieldMappingEntry>
                    {
                        new OutputFieldMappingEntry("textItems") { TargetName = "pages" }
                    })
                {
                    Context = "/document",
                    TextSplitMode = TextSplitMode.Pages,
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
                    new SearchIndexerIndexProjectionSelector(_config["DocumentIndexName"], parentKeyFieldName: "parent_id", sourceContext: "/document/pages/*", mappings: new[]
                    {
                        new InputFieldMappingEntry("chunk")
                        {
                            Source = "/document/pages/*"
                        },
                        new InputFieldMappingEntry("text_vector")
                        {
                            Source = "/document/pages/*/text_vector"
                        },
                        new InputFieldMappingEntry("Title")
                        {
                            Source = "/document/metadata_storage_name"
                        },
                        new InputFieldMappingEntry("Url")
                        {
                            Source = "/document/Url"
                        },
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

            // Create an Indexer  
            Console.WriteLine("Creating the indexer and running it...");
            var indexer = new SearchIndexer($"{_config["DocumentIndexName"]}-indexer", dataSource.Name, _config["DocumentIndexName"])
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
        /// Gets a sample search index with HNSW alorithm, built in vectorizer, semantic search turned on, compression set up, and all needed fields for documents.
        /// </summary>
        /// <returns>The sample search index.</returns>
        private SearchIndex GetSampleIndex()
        {
            const string vectorSearchHnswProfile = "document-vector-profile";
            const string vectorSearchExhasutiveKnnProfile = "documentExhaustiveKnnProfile";
            const string vectorSearchHnswConfig = "documentHnsw";
            const string vectorSearchExhaustiveKnnConfig = "documentExhaustiveKnn";
            const string vectorSearchVectorizer = "documentOpenAIVectorizer";
            const string semanticSearchConfig = "document-semantic-config";
            const string binaryCompression = "document-binary-compression";
            const int modelDimensions = 1536;

            SearchIndex searchIndex = new SearchIndex(_config["DocumentIndexName"])
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
                        })
                    },
                },
                Fields =
                {
                    new SearchableField("parent_id") { IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new SearchableField("chunk_id") { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Keyword },
                    new SearchableField("Url"),
                    new SearchableField("chunk"),
                    new SearchableField("Title"),
                    new SearchField("text_vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = modelDimensions,
                        VectorSearchProfileName = vectorSearchHnswProfile
                    },
                    new SearchField("metadata_storage_last_modified", SearchFieldDataType.DateTimeOffset)
                    {
                        IsHidden = true,
                    }
                },
            };
            
            return searchIndex;
        }
    }
}
