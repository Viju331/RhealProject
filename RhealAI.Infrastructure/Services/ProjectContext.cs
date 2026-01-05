using RhealAI.Domain.Entities;

namespace RhealAI.Infrastructure.Services;

/// <summary>
/// Maintains comprehensive project context across all analysis steps
/// </summary>
public class ProjectContext
{
    // Step 1: Structure Analysis Results
    public string ProjectName { get; set; } = "";
    public Dictionary<string, int> FolderStructure { get; set; } = new();
    public List<ProjectModule> Modules { get; set; } = new();
    public Dictionary<string, string> FileLanguageMap { get; set; } = new();
    public Dictionary<string, List<string>> FileDependencies { get; set; } = new();
    
    // Step 2: Coding Standards Summary
    public Dictionary<string, string> CodingPatterns { get; set; } = new();
    public string ErrorHandlingApproach { get; set; } = "";
    public string NamingConventions { get; set; } = "";
    public string ArchitecturalPattern { get; set; } = "";
    public List<string> CommonLibraries { get; set; } = new();
    
    // Step 3: Project Understanding
    public Dictionary<string, string> ComponentPurposes { get; set; } = new();
    public Dictionary<string, List<string>> MethodInventory { get; set; } = new();
    public Dictionary<string, string> ApiEndpoints { get; set; } = new();
    public Dictionary<string, string> ServiceMethods { get; set; } = new();
    public Dictionary<string, string> DataLayerQueries { get; set; } = new();
    
    // Analysis Metadata
    public DateTime AnalysisStartTime { get; set; } = DateTime.UtcNow;
    public int TotalFiles { get; set; }
    public int TotalLines { get; set; }
}

/// <summary>
/// Represents a project module (e.g., API, Web, Infrastructure)
/// </summary>
public class ProjectModule
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // "API", "UI", "Library"
    public string Path { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public string Purpose { get; set; } = "";
}
