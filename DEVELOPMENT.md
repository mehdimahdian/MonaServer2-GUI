# Development Reference — MonaServer2 GUI

## Project Overview

Cross-platform desktop + web management GUI for MonaServer2. MonaServer2 (C++) is the streaming backbone; this project is a .NET wrapper/manager. Never reimplement MonaServer2 features here — route everything through the MonaServer2 process.

## Solution Structure

```
MonaServer2-GUI.sln
├── src/MonaServer2.Core/       Shared library: models, INI parser, process manager, Lua API client
├── src/MonaServer2.Service/    ASP.NET Core Worker Service: REST API, SignalR, web UI host
├── src/MonaServer2.Desktop/    Avalonia UI cross-platform desktop app
├── webui/                      React + Vite + Tailwind SPA (builds into Service/wwwroot/)
└── lua/www/admin/              Lua scripts placed inside MonaServer2's www/ directory
```

## Build Commands

```bash
# Full build
dotnet restore && dotnet build

# Build web UI (must run before Service build in CI)
cd webui && pnpm install && pnpm build

# Run Service in dev mode
dotnet run --project src/MonaServer2.Service

# Run Desktop in dev mode
dotnet run --project src/MonaServer2.Desktop

# Run all tests
dotnet test

# Publish Windows self-contained
dotnet publish src/MonaServer2.Service -r win-x64 -c Release --self-contained
dotnet publish src/MonaServer2.Desktop -r win-x64 -c Release --self-contained
```

## Key Design Decisions

### MonaServer2 as Subprocess
MonaServer2 is managed as a child process, NOT embedded as a C++ library. This means:
- `MonaServerProcess.cs` uses `System.Diagnostics.Process`
- Updates to MonaServer2 only require swapping the binary
- No C++/CLI bridge maintenance

### Lua API Scripts
MonaServer2 exposes stats via Lua scripts we place in its `www/admin/` directory. These scripts respond to HTTP GET and emit JSON. The `MonaApiClient.cs` in Core calls these endpoints. When MonaServer2 doesn't have the Lua scripts deployed, the API client returns empty/defaults gracefully.

### Communication Flow
```
Desktop App → HTTP/SignalR → MonaServer2.Service → HTTP → Lua API in MonaServer2
                                                  → Process.StdOut (logs)
                                                  → INI file (config)
```

### Avalonia MVVM Pattern
- ViewModels inherit `ObservableObject` (CommunityToolkit.Mvvm)
- Navigation: `MainWindowViewModel.CurrentPage` holds the active ViewModel
- DataTemplates in `App.axaml` map each ViewModel type to its View
- DI via `Microsoft.Extensions.DependencyInjection` in `App.axaml.cs`

### SignalR Hub
`MonitorHub.cs` in the Service broadcasts:
- `LogReceived(LogEntry)` — new log line from MonaServer2 stdout
- `StatusChanged(ServerStatus)` — server state changes
- `PublicationsUpdated(List<Publication>)` — stream list refresh
- `SessionsUpdated(List<Session>)` — connection list refresh

## File Locations of Importance

- `src/MonaServer2.Core/Config/IniParser.cs` — reads/writes MonaServer.ini
- `src/MonaServer2.Core/Process/MonaServerProcess.cs` — process lifecycle
- `src/MonaServer2.Core/Api/MonaApiClient.cs` — Lua API HTTP client
- `src/MonaServer2.Service/Worker.cs` — main background loop (polls MonaServer2, broadcasts via SignalR)
- `src/MonaServer2.Service/Hubs/MonitorHub.cs` — SignalR hub
- `src/MonaServer2.Desktop/ViewModels/MainWindowViewModel.cs` — navigation root
- `src/MonaServer2.Desktop/Services/ApiService.cs` — all HTTP calls from desktop to service
- `lua/www/admin/main.lua` — Lua entry point, routes /admin/api/* requests

## Environment & Dependencies

- **.NET 8.0 LTS** — all projects target net8.0
- **Avalonia 11.x** — desktop UI framework
- **CommunityToolkit.Mvvm** — MVVM helpers
- **Microsoft.AspNetCore.SignalR.Client** — real-time in desktop app
- **Node.js 20+ / pnpm** — web UI toolchain
- **MonaServer2 binary** — bundled in `tools/monaserver2/`

## Platform Notes

- Windows service: `Microsoft.Extensions.Hosting.WindowsServices` — `UseWindowsService()`
- Linux service: `Microsoft.Extensions.Hosting.Systemd` — `UseSystemd()`
- macOS: launchd plist installed by the desktop app on first run
- Config path defaults: Windows `%APPDATA%\MonaServer2-GUI\`, Linux `~/.config/monaserver2-gui/`, macOS `~/Library/Application Support/MonaServer2-GUI/`

## Coding Standards

- C# 12, `enable` Nullable, implicit usings
- No comments unless the WHY is non-obvious
- Controllers are thin — logic lives in Core or Worker
- Never use `Thread.Sleep` — use `Task.Delay` + CancellationToken
- Prefer records for immutable data models
- All API responses use PascalCase JSON (System.Text.Json default)

## Testing

- Unit tests in `tests/MonaServer2.Core.Tests/` (xUnit)
- Integration tests in `tests/MonaServer2.Service.Tests/` (WebApplicationFactory)
- No mocking of MonaServer2 internals — test the INI parser, API client response parsing, process lifecycle separately

## MonaServer2 Credits

Always acknowledge MonaServer2 (MonaSolutions/Haivision) in any user-facing text. The project is GPL-3.0 because MonaServer2 is GPL-3.0.
