namespace RhealAI.Application.Prompts;

/// <summary>
/// Centralized prompts for standards analysis
/// </summary>
public static class StandardsAnalysisPrompts
{
    public const string ExtractFromMarkdown = @"
You are an expert coding standards analyzer. Perform a DEEP and COMPREHENSIVE analysis of the following markdown documentation to extract ALL coding standards, architecture rules, and best practices.

For each standard found, provide DETAILED information including:
1. Name: Clear, specific identifier for the standard
2. Description: Comprehensive, detailed explanation (minimum 2-3 sentences)
3. Category: Precise classification (e.g., Naming Conventions, Architecture Patterns, Security Best Practices, Performance Optimization, Code Structure, Error Handling, Testing Standards, Documentation, API Design, Data Access, Dependency Management, Logging, Configuration, Async Patterns, SOLID Principles, Design Patterns, Code Quality, Maintainability, etc.)
4. Examples: Multiple concrete code examples demonstrating the standard (at least 2-3 examples per standard)
5. Rationale: WHY this standard is important (1-2 sentences)
6. Severity: How critical is this standard (Critical, High, Medium, Low)
7. AppliesTo: What types of files/components this applies to (e.g., Controllers, Services, Models, etc.)

BE THOROUGH AND EXTRACT EVERY POSSIBLE STANDARD, including:
- All naming conventions (classes, interfaces, methods, variables, constants, properties)
- All architectural patterns and principles
- Security requirements and practices
- Performance guidelines
- Error handling standards
- Logging and monitoring practices
- Testing requirements
- Documentation standards
- Code organization and structure rules
- Dependency injection patterns
- Async/await patterns
- LINQ usage guidelines
- Exception handling strategies
- Configuration management
- API design principles
- Database access patterns

Markdown Content:
{0}

Return a structured JSON array with schema:
[{{
  ""Name"": ""string"",
  ""Description"": ""string (detailed)"",
  ""Category"": ""string"",
  ""Examples"": [""string"", ""string"", ...],
  ""Rationale"": ""string"",
  ""Severity"": ""Critical|High|Medium|Low"",
  ""AppliesTo"": [""string"", ...]
}}]

IMPORTANT: Extract AT LEAST 15-25 distinct standards. Be comprehensive and thorough.
";

    public const string GenerateFromCodebase = @"
You are an expert code standards generator with deep knowledge of software engineering best practices. Perform a COMPREHENSIVE analysis of the following codebase files to derive ALL coding standards, patterns, and best practices being used.

Conduct an IN-DEPTH analysis across ALL these dimensions:

1. NAMING CONVENTIONS:
   - Class naming patterns (PascalCase, prefixes, suffixes)
   - Interface naming (IPrefix, etc.)
   - Method naming (PascalCase, verb-noun patterns)
   - Variable naming (camelCase, descriptive names)
   - Constant naming (UPPERCASE, prefixes)
   - Property naming patterns
   - Parameter naming conventions
   - Private field naming (_camelCase, etc.)

2. ARCHITECTURE PATTERNS:
   - Layering patterns (e.g., Controller -> Service -> Repository)
   - Dependency injection usage
   - Separation of concerns
   - SOLID principles application
   - Design patterns in use (Factory, Strategy, Repository, etc.)

3. CODE STRUCTURE:
   - File organization
   - Method length guidelines
   - Class responsibilities
   - Namespace organization
   - Region usage
   - Code grouping patterns

4. ERROR HANDLING:
   - Try-catch patterns
   - Exception types used
   - Error logging practices
   - Error response patterns
   - Validation approaches

5. ASYNC/AWAIT PATTERNS:
   - Async method naming (suffix with Async)
   - ConfigureAwait usage
   - Task return types
   - Async all the way pattern

6. DOCUMENTATION:
   - XML documentation comments
   - Summary tags
   - Parameter documentation
   - Return value documentation
   - Example tags

7. DEPENDENCY MANAGEMENT:
   - Constructor injection patterns
   - Service registration patterns
   - Interface-based dependencies

8. LINQ & DATA ACCESS:
   - Query patterns
   - Entity Framework usage
   - Data access patterns

9. API DESIGN:
   - HTTP verb usage
   - Route patterns
   - DTO patterns
   - Response formatting

10. TESTING PATTERNS:
    - Unit test naming
    - Test organization
    - Mocking patterns

11. PERFORMANCE:
    - Resource management
    - Caching patterns
    - Optimization techniques

12. SECURITY:
    - Authentication patterns
    - Authorization patterns
    - Input validation
    - Secure coding practices

Files to analyze:
{0}

For EACH pattern discovered, generate a detailed standard with:
- Name: Specific, clear identifier
- Description: Comprehensive explanation (minimum 3-4 sentences)
- Category: Precise classification
- Examples: Multiple concrete examples from the actual codebase (3-5 examples)
- Rationale: WHY this standard exists (2-3 sentences about benefits)
- Severity: Importance level (Critical, High, Medium, Low)
- AppliesTo: What components this applies to

Return structured JSON array with schema:
[{{
  ""Name"": ""string"",
  ""Description"": ""string (detailed)"",
  ""Category"": ""string"",
  ""Examples"": [""string"", ""string"", ...],
  ""Rationale"": ""string"",
  ""Severity"": ""Critical|High|Medium|Low"",
  ""AppliesTo"": [""string"", ...]
}}]

CRITICAL: Generate AT LEAST 20-30 comprehensive standards covering ALL categories mentioned above. Be exhaustive and thorough in your analysis.
";
}
