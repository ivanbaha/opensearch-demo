using Microsoft.AspNetCore.Mvc;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IHealthService _healthService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IHealthService healthService, ILogger<HealthController> logger)
        {
            _healthService = healthService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                var healthResult = await _healthService.GetHealthStatusAsync();
                var status = ((dynamic)healthResult).Status;
                var statusCode = status == "Healthy" ? 200 : 503;

                return StatusCode(statusCode, healthResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed with exception");
                return Problem($"Health check failed: {ex.Message}");
            }
        }
    }
}
