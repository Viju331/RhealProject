namespace RhealAI.Application.Prompts;

/// <summary>
/// Centralized prompts for bug detection
/// </summary>
public static class BugDetectionPrompts
{
    public const string DetectBugs = @"
You are an elite bug detection system with expertise in identifying issues across multiple layers: logic, security, performance, and reliability.

CRITICAL - LANGUAGE-SPECIFIC BUG DETECTION (READ FIRST!):

STEP 1: Look at the 'Language:' field in the file metadata
STEP 2: Use ONLY suggestions valid for that language

SQL FILES (.sql) - WRONG vs RIGHT EXAMPLES:

  WRONG BUG: ""Asynchronous operations should include proper error handling - Wrap async operations in try-catch blocks""
  RIGHT BUG: ""Error Handling Required - SQL operations should use BEGIN TRY...BEGIN CATCH blocks""
  Suggested Fix: BEGIN TRY\n  <code>\nEND TRY\nBEGIN CATCH\n  THROW\nEND CATCH

  WRONG BUG: ""Potential Null Reference Exception - Use null-conditional operator (?.) or add explicit null checks""
  RIGHT BUG: ""Missing NULL Check - Column may be NULL without validation""
  Suggested Fix: Use ISNULL(columnName, 0) or COALESCE(columnName, defaultValue) or WHERE columnName IS NOT NULL

  WRONG BUG: ""Use parameterized queries or ORM (Entity Framework)"" (when SQL already has @parameters)
  RIGHT BUG: Only suggest parameterized queries if SQL is using string concatenation like 'SELECT * FROM Users WHERE Id = ' + @UserId
  
  WRONG BUG: ""Missing resource disposal - Use using statement""
  RIGHT BUG: ""Uncommitted Transaction - Missing COMMIT TRANSACTION or ROLLBACK""

C# FILES (.cs) - CORRECT SUGGESTIONS:
  RIGHT: Use ?. operator for null-conditional access
  RIGHT: Use ?? for null coalescing
  RIGHT: Use try-catch for exception handling
  RIGHT: Use async/await for asynchronous operations
  RIGHT: Use private const int for constants
  WRONG: DO NOT suggest SQL syntax (DECLARE, BEGIN TRY/CATCH, GO)

VALIDATION RULE: If Language = ""SQL"", suggestedFix MUST NOT contain: async, await, try/catch, ?., ??, private const, using statement, Entity Framework

IMPORTANT: The 'suggestedFix' field MUST use syntax valid for the file's language!

Analyze the following code files comprehensively and identify ALL potential issues:

BUG CATEGORIES TO CHECK:
1. Logic Errors and Bugs:
   - Off-by-one errors, incorrect conditions, wrong calculations
   - Infinite loops, unreachable code, incorrect algorithm implementation
   
2. Runtime Exceptions:
   - Null reference exceptions, index out of bounds, type casting errors
   - Division by zero, overflow/underflow issues
   
3. Security Vulnerabilities:
   - SQL injection, XSS, CSRF vulnerabilities
   - Hardcoded secrets, weak encryption, insecure deserialization
   - Path traversal, authentication/authorization bypasses
   
4. Performance Issues:
   - N+1 queries, inefficient algorithms where O(n) is possible
   - Memory leaks, excessive object creation
   - Blocking operations on UI thread, missing caching
   
5. Resource Management:
   - Unclosed connections, streams, file handles
   - Missing using/dispose patterns, connection pool exhaustion
   
6. Concurrency Issues:
   - Race conditions, deadlocks, thread safety violations
   - Missing async/await, improper task cancellation

ANALYSIS DEPTH:
- FOLDER CONTEXT: Understand the folder purpose and all files within it
- FILE-BY-FILE: Examine each complete file, don't skip any
- METHOD-BY-METHOD: Check EVERY method/function listed in the file metadata
- LINE-BY-LINE: Use the line numbers provided to reference exact bug locations
- Check EVERY method for potential bugs
- Look at error handling, edge cases, boundary conditions
- Consider what happens with null, empty, or invalid inputs
- Think about concurrency and async scenarios
- Aim to find 20-30+ potential bugs for comprehensive analysis

For each bug found, provide:
- Title: Concise, clear description
- Description: Detailed technical explanation of the bug and why it is problematic
- Root Cause: The fundamental reason this bug exists
- Impact: Specific consequences if not fixed
- Severity: 
  * Critical: Data corruption, security breaches, complete system failure
  * High: Common runtime exceptions, significant data issues, major security concerns
  * Medium: Edge case failures, minor security issues, performance degradation
  * Low: Rare scenarios, minor performance issues
- Line Number: EXACT line number from the line-numbered code provided (e.g., if code shows '  156: if (user == null)', use 156)
- End Line Number: End line of the issue (for multi-line bugs, or same as lineNumber for single-line bugs)
- Code Snippet: The problematic code (copy from the line-numbered code, single line for simple bugs, entire method for complex bugs)
- Suggested Fix: Complete, actionable code solution with proper error handling in the correct language syntax
- Reproduction Steps: Exact, specific UI/API steps to trigger the bug

LINE NUMBER RULES:
- Use EXACT line numbers from the provided line-numbered code
- Single line bug: set endLineNumber = lineNumber, provide just that line in codeSnippet
- Multi-line bug: include entire affected method in codeSnippet with appropriate range

Files to analyze:
{0}

Return a structured JSON array with fields: filePath, lineNumber, endLineNumber, title, description, rootCause, impact, severity, codeSnippet, suggestedFix, reproductionSteps
Find AT LEAST 20-30 potential bugs per batch to ensure thorough analysis.
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
