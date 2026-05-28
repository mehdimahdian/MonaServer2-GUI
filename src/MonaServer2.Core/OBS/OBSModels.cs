namespace MonaServer2.Core.OBS;

public record OBSPluginRegistration
{
    public string Plugin { get; init; } = "";
    public string Version { get; init; } = "";
    public string StreamKey { get; init; } = "";
    public string Transport { get; init; } = "";
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
}

public record OBSTelemetry
{
    public string StreamKey { get; init; } = "";
    public string Transport { get; init; } = "";
    public double RttMs { get; init; }
    public double BitrateMbps { get; init; }
    public double PktLossPct { get; init; }
    public long BytesSent { get; init; }
    public double FpsOut { get; init; }
    public int DroppedFrames { get; init; }
    public long Ts { get; init; }
}

public record OBSRemoteCommand
{
    public string Command { get; init; } = "";
    public string? Parameter { get; init; }
}

public record PtzCommand
{
    public string Cmd { get; init; } = "";
    public double Value { get; init; }
}

public record DroneTelemetry
{
    public double Lat { get; init; }
    public double Lng { get; init; }
    public double AltM { get; init; }
    public double Heading { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}

public record OBSSessionState
{
    public bool IsConnected { get; init; }
    public OBSPluginRegistration? Registration { get; init; }
    public OBSTelemetry? LastTelemetry { get; init; }
    public DroneTelemetry? LastDroneTelemetry { get; init; }
}
