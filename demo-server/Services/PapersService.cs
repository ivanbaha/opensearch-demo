using MongoDB.Bson;
using OpenSearchDemo.Services;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace OpenSearchDemo.Services
{
    public class PapersService : IPapersService
    {
        private readonly IOpenSearchService _openSearchService;
        private readonly IMongoDbService _mongoDbService;
        private readonly ITogetherAIService _togetherAIService;
        private readonly ILogger<PapersService> _logger;

        public PapersService(IOpenSearchService openSearchService, IMongoDbService mongoDbService, ITogetherAIService togetherAIService, ILogger<PapersService> logger)
        {
            _openSearchService = openSearchService;
            _mongoDbService = mongoDbService;
            _togetherAIService = togetherAIService;
            _logger = logger;
        }

        public async Task<object> SyncPapersAsync(int size = 1000)
        {
            try
            {
                var totalStartTime = DateTime.UtcNow;
                _logger.LogInformation("Starting papers sync operation with size limit: {Size}", size);

                // Ensure the papers index exists
                await _openSearchService.CreatePapersIndexAsync();

                // Get publication statistics documents with timing
                var mongoStartTime = DateTime.UtcNow;
                var publicationStatsDocuments = await _mongoDbService.GetPublicationStatsAsync(size);
                var mongoEndTime = DateTime.UtcNow;
                var mongoRetrievalTime = mongoEndTime - mongoStartTime;

                _logger.LogInformation("Retrieved {Count} documents from MongoDB in {Duration}ms",
                    publicationStatsDocuments.Count, mongoRetrievalTime.TotalMilliseconds);

                var documentsToIndex = new List<object>();
                var processedCount = 0;
                var openSearchStartTime = DateTime.UtcNow;

                // Extract all IDs for bulk crossref lookup
                var docIds = publicationStatsDocuments.Select(doc => doc["_id"].AsString).ToList();

                _logger.LogInformation("Starting bulk retrieval of {Count} crossref documents", docIds.Count);
                var crossrefBulkStartTime = DateTime.UtcNow;

                // Get all crossref documents in bulk
                var crossrefDocuments = await _mongoDbService.GetCrossrefDocumentsBulkAsync(docIds);

                var crossrefBulkEndTime = DateTime.UtcNow;
                var crossrefBulkTime = crossrefBulkEndTime - crossrefBulkStartTime;

                _logger.LogInformation("Bulk retrieved {Found} crossref documents in {Duration}ms",
                    crossrefDocuments.Count, crossrefBulkTime.TotalMilliseconds);

                // Process all documents with bulk data and bulk embeddings
                var documentMappings = new List<(BsonDocument statDoc, BsonDocument crossrefDoc, string docId, string contextualContent, int index)>();

                // First pass: Map documents without embeddings
                foreach (var statDoc in publicationStatsDocuments)
                {
                    try
                    {
                        var docId = statDoc["_id"].AsString;

                        // Get corresponding crossref document from bulk result
                        if (!crossrefDocuments.TryGetValue(docId, out var crossrefDoc))
                        {
                            _logger.LogWarning("No crossref document found for ID: {DocId}", docId);
                            continue;
                        }

                        // Map the document using helper method (without embedding)
                        var (document, contextualContent) = MapDocumentForOpenSearchWithoutEmbedding(statDoc, crossrefDoc, docId);

                        // Skip papers without title and abstract
                        if (string.IsNullOrWhiteSpace(document.GetType().GetProperty("title")?.GetValue(document) as string) &&
                            string.IsNullOrWhiteSpace(document.GetType().GetProperty("abstract")?.GetValue(document) as string))
                        {
                            _logger.LogWarning("The paper '{DocId}' does not have neither title nor abstract.", docId);
                            continue;
                        }

                        documentMappings.Add((statDoc, crossrefDoc, docId, contextualContent, documentsToIndex.Count));
                        // Add placeholder to maintain index alignment
                        documentsToIndex.Add(null!);
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing document with ID: {DocId}", statDoc.Contains("_id") ? statDoc["_id"].AsString : "unknown");
                    }
                }

                // Second pass: Generate embeddings in bulk
                if (documentMappings.Count > 0)
                {
                    try
                    {
                        _logger.LogInformation("Generating {Count} embeddings in bulk for papers sync", documentMappings.Count);
                        var contextualTexts = documentMappings.Select(m => m.contextualContent).ToArray();
                        var embeddings = await _togetherAIService.GenerateBulkEmbeddingsAsync(contextualTexts);

                        // Assign embeddings back to documents
                        for (int i = 0; i < Math.Min(embeddings.Length, documentMappings.Count); i++)
                        {
                            var docIndex = documentMappings[i].index;
                            var (statDoc, crossrefDoc, docId, _, index) = documentMappings[i];
                            var embedding = embeddings[i];

                            // Create new document with embedding instead of modifying existing one
                            var newDocument = MapDocumentForOpenSearchWithEmbedding(statDoc, crossrefDoc, docId, embedding);
                            documentsToIndex[index] = newDocument;
                        }

                        _logger.LogInformation("Successfully assigned {Count} embeddings to documents", embeddings.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate bulk embeddings for papers sync, falling back to zero vectors");

                        // Fallback: assign zero vectors to all documents
                        var zeroVector = new float[768];
                        foreach (var (statDoc, crossrefDoc, docId, _, index) in documentMappings)
                        {
                            var newDocument = MapDocumentForOpenSearchWithEmbedding(statDoc, crossrefDoc, docId, zeroVector);
                            documentsToIndex[index] = newDocument;
                        }
                    }
                }

                // Index all documents in batches
                var batchSize = 1000;
                for (int i = 0; i < documentsToIndex.Count; i += batchSize)
                {
                    var batch = documentsToIndex.Skip(i).Take(batchSize).ToList();
                    await _openSearchService.IndexDocumentsBatchAsync("papers_v2", batch);
                }
                if (documentsToIndex.Count > 0)
                {
                    await _openSearchService.IndexDocumentsBatchAsync("papers_v2", documentsToIndex);
                }

                var openSearchEndTime = DateTime.UtcNow;
                var openSearchIndexingTime = openSearchEndTime - openSearchStartTime;
                var totalEndTime = DateTime.UtcNow;
                var totalProcessingTime = totalEndTime - totalStartTime;

                _logger.LogInformation("OpenSearch indexing completed in {Duration}ms", openSearchIndexingTime.TotalMilliseconds);
                _logger.LogInformation("Papers sync completed. Processed {ProcessedCount} documents in {TotalDuration}ms",
                    processedCount, totalProcessingTime.TotalMilliseconds);

                return new
                {
                    message = "Papers sync completed successfully",
                    indexName = "papers",
                    documentsProcessed = processedCount,
                    timing = new
                    {
                        mongoRetrievalTimeMs = mongoRetrievalTime.TotalMilliseconds,
                        crossrefBulkRetrievalTimeMs = crossrefBulkTime.TotalMilliseconds,
                        openSearchIndexingTimeMs = openSearchIndexingTime.TotalMilliseconds,
                        totalProcessingTimeMs = totalProcessingTime.TotalMilliseconds
                    },
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during papers sync operation");
                throw;
            }
        }

        private async Task<object> MapDocumentForOpenSearchAsync(BsonDocument statDoc, BsonDocument crossrefDoc, string docId)
        {
            // Extract and map data
            var rawData = crossrefDoc.Contains("raw_data") ? crossrefDoc["raw_data"].AsBsonDocument : [];

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
                        topScore = topicDoc.Contains("topScore") && !topicDoc["topScore"].IsBsonNull ? topicDoc["topScore"].ToDouble() : 0.0,
                        hotScore = topicDoc.Contains("hotScore") && !topicDoc["hotScore"].IsBsonNull ? topicDoc["hotScore"].ToDouble() : 0.0,
                        hotScore6m = topicDoc.Contains("hotScore_6m") && !topicDoc["hotScore_6m"].IsBsonNull ? topicDoc["hotScore_6m"].ToDouble() : 0.0,
                    });
                }
            }

            // Extract publication date
            DateTime? publishedAt = null;
            var publicationDateParts = new List<int>();
            if (crossrefDoc.Contains("publishedAt") && crossrefDoc["publishedAt"].IsBsonDocument)
            {
                var pubDate = crossrefDoc["publishedAt"].AsBsonDocument;

                if (pubDate.Contains("year") && !pubDate["year"].IsBsonNull)
                {
                    var year = pubDate["year"].AsInt32;
                    var month = pubDate.Contains("month") && !pubDate["month"].IsBsonNull ? pubDate["month"].AsInt32 : 1;
                    var day = pubDate.Contains("day") && !pubDate["day"].IsBsonNull ? pubDate["day"].AsInt32 : 1;
                    publishedAt = new DateTime(year, month, day);

                    publicationDateParts.Add(year);
                    if (pubDate.Contains("month") && !pubDate["month"].IsBsonNull)
                    {
                        publicationDateParts.Add(month);
                    }
                    if (pubDate.Contains("day") && !pubDate["day"].IsBsonNull)
                    {
                        publicationDateParts.Add(day);
                    }
                }
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

            // Extract abstract
            var abstractText = rawData.Contains("abstract") ? rawData["abstract"].AsString : "";

            // Extract openSummary (placeholder for now)
            var openSummary = "";

            // Create contextual content by combining title, abstract, and openSummary
            var contextualContent = string.Join(" ", new[] { title, abstractText, openSummary }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            // Create full text content for search (same as contextual content for now)
            var fullTextContent = contextualContent;

            // Generate embedding for the contextual content
            float[] embeddingVector;
            try
            {
                embeddingVector = await _togetherAIService.GenerateEmbeddingAsync(contextualContent);
                _logger.LogDebug("Generated embedding vector for document {DocId}", docId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for document {DocId}, using zero vector", docId);
                embeddingVector = new float[768]; // Use zero vector as fallback
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
            var authors = new List<object>(); ;
            if (rawData.Contains("author") && rawData["author"].IsBsonArray)
            {
                foreach (var author in rawData["author"].AsBsonArray)
                {
                    if (author.IsBsonDocument)
                    {
                        var authorDoc = author.AsBsonDocument;
                        var given = authorDoc.Contains("given") ? authorDoc["given"].AsString : "";
                        var family = authorDoc.Contains("family") ? authorDoc["family"].AsString : "";
                        if (!string.IsNullOrEmpty(given) || !string.IsNullOrEmpty(family))
                        {
                            authors.Add(new
                            {
                                name = $"{given} {family}".Trim(),
                                ORCID = authorDoc.Contains("ORCID") ? authorDoc["ORCID"].AsString : "",
                                sequence = authorDoc.Contains("sequence") ? authorDoc["sequence"].AsString : "",
                            });
                        }
                    }
                }
            }

            // Create document for OpenSearch
            return new
            {
                id = docId,
                oipubId = 0, // Add oipubId field
                doi = rawData.Contains("DOI") ? rawData["DOI"].AsString : docId, // Use DOI from raw_data, fallback to docId
                title,
                @abstract = abstractText,
                openSummary, // Add openSummary field
                fullTextContent,
                embeddingVector,
                contextualContent,
                journal,
                publisher,
                authors,
                publishedAt = publishedAt ?? new DateTime(1, 1, 1), // Handle null values
                publicationDateParts,
                publicationHotScore = statDoc.Contains("publicationHotScore") && !statDoc["publicationHotScore"].IsBsonNull ? statDoc["publicationHotScore"].ToDouble() : 0.0,
                publicationHotScore6m = statDoc.Contains("publicationHotScore_6m") && !statDoc["publicationHotScore_6m"].IsBsonNull ? statDoc["publicationHotScore_6m"].ToDouble() : 0.0,
                pageRank = statDoc.Contains("pageRank") && !statDoc["pageRank"].IsBsonNull ? statDoc["pageRank"].ToDouble() : 0.0,
                citationsCount = rawData.Contains("is-referenced-by-count") ? rawData["is-referenced-by-count"].AsInt32 : 0, // Add citationsCount field
                voteScore = 0, // Add voteScore field (placeholder)
                topics,
            };
        }

        private object MapDocumentForOpenSearchWithEmbedding(BsonDocument statDoc, BsonDocument crossrefDoc, string docId, float[] embeddingVector)
        {
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
                        topScore = topicDoc.Contains("topScore") && !topicDoc["topScore"].IsBsonNull ? topicDoc["topScore"].ToDouble() : 0.0,
                        hotScore = topicDoc.Contains("hotScore") && !topicDoc["hotScore"].IsBsonNull ? topicDoc["hotScore"].ToDouble() : 0.0,
                        hotScore6m = topicDoc.Contains("hotScore_6m") && !topicDoc["hotScore_6m"].IsBsonNull ? topicDoc["hotScore_6m"].ToDouble() : 0.0,
                    });
                }
            }

            // Extract publication date
            DateTime? publishedAt = null;
            var publicationDateParts = new List<int>();
            if (crossrefDoc.Contains("publishedAt") && crossrefDoc["publishedAt"].IsBsonDocument)
            {
                var pubDate = crossrefDoc["publishedAt"].AsBsonDocument;

                if (pubDate.Contains("year") && !pubDate["year"].IsBsonNull)
                {
                    var year = pubDate["year"].AsInt32;
                    var month = pubDate.Contains("month") && !pubDate["month"].IsBsonNull ? pubDate["month"].AsInt32 : 1;
                    var day = pubDate.Contains("day") && !pubDate["day"].IsBsonNull ? pubDate["day"].AsInt32 : 1;
                    publishedAt = new DateTime(year, month, day);

                    publicationDateParts.Add(year);
                    if (pubDate.Contains("month") && !pubDate["month"].IsBsonNull)
                    {
                        publicationDateParts.Add(month);
                    }
                    if (pubDate.Contains("day") && !pubDate["day"].IsBsonNull)
                    {
                        publicationDateParts.Add(day);
                    }
                }
            }

            // Extract title
            var title = "";
            if (rawData.Contains("title") && rawData["title"].IsBsonArray)
            {
                var titleArray = rawData["title"].AsBsonArray;
                if (titleArray.Count > 0)
                {
                    title = CleanTextContent(titleArray[0].AsString);
                }
            }

            // Extract abstract
            var abstractText = CleanTextContent(rawData.Contains("abstract") ? rawData["abstract"].AsString : "");

            // Extract openSummary (placeholder for now)
            var openSummary = CleanTextContent("");

            // Create contextual content by combining title, abstract, and openSummary
            var contextualContent = string.Join(" ", new[] { title, abstractText, openSummary }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            // Create full text content for search (same as contextual content for now)
            var fullTextContent = contextualContent;

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
            var authors = new List<object>();
            if (rawData.Contains("author") && rawData["author"].IsBsonArray)
            {
                foreach (var author in rawData["author"].AsBsonArray)
                {
                    if (author.IsBsonDocument)
                    {
                        var authorDoc = author.AsBsonDocument;
                        var given = authorDoc.Contains("given") ? authorDoc["given"].AsString : "";
                        var family = authorDoc.Contains("family") ? authorDoc["family"].AsString : "";
                        if (!string.IsNullOrEmpty(given) || !string.IsNullOrEmpty(family))
                        {
                            authors.Add(new
                            {
                                name = $"{given} {family}".Trim(),
                                ORCID = authorDoc.Contains("ORCID") ? authorDoc["ORCID"].AsString : "",
                                sequence = authorDoc.Contains("sequence") ? authorDoc["sequence"].AsString : "",
                            });
                        }
                    }
                }
            }

            // Create document for OpenSearch with provided embedding
            return new
            {
                id = docId,
                oipubId = 0, // Add oipubId field
                doi = rawData.Contains("DOI") ? rawData["DOI"].AsString : docId, // Use DOI from raw_data, fallback to docId
                title,
                @abstract = abstractText,
                openSummary, // Add openSummary field
                fullTextContent,
                embeddingVector, // Use the provided embedding
                contextualContent,
                journal,
                publisher,
                authors,
                publishedAt = publishedAt ?? new DateTime(1, 1, 1), // Handle null values
                publicationDateParts,
                publicationHotScore = statDoc.Contains("publicationHotScore") && !statDoc["publicationHotScore"].IsBsonNull ? statDoc["publicationHotScore"].ToDouble() : 0.0,
                publicationHotScore6m = statDoc.Contains("publicationHotScore_6m") && !statDoc["publicationHotScore_6m"].IsBsonNull ? statDoc["publicationHotScore_6m"].ToDouble() : 0.0,
                pageRank = statDoc.Contains("pageRank") && !statDoc["pageRank"].IsBsonNull ? statDoc["pageRank"].ToDouble() : 0.0,
                citationsCount = rawData.Contains("is-referenced-by-count") ? rawData["is-referenced-by-count"].AsInt32 : 0, // Add citationsCount field
                voteScore = 0, // Add voteScore field (placeholder)
                topics,
            };
        }

        private (object document, string contextualContent) MapDocumentForOpenSearchWithoutEmbedding(BsonDocument statDoc, BsonDocument crossrefDoc, string docId)
        {
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
                        topScore = topicDoc.Contains("topScore") && !topicDoc["topScore"].IsBsonNull ? topicDoc["topScore"].ToDouble() : 0.0,
                        hotScore = topicDoc.Contains("hotScore") && !topicDoc["hotScore"].IsBsonNull ? topicDoc["hotScore"].ToDouble() : 0.0,
                        hotScore6m = topicDoc.Contains("hotScore_6m") && !topicDoc["hotScore_6m"].IsBsonNull ? topicDoc["hotScore_6m"].ToDouble() : 0.0,
                    });
                }
            }

            // Extract publication date
            DateTime? publishedAt = null;
            var publicationDateParts = new List<int>();
            if (crossrefDoc.Contains("publishedAt") && crossrefDoc["publishedAt"].IsBsonDocument)
            {
                var pubDate = crossrefDoc["publishedAt"].AsBsonDocument;

                if (pubDate.Contains("year") && !pubDate["year"].IsBsonNull)
                {
                    var year = pubDate["year"].AsInt32;
                    var month = pubDate.Contains("month") && !pubDate["month"].IsBsonNull ? pubDate["month"].AsInt32 : 1;
                    var day = pubDate.Contains("day") && !pubDate["day"].IsBsonNull ? pubDate["day"].AsInt32 : 1;
                    publishedAt = new DateTime(year, month, day);

                    publicationDateParts.Add(year);
                    if (pubDate.Contains("month") && !pubDate["month"].IsBsonNull)
                    {
                        publicationDateParts.Add(month);
                    }
                    if (pubDate.Contains("day") && !pubDate["day"].IsBsonNull)
                    {
                        publicationDateParts.Add(day);
                    }
                }
            }

            // Extract title
            var title = "";
            if (rawData.Contains("title") && rawData["title"].IsBsonArray)
            {
                var titleArray = rawData["title"].AsBsonArray;
                if (titleArray.Count > 0)
                {
                    title = CleanTextContent(titleArray[0].AsString);
                }
            }

            // Extract abstract
            var abstractText = CleanTextContent(rawData.Contains("abstract") ? rawData["abstract"].AsString : "");

            // Extract openSummary (placeholder for now)
            var openSummary = CleanTextContent("");

            // Create contextual content by combining title, abstract, and openSummary
            var contextualContent = string.Join(" ", new[] { title, abstractText, openSummary }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            // Create full text content for search (same as contextual content for now)
            var fullTextContent = contextualContent;

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
            var authors = new List<object>();
            if (rawData.Contains("author") && rawData["author"].IsBsonArray)
            {
                foreach (var author in rawData["author"].AsBsonArray)
                {
                    if (author.IsBsonDocument)
                    {
                        var authorDoc = author.AsBsonDocument;
                        var given = authorDoc.Contains("given") ? authorDoc["given"].AsString : "";
                        var family = authorDoc.Contains("family") ? authorDoc["family"].AsString : "";
                        if (!string.IsNullOrEmpty(given) || !string.IsNullOrEmpty(family))
                        {
                            authors.Add(new
                            {
                                name = $"{given} {family}".Trim(),
                                ORCID = authorDoc.Contains("ORCID") ? authorDoc["ORCID"].AsString : "",
                                sequence = authorDoc.Contains("sequence") ? authorDoc["sequence"].AsString : "",
                            });
                        }
                    }
                }
            }

            // Create document for OpenSearch (without embedding, will be added later)
            var document = new
            {
                id = docId,
                oipubId = 0, // Add oipubId field
                doi = rawData.Contains("DOI") ? rawData["DOI"].AsString : docId, // Use DOI from raw_data, fallback to docId
                title,
                @abstract = abstractText,
                openSummary, // Add openSummary field
                fullTextContent,
                embeddingVector = new float[768], // Placeholder, will be updated later
                contextualContent,
                journal,
                publisher,
                authors,
                publishedAt = publishedAt ?? new DateTime(1, 1, 1), // Handle null values
                publicationDateParts,
                publicationHotScore = statDoc.Contains("publicationHotScore") && !statDoc["publicationHotScore"].IsBsonNull ? statDoc["publicationHotScore"].ToDouble() : 0.0,
                publicationHotScore6m = statDoc.Contains("publicationHotScore_6m") && !statDoc["publicationHotScore_6m"].IsBsonNull ? statDoc["publicationHotScore_6m"].ToDouble() : 0.0,
                pageRank = statDoc.Contains("pageRank") && !statDoc["pageRank"].IsBsonNull ? statDoc["pageRank"].ToDouble() : 0.0,
                citationsCount = rawData.Contains("is-referenced-by-count") ? rawData["is-referenced-by-count"].AsInt32 : 0, // Add citationsCount field
                voteScore = 0, // Add voteScore field (placeholder)
                topics,
            };

            return (document, contextualContent);
        }

        private static string CleanTextContent(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove JATS XML tags while preserving content
            text = Regex.Replace(text, @"</?jats:[^>]*>", " ", RegexOptions.IgnoreCase);

            // Remove other XML/HTML tags while preserving content
            text = Regex.Replace(text, @"<[^>]*>", " ", RegexOptions.IgnoreCase);

            // Decode HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);

            // Normalize whitespace (replace multiple whitespace with single space)
            text = Regex.Replace(text, @"\s+", " ");

            // Clean up and trim
            text = text.Trim();

            // Remove the word "Abstract" from the beginning (case insensitive) - after all processing
            text = Regex.Replace(text, @"^(abstract|summary)\s*", "", RegexOptions.IgnoreCase);

            return text;
        }
    }
}
