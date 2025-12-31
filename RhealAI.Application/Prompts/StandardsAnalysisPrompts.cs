namespace RhealAI.Application.Prompts;

/// <summary>
/// Centralized prompts for standards analysis
/// </summary>
public static class StandardsAnalysisPrompts
{
    public const string ExtractFromMarkdown = @"
You are a coding standards analyzer. Analyze the following markdown documentation and extract all coding standards, architecture rules, and best practices.

For each standard found, provide:
1. Name: Clear identifier for the standard
2. Description: Detailed explanation
3. Category: Classification (Naming, Architecture, Security, Performance, etc.)
4. Examples: Code examples if provided

Markdown Content:
{0}

Return a structured JSON array of standards.
";

    public const string GenerateFromCodebase = @"
You are a code standards generator. Analyze the following codebase files and derive consistent coding standards, patterns, and best practices being used.

Look for patterns in:
- Naming conventions (classes, methods, variables)
- Code structure and organization
- Error handling approaches
- Documentation style
- Architecture patterns

Files to analyze:
{0}

Generate a comprehensive set of coding standards in JSON format with: Name, Description, Category, and Examples.
";
}
