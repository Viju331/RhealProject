namespace RhealAI.Domain.Enums;

/// <summary>
/// Types of code duplication
/// </summary>
public enum DuplicationType
{
    ExactMatch,          // Identical code blocks
    StructuralMatch,     // Similar structure with different names
    LogicalMatch,        // Same logic with different implementation
    FunctionalMatch,     // Same functionality with different approach
    PartialMatch         // Partially duplicated code
}
