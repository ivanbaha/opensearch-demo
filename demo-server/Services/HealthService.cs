using OpenSearchDemo.Services;

namespace OpenSearchDemo.Services
{
    public class HealthService : IHealthService
    {
        private readonly IOpenSearchService _openSearchService;
        private readonly IMongoDbService _mongoDbService;
        private readonly ILogger<HealthService> _logger;

        public HealthService(IOpenSearchService openSearchService, IMongoDbService mongoDbService, ILogger<HealthService> logger)
        {
            _openSearchService = openSearchService;
            _mongoDbService = mongoDbService;
            _logger = logger;
        }

        public async Task<object> GetHealthStatusAsync()
        {
            try
            {
                _logger.LogInformation("Performing health check");

                // Check OpenSearch
                var openSearchHealthy = await _openSearchService.IsHealthyAsync();

                // Check MongoDB
                var mongoHealthy = await _mongoDbService.IsHealthyAsync();

                var overallHealthy = openSearchHealthy && mongoHealthy;

                var healthResult = new
                {
                    Status = overallHealthy ? "Healthy" : "Unhealthy",
                    Services = new
                    {
                        OpenSearch = new
                        {
                            Status = openSearchHealthy ? "Healthy" : "Unhealthy",
                            Available = openSearchHealthy
                        },
                        MongoDB = new
                        {
                            Status = mongoHealthy ? "Healthy" : "Unhealthy",
                            Available = mongoHealthy
                        }
                    },
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Health check completed. Overall Status: {Status}, OpenSearch: {OpenSearchStatus}, MongoDB: {MongoStatus}",
                    healthResult.Status, healthResult.Services.OpenSearch.Status, healthResult.Services.MongoDB.Status);

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed with exception");
                throw;
            }
        }
    }
}
