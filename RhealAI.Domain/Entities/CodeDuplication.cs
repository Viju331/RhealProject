using RhealAI.Domain.Enums;

namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a detected code duplication or redundancy
/// </summary>
public class CodeDuplication
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DuplicatedCode { get; set; } = string.Empty;
    public List<DuplicationLocation> Locations { get; set; } = new();
    public DuplicationType Type { get; set; }
    public int LineCount { get; set; }
    public double SimilarityPercentage { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public SeverityLevel Impact { get; set; }
    public List<string> RefactoringOptions { get; set; } = new();
    public string EstimatedEffort { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a specific location where duplicated code exists
/// </summary>
public class DuplicationLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
}
