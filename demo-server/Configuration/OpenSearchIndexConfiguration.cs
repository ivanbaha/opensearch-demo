using System.Text.Json;

namespace OpenSearchDemo.Configuration
{
    /// <summary>
    /// Configuration class for OpenSearch index settings and mappings.
    /// This centralizes all index-related configurations for easy maintenance and reuse across projects.
    /// </summary>
    public static class OpenSearchIndexConfiguration
    {
        /// <summary>
        /// The expected dimension for embedding vectors (M2-BERT-Retrieval-32k model)
        /// </summary>
        public const int EmbeddingDimension = 768;

        /// <summary>
        /// Default index name for papers
        /// </summary>
        public const string DefaultIndexName = "papers_v3";

        /// <summary>
        /// Default alias name for papers
        /// </summary>
        public const string DefaultAliasName = "papers";

        /// <summary>
        /// Field mapping properties for the papers index
        /// </summary>
        private static readonly string IndexMappingProperties = @"{
            ""id"": { ""type"": ""keyword"" },
            ""oipubId"": { ""type"": ""keyword"" },
            ""doi"": { ""type"": ""keyword"" },
            ""title"": { 
                ""type"": ""text"",
                ""analyzer"": ""standard"",
                ""fields"": {
                    ""keyword"": { ""type"": ""keyword"" }
                }
            },
            ""abstract"": { 
                ""type"": ""text"",
                ""analyzer"": ""standard""
            },
            ""openSummary"": { 
                ""type"": ""text"",
                ""analyzer"": ""standard""
            },
            ""journal"": { 
                ""type"": ""text"",
                ""fields"": {
                    ""keyword"": { ""type"": ""keyword"" }
                }
            },
            ""publisher"": { 
                ""type"": ""text"",
                ""fields"": {
                    ""keyword"": { ""type"": ""keyword"" }
                }
            },
            ""authors"": { 
                ""type"": ""nested"",
                ""properties"": {
                    ""name"": { ""type"": ""keyword"" },
                    ""ORCID"": { ""type"": ""keyword"" },
                    ""sequence"": { ""type"": ""keyword"" }
                }
            },
            ""publicationDateParts"": { ""type"": ""integer"" },
            ""publicationHotScore"": { ""type"": ""double"", ""null_value"": 0.0 },
            ""publicationHotScore6m"": { ""type"": ""double"", ""null_value"": 0.0 },
            ""pageRank"": { ""type"": ""double"", ""null_value"": 0.0 },
            ""citationsCount"": { ""type"": ""integer"", ""null_value"": 0.0 },
            ""voteScore"": { ""type"": ""integer"", ""null_value"": 0.0 },
            ""topics"": {
                ""type"": ""nested"",
                ""properties"": {
                    ""name"": { ""type"": ""keyword"" },
                    ""relevanceScore"": { ""type"": ""double"", ""null_value"": 0.0 },
                    ""topScore"": { ""type"": ""double"", ""null_value"": 0.0 },
                    ""hotScore"": { ""type"": ""double"", ""null_value"": 0.0 },
                    ""hotScore6m"": { ""type"": ""double"", ""null_value"": 0.0 }
                }
            },
            ""hasAbstract"": {
                ""type"": ""boolean""
            },
            ""hasOpenSummary"": {
                ""type"": ""boolean""
            },
            ""publishedAt"": { ""type"": ""date"", ""null_value"": ""0001-01-01T00:00:00Z"" },
            ""embeddingVector"": {
                ""type"": ""knn_vector"",
                ""dimension"": 768,
                ""method"": {
                    ""name"": ""hnsw"",
                    ""space_type"": ""cosinesimil"",
                    ""engine"": ""lucene"",
                    ""parameters"": {
                        ""ef_construction"": 128,
                        ""m"": 24
                    }
                }
            },
            ""contextualContent"": {
                ""type"": ""text"",
                ""analyzer"": ""standard"",
                ""fields"": {
                    ""english"": {
                        ""type"": ""text"",
                        ""analyzer"": ""english""
                    },
                    ""keyword"": {
                        ""type"": ""keyword""
                    }
                }
            }
        }";

        /// <summary>
        /// Index settings configuration
        /// </summary>
        public static class IndexSettings
        {
            public const int MaxResultWindow = 50000;
            public const int NumberOfShards = 30;
            public const int NumberOfReplicas = 1;
            public const string RefreshInterval = "-1";
            public const bool KnnEnabled = true;
            public const int KnnAlgoParamEfSearch = 128;
        }

