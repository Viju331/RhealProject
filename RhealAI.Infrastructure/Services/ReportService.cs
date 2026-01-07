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
            : "AI-powered deep codebase analysis";

        return $@"Comprehensive Analysis Completed Successfully

CODING STANDARDS ANALYSIS:
✓ Extracted {standardsCount} detailed coding standards from {standardsSource}
✓ Standards cover: Naming Conventions, Architecture Patterns, Error Handling, Documentation, 
  Async Patterns, SOLID Principles, Security, Performance, Code Quality, and more
✓ Each standard includes: Description, Examples, Rationale, Severity Level, and Applicable Components

QUALITY ASSESSMENT:
• {violationsCount} coding standard violations detected across the codebase
• {bugsCount} potential bugs and issues identified
• {refactoringsCount} refactoring opportunities for improved code quality
• {duplicationsCount} code duplication instances found

All findings include specific file locations, line numbers, severity levels, detailed descriptions,
and actionable recommendations for fixes. Standards are comprehensive and ready for enforcement.

Review the detailed findings below to improve code quality, maintainability, and adherence to best practices.\";
    }

    public async Task<string> GenerateHtmlReportAsync(string reportId)
    {
        var report = _cache.GetReport(reportId);
        if (report == null)
        {
            throw new InvalidOperationException($"Report {reportId} not found");
        }

        // Generate without header/footer for viewing in app (app has its own header/footer)
        return await Task.Run(() => GenerateHtmlContent(report, includeHeaderFooter: false));
    }

    public async Task<byte[]> ExportReportToHtmlAsync(string reportId)
    {
        var report = _cache.GetReport(reportId);
        if (report == null)
        {
            throw new InvalidOperationException($"Report {reportId} not found");
        }

        // Generate with header/footer for download (standalone HTML file)
        var htmlContent = await Task.Run(() => GenerateHtmlContent(report, includeHeaderFooter: true));
        return System.Text.Encoding.UTF8.GetBytes(htmlContent);
    }

    private string GenerateHtmlContent(AnalysisReport report, bool includeHeaderFooter = true)
    {
        var violationsByCriticality = report.Violations
            .GroupBy(v => v.Severity)
            .OrderByDescending(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        var bugsByCriticality = report.Bugs
            .GroupBy(b => b.Severity)
            .OrderByDescending(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        var topViolations = report.Violations
            .Where(v => v.Severity == SeverityLevel.Critical || v.Severity == SeverityLevel.High)
            .Take(10)
            .ToList();

        var topBugs = report.Bugs
            .Where(b => b.Severity == SeverityLevel.Critical || b.Severity == SeverityLevel.High)
            .Take(10)
            .ToList();

        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Rheal AI - Code Analysis Report</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap"" rel=""stylesheet"">
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            line-height: 1.6;
            color: #1e293b;
            background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
            background-attachment: fixed;
            padding: 20px;
        }

        .container {
            max-width: 1400px;
            margin: 0 auto;
        }

        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px;
            border-radius: 16px;
            box-shadow: 0 20px 40px rgba(102, 126, 234, 0.3);
            margin-bottom: 30px;
        }

        .header h1 {
            font-size: 2.5rem;
            font-weight: 800;
            margin-bottom: 10px;
            text-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        }

        .header .subtitle {
            font-size: 1.1rem;
            opacity: 0.95;
            font-weight: 500;
        }

        .meta-info {
            background: white;
            padding: 25px;
            border-radius: 12px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.07);
            margin-bottom: 30px;
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
        }

        .meta-item {
            display: flex;
            flex-direction: column;
        }

        .meta-label {
            font-size: 0.875rem;
            color: #64748b;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 5px;
        }

        .meta-value {
            font-size: 1.125rem;
            color: #0f172a;
            font-weight: 700;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            padding: 25px;
            border-radius: 12px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.07);
            transition: all 0.3s ease;
            border-left: 4px solid;
        }

        .stat-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 12px 24px rgba(0, 0, 0, 0.1);
        }

        .stat-card.violations {
            border-left-color: #f59e0b;
        }

        .stat-card.bugs {
            border-left-color: #ef4444;
        }

        .stat-card.refactorings {
            border-left-color: #3b82f6;
        }

        .stat-card.duplications {
            border-left-color: #8b5cf6;
        }

        .stat-icon {
            font-size: 2rem;
            margin-bottom: 10px;
        }

        .stat-label {
            font-size: 0.875rem;
            color: #64748b;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
        }

        .stat-value {
            font-size: 2.5rem;
            font-weight: 800;
            color: #0f172a;
            line-height: 1;
            margin-bottom: 8px;
        }

        .stat-detail {
            font-size: 0.875rem;
            color: #64748b;
        }

        .section {
            background: white;
            padding: 30px;
            border-radius: 12px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.07);
            margin-bottom: 30px;
        }

        .section-title {
            font-size: 1.75rem;
            font-weight: 700;
            color: #0f172a;
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 3px solid;
            border-image: linear-gradient(90deg, #667eea 0%, #764ba2 100%) 1;
        }

        .severity-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 25px;
        }

        .severity-badge {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 15px 20px;
            border-radius: 8px;
            font-weight: 600;
        }

        .severity-badge.critical {
            background: #fee2e2;
            color: #991b1b;
        }

        .severity-badge.high {
            background: #fef3c7;
            color: #92400e;
        }

        .severity-badge.medium {
            background: #dbeafe;
            color: #1e40af;
        }

        .severity-badge.low {
            background: #f3f4f6;
            color: #374151;
        }

        .issue-card {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-left: 4px solid;
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 15px;
            transition: all 0.2s ease;
        }

        .issue-card:hover {
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
            transform: translateX(5px);
        }

        .issue-card.critical {
            border-left-color: #dc2626;
        }

        .issue-card.high {
            border-left-color: #f59e0b;
        }

        .issue-card.medium {
            border-left-color: #3b82f6;
        }

        .issue-card.low {
            border-left-color: #6b7280;
        }

        .issue-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 12px;
        }

        .issue-title {
            font-size: 1.125rem;
            font-weight: 700;
            color: #0f172a;
            flex: 1;
        }

        .issue-severity {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 6px;
            font-size: 0.75rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .issue-severity.critical {
            background: #dc2626;
            color: white;
        }

        .issue-severity.high {
            background: #f59e0b;
            color: white;
        }

        .issue-severity.medium {
            background: #3b82f6;
            color: white;
        }

        .issue-severity.low {
            background: #6b7280;
            color: white;
        }

        .issue-meta {
            font-size: 0.875rem;
            color: #64748b;
            margin-bottom: 12px;
        }

        .issue-description {
            color: #475569;
            margin-bottom: 12px;
            line-height: 1.7;
        }

        .code-snippet {
            background: #1e293b;
            color: #e2e8f0;
            padding: 15px;
            border-radius: 6px;
            font-family: 'Courier New', Courier, monospace;
            font-size: 0.875rem;
            overflow-x: auto;
            margin: 12px 0;
            border: 1px solid #334155;
            white-space: pre-wrap;
            word-wrap: break-word;
        }

        .suggested-fix {
            background: #ecfdf5;
            border-left: 3px solid #10b981;
            padding: 15px;
            border-radius: 6px;
            margin-top: 12px;
        }

        .suggested-fix-label {
            font-weight: 700;
            color: #047857;
            margin-bottom: 8px;
            font-size: 0.875rem;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .suggested-fix-text {
            color: #065f46;
            line-height: 1.6;
        }

        .summary-table {
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
        }

        .summary-table th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            font-size: 0.875rem;
        }

        .summary-table td {
            padding: 15px;
            border-bottom: 1px solid #e2e8f0;
        }

        .summary-table tr:hover {
            background: #f8fafc;
        }

        .footer {
            text-align: center;
            padding: 30px;
            color: #64748b;
            margin-top: 40px;
        }

        .footer-text {
            font-size: 0.875rem;
        }

        .footer-logo {
            font-size: 1.5rem;
            font-weight: 800;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            margin-bottom: 10px;
        }

        @media print {
            body {
                background: white;
            }
            .stat-card:hover {
                transform: none;
            }
            .issue-card:hover {
                transform: none;
            }
        }

        @media (max-width: 768px) {
            .header h1 {
                font-size: 1.75rem;
            }
            .stats-grid {
                grid-template-columns: 1fr;
            }
        }
    </style>
</head>
<body>
    <div class=""container"">
        " + (includeHeaderFooter ? @"
        <!-- Header -->
        <div class=""header"">
            <h1>🔍 Rheal AI - Code Analysis Report</h1>
            <div class=""subtitle"">
                Comprehensive Code Quality & Security Analysis | Generated: " + report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss") + @"
            </div>
        </div>

        <!-- Meta Information -->
        <div class=""meta-info"">
            <div class=""meta-item"">
                <div class=""meta-label"">Project Name</div>
                <div class=""meta-value"">" + System.Net.WebUtility.HtmlEncode(report.RepositoryName) + @"</div>
            </div>
            <div class=""meta-item"">
                <div class=""meta-label"">Analysis Mode</div>
                <div class=""meta-value"">Demo Mode</div>
            </div>
            <div class=""meta-item"">
                <div class=""meta-label"">Total Files</div>
                <div class=""meta-value"">" + report.TotalFiles.ToString("N0") + @"</div>
            </div>
            <div class=""meta-item"">
                <div class=""meta-label"">Execution Time</div>
                <div class=""meta-value"">" + report.ExecutionTime + @"</div>
            </div>
        </div>
        " : "") + @"

        <!-- Statistics Cards -->
        <div class=""stats-grid"">
            <div class=""stat-card violations"">
                <div class=""stat-icon"">⚠️</div>
                <div class=""stat-label"">Violations Found</div>
                <div class=""stat-value"">" + report.TotalViolations.ToString("N0") + @"</div>
                <div class=""stat-detail"">
                    " + report.FilesWithViolations + @" files affected | 
                    Critical: " + report.ViolationsBySeverity.GetValueOrDefault("Critical", 0) + @" | 
                    High: " + report.ViolationsBySeverity.GetValueOrDefault("High", 0) + @"
                </div>
            </div>

            <div class=""stat-card bugs"">
                <div class=""stat-icon"">🐛</div>
                <div class=""stat-label"">Bugs Detected</div>
                <div class=""stat-value"">" + report.TotalBugs.ToString("N0") + @"</div>
                <div class=""stat-detail"">
                    " + report.FilesWithBugs + @" files affected | 
                    Critical: " + report.BugsBySeverity.GetValueOrDefault("Critical", 0) + @" | 
                    High: " + report.BugsBySeverity.GetValueOrDefault("High", 0) + @"
                </div>
            </div>

            <div class=""stat-card refactorings"">
                <div class=""stat-icon"">🔧</div>
                <div class=""stat-label"">Refactoring Opportunities</div>
                <div class=""stat-value"">" + report.TotalRefactorings.ToString("N0") + @"</div>
                <div class=""stat-detail"">
                    " + report.FilesNeedingRefactoring + @" files need attention
                </div>
            </div>

            <div class=""stat-card duplications"">
                <div class=""stat-icon"">📋</div>
                <div class=""stat-label"">Code Duplications</div>
                <div class=""stat-value"">" + report.TotalDuplications.ToString("N0") + @"</div>
                <div class=""stat-detail"">
                    " + report.FilesWithDuplications + @" files | " + report.TotalDuplicatedLines.ToString("N0") + @" duplicated lines
                </div>
            </div>
        </div>

        <!-- Project Summary -->
        " + (report.ProjectSummary != null ? @"
        <div class=""section"">
            <h2 class=""section-title"">📊 Project Overview</h2>
            <table class=""summary-table"">
                <tr>
                    <th>Aspect</th>
                    <th>Details</th>
                </tr>
                <tr>
                    <td><strong>Architecture</strong></td>
                    <td>" + System.Net.WebUtility.HtmlEncode(report.ProjectSummary.Architecture) + @"</td>
                </tr>
                <tr>
                    <td><strong>Primary Language</strong></td>
                    <td>" + System.Net.WebUtility.HtmlEncode(report.ProjectSummary.PrimaryLanguage) + @"</td>
                </tr>
                <tr>
                    <td><strong>Description</strong></td>
                    <td>" + System.Net.WebUtility.HtmlEncode(report.ProjectSummary.Description) + @"</td>
                </tr>
                <tr>
                    <td><strong>Technology Stack</strong></td>
                    <td>" + System.Net.WebUtility.HtmlEncode(report.ProjectSummary.TechnologyStack) + @"</td>
                </tr>
                <tr>
                    <td><strong>Key Features</strong></td>
                    <td>" + string.Join(", ", report.ProjectSummary.KeyFeatures.Select(f => System.Net.WebUtility.HtmlEncode(f))) + @"</td>
                </tr>
                <tr>
                    <td><strong>Main Components</strong></td>
                    <td>" + string.Join(", ", report.ProjectSummary.MainComponents.Select(m => System.Net.WebUtility.HtmlEncode(m))) + @"</td>
                </tr>
            </table>
        </div>
        " : "") + @"

        <!-- Top Violations -->
        " + (topViolations.Any() ? @"
        <div class=""section"">
            <h2 class=""section-title"">⚠️ Top Priority Violations</h2>
            
            <div class=""severity-grid"">
                <div class=""severity-badge critical"">
                    <span>Critical</span>
                    <span>" + violationsByCriticality.GetValueOrDefault(SeverityLevel.Critical, new List<Violation>()).Count + @"</span>
                </div>
                <div class=""severity-badge high"">
                    <span>High</span>
                    <span>" + violationsByCriticality.GetValueOrDefault(SeverityLevel.High, new List<Violation>()).Count + @"</span>
                </div>
                <div class=""severity-badge medium"">
                    <span>Medium</span>
                    <span>" + violationsByCriticality.GetValueOrDefault(SeverityLevel.Medium, new List<Violation>()).Count + @"</span>
                </div>
                <div class=""severity-badge low"">
                    <span>Low</span>
                    <span>" + violationsByCriticality.GetValueOrDefault(SeverityLevel.Low, new List<Violation>()).Count + @"</span>
                </div>
            </div>

            " + string.Join("", topViolations.Select(v => @"
            <div class=""issue-card " + v.Severity.ToString().ToLower() + @""">
                <div class=""issue-header"">
                    <div class=""issue-title"">" + System.Net.WebUtility.HtmlEncode(v.RuleName) + @"</div>
                    <span class=""issue-severity " + v.Severity.ToString().ToLower() + @""">" + v.Severity + @"</span>
                </div>
                <div class=""issue-meta"">
                    📁 " + System.Net.WebUtility.HtmlEncode(v.FilePath) + @" | 📍 Lines " + v.LineNumber + @"-" + v.EndLineNumber + @" | 🏷️ " + v.Type + @"
                </div>
                <div class=""issue-description"">
                    " + System.Net.WebUtility.HtmlEncode(v.Description) + @"
                </div>
                " + (!string.IsNullOrEmpty(v.CodeSnippet) ? @"
                <div class=""code-snippet"">" + System.Net.WebUtility.HtmlEncode(v.CodeSnippet) + @"</div>
                " : "") + @"
                " + (!string.IsNullOrEmpty(v.SuggestedFix) ? @"
                <div class=""suggested-fix"">
                    <div class=""suggested-fix-label"">✅ Suggested Fix</div>
                    <div class=""suggested-fix-text"">" + System.Net.WebUtility.HtmlEncode(v.SuggestedFix) + @"</div>
                </div>
                " : "") + @"
            </div>
            ")) + @"
        </div>
        " : "") + @"

        <!-- Top Bugs -->
        " + (topBugs.Any() ? @"
        <div class=""section"">
            <h2 class=""section-title"">🐛 Top Priority Bugs</h2>
            
            <div class=""severity-grid"">
                <div class=""severity-badge critical"">
                    <span>Critical</span>
                    <span>" + bugsByCriticality.GetValueOrDefault(SeverityLevel.Critical, new List<Bug>()).Count + @"</span>
                </div>
                <div class=""severity-badge high"">
                    <span>High</span>
                    <span>" + bugsByCriticality.GetValueOrDefault(SeverityLevel.High, new List<Bug>()).Count + @"</span>
                </div>
                <div class=""severity-badge medium"">
                    <span>Medium</span>
                    <span>" + bugsByCriticality.GetValueOrDefault(SeverityLevel.Medium, new List<Bug>()).Count + @"</span>
                </div>
                <div class=""severity-badge low"">
                    <span>Low</span>
                    <span>" + bugsByCriticality.GetValueOrDefault(SeverityLevel.Low, new List<Bug>()).Count + @"</span>
                </div>
            </div>

            " + string.Join("", topBugs.Select(b => @"
            <div class=""issue-card " + b.Severity.ToString().ToLower() + @""">
                <div class=""issue-header"">
                    <div class=""issue-title"">" + System.Net.WebUtility.HtmlEncode(b.Title) + @"</div>
                    <span class=""issue-severity " + b.Severity.ToString().ToLower() + @""">" + b.Severity + @"</span>
                </div>
                <div class=""issue-meta"">
                    📁 " + System.Net.WebUtility.HtmlEncode(b.FilePath) + @" | 📍 Lines " + b.LineNumber + @"-" + b.EndLineNumber + @"
                </div>
                <div class=""issue-description"">
                    <strong>Description:</strong> " + System.Net.WebUtility.HtmlEncode(b.Description) + @"<br>
                    <strong>Root Cause:</strong> " + System.Net.WebUtility.HtmlEncode(b.RootCause) + @"<br>
                    <strong>Impact:</strong> " + System.Net.WebUtility.HtmlEncode(b.Impact) + @"
                </div>
                " + (!string.IsNullOrEmpty(b.CodeSnippet) ? @"
                <div class=""code-snippet"">" + System.Net.WebUtility.HtmlEncode(b.CodeSnippet) + @"</div>
                " : "") + @"
                " + (!string.IsNullOrEmpty(b.SuggestedFix) ? @"
                <div class=""suggested-fix"">
                    <div class=""suggested-fix-label"">✅ Suggested Fix</div>
                    <div class=""suggested-fix-text"">" + System.Net.WebUtility.HtmlEncode(b.SuggestedFix) + @"</div>
                </div>
                " : "") + @"
            </div>
            ")) + @"
        </div>
        " : "") + @"

        <!-- Refactoring Opportunities -->
        " + (report.Refactorings.Any() ? @"
        <div class=""section"">
            <h2 class=""section-title"">🔧 Refactoring Opportunities</h2>
            " + string.Join("", report.Refactorings.Take(5).Select(r => @"
            <div class=""issue-card medium"">
                <div class=""issue-header"">
                    <div class=""issue-title"">" + System.Net.WebUtility.HtmlEncode(r.Title) + @"</div>
                </div>
                <div class=""issue-meta"">
                    📁 " + System.Net.WebUtility.HtmlEncode(r.FilePath) + @" | 📍 Lines " + r.LineNumber + @"-" + r.EndLineNumber + @" | 🎯 " + r.RefactoringType + @"
                </div>
                <div class=""issue-description"">
                    <strong>Description:</strong> " + System.Net.WebUtility.HtmlEncode(r.Description) + @"<br>
                    <strong>Reason:</strong> " + System.Net.WebUtility.HtmlEncode(r.Reason) + @"
                </div>
                <div class=""suggested-fix"">
                    <div class=""suggested-fix-label"">💡 Improvement</div>
                    <div class=""suggested-fix-text"">" + System.Net.WebUtility.HtmlEncode(r.Benefits) + @"</div>
                </div>
            </div>
            ")) + @"
        </div>
        " : "") + @"

        <!-- Code Duplications -->
        " + (report.Duplications.Any() ? @"
        <div class=""section"">
            <h2 class=""section-title"">📋 Code Duplications</h2>
            " + string.Join("", report.Duplications.Take(5).Select(d =>
        {
            var loc1 = d.Locations.FirstOrDefault();
            var loc2 = d.Locations.Skip(1).FirstOrDefault();
            return @"
            <div class=""issue-card low"">
                <div class=""issue-header"">
                    <div class=""issue-title"">Duplication: " + d.Type + @" (" + d.SimilarityPercentage.ToString("F1") + @"% similar)</div>
                </div>
                <div class=""issue-meta"">
                    📁 " + (loc1 != null ? System.Net.WebUtility.HtmlEncode(loc1.FilePath) : "") + @" ↔️ " + (loc2 != null ? System.Net.WebUtility.HtmlEncode(loc2.FilePath) : "") + @"<br>
                    📍 Lines " + (loc1?.StartLine ?? 0) + @"-" + (loc1?.EndLine ?? 0) + @" | " + d.LineCount + @" lines duplicated
                </div>
                <div class=""suggested-fix"">
                    <div class=""suggested-fix-label"">💡 Suggestion</div>
                    <div class=""suggested-fix-text"">" + System.Net.WebUtility.HtmlEncode(d.Suggestion) + @"</div>
                </div>
            </div>
            ";
        })) + @"
        </div>
        " : "") + @"

        " + (includeHeaderFooter ? @"
        <!-- Footer -->
        <div class=""footer"">
            <div class=""footer-logo"">Rheal AI</div>
            <div class=""footer-text"">
                Powered by Rheal AI - Advanced Code Analysis Platform<br>
                Report generated on " + report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss") + @"
            </div>
        </div>
        " : "") + @"
    </div>
</body>
</html>";
    }
}