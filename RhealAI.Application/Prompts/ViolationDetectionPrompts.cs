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
- Line Number: Exact line
- Code Snippet: The violating code
- Suggested Fix: How to correct it

Return a structured JSON array of violations.
";
}
