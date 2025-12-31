using Microsoft.AspNetCore.SignalR;
using RhealAI.Application.Interfaces;

namespace RhealAI.API.Services;

/// <summary>
/// Implementation of IProgressHub using SignalR
/// </summary>
public class SignalRProgressHub : IProgressHub
{
    private readonly IHubContext<Hubs.UploadProgressHub> _hubContext;

    public SignalRProgressHub(IHubContext<Hubs.UploadProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendProgressAsync(string connectionId, int progress, string message)
    {
        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", progress, message);
    }
}