        /// <summary>
        /// Vector search configuration
        /// </summary>
        public static class VectorSearchSettings
        {
            public const int EfConstruction = 128;
            public const int MParameter = 24;
            public const string SpaceType = "cosinesimil";
            public const string Engine = "lucene";
            public const string Method = "hnsw";
        }

        /// <summary>
        /// Gets the field mapping properties as a JSON string
        /// </summary>
        /// <returns>JSON string containing the field mappings</returns>
        public static string GetMappingProperties()
        {
            return IndexMappingProperties;
        }

        /// <summary>
        /// Gets the complete index configuration including settings, aliases, and mappings
        /// </summary>
        /// <param name="aliasName">The alias name for the index (defaults to 'papers')</param>
        /// <param name="customSettings">Optional custom settings to override defaults</param>
        /// <returns>Complete index configuration as JSON string</returns>
        public static string GetFullIndexConfiguration(string aliasName = DefaultAliasName, object? customSettings = null)
        {
            var settings = customSettings ?? new
            {
                index = new
                {
                    max_result_window = IndexSettings.MaxResultWindow,
                    number_of_shards = IndexSettings.NumberOfShards,
                    number_of_replicas = IndexSettings.NumberOfReplicas,
                    refresh_interval = IndexSettings.RefreshInterval,
                    knn = IndexSettings.KnnEnabled,
                    knn_algo_param_ef_search = IndexSettings.KnnAlgoParamEfSearch
                }
            };

            var configuration = new
            {
                settings,
                aliases = new Dictionary<string, object>
                {
                    [aliasName] = new { is_write_index = true }
                },
                mappings = new
                {
                    properties = JsonSerializer.Deserialize<object>(IndexMappingProperties)
                }
            };

            return JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Gets index configuration with custom embedding dimension
        /// </summary>
        /// <param name="embeddingDimension">The dimension for embedding vectors</param>
        /// <param name="aliasName">The alias name for the index</param>
        /// <returns>Index configuration with custom embedding dimension</returns>
        public static string GetIndexConfigurationWithCustomEmbeddingDimension(int embeddingDimension, string aliasName = DefaultAliasName)
        {
            // Parse the original mapping properties
            var mappingDict = JsonSerializer.Deserialize<Dictionary<string, object>>(IndexMappingProperties);

            // Update the embedding vector dimension
            if (mappingDict != null && mappingDict.ContainsKey("embeddingVector"))
            {
                var embeddingVector = JsonSerializer.Deserialize<Dictionary<string, object>>(mappingDict["embeddingVector"].ToString()!);
                if (embeddingVector != null)
                {
                    embeddingVector["dimension"] = embeddingDimension;
                    mappingDict["embeddingVector"] = embeddingVector;
                }
            }

            var settings = new
            {
                index = new
                {
                    max_result_window = IndexSettings.MaxResultWindow,
                    number_of_shards = IndexSettings.NumberOfShards,
                    number_of_replicas = IndexSettings.NumberOfReplicas,
                    refresh_interval = IndexSettings.RefreshInterval,
                    knn = IndexSettings.KnnEnabled,
                    knn_algo_param_ef_search = IndexSettings.KnnAlgoParamEfSearch
                }
            };

            var configuration = new
            {
                settings,
                aliases = new Dictionary<string, object>
                {
                    [aliasName] = new { is_write_index = true }
                },
                mappings = new
                {
                    properties = mappingDict
                }
            };

            return JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Gets just the index settings without mappings or aliases
        /// </summary>
        /// <param name="customSettings">Optional custom settings to override defaults</param>
        /// <returns>Index settings as JSON string</returns>
        public static string GetIndexSettings(object? customSettings = null)
        {
            var settings = customSettings ?? new
            {
                index = new
                {
                    max_result_window = IndexSettings.MaxResultWindow,
                    number_of_shards = IndexSettings.NumberOfShards,
                    number_of_replicas = IndexSettings.NumberOfReplicas,
                    refresh_interval = IndexSettings.RefreshInterval,
                    knn = IndexSettings.KnnEnabled,
                    knn_algo_param_ef_search = IndexSettings.KnnAlgoParamEfSearch
                }
            };

            return JsonSerializer.Serialize(new { settings }, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Gets just the mappings configuration
        /// </summary>
        /// <returns>Mappings configuration as JSON string</returns>
        public static string GetMappingsConfiguration()
        {
            var mappings = new
            {
                mappings = new
                {
                    properties = JsonSerializer.Deserialize<object>(IndexMappingProperties)
                }
            };

            return JsonSerializer.Serialize(mappings, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
}
