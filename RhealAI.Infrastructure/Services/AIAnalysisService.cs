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

    public async Task<List<Refactoring>> DetectRefactoringOpportunitiesAsync(List<CodeFile> files, string? connectionId = null)
    {
        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            // Demo mode with detailed progress
            await SendProgress(connectionId, 90, "Demo Mode: Analyzing refactoring opportunities...");
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var fileName = Path.GetFileName(file.FilePath);
                var fileType = GetFileType(file.FilePath);
                await SendProgress(connectionId, 90 + (i * 5 / files.Count),
                    $"Checking {fileType} for refactoring: {fileName}");
                await Task.Delay(50); // Small delay to show progress
            }
            return await Task.FromResult(GenerateMockRefactorings(files));
        }

        var client = _agentFactory.CreateBugDetectionClient(); // Reuse bug detection client for refactoring
        var refactorings = new List<Refactoring>();

        // Analyze files in batches
        var batches = files.Chunk(10).ToList();
        var totalBatches = batches.Count;
        var currentBatch = 0;

        foreach (var batch in batches)
        {
            currentBatch++;

            for (int i = 0; i < batch.Length; i++)
            {
                var file = batch[i];
                var fileName = Path.GetFileName(file.FilePath);
                var fileType = GetFileType(file.FilePath);
                var overallProgress = 90 + ((currentBatch - 1) * 5 / totalBatches) + (i * 5 / (totalBatches * batch.Length));
                await SendProgress(connectionId, overallProgress,
                    $"AI checking {fileType} for refactoring: {fileName}");
            }

            var filesText = string.Join("\n\n", batch.Select(f =>
                $"File: {f.FilePath}\n```\n{f.Content}\n```"));

            var prompt = @$"Analyze the following code files for refactoring opportunities. 
Look for:
- Long methods (>50 lines) that need extraction
- Complex conditions that need simplification
- Duplicate code blocks
- Magic numbers that need constants
- Deep nesting (>3 levels)
- Large classes with multiple responsibilities
- Long parameter lists

For each refactoring opportunity, provide:
1. Exact file path and line number
2. Type of refactoring needed
3. Current problematic code snippet
4. Suggested improved code
5. Reason for refactoring
6. Benefits and improvement areas

Return results in JSON format as an array of objects with these fields:
filePath, lineNumber, refactoringType, title, description, currentCode, suggestedCode, reason, benefits, priority (Critical/High/Medium/Low), improvementAreas (array of strings like ['Readability', 'Maintainability']).

Files to analyze:
{filesText}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a code refactoring expert. Return refactoring suggestions in JSON format."),
                new UserChatMessage(prompt)
            };

            await SendProgress(connectionId, 90 + (currentBatch * 5 / totalBatches),
                $"AI analyzing batch {currentBatch}/{totalBatches} for refactorings...");

            var completion = await client.CompleteChatAsync(messages);
            var responseText = completion.Value.Content[0].Text;
            var batchRefactorings = ParseRefactoringsFromResponse(responseText);
            refactorings.AddRange(batchRefactorings);
        }

        return refactorings;
    }

    private List<Refactoring> ParseRefactoringsFromResponse(string response)
    {
        try
        {
            var jsonContent = ExtractJsonFromResponse(response);
            var dtos = JsonSerializer.Deserialize<List<RefactoringDto>>(jsonContent);

            return dtos?.Select(dto => new Refactoring
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = dto.FilePath ?? "",
                LineNumber = dto.LineNumber,
                EndLineNumber = dto.EndLineNumber > 0 ? dto.EndLineNumber : dto.LineNumber,
                RefactoringType = dto.RefactoringType ?? "General",
                Title = dto.Title ?? "Refactoring Opportunity",
                Description = dto.Description ?? "",
                CurrentCode = dto.CurrentCode ?? "",
                SuggestedCode = dto.SuggestedCode ?? "",
                Reason = dto.Reason ?? "",
                Benefits = dto.Benefits ?? "",
                Priority = ParseSeverity(dto.Priority),
                ImprovementAreas = dto.ImprovementAreas ?? new List<string>()
            }).ToList() ?? new List<Refactoring>();
        }
        catch
        {
            return new List<Refactoring>();
        }
    }

    private List<Refactoring> GenerateMockRefactorings(List<CodeFile> files)
    {
        var refactorings = new List<Refactoring>();
        var random = new Random(42); // Consistent seed for repeatable results

        foreach (var file in files)
        {
            var lines = file.Content.Split('\n');
            var linesArray = lines.ToArray();

            // Check for long methods
            var methodPattern = new[] { "function ", "func ", "def ", "public ", "private ", "protected ", "void ", "async " };
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (methodPattern.Any(p => line.Contains(p, StringComparison.OrdinalIgnoreCase)))
                {
                    // Count lines until method end (simple heuristic)
                    int methodLength = 0;
                    int openBraces = 0;
                    bool inMethod = false;

                    for (int j = i; j < Math.Min(i + 100, lines.Length); j++)
                    {
                        if (lines[j].Contains("{")) { openBraces++; inMethod = true; }
                        if (lines[j].Contains("}")) openBraces--;
                        if (inMethod) methodLength++;
                        if (inMethod && openBraces == 0) break;
                    }

                    if (methodLength > 50)
                    {
                        var (snippet, startLine, endLine) = GetCodeSnippetWithRange(linesArray, i + 1);
                        refactorings.Add(new Refactoring
                        {
                            Id = Guid.NewGuid().ToString(),
                            FilePath = file.FilePath,
                            LineNumber = startLine,
                            EndLineNumber = endLine,
                            RefactoringType = "Extract Method",
                            Title = "Long Method Detected",
                            Description = $"This method has approximately {methodLength} lines and should be broken down into smaller, more focused methods.",
                            CurrentCode = snippet,
                            SuggestedCode = "// Extract logical blocks into separate methods\n// Example: ExtractValidationLogic(), ExtractBusinessLogic(), ExtractDataAccess()",
                            Reason = "Long methods are difficult to understand, test, and maintain. They often violate the Single Responsibility Principle.",
                            Benefits = "Improved readability, easier testing, better maintainability, and clearer code organization.",
                            Priority = methodLength > 100 ? SeverityLevel.Critical : SeverityLevel.High,
                            ImprovementAreas = new List<string> { "Readability", "Maintainability", "Testability" }
                        });
                    }
                }
            }

            // Check for complex conditions (nested if statements)
            int nestingLevel = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("if ") || line.StartsWith("if("))
                {
                    nestingLevel++;
                    if (nestingLevel > 3)
                    {
                        var (snippet, startLine, endLine) = GetCodeSnippetWithRange(linesArray, i + 1);
                        refactorings.Add(new Refactoring
                        {
                            Id = Guid.NewGuid().ToString(),
                            FilePath = file.FilePath,
                            LineNumber = startLine,
                            EndLineNumber = endLine,
                            RefactoringType = "Simplify Conditional",
                            Title = "Deep Nesting Detected",
                            Description = $"This code has {nestingLevel} levels of nested conditions, making it hard to follow.",
                            CurrentCode = snippet,
                            SuggestedCode = "// Use guard clauses or early returns\n// Example: if (!condition) return;\n// Or extract into separate validation methods",
                            Reason = "Deeply nested conditions create cognitive load and increase the risk of logic errors.",
                            Benefits = "Flatter code structure, easier to understand control flow, reduced complexity.",
                            Priority = SeverityLevel.High,
                            ImprovementAreas = new List<string> { "Readability", "Maintainability" }
                        });
                    }
                }
                if (line.Contains("}")) nestingLevel = Math.Max(0, nestingLevel - 1);
            }

            // Check for magic numbers
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Look for hardcoded numbers (excluding 0, 1, -1, 100)
                var numbersPattern = System.Text.RegularExpressions.Regex.Matches(line, @"\b(\d{2,})\b");
                foreach (System.Text.RegularExpressions.Match match in numbersPattern)
                {
                    var number = match.Value;
                    if (number != "100" && number != "10" && !line.Contains("//") && !line.Contains("private") && !line.Contains("const"))
                    {
                        var (snippet, startLine, endLine) = GetCodeSnippetWithRange(linesArray, i + 1);
                        refactorings.Add(new Refactoring
                        {
                            Id = Guid.NewGuid().ToString(),
                            FilePath = file.FilePath,
                            LineNumber = startLine,
                            EndLineNumber = endLine,
                            RefactoringType = "Replace Magic Number",
                            Title = $"Magic Number: {number}",
                            Description = $"The hardcoded number '{number}' should be replaced with a named constant.",
                            CurrentCode = snippet,
                            SuggestedCode = $"private const int MAX_ITEMS = {number}; // Use descriptive name",
                            Reason = "Magic numbers reduce code readability and make it harder to maintain. Named constants provide context and make changes easier.",
                            Benefits = "Better code documentation, easier to update values, clearer intent.",
                            Priority = SeverityLevel.Medium,
                            ImprovementAreas = new List<string> { "Readability", "Maintainability" }
                        });
                        break; // Only one refactoring per line
                    }
                }
            }

            // Check for duplicate code patterns (simple string matching)
            var codeBlocks = new Dictionary<string, List<int>>();
            for (int i = 0; i < lines.Length - 3; i++)
            {
                var block = string.Join("\n", lines.Skip(i).Take(3)).Trim();
                if (block.Length > 50 && !block.Contains("//") && !block.StartsWith("using"))
                {
                    if (!codeBlocks.ContainsKey(block))
                        codeBlocks[block] = new List<int>();
                    codeBlocks[block].Add(i + 1);
                }
            }

            foreach (var duplicate in codeBlocks.Where(kvp => kvp.Value.Count > 1))
            {
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(linesArray, duplicate.Value.First());
                refactorings.Add(new Refactoring
                {
                    Id = Guid.NewGuid().ToString(),
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    RefactoringType = "Extract Method",
                    Title = "Duplicate Code Detected",
                    Description = $"This code block appears {duplicate.Value.Count} times in the file at lines: {string.Join(", ", duplicate.Value)}.",
                    CurrentCode = snippet,
                    SuggestedCode = "// Extract into a reusable method\nprivate void ExtractedMethod() {\n    // Common logic here\n}",
                    Reason = "Duplicate code increases maintenance burden. Changes must be made in multiple places, increasing the risk of bugs.",
                    Benefits = "Single source of truth, easier maintenance, reduced code size.",
                    Priority = duplicate.Value.Count > 2 ? SeverityLevel.High : SeverityLevel.Medium,
                    ImprovementAreas = new List<string> { "Maintainability", "DRY Principle" }
                });
                break; // Only report first duplicate
            }

            // Check for long parameter lists
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var paramCount = line.Count(c => c == ',');
                if ((line.Contains("function ") || line.Contains("public ") || line.Contains("private ")) &&
                    line.Contains("(") && paramCount > 4)
                {
                    var (snippet, startLine, endLine) = GetCodeSnippetWithRange(linesArray, i + 1);
                    refactorings.Add(new Refactoring
                    {
                        Id = Guid.NewGuid().ToString(),
                        FilePath = file.FilePath,
                        LineNumber = startLine,
                        EndLineNumber = endLine,
                        RefactoringType = "Introduce Parameter Object",
                        Title = "Long Parameter List",
                        Description = $"This method has {paramCount + 1} parameters, making it hard to use and understand.",
                        CurrentCode = snippet,
                        SuggestedCode = "// Create a parameter object class\nclass MethodParameters {\n    // Group related parameters\n}",
                        Reason = "Long parameter lists are hard to remember and often indicate that the method is doing too much.",
                        Benefits = "Clearer method signature, easier to add new parameters, better encapsulation.",
                        Priority = SeverityLevel.Medium,
                        ImprovementAreas = new List<string> { "Readability", "Maintainability", "API Design" }
                    });
                }
            }
        }

        return refactorings;
    }

    /// <summary>
    /// Detects code duplications across the project
    /// </summary>
    public async Task<List<CodeDuplication>> DetectCodeDuplicationsAsync(List<CodeFile> files, string? connectionId = null)
    {
        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            if (connectionId != null)
            {
                await SendProgress(connectionId, 95, "Demo Mode: Analyzing code for duplications...");
                await Task.Delay(1500);
            }
            return await Task.FromResult(GenerateMockDuplications(files));
        }

        var client = _agentFactory.CreateBugDetectionClient();
        var duplications = new List<CodeDuplication>();

        // Analyze all files together to find duplications
        var filesText = string.Join("\n\n", files.Select(f =>
            $"File: {f.FilePath}\n```\n{f.Content}\n```"));

        var prompt = @$"Analyze the following code files to detect duplicate or redundant code across the project.
