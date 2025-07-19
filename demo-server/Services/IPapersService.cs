using MongoDB.Bson;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Services
{
    public interface IPapersService
    {
        Task<object> SyncPapersAsync(int size = 1000, string? lastId = null);
    }
}
