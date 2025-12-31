namespace RhealAI.Application.Interfaces;

/// <summary>
/// Interface for sending progress updates via SignalR
/// </summary>
public interface IProgressHub
{
    Task SendProgressAsync(string connectionId, int progress, string message);
}
