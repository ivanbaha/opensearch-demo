using Microsoft.AspNetCore.Mvc;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Controllers
{
    /// <summary>
    /// Temporary controller for A/B testing contextual vs individual field search
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SearchComparisonController : ControllerBase
    {
        private readonly IOpenSearchService _openSearchService;
        private readonly ILogger<SearchComparisonController> _logger;

        public SearchComparisonController(IOpenSearchService openSearchService, ILogger<SearchComparisonController> logger)
        {
            _openSearchService = openSearchService;
            _logger = logger;
        }

        /// <summary>
        /// Compare contextual search vs individual field search side-by-side
        /// </summary>
        /// <param name="query">Search query to test</param>
        /// <param name="size">Number of results to return for each method</param>
        /// <returns>Side-by-side comparison of both search approaches</returns>
        [HttpGet("compare")]
        public async Task<IActionResult> CompareSearchApproaches(
            [FromQuery] string query,
            [FromQuery] int size = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            try
            {
                _logger.LogInformation("Comparing search approaches for query: {Query}", query);

                // Test 1: Contextual search (uses contextualContent field)
                var contextualResults = await _openSearchService.SearchPapersContextualAsync(query, "latest", 0, size);

                // Test 2: Regular search (uses individual title^3, abstract^2, etc.)  
                var individualFieldResults = await _openSearchService.SearchPapersAsync(query, null, null, null, null, null, "latest", 0, size, null);

                return Ok(new
                {
                    query,
                    timestamp = DateTime.UtcNow,
                    comparison = new
                    {
                        contextualSearch = new
                        {
                            method = "contextualContent field",
                            fields = "contextualContent^3, contextualContent.english^2, title^2, abstract, openSummary",
                            results = contextualResults
                        },
                        individualFieldSearch = new
                        {
                            method = "individual fields",
                            fields = "title^3, abstract^2, authors, journal",
                            results = individualFieldResults
                        }
                    },
                    testRecommendations = new
                    {
                        crossFieldPhrases = new[] {
                            "machine learning healthcare",
                            "deep neural networks applications",
                            "artificial intelligence medical diagnosis"
                        },
                        fieldSpecific = new[] {
                            "applications",  // Should favor title matches
                            "methodology",   // Should favor abstract matches
                            "conclusion"     // Should favor summary matches
                        },
                        proximityTests = new[] {
                            "neural networks optimization",
                            "machine learning algorithms",
                            "data mining techniques"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during search comparison for query: {Query}", query);
                return StatusCode(500, new { error = "Search comparison failed", message = ex.Message });
            }
        }

        /// <summary>
        /// Analyze search result differences between the two approaches
        /// </summary>
        /// <param name="query">Search query to analyze</param>
        /// <param name="size">Number of results to analyze</param>
        /// <returns>Analysis of differences between search approaches</returns>
        [HttpGet("analyze")]
        public async Task<IActionResult> AnalyzeSearchDifferences(
            [FromQuery] string query,
            [FromQuery] int size = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            try
            {
                // Get results from both approaches
                var contextualResults = await _openSearchService.SearchPapersContextualAsync(query, "latest", 0, size);
                var individualFieldResults = await _openSearchService.SearchPapersAsync(query, null, null, null, null, null, "latest", 0, size, null);

                // Extract paper IDs for comparison (this is simplified - you'd need to parse the actual results)
                var analysis = new
                {
                    query,
                    totalResults = new
                    {
                        contextual = "Parse from contextualResults",
                        individualField = "Parse from individualFieldResults"
                    },
                    recommendations = new
                    {
                        useContextualWhen = new[]
                        {
                            "Query contains phrases that span multiple fields (title + abstract)",
                            "Looking for conceptual relationships across content",
                            "Users search with natural language phrases",
                            "Term proximity matters more than field importance"
                        },
                        useIndividualFieldsWhen = new[]
                        {
                            "Field-specific importance is crucial (title >> abstract)",
                            "Users search for specific field content",
                            "Storage efficiency is important",
                            "You need granular control over field boosting"
                        }
                    },
                    nextSteps = new[]
                    {
                        "Test with real user queries from your logs",
                        "Measure user satisfaction/click-through rates",
                        "Check storage and performance impact",
                        "Consider hybrid approach: contextual for complex queries, individual for simple ones"
                    }
                };

                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during search analysis for query: {Query}", query);
                return StatusCode(500, new { error = "Search analysis failed", message = ex.Message });
            }
        }
    }
}
