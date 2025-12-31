using RhealAI.Application.Interfaces;
using RhealAI.Domain.Entities;
using RhealAI.Infrastructure.FileProcessing;
using RhealAI.Infrastructure.Persistence;

namespace RhealAI.Infrastructure.Services;

/// <summary>
/// Service for processing uploaded repositories
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly ZipExtractor _zipExtractor;
    private readonly FolderAnalyzer _folderAnalyzer;
    private readonly GitHubProcessor _gitHubProcessor;
    private readonly InMemoryCache _cache;

    public RepositoryService(
        ZipExtractor zipExtractor,
        FolderAnalyzer folderAnalyzer,
        GitHubProcessor gitHubProcessor,
        InMemoryCache cache)
    {
        _zipExtractor = zipExtractor;
        _folderAnalyzer = folderAnalyzer;
        _gitHubProcessor = gitHubProcessor;
        _cache = cache;
    }

    public async Task<Repository> ProcessUploadedRepositoryAsync(Stream zipStream, string fileName)
    {
        var (extractPath, files) = await _zipExtractor.ExtractAndAnalyzeAsync(zipStream, fileName);

        var repository = new Repository
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            UploadPath = extractPath,
            Files = files,
            SizeInBytes = files.Sum(f => f.SizeInBytes),
            HasExistingStandards = files.Any(f => f.FileType == Domain.Enums.FileType.Markdown)
        };

        _cache.AddRepository(repository);

        return repository;
    }

    public async Task<Repository> ProcessFolderAsync(string folderPath, string repositoryName)
    {
        var files = await _folderAnalyzer.AnalyzeFolderAsync(folderPath);

        var repository = new Repository
        {
            Name = repositoryName,
            UploadPath = folderPath,
            Files = files,
            SizeInBytes = files.Sum(f => f.SizeInBytes),
            HasExistingStandards = files.Any(f => f.FileType == Domain.Enums.FileType.Markdown)
        };

        _cache.AddRepository(repository);

        return repository;
    }

    public async Task<Repository> ProcessGitHubRepositoryAsync(string githubUrl, string? branch = null)
    {
        if (!_gitHubProcessor.IsValidGitHubUrl(githubUrl))
        {
            throw new ArgumentException("Invalid GitHub URL", nameof(githubUrl));
        }

        // Clone the repository
        var clonePath = await _gitHubProcessor.CloneRepositoryAsync(githubUrl, branch);

        try
        {
            // Analyze the cloned repository
            var files = await _folderAnalyzer.AnalyzeFolderAsync(clonePath);

            var repositoryName = _gitHubProcessor.ExtractRepositoryName(githubUrl);
            if (!string.IsNullOrEmpty(branch))
            {
                repositoryName += $" ({branch})";
            }

            var repository = new Repository
            {
                Name = repositoryName,
                UploadPath = clonePath,
                Files = files,
                SizeInBytes = files.Sum(f => f.SizeInBytes),
                HasExistingStandards = files.Any(f => f.FileType == Domain.Enums.FileType.Markdown)
            };

            _cache.AddRepository(repository);

            return repository;
        }
        catch
        {
            // Clean up on error
            _gitHubProcessor.CleanupRepository(clonePath);
            throw;
        }
    }

    public async Task<Repository> GetRepositoryByIdAsync(string repositoryId)
    {
        var repository = _cache.GetRepository(repositoryId);
        if (repository == null)
        {
            throw new InvalidOperationException($"Repository with ID {repositoryId} not found");
        }

        return await Task.FromResult(repository);
    }
}
