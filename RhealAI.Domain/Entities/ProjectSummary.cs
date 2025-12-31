namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a comprehensive project analysis summary
/// </summary>
public class ProjectSummary
{
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TechnologyStack { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string BusinessLogic { get; set; } = string.Empty;
    public string CoreFunctionality { get; set; } = string.Empty;
    public List<string> KeyFeatures { get; set; } = new();
    public Dictionary<string, int> FolderStructure { get; set; } = new();
    public Dictionary<string, int> FileTypeDistribution { get; set; } = new();
    public List<string> MainComponents { get; set; } = new();
    public string PrimaryLanguage { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
}
