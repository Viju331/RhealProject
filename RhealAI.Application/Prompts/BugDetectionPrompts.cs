namespace RhealAI.Application.Prompts;

/// <summary>
/// Centralized prompts for bug detection
/// </summary>
public static class BugDetectionPrompts
{
    public const string DetectBugs = @"
You are an expert bug detection system. Analyze the following code files and identify:

1. Logic errors and bugs
2. Potential runtime exceptions
3. Security vulnerabilities
4. Performance issues
5. Resource leaks
6. Null reference possibilities

For each bug found, provide:
- Title: Brief description
- Description: Detailed explanation
- Root Cause: Why this is a problem
- Impact: Consequences if not fixed
- Severity: Critical/High/Medium/Low
- Line Number: Start line where the issue occurs
- End Line Number: End line of the issue (for multi-line bugs, or same as lineNumber for single-line bugs)
- Code Snippet: The problematic code (single line for simple bugs, entire method for complex bugs)
- Suggested Fix: How to resolve it
- Reproduction Steps: Exact UI/API steps to reproduce (be very specific)

IMPORTANT:
- If the bug is on a single line, provide just that line in codeSnippet and set endLineNumber = lineNumber
- If the bug affects a whole method or block, include the entire method in codeSnippet and set appropriate lineNumber and endLineNumber range

Files to analyze:
{0}

Return a structured JSON array of bugs with fields: filePath, lineNumber, endLineNumber, title, description, rootCause, impact, severity, codeSnippet, suggestedFix, reproductionSteps
";

    public const string GenerateReproductionSteps = @"
Based on the following bug details in a UI application, generate exact step-by-step instructions for reproducing the bug from the user interface:

Bug: {0}
File: {1}
Code Context: {2}

Provide detailed UI reproduction steps like:
1. Navigate to [specific page/component]
2. Enter [specific data] in [specific field]
3. Click [specific button]
4. Expected: [what should happen]
5. Actual: [what actually happens - the bug]
";
}
