using Microsoft.AspNetCore.Mvc;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Controllers
{
    [ApiController]
    [Route("api/papers")]
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
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> SyncPapers([FromQuery] int size = 1000)
        {
            try
            {
                // Validate size parameter
                if (size < 1) size = 1000;
                if (size > 10000) size = 10000;

                var result = await _papersService.SyncPapersAsync(size);

                // Refresh the papers index to make documents immediately searchable
                _logger.LogInformation("Syncing completed, refreshing papers index...");
                var refreshResult = await _openSearchService.RefreshIndexAsync("papers");

                // Log refresh result but don't fail the sync if refresh fails
                if (refreshResult is { } refreshObj &&
                    refreshObj.GetType().GetProperty("success")?.GetValue(refreshObj) is bool success &&
                    success)
                {
                    _logger.LogInformation("Papers index refreshed successfully after sync");
                }
                else
                {
                    _logger.LogWarning("Papers index refresh failed after sync, but sync completed successfully");
                }

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
            [FromQuery] int size = 10,
            [FromQuery] bool? hasAbstract = null)
        {
            try
            {
                var result = await _openSearchService.SearchPapersAsync(
                    query, author, journal, fromDate, toDate, topics, sortBy, from, size, hasAbstract);
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
            [FromQuery] string sort = "latest",
            [FromQuery] bool? hasAbstract = null)
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

                var result = await _openSearchService.ListPapersAsync(page, perPage, sort, hasAbstract);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Papers list failed");
                return Problem($"List error: {ex.Message}");
            }
        }

        [HttpGet("topics/{topicName}")]
        public async Task<IActionResult> ListPapersByTopic(
            string topicName,
            [FromQuery] int page = 1,
            [FromQuery] int perPage = 10,
            [FromQuery] string sort = "hot")
        {
            try
            {
                // Validate topic name
                if (string.IsNullOrWhiteSpace(topicName))
                {
                    return BadRequest("Topic name is required");
                }

                // Validate pagination parameters
                if (page < 1) page = 1;
                if (perPage < 1 || perPage > 100) perPage = 10;

                // Validate sort parameter - for topic-specific sorting
                var validSorts = new[] { "hot", "top", "relevance", "latest" };
                if (!validSorts.Contains(sort.ToLower()))
                {
                    sort = "hot";
                }

                var result = await _openSearchService.ListPapersByTopicAsync(topicName, page, perPage, sort);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Papers by topic list failed for topic: {TopicName}", topicName);
                return Problem($"List by topic error: {ex.Message}");
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshIndex([FromQuery] string indexName = "papers")
        {
            try
            {
                // Validate index name
                if (string.IsNullOrWhiteSpace(indexName))
                {
                    return BadRequest("Index name is required");
                }

                _logger.LogInformation("Refreshing index: {IndexName}", indexName);

                var result = await _openSearchService.RefreshIndexAsync(indexName);

                _logger.LogInformation("Index refresh completed for: {IndexName}", indexName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index refresh failed for: {IndexName}", indexName);
                return Problem($"Index refresh error: {ex.Message}");
            }
        }

        [HttpGet("search/contextual")]
        public async Task<IActionResult> SearchPapersContextual(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int perPage = 10,
            [FromQuery] string sort = "latest")
        {
            try
            {
                // Validate required query parameter
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query parameter is required for contextual search");
                }

                // Validate pagination parameters
                if (page < 1) page = 1;
                if (perPage < 1 || perPage > 100) perPage = 10;

                // Validate sort parameter - allow both list-style and search-style sorts
                var validSorts = new[] { "hot", "top", "latest", "hotscore", "pagerank", "date", "citationscount", "votescore" };
                if (!validSorts.Contains(sort.ToLower()))
                {
                    sort = "latest";
                }

                // Convert page/perPage to from/size for OpenSearch
                var from = (page - 1) * perPage;
                var size = perPage;

                _logger.LogInformation("Performing contextual search with query: {Query}", query);

                var result = await _openSearchService.SearchPapersContextualAsync(query, sort, from, size);

                _logger.LogInformation("Contextual search completed for query: {Query}", query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Contextual search failed for query: {Query}", query);
                return Problem($"Contextual search error: {ex.Message}");
            }
        }

        [HttpGet("search/semantic")]
        public async Task<IActionResult> SearchPapersSemantic(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int perPage = 10,
            [FromQuery] string sort = "latest",
            [FromQuery] bool? hasAbstract = null)
        {
            try
            {
                // Validate required query parameter
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query parameter is required for semantic search");
                }

                // Validate pagination parameters
                if (page < 1) page = 1;
                if (perPage < 1 || perPage > 100) perPage = 10;

                // Validate sort parameter
                var validSorts = new[] { "hot", "top", "latest" };
                if (!validSorts.Contains(sort.ToLower()))
                {
                    sort = "latest";
                }

                _logger.LogInformation("Performing semantic search with query: {Query}", query);

                var result = await _openSearchService.SearchPapersSemanticAsync(query, page, perPage, sort, hasAbstract);

                _logger.LogInformation("Semantic search completed for query: {Query}", query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Semantic search failed for query: {Query}", query);
                return Problem($"Semantic search error: {ex.Message}");
            }
        }
    }
}
