using Microsoft.AspNetCore.SignalR;
using MonaServer2.Core.OBS;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MonaServer2.Service.Hubs;

/*
 * SignalR hub for the OBS plugin control channel.
 *
 * OBS plugin connects to /hub/obs-control as a WebSocket client.
 * MonaServer2 GUI Desktop connects as a normal SignalR client.
 *
 * Inbound from plugin:
 *   { "type": "register",    ...registration fields... }
 *   { "type": "telemetry",   ...stats... }
 *   { "type": "drone_telemetry", ... }
 *
 * Outbound to plugin:
 *   { "type": "remote_command", "command": "...", "parameter": "..." }
 *   { "type": "ptz",            "cmd": "...", "value": 0.0 }
 *
 * Outbound to GUI clients:
 *   OBSSessionUpdated(OBSSessionState)
 *   DroneTelemetryReceived(DroneTelemetry)
 */
public class OBSHub : Hub
{
    private static readonly ConcurrentDictionary<string, OBSPluginRegistration> _registrations = new();
    private static readonly ConcurrentDictionary<string, OBSTelemetry>          _telemetry      = new();
    private static DroneTelemetry? _lastDrone;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /* ── Inbound from OBS plugin (raw WS text messages) ──────────────────── */
    public async Task OnPluginMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("type", out var typeProp)) return;
        var type = typeProp.GetString();

        switch (type)
        {
            case "register":
                var reg = JsonSerializer.Deserialize<OBSPluginRegistration>(json, _jsonOpts);
                if (reg is not null)
                {
                    _registrations[Context.ConnectionId] = reg;
                    await BroadcastSessionAsync();
                }
                break;

            case "telemetry":
                var tel = JsonSerializer.Deserialize<OBSTelemetry>(json, _jsonOpts);
                if (tel is not null)
                {
                    _telemetry[Context.ConnectionId] = tel;
                    await Clients.Others.SendAsync("OBSTelemetryUpdated", tel);
                }
                break;

            case "drone_telemetry":
                var drone = JsonSerializer.Deserialize<DroneTelemetry>(json, _jsonOpts);
                if (drone is not null)
                {
                    _lastDrone = drone;
                    await Clients.Others.SendAsync("DroneTelemetryReceived", drone);
                }
                break;
        }
    }

    /* ── Outbound: GUI sends a remote command to the connected OBS plugin ── */
    public async Task SendRemoteCommand(string targetConnectionId, OBSRemoteCommand cmd)
    {
        var payload = JsonSerializer.Serialize(new { type = "remote_command", command = cmd.Command, parameter = cmd.Parameter });
        await Clients.Client(targetConnectionId).SendAsync("ReceiveMessage", payload);
    }

    /* ── Outbound: GUI sends PTZ command to OBS plugin ────────────────────── */
    public async Task SendPtzCommand(string targetConnectionId, PtzCommand ptz)
    {
        var payload = JsonSerializer.Serialize(new { type = "ptz", cmd = ptz.Cmd, value = ptz.Value });
        await Clients.Client(targetConnectionId).SendAsync("ReceiveMessage", payload);
    }

    /* ── Broadcast current session state to all GUI clients ──────────────── */
    private async Task BroadcastSessionAsync()
    {
        _registrations.TryGetValue(Context.ConnectionId, out var reg);
        _telemetry.TryGetValue(Context.ConnectionId, out var tel);

        var state = new OBSSessionState
        {
            IsConnected   = reg is not null,
            Registration  = reg,
            LastTelemetry = tel,
            LastDroneTelemetry = _lastDrone,
        };
        await Clients.Others.SendAsync("OBSSessionUpdated", state);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _registrations.TryRemove(Context.ConnectionId, out _);
        _telemetry.TryRemove(Context.ConnectionId, out _);
        await Clients.Others.SendAsync("OBSSessionUpdated", new OBSSessionState { IsConnected = false });
        await base.OnDisconnectedAsync(exception);
    }

    /* ── Query: GUI polls current state ───────────────────────────────────── */
    public static OBSSessionState GetCurrentState()
    {
        var reg = _registrations.Values.FirstOrDefault();
        var tel = _telemetry.Values.FirstOrDefault();
        return new OBSSessionState
        {
            IsConnected        = reg is not null,
            Registration       = reg,
            LastTelemetry      = tel,
            LastDroneTelemetry = _lastDrone,
        };
    }
}
