using Microsoft.AspNetCore.SignalR;

namespace RhealAI.API.Hubs;

/// <summary>
/// SignalR Hub for real-time upload progress updates
/// </summary>
public class UploadProgressHub : Hub
{
    public async Task SendProgress(string connectionId, int progress, string message)
    {
        await Clients.Client(connectionId).SendAsync("ReceiveProgress", progress, message);
    }
}
