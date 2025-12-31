using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RhealAI.API.Hubs;
using RhealAI.Application.Interfaces;

namespace RhealAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<RepositoryController> _logger;
    private readonly IHubContext<UploadProgressHub> _hubContext;

    public RepositoryController(
        IRepositoryService repositoryService,
        ILogger<RepositoryController> logger,
        IHubContext<UploadProgressHub> hubContext)
    {
        _repositoryService = repositoryService;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Upload a repository ZIP file for analysis (up to 2 GB)
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(2147483648)] // 2 GB limit
    [RequestFormLimits(MultipartBodyLengthLimit = 2147483648)] // 2 GB multipart limit
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadRepository(IFormFile file, [FromQuery] string? connectionId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Only ZIP files are supported" });
            }

            _logger.LogInformation("Processing upload: {FileName}, Size: {Size} bytes, ConnectionId: {ConnectionId}",
                file.FileName, file.Length, connectionId ?? "null");

            // Send progress updates
            if (!string.IsNullOrEmpty(connectionId))
            {
                _logger.LogInformation("Sending progress update to connectionId: {ConnectionId}", connectionId);
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", 10, "Reading file...");
            }
            else
            {
                _logger.LogWarning("No connectionId provided, skipping SignalR updates");
            }

            using var stream = file.OpenReadStream();

            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", 30, "Extracting ZIP...");
            }

            var repository = await _repositoryService.ProcessUploadedRepositoryAsync(stream, file.FileName);

            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", 100, "Upload complete!");
            }

            return Ok(new
            {
                repositoryId = repository.Id,
                name = repository.Name,
                filesCount = repository.Files.Count,
                sizeInBytes = repository.SizeInBytes,
                hasExistingStandards = repository.HasExistingStandards,
                uploadedAt = repository.UploadedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing upload");
            return StatusCode(500, new { error = "Failed to process upload", details = ex.Message });
        }
    }

    /// <summary>
    /// Upload multiple files in batch for analysis (up to 2 GB total)
    /// </summary>
    [HttpPost("upload-batch")]
    [RequestSizeLimit(2147483648)] // 2 GB limit
    [RequestFormLimits(MultipartBodyLengthLimit = 2147483648)] // 2 GB multipart limit
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadBatch(List<IFormFile> files, [FromQuery] string? connectionId)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "No files uploaded" });
            }

            _logger.LogInformation("Processing batch upload: {Count} files", files.Count);

            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", 5, $"Preparing to upload {files.Count} files...");
            }

            // Create a temporary directory to store all files
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Save all files to temp directory maintaining structure
                int processedFiles = 0;
                foreach (var file in files)
                {
                    var fileName = file.FileName;
                    var filePath = Path.Combine(tempDir, fileName);

                    // Create subdirectories if needed
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using var fileStream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(fileStream);

                    processedFiles++;
                    var progress = 5 + (int)((processedFiles / (double)files.Count) * 70); // 5-75%

                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", progress, $"Uploaded {processedFiles} of {files.Count} files...");
                    }
                }

                if (!string.IsNullOrEmpty(connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", 80, "Processing files...");
                }

                // Process the folder
                var repositoryName = Path.GetFileName(tempDir);
                var repository = await _repositoryService.ProcessFolderAsync(tempDir, repositoryName);

                if (!string.IsNullOrEmpty(connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", 100, "Upload complete!");
                }

                return Ok(new
                {
                    repositoryId = repository.Id,
                    name = repository.Name,
                    filesCount = repository.Files.Count,
                    sizeInBytes = repository.SizeInBytes,
                    hasExistingStandards = repository.HasExistingStandards,
                    uploadedAt = repository.UploadedAt
                });
            }
            finally
            {
                // Cleanup temp directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch upload");
            return StatusCode(500, new { error = "Failed to process batch upload", details = ex.Message });
        }
    }

    /// <summary>
    /// Process a local folder for analysis
    /// </summary>
    [HttpPost("folder")]
    public async Task<IActionResult> ProcessFolder([FromBody] ProcessFolderRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FolderPath))
            {
                return BadRequest(new { error = "Folder path is required" });
            }

            if (!Directory.Exists(request.FolderPath))
            {
                return BadRequest(new { error = "Folder does not exist" });
            }

            _logger.LogInformation("Processing folder: {FolderPath}", request.FolderPath);

            var repositoryName = request.RepositoryName ?? Path.GetFileName(request.FolderPath);
            var repository = await _repositoryService.ProcessFolderAsync(request.FolderPath, repositoryName);

            return Ok(new
            {
                repositoryId = repository.Id,
                name = repository.Name,
                filesCount = repository.Files.Count,
                sizeInBytes = repository.SizeInBytes,
                hasExistingStandards = repository.HasExistingStandards,
                uploadedAt = repository.UploadedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing folder");
            return StatusCode(500, new { error = "Failed to process folder", details = ex.Message });
        }
    }

    /// <summary>
    /// Clone and analyze a GitHub repository
    /// </summary>
    [HttpPost("github")]
    public async Task<IActionResult> ProcessGitHubRepository([FromBody] ProcessGitHubRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.GitHubUrl))
            {
                return BadRequest(new { error = "GitHub URL is required" });
            }

            _logger.LogInformation("Processing GitHub repository: {Url}, Branch: {Branch}",
                request.GitHubUrl, request.Branch ?? "default");

            var repository = await _repositoryService.ProcessGitHubRepositoryAsync(request.GitHubUrl, request.Branch);

            return Ok(new
            {
                repositoryId = repository.Id,
                name = repository.Name,
                filesCount = repository.Files.Count,
                sizeInBytes = repository.SizeInBytes,
                hasExistingStandards = repository.HasExistingStandards,
                uploadedAt = repository.UploadedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub repository");
            return StatusCode(500, new { error = "Failed to process GitHub repository", details = ex.Message });
        }
    }

    /// <summary>
    /// Get repository details by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetRepository(string id)
    {
        try
        {
            var repository = await _repositoryService.GetRepositoryByIdAsync(id);

            return Ok(new
            {
                id = repository.Id,
                name = repository.Name,
                filesCount = repository.Files.Count,
                sizeInBytes = repository.SizeInBytes,
                hasExistingStandards = repository.HasExistingStandards,
                uploadedAt = repository.UploadedAt,
                files = repository.Files.Select(f => new
                {
                    filePath = f.FilePath,
                    fileName = f.FileName,
                    fileType = f.FileType.ToString(),
                    lineCount = f.LineCount,
                    sizeInBytes = f.SizeInBytes
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving repository");
            return StatusCode(500, new { error = "Failed to retrieve repository" });
        }
    }
}

/// <summary>
/// Request model for processing a local folder
/// </summary>
public record ProcessFolderRequest
{
    public required string FolderPath { get; init; }
    public string? RepositoryName { get; init; }
}

/// <summary>
/// Request model for processing a GitHub repository
/// </summary>
public record ProcessGitHubRequest
{
    public required string GitHubUrl { get; init; }
    public string? Branch { get; init; }
}
