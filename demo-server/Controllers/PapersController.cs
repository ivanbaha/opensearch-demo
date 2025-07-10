using Microsoft.AspNetCore.Mvc;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PapersController : ControllerBase
    {
        private readonly IPapersService _papersService;
        private readonly IOpenSearchService _openSearchService;
        private readonly ILogger<PapersController> _logger;

        public PapersController(IPapersService papersService, IOpenSearchService openSearchService, ILogger<PapersController> logger)
        {
            _papersService = papersService;
            _openSearchService = openSearchService;
            _logger = logger;
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncPapers()
        {
            try
            {
                var result = await _papersService.SyncPapersAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Papers sync failed");
                return Problem($"Papers sync error: {ex.Message}");
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchPapers(
            [FromQuery] string? query = null,
            [FromQuery] string? author = null,
            [FromQuery] string? journal = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? topics = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] int from = 0,
            [FromQuery] int size = 10)
        {
            try
            {
                var result = await _openSearchService.SearchPapersAsync(
                    query, author, journal, fromDate, toDate, topics, sortBy, from, size);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Papers search failed");
                return Problem($"Search error: {ex.Message}");
            }
        }
    }
}
