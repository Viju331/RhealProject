using System.Text;
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
    
    // Store project context across all analysis steps
    private ProjectContext? _projectContext;

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

            var filesText = BuildComprehensiveAnalysisContext(batch, "batch_" + currentBatch);

            var prompt = string.Format(ViolationDetectionPrompts.DetectViolations, standardsText, filesText);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are a coding standards enforcer. Return violations in JSON format.

CRITICAL RULE: ALWAYS check the 'Language:' field in the file metadata BEFORE suggesting ANY violation!

SQL FILES - FORBIDDEN SUGGESTIONS:
- NEVER suggest: async/await, try-catch, ?. operator, ?? operator, private const, using statement, Entity Framework
- ONLY suggest: BEGIN TRY...BEGIN CATCH, ISNULL(), COALESCE(), DECLARE @variable, SET NOCOUNT ON

C# FILES - FORBIDDEN SUGGESTIONS:
- NEVER suggest: SQL syntax (DECLARE, BEGIN/END, GO, ISNULL, SET NOCOUNT)
- ONLY suggest: private const, ?. operator, try-catch, async/await

IF YOU VIOLATE THESE RULES, YOUR SUGGESTION WILL BE REJECTED."),
                new UserChatMessage(prompt)
            };

            await SendProgress(connectionId, 42 + (currentBatch * 25 / totalBatches),
                $"Processing batch {currentBatch}/{totalBatches} with AI model...");

            var completion = await client.CompleteChatAsync(messages);
            var responseText = completion.Value.Content[0].Text;
            var batchViolations = ParseViolationsFromResponse(responseText);

            // CRITICAL: Filter out language-inappropriate suggestions
            var validatedViolations = batchViolations.Where(v => IsValidLanguageSuggestion(v.FilePath, v.SuggestedFix, v.Description)).ToList();
            violations.AddRange(validatedViolations);
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

        // Group files by folder for contextual bug analysis
        var filesByFolder = files.GroupBy(f => Path.GetDirectoryName(f.FilePath) ?? "root")
                                 .OrderBy(g => g.Key)
                                 .ToList();

        var totalFolders = filesByFolder.Count;
        var currentFolder = 0;

        foreach (var folderGroup in filesByFolder)
        {
            currentFolder++;
            var folderName = Path.GetFileName(folderGroup.Key) ?? "root";
            var folderFiles = folderGroup.ToList();

            await SendProgress(connectionId, 72 + (currentFolder * 15 / totalFolders),
                $"Scanning for bugs in {folderName} ({folderFiles.Count} files)");

            // Process files in smaller batches for thorough analysis
            var batches = folderFiles.Chunk(5).ToList();
            var batchNum = 0;

            foreach (var batch in batches)
            {
                batchNum++;

                // Build comprehensive context including method signatures and line numbers
                var filesText = BuildComprehensiveAnalysisContext(batch, folderGroup.Key);
                var prompt = string.Format(BugDetectionPrompts.DetectBugs, filesText);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(@"You are a bug detection expert. Return bugs in JSON format.

CRITICAL: ALWAYS check 'Language:' field FIRST before suggesting ANY bug fix!

SQL FILES - FORBIDDEN SUGGESTIONS:
- NEVER: 'Use parameterized queries' (if @parameters already exist)
- NEVER: async/await, try-catch, ?., ??, private const, using, Entity Framework
- ONLY: BEGIN TRY...BEGIN CATCH, ISNULL(), COALESCE(), WHERE IS NOT NULL

C# FILES - FORBIDDEN:
- NEVER: SQL syntax (DECLARE, BEGIN/END, GO, ISNULL)
- ONLY: try-catch, ?., ??, async/await, using, private const

VALIDATION: If Language='SQL' and fix contains 'async' or '?.' or 'Entity Framework' → REJECT!"),
                    new UserChatMessage(prompt)
                };

                await SendProgress(connectionId, 72 + (currentFolder * 15 / totalFolders),
                    $"AI analyzing {folderName} batch {batchNum} for bugs...");

                var completion = await client.CompleteChatAsync(messages);
                var responseText = completion.Value.Content[0].Text;
                var batchBugs = ParseBugsFromResponse(responseText);

                // CRITICAL: Filter out language-inappropriate suggestions
                var validatedBugs = batchBugs.Where(b => IsValidLanguageSuggestion(b.FilePath, b.SuggestedFix, b.Description)).ToList();
                bugs.AddRange(validatedBugs);
            }
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

        // Group files by folder for contextual refactoring analysis
        var filesByFolder = files.GroupBy(f => Path.GetDirectoryName(f.FilePath) ?? "root")
                                 .OrderBy(g => g.Key)
                                 .ToList();

        var totalFolders = filesByFolder.Count;
        var currentFolder = 0;

        foreach (var folderGroup in filesByFolder)
        {
            currentFolder++;
            var folderName = Path.GetFileName(folderGroup.Key) ?? "root";
            var folderFiles = folderGroup.ToList();

            await SendProgress(connectionId, 90 + (currentFolder * 5 / totalFolders),
                $"Analyzing refactoring opportunities in {folderName} ({folderFiles.Count} files)");

            // Process files in smaller batches for detailed analysis
            var batches = folderFiles.Chunk(5).ToList();
            var batchNum = 0;

            foreach (var batch in batches)
            {
                batchNum++;

                // Build comprehensive context with method complexities and dependencies
                var filesText = BuildComprehensiveAnalysisContext(batch, folderGroup.Key);

                var prompt = @$"You are an expert code refactoring consultant specializing in clean code principles, design patterns, and software architecture.

CRITICAL - LANGUAGE-SPECIFIC REFACTORING RULES:
Before suggesting ANY refactoring, identify the programming language from the file metadata.
Provide language-appropriate suggestions:

- SQL (.sql files):
  * Use SQL-specific improvements: DECLARE @constants, table variables, CTEs, stored procedure optimizations
  * Suggest SQL patterns: SET NOCOUNT ON, proper transaction handling, indexed views
  * DO NOT suggest: private const, ?. operator, async/await, LINQ, classes/interfaces
  * Magic numbers: Suggest DECLARE @ConstantName INT = value at procedure start
  * Null handling: Suggest ISNULL(), COALESCE(), proper LEFT JOIN patterns

- C# (.cs files):
  * Use C# patterns: private const, null-conditional operators (?., ??), async/await, LINQ
  * Suggest: Extract Method, Dependency Injection, interfaces, design patterns
  * DO NOT suggest: SQL-specific syntax like DECLARE, BEGIN/END blocks, GO statements

- TypeScript/JavaScript (.ts, .js files):
  * Use TS/JS patterns: const/let, arrow functions, optional chaining (?.), async/await, Promise
  * Suggest: Extract function, modules, TypeScript types/interfaces
  * DO NOT suggest: C# or SQL syntax

- Python (.py files):
  * Use Python patterns: constants in UPPER_CASE, list comprehensions, context managers
  * DO NOT suggest: C# or SQL syntax

ANALYSIS REQUIREMENTS:
1. Respect the language of each file - SQL suggestions for SQL, C# for C#, etc.
2. Ensure suggestedCode uses correct syntax for the file's language
3. Validate that code improvements are valid in that language

Analyze the following code files comprehensively for refactoring opportunities.

REFACTORING CATEGORIES TO ANALYZE:

1. Method Complexity:
   - Long methods (>50 lines) needs Extract Method
   - Complex methods (cyclomatic complexity >10) needs Simplify and Extract
   - Methods doing multiple things needs Single Responsibility

2. Code Duplication:
   - Identical or similar code blocks needs Extract Method/Class
   - Similar algorithms with slight variations needs Template Method/Strategy Pattern
   - Repeated patterns needs Introduce abstraction

3. Conditional Complexity:
   - Deep nesting (>3 levels) needs Guard Clauses, Early Returns
   - Complex boolean expressions needs Extract Predicate Methods
   - Long if-else chains needs Polymorphism, Strategy Pattern, Dictionary Lookup

4. Data and State Management:
   - Magic numbers/strings needs Named Constants
   - Data clumps (same group of parameters) needs Introduce Parameter Object
   - Feature envy (class accessing other class data) needs Move Method
   - Large classes (>500 lines) needs Extract Class

5. Parameter and Interface Issues:
   - Long parameter lists (>3-4 params) needs Parameter Object
   - Boolean flags needs Strategy Pattern or Method Splitting
   - Out/ref parameters needs Return objects

6. Design Patterns and Architecture:
   - Missing abstractions needs Introduce Interface
   - Tight coupling needs Dependency Injection
   - God classes needs Extract Responsibilities
   - Primitive obsession needs Value Objects

7. Modern Language Features:
   - Verbose null checks needs Null coalescing operators (??, ??=)
   - Traditional loops needs LINQ, collection expressions
   - Manual resource management needs using statements
   - Callback hell needs async/await

ANALYSIS DEPTH:
- FOLDER STRUCTURE: Understand the purpose of each folder and its files
- FILE-BY-FILE: Examine every file completely, analyzing its role and quality
- METHOD-BY-METHOD: Review EVERY method/function listed - use the method metadata showing line counts and complexities
- LINE-BY-LINE: Use the line-numbered code to identify exact refactoring locations
- Examine EVERY method, class, and file thoroughly
- Look for both obvious and subtle improvements
- Consider readability, maintainability, testability, and performance
- Find 25-40+ refactoring opportunities per batch for comprehensive analysis

For each refactoring opportunity, provide:
- File Path: Exact file location from the provided context
- Line Number: EXACT start line from the line-numbered code (e.g., if you see '  89: public void Method()', use line 89)
- End Line Number: End line of the code needing refactoring
- Refactoring Type: Specific type such as Extract Method, Simplify Conditional, Introduce Constant
- Title: Clear summary of what needs to be done
- Description: Detailed explanation of the problem and why it should be refactored
- Current Code: The problematic code snippet (copy from line-numbered code provided)
- Suggested Code: Complete refactored code showing the improvement in correct language syntax
- Reason: Why this refactoring improves the codebase
- Benefits: Specific improvements like 'Reduces method complexity from 45 to 15 lines', 'Improves testability'
- Priority: 
  * Critical: Major architecture issues, severe maintainability problems
  * High: Complex methods (>50 lines or >10 cyclomatic complexity), significant code duplication, design pattern violations
  * Medium: Minor duplication, moderate complexity, readability improvements
  * Low: Style improvements, minor simplifications
- Improvement Areas: Array of categories (Readability, Maintainability, Testability, Performance, Security)

IMPORTANT:
- Use EXACT line numbers from the line-numbered code provided
- Copy code snippets exactly as shown in the provided content
- Consider method line counts shown in metadata (methods >50 lines are High priority for Extract Method)

LANGUAGE-SPECIFIC REFACTORING EXAMPLES:

WHAT IS A MAGIC NUMBER?
A ""magic number"" is a hardcoded numeric value whose meaning is unclear.
Example: WHERE Status = 50 - What does 50 mean? Active? Inactive? Pending?
Solution: Give it a descriptive name so anyone reading the code understands its purpose.

SQL MAGIC NUMBER - BEFORE & AFTER:

CURRENT (Unclear):
```sql
WHERE Status = 50
  AND Type = 25
  AND [Name] [nvarchar](50) NULL  -- Why 50?
```
Problem: What do 50 and 25 mean? No one knows without checking documentation.

SUGGESTED (Clear & Maintainable):
```sql
-- Declare constants at procedure start for clarity
DECLARE @STATUS_ACTIVE INT = 50;
DECLARE @TYPE_STANDARD INT = 25;
DECLARE @MAX_NAME_LENGTH INT = 50;

WHERE Status = @STATUS_ACTIVE
  AND Type = @TYPE_STANDARD
  AND [Name] [nvarchar](@MAX_NAME_LENGTH) NULL
```
Benefits: 
- Clear meaning: @STATUS_ACTIVE explains what 50 represents
- Easy to change: Update value once at top, used everywhere
- Better maintenance: New developers understand code immediately

C# MAGIC NUMBER - BEFORE & AFTER:

CURRENT:
```csharp
if (status == 50) {{ ... }}
if (items.Count > 25) {{ return; }}
```

SUGGESTED:
```csharp
private const int STATUS_ACTIVE = 50;
private const int MAX_ITEMS = 25; // Maximum items to process in one batch

if (status == STATUS_ACTIVE) {{ ... }}
if (items.Count > MAX_ITEMS) {{ return; }}
```

SQL ERROR HANDLING:
WRONG: ""Wrap in try-catch blocks""
RIGHT: ""Use BEGIN TRY...BEGIN CATCH""

SQL NULL CHECKS:
WRONG: ""Use ?. operator""
RIGHT: ""Use ISNULL(column, default) or COALESCE()""

C# NULL CHECKS:
RIGHT: ""Use ?. operator""
WRONG: ""Use ISNULL()"" (that's SQL!)

Return results in JSON format as an array of objects with these fields:
filePath, lineNumber, endLineNumber, refactoringType, title, description, currentCode, suggestedCode, reason, benefits, priority, improvementAreas

Files to analyze:
{filesText}

Find AT LEAST 25-40 refactoring opportunities. ENSURE suggestedCode matches the file's language syntax!";

                var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are a code refactoring expert. Return refactoring suggestions in JSON format.

CRITICAL: CHECK 'Language:' FIELD FIRST! Match suggestions to language!

SQL MAGIC NUMBERS:
- RIGHT: DECLARE @CONSTANT_NAME INT = value;
- WRONG: private const int (that's C#!)

SQL ERROR HANDLING:
- RIGHT: BEGIN TRY...BEGIN CATCH
- WRONG: try-catch (that's C#/JavaScript!)

SQL NULL CHECKS:
- RIGHT: ISNULL(), COALESCE(), IS NOT NULL
- WRONG: ?. operator (that's C#/JavaScript!)

C# REFACTORING:
- RIGHT: private const int, try-catch, ?., ??
- WRONG: DECLARE, BEGIN/END, ISNULL (that's SQL!)

BEFORE suggesting, verify syntax matches the Language field!"),
                new UserChatMessage(prompt)
            };

                await SendProgress(connectionId, 90 + (currentFolder * 5 / totalFolders),
                    $"AI analyzing {folderName} batch {batchNum} for refactorings...");

                var completion = await client.CompleteChatAsync(messages);
                var responseText = completion.Value.Content[0].Text;
                var batchRefactorings = ParseRefactoringsFromResponse(responseText);

                // CRITICAL: Filter out language-inappropriate suggestions
                var validatedRefactorings = batchRefactorings.Where(r => IsValidLanguageSuggestion(r.FilePath, r.SuggestedCode, r.Description)).ToList();
                refactorings.AddRange(validatedRefactorings);
            }
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

        // Group files by language/extension to prevent cross-language false positives
        var filesByLanguage = files.GroupBy(f => Path.GetExtension(f.FilePath).ToLowerInvariant());

        int languageCount = 0;
        int totalLanguages = filesByLanguage.Count();

        foreach (var languageGroup in filesByLanguage)
        {
            languageCount++;
            var extension = languageGroup.Key;
            var languageFiles = languageGroup.ToList();
            var languageName = GetLanguageName(extension);

            // Skip if less than 2 files (can't have duplication with 1 file)
            if (languageFiles.Count < 2)
                continue;

            var progressBase = 95 + (languageCount - 1) * 2 / totalLanguages;
            await SendProgress(connectionId, progressBase, $"Analyzing {languageName} files for duplications...");

            // Analyze files of the same language together
            var filesText = string.Join("\n\n", languageFiles.Select(f =>
            {
                var lang = GetLanguageIdentifier(Path.GetExtension(f.FilePath).ToLowerInvariant());
                return $"File: {f.FilePath}\nLanguage: {languageName}\n```{lang}\n{f.Content}\n```";
            }));

            var prompt = @$"Analyze the following {languageName} code files to detect duplicate or redundant code.

CRITICAL RULES:
1. ONLY compare code within these {languageName} files - DO NOT mix languages
2. Ensure the duplicated code snippet matches the actual language ({languageName})
3. All file paths in locations must be from the files provided below
4. The 'duplicatedCode' field MUST contain actual {languageName} code from these files
5. Verify syntax: SQL files must show SQL code (BEGIN/END, DECLARE), C# files must show C# code (try/catch, var), TypeScript files must show TS code (const/let, async/await)

LANGUAGE-SPECIFIC DUPLICATION EXAMPLES FOR {languageName}:

For SQL files, duplicatedCode must show SQL syntax like this:
  BEGIN TRY
    BEGIN TRANSACTION
    INSERT INTO ProcessedData...
    COMMIT TRANSACTION
  END TRY
  BEGIN CATCH
    ROLLBACK TRANSACTION
    THROW
  END CATCH

For C# files, duplicatedCode must show C# syntax like this:
  try {{
    var result = await operation();
    return ProcessResult(result);
  }} catch (Exception ex) {{
    logger.LogError(ex, ""Operation failed"");
    throw;
  }}

For TypeScript/JavaScript files, duplicatedCode must show TypeScript/JS syntax like this:
  try {{
    const result = await operation();
    return processResult(result);
  }} catch (error) {{
    logger.error('Operation failed', error);
    throw error;
  }}

Look for:
- Exact duplicate code blocks (100% match)
- Similar methods/functions with the same logic (80-99% match)
- Repeated functionality across different files
- Duplicate utility functions, helpers, or stored procedures
- Copy-pasted code with minor variations (60-79% match)

For each duplication found, provide:
1. The actual duplicated code snippet from these files (must be valid {languageName} syntax)
2. All locations where this code appears (file paths, line numbers, method/procedure names, class names)
3. Type: ExactMatch (100%), StructuralMatch (90-99%), LogicalMatch (80-89%), FunctionalMatch (70-79%), PartialMatch (60-69%)
4. Similarity percentage (60-100)
5. Clear description explaining what is duplicated
6. Concrete suggestion for removing duplication
7. Impact: Critical (>100 lines), High (50-100 lines), Medium (20-49 lines), Low (<20 lines)
8. Refactoring options specific to {languageName}
9. Estimated effort: Low/Medium/High

Return results in JSON format as an array with these fields:
duplicatedCode, locations (array with filePath, startLine, endLine, methodName, className), type, similarityPercentage, description, suggestion, impact, refactoringOptions (array), estimatedEffort.

{languageName} Files to analyze:
{filesText}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage($"You are a code duplication detection expert specializing in {languageName}. Return analysis in JSON format. NEVER mix code from different languages."),
                new UserChatMessage(prompt)
            };

            var completion = await client.CompleteChatAsync(messages);
            var responseText = completion.Value.Content[0].Text;
            var languageDuplications = ParseDuplicationsFromResponse(responseText);
            duplications.AddRange(languageDuplications);

            await SendProgress(connectionId, progressBase + 1, $"Found {languageDuplications.Count} duplications in {languageName} files");
        }

        await SendProgress(connectionId, 97, $"Duplication detection complete: Found {duplications.Count} total duplications");

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

        if (files.Count < 2) return duplications;

        // Generate comprehensive duplications across different categories

        // 1. Exact Matches - Duplicate validation logic
        for (int i = 0; i < Math.Min(files.Count - 1, 15); i++)
        {
            var file1 = files[i];
            var file2 = files[i + 1];

            var validationTypes = new[] {
                ("Email validation", "validateEmail", "if (!email.match(/^[^@]+@[^@]+$/)) throw new Error('Invalid email');"),
                ("Null check", "checkNull", "if (value == null || value === undefined) return false;"),
                ("Empty string check", "checkEmpty", "if (str == null || str.trim() === '') throw new Error('Empty string');"),
                ("Number validation", "validateNumber", "if (isNaN(num) || num < 0) throw new Error('Invalid number');"),
                ("Array validation", "validateArray", "if (!Array.isArray(arr) || arr.length === 0) return false;")
            };

            if (i < validationTypes.Length)
            {
                var validation = validationTypes[i];
                duplications.Add(new CodeDuplication
                {
                    DuplicatedCode = validation.Item3,
                    Locations = new List<DuplicationLocation>
                    {
                        new() { FilePath = file1.FilePath, StartLine = 45 + i * 10, EndLine = 47 + i * 10, MethodName = validation.Item2, ClassName = ExtractClassName(file1.FilePath) },
                        new() { FilePath = file2.FilePath, StartLine = 123 + i * 8, EndLine = 125 + i * 8, MethodName = validation.Item2 + "Data", ClassName = ExtractClassName(file2.FilePath) }
                    },
                    Type = DuplicationType.ExactMatch,
                    LineCount = 3,
                    SimilarityPercentage = 100,
                    Description = $"Exact duplicate {validation.Item1} found in 2 files",
                    Suggestion = $"Extract {validation.Item1} into a shared validation utility to eliminate duplication",
                    Impact = random.Next(0, 100) < 40 ? SeverityLevel.High : SeverityLevel.Medium,
                    RefactoringOptions = new List<string> { "Extract to utility method", "Create validation service", "Use validator class" },
                    EstimatedEffort = "Low"
                });
            }
        }

        // 2. Structural Matches - Similar patterns across services
        for (int i = 0; i < Math.Min(files.Count - 2, 12); i += 3)
        {
            if (i + 2 >= files.Count) break;

            var patterns = new[] {
                ("API call pattern", "HTTP request with error handling", "Create base HTTP service class"),
                ("Database query pattern", "Repository pattern with error handling", "Create base repository class"),
                ("Event handler pattern", "Event subscription and cleanup", "Use event manager service"),
                ("Logging pattern", "Structured logging with context", "Create logging service wrapper")
            };

            var patternIndex = i / 3;
            if (patternIndex < patterns.Length)
            {
                var pattern = patterns[patternIndex];
                duplications.Add(new CodeDuplication
                {
                    DuplicatedCode = @"try {
    const result = await operation();
    return processResult(result);
} catch (error) {
    logger.error('Operation failed', error);
    throw error;
}",
                    Locations = new List<DuplicationLocation>
                    {
                        new() { FilePath = files[i].FilePath, StartLine = 78 + i * 5, EndLine = 85 + i * 5, MethodName = "executeOperation" + i, ClassName = ExtractClassName(files[i].FilePath) },
                        new() { FilePath = files[i + 1].FilePath, StartLine = 156 + i * 3, EndLine = 163 + i * 3, MethodName = "performAction" + i, ClassName = ExtractClassName(files[i + 1].FilePath) },
                        new() { FilePath = files[i + 2].FilePath, StartLine = 92 + i * 4, EndLine = 99 + i * 4, MethodName = "runTask" + i, ClassName = ExtractClassName(files[i + 2].FilePath) }
                    },
                    Type = DuplicationType.StructuralMatch,
                    LineCount = 8,
                    SimilarityPercentage = 95 - random.Next(0, 10),
                    Description = $"{pattern.Item1}: {pattern.Item2} repeated across {3} files",
                    Suggestion = pattern.Item3 + " to centralize common patterns",
                    Impact = SeverityLevel.Medium,
                    RefactoringOptions = new List<string> { "Create base class", "Extract to utility", "Use design pattern" },
                    EstimatedEffort = "Medium"
                });
            }
        }

        // 3. Logical Matches - Similar logic with different implementations
        for (int i = 0; i < Math.Min(files.Count - 1, 20); i++)
        {
            var logicPatterns = new[] {
                ("Data transformation", "Mapping objects to DTOs", "Create generic mapper utility"),
                ("Filtering logic", "Array filtering with conditions", "Extract to filter service"),
                ("Sorting algorithm", "Custom sorting implementation", "Use built-in sort with comparator"),
                ("Search functionality", "Linear search in collections", "Create search utility service"),
                ("Format conversion", "Converting between data formats", "Use format converter service"),
                ("State management", "Updating component state", "Use state management library"),
                ("Cache invalidation", "Manual cache clearing logic", "Use cache manager service"),
                ("Date formatting", "Custom date format conversion", "Use date utility library"),
                ("String manipulation", "Parsing and formatting strings", "Create string utility class"),
                ("Permission checking", "Role-based access logic", "Use authorization service")
            };

            if (i < logicPatterns.Length && i + 1 < files.Count)
            {
                var logic = logicPatterns[i];
                duplications.Add(new CodeDuplication
                {
                    DuplicatedCode = @"const result = collection
    .filter(item => item.isActive)
    .map(item => transform(item))
    .sort((a, b) => compare(a, b));",
                    Locations = new List<DuplicationLocation>
                    {
                        new() { FilePath = files[i].FilePath, StartLine = 234 + i * 6, EndLine = 238 + i * 6, MethodName = "process" + i, ClassName = ExtractClassName(files[i].FilePath) },
                        new() { FilePath = files[i + 1].FilePath, StartLine = 445 + i * 4, EndLine = 449 + i * 4, MethodName = "handle" + i, ClassName = ExtractClassName(files[i + 1].FilePath) }
                    },
                    Type = DuplicationType.LogicalMatch,
                    LineCount = 5,
                    SimilarityPercentage = 85 + random.Next(0, 10),
                    Description = $"{logic.Item1}: {logic.Item2} duplicated across files",
                    Suggestion = logic.Item3,
                    Impact = random.Next(0, 100) < 30 ? SeverityLevel.Medium : SeverityLevel.Low,
                    RefactoringOptions = new List<string> { "Create utility function", "Use library method", "Extract to service" },
                    EstimatedEffort = "Low"
                });
            }
        }

        // 4. Functional Matches - Same functionality, different syntax
        for (int i = 0; i < Math.Min(files.Count - 1, 10); i++)
        {
            var functionalPatterns = new[] {
                ("Configuration loading", "Reading and parsing config files"),
                ("Error message formatting", "Formatting error messages for display"),
                ("URL construction", "Building API endpoint URLs"),
                ("Query string building", "Creating query parameters"),
                ("Response parsing", "Parsing API response data"),
                ("Token generation", "Creating authentication tokens"),
                ("ID generation", "Generating unique identifiers"),
                ("Hash calculation", "Computing data hashes"),
                ("File path resolution", "Resolving relative file paths"),
                ("Dependency resolution", "Resolving service dependencies")
            };

            if (i < functionalPatterns.Length && i + 1 < files.Count)
            {
                var func = functionalPatterns[i];
                duplications.Add(new CodeDuplication
                {
                    DuplicatedCode = @"function buildUrl(base, path, params) {
    let url = base + '/' + path;
    if (params) url += '?' + Object.keys(params).map(k => k + '=' + params[k]).join('&');
    return url;
}",
                    Locations = new List<DuplicationLocation>
                    {
                        new() { FilePath = files[i].FilePath, StartLine = 156 + i * 7, EndLine = 161 + i * 7, MethodName = "utility" + i, ClassName = ExtractClassName(files[i].FilePath) },
                        new() { FilePath = files[i + 1].FilePath, StartLine = 289 + i * 5, EndLine = 294 + i * 5, MethodName = "helper" + i, ClassName = ExtractClassName(files[i + 1].FilePath) }
                    },
                    Type = DuplicationType.FunctionalMatch,
                    LineCount = 6,
                    SimilarityPercentage = 88 + random.Next(0, 8),
                    Description = $"{func.Item1}: {func.Item2} implemented multiple times",
                    Suggestion = "Consolidate into single reusable function in shared utilities",
                    Impact = SeverityLevel.Low,
                    RefactoringOptions = new List<string> { "Extract to shared module", "Use utility library", "Create helper class" },
                    EstimatedEffort = "Low"
                });
            }
        }

        // 5. Partial Matches - Code blocks with variations
        for (int i = 0; i < Math.Min(files.Count - 1, 8); i++)
        {
            if (i + 1 >= files.Count) break;

            duplications.Add(new CodeDuplication
            {
                DuplicatedCode = @"async function processData(input) {
    validate(input);
    const transformed = transform(input);
    await save(transformed);
    return transformed;
}",
                Locations = new List<DuplicationLocation>
                {
                    new() { FilePath = files[i].FilePath, StartLine = 312 + i * 8, EndLine = 318 + i * 8, MethodName = "process" + i, ClassName = ExtractClassName(files[i].FilePath) },
                    new() { FilePath = files[i + 1].FilePath, StartLine = 523 + i * 6, EndLine = 529 + i * 6, MethodName = "execute" + i, ClassName = ExtractClassName(files[i + 1].FilePath) }
                },
                Type = DuplicationType.PartialMatch,
                LineCount = 7,
                SimilarityPercentage = 75 + random.Next(0, 15),
                Description = $"Partial code duplication in data processing workflow with slight variations",
                Suggestion = "Extract common workflow into template method pattern with customizable steps",
                Impact = SeverityLevel.Low,
                RefactoringOptions = new List<string> { "Template method pattern", "Strategy pattern", "Extract base class" },
                EstimatedEffort = "Medium"
            });
        }

        return duplications;
    }

    private string ExtractClassName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        // Remove common suffixes
        fileName = fileName.Replace("Service", "")
                          .Replace("Controller", "")
                          .Replace("Repository", "")
                          .Replace("Helper", "")
                          .Replace("Util", "")
                          .Replace("Manager", "");
        return string.IsNullOrEmpty(fileName) ? "UnknownClass" : fileName + "Class";
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
        await SendProgress(connectionId, 7, "Step 1: Analyzing project structure - Scanning folders and files...");
        
        // Initialize project context
        _projectContext = new ProjectContext
        {
            TotalFiles = files.Count,
            TotalLines = files.Sum(f => f.LineCount),
            ProjectName = DetectProjectName(files, folderStructure)
        };
        
        await SendProgress(connectionId, 10, $"Found {_projectContext.TotalFiles} files with {_projectContext.TotalLines:N0} lines of code");
        
        // Step 1.1: Analyze folder structure and identify modules
        await SendProgress(connectionId, 12, "Analyzing folder structure and detecting project modules...");
        AnalyzeModulesAndStructure(files, folderStructure);
        
        // Step 1.2: Map file languages
        await SendProgress(connectionId, 14, "Mapping file types and languages...");
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            var language = GetLanguageName(extension);
            _projectContext.FileLanguageMap[file.FilePath] = language;
        }
        
        // Step 1.3: Analyze file dependencies and connections
        await SendProgress(connectionId, 16, "Analyzing file dependencies and connections between components...");
        AnalyzeFileDependencies(files);
        
        // Step 1.4: Detect coding patterns used in the project
        await SendProgress(connectionId, 18, "Detecting coding standards and patterns used in the project...");
        DetectCodingPatterns(files);
        
        // Step 1.5: Analyze error handling approach
        await SendProgress(connectionId, 19, "Analyzing error handling strategies across the project...");
        AnalyzeErrorHandling(files);
        
        // Step 1.6: Build comprehensive method and API inventory
        await SendProgress(connectionId, 20, "Building complete inventory of methods, APIs, and database queries...");
        BuildMethodInventory(files);
        
        await SendProgress(connectionId, 22, $"Project analysis complete: {_projectContext.Modules.Count} modules detected");

        var provider = _configuration["AI:Provider"] ?? "Demo";

        if (provider.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateProjectSummary(files, folderStructure, fileTypeDistribution);
        }

        // For real AI providers, use the comprehensive context
        var client = _agentFactory.CreateViolationDetectionClient();

        // Sample key files for analysis - increased from 10 to 30 for comprehensive analysis
        var keyFiles = files
            .Where(f => IsKeyFile(f.FilePath))
            .Take(30)
            .Select(f => $"{f.FilePath}:\n{f.Content.Substring(0, Math.Min(1000, f.Content.Length))}")
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
            // Entry points
            "program", "startup", "main", "app", "index",
            // Core architecture files
            "controller", "service", "repository", "model", "entity",
            "component", "module", "routing", "guard", "interceptor",
            // Configuration files
            "package.json", "angular.json", "tsconfig",
            "pom.xml", "build.gradle", "cargo.toml",
            "requirements.txt", "gemfile", "composer.json",
            "appsettings", "launchsettings",
            ".csproj", ".vbproj", ".fsproj", ".sln",
            // Documentation
            "readme"
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

        // Check for Angular - look for @angular imports or Angular-specific patterns
        if (fileTypeDistribution.ContainsKey("Angular") ||
            files.Any(f => f.Content.Contains("@angular/") ||
                          f.Content.Contains("import { Component }") && f.Content.Contains("@Component")))
            stacks.Add("Angular");

        // Check for React - must have actual React imports or JSX, NOT just "reactive" from RxJS
        if (files.Any(f => (f.Content.Contains("from 'react'") ||
                           f.Content.Contains("from \"react\"") ||
                           f.Content.Contains("import React") ||
                           f.FileType == FileType.JSX ||
                           f.FileType == FileType.TSX) &&
                           !f.Content.Contains("@angular/")))
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

        foreach (var file in files.Where(f => f.FileType != FileType.Markdown).Take(100))
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
        var description = new System.Text.StringBuilder();

        // Start with architecture and tech stack
        description.Append($"This is a {architecture}-based application");

        if (!string.IsNullOrEmpty(techStack))
        {
            description.Append($" built using {techStack}");
        }

        description.Append(". ");

        // Add business logic/functionality
        if (!string.IsNullOrEmpty(businessLogic) && !businessLogic.Contains("General Purpose"))
        {
            description.Append($"The system implements comprehensive functionality including: {businessLogic}. ");
        }

        // Add modern practices note
        description.Append("The project follows modern development practices with a focus on ");
        description.Append("maintainability, scalability, and code quality.");

        return description.ToString();
    }

    private string GenerateCoreFunctionality(List<CodeFile> files, Dictionary<string, int> folderStructure)
    {
        var functionalities = new List<string>();

        // Analyze actual code content for functionality
        var codeContent = string.Join(" ", files.Take(150).Select(f => f.Content.ToLowerInvariant()));

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

        // Check package.json for frontend dependencies
        var packageJson = files.FirstOrDefault(f => f.FilePath.EndsWith("package.json"));
        if (packageJson != null)
        {
            var content = packageJson.Content;

            // Angular
            if (content.Contains("\"@angular/core\"") || content.Contains("\"@angular/"))
                dependencies.Add("Angular Framework");

            if (content.Contains("\"@angular/material\""))
                dependencies.Add("Angular Material UI");

            // React - check for actual React package, not RxJS
            if (content.Contains("\"react\":") && !content.Contains("@angular"))
                dependencies.Add("React Library");

            // Vue
            if (content.Contains("\"vue\":"))
                dependencies.Add("Vue.js Framework");

            // RxJS
            if (content.Contains("\"rxjs\":"))
                dependencies.Add("RxJS (Reactive Extensions)");

            // SignalR client
            if (content.Contains("@microsoft/signalr"))
                dependencies.Add("SignalR Client");

            // Tailwind CSS
            if (content.Contains("\"tailwindcss\":"))
                dependencies.Add("Tailwind CSS");
        }

        // Check .csproj files for backend dependencies
        var csprojFiles = files.Where(f => f.FilePath.EndsWith(".csproj")).ToList();
        foreach (var csproj in csprojFiles)
        {
            var content = csproj.Content;

            if (content.Contains("EntityFrameworkCore") && !dependencies.Contains("Entity Framework Core"))
                dependencies.Add("Entity Framework Core");

            if (content.Contains("Swashbuckle") && !dependencies.Contains("Swagger/OpenAPI"))
                dependencies.Add("Swagger/OpenAPI");

            if (content.Contains("SignalR") && !dependencies.Contains("SignalR"))
                dependencies.Add("SignalR");

            if (content.Contains("Azure.AI.OpenAI") || content.Contains("OpenAI"))
                dependencies.Add("OpenAI SDK");

            if (content.Contains("LibGit2Sharp"))
                dependencies.Add("LibGit2Sharp (Git Integration)");
        }

        return dependencies.Distinct().Take(15).ToList();
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

    private string GetLanguageName(string extension)
    {
        return extension switch
        {
            ".cs" => "C#",
            ".ts" => "TypeScript",
            ".tsx" => "TypeScript (React)",
            ".js" => "JavaScript",
            ".jsx" => "JavaScript (React)",
            ".sql" => "SQL",
            ".py" => "Python",
            ".java" => "Java",
            ".go" => "Go",
            ".rb" => "Ruby",
            ".php" => "PHP",
            ".html" => "HTML",
            ".css" => "CSS",
            ".scss" => "SCSS",
            ".vue" => "Vue",
            ".json" => "JSON",
            ".xml" => "XML",
            _ => "Unknown"
        };
    }

    private string GetLanguageIdentifier(string extension)
    {
        return extension switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".tsx" => "typescript",
            ".js" => "javascript",
            ".jsx" => "javascript",
            ".sql" => "sql",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".html" => "html",
            ".css" => "css",
            ".scss" => "scss",
            ".vue" => "vue",
            ".json" => "json",
            ".xml" => "xml",
            _ => "text"
        };
    }

    /// <summary>
    /// Builds comprehensive analysis context including folder structure, file metadata, method signatures, and line numbers
    /// </summary>
    private string BuildComprehensiveAnalysisContext(CodeFile[] files, string folderPath)
    {
        var contextBuilder = new StringBuilder();

        contextBuilder.AppendLine($"=== FOLDER: {folderPath} ===");
        contextBuilder.AppendLine($"Files in this folder: {files.Length}");
        contextBuilder.AppendLine();

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            var language = GetLanguageName(extension);
            var langId = GetLanguageIdentifier(extension);

            contextBuilder.AppendLine("==========================================");
            contextBuilder.AppendLine($"FILE: {file.FilePath}");
            contextBuilder.AppendLine($"*** LANGUAGE: {language.ToUpperInvariant()} ***");
            contextBuilder.AppendLine($"*** EXTENSION: {extension} ***");
            contextBuilder.AppendLine($"*** IMPORTANT: Use ONLY {language} syntax for this file! ***");
            contextBuilder.AppendLine($"Type: {file.FileType}");
            contextBuilder.AppendLine($"Total Lines: {file.LineCount}");
            contextBuilder.AppendLine();

            // Extract and list methods/functions for better context
            var methods = ExtractMethods(file.Content, language);
            if (methods.Any())
            {
                contextBuilder.AppendLine($"Methods/Functions ({methods.Count}):");
                foreach (var method in methods.Take(50)) // Limit to avoid token overflow
                {
                    contextBuilder.AppendLine($"  - Line {method.LineNumber}: {method.Signature} ({method.LineCount} lines)");
                }
            }

            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"COMPLETE FILE CONTENT WITH LINE NUMBERS:");
            contextBuilder.AppendLine($"```{langId}");

            // Add line numbers for precise referencing
            var lines = file.Content.Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                contextBuilder.AppendLine($"{i + 1,4}: {lines[i]}");
            }

            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("==========================================");
            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }

    /// <summary>
    /// Extracts method signatures and their locations from code
    /// </summary>
    private List<(int LineNumber, string Signature, int LineCount)> ExtractMethods(string content, string language)
    {
        var methods = new List<(int LineNumber, string Signature, int LineCount)>();
        var lines = content.Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNumber = i + 1;

            // C# method detection
            if (language == "C#" &&
                (line.Contains("public ") || line.Contains("private ") || line.Contains("protected ") || line.Contains("internal ")) &&
                (line.Contains("(") && (line.Contains("{") || (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("{")))))
            {
                var methodEnd = FindMethodEnd(lines, i);
                var methodLines = methodEnd - i + 1;
                methods.Add((lineNumber, line.Length > 100 ? line.Substring(0, 100) + "..." : line, methodLines));
            }
            // SQL stored procedure/function detection
            else if (language == "SQL" &&
                     (line.StartsWith("CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase) ||
                      line.StartsWith("CREATE FUNCTION", StringComparison.OrdinalIgnoreCase) ||
                      line.StartsWith("ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase)))
            {
                var methodEnd = FindSQLProcedureEnd(lines, i);
                var methodLines = methodEnd - i + 1;
                methods.Add((lineNumber, line.Length > 100 ? line.Substring(0, 100) + "..." : line, methodLines));
            }
            // TypeScript/JavaScript function detection
            else if ((language == "TypeScript" || language == "JavaScript") &&
                     (line.Contains("function ") || line.Contains("=> {") ||
                      (line.Contains("(") && line.Contains(")") && (line.Contains("{") || line.Contains("=>")))))
            {
                var methodEnd = FindMethodEnd(lines, i);
                var methodLines = methodEnd - i + 1;
                methods.Add((lineNumber, line.Length > 100 ? line.Substring(0, 100) + "..." : line, methodLines));
            }
            // Python function detection
            else if (language == "Python" && line.StartsWith("def "))
            {
                var methodEnd = FindPythonFunctionEnd(lines, i);
                var methodLines = methodEnd - i + 1;
                methods.Add((lineNumber, line.Length > 100 ? line.Substring(0, 100) + "..." : line, methodLines));
            }
        }

        return methods;
    }

    private int FindMethodEnd(string[] lines, int startIndex)
    {
        int braceCount = 0;
        bool foundOpenBrace = false;

        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var ch in line)
            {
                if (ch == '{') { braceCount++; foundOpenBrace = true; }
                if (ch == '}') braceCount--;
            }

            if (foundOpenBrace && braceCount == 0)
                return i;

            // Safety limit
            if (i - startIndex > 500)
                return i;
        }

        return Math.Min(startIndex + 50, lines.Length - 1);
    }

    private int FindSQLProcedureEnd(string[] lines, int startIndex)
    {
        for (int i = startIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Equals("GO", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("CREATE FUNCTION", StringComparison.OrdinalIgnoreCase))
                return i - 1;

            // Safety limit
            if (i - startIndex > 500)
                return i;
        }

        return lines.Length - 1;
    }

    private int FindPythonFunctionEnd(string[] lines, int startIndex)
    {
        if (startIndex >= lines.Length - 1)
            return startIndex;

        var defLine = lines[startIndex];
        var baseIndent = defLine.Length - defLine.TrimStart().Length;

        for (int i = startIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var currentIndent = line.Length - line.TrimStart().Length;

            // If we find a line at the same or less indentation level, previous line was the end
            if (currentIndent <= baseIndent)
                return i - 1;

            // Safety limit
            if (i - startIndex > 500)
                return i;
        }

        return lines.Length - 1;
    }

    /// <summary>
    /// Validates that suggestions match the file's language to prevent cross-language contamination
    /// </summary>
    private bool IsValidLanguageSuggestion(string filePath, string suggestedFix, string description)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var combinedText = $"{suggestedFix} {description}".ToLowerInvariant();

        // SQL files - reject C#/JavaScript syntax
        if (extension == ".cs")
        {
            var forbiddenTerms = new[]
            {
                "async", "await", "try-catch", "try/catch", "trycatch",
                "?.", "??", "private const", "using statement", "using (",
                "entity framework", "entityframework", "ef core",
                "null-conditional operator", "null conditional",
                "wrap async", "asynchronous operations"
            };

            foreach (var term in forbiddenTerms)
            {
                if (combinedText.Contains(term))
                {
                    // This is an invalid suggestion for SQL
                    return false;
                }
            }
        }

        // C# files - reject SQL syntax
        if (extension == ".sql")
        {
            var forbiddenTerms = new[]
            {
                "declare @", "begin try", "end try", "begin catch", "end catch",
                "set nocount", "isnull(", "coalesce(", " go\n", " go ",
                "begin transaction", "commit transaction", "rollback"
            };

            foreach (var term in forbiddenTerms)
            {
                if (combinedText.Contains(term))
                {
                    return false;
                }
            }
        }

        // JavaScript/TypeScript files - reject SQL and C# specific syntax
        if (extension == ".js" || extension == ".ts" || extension == ".jsx" || extension == ".tsx")
        {
            var forbiddenTerms = new[]
            {
                "declare @", "begin try", "private const int", "public class",
                "entity framework", "isnull(", "set nocount"
            };

            foreach (var term in forbiddenTerms)
            {
                if (combinedText.Contains(term))
                {
                    return false;
                }
            }
        }

        return true;
    }

    #region Comprehensive Project Context Building

    /// <summary>
    /// Detects project name from solution files or folder names
    /// </summary>
    private string DetectProjectName(List<CodeFile> files, Dictionary<string, int> folderStructure)
    {
        // Check for .sln file
        var slnFile = files.FirstOrDefault(f => f.FilePath.EndsWith(".sln"));
        if (slnFile != null)
        {
            return Path.GetFileNameWithoutExtension(slnFile.FilePath);
        }
        
        // Check for package.json
        var packageJson = files.FirstOrDefault(f => f.FilePath.EndsWith("package.json"));
        if (packageJson != null && packageJson.Content.Contains("\"name\":"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(packageJson.Content, @"""name"":\s*""([^""]+)""");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        
        // Fallback to root folder name
        var rootFolder = folderStructure.Keys.FirstOrDefault() ?? "UnknownProject";
        return Path.GetFileName(rootFolder);
    }

    /// <summary>
    /// Analyzes project modules (API, UI, Infrastructure, etc.)
    /// </summary>
    private void AnalyzeModulesAndStructure(List<CodeFile> files, Dictionary<string, int> folderStructure)
    {
        var modules = new List<ProjectModule>();
        
        // Group files by top-level folders
        var folderGroups = files.GroupBy(f =>
        {
            var parts = f.FilePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : "Root";
        });

        foreach (var group in folderGroups)
        {
            var folderName = group.Key;
            var moduleFiles = group.ToList();
            
            var module = new ProjectModule
            {
                Name = folderName,
                Path = folderName,
                Files = moduleFiles.Select(f => f.FilePath).ToList()
            };
            
            // Detect module type
            if (folderName.Contains("API", StringComparison.OrdinalIgnoreCase) ||
                folderName.Contains("Server", StringComparison.OrdinalIgnoreCase) ||
                moduleFiles.Any(f => f.FilePath.Contains("Controller")))
            {
                module.Type = "API";
                module.Purpose = "Backend API - Handles HTTP requests and business logic";
            }
            else if (folderName.Contains("Web", StringComparison.OrdinalIgnoreCase) ||
                     folderName.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                     folderName.Contains("Client", StringComparison.OrdinalIgnoreCase) ||
                     moduleFiles.Any(f => f.FilePath.Contains("component")))
            {
                module.Type = "UI";
                module.Purpose = "User Interface - Frontend application for user interaction";
            }
            else if (folderName.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase))
            {
                module.Type = "Library";
                module.Purpose = "Infrastructure Layer - Data access, external services, and implementations";
            }
            else if (folderName.Contains("Domain", StringComparison.OrdinalIgnoreCase))
            {
                module.Type = "Library";
                module.Purpose = "Domain Layer - Business entities and core business rules";
            }
            else if (folderName.Contains("Application", StringComparison.OrdinalIgnoreCase))
            {
                module.Type = "Library";
                module.Purpose = "Application Layer - Use cases and application business rules";
            }
            else
            {
                module.Type = "Library";
                module.Purpose = "Supporting module";
            }
            
            modules.Add(module);
        }
        
        _projectContext!.Modules = modules;
        _projectContext.FolderStructure = folderStructure;
    }

    /// <summary>
    /// Analyzes file dependencies (imports, using statements, etc.)
    /// </summary>
    private void AnalyzeFileDependencies(List<CodeFile> files)
    {
        foreach (var file in files)
        {
            var dependencies = new List<string>();
            var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            
            if (extension == ".cs")
            {
                // C# using statements
                var matches = System.Text.RegularExpressions.Regex.Matches(file.Content, @"using\s+([^;]+);");
                dependencies.AddRange(matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value.Trim()));
            }
            else if (extension == ".ts" || extension == ".js")
            {
                // TypeScript/JavaScript imports
                var matches = System.Text.RegularExpressions.Regex.Matches(file.Content, @"import\s+.*from\s+['""]([^'""]+)['""]");
                dependencies.AddRange(matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value.Trim()));
            }
            
            if (dependencies.Any())
            {
                _projectContext!.FileDependencies[file.FilePath] = dependencies;
            }
        }
    }

    /// <summary>
    /// Detects common coding patterns used in the project
    /// </summary>
    private void DetectCodingPatterns(List<CodeFile> files)
    {
        var patterns = new Dictionary<string, string>();
        
        // Naming conventions
        var hasPascalCase = files.Any(f => System.Text.RegularExpressions.Regex.IsMatch(f.Content, @"(public|private|protected)\s+class\s+[A-Z][a-z]+"));
        var hasCamelCase = files.Any(f => System.Text.RegularExpressions.Regex.IsMatch(f.Content, @"(let|const|var)\s+[a-z][a-zA-Z]+"));
        
        if (hasPascalCase) patterns["C# Class Naming"] = "PascalCase for classes";
        if (hasCamelCase) patterns["JavaScript Variable Naming"] = "camelCase for variables";
        
        // Dependency injection pattern
        var hasDI = files.Any(f => f.Content.Contains("constructor") && f.Content.Contains("private readonly"));
        if (hasDI) patterns["Dependency Injection"] = "Constructor injection pattern detected";
        
        // Async pattern
        var hasAsync = files.Any(f => f.Content.Contains("async") && f.Content.Contains("await"));
        if (hasAsync) patterns["Asynchronous Programming"] = "async/await pattern used";
        
        // Repository pattern
        var hasRepository = files.Any(f => f.FilePath.Contains("Repository") || f.Content.Contains("IRepository"));
        if (hasRepository) patterns["Repository Pattern"] = "Data access through repositories";
        
        _projectContext!.CodingPatterns = patterns;
        
        // Detect architectural pattern
        var hasCleanArchitecture = files.Any(f => f.FilePath.Contains("Domain") || f.FilePath.Contains("Application") || f.FilePath.Contains("Infrastructure"));
        _projectContext.ArchitecturalPattern = hasCleanArchitecture 
            ? "Clean Architecture (Domain-Driven Design)" 
            : "Standard Layered Architecture";
    }

    /// <summary>
    /// Analyzes how error handling is implemented across the project
    /// </summary>
    private void AnalyzeErrorHandling(List<CodeFile> files)
    {
        var approaches = new List<string>();
        
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            
            if (extension == ".cs")
            {
                if (file.Content.Contains("try") && file.Content.Contains("catch"))
                {
                    approaches.Add("C#: try-catch blocks for exception handling");
                }
                if (file.Content.Contains("ILogger") || file.Content.Contains("_logger"))
                {
                    approaches.Add("C#: Structured logging with ILogger");
                }
            }
            else if (extension == ".sql")
            {
                if (file.Content.Contains("BEGIN TRY") && file.Content.Contains("BEGIN CATCH"))
                {
                    approaches.Add("SQL: TRY-CATCH blocks for error handling");
                }
                if (file.Content.Contains("RAISERROR") || file.Content.Contains("THROW"))
                {
                    approaches.Add("SQL: RAISERROR/THROW for error propagation");
                }
            }
            else if (extension == ".ts" || extension == ".js")
            {
                if (file.Content.Contains("try") && file.Content.Contains("catch"))
                {
                    approaches.Add("TypeScript/JavaScript: try-catch blocks");
                }
                if (file.Content.Contains("catchError") || file.Content.Contains(".catch("))
                {
                    approaches.Add("TypeScript/JavaScript: Promise/Observable error handling");
                }
            }
        }
        
        _projectContext!.ErrorHandlingApproach = string.Join("; ", approaches.Distinct().Take(5));
    }

    /// <summary>
    /// Builds comprehensive inventory of methods, APIs, and database operations
    /// </summary>
    private void BuildMethodInventory(List<CodeFile> files)
    {
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            var methods = new List<string>();
            
            // API Controllers (C#)
            if (file.FilePath.Contains("Controller") && extension == ".cs")
            {
                var apiMatches = System.Text.RegularExpressions.Regex.Matches(
                    file.Content,
                    @"\[(HttpGet|HttpPost|HttpPut|HttpDelete|HttpPatch)\(""([^""]*)""\)\][\s\S]*?public\s+(?:async\s+)?(?:Task<)?(?:IActionResult|ActionResult).*?\s+(\w+)\("
                );
                
                foreach (System.Text.RegularExpressions.Match match in apiMatches)
                {
                    var httpMethod = match.Groups[1].Value;
                    var route = match.Groups[2].Value;
                    var methodName = match.Groups[3].Value;
                    _projectContext!.ApiEndpoints[$"{httpMethod} {route}"] = $"{file.FilePath}::{methodName}";
                }
            }
            
            // Services (C#)
            if (file.FilePath.Contains("Service") && extension == ".cs")
            {
                var serviceMatches = System.Text.RegularExpressions.Regex.Matches(
                    file.Content,
                    @"public\s+(?:async\s+)?(?:Task<[^>]+>|[^\s]+)\s+(\w+)\([^)]*\)"
                );
                
                foreach (System.Text.RegularExpressions.Match match in serviceMatches)
                {
                    var methodName = match.Groups[1].Value;
                    _projectContext!.ServiceMethods[methodName] = file.FilePath;
                }
            }
            
            // SQL Stored Procedures
            if (extension == ".sql")
            {
                var spMatches = System.Text.RegularExpressions.Regex.Matches(
                    file.Content,
                    @"CREATE\s+PROCEDURE\s+\[?([^\]\s]+)\]?",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                foreach (System.Text.RegularExpressions.Match match in spMatches)
                {
                    var spName = match.Groups[1].Value;
                    _projectContext!.DataLayerQueries[spName] = file.FilePath;
                }
            }
            
            // Store all methods for the file
            if (methods.Any())
            {
                _projectContext!.MethodInventory[file.FilePath] = methods;
            }
        }
    }

    #endregion
}

