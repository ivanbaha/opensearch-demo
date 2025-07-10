using MongoDB.Bson;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Services
{
    public class PapersService : IPapersService
    {
        private readonly IOpenSearchService _openSearchService;
        private readonly IMongoDbService _mongoDbService;
        private readonly ILogger<PapersService> _logger;

        public PapersService(IOpenSearchService openSearchService, IMongoDbService mongoDbService, ILogger<PapersService> logger)
        {
            _openSearchService = openSearchService;
            _mongoDbService = mongoDbService;
            _logger = logger;
        }

        public async Task<object> SyncPapersAsync()
        {
            try
            {
                _logger.LogInformation("Starting papers sync operation");

                // Ensure the papers index exists
                await _openSearchService.CreatePapersIndexAsync();

                // Get publication statistics documents
                var publicationStatsDocuments = await _mongoDbService.GetPublicationStatsAsync(100);
                _logger.LogInformation("Retrieved {Count} documents from publicationStatistics", publicationStatsDocuments.Count);

                var documentsToIndex = new List<object>();
                var processedCount = 0;

                foreach (var statDoc in publicationStatsDocuments)
                {
                    try
                    {
                        var docId = statDoc["_id"].AsString;

                        // Get corresponding crossref document
                        var crossrefDoc = await _mongoDbService.GetCrossrefDocumentAsync(docId);

                        if (crossrefDoc == null)
                        {
                            _logger.LogWarning("No crossref document found for ID: {DocId}", docId);
                            continue;
                        }

                        // Extract and map data
                        var rawData = crossrefDoc.Contains("raw_data") ? crossrefDoc["raw_data"].AsBsonDocument : new BsonDocument();

                        // Map topics from topicRelatedScores
                        var topics = new List<object>();
                        if (statDoc.Contains("topicRelatedScores") && statDoc["topicRelatedScores"].IsBsonArray)
                        {
                            foreach (var topicScore in statDoc["topicRelatedScores"].AsBsonArray)
                            {
                                var topicDoc = topicScore.AsBsonDocument;
                                topics.Add(new
                                {
                                    name = topicDoc.Contains("name") ? topicDoc["name"].AsString : "",
                                    relevanceScore = topicDoc.Contains("relevanceScore") && !topicDoc["relevanceScore"].IsBsonNull ? topicDoc["relevanceScore"].ToDouble() : 0.0,
                                    topScore = topicDoc.Contains("topScore") && !topicDoc["topScore"].IsBsonNull ? topicDoc["topScore"].ToDouble() : (double?)null,
                                    hotScore = topicDoc.Contains("hotScore") && !topicDoc["hotScore"].IsBsonNull ? topicDoc["hotScore"].ToDouble() : 0.0
                                });
                            }
                        }

                        // Extract publication date
                        DateTime? publishedAt = null;
                        if (crossrefDoc.Contains("publishedAt") && crossrefDoc["publishedAt"].IsBsonDocument)
                        {
                            var pubDate = crossrefDoc["publishedAt"].AsBsonDocument;
                            var year = pubDate.Contains("year") && !pubDate["year"].IsBsonNull ? pubDate["year"].AsInt32 : 1970;
                            var month = pubDate.Contains("month") && !pubDate["month"].IsBsonNull ? pubDate["month"].AsInt32 : 1;
                            var day = pubDate.Contains("day") && !pubDate["day"].IsBsonNull ? pubDate["day"].AsInt32 : 1;
                            publishedAt = new DateTime(year, month, day);
                        }

                        // Extract title
                        var title = "";
                        if (rawData.Contains("title") && rawData["title"].IsBsonArray)
                        {
                            var titleArray = rawData["title"].AsBsonArray;
                            if (titleArray.Count > 0)
                            {
                                title = titleArray[0].AsString;
                            }
                        }

                        // Extract journal
                        var journal = "";
                        if (rawData.Contains("container-title") && rawData["container-title"].IsBsonArray)
                        {
                            var journalArray = rawData["container-title"].AsBsonArray;
                            if (journalArray.Count > 0)
                            {
                                journal = journalArray[0].AsString;
                            }
                        }

                        // Extract publisher
                        var publisher = rawData.Contains("publisher") ? rawData["publisher"].AsString : "";

                        // Extract authors (if available in raw_data)
                        var authors = "";
                        if (rawData.Contains("author") && rawData["author"].IsBsonArray)
                        {
                            var authorsList = new List<string>();
                            foreach (var author in rawData["author"].AsBsonArray)
                            {
                                if (author.IsBsonDocument)
                                {
                                    var authorDoc = author.AsBsonDocument;
                                    var given = authorDoc.Contains("given") ? authorDoc["given"].AsString : "";
                                    var family = authorDoc.Contains("family") ? authorDoc["family"].AsString : "";
                                    if (!string.IsNullOrEmpty(given) || !string.IsNullOrEmpty(family))
                                    {
                                        authorsList.Add($"{given} {family}".Trim());
                                    }
                                }
                            }
                            authors = string.Join(", ", authorsList);
                        }

                        // Create document for OpenSearch
                        var document = new
                        {
                            id = docId,
                            title,
                            @abstract = rawData.Contains("abstract") ? rawData["abstract"].AsString : "",
                            journal,
                            publisher,
                            authors,
                            publicationHotScore = statDoc.Contains("publicationHotScore") && !statDoc["publicationHotScore"].IsBsonNull ? statDoc["publicationHotScore"].ToDouble() : 0.0,
                            publicationHotScore6m = 0.0, // Not available in current data structure
                            pageRank = statDoc.Contains("pageRank") && !statDoc["pageRank"].IsBsonNull ? statDoc["pageRank"].ToDouble() : (double?)null,
                            publishedAt,
                            topics,
                            createdAt = statDoc.Contains("createdAt") ? statDoc["createdAt"].ToUniversalTime() : DateTime.UtcNow,
                            updatedAt = statDoc.Contains("updatedAt") ? statDoc["updatedAt"].ToUniversalTime() : DateTime.UtcNow
                        };

                        documentsToIndex.Add(document);
                        processedCount++;

                        // Index in batches of 1000
                        if (documentsToIndex.Count >= 1000)
                        {
                            await _openSearchService.IndexDocumentsBatchAsync("papers", documentsToIndex);
                            documentsToIndex.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing document with ID: {DocId}", statDoc.Contains("_id") ? statDoc["_id"].AsString : "unknown");
                    }
                }

                // Index remaining documents
                if (documentsToIndex.Count > 0)
                {
                    await _openSearchService.IndexDocumentsBatchAsync("papers", documentsToIndex);
                }

                _logger.LogInformation("Papers sync completed. Processed {ProcessedCount} documents", processedCount);

                return new
                {
                    message = "Papers sync completed successfully",
                    indexName = "papers",
                    documentsProcessed = processedCount,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during papers sync operation");
                throw;
            }
        }
    }
}
