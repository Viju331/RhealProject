using RhealAI.Domain.Entities;

namespace RhealAI.Infrastructure.FileProcessing;

/// <summary>
/// Analyzes folder structure and identifies architectural patterns
/// </summary>
public class FolderStructureAnalyzer
{
    public async Task<FolderStructure> BuildStructureAsync(List<CodeFile> files)
    {
        var structure = new FolderStructure();
        
        foreach (var file in files)
        {
            var folderPath = Path.GetDirectoryName(file.FilePath) ?? "";
            
            if (!structure.Folders.ContainsKey(folderPath))
            {
                structure.Folders[folderPath] = new FolderInfo
                {
                    Path = folderPath,
                    Files = new List<CodeFile>()
                };
            }
            
            structure.Folders[folderPath].Files.Add(file);
        }
        
        // Identify component groups
        structure.ComponentGroups = await IdentifyComponentGroups(structure);
        
        // Generate suggestions
        structure.Suggestions = GenerateStructureSuggestions(structure);
        
        return structure;
    }
    
    private Task<List<ComponentGroup>> IdentifyComponentGroups(FolderStructure structure)
    {
        var groups = new List<ComponentGroup>();
        
        foreach (var folder in structure.Folders.Values)
        {
            var componentFiles = folder.Files
                .Where(f => f.FileName.Contains(".component.") || 
                           f.FileName.Contains("Controller") ||
                           f.FileName.Contains("Service"))
                .ToList();
            
            if (componentFiles.Count > 0)
            {
                var group = new ComponentGroup
                {
                    FolderPath = folder.Path,
                    Components = componentFiles
                };
                
                groups.Add(group);
            }
        }
        
        return Task.FromResult(groups);
    }
    
    private List<string> GenerateStructureSuggestions(FolderStructure structure)
    {
        var suggestions = new List<string>();
        
        // Check for large folders
        var largeFolders = structure.Folders.Values
            .Where(f => f.Files.Count > 20)
            .ToList();
        
        if (largeFolders.Any())
        {
            suggestions.Add($"Consider breaking down large folders: {string.Join(", ", largeFolders.Select(f => Path.GetFileName(f.Path)))}");
        }
        
        // Check for feature-based organization
        var hasFeaturesFolder = structure.Folders.Keys
            .Any(k => k.Contains("features", StringComparison.OrdinalIgnoreCase));
        
        if (!hasFeaturesFolder && structure.Folders.Count > 10)
        {
            suggestions.Add("Consider organizing code by features for better scalability");
        }
        
        return suggestions;
    }
}

public class FolderStructure
{
    public Dictionary<string, FolderInfo> Folders { get; set; } = new();
    public List<ComponentGroup> ComponentGroups { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

public class FolderInfo
{
    public string Path { get; set; } = string.Empty;
    public List<CodeFile> Files { get; set; } = new();
}

public class ComponentGroup
{
    public string FolderPath { get; set; } = string.Empty;
    public List<CodeFile> Components { get; set; } = new();
}
