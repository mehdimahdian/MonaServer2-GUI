# Architecture

## Overview

MonaServer2 GUI is a management layer on top of MonaServer2. It does not reimplement any streaming functionality.

```
┌─────────────────────────────────────────────────────────────┐
│  MonaServer2.Desktop  (Avalonia UI)                          │
│  Windows / Linux / macOS — native cross-platform app         │
│                                                              │
│  Dashboard · Streams · Sessions · Config · Logs · Service    │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTP REST + SignalR WebSocket
                           │ localhost:8080
┌──────────────────────────▼──────────────────────────────────┐
│  MonaServer2.Service  (ASP.NET Core Worker Service)          │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐  │
│  │  REST API    │  │  SignalR Hub │  │  Static Web UI    │  │
│  │  /api/*      │  │  /hub/monitor│  │  (React SPA)      │  │
│  └──────────────┘  └──────────────┘  └───────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │  Worker (background loop)                             │    │
│  │  - Polls MonaServer2 Lua API for stats               │    │
│  │  - Reads stdout/stderr for log streaming             │    │
│  │  - Broadcasts updates via SignalR                    │    │
│  └──────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌─────────────────────┐  ┌─────────────────────────────┐   │
│  │  MonaServerProcess  │  │  MonaApiClient              │   │
│  │  - Start/Stop/Kill  │  │  - GET /admin/api/status    │   │
│  │  - StdOut → logs    │  │  - GET /admin/api/pubs      │   │
│  │  - Crash detection  │  │  - GET /admin/api/sessions  │   │
│  └──────────┬──────────┘  └───────────────┬─────────────┘   │
└─────────────┼─────────────────────────────┼─────────────────┘
              │ child process               │ HTTP
              ▼                             ▼
┌─────────────────────────────────────────────────────────────┐
│  MonaServer2  (native binary — bundled in tools/)            │
│                                                              │
│  RTMP :1935  ·  SRT :9710  ·  HTTP :80  ·  WS :80           │
│                                                              │
│  www/admin/main.lua         ← our Lua API scripts            │
│  www/admin/api/status.lua                                    │
│  www/admin/api/pubs.lua                                      │
│  www/admin/api/sessions.lua                                  │
└─────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

### MonaServer2.Core

Shared library with no platform-specific code:

- **Models** — `Publication`, `Session`, `ServerStatus`, `LogEntry`, `StreamTrack`, `MonaServerConfig`
- **IniParser** — Read/write `MonaServer.ini` using regex-based section/key parsing
- **MonaServerProcess** — Wraps `System.Diagnostics.Process`; exposes `Start/Stop/Restart`, `IsRunning`, `LogReceived` event, crash detection loop
- **MonaApiClient** — `HttpClient`-based client for the Lua API endpoints; returns empty models gracefully when MonaServer2 isn't running or Lua scripts aren't deployed

### MonaServer2.Service

ASP.NET Core 8 Web Application running as a Worker Service:

- **`Program.cs`** — Configures Kestrel, CORS, SignalR, Swagger, static files (React SPA), Windows/Linux service integration
- **`Worker.cs`** — `BackgroundService` that starts MonaServer2, polls for stats every 2 seconds, forwards log lines in real time
- **Controllers** — Thin REST controllers; business logic lives in Core
- **`MonitorHub.cs`** — SignalR hub; clients subscribe for push notifications
- **Static files** — React SPA served from `wwwroot/` (built by CI into this directory)

### MonaServer2.Desktop

Avalonia 11 cross-platform UI using MVVM (CommunityToolkit.Mvvm):

- DI container configured in `App.axaml.cs`
- Navigation via `MainWindowViewModel.CurrentPage` (ViewModel → View via DataTemplates)
- `ApiService` — wraps all HTTP calls to the companion service
- `SignalRService` — maintains a SignalR connection; routes messages to ViewModels
- Views/ViewModels: Dashboard, Streams, Sessions, Config, Logs, Service

### Lua API Scripts (deployed to MonaServer2)

MonaServer2 runs Lua scripts in its `www/` directory. Our scripts expose JSON endpoints at `/admin/api/*`.

The Worker polls these from within the same machine (`http://localhost:<monaPort>/admin/api/*`).

## Data Flow: Real-Time Logs

```
MonaServer2 stdout/stderr
    → MonaServerProcess.LogReceived event
    → Worker.cs handles event
    → IHubContext<MonitorHub>.Clients.All.LogReceived(entry)
    → Desktop SignalRService receives message
    → LogsViewModel appends to observable collection
    → LogsView renders in real time
```

## Data Flow: Stream Stats

```
Worker timer (every 2s)
    → MonaApiClient.GetPublicationsAsync()
    → HTTP GET http://localhost:80/admin/api/publications
    → Lua script queries MonaServer2 internal state
    → Returns JSON array
    → Worker broadcasts via SignalR: PublicationsUpdated(list)
    → StreamsViewModel updates observable collection
```

## Service Installation

### Windows
```
MonaServer2.Service.exe install   # registers as Windows Service
sc start MonaServer2Service
```
Uses `Microsoft.Extensions.Hosting.WindowsServices.UseWindowsService()`.

### Linux (Phase 2)
Systemd unit file generated on install. Uses `UseSystemd()`.

### macOS (Phase 3)
launchd plist written to `~/Library/LaunchAgents/`.

## MonaServer2 Bundling Strategy

The `tools/monaserver2/` directory contains the bundled MonaServer2 binary and a `VERSION` file. On startup, `MonaServerProcess` resolves the binary path:

1. Check user-configured path (persisted in app settings)
2. Check `tools/monaserver2/MonaServer[.exe]` relative to the service executable
3. Check `PATH`

When a new MonaServer2 release is detected (by the GitHub Action or in-app updater), only the binary in `tools/monaserver2/` is replaced — no code changes required.
