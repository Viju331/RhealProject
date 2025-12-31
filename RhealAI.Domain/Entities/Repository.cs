namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents an uploaded repository for analysis
/// </summary>
public class Repository
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string UploadPath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long SizeInBytes { get; set; }
    public List<CodeFile> Files { get; set; } = new();
    public bool HasExistingStandards { get; set; }
    public List<Standard> Standards { get; set; } = new();
}
