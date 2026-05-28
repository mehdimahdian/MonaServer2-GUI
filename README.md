# MonaServer2 GUI

[![Build](https://github.com/mehdimahdian/MonaServer2-GUI/actions/workflows/build.yml/badge.svg)](https://github.com/mehdimahdian/MonaServer2-GUI/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/mehdimahdian/MonaServer2-GUI?include_prereleases)](https://github.com/mehdimahdian/MonaServer2-GUI/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://github.com/mehdimahdian/MonaServer2-GUI/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

A cross-platform desktop management application, web dashboard, and OBS Studio plugin for [MonaServer2](https://github.com/MonaSolutions/MonaServer2) — the open-source, scriptable media server by MonaSolutions / Haivision.

> **This project is a GUI management wrapper.** All streaming, media, and protocol functionality is provided entirely by [MonaServer2](https://github.com/MonaSolutions/MonaServer2). MonaServer2 GUI does not reimplement any of MonaServer2's core features.

---

## What's Inside

| Component | Description |
|---|---|
| **Desktop App** | Avalonia UI — Windows, Linux, macOS |
| **Web Dashboard** | React SPA embedded in the service |
| **Companion Service** | ASP.NET Core — REST API + SignalR hub |
| **OBS Studio Plugin** | `obs-mona-live` — WebRTC/SRT native output |
| **Push Stream** | Push any video file or SMPTE calibration to MonaServer2 |
| **Binary Updater** | In-app download of MonaServer2 binaries from GitHub |

---

## Credits

**MonaServer2** is developed and maintained by [MonaSolutions](https://github.com/MonaSolutions), with backing from [Haivision](https://www.haivision.com/).

- Repository: <https://github.com/MonaSolutions/MonaServer2>
- License: [GPL-3.0](https://github.com/MonaSolutions/MonaServer2/blob/master/LICENSE)
- Protocols: RTMP, SRT, WebSocket, HTTP, RTMFP, HLS, WebRTC, and more

MonaServer2 GUI is licensed under **GPL-3.0** in accordance with MonaServer2's license terms.

---

## Features

### Desktop Application (Avalonia UI — cross-platform)

- **Dashboard** — Real-time server stats: uptime, active connections by protocol, bandwidth in/out, publications
- **Stream Manager** — View and manage active publications: codec, bitrate, resolution, subscriber count
- **Sessions** — Active connections table with protocol filter
- **Configuration Editor** — Visual and raw `MonaServer.ini` editor
- **Log Viewer** — Real-time streaming log with level filter, search, and download
- **Service Control** — Start/stop/restart MonaServer2, install as system service, in-app binary updater
- **Push Stream** — Stream a local video file or SMPTE calibration pattern (with project branding) via RTMP/SRT
- **OBS Plugin Setup** — Detect OBS Studio and install `obs-mona-live` with one click, or follow guided manual steps

### OBS Studio Plugin — `obs-mona-live`

| Feature | Details |
|---|---|
| Native WebRTC output | WHIP protocol; full ICE/DTLS with libdatachannel |
| Ultra-low latency | SRT at 20–50 ms configurable latency |
| Browser playback | Via MonaServer2 WebRTC relay |
| SRT fallback | Automatic protocol negotiation |
| Multi-bitrate ABR | Second SRT track at low bitrate for adaptive playback |
| Remote control channel | Scene switch, recording start/stop from GUI |
| Real-time telemetry | RTT, bitrate, packet loss, FPS dashboard |
| PTZ / drone control | Bidirectional camera pan/tilt/zoom + drone GPS telemetry |

### Web Dashboard (React + Vite + Tailwind — embedded in service)

- Full dashboard accessible from any browser on the local network
- Same capabilities as the desktop application

---

## Screenshots

> Screenshots coming with v1.0 stable release.

---

## Requirements

| Component | Minimum |
|---|---|
| .NET Runtime | 8.0 LTS |
| MonaServer2 binary | Bundled or user-configured |
| OBS Studio (optional) | 28.0+ (for OBS plugin) |
| FFmpeg (optional) | Any recent version (for Push Stream / preview) |
| Windows | 10/11 x64 |
| Linux | Ubuntu 22.04+ / Debian 12+ |
| macOS | 13 Ventura+ (Apple Silicon) |

---

## Installation

### Windows — Portable Archive

1. Download `MonaServer2-GUI-x.y.z-win-x64.zip` from [Releases](https://github.com/mehdimahdian/MonaServer2-GUI/releases)
2. Extract to a folder
3. Run `MonaServer2.Desktop.exe`
4. **Service Control → Start** to launch MonaServer2

### OBS Plugin

After installing the app, open **OBS Plugin** in the sidebar:

- **OBS detected** → click **Install / Reinstall Plugin** (automatic, one click)
- **OBS not detected** → expand **Manual Installation** and follow the step-by-step guide

### Linux / macOS

```bash
tar -xzf MonaServer2-GUI-x.y.z-linux-x64.tar.gz

# Start companion service (background)
./service/MonaServer2.Service &

# Launch desktop app
./desktop/MonaServer2.Desktop
```

---

## Quick Start

1. Launch the desktop app
2. **Service Control → Start** — MonaServer2 starts as a managed subprocess
3. **Dashboard** — view live stats
4. **Push Stream** — stream a file or SMPTE calibration test card
5. **OBS Plugin** — install the OBS output plugin
6. In OBS: `Settings → Stream → Service → Mona Live Output`
7. Set `Server URL: srt://localhost:4900` · `Stream Key: live` · click Start Streaming

---

## Architecture

```
MonaServer2 GUI (this project)
├── MonaServer2.Core       Shared models, INI parser, process wrapper, Lua API client
├── MonaServer2.Service    ASP.NET Core: REST API, SignalR hub, web UI host
├── MonaServer2.Desktop    Avalonia MVVM desktop application
├── webui/                 React + Vite + Tailwind SPA → embedded in Service
├── obs-plugin/            C++ OBS Studio output plugin (CMake + libsrt + libcurl)
│   ├── src/transport/     SRT, WHIP/WebRTC, RTMP transport implementations
│   ├── src/muxer/         MPEG-TS packetizer for SRT transport
│   └── src/control/       WebSocket client, telemetry reporter, PTZ channel
└── lua/www/admin/         Lua scripts → deployed inside MonaServer2's www/ directory

MonaServer2 binary (upstream, bundled in tools/monaserver2/)
└── Handles all streaming: RTMP, SRT, HTTP, WebSocket, HLS, WebRTC, recording
```

See [docs/architecture.md](docs/architecture.md) for the full design document.

---

## Building from Source

```bash
# Prerequisites: .NET 8 SDK, Node.js 20+, pnpm
git clone https://github.com/mehdimahdian/MonaServer2-GUI
cd MonaServer2-GUI

dotnet restore
cd webui && pnpm install && pnpm build && cd ..

# Run in development
dotnet run --project src/MonaServer2.Service   # terminal 1
dotnet run --project src/MonaServer2.Desktop   # terminal 2
```

### Build the OBS Plugin

```bash
cd obs-plugin

# Windows (requires libsrt + libcurl via vcpkg)
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_TOOLCHAIN_FILE="C:/vcpkg/scripts/buildsystems/vcpkg.cmake"
cmake --build build --config Release

# Linux / macOS
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build -j$(nproc)
```

See [obs-plugin/README.md](obs-plugin/README.md) for full OBS plugin build instructions.

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting.

- **Bug reports / feature requests**: [GitHub Issues](https://github.com/mehdimahdian/MonaServer2-GUI/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mehdimahdian/MonaServer2-GUI/discussions)
- **Security**: see [SECURITY.md](SECURITY.md)

---

## Author

**Mehdi Mahdian**  
[m.mahdian@gmail.com](mailto:m.mahdian@gmail.com) · [@mehdimahdian](https://github.com/mehdimahdian)

---

## License

This project is licensed under the **GNU General Public License v3.0** — see [LICENSE](LICENSE) for the full text.

GPL-3.0 is required because MonaServer2 (which this project wraps and bundles) is itself licensed under GPL-3.0.

---

## Acknowledgements

- [MonaSolutions](https://github.com/MonaSolutions) — creators and maintainers of MonaServer2
- [Haivision](https://www.haivision.com/) — sponsors of MonaServer2 development
- [AvaloniaUI](https://avaloniaui.net/) — cross-platform .NET UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM helpers for .NET
- [OBS Project](https://obsproject.com/) — open broadcaster software
- [libsrt / Haivision](https://github.com/Haivision/srt) — Secure Reliable Transport library
