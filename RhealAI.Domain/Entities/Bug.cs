using RhealAI.Domain.Enums;

namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a detected bug or logical issue
/// </summary>
public class Bug
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public SeverityLevel Severity { get; set; }
    public string CodeSnippet { get; set; } = string.Empty;
    public List<string> ReproductionSteps { get; set; } = new();
    public string SuggestedFix { get; set; } = string.Empty;
}
