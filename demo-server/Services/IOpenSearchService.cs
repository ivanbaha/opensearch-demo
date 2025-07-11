using System.Text.Json;

namespace OpenSearchDemo.Services
{
    public interface IOpenSearchService
    {
        Task<bool> IsHealthyAsync();
        Task<object> DemoAsync();
        Task<object> CreatePapersIndexAsync();
        Task<object> SyncPapersAsync();
        Task<object> SearchPapersAsync(string? query = null, string? author = null, string? journal = null,
            DateTime? fromDate = null, DateTime? toDate = null, string? topics = null,
            string? sortBy = null, int from = 0, int size = 10);
        Task<object> ListPapersAsync(int page = 1, int perPage = 10, string sort = "latest");
        Task<object> ListPapersByTopicAsync(string topicName, int page = 1, int perPage = 10, string sort = "hot");
        Task IndexDocumentsBatchAsync(string indexName, List<object> documents);
        Task<object> DeleteIndexAsync(string indexName);
        Task<object> GetIndexInfoAsync(string indexName);
        Task<object> CheckDuplicatesAsync(string indexName = "papers");
    }
}
