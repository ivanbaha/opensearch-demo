using OpenSearchDemo.Configuration;
using System.Text.Json;

namespace OpenSearchDemo.Examples
{
    /// <summary>
    /// Example usage of the OpenSearch Index Configuration
    /// This demonstrates how to use the centralized configuration in different scenarios
    /// </summary>
    public static class ConfigurationUsageExamples
    {
        /// <summary>
        /// Example 1: Basic index creation with default configuration
        /// </summary>
        public static string CreateDefaultIndex()
        {
            // Get the full index configuration with all defaults
            var config = OpenSearchIndexConfiguration.GetFullIndexConfiguration();

            Console.WriteLine("Index Name: " + OpenSearchIndexConfiguration.DefaultIndexName);
            Console.WriteLine("Alias Name: " + OpenSearchIndexConfiguration.DefaultAliasName);
            Console.WriteLine("Embedding Dimension: " + OpenSearchIndexConfiguration.EmbeddingDimension);

            return config;
        }

        /// <summary>
        /// Example 2: Creating index configuration for a different domain (e.g., products instead of papers)
        /// </summary>
        public static string CreateProductsIndex()
        {
            // Use custom alias name for products domain
            var config = OpenSearchIndexConfiguration.GetFullIndexConfiguration("products");
            return config;
        }

        /// <summary>
        /// Example 3: Creating index with custom embedding dimension for different AI models
        /// </summary>
        public static string CreateIndexWithCustomEmbeddings()
        {
            // Example: Using OpenAI's text-embedding-ada-002 which has 1536 dimensions
            var config = OpenSearchIndexConfiguration.GetIndexConfigurationWithCustomEmbeddingDimension(1536, "papers_openai");
            return config;
        }

        /// <summary>
        /// Example 4: Creating index with custom performance settings
        /// </summary>
        public static string CreateHighPerformanceIndex()
        {
            // Custom settings for high-volume production environment
            var customSettings = new
            {
                index = new
                {
                    max_result_window = 100000,       // Higher result window
                    number_of_shards = 50,            // More shards for better distribution
                    number_of_replicas = 2,           // More replicas for high availability
                    refresh_interval = "5s",          // Faster refresh for near real-time
                    knn = true,
                    knn_algo_param_ef_search = 256    // Higher ef_search for better recall
                }
            };

            var config = OpenSearchIndexConfiguration.GetFullIndexConfiguration("papers_prod", customSettings);
            return config;
        }

        /// <summary>
        /// Example 5: Development environment with optimized settings
        /// </summary>
        public static string CreateDevelopmentIndex()
        {
            // Lightweight settings for development
            var devSettings = new
            {
                index = new
                {
                    max_result_window = OpenSearchIndexConfiguration.IndexSettings.MaxResultWindow,
                    number_of_shards = 1,             // Single shard for dev
                    number_of_replicas = 0,           // No replicas for dev
                    refresh_interval = "1s",          // Fast refresh for testing
                    knn = true,
                    knn_algo_param_ef_search = 64     // Lower ef_search for faster indexing
                }
            };

            var config = OpenSearchIndexConfiguration.GetFullIndexConfiguration("papers_dev", devSettings);
            return config;
        }

        /// <summary>
        /// Example 6: Getting individual configuration components
        /// </summary>
        public static void ShowIndividualComponents()
        {
            // Get just the field mappings
            var mappings = OpenSearchIndexConfiguration.GetMappingsConfiguration();
            Console.WriteLine("Mappings only:");
            Console.WriteLine(mappings);
            Console.WriteLine();

            // Get just the index settings
            var settings = OpenSearchIndexConfiguration.GetIndexSettings();
            Console.WriteLine("Settings only:");
            Console.WriteLine(settings);
            Console.WriteLine();

            // Get raw mapping properties (for custom index creation)
            var properties = OpenSearchIndexConfiguration.GetMappingProperties();
            Console.WriteLine("Raw mapping properties:");
            Console.WriteLine(properties);
        }

        /// <summary>
        /// Example 7: Accessing configuration constants
        /// </summary>
        public static void ShowConfigurationConstants()
        {
            Console.WriteLine("Default Values:");
            Console.WriteLine($"  Default Index Name: {OpenSearchIndexConfiguration.DefaultIndexName}");
            Console.WriteLine($"  Default Alias Name: {OpenSearchIndexConfiguration.DefaultAliasName}");
            Console.WriteLine($"  Embedding Dimension: {OpenSearchIndexConfiguration.EmbeddingDimension}");
            Console.WriteLine();

            Console.WriteLine("Index Settings:");
            Console.WriteLine($"  Max Result Window: {OpenSearchIndexConfiguration.IndexSettings.MaxResultWindow}");
            Console.WriteLine($"  Number of Shards: {OpenSearchIndexConfiguration.IndexSettings.NumberOfShards}");
            Console.WriteLine($"  Number of Replicas: {OpenSearchIndexConfiguration.IndexSettings.NumberOfReplicas}");
            Console.WriteLine($"  Refresh Interval: {OpenSearchIndexConfiguration.IndexSettings.RefreshInterval}");
            Console.WriteLine($"  KNN Enabled: {OpenSearchIndexConfiguration.IndexSettings.KnnEnabled}");
            Console.WriteLine($"  KNN EF Search: {OpenSearchIndexConfiguration.IndexSettings.KnnAlgoParamEfSearch}");
            Console.WriteLine();

            Console.WriteLine("Vector Search Settings:");
            Console.WriteLine($"  EF Construction: {OpenSearchIndexConfiguration.VectorSearchSettings.EfConstruction}");
            Console.WriteLine($"  M Parameter: {OpenSearchIndexConfiguration.VectorSearchSettings.MParameter}");
            Console.WriteLine($"  Space Type: {OpenSearchIndexConfiguration.VectorSearchSettings.SpaceType}");
            Console.WriteLine($"  Engine: {OpenSearchIndexConfiguration.VectorSearchSettings.Engine}");
            Console.WriteLine($"  Method: {OpenSearchIndexConfiguration.VectorSearchSettings.Method}");
        }

        /// <summary>
        /// Example 8: Cross-project reuse scenario
        /// </summary>
        public static string CreateNewsArticlesIndex()
        {
            // Reusing the same field mappings but for news articles instead of academic papers
            // The mappings work well for any document with title, abstract, authors, topics, etc.

            var newsSettings = new
            {
                index = new
                {
                    max_result_window = 25000,        // Medium result window
                    number_of_shards = 15,            // Moderate sharding
                    number_of_replicas = 1,           // Standard replication
                    refresh_interval = "10s",         // Moderate refresh rate
                    knn = true,
                    knn_algo_param_ef_search = 128    // Balanced search performance
                }
            };

            var config = OpenSearchIndexConfiguration.GetFullIndexConfiguration("news_articles", newsSettings);
            return config;
        }

        /// <summary>
        /// Example 9: Pretty printing configuration for debugging
        /// </summary>
        public static void PrintPrettyConfiguration()
        {
            var config = OpenSearchIndexConfiguration.GetFullIndexConfiguration();

            // Parse and re-serialize with pretty formatting
            var jsonDocument = JsonDocument.Parse(config);
            var prettyConfig = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Console.WriteLine("Pretty-printed configuration:");
            Console.WriteLine(prettyConfig);
        }
    }
}
