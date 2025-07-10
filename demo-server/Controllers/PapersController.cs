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
        public async Task<IActionResult> SyncPapers([FromQuery] int size = 1000)
        {
            try
            {
                // Validate size parameter
                if (size < 1) size = 1000;
                if (size > 10000) size = 10000;

                var result = await _papersService.SyncPapersAsync(size);
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

        [HttpGet("list")]
        public async Task<IActionResult> ListPapers(
            [FromQuery] int page = 1,
            [FromQuery] int perPage = 10,
            [FromQuery] string sort = "latest")
        {
            try
            {
                // Validate pagination parameters
                if (page < 1) page = 1;
                if (perPage < 1 || perPage > 100) perPage = 10;

                // Validate sort parameter
                var validSorts = new[] { "hot", "top", "latest" };
                if (!validSorts.Contains(sort.ToLower()))
                {
                    sort = "latest";
                }

                var result = await _openSearchService.ListPapersAsync(page, perPage, sort);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Papers list failed");
                return Problem($"List error: {ex.Message}");
            }
        }
    }
}
