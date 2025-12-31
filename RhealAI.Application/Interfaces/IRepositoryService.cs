using RhealAI.Domain.Entities;

namespace RhealAI.Application.Interfaces;

/// <summary>
/// Service for processing uploaded repositories
/// </summary>
public interface IRepositoryService
{
    Task<Repository> ProcessUploadedRepositoryAsync(Stream zipStream, string fileName);
    Task<Repository> ProcessFolderAsync(string folderPath, string repositoryName);
    Task<Repository> ProcessGitHubRepositoryAsync(string githubUrl, string? branch = null);
    Task<Repository> GetRepositoryByIdAsync(string repositoryId);
}
