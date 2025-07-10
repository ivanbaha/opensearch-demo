using OpenSearch.Net;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure OpenSearch client
builder.Services.AddSingleton<IOpenSearchLowLevelClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var openSearchConfig = configuration.GetSection("OpenSearch");

    var url = openSearchConfig["Url"] ?? throw new InvalidOperationException("OpenSearch URL is not configured");
    var username = openSearchConfig["Username"] ?? throw new InvalidOperationException("OpenSearch Username is not configured");
    var password = openSearchConfig["Password"] ?? throw new InvalidOperationException("OpenSearch Password is not configured");
    var trustSelfSigned = bool.Parse(openSearchConfig["TrustSelfSignedCertificate"] ?? "false");

    var node = new Uri(url);
    var config = new ConnectionConfiguration(node)
        .BasicAuthentication(username, password);

    if (trustSelfSigned)
    {
        config.ServerCertificateValidationCallback((o, certificate, chain, errors) => true);
    }

    return new OpenSearchLowLevelClient(config);
});

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();


app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Demo endpoint
app.MapPost("/api/demo", (IOpenSearchLowLevelClient client, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Starting OpenSearch demo operation");

        var indexName = "demo-index";
        var document = new { title = "Hello OpenSearch", content = "This is a test document." };

        logger.LogInformation("Attempting to index document to {IndexName}", indexName);

        // Index a document
        var indexResponse = client.Index<StringResponse>(indexName, PostData.Serializable(document));

        logger.LogInformation("Index response received. Success: {Success}, Status: {Status}, Body length: {BodyLength}",
            indexResponse.Success, indexResponse.HttpStatusCode, indexResponse.Body?.Length ?? 0);

        // Check if indexing was successful
        if (!indexResponse.Success)
        {
            var errorMessage = $"Failed to index document. Status: {indexResponse.HttpStatusCode}. Response: {indexResponse.Body}";

            // Check for connection issues
            if (indexResponse.OriginalException != null)
            {
                errorMessage += $". Exception: {indexResponse.OriginalException.Message}";
                logger.LogError(indexResponse.OriginalException, "Connection error while indexing");
            }

            return Results.Problem(errorMessage);
        }

        logger.LogInformation("Document indexed successfully, now searching...");

        // Search for the document
        var searchJson = @"{
            ""query"": {
                ""match"": {
                    ""content"": ""test""
                }
            }
        }";

        var searchResponse = client.Search<StringResponse>(indexName, searchJson);

        logger.LogInformation("Search response received. Success: {Success}, Status: {Status}, Body length: {BodyLength}",
            searchResponse.Success, searchResponse.HttpStatusCode, searchResponse.Body?.Length ?? 0);

        // Check if search was successful
        if (!searchResponse.Success)
        {
            var errorMessage = $"Failed to search documents. Status: {searchResponse.HttpStatusCode}. Response: {searchResponse.Body}";

            // Check for connection issues
            if (searchResponse.OriginalException != null)
            {
                errorMessage += $". Exception: {searchResponse.OriginalException.Message}";
                logger.LogError(searchResponse.OriginalException, "Connection error while searching");
            }

            return Results.Problem(errorMessage);
        }

        // Parse responses safely
        object? indexResponseObj = null;
        object? searchResponseObj = null;

        try
        {
            if (!string.IsNullOrEmpty(indexResponse.Body))
            {
                indexResponseObj = JsonSerializer.Deserialize<object>(indexResponse.Body);
            }
            else
            {
                indexResponseObj = new { message = "Empty response body" };
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse index response as JSON");
            indexResponseObj = new { error = "Failed to parse index response", raw = indexResponse.Body, exception = ex.Message };
        }

        try
        {
            if (!string.IsNullOrEmpty(searchResponse.Body))
            {
                searchResponseObj = JsonSerializer.Deserialize<object>(searchResponse.Body);
            }
            else
            {
                searchResponseObj = new { message = "Empty response body" };
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse search response as JSON");
            searchResponseObj = new { error = "Failed to parse search response", raw = searchResponse.Body, exception = ex.Message };
        }

        var result = new
        {
            IndexResponse = indexResponseObj,
            SearchResponse = searchResponseObj,
            Metadata = new
            {
                IndexSuccess = indexResponse.Success,
                SearchSuccess = searchResponse.Success,
                IndexHttpStatusCode = indexResponse.HttpStatusCode,
                SearchHttpStatusCode = searchResponse.HttpStatusCode,
                Timestamp = DateTime.UtcNow
            }
        };

        logger.LogInformation("Demo operation completed successfully");
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error during demo operation");
        return Results.Problem($"Error: {ex.Message}");
    }
});

// Health check endpoint
app.MapGet("/api/health", (IOpenSearchLowLevelClient client, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Performing health check");

        // Simple ping to OpenSearch
        var pingResponse = client.Ping<StringResponse>();

        var isHealthy = pingResponse.Success;
        var statusCode = isHealthy ? 200 : 503;

        var healthResult = new
        {
            Status = isHealthy ? "Healthy" : "Unhealthy",
            OpenSearchAvailable = isHealthy,
            pingResponse.HttpStatusCode,
            Timestamp = DateTime.UtcNow,
            Details = new
            {
                pingResponse.Success,
                pingResponse.Body,
                Exception = pingResponse.OriginalException?.Message
            }
        };

        logger.LogInformation("Health check completed. Status: {Status}", healthResult.Status);

        return Results.Json(healthResult, statusCode: statusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check failed with exception");
        return Results.Problem($"Health check failed: {ex.Message}");
    }
});

app.Run();
