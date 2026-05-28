using Microsoft.Extensions.Options;
using MonaServer2.Core.Api;
using MonaServer2.Core.Process;
using MonaServer2.Core.Streaming;
using MonaServer2.Core.Update;
using MonaServer2.Service;
using MonaServer2.Service.Hubs;
using Serilog;
using MonaServer2.Core.OBS;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host
        .UseWindowsService(o => o.ServiceName = "MonaServer2GUI")
        .UseSystemd()
        .UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services));

    var monaSettings = builder.Configuration
        .GetSection("MonaServer")
        .Get<MonaServerSettings>() ?? new MonaServerSettings();

    builder.Services.Configure<MonaServerSettings>(builder.Configuration.GetSection("MonaServer"));
    builder.Services.Configure<ServiceConfiguration>(builder.Configuration.GetSection("Service"));
    builder.Services.Configure<UpdateSettings>(builder.Configuration.GetSection("Update"));
    builder.Services.Configure<StreamingSettings>(builder.Configuration.GetSection("Streaming"));
    builder.Services.Configure<MonaApiOptions>(o =>
    {
        o.BaseUrl = monaSettings.ApiBaseUrl;
        o.AdminPath = monaSettings.ApiAdminPath;
        o.TimeoutMs = monaSettings.ApiTimeoutMs;
    });

    builder.Services.AddSingleton<OBSDetectionService>();
    builder.Services.AddSingleton<MonaServerProcess>();
    builder.Services.AddSingleton<StreamingProcess>();
    builder.Services.AddHttpClient<MonaApiClient>();
    builder.Services.AddSingleton<MonaApiClient>();

    builder.Services.AddHttpClient("updater", c =>
    {
        c.DefaultRequestHeaders.Add("User-Agent", "MonaServer2-GUI/1.0");
        c.Timeout = TimeSpan.FromMinutes(15);
    });
    builder.Services.AddSingleton<BinaryUpdateService>(sp =>
    {
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("updater");
        var settings = sp.GetRequiredService<IOptions<UpdateSettings>>();
        var logger = sp.GetRequiredService<ILogger<BinaryUpdateService>>();
        return new BinaryUpdateService(http, settings, logger);
    });

    builder.Services.AddSignalR();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "MonaServer2 GUI API",
            Version = "v1",
            Description = "Management API for MonaServer2 GUI. Powered by MonaServer2 (MonaSolutions / Haivision)."
        });
    });

    var allowedOrigins = builder.Configuration
        .GetSection("Service:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    // Register Worker as singleton so ProcessController can inject it
    builder.Services.AddSingleton<Worker>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<Worker>());

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.MapControllers();
    app.MapHub<MonitorHub>("/hub/monitor");
    app.MapHub<OBSHub>("/hub/obs-control");

    // SPA fallback — serve index.html for unknown routes
    app.MapFallbackToFile("index.html");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MonaServer2 GUI service failed to start");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
