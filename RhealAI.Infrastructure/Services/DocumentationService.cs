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

        // Take representative files for analysis (increased to 50 for comprehensive analysis)
        var sampledFiles = codeFiles
            .Where(f => FileAnalyzer.IsCodeFile(f.FileType))
            .Take(50)
            .ToList();

        await SendProgress(connectionId, 30, $"AI performing deep analysis on {sampledFiles.Count} code files...");

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
                Examples = s.Examples ?? new List<string>(),
                Rationale = s.Rationale ?? "",
                Severity = s.Severity ?? "Medium",
                AppliesTo = s.AppliesTo ?? new List<string>()
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
                Name = "PascalCase for Public Members",
                Description = "All public classes, methods, properties, and interfaces must use PascalCase naming convention. This ensures consistency across the codebase and follows .NET Framework Design Guidelines.",
                Category = "Naming Conventions",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "public class UserService { }",
                    "public interface IRepository { }",
                    "public string UserName { get; set; }",
                    "public async Task ProcessDataAsync() { }"
                },
                Rationale = "PascalCase improves readability and follows industry standards. It helps distinguish public APIs from internal implementation details.",
                Severity = "High",
                AppliesTo = new List<string> { "Classes", "Methods", "Properties", "Interfaces" }
            },
            new Standard
            {
                Name = "camelCase for Local Variables",
                Description = "Local variables and method parameters should use camelCase naming convention. Private fields should be prefixed with underscore (_camelCase).",
                Category = "Naming Conventions",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "private string _userName;",
                    "var userId = GetUserId();",
                    "public void Process(string inputData) { }"
                },
                Rationale = "camelCase for locals and underscore prefix for fields helps distinguish between different scopes and improves code readability.",
                Severity = "High",
                AppliesTo = new List<string> { "Variables", "Parameters", "Fields" }
            },
            new Standard
            {
                Name = "Comprehensive Error Handling",
                Description = "All operations that may throw exceptions must be wrapped in try-catch blocks. Exceptions should be logged with appropriate context before being handled or rethrown. Never swallow exceptions silently.",
                Category = "Error Handling",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "try { await service.Process(); } catch (Exception ex) { _logger.LogError(ex, \"Error processing\"); throw; }",
                    "catch (NotFoundException ex) { return NotFound(ex.Message); }",
                    "catch (ValidationException ex) { return BadRequest(ex.Errors); }"
                },
                Rationale = "Proper error handling prevents application crashes, aids in debugging, and provides better user experience through meaningful error messages.",
                Severity = "Critical",
                AppliesTo = new List<string> { "Controllers", "Services", "Repositories" }
            },
            new Standard
            {
                Name = "XML Documentation for Public APIs",
                Description = "All public classes, methods, and properties must have XML documentation comments including summary, parameter descriptions, return values, and exceptions thrown. This enables IntelliSense and auto-generated documentation.",
                Category = "Documentation",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "/// <summary>\n/// Processes the user request\n/// </summary>\n/// <param name=\"userId\">The user identifier</param>\n/// <returns>Processing result</returns>",
                    "/// <exception cref=\"NotFoundException\">Thrown when user not found</exception>"
                },
                Rationale = "Documentation improves code maintainability, helps new team members understand the codebase, and provides IntelliSense support in IDEs.",
                Severity = "High",
                AppliesTo = new List<string> { "Public Classes", "Public Methods", "Public Properties" }
            },
            new Standard
            {
                Name = "Async/Await for I/O Operations",
                Description = "All I/O operations (database calls, HTTP requests, file operations) must use async/await pattern. Never block on async code using .Result or .Wait(). Use Task or Task<T> return types for async methods.",
                Category = "Async Patterns",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "public async Task<User> GetUserAsync(string id) { return await _repository.FindAsync(id); }",
                    "await repository.SaveChangesAsync();",
                    "var data = await httpClient.GetStringAsync(url);",
                    "// Avoid: var result = GetDataAsync().Result; // BAD!"
                },
                Rationale = "Async/await prevents thread blocking, improves application scalability, and enables better resource utilization especially under high load.",
                Severity = "Critical",
                AppliesTo = new List<string> { "Controllers", "Services", "Repositories", "HTTP Clients" }
            },
            new Standard
            {
                Name = "Constructor Dependency Injection",
                Description = "Dependencies should be injected through constructors, not through property injection or service locator pattern. All dependencies should be interfaces, not concrete types. Dependencies should be stored as readonly fields.",
                Category = "Dependency Injection",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "private readonly IRepository _repository;\nprivate readonly ILogger _logger;\npublic Service(IRepository repository, ILogger logger) { _repository = repository; _logger = logger; }",
                    "// In Startup.cs: services.AddScoped<IUserService, UserService>();"
                },
                Rationale = "Constructor injection makes dependencies explicit, improves testability through dependency mocking, and follows SOLID principles (Dependency Inversion).",
                Severity = "Critical",
                AppliesTo = new List<string> { "Services", "Controllers", "Repositories" }
            },
            new Standard
            {
                Name = "Repository Pattern for Data Access",
                Description = "All data access must go through repository interfaces. Direct DbContext usage outside repositories is prohibited. Repositories should expose IQueryable or specific query methods.",
                Category = "Architecture Patterns",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "public interface IUserRepository { Task<User> GetByIdAsync(string id); Task<List<User>> GetAllAsync(); }",
                    "public class UserRepository : IUserRepository { private readonly DbContext _context; }"
                },
                Rationale = "Repository pattern abstracts data access, improves testability, centralizes data access logic, and makes it easier to switch data sources.",
                Severity = "High",
                AppliesTo = new List<string> { "Data Access Layer", "Repositories" }
            },
            new Standard
            {
                Name = "Single Responsibility Principle",
                Description = "Each class should have only one reason to change. Classes should focus on a single responsibility. Methods should be small and focused (preferably under 20 lines).",
                Category = "SOLID Principles",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "// Good: UserValidator only validates\npublic class UserValidator { public bool Validate(User user) { } }",
                    "// Good: UserRepository only handles data access\npublic class UserRepository : IUserRepository { }",
                    "// Bad: UserManager that validates, saves, sends email, logs - too many responsibilities"
                },
                Rationale = "SRP improves code maintainability, makes testing easier, reduces coupling, and makes code easier to understand and modify.",
                Severity = "High",
                AppliesTo = new List<string> { "All Classes", "Services", "Utilities" }
            },
            new Standard
            {
                Name = "DTOs for API Responses",
                Description = "Never expose domain entities directly through APIs. Always use Data Transfer Objects (DTOs) to control what data is sent to clients. Use AutoMapper or manual mapping between entities and DTOs.",
                Category = "API Design",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "public class UserDto { public string Id { get; set; } public string Name { get; set; } }",
                    "var userDto = _mapper.Map<UserDto>(user);",
                    "return Ok(userDto); // Never: return Ok(user); where user is domain entity"
                },
                Rationale = "DTOs decouple API contracts from domain models, prevent over-posting vulnerabilities, control serialization, and enable API versioning.",
                Severity = "Critical",
                AppliesTo = new List<string> { "Controllers", "API Responses" }
            },
            new Standard
            {
                Name = "LINQ for Collection Operations",
                Description = "Use LINQ methods (Where, Select, FirstOrDefault, Any, etc.) for collection operations instead of loops when possible. Use method syntax for simple operations and query syntax for complex queries.",
                Category = "Code Style",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "var activeUsers = users.Where(u => u.IsActive).ToList();",
                    "var userNames = users.Select(u => u.Name).ToList();",
                    "var hasAdmins = users.Any(u => u.Role == \"Admin\");",
                    "var firstMatch = users.FirstOrDefault(u => u.Email == email);"
                },
                Rationale = "LINQ provides more readable and maintainable code, is optimized by compiler, reduces bugs from manual loop logic, and works well with async operations.",
                Severity = "Medium",
                AppliesTo = new List<string> { "Services", "Repositories", "Utilities" }
            },
            new Standard
            {
                Name = "Interface Segregation",
                Description = "Interfaces should be small and focused. Clients should not be forced to depend on methods they don't use. Create multiple specific interfaces rather than one large interface.",
                Category = "SOLID Principles",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "// Good: Separate interfaces\npublic interface IUserReader { Task<User> GetAsync(string id); }\npublic interface IUserWriter { Task SaveAsync(User user); }",
                    "// Bad: Single large interface with many methods"
                },
                Rationale = "Interface segregation reduces coupling, makes testing easier, improves code flexibility, and follows the principle that no client should depend on methods it doesn't use.",
                Severity = "Medium",
                AppliesTo = new List<string> { "Interfaces", "Service Contracts" }
            },
            new Standard
            {
                Name = "Structured Logging",
                Description = "Use structured logging with placeholders instead of string interpolation. Include relevant context (userId, requestId, etc.) in log messages. Use appropriate log levels (Debug, Info, Warning, Error, Critical).",
                Category = "Logging",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "_logger.LogInformation(\"User {UserId} logged in at {LoginTime}\", userId, DateTime.UtcNow);",
                    "_logger.LogError(ex, \"Failed to process order {OrderId} for user {UserId}\", orderId, userId);",
                    "// Avoid: _logger.LogInformation($\"User {userId} logged in\"); // String interpolation"
                },
                Rationale = "Structured logging enables better log querying, improves performance by deferring string formatting, and provides better context for debugging production issues.",
                Severity = "High",
                AppliesTo = new List<string> { "All Classes", "Services", "Controllers" }
            },
            new Standard
            {
                Name = "Input Validation",
                Description = "All user inputs must be validated before processing. Use data annotations, FluentValidation, or manual validation. Return appropriate HTTP status codes (400 Bad Request) for validation failures.",
                Category = "Security",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "[Required]\n[StringLength(100)]\npublic string Name { get; set; }",
                    "if (!ModelState.IsValid) return BadRequest(ModelState);",
                    "var validator = new UserValidator();\nvar result = validator.Validate(user);\nif (!result.IsValid) return BadRequest(result.Errors);"
                },
                Rationale = "Input validation prevents security vulnerabilities (SQL injection, XSS), ensures data integrity, and provides better user experience through clear error messages.",
                Severity = "Critical",
                AppliesTo = new List<string> { "Controllers", "DTOs", "Models" }
            },
            new Standard
            {
                Name = "Resource Disposal",
                Description = "Always dispose of IDisposable resources using 'using' statements or try-finally blocks. Implement IDisposable for classes that hold unmanaged resources.",
                Category = "Resource Management",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "using var stream = File.OpenRead(path);\nusing var reader = new StreamReader(stream);",
                    "using (var connection = new SqlConnection(connectionString)) { await connection.OpenAsync(); }"
                },
                Rationale = "Proper resource disposal prevents memory leaks, releases file handles and connections, and ensures application stability under load.",
                Severity = "Critical",
                AppliesTo = new List<string> { "All Classes", "Data Access", "File Operations" }
            },
            new Standard
            {
                Name = "Immutable DTOs",
                Description = "DTOs should preferably be immutable using init-only setters or readonly properties with constructor initialization. This prevents accidental modification after creation.",
                Category = "Data Transfer",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "public class UserDto { public string Id { get; init; } public string Name { get; init; } }",
                    "public record UserDto(string Id, string Name); // Using C# 9 records"
                },
                Rationale = "Immutability prevents bugs from unexpected modifications, makes code thread-safe, and clearly communicates intent that DTOs are data containers.",
                Severity = "Medium",
                AppliesTo = new List<string> { "DTOs", "View Models" }
            },
            new Standard
            {
                Name = "Constants for Magic Values",
                Description = "Extract magic numbers and strings into named constants or enums. Use const for compile-time constants and static readonly for runtime constants.",
                Category = "Code Quality",
                IsFromExistingDocs = false,
                SourceFile = "Demo-Generated",
                Examples = new List<string>
                {
                    "private const int MaxRetries = 3;",
                    "private const string DefaultCulture = \"en-US\";",
                    "public enum OrderStatus { Pending, Processing, Completed, Cancelled }",
                    "// Avoid: if (status == 1) // What does 1 mean?"
                },
                Rationale = "Named constants improve code readability, make values easier to change, prevent typos, and provide better context for what values represent.",
                Severity = "Medium",
                AppliesTo = new List<string> { "All Classes", "Configuration" }
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
        public string? Rationale { get; set; }
        public string? Severity { get; set; }
        public List<string>? AppliesTo { get; set; }
    }
}
