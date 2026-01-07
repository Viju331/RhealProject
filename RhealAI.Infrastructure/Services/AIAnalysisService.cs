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

        // Filter out SQL files - only process UI and API files
        var filteredFiles = files.Where(f =>
            !f.FilePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) &&
            f.FileType != FileType.SQL &&
            f.Content.Length > 100
        ).ToList();

        foreach (var file in filteredFiles)
        {
            var lines = file.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var linesArray = lines.ToArray();

            // 1. Check for long methods - GENUINE ANALYSIS
            var methodPattern = new[] { "function ", "func ", "def ", "public ", "private ", "protected ", "void ", "async ", "Task<", "Task " };
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (methodPattern.Any(p => line.Contains(p, StringComparison.OrdinalIgnoreCase)) &&
                    line.Contains("(") && !line.TrimStart().StartsWith("//"))
                {
                    // Count actual lines until method end
                    int methodLength = 0;
                    int openBraces = 0;
                    bool inMethod = false;
                    int methodStartLine = i;

                    for (int j = i; j < Math.Min(i + 200, lines.Length); j++)
                    {
                        var currentLine = lines[j];
                        if (currentLine.Contains("{")) { openBraces++; inMethod = true; }
                        if (currentLine.Contains("}")) openBraces--;
                        if (inMethod) methodLength++;
                        if (inMethod && openBraces == 0) break;
                    }

                    if (methodLength > 50)
                    {
                        var methodName = ExtractMethodNameFromLine(line);
                        var snippet = GetMethodSnippet(lines, methodStartLine, methodLength);
                        var endLine = methodStartLine + methodLength;

                        refactorings.Add(new Refactoring
                        {
                            Id = Guid.NewGuid().ToString(),
                            FilePath = file.FilePath,
                            LineNumber = methodStartLine + 1,
                            EndLineNumber = Math.Min(endLine + 1, lines.Length),
                            RefactoringType = "Extract Method",
                            Title = $"Long Method: {methodName}",
                            Description = $"Method '{methodName}' has {methodLength} lines. Consider breaking it into smaller, focused methods.",
                            CurrentCode = snippet,
                            SuggestedCode = $"// Break down '{methodName}' into logical steps:\n// 1. Extract validation logic\n// 2. Extract business logic\n// 3. Extract data access/transformation",
                            Reason = "Long methods violate Single Responsibility Principle and are hard to test and maintain.",
                            Benefits = "Improved readability, easier testing, better maintainability, clearer separation of concerns.",
                            Priority = methodLength > 100 ? SeverityLevel.Critical : methodLength > 70 ? SeverityLevel.High : SeverityLevel.Medium,
                            ImprovementAreas = new List<string> { "Readability", "Maintainability", "Testability", "Single Responsibility" }
                        });

                        // Skip ahead to avoid duplicate detections
                        i = endLine;
                    }
                }
            }

            // 2. Check for deeply nested conditionals - GENUINE ANALYSIS
            int currentNesting = 0;
            int maxNestingLine = -1;
            int maxNesting = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Track nesting level
                if (line.StartsWith("if ") || line.StartsWith("if(") ||
                    line.StartsWith("for ") || line.StartsWith("for(") ||
                    line.StartsWith("while ") || line.StartsWith("while(") ||
                    line.StartsWith("switch ") || line.StartsWith("switch("))
                {
                    currentNesting++;
                    if (currentNesting > maxNesting)
                    {
                        maxNesting = currentNesting;
                        maxNestingLine = i;
                    }
                }

                if (line.Contains("{")) currentNesting++;
                if (line.Contains("}")) currentNesting = Math.Max(0, currentNesting - 1);
            }

            if (maxNesting > 3 && maxNestingLine >= 0)
            {
                var snippet = GetContextSnippet(lines, maxNestingLine, 7);
                refactorings.Add(new Refactoring
                {
                    Id = Guid.NewGuid().ToString(),
                    FilePath = file.FilePath,
                    LineNumber = Math.Max(1, maxNestingLine - 2),
                    EndLineNumber = Math.Min(lines.Length, maxNestingLine + 5),
                    RefactoringType = "Simplify Conditional",
                    Title = $"Deep Nesting: {maxNesting} Levels",
                    Description = $"Code has {maxNesting} levels of nesting at line {maxNestingLine + 1}, making it difficult to follow.",
                    CurrentCode = snippet,
                    SuggestedCode = "// Refactor options:\n// 1. Use guard clauses: if (!condition) return;\n// 2. Extract nested logic to separate methods\n// 3. Use early returns to reduce nesting",
                    Reason = "Deep nesting increases cyclomatic complexity and cognitive load, making code error-prone.",
                    Benefits = "Flatter code structure, reduced complexity, easier to understand and debug.",
                    Priority = maxNesting > 5 ? SeverityLevel.High : SeverityLevel.Medium,
                    ImprovementAreas = new List<string> { "Readability", "Complexity", "Maintainability" }
                });
            }

            // 3. Check for magic numbers - GENUINE ANALYSIS
            var magicNumberPattern = new System.Text.RegularExpressions.Regex(@"\b(\d{2,})\b");
            var reportedLines = new HashSet<int>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Skip comments, constants, and already reported lines
                if (line.TrimStart().StartsWith("//") ||
                    line.Contains("const ") ||
                    line.Contains("readonly ") ||
                    reportedLines.Contains(i))
                    continue;

                var matches = magicNumberPattern.Matches(line);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var number = match.Value;

                    // Skip common numbers
                    if (number == "10" || number == "100" || number == "1000" ||
                        number == "24" || number == "60" || number == "12")
                        continue;

                    var snippet = GetContextSnippet(lines, i, 3);
                    var constantName = GenerateConstantName(line, number);

                    refactorings.Add(new Refactoring
                    {
                        Id = Guid.NewGuid().ToString(),
                        FilePath = file.FilePath,
                        LineNumber = Math.Max(1, i),
                        EndLineNumber = Math.Min(lines.Length, i + 2),
                        RefactoringType = "Replace Magic Number",
                        Title = $"Magic Number: {number}",
                        Description = $"Hardcoded value '{number}' should be replaced with a named constant for clarity.",
                        CurrentCode = snippet,
                        SuggestedCode = $"private const int {constantName} = {number};\n// Then use: {constantName}",
                        Reason = "Magic numbers lack context and make code harder to understand and maintain.",
                        Benefits = "Self-documenting code, easier to update values, clearer intent.",
                        Priority = SeverityLevel.Medium,
                        ImprovementAreas = new List<string> { "Readability", "Maintainability", "Code Quality" }
                    });

                    reportedLines.Add(i);
                    break; // Only one refactoring per line
                }
            }

            // 4. Check for duplicate code within file - GENUINE ANALYSIS
            var codeBlockMap = new Dictionary<string, List<int>>();
            const int blockSize = 4;

            for (int i = 0; i <= lines.Length - blockSize; i++)
            {
                var block = string.Join("\n", lines.Skip(i).Take(blockSize));
                var normalized = NormalizeCode(block);

                // Skip if too small or mostly whitespace
                if (normalized.Length < 60 || string.IsNullOrWhiteSpace(normalized))
                    continue;

                if (!codeBlockMap.ContainsKey(normalized))
                    codeBlockMap[normalized] = new List<int>();

                codeBlockMap[normalized].Add(i);
            }

            var duplicates = codeBlockMap.Where(kvp => kvp.Value.Count > 1).ToList();
            foreach (var duplicate in duplicates.Take(3)) // Limit to top 3 per file
            {
                var locations = duplicate.Value;
                var firstLocation = locations.First();
                var snippet = GetContextSnippet(lines, firstLocation, blockSize + 2);

                refactorings.Add(new Refactoring
                {
                    Id = Guid.NewGuid().ToString(),
                    FilePath = file.FilePath,
                    LineNumber = firstLocation + 1,
                    EndLineNumber = firstLocation + blockSize + 1,
                    RefactoringType = "Extract Method",
                    Title = $"Duplicate Code: {locations.Count} Occurrences",
                    Description = $"This {blockSize}-line code block appears {locations.Count} times at lines: {string.Join(", ", locations.Select(l => l + 1))}",
                    CurrentCode = snippet,
                    SuggestedCode = $"// Extract to reusable method:\nprivate void ExtractedMethod() {{\n    // Move common logic here\n}}",
                    Reason = "Code duplication violates DRY principle. Changes must be made in multiple places, increasing bug risk.",
                    Benefits = "Single source of truth, easier maintenance, reduced code size, consistent behavior.",
                    Priority = locations.Count > 3 ? SeverityLevel.High : SeverityLevel.Medium,
                    ImprovementAreas = new List<string> { "Maintainability", "DRY Principle", "Code Duplication" }
                });
            }

            // 5. Check for long parameter lists - GENUINE ANALYSIS
            var methodSignaturePattern = new System.Text.RegularExpressions.Regex(@"(public|private|protected|internal)?\s*(static)?\s*(async)?\s*\w+\s+\w+\s*\([^)]+\)");

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var match = methodSignaturePattern.Match(line);

                if (match.Success)
                {
                    var paramCount = line.Count(c => c == ',') + 1;
                    var openParen = line.IndexOf('(');
                    var closeParen = line.IndexOf(')');

                    // Verify it's actually a method with parameters
                    if (openParen > 0 && closeParen > openParen && paramCount > 4)
                    {
                        var methodName = ExtractMethodNameFromLine(line);
                        var snippet = GetContextSnippet(lines, i, 3);

                        refactorings.Add(new Refactoring
                        {
                            Id = Guid.NewGuid().ToString(),
                            FilePath = file.FilePath,
                            LineNumber = i + 1,
                            EndLineNumber = i + 2,
                            RefactoringType = "Introduce Parameter Object",
                            Title = $"Long Parameter List: {methodName}",
                            Description = $"Method '{methodName}' has {paramCount} parameters. Consider using a parameter object.",
                            CurrentCode = snippet,
                            SuggestedCode = $"// Create parameter object:\npublic class {methodName}Parameters {{\n    // Group related parameters\n}}\n\n// Update method signature:\npublic void {methodName}({methodName}Parameters params)",
                            Reason = "Long parameter lists are hard to use and often indicate the method has too many responsibilities.",
                            Benefits = "Clearer API, easier to extend, better encapsulation, reduced coupling.",
                            Priority = paramCount > 6 ? SeverityLevel.High : SeverityLevel.Medium,
                            ImprovementAreas = new List<string> { "API Design", "Maintainability", "Usability" }
                        });
                    }
                }
            }
        }

        return refactorings.OrderByDescending(r => r.Priority)
                          .ThenBy(r => r.FilePath)
                          .ToList();
    }

    private string GetMethodSnippet(string[] lines, int startLine, int length)
    {
        var endLine = Math.Min(startLine + Math.Min(length, 15), lines.Length);
        var snippet = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine));
        return snippet.Length > 500 ? snippet.Substring(0, 500) + "\n..." : snippet;
    }

    private string GetContextSnippet(string[] lines, int centerLine, int contextSize)
    {
        var start = Math.Max(0, centerLine - contextSize / 2);
        var end = Math.Min(lines.Length, centerLine + contextSize / 2 + 1);
        return string.Join("\n", lines.Skip(start).Take(end - start));
    }

    private string ExtractMethodNameFromLine(string line)
    {
        // Try to extract method name from various patterns
        var patterns = new[]
        {
            @"(?:public|private|protected|internal)?\s*(?:static)?\s*(?:async)?\s*\w+\s+(\w+)\s*\(",
            @"function\s+(\w+)\s*\(",
            @"const\s+(\w+)\s*=\s*(?:async)?\s*\(",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, pattern);
            if (match.Success && match.Groups.Count > 1)
                return match.Groups[1].Value;
        }

        return "UnknownMethod";
    }

    private string GenerateConstantName(string line, string number)
    {
        // Try to infer constant name from context
        if (line.Contains("max") || line.Contains("Max"))
            return $"MAX_VALUE_{number}";
        if (line.Contains("min") || line.Contains("Min"))
            return $"MIN_VALUE_{number}";
        if (line.Contains("size") || line.Contains("Size"))
            return $"DEFAULT_SIZE_{number}";
        if (line.Contains("limit") || line.Contains("Limit"))
            return $"LIMIT_{number}";
        if (line.Contains("count") || line.Contains("Count"))
            return $"DEFAULT_COUNT_{number}";
        if (line.Contains("timeout") || line.Contains("Timeout"))
            return $"TIMEOUT_MS_{number}";

        return $"CONSTANT_{number}";
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

        if (files.Count < 2) return duplications;

        // Filter out SQL files - only process UI and API files
        var filteredFiles = files.Where(f =>
            !f.FilePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) &&
            f.FileType != FileType.SQL &&
            f.Content.Length > 100
        ).ToList();

        if (filteredFiles.Count < 2) return duplications;

        // Find genuine code duplications
        const int minBlockSize = 5; // Minimum lines for a code block
        const int minSimilarity = 70; // Minimum similarity percentage

        // Create code blocks dictionary: normalized code -> (file, start line, end line, original code)
        var codeBlocks = new Dictionary<string, List<(CodeFile file, int startLine, int endLine, string originalCode)>>();

        foreach (var file in filteredFiles)
        {
            var lines = file.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            // Extract code blocks of varying sizes
            for (int blockSize = minBlockSize; blockSize <= Math.Min(20, lines.Length); blockSize++)
            {
                for (int i = 0; i <= lines.Length - blockSize; i++)
                {
                    var block = lines.Skip(i).Take(blockSize).ToArray();
                    var originalCode = string.Join("\n", block);

                    // Normalize code for comparison (remove comments, extra whitespace)
                    var normalized = NormalizeCode(originalCode);

                    // Skip if block is too small after normalization or is mostly comments
                    if (normalized.Length < 50 || string.IsNullOrWhiteSpace(normalized)) continue;

                    if (!codeBlocks.ContainsKey(normalized))
                    {
                        codeBlocks[normalized] = new List<(CodeFile, int, int, string)>();
                    }

                    codeBlocks[normalized].Add((file, i + 1, i + blockSize, originalCode));
                }
            }
        }

        // Find duplicates - blocks that appear in multiple locations
        var duplicateBlocks = codeBlocks
            .Where(kvp => kvp.Value.Count > 1)
            .Where(kvp => kvp.Value.Select(v => v.file.FilePath).Distinct().Count() > 1) // Must be in different files
            .OrderByDescending(kvp => kvp.Key.Length) // Prioritize longer duplications
            .ToList();

        // Track already reported duplications to avoid overlapping reports
        var reportedLocations = new HashSet<string>();

        foreach (var duplicateBlock in duplicateBlocks.Take(50)) // Limit to top 50
        {
            var locations = duplicateBlock.Value;
            var normalizedCode = duplicateBlock.Key;

            // Check if any location already reported
            var locationKey = string.Join("|", locations.Select(l => $"{l.file.FilePath}:{l.startLine}"));
            if (reportedLocations.Contains(locationKey)) continue;

            // Get the original code from first occurrence
            var originalCode = locations.First().originalCode;
            var lineCount = originalCode.Split('\n').Length;

            // Calculate average similarity
            var similarities = new List<double>();
            for (int i = 0; i < locations.Count - 1; i++)
            {
                for (int j = i + 1; j < locations.Count; j++)
                {
                    var sim = CalculateSimilarity(locations[i].originalCode, locations[j].originalCode);
                    similarities.Add(sim);
                }
            }
            var avgSimilarity = similarities.Any() ? similarities.Average() : 100;

            if (avgSimilarity < minSimilarity) continue;

            // Determine duplication type based on similarity
            var duplicationType = avgSimilarity >= 95 ? DuplicationType.ExactMatch :
                                  avgSimilarity >= 85 ? DuplicationType.StructuralMatch :
                                  avgSimilarity >= 75 ? DuplicationType.LogicalMatch :
                                  DuplicationType.PartialMatch;

            // Determine impact based on line count and number of duplications
            var impact = lineCount > 50 || locations.Count > 5 ? SeverityLevel.High :
                         lineCount > 20 || locations.Count > 3 ? SeverityLevel.Medium :
                         SeverityLevel.Low;

            // Extract method names from code if possible
            var duplicationLocations = locations.Select(loc => new DuplicationLocation
            {
                FilePath = loc.file.FilePath,
                StartLine = loc.startLine,
                EndLine = loc.endLine,
                MethodName = ExtractMethodName(loc.originalCode),
                ClassName = ExtractClassName(loc.file.FilePath)
            }).ToList();

            duplications.Add(new CodeDuplication
            {
                DuplicatedCode = originalCode.Trim(),
                Locations = duplicationLocations,
                Type = duplicationType,
                LineCount = lineCount,
                SimilarityPercentage = Math.Round(avgSimilarity, 1),
                Description = $"Code duplication found in {locations.Count} locations across {locations.Select(l => l.file.FilePath).Distinct().Count()} files",
                Suggestion = GenerateSuggestion(originalCode, lineCount, locations.Count),
                Impact = impact,
                RefactoringOptions = GenerateRefactoringOptions(originalCode, lineCount),
                EstimatedEffort = lineCount > 30 ? "Medium" : "Low"
            });

            reportedLocations.Add(locationKey);
        }

        return duplications.OrderByDescending(d => d.Impact)
                          .ThenByDescending(d => d.LineCount)
                          .ToList();
    }

    private string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";

        // Remove single-line comments
        var lines = code.Split('\n');
        var normalized = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.StartsWith("//") ||
                trimmed.StartsWith("/*") ||
                trimmed.StartsWith("*") ||
                trimmed.StartsWith("*/") ||
                trimmed.StartsWith("#") ||
                trimmed.StartsWith("<!--"))
                continue;

            // Remove inline comments
            var codeOnly = trimmed;
            var commentIndex = codeOnly.IndexOf("//");
            if (commentIndex >= 0)
                codeOnly = codeOnly.Substring(0, commentIndex).Trim();

            // Normalize whitespace
            codeOnly = System.Text.RegularExpressions.Regex.Replace(codeOnly, @"\s+", " ");

            if (!string.IsNullOrWhiteSpace(codeOnly))
                normalized.AppendLine(codeOnly);
        }

        return normalized.ToString().Trim();
    }

    private double CalculateSimilarity(string code1, string code2)
    {
        if (string.IsNullOrEmpty(code1) || string.IsNullOrEmpty(code2)) return 0;

        var norm1 = NormalizeCode(code1);
        var norm2 = NormalizeCode(code2);

        if (norm1 == norm2) return 100;

        // Levenshtein distance based similarity
        var maxLength = Math.Max(norm1.Length, norm2.Length);
        if (maxLength == 0) return 100;

        var distance = LevenshteinDistance(norm1, norm2);
        return Math.Max(0, (1.0 - (double)distance / maxLength) * 100);
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var len1 = s1.Length;
        var len2 = s2.Length;
        var matrix = new int[len1 + 1, len2 + 1];

        for (int i = 0; i <= len1; i++) matrix[i, 0] = i;
        for (int j = 0; j <= len2; j++) matrix[0, j] = j;

        for (int i = 1; i <= len1; i++)
        {
            for (int j = 1; j <= len2; j++)
            {
                var cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                matrix[i, j] = Math.Min(Math.Min(
                    matrix[i - 1, j] + 1,
                    matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[len1, len2];
    }

    private string ExtractMethodName(string code)
    {
        var patterns = new[]
        {
            @"(public|private|protected|internal)?\s*(async\s+)?(void|Task|string|int|bool|var)\s+(\w+)\s*\(",
            @"function\s+(\w+)\s*\(",
            @"const\s+(\w+)\s*=\s*\(",
            @"(\w+)\s*\(\s*\)\s*=>",
            @"def\s+(\w+)\s*\("
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(code, pattern);
            if (match.Success)
            {
                var groups = match.Groups;
                return groups[groups.Count - 1].Value;
            }
        }

        return "Unknown";
    }

    private string GenerateSuggestion(string code, int lineCount, int occurrences)
    {
        if (code.Contains("service") || code.Contains("Service"))
            return $"Extract this {lineCount}-line code block into a shared service method to eliminate {occurrences} duplications";
        if (code.Contains("component") || code.Contains("Component"))
            return $"Create a reusable component to replace these {occurrences} duplicate implementations";
        if (code.Contains("valid") || code.Contains("check"))
            return $"Consolidate validation logic into a shared utility function ({occurrences} duplications found)";
        if (code.Contains("async") || code.Contains("await"))
            return $"Extract async operation into a base class or utility method ({occurrences} duplications detected)";

        return $"Extract this {lineCount}-line duplicate code into a shared utility method to eliminate {occurrences} occurrences";
    }

    private List<string> GenerateRefactoringOptions(string code, int lineCount)
    {
        var options = new List<string>();

        if (code.Contains("class") || code.Contains("public"))
            options.Add("Extract to base class");

        options.Add("Create utility method");

        if (code.Contains("async") || code.Contains("await"))
            options.Add("Extract to async service");

        if (lineCount > 15)
            options.Add("Consider design pattern (Template Method/Strategy)");
        else
            options.Add("Extract to helper function");

        return options;
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

        // Analyze each file for potential violations with language-specific detection
        foreach (var file in files.Where(f => f.FileType != FileType.Markdown && f.Content.Length > 50))
        {
            var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            var fileContent = file.Content;
            var fileContentLower = fileContent.ToLower();
            var lines = fileContent.Split('\n');

            // Language-specific violation detection - Supporting 25+ programming languages
            violations.AddRange(extension switch
            {
                // C-family languages
                ".cs" => DetectCSharpViolations(file, fileContent, fileContentLower, lines, random),
                ".c" or ".h" => DetectCViolations(file, fileContent, fileContentLower, lines, random),
                ".cpp" or ".hpp" or ".cc" or ".cxx" or ".h++" => DetectCppViolations(file, fileContent, fileContentLower, lines, random),
                ".m" or ".mm" => DetectObjectiveCViolations(file, fileContent, fileContentLower, lines, random),

                // JVM languages
                ".java" => DetectJavaViolations(file, fileContent, fileContentLower, lines, random),
                ".kt" or ".kts" => DetectKotlinViolations(file, fileContent, fileContentLower, lines, random),
                ".scala" => DetectScalaViolations(file, fileContent, fileContentLower, lines, random),
                ".groovy" => DetectGroovyViolations(file, fileContent, fileContentLower, lines, random),
                ".clj" or ".cljs" => DetectClojureViolations(file, fileContent, fileContentLower, lines, random),

                // JavaScript/TypeScript ecosystem
                ".js" or ".mjs" or ".cjs" => DetectJavaScriptViolations(file, fileContent, fileContentLower, lines, random),
                ".ts" or ".mts" or ".cts" => DetectTypeScriptViolations(file, fileContent, fileContentLower, lines, random),
                ".jsx" => DetectReactJSXViolations(file, fileContent, fileContentLower, lines, random),
                ".tsx" => DetectReactTSXViolations(file, fileContent, fileContentLower, lines, random),
                ".vue" => DetectVueViolations(file, fileContent, fileContentLower, lines, random),

                // Python
                ".py" or ".pyw" or ".pyi" => DetectPythonViolations(file, fileContent, fileContentLower, lines, random),

                // Web languages
                ".php" => DetectPHPViolations(file, fileContent, fileContentLower, lines, random),
                ".rb" or ".rake" => DetectRubyViolations(file, fileContent, fileContentLower, lines, random),

                // Systems languages
                ".go" => DetectGoViolations(file, fileContent, fileContentLower, lines, random),
                ".rs" => DetectRustViolations(file, fileContent, fileContentLower, lines, random),
                ".swift" => DetectSwiftViolations(file, fileContent, fileContentLower, lines, random),

                // Functional languages
                ".fs" or ".fsx" or ".fsi" => DetectFSharpViolations(file, fileContent, fileContentLower, lines, random),
                ".hs" or ".lhs" => DetectHaskellViolations(file, fileContent, fileContentLower, lines, random),
                ".ex" or ".exs" => DetectElixirViolations(file, fileContent, fileContentLower, lines, random),
                ".erl" or ".hrl" => DetectErlangViolations(file, fileContent, fileContentLower, lines, random),

                // Mobile
                ".dart" => DetectDartViolations(file, fileContent, fileContentLower, lines, random),

                // Scripting languages
                ".sh" or ".bash" or ".zsh" => DetectShellViolations(file, fileContent, fileContentLower, lines, random),
                ".ps1" or ".psm1" or ".psd1" => DetectPowerShellViolations(file, fileContent, fileContentLower, lines, random),
                ".lua" => DetectLuaViolations(file, fileContent, fileContentLower, lines, random),
                ".perl" or ".pl" or ".pm" => DetectPerlViolations(file, fileContent, fileContentLower, lines, random),

                // Data science & scientific
                ".r" or ".R" => DetectRViolations(file, fileContent, fileContentLower, lines, random),
                ".jl" => DetectJuliaViolations(file, fileContent, fileContentLower, lines, random),

                // Database
                ".sql" => DetectSQLViolations(file, fileContent, fileContentLower, lines, random),

                // .NET
                ".vb" => DetectVBNetViolations(file, fileContent, fileContentLower, lines, random),

                // Blockchain
                ".sol" => DetectSolidityViolations(file, fileContent, fileContentLower, lines, random),

                // Markup with logic
                ".xml" or ".xaml" => DetectXMLViolations(file, fileContent, fileContentLower, lines, random),
                ".yaml" or ".yml" => DetectYAMLViolations(file, fileContent, fileContentLower, lines, random),

                _ => new List<Violation>()
            });
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

    #region Language-Specific Violation Detection

    private List<Violation> DetectCSharpViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing async/await error handling
        if (contentLower.Contains("async") && contentLower.Contains("await") && !contentLower.Contains("try"))
        {
            var lineNum = FindLineContaining(lines, "await", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Async Error Handling",
                Description = "Async methods should have proper try-catch blocks to handle exceptions",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Wrap await operations in try-catch: try { await operation(); } catch (Exception ex) { _logger.LogError(ex, \"Error\"); throw; }"
            });
        }

        // 2. Missing null checks (not using null-conditional operator)
        if (content.Contains(".") && !contentLower.Contains("?.") && (contentLower.Contains("var ") || contentLower.Contains("return ")))
        {
            var lineNum = FindLineContaining(lines, ".", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Null Safety",
                Description = "Object property access without null checking can cause NullReferenceException",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use null-conditional operator: object?.Property or add explicit null check: if (object != null) { ... }"
            });
        }

        // 3. Missing XML documentation
        if ((contentLower.Contains("public class") || contentLower.Contains("public interface") || contentLower.Contains("public async")) &&
            !content.Contains("///"))
        {
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Documentation Standard",
                Description = "Public classes, interfaces, and methods should have XML documentation comments",
                Type = ViolationType.Documentation,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Add XML documentation: /// <summary>Description of class/method</summary>"
            });
        }

        // 4. Hardcoded configuration values
        if (contentLower.Contains("connectionstring") || contentLower.Contains("\"localhost\"") ||
            contentLower.Contains("\"password\"") || contentLower.Contains("\"apikey\""))
        {
            var lineNum = FindLineContaining(lines, "localhost|password|apikey|connectionstring", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Configuration Management",
                Description = "Hardcoded credentials or connection strings pose security risks",
                Type = ViolationType.Security,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Move to appsettings.json and use IConfiguration: var connString = _configuration[\"ConnectionStrings:Default\"];"
            });
        }

        // 5. Missing IDisposable pattern
        if ((contentLower.Contains("new stream") || contentLower.Contains("new sqlconnection") || contentLower.Contains("new httpclient")) &&
            !contentLower.Contains("using"))
        {
            var lineNum = FindLineContaining(lines, "new ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Resource Management",
                Description = "IDisposable resources should be wrapped in using statements to prevent memory leaks",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use using statement: using (var resource = new Resource()) { ... } or using var resource = new Resource();"
            });
        }

        // 6. String concatenation in loops
        if (contentLower.Contains("for") && contentLower.Contains("+=") && contentLower.Contains("string"))
        {
            var lineNum = FindLineContaining(lines, "for|foreach", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Performance Optimization",
                Description = "String concatenation in loops creates multiple string objects, impacting performance",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use StringBuilder: var sb = new StringBuilder(); foreach(...) { sb.Append(value); } return sb.ToString();"
            });
        }

        return violations;
    }

    private List<Violation> DetectSQLViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing error handling
        if (!contentLower.Contains("begin try") && !contentLower.Contains("begin catch") && content.Length > 200)
        {
            var lineNum = random.Next(1, Math.Max(2, lines.Length));
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "SQL Error Handling",
                Description = "Stored procedures should include TRY-CATCH blocks for robust error handling",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Add error handling: BEGIN TRY ... END TRY BEGIN CATCH SELECT ERROR_MESSAGE(); THROW; END CATCH"
            });
        }

        // 2. Missing SET NOCOUNT ON
        if (contentLower.Contains("procedure") && !contentLower.Contains("set nocount on"))
        {
            var lineNum = FindLineContaining(lines, "PROCEDURE", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "SQL Performance",
                Description = "Missing SET NOCOUNT ON can impact performance by sending unnecessary messages",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Add at the beginning of stored procedure: SET NOCOUNT ON;"
            });
        }

        // 3. SQL Injection risk from dynamic SQL
        if (contentLower.Contains("exec(") || contentLower.Contains("execute(") ||
            (contentLower.Contains("@sql") && contentLower.Contains("+")))
        {
            var lineNum = FindLineContaining(lines, "EXEC|EXECUTE|@sql", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "SQL Injection Prevention",
                Description = "Dynamic SQL with string concatenation is vulnerable to SQL injection attacks",
                Type = ViolationType.Security,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use sp_executesql with parameters: EXEC sp_executesql @sql, N'@Param INT', @Param = @Value"
            });
        }

        // 4. Missing transaction handling for multi-statement operations
        if (contentLower.Contains("insert") && contentLower.Contains("update") && !contentLower.Contains("begin transaction"))
        {
            var lineNum = FindLineContaining(lines, "INSERT|UPDATE", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Transaction Management",
                Description = "Multiple DML operations should be wrapped in explicit transactions for data consistency",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use explicit transaction: BEGIN TRANSACTION; ... COMMIT TRANSACTION; (with error handling)"
            });
        }

        // 5. SELECT * usage
        if (contentLower.Contains("select *"))
        {
            var lineNum = FindLineContaining(lines, "SELECT \\*", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "SQL Best Practice",
                Description = "SELECT * should be avoided; explicitly specify required columns for better performance",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Specify columns explicitly: SELECT Column1, Column2, Column3 FROM Table"
            });
        }

        return violations;
    }

    private List<Violation> DetectJavaScriptViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using var instead of let/const
        if (contentLower.Contains("var "))
        {
            var lineNum = FindLineContaining(lines, "var ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Modern JavaScript Standards",
                Description = "Use 'let' or 'const' instead of 'var' for better scoping and immutability",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Replace var with const (for immutable) or let (for mutable): const value = ...; or let counter = ...;"
            });
        }

        // 2. Missing error handling in promises
        if (contentLower.Contains(".then(") && !contentLower.Contains(".catch("))
        {
            var lineNum = FindLineContaining(lines, ".then\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Promise Error Handling",
                Description = "Promise chains should include .catch() to handle rejection errors",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Add error handler: promise.then(result => {...}).catch(error => console.error(error));"
            });
        }

        // 3. Console.log in production code
        if (contentLower.Contains("console.log"))
        {
            var lineNum = FindLineContaining(lines, "console\\.log", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Production Code Quality",
                Description = "console.log statements should be removed from production code",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Use proper logging library or remove console.log statements before production deployment"
            });
        }

        // 4. == instead of ===
        if (content.Contains("==") && !content.Contains("==="))
        {
            var lineNum = FindLineContaining(lines, "==", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Type Safety",
                Description = "Use strict equality (===) instead of loose equality (==) to avoid type coercion issues",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use === for equality: if (value === expected) instead of if (value == expected)"
            });
        }

        return violations;
    }

    private List<Violation> DetectTypeScriptViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // Start with JavaScript violations
        violations.AddRange(DetectJavaScriptViolations(file, content, contentLower, lines, random));

        // 1. Missing type annotations
        if ((contentLower.Contains("function ") || contentLower.Contains("const ") || contentLower.Contains("let ")) &&
            !content.Contains(":") && !content.Contains("<"))
        {
            var lineNum = FindLineContaining(lines, "function|const |let ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "TypeScript Type Safety",
                Description = "Add explicit type annotations for better type safety and code documentation",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Add type annotations: const value: string = ...; function method(param: number): boolean { ... }"
            });
        }

        // 2. Using 'any' type
        if (contentLower.Contains(": any"))
        {
            var lineNum = FindLineContaining(lines, ": any", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "TypeScript Type Safety",
                Description = "Avoid using 'any' type as it defeats TypeScript's type checking benefits",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use specific types or 'unknown' instead: const value: SpecificType = ... or const value: unknown = ..."
            });
        }

        // 3. Missing null/undefined checks
        if (!content.Contains("?") && !content.Contains("!") && (contentLower.Contains("return ") || contentLower.Contains("const ")))
        {
            var lineNum = FindLineContaining(lines, "return|const", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Null Safety",
                Description = "Consider using optional chaining (?.) or nullish coalescing (??) for null safety",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use optional chaining: object?.property or nullish coalescing: value ?? defaultValue"
            });
        }

        return violations;
    }

    private List<Violation> DetectReactJSXViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // Start with JavaScript violations
        violations.AddRange(DetectJavaScriptViolations(file, content, contentLower, lines, random));

        // 1. Missing key prop in lists
        if (contentLower.Contains(".map(") && !content.Contains("key="))
        {
            var lineNum = FindLineContaining(lines, "\\.map\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "React Best Practices",
                Description = "Lists rendered with .map() should include a unique 'key' prop for optimal performance",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Add key prop: {items.map(item => <Component key={item.id} {...item} />)}"
            });
        }

        // 2. Inline function definitions in JSX
        if (contentLower.Contains("onclick={() =>") || contentLower.Contains("onchange={() =>"))
        {
            var lineNum = FindLineContaining(lines, "onClick=\\{\\(\\)|onChange=\\{\\(\\)", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "React Performance",
                Description = "Inline arrow functions in JSX cause unnecessary re-renders",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Define handler outside JSX: const handleClick = () => {...}; then use onClick={handleClick}"
            });
        }

        // 3. Direct state mutation
        if (contentLower.Contains("state.") && content.Contains("=") && !contentLower.Contains("setstate"))
        {
            var lineNum = FindLineContaining(lines, "state\\.", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "React State Management",
                Description = "Never mutate state directly; use setState or state updater functions",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use setState: this.setState({...}) or useState updater: setValue(newValue)"
            });
        }

        return violations;
    }

    private List<Violation> DetectReactTSXViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // Combine React and TypeScript violations
        violations.AddRange(DetectReactJSXViolations(file, content, contentLower, lines, random));

        // TSX-specific: Missing prop types interface
        if ((contentLower.Contains("function ") || contentLower.Contains("const ")) &&
            content.Contains("props") && !content.Contains("interface") && !content.Contains("type "))
        {
            var lineNum = FindLineContaining(lines, "function|const", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "React TypeScript Types",
                Description = "Component props should have explicit TypeScript interface or type definitions",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Define props interface: interface MyComponentProps { prop1: string; prop2: number; } then use: React.FC<MyComponentProps>"
            });
        }

        return violations;
    }

    private List<Violation> DetectPythonViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing type hints
        if (contentLower.Contains("def ") && !content.Contains("->") && !content.Contains(":"))
        {
            var lineNum = FindLineContaining(lines, "def ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Python Type Hints (PEP 484)",
                Description = "Functions should include type hints for parameters and return values",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Add type hints: def function(param: str, count: int) -> bool:"
            });
        }

        // 2. Bare except clause
        if (contentLower.Contains("except:") && !contentLower.Contains("except "))
        {
            var lineNum = FindLineContaining(lines, "except:", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Exception Handling",
                Description = "Bare except clauses catch all exceptions, including system exits and keyboard interrupts",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Specify exception type: except ValueError: or except Exception as e:"
            });
        }

        // 3. Mutable default arguments
        if (contentLower.Contains("def ") && (content.Contains("=[]") || content.Contains("={}")))
        {
            var lineNum = FindLineContaining(lines, "=\\[\\]|=\\{\\}", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Mutable Default Arguments",
                Description = "Mutable default arguments are shared across function calls, causing unexpected behavior",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use None as default: def func(items=None): items = items if items is not None else []"
            });
        }

        // 4. Missing docstrings
        if (contentLower.Contains("def ") && !content.Contains("\"\"\"") && !content.Contains("'''"))
        {
            var lineNum = FindLineContaining(lines, "def ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Python Documentation (PEP 257)",
                Description = "Public functions and classes should have docstrings describing their behavior",
                Type = ViolationType.Documentation,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Add docstring: def func(): \"\"\"Brief description of function.\"\"\""
            });
        }

        return violations;
    }

    private List<Violation> DetectJavaViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing @Override annotation
        if (contentLower.Contains("public ") && contentLower.Contains("equals") && !content.Contains("@Override"))
        {
            var lineNum = FindLineContaining(lines, "equals|toString|hashCode", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Java Override Annotation",
                Description = "Methods overriding superclass methods should use @Override annotation",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Add @Override annotation above method: @Override public boolean equals(Object obj) { ... }"
            });
        }

        // 2. Missing try-with-resources
        if ((contentLower.Contains("new fileinputstream") || contentLower.Contains("new bufferedreader")) &&
            !contentLower.Contains("try ("))
        {
            var lineNum = FindLineContaining(lines, "new File|new Buffered|new Input|new Output", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Resource Management",
                Description = "AutoCloseable resources should use try-with-resources to ensure proper closure",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use try-with-resources: try (BufferedReader reader = new BufferedReader(...)) { ... }"
            });
        }

        // 3. Missing JavaDoc
        if ((contentLower.Contains("public class") || contentLower.Contains("public interface")) &&
            !content.Contains("/**"))
        {
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Java Documentation",
                Description = "Public classes and interfaces should have JavaDoc comments",
                Type = ViolationType.Documentation,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Add JavaDoc: /** * Description of class/interface * @author Name */"
            });
        }

        // 4. String concatenation in loops
        if (contentLower.Contains("for") && content.Contains("+=") && contentLower.Contains("string"))
        {
            var lineNum = FindLineContaining(lines, "for\\(|for ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Performance Optimization",
                Description = "String concatenation in loops creates unnecessary String objects",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use StringBuilder: StringBuilder sb = new StringBuilder(); for(...) { sb.append(value); } return sb.toString();"
            });
        }

        // 5. Catching generic Exception
        if (contentLower.Contains("catch (exception"))
        {
            var lineNum = FindLineContaining(lines, "catch \\(Exception", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Exception Handling Best Practice",
                Description = "Avoid catching generic Exception; catch specific exception types",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Catch specific exceptions: catch (IOException | SQLException ex) { ... }"
            });
        }

        return violations;
    }

    // ==================== ADDITIONAL LANGUAGE SUPPORT ====================
    // Supporting 20+ additional programming languages

    #region C/C++ and Objective-C

    private List<Violation> DetectCViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Buffer overflow risk
        if (contentLower.Contains("gets(") || contentLower.Contains("strcpy(") || contentLower.Contains("sprintf("))
        {
            var lineNum = FindLineContaining(lines, "gets\\(|strcpy\\(|sprintf\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Buffer Overflow Prevention",
                Description = "Unsafe C functions can cause buffer overflows and security vulnerabilities",
                Type = ViolationType.Security,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use safe alternatives: fgets() instead of gets(), strncpy() instead of strcpy(), snprintf() instead of sprintf()"
            });
        }

        // 2. Missing NULL check after malloc
        if (contentLower.Contains("malloc(") && !contentLower.Contains("if") && !contentLower.Contains("null"))
        {
            var lineNum = FindLineContaining(lines, "malloc\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Memory Allocation Check",
                Description = "malloc() can return NULL on failure; always check the return value",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Check for NULL: ptr = malloc(size); if (ptr == NULL) { /* handle error */ }"
            });
        }

        // 3. Memory leak - missing free()
        if (contentLower.Contains("malloc(") && !contentLower.Contains("free("))
        {
            var lineNum = FindLineContaining(lines, "malloc\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Memory Management",
                Description = "Allocated memory must be freed to prevent memory leaks",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Call free() when done: free(ptr); ptr = NULL;"
            });
        }

        return violations;
    }

    private List<Violation> DetectCppViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // Include C violations
        violations.AddRange(DetectCViolations(file, content, contentLower, lines, random));

        // 1. Using new without delete
        if (contentLower.Contains("new ") && !contentLower.Contains("delete"))
        {
            var lineNum = FindLineContaining(lines, "new ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Resource Management - RAII",
                Description = "Raw pointers with new/delete should be avoided; use smart pointers",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use smart pointers: std::unique_ptr<Type> ptr = std::make_unique<Type>(); or std::shared_ptr"
            });
        }

        // 2. Using NULL instead of nullptr
        if (content.Contains("NULL") && !content.Contains("nullptr"))
        {
            var lineNum = FindLineContaining(lines, "NULL", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Modern C++ Standards",
                Description = "Use nullptr instead of NULL for better type safety in C++11 and later",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Replace NULL with nullptr: if (ptr == nullptr) { ... }"
            });
        }

        // 3. Missing virtual destructor in base class
        if ((contentLower.Contains("class") && contentLower.Contains("virtual")) && !contentLower.Contains("virtual ~"))
        {
            var lineNum = FindLineContaining(lines, "class|virtual", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Polymorphism Best Practice",
                Description = "Classes with virtual functions should have virtual destructors",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Add virtual destructor: virtual ~ClassName() = default;"
            });
        }

        return violations;
    }

    private List<Violation> DetectObjectiveCViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing @property attributes
        if (contentLower.Contains("@property") && !contentLower.Contains("nonatomic") && !contentLower.Contains("atomic"))
        {
            var lineNum = FindLineContaining(lines, "@property", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Property Declaration",
                Description = "Properties should explicitly specify atomicity (atomic/nonatomic)",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "@property (nonatomic, strong) NSString *name; or @property (atomic, strong) NSString *name;"
            });
        }

        // 2. Potential retain cycle
        if (contentLower.Contains("block") && contentLower.Contains("self") && !contentLower.Contains("weak"))
        {
            var lineNum = FindLineContaining(lines, "self", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Retain Cycle Prevention",
                Description = "Using 'self' in blocks can cause retain cycles",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use weak reference: __weak typeof(self) weakSelf = self; then use weakSelf in block"
            });
        }

        return violations;
    }

    #endregion

    #region JVM Languages (Kotlin, Scala, Groovy, Clojure)

    private List<Violation> DetectKotlinViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using !! (not-null assertion) operator
        if (content.Contains("!!"))
        {
            var lineNum = FindLineContaining(lines, "!!", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Null Safety",
                Description = "The !! operator defeats Kotlin's null safety; use safe calls instead",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use safe call operator: value?.method() or elvis operator: value ?: defaultValue"
            });
        }

        // 2. Mutable collections when immutable would work
        if (contentLower.Contains("var ") && (contentLower.Contains("mutablelistof") || contentLower.Contains("mutablemapof")))
        {
            var lineNum = FindLineContaining(lines, "mutableListOf|mutableMapOf", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Immutability Best Practice",
                Description = "Prefer immutable collections for better thread safety and predictability",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use immutable: val list = listOf(...) instead of var list = mutableListOf(...)"
            });
        }

        return violations;
    }

    private List<Violation> DetectScalaViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using null instead of Option
        if (content.Contains("null"))
        {
            var lineNum = FindLineContaining(lines, "null", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Functional Programming Best Practice",
                Description = "In Scala, use Option[T] instead of null for better type safety",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use Option: def findUser(id: Int): Option[User] instead of returning null"
            });
        }

        // 2. Using var when val would work
        if (contentLower.Contains("var "))
        {
            var lineNum = FindLineContaining(lines, "var ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Immutability Principle",
                Description = "Prefer val (immutable) over var (mutable) for functional programming",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Use val for immutable: val count = 10 instead of var count = 10"
            });
        }

        return violations;
    }

    private List<Violation> DetectGroovyViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing @CompileStatic for performance
        if ((contentLower.Contains("class") || contentLower.Contains("def")) && !contentLower.Contains("@compilestatic"))
        {
            var lineNum = FindLineContaining(lines, "class|def", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Performance Optimization",
                Description = "Use @CompileStatic for better performance in Groovy",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Add annotation: @groovy.transform.CompileStatic class MyClass { ... }"
            });
        }

        return violations;
    }

    private List<Violation> DetectClojureViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using mutable state unnecessarily
        if (contentLower.Contains("atom") || contentLower.Contains("ref"))
        {
            var lineNum = FindLineContaining(lines, "atom|ref", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Functional Programming",
                Description = "Minimize use of mutable state; prefer pure functions and immutable data",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Consider using pure functions with immutable data structures when possible"
            });
        }

        return violations;
    }

    #endregion

    #region Web Languages (PHP, Ruby, Vue)

    private List<Violation> DetectPHPViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. SQL Injection via string concatenation
        if ((contentLower.Contains("select") || contentLower.Contains("insert")) && content.Contains("$"))
        {
            var lineNum = FindLineContaining(lines, "SELECT|INSERT|\\$", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "SQL Injection Prevention",
                Description = "Direct variable interpolation in SQL queries is vulnerable to SQL injection",
                Type = ViolationType.Security,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use prepared statements: $stmt = $pdo->prepare('SELECT * FROM users WHERE id = :id'); $stmt->execute(['id' => $id]);"
            });
        }

        // 2. Using deprecated mysql_ functions
        if (contentLower.Contains("mysql_"))
        {
            var lineNum = FindLineContaining(lines, "mysql_", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Deprecated Functions",
                Description = "mysql_ functions are deprecated; use mysqli or PDO",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use PDO: $pdo = new PDO('mysql:host=localhost;dbname=test', $user, $pass);"
            });
        }

        // 3. Missing type declarations (PHP 7+)
        if (contentLower.Contains("function") && !content.Contains(":"))
        {
            var lineNum = FindLineContaining(lines, "function", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Type Safety (PHP 7+)",
                Description = "Use type declarations for better code safety and documentation",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Add type hints: function getUser(int $id): ?User { ... }"
            });
        }

        return violations;
    }

    private List<Violation> DetectRubyViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using unless with else (confusing)
        if (contentLower.Contains("unless") && contentLower.Contains("else"))
        {
            var lineNum = FindLineContaining(lines, "unless", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Code Readability",
                Description = "'unless' with 'else' is confusing; use 'if' instead",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use if: if condition then ... else ... end"
            });
        }

        // 2. Mixing string interpolation styles
        if (content.Contains("#{") && content.Contains("+"))
        {
            var lineNum = FindLineContaining(lines, "#\\{", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Consistent String Handling",
                Description = "Prefer string interpolation over concatenation in Ruby",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Use interpolation: \"Hello #{name}\" instead of \"Hello \" + name"
            });
        }

        return violations;
    }

    private List<Violation> DetectVueViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing key in v-for
        if (contentLower.Contains("v-for") && !content.Contains(":key"))
        {
            var lineNum = FindLineContaining(lines, "v-for", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Vue.js Best Practices",
                Description = "v-for directives should always have a unique :key binding",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "<div v-for=\"item in items\" :key=\"item.id\">{{ item.name }}</div>"
            });
        }

        // 2. Mutating props directly
        if (contentLower.Contains("props") && content.Contains("this.") && content.Contains("="))
        {
            var lineNum = FindLineContaining(lines, "this\\.", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Vue Component Communication",
                Description = "Props should not be mutated directly; emit events to parent instead",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Emit event: this.$emit('update:propName', newValue); Parent handles update"
            });
        }

        return violations;
    }

    #endregion

    #region Systems Languages (Go, Rust, Swift)

    private List<Violation> DetectGoViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Ignoring error returns
        if (contentLower.Contains("err") && content.Contains("_"))
        {
            var lineNum = FindLineContaining(lines, "_, err|err :=", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Error Handling",
                Description = "Never ignore error returns in Go; always handle or explicitly ignore with comment",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Handle error: if err != nil { return err } or log.Printf(\"Error: %v\", err)"
            });
        }

        // 2. Not using defer for cleanup
        if ((contentLower.Contains("open(") || contentLower.Contains("create(")) && !contentLower.Contains("defer"))
        {
            var lineNum = FindLineContaining(lines, "Open\\(|Create\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Resource Management",
                Description = "Use defer to ensure resources are properly closed",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use defer: file, err := os.Open(name); if err == nil { defer file.Close() }"
            });
        }

        // 3. Goroutine without proper synchronization
        if (contentLower.Contains("go ") && !contentLower.Contains("sync") && !contentLower.Contains("chan"))
        {
            var lineNum = FindLineContaining(lines, "go ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Concurrency Safety",
                Description = "Goroutines should be synchronized with channels or sync primitives",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use WaitGroup: var wg sync.WaitGroup; wg.Add(1); go func() { defer wg.Done(); ... }(); wg.Wait()"
            });
        }

        return violations;
    }

    private List<Violation> DetectRustViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using unwrap() instead of proper error handling
        if (contentLower.Contains(".unwrap()"))
        {
            var lineNum = FindLineContaining(lines, "\\.unwrap\\(\\)", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Error Handling",
                Description = "unwrap() can panic; use proper error handling with ? operator or match",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use ? operator: let value = result?; or match: match result { Ok(v) => v, Err(e) => handle_error(e) }"
            });
        }

        // 2. Clone() when borrow would work
        if (contentLower.Contains(".clone()"))
        {
            var lineNum = FindLineContaining(lines, "\\.clone\\(\\)", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Performance - Ownership",
                Description = "Unnecessary cloning impacts performance; consider borrowing instead",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use borrowing: fn process(data: &Vec<String>) instead of fn process(data: Vec<String>)"
            });
        }

        return violations;
    }

    private List<Violation> DetectSwiftViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Force unwrapping optionals
        if (content.Contains("!") && !contentLower.Contains("guard") && !contentLower.Contains("if let"))
        {
            var lineNum = FindLineContaining(lines, "!", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Optional Safety",
                Description = "Force unwrapping with ! can crash; use optional binding or optional chaining",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use optional binding: if let value = optional { use value } or guard let value = optional else { return }"
            });
        }

        // 2. Strong reference cycle risk
        if (contentLower.Contains("[self") && !contentLower.Contains("weak") && !contentLower.Contains("unowned"))
        {
            var lineNum = FindLineContaining(lines, "\\[self", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Memory Management",
                Description = "Capturing self in closures can create retain cycles",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use weak self: { [weak self] in guard let self = self else { return }; self.method() }"
            });
        }

        return violations;
    }

    #endregion

    #region Functional Languages (F#, Haskell, Elixir, Erlang)

    private List<Violation> DetectFSharpViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using mutable when immutable would work
        if (contentLower.Contains("mutable"))
        {
            var lineNum = FindLineContaining(lines, "mutable", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Functional Programming",
                Description = "Prefer immutable values in F# for better composition and reasoning",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use immutable: let value = 10 instead of let mutable value = 10"
            });
        }

        // 2. Not using pattern matching
        if (contentLower.Contains("if") && contentLower.Contains("then") && !contentLower.Contains("match"))
        {
            var lineNum = FindLineContaining(lines, "if.*then", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Pattern Matching",
                Description = "Consider using pattern matching instead of if-then-else for exhaustiveness",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Use match: match value with | Some x -> ... | None -> ..."
            });
        }

        return violations;
    }

    private List<Violation> DetectHaskellViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Partial functions without handling
        if (contentLower.Contains("head") || contentLower.Contains("tail") || contentLower.Contains("init"))
        {
            var lineNum = FindLineContaining(lines, "head|tail|init", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Total Functions",
                Description = "Avoid partial functions that can fail on empty lists; use safe alternatives",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use safe functions: listToMaybe, headMay from safe package, or pattern match on list"
            });
        }

        return violations;
    }

    private List<Violation> DetectElixirViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Not using pattern matching in function heads
        if (contentLower.Contains("def") && contentLower.Contains("if") && !content.Contains("when"))
        {
            var lineNum = FindLineContaining(lines, "def.*if", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Pattern Matching",
                Description = "Use pattern matching in function heads instead of if inside function body",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use multiple function heads: def process([]), do: ... and def process([head | tail]), do: ..."
            });
        }

        return violations;
    }

    private List<Violation> DetectErlangViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Not handling all message types in receive
        if (contentLower.Contains("receive") && !content.Contains("after") && !content.Contains("_"))
        {
            var lineNum = FindLineContaining(lines, "receive", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Message Handling",
                Description = "receive blocks should handle all message types or use catch-all pattern",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Add catch-all: receive {expected, Data} -> handle(Data); _ -> ok end."
            });
        }

        return violations;
    }

    #endregion

    #region Mobile (Dart)

    private List<Violation> DetectDartViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Not using const constructors
        if (contentLower.Contains("statelesswidget") && !contentLower.Contains("const "))
        {
            var lineNum = FindLineContaining(lines, "StatelessWidget", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Flutter Performance",
                Description = "Use const constructors for stateless widgets to improve performance",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Add const: return const MyWidget(); This prevents unnecessary rebuilds"
            });
        }

        // 2. Missing null safety
        if (contentLower.Contains("?") && !content.Contains("!") && content.Contains("?."))
        {
            var lineNum = FindLineContaining(lines, "\\?\\.", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Null Safety (Dart 2.12+)",
                Description = "Leverage Dart's null safety features for better type safety",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Use null-aware operators: value?.method() or value ?? defaultValue"
            });
        }

        return violations;
    }

    #endregion

    #region Scripting Languages (Shell, PowerShell, Lua, Perl)

    private List<Violation> DetectShellViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Unquoted variables
        if (content.Contains("$") && !content.Contains("\"$"))
        {
            var lineNum = FindLineContaining(lines, "\\$[A-Za-z]", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Shell Best Practices",
                Description = "Always quote variables to prevent word splitting and globbing",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Quote variables: \"$variable\" instead of $variable"
            });
        }

        // 2. Not checking command exit status
        if (!contentLower.Contains("if") && !content.Contains("||") && !content.Contains("&&"))
        {
            var lineNum = random.Next(1, Math.Max(2, lines.Length));
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Error Handling",
                Description = "Check command exit status to handle failures properly",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Check status: if ! command; then echo \"Failed\"; exit 1; fi or use set -e"
            });
        }

        return violations;
    }

    private List<Violation> DetectPowerShellViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using aliases in scripts
        if (contentLower.Contains("ls") || contentLower.Contains("cd") || contentLower.Contains("rm"))
        {
            var lineNum = FindLineContaining(lines, "^ls |^cd |^rm ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "PowerShell Best Practices",
                Description = "Use full cmdlet names instead of aliases in scripts for clarity",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Use full names: Get-ChildItem instead of ls, Set-Location instead of cd, Remove-Item instead of rm"
            });
        }

        // 2. Missing error handling
        if (!contentLower.Contains("try") && !contentLower.Contains("$?") && !contentLower.Contains("erroraction"))
        {
            var lineNum = random.Next(1, Math.Max(2, lines.Length));
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Error Handling",
                Description = "PowerShell scripts should handle errors with Try-Catch or -ErrorAction",
                Type = ViolationType.ErrorHandling,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Use Try-Catch: try { command } catch { Write-Error $_.Exception.Message } or -ErrorAction Stop"
            });
        }

        return violations;
    }

    private List<Violation> DetectLuaViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Global variables without local
        if (!contentLower.Contains("local") && contentLower.Contains("="))
        {
            var lineNum = FindLineContaining(lines, "=", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Variable Scope",
                Description = "Prefer local variables over global for better encapsulation",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use local: local myVar = 10 instead of myVar = 10"
            });
        }

        return violations;
    }

    private List<Violation> DetectPerlViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Not using strict and warnings
        if (!contentLower.Contains("use strict") || !contentLower.Contains("use warnings"))
        {
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Perl Best Practices",
                Description = "Always use strict and warnings pragmas for better error detection",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Add at top of file: use strict; use warnings;"
            });
        }

        return violations;
    }

    #endregion

    #region Data Science (R, Julia)

    private List<Violation> DetectRViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using = instead of <-
        if (content.Contains(" = ") && !content.Contains("<-"))
        {
            var lineNum = FindLineContaining(lines, " = ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "R Style Guide",
                Description = "Use <- for assignment instead of = for better R idioms",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Use assignment operator: x <- 10 instead of x = 10"
            });
        }

        // 2. Not vectorizing operations
        if (contentLower.Contains("for") && contentLower.Contains("[i]"))
        {
            var lineNum = FindLineContaining(lines, "for.*\\[i\\]", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "R Performance",
                Description = "Vectorize operations instead of using loops for better performance",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use vectorized operations: result <- vector1 + vector2 instead of for loops"
            });
        }

        return violations;
    }

    private List<Violation> DetectJuliaViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Type instability
        if (contentLower.Contains("function") && !content.Contains("::"))
        {
            var lineNum = FindLineContaining(lines, "function", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Julia Performance",
                Description = "Add type annotations for better performance in Julia",
                Type = ViolationType.Performance,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Add types: function process(x::Float64, y::Int)::Float64 ... end"
            });
        }

        return violations;
    }

    #endregion

    #region .NET (VB.NET)

    private List<Violation> DetectVBNetViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Not using Option Strict
        if (!contentLower.Contains("option strict on"))
        {
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "VB.NET Best Practices",
                Description = "Enable Option Strict On for type safety and better error detection",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                SuggestedFix = "Add at top of file: Option Strict On"
            });
        }

        // 2. Using On Error instead of Try-Catch
        if (contentLower.Contains("on error"))
        {
            var lineNum = FindLineContaining(lines, "On Error", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Modern Error Handling",
                Description = "Use Try-Catch instead of On Error for structured exception handling",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                SuggestedFix = "Use Try-Catch: Try ... Catch ex As Exception ... End Try"
            });
        }

        return violations;
    }

    #endregion

    #region Blockchain (Solidity)

    private List<Violation> DetectSolidityViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing access modifiers
        if (contentLower.Contains("function") && !contentLower.Contains("public") && !contentLower.Contains("private")
            && !contentLower.Contains("internal") && !contentLower.Contains("external"))
        {
            var lineNum = FindLineContaining(lines, "function", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Solidity Security",
                Description = "Always specify function visibility (public/private/internal/external)",
                Type = ViolationType.Security,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Add visibility: function myFunction() public { ... } or external/private/internal"
            });
        }

        // 2. Reentrancy risk
        if (contentLower.Contains("call{value:") || contentLower.Contains("call.value"))
        {
            var lineNum = FindLineContaining(lines, "call\\{value:|call\\.value", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Reentrancy Prevention",
                Description = "External calls can enable reentrancy attacks; update state before calling",
                Type = ViolationType.Security,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Follow Checks-Effects-Interactions pattern: update state BEFORE external calls, or use ReentrancyGuard"
            });
        }

        // 3. Using tx.origin for authorization
        if (contentLower.Contains("tx.origin"))
        {
            var lineNum = FindLineContaining(lines, "tx\\.origin", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "Authorization Security",
                Description = "Never use tx.origin for authorization; use msg.sender instead",
                Type = ViolationType.Security,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Use msg.sender: require(msg.sender == owner, \"Not authorized\");"
            });
        }

        return violations;
    }

    #endregion

    #region Markup Languages (XML, YAML)

    private List<Violation> DetectXMLViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Missing XML declaration
        if (!content.StartsWith("<?xml"))
        {
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "XML Best Practices",
                Description = "XML files should start with proper XML declaration",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Low,
                CodeSnippet = snippet,
                SuggestedFix = "Add XML declaration: <?xml version=\"1.0\" encoding=\"UTF-8\"?>"
            });
        }

        return violations;
    }

    private List<Violation> DetectYAMLViolations(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var violations = new List<Violation>();

        // 1. Using tabs instead of spaces
        if (content.Contains("\t"))
        {
            var lineNum = FindLineContaining(lines, "\t", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            violations.Add(new Violation
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                RuleName = "YAML Syntax",
                Description = "YAML requires spaces for indentation, not tabs",
                Type = ViolationType.BestPractice,
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                SuggestedFix = "Replace tabs with spaces (typically 2 or 4 spaces per indentation level)"
            });
        }

        return violations;
    }

    #endregion

    /// <summary>
    /// Helper method to find line containing a pattern (supports regex)
    /// </summary>
    private int FindLineContaining(string[] lines, string pattern, Random random)
    {
        var matchingLines = new List<int>();

        for (int i = 0; i < lines.Length; i++)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                matchingLines.Add(i + 1);
            }
        }

        return matchingLines.Any() ? matchingLines[random.Next(matchingLines.Count)] : random.Next(1, Math.Max(2, lines.Length));
    }

    #endregion

    private List<Bug> GenerateMockBugs(List<CodeFile> files)
    {
        var bugs = new List<Bug>();
        var random = new Random(42);

        // Analyze each file for potential bugs with language-specific detection
        foreach (var file in files.Where(f => f.FileType != FileType.Markdown && f.Content.Length > 50))
        {
            var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            var fileContent = file.Content;
            var fileContentLower = fileContent.ToLower();
            var lines = fileContent.Split('\n');

            // Language-specific bug detection - Supporting 25+ programming languages
            bugs.AddRange(extension switch
            {
                // C-family languages
                ".cs" => DetectCSharpBugs(file, fileContent, fileContentLower, lines, random),
                ".c" or ".h" => DetectCBugs(file, fileContent, fileContentLower, lines, random),
                ".cpp" or ".hpp" or ".cc" or ".cxx" or ".h++" => DetectCppBugs(file, fileContent, fileContentLower, lines, random),
                ".m" or ".mm" => DetectObjectiveCBugs(file, fileContent, fileContentLower, lines, random),

                // JVM languages
                ".java" => DetectJavaBugs(file, fileContent, fileContentLower, lines, random),
                ".kt" or ".kts" => DetectKotlinBugs(file, fileContent, fileContentLower, lines, random),
                ".scala" => DetectScalaBugs(file, fileContent, fileContentLower, lines, random),
                ".groovy" => DetectGroovyBugs(file, fileContent, fileContentLower, lines, random),

                // JavaScript/TypeScript ecosystem
                ".js" or ".mjs" or ".cjs" => DetectJavaScriptBugs(file, fileContent, fileContentLower, lines, random),
                ".ts" or ".tsx" or ".mts" or ".cts" => DetectTypeScriptBugs(file, fileContent, fileContentLower, lines, random),
                ".jsx" => DetectReactBugs(file, fileContent, fileContentLower, lines, random),
                ".vue" => DetectVueBugs(file, fileContent, fileContentLower, lines, random),

                // Python
                ".py" or ".pyw" or ".pyi" => DetectPythonBugs(file, fileContent, fileContentLower, lines, random),

                // Web languages
                ".php" => DetectPHPBugs(file, fileContent, fileContentLower, lines, random),
                ".rb" or ".rake" => DetectRubyBugs(file, fileContent, fileContentLower, lines, random),

                // Systems languages
                ".go" => DetectGoBugs(file, fileContent, fileContentLower, lines, random),
                ".rs" => DetectRustBugs(file, fileContent, fileContentLower, lines, random),
                ".swift" => DetectSwiftBugs(file, fileContent, fileContentLower, lines, random),

                // Functional languages
                ".fs" or ".fsx" or ".fsi" => DetectFSharpBugs(file, fileContent, fileContentLower, lines, random),
                ".hs" or ".lhs" => DetectHaskellBugs(file, fileContent, fileContentLower, lines, random),
                ".ex" or ".exs" => DetectElixirBugs(file, fileContent, fileContentLower, lines, random),

                // Mobile
                ".dart" => DetectDartBugs(file, fileContent, fileContentLower, lines, random),

                // Scripting languages
                ".sh" or ".bash" or ".zsh" => DetectShellBugs(file, fileContent, fileContentLower, lines, random),
                ".ps1" or ".psm1" or ".psd1" => DetectPowerShellBugs(file, fileContent, fileContentLower, lines, random),
                ".lua" => DetectLuaBugs(file, fileContent, fileContentLower, lines, random),

                // Database
                ".sql" => DetectSQLBugs(file, fileContent, fileContentLower, lines, random),

                // .NET
                ".vb" => DetectVBNetBugs(file, fileContent, fileContentLower, lines, random),

                // Blockchain
                ".sol" => DetectSolidityBugs(file, fileContent, fileContentLower, lines, random),

                _ => new List<Bug>()
            });
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

    #region Language-Specific Bug Detection

    private List<Bug> DetectCSharpBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Null reference potential
        if (content.Contains(".") && !contentLower.Contains("?.") &&
            (contentLower.Contains("var ") || contentLower.Contains("return ")))
        {
            var lineNum = FindLineContaining(lines, "\\.", random);
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
                SuggestedFix = "Use null-conditional operator (?.) or add explicit null checks: if (obj != null) { obj.Method(); } or obj?.Method()"
            });
        }

        // 2. Unhandled async exceptions
        if (contentLower.Contains("async") && contentLower.Contains("await") &&
            !contentLower.Contains("try") && !contentLower.Contains("catch"))
        {
            var lineNum = FindLineContaining(lines, "await ", random);
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
                SuggestedFix = "Wrap async operations in try-catch: try { await operation(); } catch (Exception ex) { _logger.LogError(ex, \"Error\"); }"
            });
        }

        // 3. Resource disposal issues
        if ((contentLower.Contains("new stream") || contentLower.Contains("new file") ||
             contentLower.Contains("httpclient") || contentLower.Contains("new sqlconnection")) &&
            !contentLower.Contains("using") && !contentLower.Contains("dispose"))
        {
            var lineNum = FindLineContaining(lines, "new Stream|new File|HttpClient|new SqlConnection", random);
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
                SuggestedFix = "Use 'using' statement: using (var resource = new Resource()) { ... } or using var resource = new Resource();"
            });
        }

        // 4. Thread safety issues
        if (contentLower.Contains("static") && contentLower.Contains("list<") &&
            !contentLower.Contains("readonly") && !contentLower.Contains("concurrent"))
        {
            var lineNum = FindLineContaining(lines, "static.*List<", random);
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
                SuggestedFix = "Use ConcurrentDictionary/ConcurrentBag or add proper locking: lock(_syncObject) { ... }"
            });
        }

        return bugs;
    }

    private List<Bug> DetectSQLBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. SQL Injection vulnerability
        if (contentLower.Contains("select") && content.Contains("+") &&
            (contentLower.Contains("execute") || contentLower.Contains("exec ")))
        {
            var lineNum = FindLineContaining(lines, "EXEC|EXECUTE", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "SQL Injection Vulnerability",
                Description = "Dynamic SQL construction using string concatenation",
                RootCause = "User input concatenated directly into SQL queries without parameterization",
                Impact = "Critical security vulnerability - attackers can execute arbitrary SQL commands, access/modify unauthorized data",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Input malicious SQL in user-provided data (e.g., '; DROP TABLE Users--)",
                    "SQL gets executed with injected code",
                    "Unauthorized data access or modification occurs"
                },
                SuggestedFix = "Use sp_executesql with parameters: EXEC sp_executesql N'SELECT * FROM Users WHERE Id = @Id', N'@Id INT', @Id = @UserId"
            });
        }

        // 2. Missing transaction for data integrity
        if ((contentLower.Contains("insert") || contentLower.Contains("update") || contentLower.Contains("delete")) &&
            !contentLower.Contains("begin transaction") && !contentLower.Contains("begin tran"))
        {
            var lineNum = FindLineContaining(lines, "INSERT|UPDATE|DELETE", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Data Integrity Risk - Missing Transaction",
                Description = "DML operations without explicit transaction management",
                RootCause = "Multiple data modifications not wrapped in transaction",
                Impact = "Partial updates may occur on failure, leaving database in inconsistent state",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Execute procedure with multiple DML statements",
                    "Trigger error after first DML completes",
                    "Observe partial data changes"
                },
                SuggestedFix = "Wrap in transaction: BEGIN TRANSACTION; ... IF @@ERROR = 0 COMMIT TRANSACTION ELSE ROLLBACK TRANSACTION"
            });
        }

        // 3. Cursor usage (performance issue)
        if (contentLower.Contains("declare") && contentLower.Contains("cursor"))
        {
            var lineNum = FindLineContaining(lines, "CURSOR", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Performance Issue - Cursor Usage",
                Description = "Cursor-based row-by-row processing is significantly slower than set-based operations",
                RootCause = "Using cursor instead of set-based SQL operations",
                Impact = "Severe performance degradation, especially with large datasets; increased server resource consumption",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Execute cursor-based procedure with large dataset",
                    "Observe slow execution time and high CPU usage",
                    "Compare with set-based equivalent"
                },
                SuggestedFix = "Replace cursor with set-based operation: UPDATE table SET column = value WHERE condition (processes all rows at once)"
            });
        }

        return bugs;
    }

    private List<Bug> DetectJavaScriptBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Unhandled promise rejection
        if (contentLower.Contains(".then(") && !contentLower.Contains(".catch("))
        {
            var lineNum = FindLineContaining(lines, "\\.then\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Unhandled Promise Rejection",
                Description = "Promise chain lacks error handling, leading to silent failures",
                RootCause = "Missing .catch() handler on promise chain",
                Impact = "Errors go unnoticed, making debugging difficult and potentially causing application instability",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Trigger promise that rejects",
                    "Observe error is swallowed without logging",
                    "Application continues in unknown state"
                },
                SuggestedFix = "Add .catch() handler: promise.then(result => {...}).catch(error => { console.error('Error:', error); handleError(error); })"
            });
        }

        // 2. Callback hell / Pyramid of doom
        if (contentLower.Split("function(").Length > 4)
        {
            var lineNum = random.Next(1, Math.Max(2, lines.Length));
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Callback Hell - Code Readability Issue",
                Description = "Deeply nested callbacks make code hard to read and maintain",
                RootCause = "Excessive nesting of callback functions",
                Impact = "Reduced code maintainability, increased likelihood of bugs, difficult error handling",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Try to add new functionality to nested callbacks",
                    "Experience difficulty understanding control flow",
                    "Errors become hard to track"
                },
                SuggestedFix = "Refactor to async/await: async function doWork() { const result1 = await operation1(); const result2 = await operation2(result1); }"
            });
        }

        // 3. == instead of === causing type coercion bugs
        if (content.Contains("==") && !content.Contains("==="))
        {
            var lineNum = FindLineContaining(lines, " == ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Type Coercion Bug - Loose Equality",
                Description = "Using == allows type coercion, leading to unexpected comparison results",
                RootCause = "Loose equality (==) instead of strict equality (===)",
                Impact = "Subtle bugs from type coercion: 0 == '0' is true, 0 == [] is true, null == undefined is true",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Compare values of different types using ==",
                    "Observe unexpected true results",
                    "Logic errors occur in conditional statements"
                },
                SuggestedFix = "Use strict equality: if (value === expected) instead of if (value == expected)"
            });
        }

        return bugs;
    }

    private List<Bug> DetectTypeScriptBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // Include JavaScript bugs
        bugs.AddRange(DetectJavaScriptBugs(file, content, contentLower, lines, random));

        // TypeScript-specific: any type usage
        if (contentLower.Contains(": any"))
        {
            var lineNum = FindLineContaining(lines, ": any", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Type Safety Compromise - 'any' Type Usage",
                Description = "Using 'any' type disables TypeScript's type checking, allowing runtime errors",
                RootCause = "Explicitly typing variable as 'any' to bypass type system",
                Impact = "Loss of type safety benefits, potential runtime errors that TypeScript could have caught",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Variable typed as 'any' receives unexpected type",
                    "Code compiles without errors",
                    "Runtime error occurs due to type mismatch"
                },
                SuggestedFix = "Use specific type or 'unknown': const value: SpecificType = ... or const value: unknown = ... (requires type checking before use)"
            });
        }

        return bugs;
    }

    private List<Bug> DetectReactBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Direct state mutation
        if (contentLower.Contains("this.state.") && content.Contains("=") && !contentLower.Contains("setstate"))
        {
            var lineNum = FindLineContaining(lines, "this\\.state\\.", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "React State Mutation Bug",
                Description = "Directly mutating state object instead of using setState",
                RootCause = "Assignment to this.state property without setState() call",
                Impact = "Component doesn't re-render, UI doesn't update, state changes are lost, unpredictable behavior",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Directly modify this.state.property",
                    "Observe UI does not update",
                    "State is in inconsistent state"
                },
                SuggestedFix = "Use setState: this.setState({ property: newValue }) or with useState: setValue(newValue)"
            });
        }

        // 2. Missing dependencies in useEffect
        if (contentLower.Contains("useeffect") && content.Contains("[]"))
        {
            var lineNum = FindLineContaining(lines, "useEffect", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "React Hooks - Missing Dependencies",
                Description = "useEffect with empty dependency array may miss required dependencies",
                RootCause = "useEffect depends on props/state but doesn't list them in dependency array",
                Impact = "Effect doesn't run when dependencies change, stale closure bugs, incorrect behavior",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "useEffect uses prop/state variable",
                    "Variable changes but effect doesn't re-run",
                    "Component uses stale data"
                },
                SuggestedFix = "Add all dependencies: useEffect(() => { ... }, [dependency1, dependency2]) or use ESLint plugin to auto-detect"
            });
        }

        return bugs;
    }

    private List<Bug> DetectPythonBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Mutable default argument
        if (contentLower.Contains("def ") && (content.Contains("=[]") || content.Contains("={}")))
        {
            var lineNum = FindLineContaining(lines, "def.*=\\[\\]|def.*=\\{\\}", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Mutable Default Argument Bug",
                Description = "Mutable default arguments are shared across all function calls, causing unexpected behavior",
                RootCause = "Using list or dict as default parameter value",
                Impact = "Data persists between function calls, leading to data corruption and hard-to-debug issues",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Call function multiple times without providing argument",
                    "Observe data from previous calls persisting",
                    "Unexpected shared state causes bugs"
                },
                SuggestedFix = "Use None as default: def func(items=None): items = items if items is not None else []"
            });
        }

        // 2. Bare except clause
        if (contentLower.Contains("except:"))
        {
            var lineNum = FindLineContaining(lines, "except:", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Overly Broad Exception Handling",
                Description = "Bare except clause catches all exceptions including system exits",
                RootCause = "Using except: without specifying exception type",
                Impact = "Catches KeyboardInterrupt and SystemExit, making program difficult to stop, hides real errors",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Try to interrupt program with Ctrl+C",
                    "Bare except catches KeyboardInterrupt",
                    "Program cannot be stopped normally"
                },
                SuggestedFix = "Catch specific exceptions: except ValueError: or except Exception as e: (doesn't catch system exits)"
            });
        }

        // 3. Missing file close / No context manager
        if (contentLower.Contains("open(") && !contentLower.Contains("with open"))
        {
            var lineNum = FindLineContaining(lines, "open\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Resource Leak - File Not Closed",
                Description = "File opened without context manager may not be properly closed",
                RootCause = "Using open() without 'with' statement",
                Impact = "File handles leak, reaching system limits, data may not be flushed, file locks persist",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Open many files without closing",
                    "Reach system file descriptor limit",
                    "Application fails with 'Too many open files' error"
                },
                SuggestedFix = "Use context manager: with open('file.txt', 'r') as f: data = f.read() (automatically closes file)"
            });
        }

        return bugs;
    }

    private List<Bug> DetectJavaBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Potential NullPointerException
        if (content.Contains(".") && !content.Contains("!= null") && !content.Contains("Optional"))
        {
            var lineNum = FindLineContaining(lines, "\\.", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Potential NullPointerException",
                Description = "Object may be null when dereferenced",
                RootCause = "Missing null check before accessing object members",
                Impact = "NullPointerException at runtime causes application crash or service disruption",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Pass null object to method",
                    "Attempt to access object property or method",
                    "NullPointerException is thrown"
                },
                SuggestedFix = "Add null check: if (object != null) { object.method(); } or use Optional<T>: optional.ifPresent(obj -> obj.method())"
            });
        }

        // 2. Resource not closed (missing try-with-resources)
        if ((contentLower.Contains("new fileinputstream") || contentLower.Contains("new bufferedreader") ||
             contentLower.Contains("new connection")) && !contentLower.Contains("try ("))
        {
            var lineNum = FindLineContaining(lines, "new File|new Buffered|new Connection", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Resource Leak - Missing try-with-resources",
                Description = "AutoCloseable resource not properly closed, causing resource leak",
                RootCause = "Resource created without try-with-resources statement",
                Impact = "File handles, database connections, or network sockets leak, eventually exhausting system resources",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Create resource without try-with-resources",
                    "Exception occurs before manual close",
                    "Resource remains open, leaking memory/handles"
                },
                SuggestedFix = "Use try-with-resources: try (BufferedReader reader = new BufferedReader(new FileReader(file))) { ... } (auto-closes)"
            });
        }

        // 3. String comparison with ==
        if (content.Contains("==") && contentLower.Contains("string"))
        {
            var lineNum = FindLineContaining(lines, "==.*String|String.*==", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "String Comparison Bug - Using == Instead of equals()",
                Description = "Comparing string references with == instead of content with equals()",
                RootCause = "Using == operator for string comparison instead of .equals() method",
                Impact = "Comparison checks reference equality, not value equality; may work in some cases but fail in others",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Compare two strings with same content but different references",
                    "== returns false even though content is identical",
                    "Logic error in conditional statements"
                },
                SuggestedFix = "Use .equals() method: if (str1.equals(str2)) or Objects.equals(str1, str2) for null safety"
            });
        }

        return bugs;
    }

    // ==================== BUG DETECTION FOR ADDITIONAL LANGUAGES ====================

    #region C/C++ and Objective-C Bugs

    private List<Bug> DetectCBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Null pointer dereference
        if (contentLower.Contains("malloc(") && content.Contains("->"))
        {
            var lineNum = FindLineContaining(lines, "->", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Null Pointer Dereference",
                Description = "Dereferencing pointer without checking for NULL",
                RootCause = "malloc() can return NULL on failure, but pointer is used without validation",
                Impact = "Segmentation fault and program crash",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Allocate memory with malloc()",
                    "Allocation fails, returns NULL",
                    "Dereference NULL pointer -> segfault"
                },
                SuggestedFix = "Check for NULL: ptr = malloc(size); if (ptr == NULL) { handle_error(); return; }"
            });
        }

        // 2. Buffer overflow
        if (contentLower.Contains("strcpy") || contentLower.Contains("gets"))
        {
            var lineNum = FindLineContaining(lines, "strcpy|gets", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Buffer Overflow Vulnerability",
                Description = "Unsafe string copy function can cause buffer overflow",
                RootCause = "Using strcpy() or gets() without size checks",
                Impact = "Memory corruption, arbitrary code execution, security vulnerability",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Input string larger than destination buffer",
                    "strcpy/gets copies without size check",
                    "Buffer overflows, corrupting adjacent memory"
                },
                SuggestedFix = "Use safe alternatives: strncpy(dest, src, sizeof(dest)-1); dest[sizeof(dest)-1] = '\\0';"
            });
        }

        return bugs;
    }

    private List<Bug> DetectCppBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // Include C bugs
        bugs.AddRange(DetectCBugs(file, content, contentLower, lines, random));

        // 1. Use after delete
        if (contentLower.Contains("delete ") && !contentLower.Contains("= nullptr"))
        {
            var lineNum = FindLineContaining(lines, "delete ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Use After Delete - Dangling Pointer",
                Description = "Pointer not set to nullptr after delete, risking use-after-delete bugs",
                RootCause = "Deleted pointer remains with old address value",
                Impact = "Undefined behavior, memory corruption, crash",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Delete pointer: delete ptr;",
                    "Reuse pointer accidentally",
                    "Access freed memory -> undefined behavior"
                },
                SuggestedFix = "Set to nullptr after delete: delete ptr; ptr = nullptr;"
            });
        }

        return bugs;
    }

    private List<Bug> DetectObjectiveCBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Messaging nil object
        if (contentLower.Contains("[") && contentLower.Contains("]") && !contentLower.Contains("if"))
        {
            var lineNum = FindLineContaining(lines, "\\[.*\\]", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Unhandled Nil Object",
                Description = "Sending message to potentially nil object without checking",
                RootCause = "Objective-C silently ignores messages to nil, can hide bugs",
                Impact = "Silent failures, unexpected behavior, difficult debugging",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Object becomes nil unexpectedly",
                    "Message sent to nil",
                    "Operation silently fails with no feedback"
                },
                SuggestedFix = "Check for nil: if (object != nil) { [object method]; } else { handle_nil_case(); }"
            });
        }

        return bugs;
    }

    #endregion

    #region JVM Languages Bugs

    private List<Bug> DetectKotlinBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. NullPointerException from !!
        if (content.Contains("!!"))
        {
            var lineNum = FindLineContaining(lines, "!!", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "NullPointerException from !! Operator",
                Description = "Not-null assertion operator (!!) throws NPE if value is null",
                RootCause = "Using !! without ensuring value is actually non-null",
                Impact = "Runtime crash with NullPointerException",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Variable becomes null unexpectedly",
                    "!! operator asserts non-null",
                    "NPE thrown at runtime"
                },
                SuggestedFix = "Use safe call: value?.method() or provide default: value ?: defaultValue"
            });
        }

        return bugs;
    }

    private List<Bug> DetectScalaBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. NullPointerException from null usage
        if (content.Contains("null"))
        {
            var lineNum = FindLineContaining(lines, "null", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "NullPointerException Risk",
                Description = "Using null defeats Scala's Option type safety",
                RootCause = "null used instead of Option[T]",
                Impact = "Runtime NullPointerException",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "null value assigned or returned",
                    "Code attempts to use value",
                    "NPE thrown at runtime"
                },
                SuggestedFix = "Use Option: def findUser(id: Int): Option[User]; then use .getOrElse, .map, .flatMap"
            });
        }

        return bugs;
    }

    private List<Bug> DetectGroovyBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Dynamic typing issues
        if (!contentLower.Contains("@compilestatic"))
        {
            var lineNum = random.Next(1, Math.Max(2, lines.Length));
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Runtime Type Error",
                Description = "Dynamic typing can cause runtime errors that static typing would catch",
                RootCause = "No compile-time type checking without @CompileStatic",
                Impact = "Runtime errors for type mismatches that could be caught at compile time",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Pass wrong type to method",
                    "Compile succeeds without warnings",
                    "Runtime error when code executes"
                },
                SuggestedFix = "Use @CompileStatic: @groovy.transform.CompileStatic class MyClass { ... }"
            });
        }

        return bugs;
    }

    private List<Bug> DetectClojureBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Missing nil checks
        if (contentLower.Contains("get") && !contentLower.Contains("if-let") && !contentLower.Contains("when-let"))
        {
            var lineNum = FindLineContaining(lines, "get", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "NullPointerException on Nil",
                Description = "Accessing nested data without nil checks can cause NPE",
                RootCause = "get/get-in called without checking for nil values",
                Impact = "Runtime NullPointerException",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Data structure contains nil",
                    "Attempt to access nested value",
                    "NPE thrown"
                },
                SuggestedFix = "Use safe navigation: (when-let [value (get-in data [:key])] (use value))"
            });
        }

        return bugs;
    }

    #endregion

    #region Web Languages Bugs

    private List<Bug> DetectPHPBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. SQL Injection
        if ((contentLower.Contains("select") || contentLower.Contains("insert")) && content.Contains("$"))
        {
            var lineNum = FindLineContaining(lines, "SELECT|INSERT|\\$", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "SQL Injection Vulnerability",
                Description = "Direct variable interpolation in SQL allows SQL injection attacks",
                RootCause = "User input concatenated directly into SQL without sanitization",
                Impact = "Critical security vulnerability - data breach, data loss, unauthorized access",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "User inputs malicious SQL: '; DROP TABLE users--",
                    "SQL executes injected code",
                    "Database compromised"
                },
                SuggestedFix = "Use prepared statements: $stmt = $pdo->prepare('SELECT * FROM users WHERE id = :id'); $stmt->execute(['id' => $id]);"
            });
        }

        // 2. XSS vulnerability
        if (contentLower.Contains("echo") && content.Contains("$") && !contentLower.Contains("htmlspecialchars"))
        {
            var lineNum = FindLineContaining(lines, "echo.*\\$", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Cross-Site Scripting (XSS) Vulnerability",
                Description = "Echoing user input without sanitization allows XSS attacks",
                RootCause = "Output not escaped before rendering in HTML",
                Impact = "XSS attacks can steal cookies, session tokens, execute malicious scripts",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "User inputs <script>alert('XSS')</script>",
                    "PHP echoes unsanitized input",
                    "Script executes in victim's browser"
                },
                SuggestedFix = "Escape output: echo htmlspecialchars($userInput, ENT_QUOTES, 'UTF-8');"
            });
        }

        return bugs;
    }

    private List<Bug> DetectRubyBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Mass assignment vulnerability
        if (contentLower.Contains(".new(params") || contentLower.Contains(".update(params"))
        {
            var lineNum = FindLineContaining(lines, "\\.new\\(params|\\.update\\(params", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Mass Assignment Vulnerability",
                Description = "Allowing all parameters can let attackers modify protected attributes",
                RootCause = "Using params hash directly without filtering",
                Impact = "Security breach - unauthorized attribute modification",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Attacker adds unauthorized parameter (e.g., admin=true)",
                    "Model accepts all params",
                    "Protected attribute modified"
                },
                SuggestedFix = "Use strong parameters: User.new(user_params) with def user_params; params.require(:user).permit(:name, :email); end"
            });
        }

        return bugs;
    }

    private List<Bug> DetectVueBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Memory leak from event listeners
        if (contentLower.Contains("addeventlistener") && !contentLower.Contains("removeeventlistener"))
        {
            var lineNum = FindLineContaining(lines, "addEventListener", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Memory Leak - Event Listeners Not Removed",
                Description = "Event listeners not removed in beforeDestroy hook cause memory leaks",
                RootCause = "Adding event listeners without cleanup",
                Impact = "Memory leaks, performance degradation over time",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Component adds event listener in mounted()",
                    "Component destroyed without cleanup",
                    "Event listener remains, leaking memory"
                },
                SuggestedFix = "Remove in beforeDestroy: beforeDestroy() { window.removeEventListener('resize', this.handler); }"
            });
        }

        return bugs;
    }

    #endregion

    #region Systems Languages Bugs

    private List<Bug> DetectGoBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Goroutine leak
        if (contentLower.Contains("go ") && !contentLower.Contains("context"))
        {
            var lineNum = FindLineContaining(lines, "go ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Goroutine Leak",
                Description = "Goroutine without cancellation mechanism can leak if parent exits",
                RootCause = "No context or done channel to signal goroutine termination",
                Impact = "Resource leak, goroutines accumulate, memory exhaustion",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Launch goroutine without cancellation",
                    "Parent function returns",
                    "Goroutine continues indefinitely, leaking"
                },
                SuggestedFix = "Use context: go func(ctx context.Context) { select { case <-ctx.Done(): return; ... } }(ctx)"
            });
        }

        // 2. Data race
        if (contentLower.Contains("go ") && !contentLower.Contains("mutex") && !contentLower.Contains("chan"))
        {
            var lineNum = FindLineContaining(lines, "go ", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Data Race - Concurrent Access Without Synchronization",
                Description = "Multiple goroutines accessing shared data without synchronization",
                RootCause = "No mutex or channel for coordinating access",
                Impact = "Race conditions, data corruption, unpredictable behavior",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Multiple goroutines access same variable",
                    "No synchronization mechanism",
                    "Run with -race flag: data race detected"
                },
                SuggestedFix = "Use mutex: var mu sync.Mutex; mu.Lock(); /* access data */; mu.Unlock(); or use channels"
            });
        }

        return bugs;
    }

    private List<Bug> DetectRustBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Panic from unwrap
        if (contentLower.Contains(".unwrap()"))
        {
            var lineNum = FindLineContaining(lines, "\\.unwrap\\(\\)", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Panic from unwrap()",
                Description = "unwrap() panics if Result is Err or Option is None",
                RootCause = "Using unwrap() without ensuring value is Ok/Some",
                Impact = "Program crash with panic",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Function returns Err or None",
                    "unwrap() called",
                    "Program panics and crashes"
                },
                SuggestedFix = "Use ? operator: let value = result?; or match: match result { Ok(v) => v, Err(e) => return Err(e) }"
            });
        }

        return bugs;
    }

    private List<Bug> DetectSwiftBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Force unwrap crash
        if (content.Contains("!") && !contentLower.Contains("guard") && !contentLower.Contains("if let"))
        {
            var lineNum = FindLineContaining(lines, "!", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Crash from Force Unwrapping nil",
                Description = "Force unwrapping (!) crashes if optional is nil",
                RootCause = "Using ! without ensuring optional has value",
                Impact = "App crash with fatal error",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Optional becomes nil",
                    "Force unwrap with !",
                    "App crashes: Fatal error: Unexpectedly found nil"
                },
                SuggestedFix = "Use optional binding: if let value = optional { use value } or guard let value = optional else { return }"
            });
        }

        return bugs;
    }

    #endregion

    #region Functional Languages Bugs

    private List<Bug> DetectFSharpBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. NullReferenceException from mutable
        if (contentLower.Contains("mutable"))
        {
            var lineNum = FindLineContaining(lines, "mutable", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "NullReferenceException from Mutable State",
                Description = "Mutable values can become null, causing NullReferenceException",
                RootCause = "Using mutable values without null checks",
                Impact = "Runtime NullReferenceException",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Mutable value set to null",
                    "Code accesses value",
                    "NullReferenceException thrown"
                },
                SuggestedFix = "Use immutable and Option: let value: Option<int> = Some 10"
            });
        }

        return bugs;
    }

    private List<Bug> DetectHaskellBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Exception from partial functions
        if (contentLower.Contains("head") || contentLower.Contains("tail"))
        {
            var lineNum = FindLineContaining(lines, "head|tail", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Exception from Partial Function on Empty List",
                Description = "head/tail throw exception on empty lists",
                RootCause = "Using partial functions without checking for empty list",
                Impact = "Runtime exception and program crash",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Function receives empty list",
                    "head or tail called",
                    "Exception: Prelude.head: empty list"
                },
                SuggestedFix = "Use safe alternatives: listToMaybe, pattern matching: case xs of (x:_) -> x; [] -> defaultValue"
            });
        }

        return bugs;
    }

    private List<Bug> DetectElixirBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. BadMatchError from pattern matching
        if (contentLower.Contains("=") && contentLower.Contains("{:ok,"))
        {
            var lineNum = FindLineContaining(lines, "\\{:ok,", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "BadMatchError from Unhandled Pattern",
                Description = "Pattern match expects {:ok, value} but may receive {:error, reason}",
                RootCause = "Not handling all possible return patterns",
                Impact = "Runtime crash with BadMatchError",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Function returns {:error, reason}",
                    "Code expects {:ok, value}",
                    "BadMatchError raised"
                },
                SuggestedFix = "Handle both patterns: case result do {:ok, value} -> ...; {:error, reason} -> ...; end"
            });
        }

        return bugs;
    }

    private List<Bug> DetectErlangBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Unhandled message causing mailbox overflow
        if (contentLower.Contains("receive") && !content.Contains("_"))
        {
            var lineNum = FindLineContaining(lines, "receive", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Mailbox Overflow from Unhandled Messages",
                Description = "Messages not matching any pattern accumulate in mailbox",
                RootCause = "No catch-all pattern in receive block",
                Impact = "Memory leak, process slowdown, eventual crash",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Unexpected messages sent to process",
                    "No pattern matches in receive",
                    "Messages accumulate infinitely"
                },
                SuggestedFix = "Add catch-all: receive {expected, Data} -> handle(Data); _ -> ok end."
            });
        }

        return bugs;
    }

    #endregion

    #region Mobile Bugs

    private List<Bug> DetectDartBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Null safety violation
        if (content.Contains("!") && !contentLower.Contains("?"))
        {
            var lineNum = FindLineContaining(lines, "!", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Null Check Failure",
                Description = "Null assertion operator (!) fails if value is null",
                RootCause = "Using ! without ensuring value is non-null",
                Impact = "Runtime crash with Null check operator used on a null value",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Variable becomes null",
                    "! operator asserts non-null",
                    "App crashes with null check error"
                },
                SuggestedFix = "Use null-aware operators: value?.method() or if (value != null) { value.method() }"
            });
        }

        return bugs;
    }

    #endregion

    #region Scripting Languages Bugs

    private List<Bug> DetectShellBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Command injection
        if (content.Contains("$") && (contentLower.Contains("eval") || contentLower.Contains("exec")))
        {
            var lineNum = FindLineContaining(lines, "eval|exec", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Command Injection Vulnerability",
                Description = "Executing user input in eval/exec allows arbitrary command execution",
                RootCause = "User input passed to eval or exec without sanitization",
                Impact = "Critical security vulnerability - arbitrary code execution",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "User inputs malicious command: ; rm -rf /",
                    "eval/exec executes injected command",
                    "System compromised"
                },
                SuggestedFix = "Avoid eval/exec with user input; validate and sanitize all inputs; use safe alternatives"
            });
        }

        return bugs;
    }

    private List<Bug> DetectPowerShellBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Script injection
        if (content.Contains("$") && (contentLower.Contains("invoke-expression") || contentLower.Contains("iex")))
        {
            var lineNum = FindLineContaining(lines, "Invoke-Expression|iex", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Script Injection via Invoke-Expression",
                Description = "Invoke-Expression with user input allows arbitrary code execution",
                RootCause = "Using Invoke-Expression (or iex) with unsanitized input",
                Impact = "Critical security vulnerability - arbitrary PowerShell code execution",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "User provides malicious input",
                    "Invoke-Expression executes it",
                    "Arbitrary code runs with script privileges"
                },
                SuggestedFix = "Avoid Invoke-Expression; use cmdlets directly; validate/sanitize all input"
            });
        }

        return bugs;
    }

    private List<Bug> DetectLuaBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Global variable collision
        if (!contentLower.Contains("local") && contentLower.Contains("="))
        {
            var lineNum = FindLineContaining(lines, "=", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Global Variable Collision",
                Description = "Global variables can collide across modules causing bugs",
                RootCause = "Not using 'local' keyword for variables",
                Impact = "Unexpected variable overwrites, hard-to-debug issues",
                Severity = SeverityLevel.Medium,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Two modules use same global variable name",
                    "One module overwrites other's variable",
                    "Unexpected behavior occurs"
                },
                SuggestedFix = "Use local variables: local myVar = 10"
            });
        }

        return bugs;
    }

    private List<Bug> DetectPerlBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Bareword warnings
        if (!contentLower.Contains("use strict"))
        {
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Bareword Issues Without strict",
                Description = "Without 'use strict', barewords can cause subtle bugs",
                RootCause = "Not using strict pragma",
                Impact = "Typos in variable names create new variables instead of errors",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Typo in variable name: $usr instead of $user",
                    "Creates new variable instead of error",
                    "Logic bug with undefined variable"
                },
                SuggestedFix = "Add at top: use strict; use warnings;"
            });
        }

        return bugs;
    }

    #endregion

    #region Data Science Bugs

    private List<Bug> DetectRBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Factor conversion issues
        if (contentLower.Contains("as.numeric") && contentLower.Contains("factor"))
        {
            var lineNum = FindLineContaining(lines, "as\\.numeric.*factor|factor.*as\\.numeric", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Incorrect Factor to Numeric Conversion",
                Description = "Using as.numeric() on factors returns level indices, not values",
                RootCause = "Direct conversion of factor to numeric",
                Impact = "Data corruption, incorrect statistical results",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Factor levels: \"low\", \"medium\", \"high\"",
                    "as.numeric(factor) returns 1, 2, 3 instead of actual values",
                    "Incorrect calculations result"
                },
                SuggestedFix = "Convert correctly: as.numeric(as.character(factor_var))"
            });
        }

        return bugs;
    }

    private List<Bug> DetectJuliaBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Type instability causing performance
        if (contentLower.Contains("global") || (!contentLower.Contains("::") && contentLower.Contains("function")))
        {
            var lineNum = FindLineContaining(lines, "global|function", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Type Instability Performance Bug",
                Description = "Type-unstable code runs 10-100x slower in Julia",
                RootCause = "Variables change types or lack type annotations",
                Impact = "Severe performance degradation",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Function with type-unstable variables",
                    "Julia can't optimize due to type uncertainty",
                    "Code runs extremely slowly"
                },
                SuggestedFix = "Add type annotations: function process(x::Float64)::Float64; avoid global variables in tight loops"
            });
        }

        return bugs;
    }

    #endregion

    #region .NET Bugs

    private List<Bug> DetectVBNetBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Late binding without Option Strict
        if (!contentLower.Contains("option strict on"))
        {
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, 1);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Late Binding Errors Without Option Strict",
                Description = "Without Option Strict, type errors become runtime exceptions",
                RootCause = "Option Strict not enabled",
                Impact = "Type mismatches cause runtime errors instead of compile-time errors",
                Severity = SeverityLevel.High,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Assign wrong type to variable",
                    "Compiles successfully",
                    "Runtime exception when code executes"
                },
                SuggestedFix = "Add at top of file: Option Strict On"
            });
        }

        return bugs;
    }

    #endregion

    #region Blockchain Bugs

    private List<Bug> DetectSolidityBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Reentrancy attack vulnerability
        if (contentLower.Contains("call{value:") && !contentLower.Contains("nonreentrant"))
        {
            var lineNum = FindLineContaining(lines, "call\\{value:", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Reentrancy Attack Vulnerability",
                Description = "External call before state update allows reentrancy attacks",
                RootCause = "State updated after external call (violates Checks-Effects-Interactions)",
                Impact = "Critical - attacker can drain contract funds (like DAO hack)",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Contract calls external address",
                    "External contract calls back into original",
                    "State not yet updated -> can withdraw multiple times"
                },
                SuggestedFix = "Update state BEFORE external call, or use ReentrancyGuard modifier from OpenZeppelin"
            });
        }

        // 2. Integer overflow/underflow
        if ((content.Contains("+") || content.Contains("-")) && !contentLower.Contains("safemath") && !content.Contains("unchecked"))
        {
            var lineNum = FindLineContaining(lines, "\\+|-", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Integer Overflow/Underflow (pre-0.8.0)",
                Description = "Arithmetic operations can overflow/underflow without SafeMath",
                RootCause = "Using arithmetic without overflow checks (Solidity < 0.8.0)",
                Impact = "Critical - can manipulate balances, bypass checks",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "uint256 balance = 0",
                    "balance -= 1 (underflows to 2^256-1)",
                    "Now has maximum balance"
                },
                SuggestedFix = "Use Solidity >= 0.8.0 (built-in checks) or import SafeMath library for older versions"
            });
        }

        return bugs;
    }

    #endregion

    #region Markup Languages Bugs

    private List<Bug> DetectXMLBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. XML External Entity (XXE) injection
        if (contentLower.Contains("<!entity"))
        {
            var lineNum = FindLineContaining(lines, "<!ENTITY", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "XML External Entity (XXE) Injection Risk",
                Description = "External entities can be exploited to read files or perform SSRF attacks",
                RootCause = "XML parser allows external entity processing",
                Impact = "Security vulnerability - file disclosure, SSRF, DoS",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Attacker provides XML with external entity",
                    "Parser processes external reference",
                    "Sensitive files exposed or SSRF executed"
                },
                SuggestedFix = "Disable external entities in XML parser settings; validate and sanitize XML input"
            });
        }

        return bugs;
    }

    private List<Bug> DetectYAMLBugs(CodeFile file, string content, string contentLower, string[] lines, Random random)
    {
        var bugs = new List<Bug>();

        // 1. Unsafe YAML deserialization
        if (contentLower.Contains("load(") && !contentLower.Contains("safeload"))
        {
            var lineNum = FindLineContaining(lines, "load\\(", random);
            var (snippet, startLine, endLine) = GetCodeSnippetWithRange(lines, lineNum);
            bugs.Add(new Bug
            {
                FilePath = file.FilePath,
                LineNumber = startLine,
                EndLineNumber = endLine,
                Title = "Unsafe YAML Deserialization",
                Description = "yaml.load() can execute arbitrary code; use safe_load()",
                RootCause = "Using yaml.load() instead of yaml.safe_load()",
                Impact = "Critical security vulnerability - arbitrary code execution",
                Severity = SeverityLevel.Critical,
                CodeSnippet = snippet,
                ReproductionSteps = new List<string>
                {
                    "Attacker provides YAML with Python objects",
                    "yaml.load() deserializes and executes",
                    "Arbitrary code runs on server"
                },
                SuggestedFix = "Use safe_load: yaml.safe_load(data) instead of yaml.load(data)"
            });
        }

        return bugs;
    }

    #endregion

    #endregion

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

