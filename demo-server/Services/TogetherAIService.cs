using System.Text;
using System.Text.Json;

namespace OpenSearchDemo.Services
{
    public class TogetherAIService : ITogetherAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TogetherAIService> _logger;

        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _embeddingModel;
        private readonly int _bulkChunkSize;

        public TogetherAIService(HttpClient httpClient, IConfiguration configuration, ILogger<TogetherAIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _apiKey = _configuration["TogetherAI:ApiKey"] ?? throw new ArgumentException("TogetherAI:ApiKey not configured");
            _baseUrl = _configuration["TogetherAI:BaseUrl"] ?? "https://api.together.xyz/v1";
            _embeddingModel = _configuration["TogetherAI:EmbeddingModel"] ?? throw new ArgumentException("TogetherAI:EmbeddingModel not configured");
            _bulkChunkSize = _configuration.GetValue<int>("TogetherAI:BulkChunkSize", 20);

            // Configure HTTP timeout
            var timeoutSeconds = _configuration.GetValue<int>("TogetherAI:HttpTimeoutSeconds", 300);
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Empty text provided for embedding generation");
                    return new float[768]; // Return zero vector for empty text
                }

                // Truncate text if too long (Together AI has limits)
                var truncatedText = text.Length > 32000 ? text[..32000] : text;

                var requestBody = new
                {
                    model = _embeddingModel,
                    input = truncatedText
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/embeddings", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Together AI API request failed. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Together AI API request failed: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var embeddingResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (embeddingResponse.TryGetProperty("data", out var dataArray) &&
                    dataArray.EnumerateArray().FirstOrDefault().TryGetProperty("embedding", out var embedding))
                {
                    var embeddingVector = embedding.EnumerateArray()
                        .Select(e => e.GetSingle())
                        .ToArray();

                    _logger.LogDebug("Generated embedding vector of length {Length} for text of length {TextLength}",
                        embeddingVector.Length, text.Length);

                    return embeddingVector;
                }

                _logger.LogError("Unexpected response format from Together AI API: {Response}", responseContent);
                throw new InvalidOperationException("Unexpected response format from Together AI API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text of length {TextLength}", text.Length);
                throw;
            }
        }

        public async Task<float[][]> GenerateBulkEmbeddingsAsync(IEnumerable<string> texts)
        {
            try
            {
                var textList = texts.ToList();
                if (textList.Count == 0)
                {
                    _logger.LogWarning("Empty text list provided for bulk embedding generation");
                    return [];
                }

                // Truncate texts if too long and filter out empty ones
                var processedTexts = textList
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Select(text => text.Length > 32000 ? text[..32000] : text)
                    .ToArray();

                if (processedTexts.Length == 0)
                {
                    _logger.LogWarning("No valid texts provided for bulk embedding generation");
                    return [];
                }

                _logger.LogDebug("Processing {TotalCount} texts in chunks of {ChunkSize} with parallel requests",
                    processedTexts.Length, _bulkChunkSize);

                // Split texts into chunks for parallel processing
                var chunks = new List<string[]>();
                for (int i = 0; i < processedTexts.Length; i += _bulkChunkSize)
                {
                    var chunk = processedTexts
                        .Skip(i)
                        .Take(_bulkChunkSize)
                        .ToArray();
                    chunks.Add(chunk);
                }

                _logger.LogDebug("Split into {ChunkCount} chunks for parallel processing", chunks.Count);

                // Process chunks in parallel
                var tasks = chunks.Select(async (chunk, index) =>
                {
                    try
                    {
                        _logger.LogDebug("Processing chunk {ChunkIndex} with {ItemCount} items", index + 1, chunk.Length);
                        var chunkEmbeddings = await ProcessEmbeddingChunkAsync(chunk);
                        _logger.LogDebug("Completed chunk {ChunkIndex} with {ResultCount} embeddings", index + 1, chunkEmbeddings.Length);
                        return chunkEmbeddings;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing chunk {ChunkIndex} with {ItemCount} items", index + 1, chunk.Length);
                        // Return zero vectors for failed chunk
                        return chunk.Select(_ => new float[768]).ToArray();
                    }
                });

                var chunkResults = await Task.WhenAll(tasks);

                // Combine all chunk results
                var allEmbeddings = chunkResults.SelectMany(chunk => chunk).ToArray();

                _logger.LogDebug("Generated {TotalCount} embedding vectors for {TextCount} texts using {ChunkCount} parallel chunks",
                    allEmbeddings.Length, processedTexts.Length, chunks.Count);

                return allEmbeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bulk embeddings for {TextCount} texts", texts.Count());
                throw;
            }
        }

        private async Task<float[][]> ProcessEmbeddingChunkAsync(string[] texts)
        {
            var requestBody = new
            {
                model = _embeddingModel,
                input = texts
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/embeddings", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Together AI chunk embedding API request failed. Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Together AI chunk embedding API request failed: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var embeddingResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (embeddingResponse.TryGetProperty("data", out var dataArray))
            {
                var embeddings = new List<float[]>();

                foreach (var item in dataArray.EnumerateArray())
                {
                    if (item.TryGetProperty("embedding", out var embedding))
                    {
                        var embeddingVector = embedding.EnumerateArray()
                            .Select(e => e.GetSingle())
                            .ToArray();
                        embeddings.Add(embeddingVector);
                    }
                }

                return embeddings.ToArray();
            }

            _logger.LogError("Unexpected response format from Together AI chunk embedding API: {Response}", responseContent);
            throw new InvalidOperationException("Unexpected response format from Together AI chunk embedding API");
        }
    }
}
