using LibGit2Sharp;

namespace RhealAI.Infrastructure.FileProcessing;

/// <summary>
/// Service for cloning and processing GitHub repositories
/// </summary>
public class GitHubProcessor
{
    private readonly string _tempDirectory;

    public GitHubProcessor()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "RhealAI", "GitRepos");
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Clones a GitHub repository and returns the local path
    /// </summary>
    public async Task<string> CloneRepositoryAsync(string githubUrl, string? branch = null)
    {
        var repoPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(repoPath);

        try
        {
            var cloneOptions = new CloneOptions
            {
                BranchName = branch,
                Checkout = true
            };

            await Task.Run(() =>
            {
                Repository.Clone(githubUrl, repoPath, cloneOptions);
            });

            return repoPath;
        }
        catch (Exception ex)
        {
            // Clean up on error
            if (Directory.Exists(repoPath))
            {
                Directory.Delete(repoPath, true);
            }
            throw new InvalidOperationException($"Failed to clone repository: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts repository name from GitHub URL
    /// </summary>
    public string ExtractRepositoryName(string githubUrl)
    {
        var uri = new Uri(githubUrl);
        var segments = uri.Segments;
        var repoName = segments.Last().TrimEnd('/');

        // Remove .git extension if present
        if (repoName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repoName = repoName[..^4];
        }

        return repoName;
    }

    /// <summary>
    /// Validates if the URL is a valid GitHub repository URL
    /// </summary>
    public bool IsValidGitHubUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            var uri = new Uri(url);
            return uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cleans up cloned repository
    /// </summary>
    public void CleanupRepository(string repoPath)
    {
        try
        {
            if (Directory.Exists(repoPath))
            {
                Directory.Delete(repoPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
