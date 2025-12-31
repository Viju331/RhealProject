using System.IO.Compression;
using RhealAI.Domain.Entities;

namespace RhealAI.Infrastructure.FileProcessing;

/// <summary>
/// Service for extracting and processing ZIP files
/// </summary>
public class ZipExtractor
{
    private readonly string _tempDirectory;

    public ZipExtractor()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "RhealAI");
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Extracts a ZIP file and returns list of code files
    /// </summary>
    public async Task<(string ExtractPath, List<CodeFile> Files)> ExtractAndAnalyzeAsync(Stream zipStream, string fileName)
    {
        var extractPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(extractPath);

        // Extract ZIP
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            archive.ExtractToDirectory(extractPath);
        }

        // Analyze files
        var files = new List<CodeFile>();
        await ProcessDirectoryAsync(extractPath, extractPath, files);

        return (extractPath, files);
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

        foreach (var subDir in Directory.GetDirectories(directoryPath))
        {
            var relativeDirPath = Path.GetRelativePath(basePath, subDir);
            if (!FileAnalyzer.ShouldIgnoreFile(relativeDirPath))
            {
                await ProcessDirectoryAsync(subDir, basePath, files);
            }
        }
    }

    /// <summary>
    /// Cleans up temporary extraction directory
    /// </summary>
    public void CleanupExtraction(string extractPath)
    {
        try
        {
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
