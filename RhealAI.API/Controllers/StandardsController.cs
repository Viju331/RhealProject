using Microsoft.AspNetCore.Mvc;
using RhealAI.Infrastructure.Persistence;

namespace RhealAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StandardsController : ControllerBase
{
    private readonly InMemoryCache _cache;
    private readonly ILogger<StandardsController> _logger;

    public StandardsController(InMemoryCache cache, ILogger<StandardsController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get standards for a repository
    /// </summary>
    [HttpGet("repository/{repositoryId}")]
    public IActionResult GetStandards(string repositoryId)
    {
        try
        {
            var repository = _cache.GetRepository(repositoryId);
            if (repository == null)
            {
                return NotFound(new { error = "Repository not found" });
            }

            return Ok(new
            {
                repositoryId = repository.Id,
                standardsCount = repository.Standards.Count,
                hasExistingStandards = repository.HasExistingStandards,
                standards = repository.Standards.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    description = s.Description,
                    category = s.Category,
                    isFromExistingDocs = s.IsFromExistingDocs,
                    sourceFile = s.SourceFile,
                    examples = s.Examples
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving standards");
            return StatusCode(500, new { error = "Failed to retrieve standards" });
        }
    }
}
