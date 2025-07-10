using Microsoft.AspNetCore.Mvc;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OpenSearchController : ControllerBase
    {
        private readonly IOpenSearchService _openSearchService;
        private readonly ILogger<OpenSearchController> _logger;

        public OpenSearchController(IOpenSearchService openSearchService, ILogger<OpenSearchController> logger)
        {
            _openSearchService = openSearchService;
            _logger = logger;
        }

        [HttpPost("demo")]
        public async Task<IActionResult> Demo()
        {
            try
            {
                var result = await _openSearchService.DemoAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenSearch demo failed");
                return Problem($"Error: {ex.Message}");
            }
        }
    }
}
