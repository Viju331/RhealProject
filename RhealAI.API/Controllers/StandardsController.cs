using Microsoft.AspNetCore.Mvc;
using RhealAI.Infrastructure.Persistence;
using RhealAI.Infrastructure.Services;

namespace RhealAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StandardsController : ControllerBase
{
    private readonly InMemoryCache _cache;
    private readonly ILogger<StandardsController> _logger;
    private readonly StandardsGeneratorService _standardsGenerator;

    public StandardsController(
        InMemoryCache cache, 
        ILogger<StandardsController> logger,
        StandardsGeneratorService standardsGenerator)
    {
        _cache = cache;
        _logger = logger;
        _standardsGenerator = standardsGenerator;
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

            // Generate standards if not already generated
            if (repository.Standards.Count == 0)
            {
                repository.Standards = _standardsGenerator.GenerateStandardsByTechStack(repository.Files);
            }

            // Group standards by tech stack
            var standardsByTechStack = repository.Standards
                .GroupBy(s => s.TechStack)
                .ToDictionary(g => g.Key, g => g.ToList());

            return Ok(new
            {
                repositoryId = repository.Id,
                repositoryName = repository.Name,
                totalStandards = repository.Standards.Count,
                hasExistingStandards = repository.HasExistingStandards,
                techStacks = standardsByTechStack.Keys.ToList(),
                standardsByTechStack = standardsByTechStack.Select(kvp => new
                {
                    techStack = kvp.Key,
                    count = kvp.Value.Count,
                    standards = kvp.Value.Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        description = s.Description,
                        category = s.Category,
                        techStack = s.TechStack,
                        priority = s.Priority,
                        isFromExistingDocs = s.IsFromExistingDocs,
                        sourceFile = s.SourceFile,
                        examples = s.Examples,
                        tags = s.Tags
                    }).ToList()
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving standards");
            return StatusCode(500, new { error = "Failed to retrieve standards" });
        }
    }
}
