using OpenSearchDemo.Models;
using OpenSearchDemo.Services;
using System.Threading;

namespace OpenSearchDemo.Services;

public class BackgroundSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundSyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _statisticsFile;
    private readonly int _batchSize;
    private CancellationTokenSource? _syncCancellationTokenSource;
    private Task? _syncTask;
    private readonly object _lockObject = new object();

    public BackgroundSyncService(IServiceProvider serviceProvider, ILogger<BackgroundSyncService> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _statisticsFile = configuration["Sync:StatisticsFile"] ?? "data/sync-statistics.json";
        _batchSize = configuration.GetValue<int>("Sync:BatchSize", 200);
    }

    public bool IsRunning => _syncTask != null && !_syncTask.IsCompleted;

    public async Task<bool> StartSyncAsync()
    {
        await Task.CompletedTask; // To satisfy async requirement

        lock (_lockObject)
        {
            if (IsRunning)
            {
                _logger.LogWarning("Sync is already running");
                return false;
            }

            _syncCancellationTokenSource = new CancellationTokenSource();
            _syncTask = Task.Run(async () => await RunSyncProcessAsync(_syncCancellationTokenSource.Token));
        }

        _logger.LogInformation("Background sync started");
        return true;
    }

    public async Task<bool> StopSyncAsync()
    {
        lock (_lockObject)
        {
            if (!IsRunning)
            {
                _logger.LogWarning("Sync is not running");
                return false;
            }

            _syncCancellationTokenSource?.Cancel();
        }

        if (_syncTask != null)
        {
            try
            {
                await _syncTask;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sync stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while stopping sync");
            }
        }

        return true;
    }

    public async Task<SyncStatistics> GetSyncStatusAsync()
    {
        try
        {
            var stats = await SyncStatistics.LoadAsync(_statisticsFile);
            stats.IsRunning = IsRunning;
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sync statistics");
            return new SyncStatistics { IsRunning = IsRunning };
        }
    }

    private async Task RunSyncProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var papersService = scope.ServiceProvider.GetRequiredService<IPapersService>();
            var openSearchService = scope.ServiceProvider.GetRequiredService<IOpenSearchService>();

            _logger.LogInformation("Starting background sync process...");
            _logger.LogInformation("Configuration: BatchSize={BatchSize}", _batchSize);

            // Load statistics
            var stats = await SyncStatistics.LoadAsync(_statisticsFile);
            stats.IsRunning = true;
            stats.StartedAt = DateTime.UtcNow;
            await stats.SaveAsync(_statisticsFile);

            _logger.LogInformation("Loaded statistics: LastId={LastId}, TotalRetrieved={TotalRetrieved}, TotalIndexed={TotalIndexed}",
                stats.LastId ?? "none", stats.TotalRetrieved, stats.TotalIndexed);

            var batchCount = 0;
            var totalProcessedInSession = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    batchCount++;
                    _logger.LogInformation("Processing batch {BatchCount}", batchCount);                    // Use the existing PapersService.SyncPapersAsync method with the configured batch size and lastId
                    var startTime = DateTime.UtcNow;
                    var result = await papersService.SyncPapersAsync(_batchSize, stats.LastId);
                    var endTime = DateTime.UtcNow;

                    // Extract documentsProcessed and lastId from the result
                    var resultType = result.GetType();
                    var documentsProcessedProp = resultType.GetProperty("documentsProcessed");
                    var lastIdProp = resultType.GetProperty("lastId");

                    if (documentsProcessedProp?.GetValue(result) is int documentsProcessed)
                    {
                        if (documentsProcessed == 0)
                        {
                            _logger.LogInformation("No more documents to process. Sync completed.");
                            break;
                        }

                        stats.TotalIndexed += documentsProcessed;
                        totalProcessedInSession += documentsProcessed;

                        // Update lastId if available
                        if (lastIdProp?.GetValue(result) is string newLastId && !string.IsNullOrEmpty(newLastId))
                        {
                            stats.LastId = newLastId;
                        }

                        _logger.LogInformation("Batch {BatchCount} completed: {DocumentsProcessed} documents processed in {Duration}ms. Total session: {TotalSession}. LastId: {LastId}",
                            batchCount, documentsProcessed, (endTime - startTime).TotalMilliseconds, totalProcessedInSession, stats.LastId ?? "none");
                    }
                    else
                    {
                        _logger.LogInformation("Batch {BatchCount} completed in {Duration}ms. Result: {Result}",
                            batchCount, (endTime - startTime).TotalMilliseconds, result.ToString());
                    }

                    // Save progress after each batch
                    stats.LastInteractionAt = DateTime.UtcNow;
                    await stats.SaveAsync(_statisticsFile);

                    // Small delay to prevent overwhelming the system
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw to exit gracefully
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in sync batch {BatchCount}", batchCount);
                    stats.AddCriticalError($"Batch {batchCount}: {ex.Message}");
                    await stats.SaveAsync(_statisticsFile);

                    // Continue with next batch after error
                    await Task.Delay(5000, cancellationToken);
                }
            }

            _logger.LogInformation("Background sync process completed. Total processed in session: {TotalProcessed}", totalProcessedInSession);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background sync process was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background sync process failed");
        }
        finally
        {
            // Mark sync as not running
            try
            {
                var stats = await SyncStatistics.LoadAsync(_statisticsFile);
                stats.IsRunning = false;
                stats.LastInteractionAt = DateTime.UtcNow;
                await stats.SaveAsync(_statisticsFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync status");
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This background service doesn't auto-start sync
        // Sync is controlled via the API endpoints
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopSyncAsync();
        await base.StopAsync(cancellationToken);
    }
}
