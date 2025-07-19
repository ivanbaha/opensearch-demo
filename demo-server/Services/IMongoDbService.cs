using MongoDB.Bson;
using MongoDB.Driver;

namespace OpenSearchDemo.Services
{
    public interface IMongoDbService
    {
        Task<bool> IsHealthyAsync();
        Task<List<string>> GetCollectionNamesAsync();
        Task<List<BsonDocument>> GetPublicationStatsAsync(int sampleSize = 100000);
        Task<List<BsonDocument>> GetPublicationStatsBatchAsync(int batchSize, string? lastId = null);
        Task<BsonDocument?> GetCrossrefDocumentAsync(string id);
        Task<Dictionary<string, BsonDocument>> GetCrossrefDocumentsBulkAsync(List<string> ids);
    }
}
