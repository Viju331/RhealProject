namespace RhealAI.Application.Prompts;

/// <summary>
/// Centralized prompts for violation detection
/// </summary>
public static class ViolationDetectionPrompts
{
    public const string DetectViolations = @"
You are a coding standards enforcer. Compare the following code files against the established coding standards and identify all violations.

Coding Standards:
{0}

Code Files:
{1}

For each violation found, provide:
- Rule Name: Which standard was violated
- Description: Explanation of the violation
- Type: Classification (NamingConvention, Architecture, Security, etc.)
- Severity: Critical/High/Medium/Low
- File Path: Location of violation
- Line Number: Start line of the violation
- End Line Number: End line of the violation (for multi-line issues, or same as lineNumber for single-line issues)
- Code Snippet: The violating code (single line for simple issues, entire method/block for complex issues)
- Suggested Fix: How to correct it

IMPORTANT: 
- If the violation is a single line (e.g., variable naming), provide just that line in codeSnippet and set endLineNumber = lineNumber
- If the violation spans multiple lines or affects a whole method, include the entire method in codeSnippet and set appropriate lineNumber and endLineNumber range

Return a structured JSON array of violations with fields: filePath, lineNumber, endLineNumber, ruleName, description, type, severity, codeSnippet, suggestedFix
";
}
