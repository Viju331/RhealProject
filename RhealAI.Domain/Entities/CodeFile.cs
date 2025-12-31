using RhealAI.Domain.Enums;

namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a code file in the repository
/// </summary>
public class CodeFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public FileType FileType { get; set; }
    public string Content { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public long SizeInBytes { get; set; }
    public List<Violation> Violations { get; set; } = new();
    public List<Bug> Bugs { get; set; } = new();
}
