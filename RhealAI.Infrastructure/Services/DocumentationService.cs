using System.Text.Json;
using OpenAI.Chat;
using RhealAI.Application.Interfaces;
using RhealAI.Application.Prompts;
using RhealAI.Domain.Entities;
using RhealAI.Domain.Enums;
using RhealAI.Infrastructure.AI;
using RhealAI.Infrastructure.FileProcessing;
using Microsoft.Extensions.Configuration;

namespace RhealAI.Infrastructure.Services;

/// <summary>
/// Service for analyzing documentation and extracting/generating standards
/// </summary>
public class DocumentationService : IDocumentationService
{
    private readonly AgentFactory _agentFactory;
    private readonly IConfiguration _configuration;
    private readonly IProgressHub? _progressHub;

    public DocumentationService(AgentFactory agentFactory, IConfiguration configuration, IProgressHub? progressHub = null)
    {
        _agentFactory = agentFactory;
        _configuration = configuration;
        _progressHub = progressHub;
    }

    public async Task<List<Standard>> ExtractStandardsFromMarkdownAsync(List<CodeFile> markdownFiles, string? connectionId = null)
    {
        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            await SendProgress(connectionId, 26, "Extracting standards from documentation files...");
            for (int i = 0; i < markdownFiles.Count; i++)
            {
                var file = markdownFiles[i];
                var fileName = Path.GetFileName(file.FilePath);
                await SendProgress(connectionId, 26 + (i * 12 / markdownFiles.Count),
                    $"Reading documentation: {fileName}");
                await Task.Delay(150);
            }
            return await Task.FromResult(GenerateMockStandards(markdownFiles));
        }

        await SendProgress(connectionId, 26, $"Analyzing {markdownFiles.Count} documentation files...");
        var allStandards = new List<Standard>();

        for (int i = 0; i < markdownFiles.Count; i++)
        {
            var file = markdownFiles[i];
            if (file.FileType == FileType.Markdown)
            {
                var fileName = Path.GetFileName(file.FilePath);
                await SendProgress(connectionId, 26 + (i * 12 / markdownFiles.Count),
                    $"Extracting standards from: {fileName}");

                var client = _agentFactory.CreateStandardsClient();
                var prompt = string.Format(StandardsAnalysisPrompts.ExtractFromMarkdown, file.Content);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a coding standards expert. Extract standards from documentation and return JSON."),
                    new UserChatMessage(prompt)
                };

                var completion = await client.CompleteChatAsync(messages);
                var responseText = completion.Value.Content[0].Text;
                var standards = ParseStandardsFromResponse(responseText, file.FilePath, isFromDocs: true);
                allStandards.AddRange(standards);
            }
        }

        await SendProgress(connectionId, 38, $"Extracted {allStandards.Count} standards from documentation");
        return allStandards;
    }

    public async Task<List<Standard>> GenerateStandardsFromCodebaseAsync(List<CodeFile> codeFiles, string? connectionId = null)
    {
        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            await SendProgress(connectionId, 26, "Analyzing project structure...");
            await Task.Delay(200);
            await SendProgress(connectionId, 30, "Identifying common patterns...");
            await Task.Delay(200);
            await SendProgress(connectionId, 35, "Generating coding standards...");
            await Task.Delay(200);
            return await Task.FromResult(GenerateMockStandards(codeFiles));
        }

        await SendProgress(connectionId, 26, "Analyzing project structure and code patterns...");

        var client = _agentFactory.CreateStandardsClient();

        // Take representative files for analysis (limit to avoid token limits)
        var sampledFiles = codeFiles
            .Where(f => FileAnalyzer.IsCodeFile(f.FileType))
            .Take(20)
            .ToList();

        await SendProgress(connectionId, 30, $"AI analyzing {sampledFiles.Count} code files for patterns...");

        var filesContent = string.Join("\n\n", sampledFiles.Select(f => $"File: {f.FilePath}\n```\n{f.Content}\n```"));
        var prompt = string.Format(StandardsAnalysisPrompts.GenerateFromCodebase, filesContent);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a coding standards expert. Generate standards from code and return JSON."),
            new UserChatMessage(prompt)
        };

        await SendProgress(connectionId, 35, "AI generating coding standards from codebase...");

        var completion = await client.CompleteChatAsync(messages);
        var responseText = completion.Value.Content[0].Text;
        var standards = ParseStandardsFromResponse(responseText, "AI-Generated", isFromDocs: false);

        await SendProgress(connectionId, 40, $"Generated {standards.Count} coding standards");
        return standards;
    }

    private List<Standard> ParseStandardsFromResponse(string response, string sourceFile, bool isFromDocs)
    {
        try
        {
            // Extract JSON from response (may be wrapped in markdown code blocks)
            var jsonContent = ExtractJsonFromResponse(response);
            var standards = JsonSerializer.Deserialize<List<StandardDto>>(jsonContent);

            return standards?.Select(s => new Standard
            {
                Name = s.Name ?? "Unnamed Standard",
                Description = s.Description ?? "",
                Category = s.Category ?? "General",
                IsFromExistingDocs = isFromDocs,
                SourceFile = sourceFile,
                Examples = s.Examples ?? new List<string>()
            }).ToList() ?? new List<Standard>();
        }
        catch
        {
            // If parsing fails, return empty list
            return new List<Standard>();
        }
    }

    private string ExtractJsonFromResponse(string response)
    {
        // Remove markdown code blocks if present
        var cleaned = response.Trim();
        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned.Substring(7);
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Substring(3);
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3);
        }

        return cleaned.Trim();
    }

    private List<Standard> GenerateMockStandards(List<CodeFile> files)
    {
        return new List<Standard>
        {
            new Standard
            {
                Name = "Consistent Naming Conventions",
                Description = "Use PascalCase for class names, camelCase for variables, and UPPERCASE for constants.",
                Category = "Naming",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "public class UserService { }",
                    "private string userName;",
                    "const int MAX_RETRIES = 3;"
                }
            },
            new Standard
            {
                Name = "Error Handling",
                Description = "Always use try-catch blocks for operations that may throw exceptions. Log errors appropriately.",
                Category = "Error Handling",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "try { await service.Process(); } catch (Exception ex) { _logger.LogError(ex, \"Error\"); }"
                }
            },
            new Standard
            {
                Name = "Code Documentation",
                Description = "All public methods must have XML documentation comments.",
                Category = "Documentation",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "/// <summary>\n/// Processes the user request\n/// </summary>"
                }
            },
            new Standard
            {
                Name = "Async/Await Usage",
                Description = "Use async/await for I/O operations. Avoid blocking calls like .Result or .Wait().",
                Category = "Performance",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "await repository.SaveAsync();",
                    "var data = await httpClient.GetStringAsync(url);"
                }
            },
            new Standard
            {
                Name = "Dependency Injection",
                Description = "Use constructor injection for dependencies. Avoid service locator pattern.",
                Category = "Architecture",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "public class Service { private readonly IRepository _repo; public Service(IRepository repo) { _repo = repo; } }"
                }
            }
        };
    }

    private async Task SendProgress(string? connectionId, int progress, string message)
    {
        if (_progressHub != null && !string.IsNullOrEmpty(connectionId))
        {
            await _progressHub.SendProgressAsync(connectionId, progress, message);
        }
    }

    private class StandardDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Examples { get; set; }
    }
}
