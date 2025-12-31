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
- Line Number: Where the issue occurs
- Code Snippet: The problematic code
- Suggested Fix: How to resolve it
- Reproduction Steps: Exact UI/API steps to reproduce (be very specific)

Files to analyze:
{0}

Return a structured JSON array of bugs.
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
