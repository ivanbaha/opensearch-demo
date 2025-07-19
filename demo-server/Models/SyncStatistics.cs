using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;

namespace OpenSearchDemo.Models;

public class SyncStatistics
{
    public string? LastId { get; set; }
    public long TotalRetrieved { get; set; }
    public long TotalIndexed { get; set; }
    public DateTime? LastInteractionAt { get; set; }
    public List<string> CriticalErrors { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public bool IsRunning { get; set; } = false;

    public static async Task<SyncStatistics> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new SyncStatistics();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<SyncStatistics>(json) ?? new SyncStatistics();
        }
        catch
        {
            return new SyncStatistics();
        }
    }

    public async Task SaveAsync(string filePath)
    {
        try
        {
            LastInteractionAt = DateTime.UtcNow;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save statistics to {filePath}: {ex.Message}", ex);
        }
    }

    public void AddCriticalError(string error)
    {
        CriticalErrors.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: {error}");

        // Keep only last 50 errors
        if (CriticalErrors.Count > 50)
        {
            CriticalErrors.RemoveRange(0, CriticalErrors.Count - 50);
        }
    }
}
