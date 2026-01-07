using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using RhealAI.Domain.Entities;

namespace RhealAI.Infrastructure.Persistence;

/// <summary>
/// In-memory cache for repositories and analysis results with automatic expiration
/// </summary>
public class InMemoryCache
{
    private readonly ConcurrentDictionary<string, CacheEntry<Repository>> _repositories = new();
    private readonly ConcurrentDictionary<string, CacheEntry<AnalysisReport>> _reports = new();
    private readonly TimeSpan _defaultExpiration;

    public InMemoryCache(IConfiguration configuration)
    {
        var expirationHours = int.TryParse(configuration["Cache:ExpirationHours"], out var hours) ? hours : 24;
        _defaultExpiration = TimeSpan.FromHours(expirationHours);
    }

    private class CacheEntry<T>
    {
        public T Value { get; set; }
        public DateTime ExpiresAt { get; set; }

        public CacheEntry(T value, TimeSpan expiration)
        {
            Value = value;
            ExpiresAt = DateTime.UtcNow.Add(expiration);
        }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public void AddRepository(Repository repository, TimeSpan? expiration = null)
    {
        var entry = new CacheEntry<Repository>(repository, expiration ?? _defaultExpiration);
        _repositories[repository.Id] = entry;
    }

    public Repository? GetRepository(string id)
    {
        if (_repositories.TryGetValue(id, out var entry))
        {
            if (entry.IsExpired)
            {
                _repositories.TryRemove(id, out _);
                return null;
            }
            return entry.Value;
        }
        return null;
    }

    public void AddReport(AnalysisReport report, TimeSpan? expiration = null)
    {
        var entry = new CacheEntry<AnalysisReport>(report, expiration ?? _defaultExpiration);
        _reports[report.Id] = entry;
    }

    public AnalysisReport? GetReport(string id)
    {
        if (_reports.TryGetValue(id, out var entry))
        {
            if (entry.IsExpired)
            {
                _reports.TryRemove(id, out _);
                return null;
            }
            return entry.Value;
        }
        return null;
    }

    public List<Repository> GetAllRepositories()
    {
        return _repositories.Values
            .Where(e => !e.IsExpired)
            .Select(e => e.Value)
            .ToList();
    }

    public List<AnalysisReport> GetReportsByRepositoryId(string repositoryId)
    {
        return _reports.Values
            .Where(e => !e.IsExpired && e.Value.RepositoryId == repositoryId)
            .Select(e => e.Value)
            .OrderByDescending(r => r.GeneratedAt)
            .ToList();
    }

    /// <summary>
    /// Remove all expired entries from cache
    /// </summary>
    public void CleanupExpiredEntries()
    {
        var expiredRepos = _repositories
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredRepos)
        {
            _repositories.TryRemove(key, out _);
        }

        var expiredReports = _reports
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredReports)
        {
            _reports.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Get cache statistics for monitoring
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalRepositories = _repositories.Count,
            ActiveRepositories = _repositories.Values.Count(e => !e.IsExpired),
            TotalReports = _reports.Count,
            ActiveReports = _reports.Values.Count(e => !e.IsExpired)
        };
    }
}

public class CacheStatistics
{
    public int TotalRepositories { get; set; }
    public int ActiveRepositories { get; set; }
    public int TotalReports { get; set; }
    public int ActiveReports { get; set; }
}
