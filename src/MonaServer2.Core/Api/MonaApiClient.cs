using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonaServer2.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MonaServer2.Core.Api;

public class MonaApiOptions
{
    public string BaseUrl { get; set; } = "http://localhost:80";
    public string AdminPath { get; set; } = "/admin/api";
    public int TimeoutMs { get; set; } = 3000;
}

public class MonaApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MonaApiClient> _logger;
    private readonly MonaApiOptions _options;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MonaApiClient(HttpClient http, IOptions<MonaApiOptions> options, ILogger<MonaApiClient> logger)
    {
        _http = http;
        _logger = logger;
        _options = options.Value;
        _http.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);
        _http.BaseAddress = new Uri(_options.BaseUrl);
    }

    public async Task<ServerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync($"{_options.AdminPath}/status", ct).ConfigureAwait(false);
            var node = JsonNode.Parse(json);
            if (node is null) return new ServerStatus { IsRunning = false };

            return new ServerStatus
            {
                IsRunning = node["running"]?.GetValue<bool>() ?? false,
                Version = node["version"]?.GetValue<string>(),
                StartedAt = ParseDateTime(node["startedAt"]),
                TotalConnections = node["totalConnections"]?.GetValue<int>() ?? 0,
                TotalPublications = node["totalPublications"]?.GetValue<int>() ?? 0,
                TotalByteRateIn = node["byteRateIn"]?.GetValue<long>() ?? 0,
                TotalByteRateOut = node["byteRateOut"]?.GetValue<long>() ?? 0,
                ConnectionsByProtocol = ParseProtocolCounts(node["byProtocol"])
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "MonaServer2 API unavailable (status)");
            return new ServerStatus { IsRunning = false };
        }
    }

    public async Task<List<Publication>> GetPublicationsAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync($"{_options.AdminPath}/publications", ct).ConfigureAwait(false);
            var array = JsonSerializer.Deserialize<List<JsonNode>>(json, JsonOpts) ?? [];
            return array.Select(ParsePublication).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "MonaServer2 API unavailable (publications)");
            return [];
        }
    }

    public async Task<List<Session>> GetSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync($"{_options.AdminPath}/sessions", ct).ConfigureAwait(false);
            var array = JsonSerializer.Deserialize<List<JsonNode>>(json, JsonOpts) ?? [];
            return array.Select(ParseSession).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "MonaServer2 API unavailable (sessions)");
            return [];
        }
    }

    private static Publication ParsePublication(JsonNode? n)
    {
        if (n is null) return new Publication();
        return new Publication
        {
            Name = n["name"]?.GetValue<string>() ?? string.Empty,
            Protocol = n["protocol"]?.GetValue<string>() ?? string.Empty,
            ClientAddress = n["address"]?.GetValue<string>() ?? string.Empty,
            StartedAt = ParseDateTime(n["startedAt"]) ?? DateTime.UtcNow,
            SubscriberCount = n["subscribers"]?.GetValue<int>() ?? 0,
            IsRecording = n["recording"]?.GetValue<bool>() ?? false,
            RecordingPath = n["recordingPath"]?.GetValue<string>(),
            ByteRateIn = n["byteRateIn"]?.GetValue<long>() ?? 0,
            ByteRateOut = n["byteRateOut"]?.GetValue<long>() ?? 0,
            LostRateIn = n["lostRateIn"]?.GetValue<double>() ?? 0,
            LostRateOut = n["lostRateOut"]?.GetValue<double>() ?? 0,
            Tracks = ParseTracks(n["tracks"])
        };
    }

    private static Session ParseSession(JsonNode? n)
    {
        if (n is null) return new Session();
        return new Session
        {
            Id = n["id"]?.GetValue<string>() ?? string.Empty,
            Address = n["address"]?.GetValue<string>() ?? string.Empty,
            Protocol = n["protocol"]?.GetValue<string>() ?? string.Empty,
            ConnectedAt = ParseDateTime(n["connectedAt"]) ?? DateTime.UtcNow,
            PublishingTo = n["publishing"]?.GetValue<string>(),
            SubscribingTo = n["subscribing"]?.AsArray().Select(x => x?.GetValue<string>() ?? "").ToList() ?? [],
            ByteRateIn = n["byteRateIn"]?.GetValue<long>() ?? 0,
            ByteRateOut = n["byteRateOut"]?.GetValue<long>() ?? 0
        };
    }

    private static List<StreamTrack> ParseTracks(JsonNode? node)
    {
        if (node is not JsonArray arr) return [];
        return arr.Select(t => new StreamTrack
        {
            Type = (t?["type"]?.GetValue<string>()?.ToLower()) switch
            {
                "video" => TrackType.Video,
                "audio" => TrackType.Audio,
                _ => TrackType.Data
            },
            Codec = t?["codec"]?.GetValue<string>() ?? string.Empty,
            Language = t?["language"]?.GetValue<string>(),
            Width = t?["width"]?.GetValue<int>() ?? 0,
            Height = t?["height"]?.GetValue<int>() ?? 0,
            Fps = t?["fps"]?.GetValue<double>() ?? 0,
            SampleRate = t?["sampleRate"]?.GetValue<int>() ?? 0,
            Channels = t?["channels"]?.GetValue<int>() ?? 0,
            Bitrate = t?["bitrate"]?.GetValue<long>() ?? 0
        }).ToList();
    }

    private static DateTime? ParseDateTime(JsonNode? node)
    {
        if (node is null) return null;
        if (DateTime.TryParse(node.GetValue<string>(), out var dt))
            return dt.ToUniversalTime();
        return null;
    }

    private static Dictionary<string, int> ParseProtocolCounts(JsonNode? node)
    {
        if (node is not JsonObject obj) return [];
        return obj.ToDictionary(kv => kv.Key, kv => kv.Value?.GetValue<int>() ?? 0);
    }
}
