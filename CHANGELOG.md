# Changelog

All notable changes to MonaServer2 GUI are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)  
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [Unreleased] — v1.0.0

### Added

#### Core application
- `MonaServer2.Core` — shared models, INI config parser, process lifecycle manager, Lua HTTP API client
- `MonaServer2.Service` — ASP.NET Core companion service: REST API, SignalR hub, static web UI host
- `MonaServer2.Desktop` — Avalonia 11 cross-platform MVVM desktop application
- Web UI (React + Vite + Tailwind) embedded in the service, served at `http://localhost:8080`
- Lua admin API scripts for MonaServer2 `www/admin/` integration

#### Desktop pages
- **Dashboard** — real-time server stats via SignalR (uptime, connections by protocol, bandwidth, publications)
- **Streams** — active publications table with codec, bitrate, resolution, subscriber count
- **Sessions** — live connections table with protocol filter
- **Configuration** — visual editor + raw INI editor for `MonaServer.ini`
- **Logs** — real-time streamed log viewer with level filter, search, and download
- **Service Control** — start/stop/restart MonaServer2; install/uninstall as Windows Service / systemd / launchd

#### Push Stream
- Stream any local video file to MonaServer2 via RTMP/SRT (FFmpeg subprocess)
- SMPTE colour bars calibration pattern with live text overlays:
  - Project branding ("MonaServer2 GUI")
  - Stream key, timecode, resolution, bitrate
- Live preview panel: polls `/api/pusher/preview` at 1.5 s intervals, renders JPEG frame

#### Binary Updater
- Checks GitHub releases API for new MonaServer2 binaries
- Downloads with chunked progress bar via SignalR
- Extracts zip/tar.gz, preserves user config files (`*.ini`, `*.pem`)
- Sets executable bit on Linux/macOS
- Supports custom download URL for self-hosted or Haivision-distributed binaries

#### OBS Studio Plugin — `obs-mona-live`
- Full C++ OBS Studio output plugin (obs-plugintemplate CMake structure)
- **Transports**: SRT (libsrt, 20–2000 ms latency), WHIP/WebRTC (libdatachannel), RTMP fallback
- **MPEG-TS muxer**: hand-written PAT + PMT + H.264 PES + AAC PES packetiser (188-byte packets)
- **Multi-bitrate ABR**: second SRT connection at configurable low bitrate
- **Remote control channel**: RFC 6455 WebSocket client (no external WS library) → GUI service
- **Telemetry reporter**: RTT, bitrate, packet loss, FPS, dropped frames every 1 s via WebSocket
- **PTZ / drone control**: inbound JSON PTZ commands + outbound drone GPS telemetry
- OBS settings panel with all options (protocol, URL, latency, bitrate, ABR, telemetry, PTZ)
- Windows Inno Setup installer script
- Locale file (`en-US.ini`) with all UI strings and tooltips

#### OBS Plugin Setup (in-app)
- Auto-detects OBS Studio installation on Windows (registry + common paths), Linux, macOS
- One-click automatic plugin installation (copies DLL + data files)
- Step-by-step manual installation guide shown in-app when auto-install is unavailable
- Plugin status indicator (installed version, OBS path)

#### Service integration (C#)
- `OBSController` — REST: status, remote command, PTZ, scene switch, record, WHIP ingest proxy
- `OBSHub` — SignalR hub at `/hub/obs-control` for real-time plugin ↔ GUI control
- `PusherController` — REST: stream start/stop/status + single-frame JPEG preview endpoint
- `UpdateController` — REST: version check, install with progress via SignalR
- `StreamingController` — FFmpeg subprocess for Push Stream feature

#### GitHub Actions CI/CD
- `build.yml` — matrix build on Windows / Linux / macOS on every push and PR
- `release.yml` — publishes self-contained binaries for Windows x64, Linux x64, macOS ARM64 on tag push
- MonaServer2 version check — opens an issue when upstream releases a new binary

#### Infrastructure
- `.gitignore` — excludes binaries, build artifacts, OBS plugin build, node_modules, IDE files
- `Directory.Build.props` — shared MSBuild properties (TargetFramework, nullable, etc.)
- `Directory.Packages.props` — centralized NuGet package version management

---

[Unreleased]: https://github.com/mehdimahdian/MonaServer2-GUI/compare/HEAD...HEAD
