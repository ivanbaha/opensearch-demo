namespace OpenSearchDemo.Services
{
    public interface ITogetherAIService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<float[][]> GenerateBulkEmbeddingsAsync(IEnumerable<string> texts);
    }
}
