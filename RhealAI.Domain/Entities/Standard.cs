namespace RhealAI.Domain.Entities;

/// <summary>
/// Represents a coding standard or architectural rule
/// </summary>
public class Standard
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TechStack { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public bool IsFromExistingDocs { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    // Enhanced properties for deeper analysis
    public string Rationale { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low
    public List<string> AppliesTo { get; set; } = new(); // Controllers, Services, Models, etc.
}
