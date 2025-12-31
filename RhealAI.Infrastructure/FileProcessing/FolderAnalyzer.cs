using RhealAI.Domain.Entities;

namespace RhealAI.Infrastructure.FileProcessing;

/// <summary>
/// Service for analyzing folders and directories
/// </summary>
public class FolderAnalyzer
{
    /// <summary>
    /// Analyzes a folder and returns list of code files
    /// </summary>
    public async Task<List<CodeFile>> AnalyzeFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        var files = new List<CodeFile>();
        await ProcessDirectoryAsync(folderPath, folderPath, files);

        return files;
    }

    private async Task ProcessDirectoryAsync(string directoryPath, string basePath, List<CodeFile> files)
    {
        foreach (var filePath in Directory.GetFiles(directoryPath))
        {
            var relativePath = Path.GetRelativePath(basePath, filePath);

            if (FileAnalyzer.ShouldIgnoreFile(relativePath))
                continue;

            var fileType = FileAnalyzer.GetFileType(filePath);
            if (fileType == Domain.Enums.FileType.Unknown)
                continue;

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
            }
            catch (Exception)
            {
                // Skip files that can't be read
                continue;
            }
        }

        foreach (var subDir in Directory.GetDirectories(directoryPath))
        {
            var dirName = Path.GetFileName(subDir);
            if (!FileAnalyzer.ShouldIgnoreFile(dirName))
            {
                await ProcessDirectoryAsync(subDir, basePath, files);
            }
        }
    }
}
