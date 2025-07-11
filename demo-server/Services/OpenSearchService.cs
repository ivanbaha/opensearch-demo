using OpenSearch.Net;
using System.Text.Json;

namespace OpenSearchDemo.Services
{
    public class OpenSearchService : IOpenSearchService
    {
        private readonly IOpenSearchLowLevelClient _client;
        private readonly ILogger<OpenSearchService> _logger;

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
                var indexName = "papers";
                var indexExistsResponse = await _client.Indices.ExistsAsync<StringResponse>(indexName);

                if (indexExistsResponse.Success)
                {
                    return new { message = "Papers index already exists", indexName };
                }

                _logger.LogInformation("Creating papers index");

                var indexMapping = @"{
                    ""settings"": {
                        ""index"": {
                            ""max_result_window"": 1000000000,
                            ""number_of_shards"": 10,
                            ""number_of_replicas"": 1,
                            ""refresh_interval"": ""60s""
                        }
                    },
                    ""mappings"": {
                        ""properties"": {
                            ""id"": { ""type"": ""keyword"" },
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
                                ""type"": ""text"",
                                ""analyzer"": ""standard"",
                                ""fields"": {
                                    ""keyword"": { ""type"": ""keyword"" }
                                }
                            },
                            ""publicationHotScore"": { ""type"": ""double"" },
                            ""publicationHotScore6m"": { ""type"": ""double"" },
                            ""pageRank"": { ""type"": ""double"" },
                            ""publishedAt"": { ""type"": ""date"" },
                            ""topics"": {
                                ""type"": ""nested"",
                                ""properties"": {
                                    ""name"": { ""type"": ""keyword"" },
                                    ""relevanceScore"": { ""type"": ""double"" },
                                    ""topScore"": { ""type"": ""double"" },
                                    ""hotScore"": { ""type"": ""double"" }
                                }
                            },
                            ""createdAt"": { ""type"": ""date"" },
                            ""updatedAt"": { ""type"": ""date"" }
                        }
                    }
                }";

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
                    query = new
                    {
                        match_all = new { }
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

        public async Task IndexDocumentsBatchAsync(string indexName, List<object> documents)
        {
            var bulkBodyLines = new List<string>();

            foreach (var doc in documents)
            {
                // Add index action line
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

            _logger.LogInformation("Indexed batch of {Count} documents", documents.Count);
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
    }
}
