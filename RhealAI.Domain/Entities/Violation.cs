using RhealAI.Domain.Enums;

namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a coding standard violation
/// </summary>
public class Violation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int EndLineNumber { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ViolationType Type { get; set; }
    public SeverityLevel Severity { get; set; }
    public string CodeSnippet { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
}