Look for:
- Exact duplicate code blocks
- Similar methods with the same logic
- Repeated functionality across different files
- Duplicate utility functions or helpers
- Copy-pasted code with minor variations

For each duplication found, provide:
1. The duplicated code snippet
2. All locations where this code appears (file paths, start/end line numbers, method names, class names)
3. Type of duplication (ExactMatch, StructuralMatch, LogicalMatch, FunctionalMatch, PartialMatch)
4. Similarity percentage (0-100)
5. Description of the duplication
6. Suggestion for removing the duplication
7. Impact level (Critical/High/Medium/Low)
8. Refactoring options (e.g., 'Extract to utility method', 'Create base class', 'Use shared service')
9. Estimated effort to fix

Return results in JSON format as an array of objects with these fields:
duplicatedCode, locations (array with filePath, startLine, endLine, methodName, className), type, similarityPercentage, description, suggestion, impact, refactoringOptions (array of strings), estimatedEffort.

Files to analyze:
{filesText}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a code duplication detection expert. Return duplication analysis in JSON format."),
            new UserChatMessage(prompt)
        };

        await SendProgress(connectionId, 95, "AI analyzing entire project for code duplications...");

        var completion = await client.CompleteChatAsync(messages);
        var responseText = completion.Value.Content[0].Text;
        duplications = ParseDuplicationsFromResponse(responseText);

        await SendProgress(connectionId, 97, $"Duplication detection complete: Found {duplications.Count} duplications");

        return duplications;
    }

    private List<CodeDuplication> ParseDuplicationsFromResponse(string response)
    {
        try
        {
            var jsonContent = ExtractJsonFromResponse(response);
            var dtos = JsonSerializer.Deserialize<List<CodeDuplicationDto>>(jsonContent);

            return dtos?.Select(dto => new CodeDuplication
            {
                Id = Guid.NewGuid().ToString(),
                DuplicatedCode = dto.DuplicatedCode ?? "",
                Locations = dto.Locations?.Select(loc => new DuplicationLocation
                {
                    FilePath = loc.FilePath ?? "",
                    StartLine = loc.StartLine,
                    EndLine = loc.EndLine,
                    MethodName = loc.MethodName ?? "",
                    ClassName = loc.ClassName ?? ""
                }).ToList() ?? new List<DuplicationLocation>(),
                Type = ParseDuplicationType(dto.Type),
                LineCount = dto.DuplicatedCode?.Split('\n').Length ?? 0,
                SimilarityPercentage = dto.SimilarityPercentage,
                Description = dto.Description ?? "",
                Suggestion = dto.Suggestion ?? "",
                Impact = ParseSeverity(dto.Impact),
                RefactoringOptions = dto.RefactoringOptions ?? new List<string>(),
                EstimatedEffort = dto.EstimatedEffort ?? "Medium"
            }).ToList() ?? new List<CodeDuplication>();
        }
        catch
        {
            return new List<CodeDuplication>();
        }
    }

    private List<CodeDuplication> GenerateMockDuplications(List<CodeFile> files)
    {
        var duplications = new List<CodeDuplication>();
        var random = new Random(42);

        // Simulate finding duplications across files
        if (files.Count >= 2)
        {
            // Example 1: Exact duplicate validation logic
            var file1 = files[0];
            var file2 = files.Count > 1 ? files[1] : files[0];

            duplications.Add(new CodeDuplication
            {
                DuplicatedCode = @"if (input == null || input.trim() == '') {
    throw new Error('Input cannot be empty');
}",
                Locations = new List<DuplicationLocation>
                {
                    new() { FilePath = file1.FilePath, StartLine = 45, EndLine = 47, MethodName = "validateInput", ClassName = "UserService" },
                    new() { FilePath = file2.FilePath, StartLine = 123, EndLine = 125, MethodName = "checkData", ClassName = "DataValidator" }
                },
                Type = DuplicationType.ExactMatch,
                LineCount = 3,
                SimilarityPercentage = 100,
                Description = "Exact duplicate validation logic found in 2 files",
                Suggestion = "Extract this validation logic into a shared utility function to reduce code duplication and improve maintainability",
                Impact = SeverityLevel.High,
                RefactoringOptions = new List<string> { "Extract to utility method", "Create validation service", "Use decorator pattern" },
                EstimatedEffort = "Low"
            });

            // Example 2: Similar API call patterns
            if (files.Count >= 3)
            {
                var file3 = files[2];
                duplications.Add(new CodeDuplication
                {
                    DuplicatedCode = @"try {
    const response = await fetch(url);
    const data = await response.json();
    return data;
} catch (error) {
    console.error('API call failed:', error);
    throw error;
}",
                    Locations = new List<DuplicationLocation>
                    {
                        new() { FilePath = file1.FilePath, StartLine = 78, EndLine = 85, MethodName = "getUserData", ClassName = "UserService" },
                        new() { FilePath = file2.FilePath, StartLine = 156, EndLine = 163, MethodName = "getProducts", ClassName = "ProductService" },
                        new() { FilePath = file3.FilePath, StartLine = 92, EndLine = 99, MethodName = "fetchOrders", ClassName = "OrderService" }
                    },
                    Type = DuplicationType.StructuralMatch,
                    LineCount = 8,
                    SimilarityPercentage = 95,
                    Description = "Similar API call pattern repeated across 3 service files",
                    Suggestion = "Create a base HTTP service class with a generic fetch method that all services can extend or use",
                    Impact = SeverityLevel.Medium,
                    RefactoringOptions = new List<string> { "Create base service class", "Extract to HTTP utility", "Use interceptor pattern" },
                    EstimatedEffort = "Medium"
                });
            }

            // Example 3: Duplicate data transformation logic
            duplications.Add(new CodeDuplication
            {
                DuplicatedCode = @"const formatted = items.map(item => ({
    id: item.id,
    name: item.name,
    displayValue: `${item.name} (${item.id})`
}));",
                Locations = new List<DuplicationLocation>
                {
                    new() { FilePath = file1.FilePath, StartLine = 234, EndLine = 238, MethodName = "formatUsers", ClassName = "UserHelper" },
                    new() { FilePath = file2.FilePath, StartLine = 445, EndLine = 449, MethodName = "formatProducts", ClassName = "ProductHelper" }
                },
                Type = DuplicationType.LogicalMatch,
                LineCount = 5,
                SimilarityPercentage = 90,
                Description = "Duplicate data transformation logic with the same structure",
                Suggestion = "Create a generic formatting function that accepts the items array and field names as parameters",
                Impact = SeverityLevel.Low,
                RefactoringOptions = new List<string> { "Create generic formatter", "Use mapper utility", "Template method pattern" },
                EstimatedEffort = "Low"
            });
        }

        return duplications;
    }

    private DuplicationType ParseDuplicationType(string? type)
    {
        return type?.ToLower().Replace(" ", "") switch
        {
            "exactmatch" => DuplicationType.ExactMatch,
            "structuralmatch" => DuplicationType.StructuralMatch,
            "logicalmatch" => DuplicationType.LogicalMatch,
            "functionalmatch" => DuplicationType.FunctionalMatch,
            "partialmatch" => DuplicationType.PartialMatch,
            _ => DuplicationType.StructuralMatch
        };
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
                EndLineNumber = dto.EndLineNumber > 0 ? dto.EndLineNumber : dto.LineNumber,
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
                EndLineNumber = dto.EndLineNumber > 0 ? dto.EndLineNumber : dto.LineNumber,
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
        public int EndLineNumber { get; set; }
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
        public int EndLineNumber { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? RootCause { get; set; }
        public string? Impact { get; set; }
        public string? Severity { get; set; }
        public string? CodeSnippet { get; set; }
        public List<string>? ReproductionSteps { get; set; }
        public string? SuggestedFix { get; set; }
    }

    private class RefactoringDto
    {
        public string? FilePath { get; set; }
        public int LineNumber { get; set; }
        public int EndLineNumber { get; set; }
        public string? RefactoringType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CurrentCode { get; set; }
        public string? SuggestedCode { get; set; }
        public string? Reason { get; set; }
        public string? Benefits { get; set; }
        public string? Priority { get; set; }
        public List<string>? ImprovementAreas { get; set; }
    }

    private class CodeDuplicationDto
    {
        public string? DuplicatedCode { get; set; }
        public List<DuplicationLocationDto>? Locations { get; set; }
        public string? Type { get; set; }
        public double SimilarityPercentage { get; set; }
        public string? Description { get; set; }
        public string? Suggestion { get; set; }
        public string? Impact { get; set; }
        public List<string>? RefactoringOptions { get; set; }
        public string? EstimatedEffort { get; set; }
    }

    private class DuplicationLocationDto
    {
        public string? FilePath { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string? MethodName { get; set; }
        public string? ClassName { get; set; }
    }

    private List<Violation> GenerateMockViolations(List<CodeFile> files, List<Standard> standards)
    {
        var violations = new List<Violation>();
        var random = new Random(42); // Fixed seed for consistent results

        // Analyze each file for potential violations
        foreach (var file in files.Where(f => f.FileType != FileType.Markdown && f.Content.Length > 50))
        {
            var fileContent = file.Content.ToLower();
            var lines = file.Content.Split('\n');

            // Naming convention violations
            if (fileContent.Contains("_") && (fileContent.Contains("var ") || fileContent.Contains("public ") || fileContent.Contains("private ")))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                violations.Add(new Violation
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    RuleName = "Naming Convention Standard",
                    Description = "Variable or method naming does not follow camelCase/PascalCase convention",
                    Type = ViolationType.NamingConvention,
                    Severity = SeverityLevel.Medium,
                    CodeSnippet = snippet,
                    SuggestedFix = "Use camelCase for variables and PascalCase for classes/methods"
                });
            }

            // Missing error handling
            if ((fileContent.Contains("try") && !fileContent.Contains("catch")) ||
                (!fileContent.Contains("try") && fileContent.Contains("await") && fileContent.Contains("async")))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                violations.Add(new Violation
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    RuleName = "Error Handling Required",
                    Description = "Asynchronous operations should include proper error handling",
                    Type = ViolationType.ErrorHandling,
                    Severity = SeverityLevel.High,
                    CodeSnippet = snippet,
                    SuggestedFix = "Wrap async operations in try-catch blocks"
                });
            }

            // Missing documentation
            if ((fileContent.Contains("public class") || fileContent.Contains("public interface")) &&
                !fileContent.Contains("///") && !fileContent.Contains("/**"))
            {
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
                violations.Add(new Violation
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    RuleName = "Documentation Required",
                    Description = "Public classes and interfaces should have XML documentation comments",
                    Type = ViolationType.Documentation,
                    Severity = SeverityLevel.Low,
                    CodeSnippet = snippet,
                    SuggestedFix = "Add /// <summary> documentation comments"
                });
            }

            // Hardcoded strings/configuration
            if (fileContent.Contains("\"localhost\"") || fileContent.Contains("\"127.0.0.1\"") ||
                fileContent.Contains("\"password\"") || fileContent.Contains("connectionstring"))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                violations.Add(new Violation
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    RuleName = "Configuration Management",
                    Description = "Hardcoded configuration values should be moved to configuration files",
                    Type = ViolationType.Security,
                    Severity = SeverityLevel.Critical,
                    CodeSnippet = snippet,
                    SuggestedFix = "Use IConfiguration or environment variables"
                });
            }

            // Performance: String concatenation in loops
            if (fileContent.Contains("for") && fileContent.Contains("+=") && fileContent.Contains("string"))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                violations.Add(new Violation
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    RuleName = "Performance Best Practice",
                    Description = "String concatenation in loops causes performance issues",
                    Type = ViolationType.Performance,
                    Severity = SeverityLevel.Medium,
                    CodeSnippet = snippet,
                    SuggestedFix = "Use StringBuilder for string concatenation in loops"
                });
            }
        }

        // Add some general violations if none were found
        if (violations.Count < 3)
        {
            foreach (var file in files.Take(3))
            {
                violations.Add(new Violation
                {
                    FilePath = file.FilePath,
                    LineNumber = random.Next(1, Math.Max(2, file.LineCount)),
                    RuleName = standards.FirstOrDefault()?.Name ?? "Code Quality Standard",
                    Description = "Code could be improved to follow best practices",
                    Type = ViolationType.BestPractice,
                    Severity = SeverityLevel.Low,
                    CodeSnippet = "Review code for potential improvements",
                    SuggestedFix = "Follow established coding standards"
                });
            }
        }

        return violations;
    }

    private List<Bug> GenerateMockBugs(List<CodeFile> files)
    {
        var bugs = new List<Bug>();
        var random = new Random(42);

        // Analyze each file for potential bugs
        foreach (var file in files.Where(f => f.FileType != FileType.Markdown && f.Content.Length > 50))
        {
            var fileContent = file.Content.ToLower();
            var lines = file.Content.Split('\n');

            // Null reference potential
            if (fileContent.Contains(".") && !fileContent.Contains("?.") &&
                (fileContent.Contains("var ") || fileContent.Contains("return ")))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                bugs.Add(new Bug
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    Title = "Potential Null Reference Exception",
                    Description = "Object may be null when accessed without null checking",
                    RootCause = "Missing null check before accessing object property or method",
                    Impact = "Application may crash with NullReferenceException at runtime, causing service interruption",
                    Severity = SeverityLevel.High,
                    CodeSnippet = snippet,
                    ReproductionSteps = new List<string>
                    {
                        "Call the method with a null object",
                        "Access the property or method without null check",
                        "NullReferenceException is thrown"
                    },
                    SuggestedFix = "Use null-conditional operator (?.) or add explicit null checks"
                });
            }

            // Unhandled async exceptions
            if (fileContent.Contains("async") && fileContent.Contains("await") &&
                !fileContent.Contains("try") && !fileContent.Contains("catch"))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                bugs.Add(new Bug
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    Title = "Unhandled Async Exception",
                    Description = "Asynchronous operation lacks proper exception handling",
                    RootCause = "Async method does not handle potential exceptions from awaited operations",
                    Impact = "Unhandled exceptions can crash the application or leave it in an inconsistent state",
                    Severity = SeverityLevel.Critical,
                    CodeSnippet = snippet,
                    ReproductionSteps = new List<string>
                    {
                        "Trigger the async operation",
                        "Cause an exception in the awaited task",
                        "Exception propagates without handling"
                    },
                    SuggestedFix = "Wrap async operations in try-catch blocks or use global exception handlers"
                });
            }

            // Resource disposal issues
            if ((fileContent.Contains("new stream") || fileContent.Contains("new file") ||
                 fileContent.Contains("httpclient")) && !fileContent.Contains("using") && !fileContent.Contains("dispose"))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                bugs.Add(new Bug
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    Title = "Resource Leak - Missing Disposal",
                    Description = "Unmanaged resources are not properly disposed",
                    RootCause = "IDisposable objects created without using statement or explicit disposal",
                    Impact = "Memory leaks and resource exhaustion over time, degraded performance",
                    Severity = SeverityLevel.High,
                    CodeSnippet = snippet,
                    ReproductionSteps = new List<string>
                    {
                        "Create resource without using statement",
                        "Run application over extended period",
                        "Observe memory/resource leaks"
                    },
                    SuggestedFix = "Use 'using' statement or implement IDisposable pattern"
                });
            }

            // SQL Injection vulnerability
            if (fileContent.Contains("select") && fileContent.Contains("+") &&
                (fileContent.Contains("execute") || fileContent.Contains("query")))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                bugs.Add(new Bug
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    Title = "SQL Injection Vulnerability",
                    Description = "Dynamic SQL construction using string concatenation",
                    RootCause = "User input concatenated directly into SQL queries without parameterization",
                    Impact = "Critical security vulnerability - attackers can execute arbitrary SQL commands",
                    Severity = SeverityLevel.Critical,
                    CodeSnippet = snippet,
                    ReproductionSteps = new List<string>
                    {
                        "Input malicious SQL in user-provided data",
                        "SQL gets executed with injected code",
                        "Unauthorized data access or modification"
                    },
                    SuggestedFix = "Use parameterized queries or ORM (Entity Framework)"
                });
            }

            // Thread safety issues
            if (fileContent.Contains("static") && fileContent.Contains("list<") &&
                !fileContent.Contains("readonly") && !fileContent.Contains("concurrent"))
            {
                var lineNum = random.Next(1, Math.Max(2, file.LineCount));
                var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
                bugs.Add(new Bug
                {
                    FilePath = file.FilePath,
                    LineNumber = startLine,
                    EndLineNumber = endLine,
                    Title = "Thread Safety Issue - Shared Mutable State",
                    Description = "Static mutable collection accessed without synchronization",
                    RootCause = "Non-thread-safe collection used in multi-threaded context",
                    Impact = "Race conditions, data corruption, unpredictable behavior in concurrent scenarios",
                    Severity = SeverityLevel.High,
                    CodeSnippet = snippet,
                    ReproductionSteps = new List<string>
                    {
                        "Access static collection from multiple threads",
                        "Perform concurrent read/write operations",
                        "Experience data corruption or exceptions"
                    },
                    SuggestedFix = "Use ConcurrentDictionary/ConcurrentBag or add proper locking"
                });
            }
        }

        // Add some general bugs if none were found
        if (bugs.Count < 2)
        {
            foreach (var file in files.Take(2))
            {
                bugs.Add(new Bug
                {
                    FilePath = file.FilePath,
                    LineNumber = random.Next(1, Math.Max(2, file.LineCount)),
                    Title = "Code Quality Issue",
                    Description = "Potential code improvement opportunity detected",
                    RootCause = "Code pattern that could lead to issues",
                    Impact = "May cause issues under certain conditions",
                    Severity = SeverityLevel.Medium,
                    CodeSnippet = "Review code implementation",
                    ReproductionSteps = new List<string> { "Review code logic", "Test edge cases" },
                    SuggestedFix = "Review and refactor as needed"
                });
            }
        }

        return bugs;
    }

    /// <summary>
    /// Extracts code snippet with intelligent context detection
    /// Returns tuple of (snippet, startLine, endLine)
    /// </summary>
    private (string snippet, int startLine, int endLine) GetCodeSnippetWithRange(string[] lines, int lineNumber)
    {
        if (lines.Length == 0) return ("// No code available", lineNumber, lineNumber);

        var index = Math.Max(0, Math.Min(lineNumber - 1, lines.Length - 1));
        var currentLine = lines[index].Trim();

        // Check if we're on a single-line change (simple statement)
        if (IsSingleLineChange(currentLine))
        {
            return (currentLine, lineNumber, lineNumber);
        }

        // Extract the entire method or code block
        var (startIndex, endIndex) = FindCodeBlock(lines, index);
        var snippetLines = new List<string>();

        for (int i = startIndex; i <= endIndex && i < lines.Length; i++)
        {
            snippetLines.Add(lines[i]);
        }

        return (string.Join("\n", snippetLines), startIndex + 1, endIndex + 1);
    }

    private bool IsSingleLineChange(string line)
    {
        // Single line statements/declarations
        var singleLinePatterns = new[]
        {
            "var ", "const ", "let ", "return ",
            "throw ", "break;", "continue;",
            "import ", "using ", "};", ");"
        };

        return singleLinePatterns.Any(pattern =>
            line.Contains(pattern, StringComparison.OrdinalIgnoreCase)) &&
            line.Length < 150 &&
            !line.Contains("{") &&
            !line.Contains("(") ||
            line.EndsWith(";");
    }

    private (int startIndex, int endIndex) FindCodeBlock(string[] lines, int currentIndex)
    {
        int startIndex = currentIndex;
        int endIndex = currentIndex;
        int braceCount = 0;
        bool inMethod = false;

        // Look backwards to find method or block start
        for (int i = currentIndex; i >= 0; i--)
        {
            var line = lines[i].Trim();

            // Method declaration patterns
            if (line.Contains("public ") || line.Contains("private ") ||
                line.Contains("protected ") || line.Contains("async ") ||
                line.Contains("function ") || line.Contains("def "))
            {
                startIndex = i;
                inMethod = true;
                break;
            }

            // Opening brace
            if (line.Contains("{"))
            {
                startIndex = i;
                break;
            }

            // Stop at previous closing brace
            if (line.Contains("}"))
            {
                startIndex = i + 1;
                break;
            }

            // Don't go too far back (max 50 lines)
            if (currentIndex - i > 50)
            {
                startIndex = Math.Max(0, currentIndex - 5);
                break;
            }
        }

        // Look forward to find method or block end
        for (int i = currentIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.Contains("{")) braceCount++;
            if (line.Contains("}")) braceCount--;

            // Found matching closing brace
            if (inMethod && braceCount == 0 && line.Contains("}"))
            {
                endIndex = i;
                break;
            }

            // Stop at next method declaration
            if (i > currentIndex && (line.Contains("public ") || line.Contains("private ")))
            {
                endIndex = i - 1;
                break;
            }

            // Don't go too far forward (max 50 lines)
            if (i - currentIndex > 50)
            {
                endIndex = Math.Min(lines.Length - 1, currentIndex + 5);
                break;
            }
        }

        // Ensure we have at least a few lines of context
        if (!inMethod && endIndex - startIndex < 3)
        {
            startIndex = Math.Max(0, currentIndex - 2);
            endIndex = Math.Min(lines.Length - 1, currentIndex + 2);
        }

        return (startIndex, endIndex);
    }

    private string GetCodeSnippet(string[] lines, int lineNumber)
    {
        var (snippet, _, _) = GetCodeSnippetWithRange(lines, lineNumber);
        return snippet;
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
            { "scheduler", "Job Scheduling" },
            { "violation", "Standards Compliance Checking" },
            { "bug", "Bug Detection" },
            { "ai", "AI/ML Processing" },
            { "openai", "AI Integration" },
            { "documentation", "Documentation Management" },
            { "standard", "Standards Management" },
            { "file", "File Processing" },
            { "upload", "File Upload Handling" },
            { "git", "Version Control Integration" }
        };

        var detectedLogic = new List<string>();

        foreach (var kvp in businessKeywords)
        {
            var hasFolder = folderStructure.Keys.Any(f => f.ToLowerInvariant().Contains(kvp.Key));
            var hasFiles = files.Any(f => f.FilePath.ToLowerInvariant().Contains(kvp.Key));
            var hasCodeReference = files.Any(f => f.Content.ToLowerInvariant().Contains(kvp.Key));

            if (hasFolder || hasFiles || hasCodeReference)
                detectedLogic.Add(kvp.Value);
        }

        // Add detailed business logic by analyzing actual code content
        var codeAnalysis = new List<string>();

        foreach (var file in files.Where(f => f.FileType != FileType.Markdown).Take(50))
        {
            var content = file.Content.ToLowerInvariant();

            // Detect API endpoints and their purposes
            if (content.Contains("[httppost]") || content.Contains("[httpget]"))
            {
                if (content.Contains("analyze") || content.Contains("analysis"))
                    codeAnalysis.Add("Code Analysis Operations");
                if (content.Contains("report"))
                    codeAnalysis.Add("Report Generation");
                if (content.Contains("upload") || content.Contains("repository"))
                    codeAnalysis.Add("File/Repository Upload Management");
            }

            // Detect data processing logic
            if (content.Contains("async") && content.Contains("await"))
            {
                if (content.Contains("detect") || content.Contains("find"))
                    codeAnalysis.Add("Pattern Detection & Analysis");
            }

            // Detect external service integrations
            if (content.Contains("httpclient") || content.Contains("api"))
                codeAnalysis.Add("External API Integration");
        }

        detectedLogic.AddRange(codeAnalysis.Distinct());

        return detectedLogic.Any()
            ? string.Join(", ", detectedLogic.Distinct().Take(10))
            : "General Purpose Application with Custom Business Logic";
    }

    private string GenerateProjectDescription(string techStack, string architecture, string businessLogic)
    {
        return $"This is a {architecture}-based application built using {techStack}. " +
               $"The system implements comprehensive functionality including: {businessLogic}. " +
               $"The project follows modern development practices with a focus on maintainability, scalability, and code quality. " +
               $"It includes automated analysis capabilities, real-time processing, and extensive file handling features.";
    }

    private string GenerateCoreFunctionality(List<CodeFile> files, Dictionary<string, int> folderStructure)
    {
        var functionalities = new List<string>();

        // Analyze actual code content for functionality
        var codeContent = string.Join(" ", files.Take(100).Select(f => f.Content.ToLowerInvariant()));

        // Check for API/Web functionality
        if (codeContent.Contains("[apicontroller]") || codeContent.Contains("@restcontroller") ||
            codeContent.Contains("app.get(") || codeContent.Contains("router."))
            functionalities.Add("RESTful API Services with multiple endpoints");

        // Check for database operations
        if (codeContent.Contains("dbcontext") || codeContent.Contains("@entity") ||
            codeContent.Contains("select ") || codeContent.Contains("insert "))
            functionalities.Add("Database Operations & Data Persistence");

        // Check for UI
        if (files.Any(f => f.FilePath.EndsWith(".html") || f.FilePath.EndsWith(".jsx") ||
                          f.FilePath.EndsWith(".tsx") || f.FilePath.EndsWith(".vue") ||
                          f.FilePath.EndsWith(".cshtml")))
            functionalities.Add("Interactive User Interface with real-time updates");

        // Check for authentication
        if (codeContent.Contains("authentication") || codeContent.Contains("jwt") ||
            codeContent.Contains("login") || codeContent.Contains("authorize"))
            functionalities.Add("User Authentication & Authorization");

        // Check for external integrations
        if (codeContent.Contains("httpclient") || codeContent.Contains("axios") || codeContent.Contains("fetch("))
            functionalities.Add("External API Integration & Data Synchronization");

        // Check for file processing
        if (codeContent.Contains("filestream") || codeContent.Contains("zipfile") ||
            codeContent.Contains("upload") || codeContent.Contains("iformfile"))
            functionalities.Add("File Upload, Processing & Storage");

        // Check for async/background processing
        if (codeContent.Contains("async task") || codeContent.Contains("background"))
            functionalities.Add("Asynchronous Background Processing");

        // Check for real-time communication
        if (codeContent.Contains("signalr") || codeContent.Contains("websocket"))
            functionalities.Add("Real-Time Communication & Progress Tracking");

        // Check for AI/ML features
        if (codeContent.Contains("openai") || codeContent.Contains("ai") ||
            codeContent.Contains("chatgpt") || codeContent.Contains("machinelearning"))
            functionalities.Add("AI-Powered Code Analysis & Recommendations");

        // Check for reporting
        if (codeContent.Contains("report") || codeContent.Contains("export") || codeContent.Contains("pdf"))
            functionalities.Add("Report Generation & Export");

        // Check for version control
        if (codeContent.Contains("libgit2") || codeContent.Contains("gitrepository") ||
            files.Any(f => f.FilePath.Contains("git", StringComparison.OrdinalIgnoreCase)))
            functionalities.Add("Git Repository Integration & Cloning");

        return functionalities.Any()
            ? string.Join("; ", functionalities)
            : "Core application logic, data processing, and business workflow management";
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

