using RhealAI.Domain.Entities;

namespace RhealAI.Application.Interfaces;

/// <summary>
/// Service for generating analysis reports
/// </summary>
public interface IReportService
{
    Task<AnalysisReport> GenerateReportAsync(string repositoryId, string? connectionId = null);
    Task<byte[]> ExportReportToPdfAsync(string reportId);
    Task<string> ExportReportToJsonAsync(string reportId);
}
