using System.Text.Json;
using OpenAI.Chat;
using RhealAI.Application.Interfaces;
using RhealAI.Application.Prompts;
using RhealAI.Domain.Entities;
using RhealAI.Domain.Enums;
using RhealAI.Infrastructure.AI;
using Microsoft.Extensions.Configuration;

namespace RhealAI.Infrastructure.Services;

/// <summary>
/// Service for AI-powered code analysis
/// </summary>
public class AIAnalysisService : IAIAnalysisService
{
    private readonly AgentFactory _agentFactory;
    private readonly IConfiguration _configuration;
    private readonly IProgressHub? _progressHub;

    public AIAnalysisService(AgentFactory agentFactory, IConfiguration configuration, IProgressHub? progressHub = null)
    {
        _agentFactory = agentFactory;
        _configuration = configuration;
        _progressHub = progressHub;
    }

    public async Task<List<Violation>> AnalyzeCodeViolationsAsync(List<CodeFile> files, List<Standard> standards, string? connectionId = null)
    {
        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            // Demo mode with detailed progress
            await SendProgress(connectionId, 42, "Demo Mode: Generating mock violations...");
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var fileName = Path.GetFileName(file.FilePath);
                var fileType = GetFileType(file.FilePath);
                await SendProgress(connectionId, 42 + (i * 25 / files.Count),
                    $"Analyzing {fileType}: {fileName}");
                await Task.Delay(100); // Small delay to show progress
            }
            return await Task.FromResult(GenerateMockViolations(files, standards));
        }

        var client = _agentFactory.CreateViolationDetectionClient();
        var violations = new List<Violation>();

        // Format standards
        var standardsText = string.Join("\n", standards.Select(s =>
            $"- {s.Name}: {s.Description} (Category: {s.Category})"));

        // Analyze files in batches to manage token limits
        var batches = files.Chunk(10).ToList();
        var totalBatches = batches.Count;
        var currentBatch = 0;

        foreach (var batch in batches)
        {
            currentBatch++;

            // Report progress for each file in the batch
            for (int i = 0; i < batch.Length; i++)
            {
                var file = batch[i];
                var fileName = Path.GetFileName(file.FilePath);
                var fileType = GetFileType(file.FilePath);
                var overallProgress = 42 + ((currentBatch - 1) * 25 / totalBatches) + (i * 25 / (totalBatches * batch.Length));
                await SendProgress(connectionId, overallProgress,
                    $"AI analyzing {fileType}: {fileName}");
            }

            var filesText = string.Join("\n\n", batch.Select(f =>
                $"File: {f.FilePath}\n```\n{f.Content}\n```"));

            var prompt = string.Format(ViolationDetectionPrompts.DetectViolations, standardsText, filesText);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a coding standards enforcer. Return violations in JSON format."),
                new UserChatMessage(prompt)
            };

            await SendProgress(connectionId, 42 + (currentBatch * 25 / totalBatches),
                $"Processing batch {currentBatch}/{totalBatches} with AI model...");

            var completion = await client.CompleteChatAsync(messages);
            var responseText = completion.Value.Content[0].Text;
            var batchViolations = ParseViolationsFromResponse(responseText);
            violations.AddRange(batchViolations);
        }

        return violations;
    }

    public async Task<List<Bug>> DetectBugsAsync(List<CodeFile> files, string? connectionId = null)
    {
        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            // Demo mode with detailed progress
            await SendProgress(connectionId, 72, "Demo Mode: Detecting potential bugs...");
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var fileName = Path.GetFileName(file.FilePath);
                var fileType = GetFileType(file.FilePath);
                await SendProgress(connectionId, 72 + (i * 15 / files.Count),
                    $"Bug detection in {fileType}: {fileName}");
                await Task.Delay(100); // Small delay to show progress
            }
            return await Task.FromResult(GenerateMockBugs(files));
        }

        var client = _agentFactory.CreateBugDetectionClient();
        var bugs = new List<Bug>();

        // Analyze files in batches
        var batches = files.Chunk(10).ToList();
        var totalBatches = batches.Count;
        var currentBatch = 0;

        foreach (var batch in batches)
        {
            currentBatch++;

            // Report progress for each file in the batch
            for (int i = 0; i < batch.Length; i++)
            {
                var file = batch[i];
                var fileName = Path.GetFileName(file.FilePath);
                var fileType = GetFileType(file.FilePath);
                var overallProgress = 72 + ((currentBatch - 1) * 15 / totalBatches) + (i * 15 / (totalBatches * batch.Length));
                await SendProgress(connectionId, overallProgress,
                    $"AI bug detection in {fileType}: {fileName}");
            }

            var filesText = string.Join("\n\n", batch.Select(f =>
                $"File: {f.FilePath}\n```\n{f.Content}\n```"));

            var prompt = string.Format(BugDetectionPrompts.DetectBugs, filesText);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a bug detection expert. Return bugs in JSON format."),
                new UserChatMessage(prompt)
            };

            await SendProgress(connectionId, 72 + (currentBatch * 15 / totalBatches),
                $"AI analyzing batch {currentBatch}/{totalBatches} for bugs...");

            var completion = await client.CompleteChatAsync(messages);
            var responseText = completion.Value.Content[0].Text;
            var batchBugs = ParseBugsFromResponse(responseText);
            bugs.AddRange(batchBugs);
        }

        return bugs;
    }

    private List<Violation> ParseViolationsFromResponse(string response)
    {
        try
        {
            var jsonContent = ExtractJsonFromResponse(response);
            var dtos = JsonSerializer.Deserialize<List<ViolationDto>>(jsonContent);

            return dtos?.Select(dto => new Violation
            {
                FilePath = dto.FilePath ?? "",
                LineNumber = dto.LineNumber,
                RuleName = dto.RuleName ?? "Unknown Rule",
                Description = dto.Description ?? "",
                Type = ParseViolationType(dto.Type),
                Severity = ParseSeverity(dto.Severity),
                CodeSnippet = dto.CodeSnippet ?? "",
                SuggestedFix = dto.SuggestedFix ?? ""
            }).ToList() ?? new List<Violation>();
        }
        catch
        {
            return new List<Violation>();
        }
    }

    private List<Bug> ParseBugsFromResponse(string response)
    {
        try
        {
            var jsonContent = ExtractJsonFromResponse(response);
            var dtos = JsonSerializer.Deserialize<List<BugDto>>(jsonContent);

            return dtos?.Select(dto => new Bug
            {
                FilePath = dto.FilePath ?? "",
                LineNumber = dto.LineNumber,
                Title = dto.Title ?? "Untitled Bug",
                Description = dto.Description ?? "",
                RootCause = dto.RootCause ?? "",
                Impact = dto.Impact ?? "",
                Severity = ParseSeverity(dto.Severity),
                CodeSnippet = dto.CodeSnippet ?? "",
                ReproductionSteps = dto.ReproductionSteps ?? new List<string>(),
                SuggestedFix = dto.SuggestedFix ?? ""
            }).ToList() ?? new List<Bug>();
        }
        catch
        {
            return new List<Bug>();
        }
    }

    private string ExtractJsonFromResponse(string response)
    {
        var cleaned = response.Trim();
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned.Substring(7);
        else if (cleaned.StartsWith("```"))
            cleaned = cleaned.Substring(3);

        if (cleaned.EndsWith("```"))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);

        return cleaned.Trim();
    }

    private ViolationType ParseViolationType(string? type)
    {
        return type?.ToLower() switch
        {
            "namingconvention" => ViolationType.NamingConvention,
            "architecture" => ViolationType.Architecture,
            "security" => ViolationType.Security,
            "performance" => ViolationType.Performance,
            "codesmell" => ViolationType.CodeSmell,
            "documentation" => ViolationType.Documentation,
            "testing" => ViolationType.Testing,
            "errorhandling" => ViolationType.ErrorHandling,
            _ => ViolationType.BestPractice
        };
    }

    private SeverityLevel ParseSeverity(string? severity)
    {
        return severity?.ToLower() switch
        {
            "critical" => SeverityLevel.Critical,
            "high" => SeverityLevel.High,
            "medium" => SeverityLevel.Medium,
            _ => SeverityLevel.Low
        };
    }

    private class ViolationDto
    {
        public string? FilePath { get; set; }
        public int LineNumber { get; set; }
        public string? RuleName { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string? CodeSnippet { get; set; }
        public string? SuggestedFix { get; set; }
    }

    private class BugDto
    {
        public string? FilePath { get; set; }
        public int LineNumber { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? RootCause { get; set; }
        public string? Impact { get; set; }
        public string? Severity { get; set; }
        public string? CodeSnippet { get; set; }
        public List<string>? ReproductionSteps { get; set; }
        public string? SuggestedFix { get; set; }
    }

    private List<Violation> GenerateMockViolations(List<CodeFile> files, List<Standard> standards)
    {
        var violations = new List<Violation>();
        var random = new Random(42); // Fixed seed for consistent results

        foreach (var file in files.Take(5))
        {
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = random.Next(1, Math.Max(2, file.LineCount)),
                RuleName = standards.FirstOrDefault()?.Name ?? "Naming Convention",
                Description = $"Variable naming does not follow {standards.FirstOrDefault()?.Name ?? "standard naming"} convention",
                Type = ViolationType.NamingConvention,
                Severity = SeverityLevel.Medium,
                CodeSnippet = "var user_name = \"John\";",
                SuggestedFix = "var userName = \"John\";"
            });
        }

        return violations;
    }

    private List<Bug> GenerateMockBugs(List<CodeFile> files)
    {
        var bugs = new List<Bug>();
        var random = new Random(42);

        foreach (var file in files.Take(3))
        {
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = random.Next(1, Math.Max(2, file.LineCount)),
                Title = "Potential Null Reference Exception",
                Description = "Object may be null when accessed",
                RootCause = "Missing null check before accessing object property",
                Impact = "Application may crash with NullReferenceException at runtime",
                Severity = SeverityLevel.High,
                CodeSnippet = "var result = user.Name.ToUpper();",
                ReproductionSteps = new List<string>
                {
                    "Call the method with a null user object",
                    "Access the Name property",
                    "NullReferenceException is thrown"
                },
                SuggestedFix = "var result = user?.Name?.ToUpper() ?? string.Empty;"
            });
        }

        return bugs;
    }

    private async Task SendProgress(string? connectionId, int progress, string message)
    {
        if (_progressHub != null && !string.IsNullOrEmpty(connectionId))
        {
            await _progressHub.SendProgressAsync(connectionId, progress, message);
        }
    }

    private string GetFileType(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var directory = Path.GetDirectoryName(filePath)?.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? "";

        // Determine file type based on directory and extension
        if (directory.Contains("Controller", StringComparison.OrdinalIgnoreCase))
            return "Controller";
        if (directory.Contains("Service", StringComparison.OrdinalIgnoreCase))
            return "Service";
        if (directory.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Entity", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Entities", StringComparison.OrdinalIgnoreCase))
            return "Model";
        if (directory.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Repositories", StringComparison.OrdinalIgnoreCase))
            return "Repository";
        if (directory.Contains("Interface", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Interfaces", StringComparison.OrdinalIgnoreCase))
            return "Interface";
        if (directory.Contains("DTO", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("DTOs", StringComparison.OrdinalIgnoreCase))
            return "DTO";
        if (directory.Contains("Helper", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Helpers", StringComparison.OrdinalIgnoreCase))
            return "Helper";
        if (directory.Contains("Util", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Utils", StringComparison.OrdinalIgnoreCase))
            return "Utility";
        if (directory.Contains("Config", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Configuration", StringComparison.OrdinalIgnoreCase))
            return "Configuration";
        if (directory.Contains("Middleware", StringComparison.OrdinalIgnoreCase))
            return "Middleware";
        if (directory.Contains("Filter", StringComparison.OrdinalIgnoreCase) ||
            directory.Contains("Filters", StringComparison.OrdinalIgnoreCase))
            return "Filter";

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "C# File",
            ".vb" => "VB.NET File",
            ".vbproj" => "VB.NET Project",
            ".aspx" => "ASP.NET Page",
            ".ascx" => "ASP.NET User Control",
            ".asmx" => "ASP.NET Web Service",
            ".ashx" => "ASP.NET Handler",
            ".master" => "ASP.NET Master Page",
            ".vbhtml" => "VB.NET Razor View",
            ".cshtml" => "C# Razor View",
            ".resx" => "Resource File",
            ".ts" => "TypeScript File",
            ".js" => "JavaScript File",
            ".html" => "HTML Template",
            ".htm" => "HTML File",
            ".css" => "Stylesheet",
            ".scss" => "SCSS Stylesheet",
            ".json" => "JSON Config",
            ".xml" => "XML File",
            ".config" => "Configuration File",
            ".md" => "Documentation",
            ".txt" => "Text File",
            ".sql" => "SQL Script",
            _ => "Code File"
        };
    }

    public async Task<ProjectSummary> AnalyzeProjectStructureAsync(
        List<CodeFile> files,
        Dictionary<string, int> folderStructure,
        Dictionary<string, int> fileTypeDistribution,
        string? connectionId = null)
    {
        await SendProgress(connectionId, 7, "Analyzing project structure and business logic...");

        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateProjectSummary(files, folderStructure, fileTypeDistribution);
        }

        // For real AI providers, use AI to analyze the project
        var client = _agentFactory.CreateViolationDetectionClient();

        // Sample key files for analysis
        var keyFiles = files
            .Where(f => IsKeyFile(f.FilePath))
            .Take(10)
            .Select(f => $"{f.FilePath}:\n{f.Content.Substring(0, Math.Min(500, f.Content.Length))}")
            .ToList();

        var prompt = $@"Analyze this project and provide a comprehensive summary:

Folder Structure:
{string.Join("\n", folderStructure.Select(kvp => $"- {kvp.Key}: {kvp.Value} files"))}

File Type Distribution:
{string.Join("\n", fileTypeDistribution.Select(kvp => $"- {kvp.Key}: {kvp.Value} files"))}

Sample Key Files:
{string.Join("\n\n", keyFiles)}

Provide a detailed analysis including:
1. Project name and description
2. Technology stack
3. Architecture pattern (e.g., Clean Architecture, MVC, Microservices)
4. Business logic and core functionality
5. Key features
6. Main components
7. Primary programming language
8. Dependencies";

        try
        {
            var response = await client.CompleteChatAsync(prompt);
            // Parse AI response into ProjectSummary
            return ParseAIResponse(response.Value.Content[0].Text, folderStructure, fileTypeDistribution);
        }
        catch
        {
            // Fallback to demo analysis
            return GenerateProjectSummary(files, folderStructure, fileTypeDistribution);
        }
    }

    private bool IsKeyFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var keyPatterns = new[] {
            "program", "startup", "main", "app", "index",
            "controller", "service", "repository", "model",
            "package.json", "pom.xml", "build.gradle", "cargo.toml",
            "requirements.txt", "gemfile", "composer.json",
            ".csproj", ".vbproj", ".fsproj", ".sln"
        };

        return keyPatterns.Any(pattern => fileName.Contains(pattern));
    }

    private ProjectSummary GenerateProjectSummary(
        List<CodeFile> files,
        Dictionary<string, int> folderStructure,
        Dictionary<string, int> fileTypeDistribution)
    {
        // Determine primary language
        var primaryLanguage = fileTypeDistribution
            .Where(kvp => !new[] { "Configuration", "Documentation", "Markdown" }.Contains(kvp.Key))
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault().Key ?? "Mixed";

        // Detect technology stack
        var techStack = DetectTechnologyStack(files, fileTypeDistribution);

        // Detect architecture pattern
        var architecture = DetectArchitecturePattern(folderStructure);

        // Extract business logic
        var businessLogic = ExtractBusinessLogic(files, folderStructure);

        // Identify key features
        var keyFeatures = IdentifyKeyFeatures(files, folderStructure);

        // Identify main components
        var mainComponents = IdentifyMainComponents(folderStructure);

        // Extract dependencies
        var dependencies = ExtractDependencies(files);

        return new ProjectSummary
        {
            ProjectName = Path.GetFileName(Environment.CurrentDirectory),
            Description = GenerateProjectDescription(techStack, architecture, businessLogic),
            TechnologyStack = techStack,
            Architecture = architecture,
            BusinessLogic = businessLogic,
            CoreFunctionality = GenerateCoreFunctionality(files, folderStructure),
            KeyFeatures = keyFeatures,
            FolderStructure = folderStructure,
            FileTypeDistribution = fileTypeDistribution,
            MainComponents = mainComponents,
            PrimaryLanguage = primaryLanguage,
            Dependencies = dependencies
        };
    }

    private string DetectTechnologyStack(List<CodeFile> files, Dictionary<string, int> fileTypeDistribution)
    {
        var stacks = new List<string>();

        // Backend technologies
        if (fileTypeDistribution.ContainsKey("CSharp") || fileTypeDistribution.ContainsKey("VisualBasic"))
            stacks.Add(".NET");
        if (fileTypeDistribution.ContainsKey("Java"))
            stacks.Add("Java");
        if (fileTypeDistribution.ContainsKey("Python"))
            stacks.Add("Python");
        if (fileTypeDistribution.ContainsKey("PHP"))
            stacks.Add("PHP");
        if (fileTypeDistribution.ContainsKey("Ruby"))
            stacks.Add("Ruby");
        if (fileTypeDistribution.ContainsKey("Go"))
            stacks.Add("Go");
        if (fileTypeDistribution.ContainsKey("Rust"))
            stacks.Add("Rust");

        // Frontend technologies
        if (fileTypeDistribution.ContainsKey("TypeScript"))
            stacks.Add("TypeScript");
        if (fileTypeDistribution.ContainsKey("JavaScript") || fileTypeDistribution.ContainsKey("JSX"))
            stacks.Add("JavaScript");
        if (fileTypeDistribution.ContainsKey("Vue"))
            stacks.Add("Vue.js");
        if (fileTypeDistribution.ContainsKey("Angular") || files.Any(f => f.Content.Contains("@angular")))
            stacks.Add("Angular");
        if (files.Any(f => f.Content.Contains("react")))
            stacks.Add("React");

        // Database
        if (fileTypeDistribution.ContainsKey("SQL") || files.Any(f => f.Content.Contains("SqlConnection") || f.Content.Contains("EntityFramework")))
            stacks.Add("SQL Database");

        return string.Join(", ", stacks.Distinct());
    }

    private string DetectArchitecturePattern(Dictionary<string, int> folderStructure)
    {
        var folders = folderStructure.Keys.Select(k => k.ToLowerInvariant()).ToList();

        if (folders.Any(f => f.Contains("domain")) && folders.Any(f => f.Contains("application")) && folders.Any(f => f.Contains("infrastructure")))
            return "Clean Architecture (DDD)";

        if (folders.Any(f => f.Contains("models")) && folders.Any(f => f.Contains("views")) && folders.Any(f => f.Contains("controllers")))
            return "MVC (Model-View-Controller)";

        if (folders.Any(f => f.Contains("services")) && folders.Any(f => f.Contains("api")))
            return "Service-Oriented Architecture";

        if (folders.Any(f => f.Contains("components")) && folders.Any(f => f.Contains("services")))
            return "Component-Based Architecture";

        if (folders.Any(f => f.Contains("layers") || f.Contains("business") || f.Contains("data")))
            return "Layered Architecture";

        return "Custom Architecture";
    }

    private string ExtractBusinessLogic(List<CodeFile> files, Dictionary<string, int> folderStructure)
    {
        var businessKeywords = new Dictionary<string, string>
        {
            { "analysis", "Code Analysis" },
            { "report", "Reporting" },
            { "repository", "Data Management" },
            { "authentication", "Security & Authentication" },
            { "authorization", "Access Control" },
            { "payment", "Payment Processing" },
            { "order", "Order Management" },
            { "inventory", "Inventory Management" },
            { "user", "User Management" },
            { "product", "Product Catalog" },
            { "invoice", "Invoicing" },
            { "notification", "Notifications" },
            { "workflow", "Workflow Management" },
            { "task", "Task Management" },
            { "scheduler", "Job Scheduling" }
        };

        var detectedLogic = new List<string>();

        foreach (var kvp in businessKeywords)
        {
            var hasFolder = folderStructure.Keys.Any(f => f.ToLowerInvariant().Contains(kvp.Key));
            var hasFiles = files.Any(f => f.FilePath.ToLowerInvariant().Contains(kvp.Key));

            if (hasFolder || hasFiles)
                detectedLogic.Add(kvp.Value);
        }

        return detectedLogic.Any()
            ? string.Join(", ", detectedLogic.Distinct())
            : "General Purpose Application";
    }

    private string GenerateProjectDescription(string techStack, string architecture, string businessLogic)
    {
        return $"A {architecture}-based application built with {techStack}. " +
               $"The project implements {businessLogic} functionality with a focus on maintainability and scalability.";
    }

    private string GenerateCoreFunctionality(List<CodeFile> files, Dictionary<string, int> folderStructure)
    {
        var functionalities = new List<string>();

        // Check for API/Web functionality
        if (files.Any(f => f.Content.Contains("[ApiController]") || f.Content.Contains("@RestController") || f.Content.Contains("app.get(")))
            functionalities.Add("RESTful API Services");

        // Check for database operations
        if (files.Any(f => f.Content.Contains("DbContext") || f.Content.Contains("@Entity") || f.Content.Contains("SELECT") || f.Content.Contains("INSERT")))
            functionalities.Add("Database Operations");

        // Check for UI
        if (files.Any(f => f.FilePath.EndsWith(".html") || f.FilePath.EndsWith(".jsx") || f.FilePath.EndsWith(".tsx") || f.FilePath.EndsWith(".vue")))
            functionalities.Add("User Interface");

        // Check for authentication
        if (files.Any(f => f.Content.Contains("authentication") || f.Content.Contains("jwt") || f.Content.Contains("login") || f.Content.Contains("authorize")))
            functionalities.Add("Authentication & Authorization");

        // Check for external integrations
        if (files.Any(f => f.Content.Contains("HttpClient") || f.Content.Contains("axios") || f.Content.Contains("fetch")))
            functionalities.Add("External API Integration");

        return functionalities.Any()
            ? string.Join(", ", functionalities)
            : "Core application logic and data processing";
    }

    private List<string> IdentifyKeyFeatures(List<CodeFile> files, Dictionary<string, int> folderStructure)
    {
        var features = new List<string>();

        // AI/ML features
        if (files.Any(f => f.Content.Contains("OpenAI") || f.Content.Contains("ChatGPT") || f.Content.Contains("AI") || f.Content.Contains("MachineLearning")))
            features.Add("AI-Powered Analysis");

        // Real-time features
        if (files.Any(f => f.Content.Contains("SignalR") || f.Content.Contains("WebSocket") || f.Content.Contains("socket.io")))
            features.Add("Real-Time Communication");

        // File processing
        if (files.Any(f => f.Content.Contains("ZipFile") || f.Content.Contains("FileStream") || f.Content.Contains("Upload")))
            features.Add("File Upload & Processing");

        // Git integration
        if (files.Any(f => f.Content.Contains("LibGit2") || f.Content.Contains("GitRepository") || f.Content.Contains("git")))
            features.Add("Git Repository Integration");

        // Reporting
        if (folderStructure.Keys.Any(f => f.ToLowerInvariant().Contains("report")))
            features.Add("Comprehensive Reporting");

        // Dashboard
        if (files.Any(f => f.FilePath.ToLowerInvariant().Contains("dashboard")))
            features.Add("Analytics Dashboard");

        return features.Any()
            ? features
            : new List<string> { "Data Processing", "Business Logic", "User Management" };
    }

    private List<string> IdentifyMainComponents(Dictionary<string, int> folderStructure)
    {
        var components = new List<string>();

        foreach (var folder in folderStructure.Keys)
        {
            var folderName = Path.GetFileName(folder);
            if (folderName.Length > 3 && folderStructure[folder] > 5)
            {
                components.Add($"{folderName} ({folderStructure[folder]} files)");
            }
        }

        return components.Take(10).ToList();
    }

    private List<string> ExtractDependencies(List<CodeFile> files)
    {
        var dependencies = new List<string>();

        // Check package.json
        var packageJson = files.FirstOrDefault(f => f.FilePath.EndsWith("package.json"));
        if (packageJson != null)
        {
            if (packageJson.Content.Contains("\"@angular/"))
                dependencies.Add("Angular Framework");
            if (packageJson.Content.Contains("\"react\""))
                dependencies.Add("React Library");
            if (packageJson.Content.Contains("\"vue\""))
                dependencies.Add("Vue.js Framework");
        }

        // Check .csproj
        var csproj = files.FirstOrDefault(f => f.FilePath.EndsWith(".csproj"));
        if (csproj != null)
        {
            if (csproj.Content.Contains("EntityFrameworkCore"))
                dependencies.Add("Entity Framework Core");
            if (csproj.Content.Contains("Swashbuckle"))
                dependencies.Add("Swagger/OpenAPI");
            if (csproj.Content.Contains("SignalR"))
                dependencies.Add("SignalR");
        }

        return dependencies.Take(10).ToList();
    }

    private ProjectSummary ParseAIResponse(
        string aiResponse,
        Dictionary<string, int> folderStructure,
        Dictionary<string, int> fileTypeDistribution)
    {
        // Fallback to demo analysis if AI response parsing fails
        return new ProjectSummary
        {
            ProjectName = "Analyzed Project",
            Description = aiResponse.Length > 500 ? aiResponse.Substring(0, 500) : aiResponse,
            FolderStructure = folderStructure,
            FileTypeDistribution = fileTypeDistribution
        };
    }
}

