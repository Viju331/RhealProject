using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhealAI.Infrastructure.Persistence;

namespace RhealAI.Infrastructure.Services;

/// <summary>
/// Background service that periodically cleans up expired cache entries
/// </summary>
public class CacheCleanupService : BackgroundService
{
    private readonly InMemoryCache _cache;
    private readonly ILogger<CacheCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;

    public CacheCleanupService(
        InMemoryCache cache,
        ILogger<CacheCleanupService> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;

        var cleanupHours = int.TryParse(configuration["Cache:CleanupIntervalHours"], out var hours) ? hours : 1;
        _cleanupInterval = TimeSpan.FromHours(cleanupHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache Cleanup Service started. Cleanup will run every {Interval} hours.",
            _cleanupInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                _logger.LogInformation("Running cache cleanup...");

                var statsBefore = _cache.GetStatistics();
                _cache.CleanupExpiredEntries();
                var statsAfter = _cache.GetStatistics();

                var removedRepos = statsBefore.TotalRepositories - statsAfter.TotalRepositories;
                var removedReports = statsBefore.TotalReports - statsAfter.TotalReports;

                _logger.LogInformation(
                    "Cache cleanup completed. Removed {RemovedRepos} repositories and {RemovedReports} reports. " +
                    "Remaining: {ActiveRepos} repositories, {ActiveReports} reports.",
                    removedRepos, removedReports, statsAfter.ActiveRepositories, statsAfter.ActiveReports);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during cache cleanup");
            }
        }

        _logger.LogInformation("Cache Cleanup Service stopped.");
    }
}
