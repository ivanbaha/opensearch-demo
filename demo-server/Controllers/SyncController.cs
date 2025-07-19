using Microsoft.AspNetCore.Mvc;
using OpenSearchDemo.Models;
using OpenSearchDemo.Services;

namespace OpenSearchDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly BackgroundSyncService _backgroundSyncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(BackgroundSyncService backgroundSyncService, ILogger<SyncController> logger)
    {
        _backgroundSyncService = backgroundSyncService;
        _logger = logger;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartSync()
    {
        try
        {
            var started = await _backgroundSyncService.StartSyncAsync();

            if (started)
            {
                _logger.LogInformation("Sync process started successfully");
                return Ok(new { message = "Sync process started successfully", started = true, timestamp = DateTime.UtcNow });
            }
            else
            {
                _logger.LogWarning("Sync process was already running");
                return Conflict(new { message = "Sync process is already running", started = false, timestamp = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting sync process");
            return Problem($"Error starting sync process: {ex.Message}");
        }
    }

    [HttpPost("stop")]
    public async Task<IActionResult> StopSync()
    {
        try
        {
            var stopped = await _backgroundSyncService.StopSyncAsync();

            if (stopped)
            {
                _logger.LogInformation("Sync process stopped successfully");
                return Ok(new { message = "Sync process stopped successfully", stopped = true, timestamp = DateTime.UtcNow });
            }
            else
            {
                _logger.LogWarning("Sync process was not running");
                return Ok(new { message = "Sync process was not running", stopped = false, timestamp = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping sync process");
            return Problem($"Error stopping sync process: {ex.Message}");
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetSyncStatus()
    {
        try
        {
            var status = await _backgroundSyncService.GetSyncStatusAsync();
            _logger.LogInformation("Retrieved sync status - IsRunning: {IsRunning}, TotalIndexed: {TotalIndexed}",
                status.IsRunning, status.TotalIndexed);

            return Ok(new
            {
                isRunning = status.IsRunning,
                lastId = status.LastId,
                totalRetrieved = status.TotalRetrieved,
                totalIndexed = status.TotalIndexed,
                lastInteractionAt = status.LastInteractionAt,
                startedAt = status.StartedAt,
                criticalErrors = status.CriticalErrors,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sync status");
            return Problem($"Error retrieving sync status: {ex.Message}");
        }
    }
}
