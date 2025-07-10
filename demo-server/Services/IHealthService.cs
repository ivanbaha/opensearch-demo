using OpenSearchDemo.Services;

namespace OpenSearchDemo.Services
{
    public interface IHealthService
    {
        Task<object> GetHealthStatusAsync();
    }
}
