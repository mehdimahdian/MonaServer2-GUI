using Microsoft.AspNetCore.SignalR;

namespace MonaServer2.Service.Hubs;

public class MonitorHub : Hub
{
    // Clients subscribe to this hub; the Worker broadcasts to all clients.
    // Outbound messages: StatusChanged, PublicationsUpdated, SessionsUpdated, LogReceived
    // No inbound RPC methods needed — control goes through REST controllers.

    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }
}
