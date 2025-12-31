using System.Diagnostics;
using System.Text.Json;
using RhealAI.Application.Interfaces;
using RhealAI.Domain.Entities;
using RhealAI.Domain.Enums;
using RhealAI.Infrastructure.Persistence;

namespace RhealAI.Infrastructure.Services;

/// <summary>
/// Service for generating and managing analysis reports
/// </summary>
public class ReportService : IReportService
{
    private readonly InMemoryCache _cache;
    private readonly IRepositoryService _repositoryService;
    private readonly IDocumentationService _documentationService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IProgressHub? _progressHub;

    public ReportService(
        InMemoryCache cache,
        IRepositoryService repositoryService,
        IDocumentationService documentationService,
        IAIAnalysisService aiAnalysisService,
        IProgressHub? progressHub = null)
    {
        _cache = cache;
        _repositoryService = repositoryService;
        _documentationService = documentationService;
        _aiAnalysisService = aiAnalysisService;
        _progressHub = progressHub;
    }

    public async Task<AnalysisReport> GenerateReportAsync(string repositoryId, string? connectionId = null)
    {
        var stopwatch = Stopwatch.StartNew();

        await SendProgress(connectionId, 5, "Loading repository...");
        var repository = await _repositoryService.GetRepositoryByIdAsync(repositoryId);

        // Step 1: Analyze folder structure
        await SendProgress(connectionId, 10, "Analyzing project folder structure...");
        var folderStructure = AnalyzeFolderStructure(repository.Files);
        await SendProgress(connectionId, 12, $"Found: {folderStructure.Count} folders with {repository.Files.Count} files");
        await Task.Delay(300);

        // Step 2: Count file types
        await SendProgress(connectionId, 15, "Categorizing files by type...");
        var markdownFiles = repository.Files.Where(f => f.FileType == FileType.Markdown).ToList();
        var codeFiles = repository.Files.Where(f => f.FileType != FileType.Markdown).ToList();

        var fileTypeDistribution = repository.Files
            .GroupBy(f => f.FileType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        await SendProgress(connectionId, 18, $"Code files: {codeFiles.Count}, Documentation: {markdownFiles.Count}");

        // Step 2.5: Generate project summary with AI
        await SendProgress(connectionId, 20, "Analyzing business logic and generating project summary...");
        var projectSummary = await _aiAnalysisService.AnalyzeProjectStructureAsync(
            codeFiles,
            folderStructure,
            fileTypeDistribution,
            connectionId);
        await SendProgress(connectionId, 22, $"Project Analysis: {projectSummary.Architecture} architecture detected");

        // Step 3: Extract or generate standards
        List<Standard> standards;
        if (repository.HasExistingStandards && markdownFiles.Any())
        {
            // Extract from existing documentation
            await SendProgress(connectionId, 25, "Found existing documentation, extracting standards...");
            standards = await _documentationService.ExtractStandardsFromMarkdownAsync(markdownFiles, connectionId);
        }
        else
        {
            // Generate from codebase
            await SendProgress(connectionId, 25, "No documentation found, analyzing codebase...");
            standards = await _documentationService.GenerateStandardsFromCodebaseAsync(codeFiles, connectionId);
        }

        repository.Standards = standards;

        // Step 4: Analyze violations
        await SendProgress(connectionId, 42, $"Starting standards compliance check on {codeFiles.Count} files...");
        var violations = await _aiAnalysisService.AnalyzeCodeViolationsAsync(codeFiles, standards, connectionId);

        await SendProgress(connectionId, 70, $"Standards check complete: Found {violations.Count} violations");

        // Step 5: Detect bugs
        await SendProgress(connectionId, 72, $"Starting bug detection on {codeFiles.Count} files...");
        var bugs = await _aiAnalysisService.DetectBugsAsync(codeFiles, connectionId);

        await SendProgress(connectionId, 90, $"Bug detection complete: Found {bugs.Count} potential bugs");

        // Step 5.5: Detect refactoring opportunities
        await SendProgress(connectionId, 91, $"Analyzing refactoring opportunities...");
        var refactorings = await _aiAnalysisService.DetectRefactoringOpportunitiesAsync(codeFiles, connectionId);

        await SendProgress(connectionId, 94, $"Refactoring analysis complete: Found {refactorings.Count} suggestions");

        // Step 5.6: Detect code duplications
        await SendProgress(connectionId, 95, $"Scanning project for code duplications...");
        var duplications = await _aiAnalysisService.DetectCodeDuplicationsAsync(codeFiles, connectionId);

        await SendProgress(connectionId, 97, $"Duplication detection complete: Found {duplications.Count} duplications");

        // Calculate statistics
        var filesWithViolations = violations.Select(v => v.FilePath).Distinct().Count();
        var filesWithBugs = bugs.Select(b => b.FilePath).Distinct().Count();
        var filesNeedingRefactoring = refactorings.Select(r => r.FilePath).Distinct().Count();
        var filesWithDuplications = duplications.SelectMany(d => d.Locations.Select(l => l.FilePath)).Distinct().Count();
        var totalDuplicatedLines = duplications.Sum(d => d.LineCount * (d.Locations.Count - 1));

        stopwatch.Stop();
        var executionTime = FormatExecutionTime(stopwatch.Elapsed);

        // Step 6: Create report
        await SendProgress(connectionId, 96, "Generating final report...");
        var report = new AnalysisReport
        {
            RepositoryId = repository.Id,
            RepositoryName = repository.Name,
            TotalFiles = repository.Files.Count,
            FilesWithViolations = filesWithViolations,
            FilesWithBugs = filesWithBugs,
            FilesNeedingRefactoring = filesNeedingRefactoring,
            FilesWithDuplications = filesWithDuplications,
            TotalViolations = violations.Count,
            TotalBugs = bugs.Count,
            TotalRefactorings = refactorings.Count,
            TotalDuplications = duplications.Count,
            TotalDuplicatedLines = totalDuplicatedLines,
            ExecutionTime = executionTime,
            Violations = violations,
            Bugs = bugs,
            Refactorings = refactorings,
            Duplications = duplications,
            Standards = standards,
            ViolationsBySeverity = violations
                .GroupBy(v => v.Severity.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            BugsBySeverity = bugs
                .GroupBy(b => b.Severity.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            RefactoringsByPriority = refactorings
                .GroupBy(r => r.Priority.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            DuplicationsByImpact = duplications
                .GroupBy(d => d.Impact.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            Summary = GenerateSummary(violations.Count, bugs.Count, refactorings.Count, duplications.Count, standards.Count, repository.HasExistingStandards),
            ProjectSummary = projectSummary
        };

        _cache.AddReport(report);

        await SendProgress(connectionId, 100, "Analysis completed!");

        return report;
    }

    private async Task SendProgress(string? connectionId, int progress, string message)
    {
        if (_progressHub != null && !string.IsNullOrEmpty(connectionId))
        {
            await _progressHub.SendProgressAsync(connectionId, progress, message);
        }
    }

    private string FormatExecutionTime(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 60)
            return $"{elapsed.TotalSeconds:F0}s";
        else if (elapsed.TotalMinutes < 60)
            return $"{elapsed.Minutes}m {elapsed.Seconds}s";
        else
            return $"{elapsed.Hours}h {elapsed.Minutes}m";
    }

    private Dictionary<string, int> AnalyzeFolderStructure(List<CodeFile> files)
    {
        var folderCounts = new Dictionary<string, int>();

        foreach (var file in files)
        {
            var directory = Path.GetDirectoryName(file.FilePath) ?? "";
            var folders = directory.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var folder in folders)
            {
                if (!folderCounts.ContainsKey(folder))
                    folderCounts[folder] = 0;
                folderCounts[folder]++;
            }
        }

        return folderCounts;
    }

    public async Task<byte[]> ExportReportToPdfAsync(string reportId)
    {
        // TODO: Implement PDF export (e.g., using QuestPDF or similar)
        await Task.CompletedTask;
        throw new NotImplementedException("PDF export not yet implemented");
    }

    public async Task<string> ExportReportToJsonAsync(string reportId)
    {
        var report = _cache.GetReport(reportId);
        if (report == null)
        {
            throw new InvalidOperationException($"Report with ID {reportId} not found");
        }

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return await Task.FromResult(json);
    }

    private string GenerateSummary(int violationsCount, int bugsCount, int refactoringsCount, int duplicationsCount, int standardsCount, bool hasExistingStandards)
    {
        var standardsSource = hasExistingStandards
            ? "existing documentation"
            : "AI-generated from codebase analysis";

        return $@"Analysis completed successfully. 
Found {standardsCount} coding standards from {standardsSource}.
Detected {violationsCount} coding standard violations.
Identified {bugsCount} potential bugs and issues.
Discovered {refactoringsCount} refactoring opportunities.
Found {duplicationsCount} code duplication instances.
Review the detailed findings below for specific file locations, severity levels, and recommended fixes.";
    }
}
