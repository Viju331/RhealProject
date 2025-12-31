using System.Collections.Concurrent;
using RhealAI.Domain.Entities;

namespace RhealAI.Infrastructure.Persistence;

/// <summary>
/// Simple in-memory cache for repositories and analysis results
/// </summary>
public class InMemoryCache
{
    private readonly ConcurrentDictionary<string, Repository> _repositories = new();
    private readonly ConcurrentDictionary<string, AnalysisReport> _reports = new();

    public void AddRepository(Repository repository)
    {
        _repositories[repository.Id] = repository;
    }

    public Repository? GetRepository(string id)
    {
        _repositories.TryGetValue(id, out var repository);
        return repository;
    }

    public void AddReport(AnalysisReport report)
    {
        _reports[report.Id] = report;
    }

    public AnalysisReport? GetReport(string id)
    {
        _reports.TryGetValue(id, out var report);
        return report;
    }

    public List<Repository> GetAllRepositories()
    {
        return _repositories.Values.ToList();
    }

    public List<AnalysisReport> GetReportsByRepositoryId(string repositoryId)
    {
        return _reports.Values
            .Where(r => r.RepositoryId == repositoryId)
            .OrderByDescending(r => r.GeneratedAt)
            .ToList();
    }
}
