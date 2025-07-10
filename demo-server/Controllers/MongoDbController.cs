using Microsoft.AspNetCore.Mvc;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MongoDbController : ControllerBase
    {
        private readonly IMongoDbService _mongoDbService;
        private readonly ILogger<MongoDbController> _logger;

        public MongoDbController(IMongoDbService mongoDbService, ILogger<MongoDbController> logger)
        {
            _mongoDbService = mongoDbService;
            _logger = logger;
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckMongoDB()
        {
            try
            {
                var collectionNames = await _mongoDbService.GetCollectionNamesAsync();
                return Ok(collectionNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB check failed");
                return Problem($"MongoDB Error: {ex.Message}");
            }
        }
    }
}
