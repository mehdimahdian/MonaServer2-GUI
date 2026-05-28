# Development Guide

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| .NET SDK | 8.0 LTS | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Node.js | 20 LTS | https://nodejs.org/ |
| pnpm | 9.x | `npm install -g pnpm` |
| MonaServer2 binary | Latest | https://github.com/MonaSolutions/MonaServer2/releases |

## First-Time Setup

```bash
git clone https://github.com/mehdimahdian/MonaServer2-GUI
cd MonaServer2-GUI

# Restore .NET packages
dotnet restore

# Install and build the web UI
cd webui
pnpm install
pnpm build
cd ..

# Place MonaServer2 binary
mkdir -p tools/monaserver2
# Copy MonaServer.exe (Windows) or MonaServer (Linux/macOS) into tools/monaserver2/
# Also create tools/monaserver2/MonaServer.ini (copy from MonaServer2 repo)
```

## Running in Development

### Option A: Run Service + Desktop separately (recommended)

```bash
# Terminal 1: Service (API + web UI on localhost:8080)
dotnet run --project src/MonaServer2.Service

# Terminal 2: Desktop app
dotnet run --project src/MonaServer2.Desktop

# Terminal 3: Web UI dev server (optional, for hot-reload)
cd webui && pnpm dev
```

In web UI dev mode, Vite proxies `/api` and `/hub` to `localhost:8080`.

### Option B: Service only (web UI)

```bash
dotnet run --project src/MonaServer2.Service
# Open http://localhost:8080 in browser
```

## Project Layout

```
src/
  MonaServer2.Core/
    Models/           Publication, Session, ServerStatus, LogEntry, StreamTrack
    Config/           IniParser, MonaServerConfig (POCO for MonaServer.ini)
    Process/          MonaServerProcess, IMonaServerProcess
    Api/              MonaApiClient, IMonaApiClient

  MonaServer2.Service/
    Controllers/      StatusController, PublicationsController, SessionsController,
                      ConfigController, LogsController, ProcessController
    Hubs/             MonitorHub (SignalR)
    Worker.cs         Background loop: process management + stat polling + log relay
    Program.cs        Host builder, DI, Kestrel, middleware
    appsettings.json

  MonaServer2.Desktop/
    ViewModels/       MainWindowViewModel (nav root), DashboardViewModel,
                      StreamsViewModel, SessionsViewModel, ConfigViewModel,
                      LogsViewModel, ServiceViewModel
    Views/            MainWindow.axaml, DashboardView.axaml, StreamsView.axaml,
                      SessionsView.axaml, ConfigView.axaml, LogsView.axaml,
                      ServiceView.axaml
    Services/         ApiService (HTTP), SignalRService (real-time)
    Converters/       BoolToColorConverter, BytesToHumanConverter
    App.axaml         Theme + DataTemplates (ViewModel → View mapping)
    Program.cs        Entry point

webui/
  src/
    components/       Dashboard, Streams, Sessions, Logs, Config (React)
    hooks/            useApi, useSignalR
    types/            Publication, Session, ServerStatus (TypeScript mirrors of C# models)
  vite.config.ts      Proxy /api and /hub to localhost:8080

lua/
  www/admin/
    main.lua          Entry point; routes /admin/api/* requests
    api/
      status.lua      Server uptime, connection counts, protocol breakdown
      publications.lua Active publications with track metadata
      sessions.lua    Active sessions/connections

tools/
  monaserver2/
    MonaServer.exe    (not committed, downloaded separately)
    VERSION           Currently bundled MonaServer2 version tag

tests/
  MonaServer2.Core.Tests/   Unit tests for IniParser, MonaApiClient response parsing
```

## Adding a New Feature

### New API endpoint

1. Add model to `MonaServer2.Core/Models/`
2. Add method to `IMonaApiClient` and implement in `MonaApiClient`
3. Add controller to `MonaServer2.Service/Controllers/`
4. Call the endpoint from `ApiService.cs` in Desktop
5. Bind in the relevant ViewModel
6. Wire into the View

### New Desktop page

1. Create `ViewModels/MyPageViewModel.cs` (inherits `ObservableObject`)
2. Create `Views/MyPageView.axaml` + `.axaml.cs`
3. Register DataTemplate in `App.axaml`
4. Add navigation entry in `MainWindowViewModel`
5. Add nav item in `MainWindow.axaml`

## Building for Release

```bash
# Build web UI first
cd webui && pnpm build && cd ..

# The build copies dist/ to src/MonaServer2.Service/wwwroot/

# Publish all targets
dotnet publish src/MonaServer2.Service -r win-x64 -c Release --self-contained
dotnet publish src/MonaServer2.Desktop -r win-x64 -c Release --self-contained
```

## Code Style

- C# 12, nullable enabled, implicit usings
- XAML: 2-space indent, kebab-case resource keys
- TypeScript: 2-space indent, strict mode
- Run `dotnet format` before pushing

## Lua Script Development

Place the Lua scripts from `lua/www/admin/` into your MonaServer2 `www/admin/` directory. MonaServer2 serves them automatically at `http://<host>/admin/api/*`.

Test with:
```bash
curl http://localhost:80/admin/api/status
curl http://localhost:80/admin/api/publications
curl http://localhost:80/admin/api/sessions
```
