using Microsoft.AspNetCore.Mvc;
using RhealAI.Application.Interfaces;

namespace RhealAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(IReportService reportService, ILogger<AnalysisController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Start analysis for a repository
    /// </summary>
    [HttpPost("{repositoryId}/analyze")]
    public async Task<IActionResult> AnalyzeRepository(string repositoryId, [FromQuery] string? connectionId = null)
    {
        try
        {
            _logger.LogInformation("Starting analysis for repository: {RepositoryId} with connectionId: {ConnectionId}",
                repositoryId, connectionId ?? "none");

            var report = await _reportService.GenerateReportAsync(repositoryId, connectionId);

            return Ok(new
            {
                reportId = report.Id,
                repositoryId = report.RepositoryId,
                repositoryName = report.RepositoryName,
                generatedAt = report.GeneratedAt,
                totalFiles = report.TotalFiles,
                filesWithViolations = report.FilesWithViolations,
                filesWithBugs = report.FilesWithBugs,
                totalViolations = report.TotalViolations,
                totalBugs = report.TotalBugs,
                executionTime = report.ExecutionTime,
                violationsBySeverity = report.ViolationsBySeverity,
                bugsBySeverity = report.BugsBySeverity,
                summary = report.Summary
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during analysis");
            return StatusCode(500, new { error = "Analysis failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed analysis report
    /// </summary>
    [HttpGet("report/{reportId}")]
    public async Task<IActionResult> GetReport(string reportId)
    {
        try
        {
            // Get from cache
            var json = await _reportService.ExportReportToJsonAsync(reportId);
            return Ok(json);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report");
            return StatusCode(500, new { error = "Failed to retrieve report" });
        }
    }

    /// <summary>
    /// Export report as JSON
    /// </summary>
    [HttpGet("report/{reportId}/export/json")]
    public async Task<IActionResult> ExportReportJson(string reportId)
    {
        try
        {
            var json = await _reportService.ExportReportToJsonAsync(reportId);
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"report-{reportId}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report");
            return StatusCode(500, new { error = "Failed to export report" });
        }
    }
}
