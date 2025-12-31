namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a complete analysis report
/// </summary>
public class AnalysisReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RepositoryId { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalFiles { get; set; }
    public int FilesWithViolations { get; set; }
    public int FilesWithBugs { get; set; }
    public int FilesNeedingRefactoring { get; set; }
    public int FilesWithDuplications { get; set; }
    public int TotalViolations { get; set; }
    public int TotalBugs { get; set; }
    public int TotalRefactorings { get; set; }
    public int TotalDuplications { get; set; }
    public int TotalDuplicatedLines { get; set; }
    public string ExecutionTime { get; set; } = string.Empty;
    public Dictionary<string, int> ViolationsBySeverity { get; set; } = new();
    public Dictionary<string, int> BugsBySeverity { get; set; } = new();
    public Dictionary<string, int> RefactoringsByPriority { get; set; } = new();
    public Dictionary<string, int> DuplicationsByImpact { get; set; } = new();
    public List<Violation> Violations { get; set; } = new();
    public List<Bug> Bugs { get; set; } = new();
    public List<Refactoring> Refactorings { get; set; } = new();
    public List<CodeDuplication> Duplications { get; set; } = new();
    public List<Standard> Standards { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public ProjectSummary? ProjectSummary { get; set; }
}
