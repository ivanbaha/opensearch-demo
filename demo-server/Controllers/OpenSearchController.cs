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

        [HttpDelete("index/{indexName}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> DeleteIndex(string indexName)
        {
            try
            {
                // Validate index name
                if (string.IsNullOrWhiteSpace(indexName))
                {
                    return BadRequest("Index name cannot be empty");
                }

                // Prevent deletion of critical system indices
                var restrictedIndices = new[] { ".opensearch", ".security", ".kibana", ".opendistro" };
                if (restrictedIndices.Any(restricted => indexName.StartsWith(restricted, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest("Cannot delete system indices");
                }

                var result = await _openSearchService.DeleteIndexAsync(indexName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete index: {IndexName}", indexName);
                return Problem($"Error deleting index '{indexName}': {ex.Message}");
            }
        }

        [HttpGet("index/{indexName}")]
        public async Task<IActionResult> GetIndexInfo(string indexName)
        {
            try
            {
                // Validate index name
                if (string.IsNullOrWhiteSpace(indexName))
                {
                    return BadRequest("Index name cannot be empty");
                }

                var result = await _openSearchService.GetIndexInfoAsync(indexName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get index information: {IndexName}", indexName);
                return Problem($"Error getting index information for '{indexName}': {ex.Message}");
            }
        }

        [HttpGet("duplicates/{indexName?}")]
        public async Task<IActionResult> CheckDuplicates(string? indexName = "papers")
        {
            try
            {
                // Validate index name
                if (string.IsNullOrWhiteSpace(indexName))
                {
                    indexName = "papers"; // Default to papers index
                }

                var result = await _openSearchService.CheckDuplicatesAsync(indexName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check duplicates in index: {IndexName}", indexName);
                return Problem($"Error checking duplicates in index '{indexName}': {ex.Message}");
            }
        }

        [HttpGet("distribution/{indexName?}")]
        public async Task<IActionResult> GetDataDistribution(string? indexName = "papers")
        {
            try
            {
                // Validate index name
                if (string.IsNullOrWhiteSpace(indexName))
                {
                    indexName = "papers"; // Default to papers index
                }

                var result = await _openSearchService.CheckDataDistributionAsync(indexName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check data distribution for index: {IndexName}", indexName);
                return Problem($"Error checking data distribution for index '{indexName}': {ex.Message}");
            }
        }
    }
}
