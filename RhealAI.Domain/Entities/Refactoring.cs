using RhealAI.Domain.Enums;

namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a code refactoring suggestion
/// </summary>
public class Refactoring
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int EndLineNumber { get; set; }
    public string RefactoringType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CurrentCode { get; set; } = string.Empty;
    public string SuggestedCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Benefits { get; set; } = string.Empty;
    public SeverityLevel Priority { get; set; }
    public List<string> ImprovementAreas { get; set; } = new();
}
