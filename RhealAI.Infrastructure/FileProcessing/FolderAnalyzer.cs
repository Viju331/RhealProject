using RhealAI.Domain.Entities;

namespace RhealAI.Infrastructure.FileProcessing;

/// <summary>
/// Service for analyzing folders and directories with component-by-component analysis
/// </summary>
public class FolderAnalyzer
{
    private readonly FolderStructureAnalyzer _structureAnalyzer;

    public FolderAnalyzer()
    {
        _structureAnalyzer = new FolderStructureAnalyzer();
    }

    /// <summary>
    /// Analyzes a folder with component-by-component processing and returns list of code files
    /// </summary>
    public async Task<List<CodeFile>> AnalyzeFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        var files = new List<CodeFile>();
        
        // Process folder by folder, component by component
        await ProcessDirectoryByComponentAsync(folderPath, folderPath, files);
        
        // Build structure analysis and suggestions
        var structure = await _structureAnalyzer.BuildStructureAsync(files);
        
        // Log structure suggestions (can be used for reporting)
        LogStructureAnalysis(structure);

        return files;
    }

    private void LogStructureAnalysis(FolderStructure structure)
    {
        Console.WriteLine("\n=== Project Structure Analysis ===");
        Console.WriteLine($"Total Folders: {structure.Folders.Count}");
        Console.WriteLine($"Component Groups: {structure.ComponentGroups.Count}");
        
        if (structure.Suggestions.Any())
        {
            Console.WriteLine("\n=== Structure Suggestions ===");
            foreach (var suggestion in structure.Suggestions)
            {
                Console.WriteLine($"- {suggestion}");
            }
        }
    }

    /// <summary>
    /// Processes directory by grouping and reading components together
    /// </summary>
    private async Task ProcessDirectoryByComponentAsync(string directoryPath, string basePath, List<CodeFile> files)
    {
        // Get all files in current directory first
        var currentDirFiles = Directory.GetFiles(directoryPath).ToList();
        
        // Group related component files together (e.g., .ts, .html, .scss for same component)
        var componentGroups = GroupComponentFiles(currentDirFiles);
        
        // Process each component group together
        foreach (var componentGroup in componentGroups)
        {
            foreach (var filePath in componentGroup)
            {
                await ProcessSingleFileAsync(filePath, basePath, files);
            }
        }
        
        // Then recursively process subdirectories
        foreach (var subDir in Directory.GetDirectories(directoryPath))
        {
            var dirName = Path.GetFileName(subDir);
            if (!FileAnalyzer.ShouldIgnoreFile(dirName))
            {
                await ProcessDirectoryByComponentAsync(subDir, basePath, files);
            }
        }
    }

    /// <summary>
    /// Groups component files together (e.g., component.ts, component.html, component.scss)
    /// </summary>
    private List<List<string>> GroupComponentFiles(List<string> filePaths)
    {
        var groups = new List<List<string>>();
        var processed = new HashSet<string>();
        
        foreach (var filePath in filePaths)
        {
            if (processed.Contains(filePath))
                continue;
                
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? "";
            
            // Find all related files for this component
            var relatedFiles = filePaths.Where(f => 
            {
                var fName = Path.GetFileNameWithoutExtension(f);
                var fDir = Path.GetDirectoryName(f) ?? "";
                return fDir == directory && 
                       (fName == fileName || fName.StartsWith(fileName + "."));
            }).ToList();
            
            if (relatedFiles.Any())
            {
                groups.Add(relatedFiles);
                foreach (var file in relatedFiles)
                {
                    processed.Add(file);
                }
            }
        }
        
        return groups;
    }

    private async Task ProcessSingleFileAsync(string filePath, string basePath, List<CodeFile> files)
    {
        var relativePath = Path.GetRelativePath(basePath, filePath);

        if (FileAnalyzer.ShouldIgnoreFile(relativePath))
            return;

        var fileType = FileAnalyzer.GetFileType(filePath);
        if (fileType == Domain.Enums.FileType.Unknown)
            return;

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var fileInfo = new FileInfo(filePath);

            files.Add(new CodeFile
            {
                FilePath = relativePath,
                FileName = Path.GetFileName(filePath),
                FileType = fileType,
                Content = content,
                LineCount = content.Split('\n').Length,
                SizeInBytes = fileInfo.Length
            });
            
            Console.WriteLine($"Analyzed: {relativePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skipped {relativePath}: {ex.Message}");
        }
    }

}
