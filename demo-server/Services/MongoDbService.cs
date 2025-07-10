using MongoDB.Bson;
using MongoDB.Driver;

namespace OpenSearchDemo.Services
{
    public class MongoDbService : IMongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbService> _logger;

        public MongoDbService(IMongoDatabase database, ILogger<MongoDbService> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var pingResult = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                return pingResult["ok"].ToDouble() == 1.0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB health check failed");
                return false;
            }
        }

        public async Task<List<string>> GetCollectionNamesAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving MongoDB collection names");
                var collectionNames = await _database.ListCollectionNamesAsync();
                var result = await collectionNames.ToListAsync();
                _logger.LogInformation("Retrieved {Count} collection names", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving collection names");
                throw;
            }
        }

        public async Task<List<BsonDocument>> GetPublicationStatsAsync(int sampleSize = 100000)
        {
            try
            {
                _logger.LogInformation("Retrieving {SampleSize} publication statistics documents", sampleSize);
                var collection = _database.GetCollection<BsonDocument>("publicationStatistics");

                var pipeline = new[]
                {
                    new BsonDocument("$sample", new BsonDocument("size", sampleSize))
                };

                var documents = await collection.AggregateAsync<BsonDocument>(pipeline);
                var result = await documents.ToListAsync();
                _logger.LogInformation("Retrieved {Count} publication statistics documents", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving publication statistics");
                throw;
            }
        }

        public async Task<BsonDocument?> GetCrossrefDocumentAsync(string id)
        {
            try
            {
                var collection = _database.GetCollection<BsonDocument>("crossref_raw_data");
                var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
                var document = await collection.FindAsync(filter);
                return await document.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving crossref document for ID: {Id}", id);
                return null;
            }
        }
    }
}
