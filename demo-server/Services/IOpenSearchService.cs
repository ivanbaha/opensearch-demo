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
        Task IndexDocumentsBatchAsync(string indexName, List<object> documents);
    }
}
