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

        public TogetherAIService(HttpClient httpClient, IConfiguration configuration, ILogger<TogetherAIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _apiKey = _configuration["TogetherAI:ApiKey"] ?? throw new ArgumentException("TogetherAI:ApiKey not configured");
            _baseUrl = _configuration["TogetherAI:BaseUrl"] ?? "https://api.together.xyz/v1";
            _embeddingModel = _configuration["TogetherAI:EmbeddingModel"] ?? throw new ArgumentException("TogetherAI:EmbeddingModel not configured");

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

                var requestBody = new
                {
                    model = _embeddingModel,
                    input = processedTexts
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/embeddings", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Together AI bulk embedding API request failed. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Together AI bulk embedding API request failed: {response.StatusCode}");
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

                    _logger.LogDebug("Generated {Count} embedding vectors for {TextCount} texts",
                        embeddings.Count, processedTexts.Length);

                    return embeddings.ToArray();
                }

                _logger.LogError("Unexpected response format from Together AI bulk embedding API: {Response}", responseContent);
                throw new InvalidOperationException("Unexpected response format from Together AI bulk embedding API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bulk embeddings for {TextCount} texts", texts.Count());
                throw;
            }
        }
    }
}
