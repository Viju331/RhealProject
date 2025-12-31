using RhealAI.Domain.Entities;

namespace RhealAI.Application.Interfaces;

/// <summary>
/// Service for AI-powered code analysis using Microsoft Agent Framework
/// </summary>
public interface IAIAnalysisService
{
    Task<List<Violation>> AnalyzeCodeViolationsAsync(List<CodeFile> files, List<Standard> standards, string? connectionId = null);
    Task<List<Bug>> DetectBugsAsync(List<CodeFile> files, string? connectionId = null);
    Task<ProjectSummary> AnalyzeProjectStructureAsync(
        List<CodeFile> files,
        Dictionary<string, int> folderStructure,
        Dictionary<string, int> fileTypeDistribution,
        string? connectionId = null);
}
