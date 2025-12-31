using RhealAI.Domain.Entities;

namespace RhealAI.Application.Interfaces;

/// <summary>
/// Service for analyzing documentation files (.md)
/// </summary>
public interface IDocumentationService
{
    Task<List<Standard>> ExtractStandardsFromMarkdownAsync(List<CodeFile> markdownFiles, string? connectionId = null);
    Task<List<Standard>> GenerateStandardsFromCodebaseAsync(List<CodeFile> codeFiles, string? connectionId = null);
}
