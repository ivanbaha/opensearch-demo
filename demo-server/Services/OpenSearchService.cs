using OpenSearch.Net;
using System.Text.Json;

namespace OpenSearchDemo.Services
{
    public class OpenSearchService : IOpenSearchService
    {
        private readonly IOpenSearchLowLevelClient _client;
        private readonly ILogger<OpenSearchService> _logger;

        // Centralized mapping definition (copied from sync-console)
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
            ""publishedAt"": { ""type"": ""date"", ""null_value"": ""0001-01-01T00:00:00Z"" },
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
            }
        }";

        private static string GetFullIndexMapping()
        {
            return $@"{{
                ""settings"": {{
                    ""index"": {{
                        ""max_result_window"": 50000,
                        ""number_of_shards"": 30,
                        ""number_of_replicas"": 0,
                        ""refresh_interval"": ""-1""
                    }}
                }},
                ""aliases"": {{
                    ""papers"": {{
                        ""is_write_index"": true
                    }}
                }},
                ""mappings"": {{
                    ""properties"": {IndexMappingProperties}
                }}
            }}";
        }

        public OpenSearchService(IOpenSearchLowLevelClient client, ILogger<OpenSearchService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var pingResponse = await _client.PingAsync<StringResponse>();
                return pingResponse.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenSearch health check failed");
                return false;
            }
        }

        public async Task<object> DemoAsync()
        {
            try
            {
                _logger.LogInformation("Starting OpenSearch demo operation");

                var indexName = "demo-index";
                var document = new { title = "Hello OpenSearch", content = "This is a test document." };

                _logger.LogInformation("Attempting to index document to {IndexName}", indexName);

                // Index a document
                var indexResponse = await _client.IndexAsync<StringResponse>(indexName, PostData.Serializable(document));

                _logger.LogInformation("Index response received. Success: {Success}, Status: {Status}, Body length: {BodyLength}",
                    indexResponse.Success, indexResponse.HttpStatusCode, indexResponse.Body?.Length ?? 0);

                // Check if indexing was successful
                if (!indexResponse.Success)
                {
                    var errorMessage = $"Failed to index document. Status: {indexResponse.HttpStatusCode}. Response: {indexResponse.Body}";

                    // Check for connection issues
                    if (indexResponse.OriginalException != null)
                    {
                        errorMessage += $". Exception: {indexResponse.OriginalException.Message}";
                        _logger.LogError(indexResponse.OriginalException, "Connection error while indexing");
                    }

                    throw new Exception(errorMessage);
                }

                _logger.LogInformation("Document indexed successfully, now searching...");

                // Search for the document
                var searchJson = @"{
                    ""query"": {
                        ""match"": {
                            ""content"": ""test""
                        }
                    }
                }";

                var searchResponse = await _client.SearchAsync<StringResponse>(indexName, searchJson);

                _logger.LogInformation("Search response received. Success: {Success}, Status: {Status}, Body length: {BodyLength}",
                    searchResponse.Success, searchResponse.HttpStatusCode, searchResponse.Body?.Length ?? 0);

                // Check if search was successful
                if (!searchResponse.Success)
                {
                    var errorMessage = $"Failed to search documents. Status: {searchResponse.HttpStatusCode}. Response: {searchResponse.Body}";

                    // Check for connection issues
                    if (searchResponse.OriginalException != null)
                    {
                        errorMessage += $". Exception: {searchResponse.OriginalException.Message}";
                        _logger.LogError(searchResponse.OriginalException, "Connection error while searching");
                    }

                    throw new Exception(errorMessage);
                }

                // Parse responses safely
                object? indexResponseObj = null;
                object? searchResponseObj = null;

                try
                {
                    if (!string.IsNullOrEmpty(indexResponse.Body))
                    {
                        indexResponseObj = JsonSerializer.Deserialize<object>(indexResponse.Body);
                    }
                    else
                    {
                        indexResponseObj = new { message = "Empty response body" };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse index response as JSON");
                    indexResponseObj = new { error = "Failed to parse index response", raw = indexResponse.Body, exception = ex.Message };
                }

                try
                {
                    if (!string.IsNullOrEmpty(searchResponse.Body))
                    {
                        searchResponseObj = JsonSerializer.Deserialize<object>(searchResponse.Body);
                    }
                    else
                    {
                        searchResponseObj = new { message = "Empty response body" };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse search response as JSON");
                    searchResponseObj = new { error = "Failed to parse search response", raw = searchResponse.Body, exception = ex.Message };
                }

                var result = new
                {
                    IndexResponse = indexResponseObj,
                    SearchResponse = searchResponseObj,
                    Metadata = new
                    {
                        IndexSuccess = indexResponse.Success,
                        SearchSuccess = searchResponse.Success,
                        IndexHttpStatusCode = indexResponse.HttpStatusCode,
                        SearchHttpStatusCode = searchResponse.HttpStatusCode,
                        Timestamp = DateTime.UtcNow
                    }
                };

                _logger.LogInformation("Demo operation completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during demo operation");
                throw;
            }
        }

        public async Task<object> CreatePapersIndexAsync()
        {
            try
            {
                var indexName = "papers_v1";
                var indexExistsResponse = await _client.Indices.ExistsAsync<StringResponse>(indexName);

                if (indexExistsResponse.Success)
                {
                    return new { message = "Papers index already exists", indexName };
                }

                _logger.LogInformation("Creating papers index");

                var indexMapping = GetFullIndexMapping();
                var createIndexResponse = await _client.Indices.CreateAsync<StringResponse>(indexName, PostData.String(indexMapping));
                if (!createIndexResponse.Success)
                {
                    throw new Exception($"Failed to create papers index: {createIndexResponse.Body}");
                }

                _logger.LogInformation("Papers index created successfully");
                return new { message = "Papers index created successfully", indexName };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating papers index");
                throw;
            }
        }

        public async Task<object> SyncPapersAsync()
        {
            try
            {
                _logger.LogInformation("Starting papers sync operation");

                // First ensure the index exists
                await CreatePapersIndexAsync();

                return new { message = "Papers sync operation initiated" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during papers sync operation");
                throw;
            }
        }

        public async Task<object> SearchPapersAsync(string? query = null, string? author = null, string? journal = null,
            DateTime? fromDate = null, DateTime? toDate = null, string? topics = null,
            string? sortBy = null, int from = 0, int size = 10)
        {
            try
            {
                _logger.LogInformation("Starting papers search operation");

                var indexName = "papers";
                var topicsArray = !string.IsNullOrEmpty(topics) ? topics.Split(',', StringSplitOptions.RemoveEmptyEntries) : null;

                // Build search query
                var searchQuery = new
                {
                    from,
                    size,
                    track_total_hits = true,
                    query = new
                    {
                        @bool = new
                        {
                            must = new List<object>(),
                            filter = new List<object>()
                        }
                    },
                    sort = new List<object>()
                };

                var mustClauses = (List<object>)searchQuery.query.@bool.must;
                var filterClauses = (List<object>)searchQuery.query.@bool.filter;
                var sortClauses = (List<object>)searchQuery.sort;

                // Add text search if provided
                if (!string.IsNullOrEmpty(query))
                {
                    mustClauses.Add(new
                    {
                        multi_match = new
                        {
                            query,
                            fields = new[] { "title^3", "abstract^2", "authors", "journal" },
                            type = "best_fields",
                            fuzziness = "AUTO"
                        }
                    });
                }

                // Add author filter if provided
                if (!string.IsNullOrEmpty(author))
                {
                    mustClauses.Add(new
                    {
                        match = new
                        {
                            authors = author
                        }
                    });
                }

                // Add journal filter if provided
                if (!string.IsNullOrEmpty(journal))
                {
                    filterClauses.Add(new
                    {
                        term = new
                        {
                            journal = new { value = journal }
                        }
                    });
                }

                // Add publication date range if provided
                if (fromDate.HasValue || toDate.HasValue)
                {
                    var dateRange = new Dictionary<string, object>();
                    if (fromDate.HasValue)
                        dateRange["gte"] = fromDate.Value.ToString("yyyy-MM-dd");
                    if (toDate.HasValue)
                        dateRange["lte"] = toDate.Value.ToString("yyyy-MM-dd");

                    filterClauses.Add(new
                    {
                        range = new
                        {
                            publishedAt = dateRange
                        }
                    });
                }

                // Add topics filter if provided
                if (topicsArray != null && topicsArray.Length > 0)
                {
                    filterClauses.Add(new
                    {
                        nested = new
                        {
                            path = "topics",
                            query = new
                            {
                                terms = new Dictionary<string, object>
                                {
                                    ["topics.name"] = topicsArray
                                }
                            }
                        }
                    });
                }

                // Add sorting
                switch (sortBy?.ToLower())
                {
                    case "hotscore":
                        sortClauses.Add(new { publicationHotScore = new { order = "desc" } });
                        break;
                    case "pagerank":
                        sortClauses.Add(new { pageRank = new { order = "desc" } });
                        break;
                    case "date":
                        sortClauses.Add(new { publishedAt = new { order = "desc" } });
                        break;
                    default:
                        sortClauses.Add(new { _score = new { order = "desc" } });
                        break;
                }

                var searchResponse = await _client.SearchAsync<StringResponse>(indexName, PostData.Serializable(searchQuery));

                if (!searchResponse.Success)
                {
                    throw new Exception($"Search failed: {searchResponse.Body}");
                }

                // Parse response
                object? searchResult = null;
                try
                {
                    searchResult = JsonSerializer.Deserialize<object>(searchResponse.Body);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse search response");
                    searchResult = new { error = "Failed to parse response", raw = searchResponse.Body };
                }

                return new
                {
                    searchParameters = new { query, author, journal, fromDate, toDate, topics = topicsArray, sortBy, from, size },
                    result = searchResult,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during papers search operation");
                throw;
            }
        }

        public async Task<object> ListPapersAsync(int page = 1, int perPage = 10, string sort = "latest")
        {
            try
            {
                _logger.LogInformation("Starting papers list operation");

                var indexName = "papers";

                // Calculate pagination
                var from = (page - 1) * perPage;
                var size = perPage;

                // Build search query
                var searchQuery = new
                {
                    from,
                    size,
                    track_total_hits = true,
                    query = new
                    {
                        @bool = new
                        {
                            must = new object[]
                            {
                                new { match_all = new { } }
                            },
                            filter = new object[]
                            {
                                new
                                {
                                    range = new Dictionary<string, object>
                                    {
                                        ["publishedAt"] = new { lte = DateTime.UtcNow.ToString("yyyy-MM-dd") }
                                    }
                                }
                            }
                        }
                    },
                    sort = new List<object>()
                };

                var sortClauses = (List<object>)searchQuery.sort;

                // Add sorting based on sort parameter
                switch (sort?.ToLower())
                {
                    case "hot":
                        sortClauses.Add(new { publicationHotScore = new { order = "desc" } });
                        break;
                    case "top":
                        sortClauses.Add(new { pageRank = new { order = "desc" } });
                        break;
                    case "latest":
                    default:
                        sortClauses.Add(new { publishedAt = new { order = "desc" } });
                        break;
                }

                var searchResponse = await _client.SearchAsync<StringResponse>(indexName, PostData.Serializable(searchQuery));

                if (!searchResponse.Success)
                {
                    throw new Exception($"List papers failed: {searchResponse.Body}");
                }

                // Parse response
                object? searchResult = null;
                try
                {
                    searchResult = JsonSerializer.Deserialize<object>(searchResponse.Body);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse list papers response");
                    searchResult = new { error = "Failed to parse response", raw = searchResponse.Body };
                }

                return new
                {
                    pagination = new { page, perPage, from, size },
                    sorting = sort,
                    result = searchResult,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during papers list operation");
                throw;
            }
        }

        public async Task<object> ListPapersByTopicAsync(string topicName, int page = 1, int perPage = 10, string sort = "hot")
        {
            try
            {
                _logger.LogInformation("Starting papers by topic list operation for topic: {TopicName}", topicName);

                var indexName = "papers";

                // Calculate pagination
                var from = (page - 1) * perPage;
                var size = perPage;

                // Build search query with nested topic filter and future date exclusion
                var searchQuery = new
                {
                    from,
                    size,
                    track_total_hits = true,
                    query = new
                    {
                        @bool = new
                        {
                            must = new object[]
                            {
                                new
                                {
                                    nested = new
                                    {
                                        path = "topics",
                                        query = new
                                        {
                                            term = new Dictionary<string, object>
                                            {
                                                ["topics.name"] = topicName
                                            }
                                        },
                                        inner_hits = new
                                        {
                                            size = 1,
                                            _source = new[] { "relevanceScore", "topScore", "hotScore" }
                                        }
                                    }
                                }
                            },
                            filter = new object[]
                            {
                                new
                                {
                                    range = new Dictionary<string, object>
                                    {
                                        ["publishedAt"] = new { lte = DateTime.UtcNow.ToString("yyyy-MM-dd") }
                                    }
                                }
                            }
                        }
                    },
                    sort = new List<object>()
                };

                var sortClauses = (List<object>)searchQuery.sort;

                // Add sorting based on sort parameter - topic-specific scores
                switch (sort?.ToLower())
                {
                    case "hot":
                        // Sort by topic hot score using script
                        sortClauses.Add(new
                        {
                            _script = new
                            {
                                type = "number",
                                script = new
                                {
                                    source = @"
                                        if (params._source.topics != null) {
                                            for (topic in params._source.topics) {
                                                if (topic.name == params.topicName) {
                                                    return topic.hotScore != null ? topic.hotScore : 0;
                                                }
                                            }
                                        }
                                        return 0;
                                    ",
                                    @params = new { topicName }
                                },
                                order = "desc"
                            }
                        });
                        break;
                    case "top":
                        // Sort by topic top score using script
                        sortClauses.Add(new
                        {
                            _script = new
                            {
                                type = "number",
                                script = new
                                {
                                    source = @"
                                        if (params._source.topics != null) {
                                            for (topic in params._source.topics) {
                                                if (topic.name == params.topicName) {
                                                    return topic.topScore != null ? topic.topScore : 0;
                                                }
                                            }
                                        }
                                        return 0;
                                    ",
                                    @params = new { topicName }
                                },
                                order = "desc"
                            }
                        });
                        break;
                    case "relevance":
                        // Sort by topic relevance score using script
                        sortClauses.Add(new
                        {
                            _script = new
                            {
                                type = "number",
                                script = new
                                {
                                    source = @"
                                        if (params._source.topics != null) {
                                            for (topic in params._source.topics) {
                                                if (topic.name == params.topicName) {
                                                    return topic.relevanceScore != null ? topic.relevanceScore : 0;
                                                }
                                            }
                                        }
                                        return 0;
                                    ",
                                    @params = new { topicName }
                                },
                                order = "desc"
                            }
                        });
                        break;
                    case "latest":
                        // Sort by publication date
                        sortClauses.Add(new { publishedAt = new { order = "desc" } });
                        break;
                    default:
                        // Default to hot score for topic-specific listing
                        sortClauses.Add(new
                        {
                            _script = new
                            {
                                type = "number",
                                script = new
                                {
                                    source = @"
                                        if (params._source.topics != null) {
                                            for (topic in params._source.topics) {
                                                if (topic.name == params.topicName) {
                                                    return topic.hotScore != null ? topic.hotScore : 0;
                                                }
                                            }
                                        }
                                        return 0;
                                    ",
                                    @params = new { topicName }
                                },
                                order = "desc"
                            }
                        });
                        break;
                }

                var searchResponse = await _client.SearchAsync<StringResponse>(indexName, PostData.Serializable(searchQuery));

                if (!searchResponse.Success)
                {
                    throw new Exception($"List papers by topic failed: {searchResponse.Body}");
                }

                // Parse response
                object? searchResult = null;
                try
                {
                    searchResult = JsonSerializer.Deserialize<object>(searchResponse.Body);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse list papers by topic response");
                    searchResult = new { error = "Failed to parse response", raw = searchResponse.Body };
                }

                return new
                {
                    topicName,
                    pagination = new { page, perPage, from, size },
                    sorting = sort,
                    result = searchResult,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during papers by topic list operation for topic: {TopicName}", topicName);
                throw;
            }
        }

        public async Task IndexDocumentsBatchAsync(string indexName, List<object> documents)
        {
            var bulkBodyLines = new List<string>();

            foreach (var doc in documents)
            {
                // Add index action line (index operation will create or update)
                var indexAction = new { index = new { _index = indexName, _id = ((dynamic)doc).id } };
                bulkBodyLines.Add(JsonSerializer.Serialize(indexAction));

                // Add document line
                bulkBodyLines.Add(JsonSerializer.Serialize(doc));
            }

            // Join with newlines and add final newline as required by OpenSearch
            var bulkBodyString = string.Join("\n", bulkBodyLines) + "\n";

            var bulkResponse = await _client.BulkAsync<StringResponse>(PostData.String(bulkBodyString));

            if (!bulkResponse.Success)
            {
                _logger.LogError("Bulk indexing failed: {Error}", bulkResponse.Body);
                throw new Exception($"Bulk indexing failed: {bulkResponse.Body}");
            }

            // Parse response to get detailed information about operations
            try
            {
                if (!string.IsNullOrEmpty(bulkResponse.Body))
                {
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(bulkResponse.Body);
                    if (responseObj.TryGetProperty("items", out var items))
                    {
                        var created = 0;
                        var updated = 0;
                        var errors = 0;

                        foreach (var item in items.EnumerateArray())
                        {
                            if (item.TryGetProperty("index", out var indexOp))
                            {
                                if (indexOp.TryGetProperty("result", out var result))
                                {
                                    var resultValue = result.GetString();
                                    if (resultValue == "created") created++;
                                    else if (resultValue == "updated") updated++;
                                }
                                if (indexOp.TryGetProperty("error", out var error))
                                {
                                    errors++;
                                    _logger.LogWarning("Indexing error: {Error}", error.ToString());
                                }
                            }
                        }

                        _logger.LogInformation("Bulk indexing completed: {Total} documents ({Created} created, {Updated} updated, {Errors} errors)",
                            documents.Count, created, updated, errors);
                    }
                    else
                    {
                        _logger.LogInformation("Indexed batch of {Count} documents", documents.Count);
                    }
                }
                else
                {
                    _logger.LogInformation("Indexed batch of {Count} documents", documents.Count);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse bulk response, but indexing appears successful");
                _logger.LogInformation("Indexed batch of {Count} documents", documents.Count);
            }
        }

        public async Task<object> DeleteIndexAsync(string indexName)
        {
            try
            {
                _logger.LogInformation("Attempting to delete index: {IndexName}", indexName);

                // Check if index exists first
                var indexExistsResponse = await _client.Indices.ExistsAsync<StringResponse>(indexName);

                if (!indexExistsResponse.Success)
                {
                    _logger.LogWarning("Failed to check if index exists: {IndexName}", indexName);
                    return new
                    {
                        success = false,
                        message = "Failed to check if index exists",
                        indexName,
                        error = indexExistsResponse.Body
                    };
                }

                // If index doesn't exist (404), return appropriate message
                if (indexExistsResponse.HttpStatusCode == 404)
                {
                    _logger.LogInformation("Index does not exist: {IndexName}", indexName);
                    return new
                    {
                        success = false,
                        message = "Index does not exist",
                        indexName
                    };
                }

                // Delete the index
                var deleteResponse = await _client.Indices.DeleteAsync<StringResponse>(indexName);

                if (!deleteResponse.Success)
                {
                    _logger.LogError("Failed to delete index {IndexName}: {Error}", indexName, deleteResponse.Body);
                    return new
                    {
                        success = false,
                        message = "Failed to delete index",
                        indexName,
                        error = deleteResponse.Body,
                        statusCode = deleteResponse.HttpStatusCode
                    };
                }

                _logger.LogInformation("Successfully deleted index: {IndexName}", indexName);

                // Parse the response
                object? deleteResponseObj = null;
                try
                {
                    if (!string.IsNullOrEmpty(deleteResponse.Body))
                    {
                        deleteResponseObj = JsonSerializer.Deserialize<object>(deleteResponse.Body);
                    }
                    else
                    {
                        deleteResponseObj = new { message = "Empty response body" };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse delete response as JSON");
                    deleteResponseObj = new { error = "Failed to parse delete response", raw = deleteResponse.Body };
                }

                return new
                {
                    success = true,
                    message = "Index deleted successfully",
                    indexName,
                    response = deleteResponseObj,
                    statusCode = deleteResponse.HttpStatusCode,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deleting index: {IndexName}", indexName);
                return new
                {
                    success = false,
                    message = "Unexpected error while deleting index",
                    indexName,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        public async Task<object> GetIndexInfoAsync(string indexName)
        {
            try
            {
                _logger.LogInformation("Getting index information for: {IndexName}", indexName);

                // Check if index exists first
                var indexExistsResponse = await _client.Indices.ExistsAsync<StringResponse>(indexName);

                if (!indexExistsResponse.Success)
                {
                    _logger.LogWarning("Failed to check if index exists: {IndexName}", indexName);
                    return new
                    {
                        success = false,
                        message = "Failed to check if index exists",
                        indexName,
                        error = indexExistsResponse.Body
                    };
                }

                // If index doesn't exist (404), return appropriate message
                if (indexExistsResponse.HttpStatusCode == 404)
                {
                    _logger.LogInformation("Index does not exist: {IndexName}", indexName);
                    return new
                    {
                        success = false,
                        message = "Index does not exist",
                        indexName
                    };
                }

                // Get index settings and mappings
                var getIndexResponse = await _client.Indices.GetAsync<StringResponse>(indexName);

                if (!getIndexResponse.Success)
                {
                    _logger.LogError("Failed to get index information for {IndexName}: {Error}", indexName, getIndexResponse.Body);
                    return new
                    {
                        success = false,
                        message = "Failed to get index information",
                        indexName,
                        error = getIndexResponse.Body,
                        statusCode = getIndexResponse.HttpStatusCode
                    };
                }

                // Get index statistics
                var statsResponse = await _client.Indices.StatsAsync<StringResponse>(indexName);
                object? statsResponseObj = null;

                if (statsResponse.Success && !string.IsNullOrEmpty(statsResponse.Body))
                {
                    try
                    {
                        statsResponseObj = JsonSerializer.Deserialize<object>(statsResponse.Body);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse stats response as JSON");
                        statsResponseObj = new { error = "Failed to parse stats response", raw = statsResponse.Body };
                    }
                }

                _logger.LogInformation("Successfully retrieved index information for: {IndexName}", indexName);

                // Parse the main index response
                object? indexResponseObj = null;
                try
                {
                    if (!string.IsNullOrEmpty(getIndexResponse.Body))
                    {
                        indexResponseObj = JsonSerializer.Deserialize<object>(getIndexResponse.Body);
                    }
                    else
                    {
                        indexResponseObj = new { message = "Empty response body" };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse index response as JSON");
                    indexResponseObj = new { error = "Failed to parse index response", raw = getIndexResponse.Body };
                }

                return new
                {
                    success = true,
                    message = "Index information retrieved successfully",
                    indexName,
                    indexInfo = indexResponseObj,
                    statistics = statsResponseObj,
                    statusCode = getIndexResponse.HttpStatusCode,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while getting index information: {IndexName}", indexName);
                return new
                {
                    success = false,
                    message = "Unexpected error while getting index information",
                    indexName,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        public async Task<object> RefreshIndexAsync(string indexName)
        {
            try
            {
                _logger.LogInformation("Refreshing index: {IndexName}", indexName);

                // Check if index exists first
                var indexExistsResponse = await _client.Indices.ExistsAsync<StringResponse>(indexName);
                if (!indexExistsResponse.Success || indexExistsResponse.HttpStatusCode == 404)
                {
                    return new
                    {
                        success = false,
                        message = "Index does not exist",
                        indexName
                    };
                }

                // Refresh the index to make documents searchable immediately
                var refreshResponse = await _client.Indices.RefreshAsync<StringResponse>(indexName);

                if (!refreshResponse.Success)
                {
                    _logger.LogError("Failed to refresh index {IndexName}: {Error}", indexName, refreshResponse.Body);
                    return new
                    {
                        success = false,
                        message = "Failed to refresh index",
                        indexName,
                        error = refreshResponse.Body,
                        statusCode = refreshResponse.HttpStatusCode
                    };
                }

                _logger.LogInformation("Successfully refreshed index: {IndexName}", indexName);

                return new
                {
                    success = true,
                    message = "Index refreshed successfully",
                    indexName,
                    statusCode = refreshResponse.HttpStatusCode,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while refreshing index: {IndexName}", indexName);
                return new
                {
                    success = false,
                    message = "Unexpected error while refreshing index",
                    indexName,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        public async Task<object> CheckDuplicatesAsync(string indexName = "papers")
        {
            try
            {
                _logger.LogInformation("Checking for duplicates in index: {IndexName}", indexName);

                // Check if index exists first
                var indexExistsResponse = await _client.Indices.ExistsAsync<StringResponse>(indexName);
                if (!indexExistsResponse.Success || indexExistsResponse.HttpStatusCode == 404)
                {
                    return new
                    {
                        success = false,
                        message = "Index does not exist",
                        indexName
                    };
                }

                // Use aggregation to find duplicate IDs
                var duplicateQuery = @"{
                    ""size"": 0,
                    ""aggs"": {
                        ""duplicate_ids"": {
                            ""terms"": {
                                ""field"": ""id"",
                                ""min_doc_count"": 2,
                                ""size"": 1000
                            },
                            ""aggs"": {
                                ""document_count"": {
                                    ""value_count"": {
                                        ""field"": ""id""
                                    }
                                }
                            }
                        }
                    }
                }";

                var duplicateResponse = await _client.SearchAsync<StringResponse>(indexName, duplicateQuery);

                if (!duplicateResponse.Success)
                {
                    _logger.LogError("Failed to check duplicates for {IndexName}: {Error}", indexName, duplicateResponse.Body);
                    return new
                    {
                        success = false,
                        message = "Failed to check for duplicates",
                        indexName,
                        error = duplicateResponse.Body
                    };
                }

                // Parse the aggregation response
                object? duplicateResult = null;
                try
                {
                    if (!string.IsNullOrEmpty(duplicateResponse.Body))
                    {
                        duplicateResult = JsonSerializer.Deserialize<object>(duplicateResponse.Body);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse duplicate response as JSON");
                    duplicateResult = new { error = "Failed to parse response", raw = duplicateResponse.Body };
                }

                // Get total document count for context
                var countQuery = @"{
                    ""size"": 0,
                    ""track_total_hits"": true,
                    ""query"": {
                        ""match_all"": {}
                    }
                }";

                var countResponse = await _client.SearchAsync<StringResponse>(indexName, countQuery);
                object? countResult = null;

                if (countResponse.Success && !string.IsNullOrEmpty(countResponse.Body))
                {
                    try
                    {
                        countResult = JsonSerializer.Deserialize<object>(countResponse.Body);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse count response as JSON");
                    }
                }

                _logger.LogInformation("Successfully checked duplicates for index: {IndexName}", indexName);

                return new
                {
                    success = true,
                    message = "Duplicate check completed",
                    indexName,
                    duplicateAnalysis = duplicateResult,
                    totalDocumentCount = countResult,
                    statusCode = duplicateResponse.HttpStatusCode,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking duplicates in index: {IndexName}", indexName);
                return new
                {
                    success = false,
                    message = "Unexpected error while checking duplicates",
                    indexName,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        public async Task<object> CheckDataDistributionAsync(string indexName = "papers")
        {
            try
            {
                _logger.LogInformation("Checking data distribution for index: {IndexName}", indexName);

                // Check if index exists first
                var indexExistsResponse = await _client.Indices.ExistsAsync<StringResponse>(indexName);
                if (!indexExistsResponse.Success || indexExistsResponse.HttpStatusCode == 404)
                {
                    return new
                    {
                        success = false,
                        message = "Index does not exist",
                        indexName
                    };
                }

                // Get cluster health to see overall status
                var clusterHealthResponse = await _client.Cluster.HealthAsync<StringResponse>(indexName);
                object? clusterHealth = null;

                if (clusterHealthResponse.Success)
                {
                    try
                    {
                        clusterHealth = JsonSerializer.Deserialize<object>(clusterHealthResponse.Body);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse cluster health response");
                        clusterHealth = new { error = "Failed to parse response", raw = clusterHealthResponse.Body };
                    }
                }

                // Get cluster state for detailed shard allocation info (limit depth to avoid serialization issues)
                var clusterStateResponse = await _client.Cluster.StateAsync<StringResponse>();

                object? clusterState = null;
                if (clusterStateResponse.Success)
                {
                    try
                    {
                        // Parse and extract only relevant parts to avoid deep nesting issues
                        var fullState = JsonSerializer.Deserialize<JsonElement>(clusterStateResponse.Body);

                        // Extract only the parts we need for distribution analysis
                        var routingTable = fullState.TryGetProperty("routing_table", out var rt) ? rt : new JsonElement();
                        var nodes = fullState.TryGetProperty("nodes", out var n) ? n : new JsonElement();

                        clusterState = new
                        {
                            cluster_name = fullState.TryGetProperty("cluster_name", out var cn) ? cn.GetString() : "unknown",
                            routing_table = ExtractIndexRoutingInfo(routingTable, indexName),
                            nodes = ExtractNodesInfo(nodes),
                            state_uuid = fullState.TryGetProperty("state_uuid", out var su) ? su.GetString() : "unknown"
                        };
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse cluster state response");
                        clusterState = new { error = "Failed to parse cluster state - too complex", message = "Cluster state response too deeply nested" };
                    }
                }
                else
                {
                    clusterState = new { error = "Failed to get cluster state", raw = "Response failed" };
                }

                // Get index statistics for detailed info
                var statsResponse = await _client.Indices.StatsAsync<StringResponse>(indexName);
                object? indexStats = null;

                if (statsResponse.Success)
                {
                    try
                    {
                        indexStats = JsonSerializer.Deserialize<object>(statsResponse.Body);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse index stats response");
                        indexStats = new { error = "Failed to parse response", raw = statsResponse.Body };
                    }
                }

                _logger.LogInformation("Successfully retrieved data distribution for index: {IndexName}", indexName);

                return new
                {
                    success = true,
                    message = "Data distribution analysis completed",
                    indexName,
                    clusterHealth,
                    clusterState,
                    indexStatistics = indexStats,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking data distribution for index: {IndexName}", indexName);
                return new
                {
                    success = false,
                    message = "Unexpected error while checking data distribution",
                    indexName,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        private object ExtractIndexRoutingInfo(JsonElement routingTable, string indexName)
        {
            try
            {
                if (routingTable.TryGetProperty("indices", out var indices) &&
                    indices.TryGetProperty(indexName, out var indexInfo) &&
                    indexInfo.TryGetProperty("shards", out var shards))
                {
                    var shardInfo = new List<object>();

                    foreach (var shardProperty in shards.EnumerateObject())
                    {
                        var shardNumber = shardProperty.Name;
                        var shardArray = shardProperty.Value;

                        foreach (var shard in shardArray.EnumerateArray())
                        {
                            shardInfo.Add(new
                            {
                                shard = shardNumber,
                                primary = shard.TryGetProperty("primary", out var p) ? p.GetBoolean() : false,
                                state = shard.TryGetProperty("state", out var s) ? s.GetString() : "unknown",
                                node = shard.TryGetProperty("node", out var n) ? n.GetString() : "unknown"
                            });
                        }
                    }

                    return new { index = indexName, shards = shardInfo };
                }

                return new { index = indexName, shards = "No routing info found" };
            }
            catch (Exception ex)
            {
                return new { index = indexName, error = ex.Message };
            }
        }

        private object ExtractNodesInfo(JsonElement nodes)
        {
            try
            {
                var nodeList = new List<object>();

                foreach (var nodeProperty in nodes.EnumerateObject())
                {
                    var nodeId = nodeProperty.Name;
                    var nodeInfo = nodeProperty.Value;

                    nodeList.Add(new
                    {
                        id = nodeId,
                        name = nodeInfo.TryGetProperty("name", out var name) ? name.GetString() : "unknown",
                        transport_address = nodeInfo.TryGetProperty("transport_address", out var addr) ? addr.GetString() : "unknown",
                        roles = nodeInfo.TryGetProperty("roles", out var roles) ?
                            roles.EnumerateArray().Select(r => r.GetString()).ToArray() :
                            Array.Empty<string>()
                    });
                }

                return nodeList;
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
    }
}
