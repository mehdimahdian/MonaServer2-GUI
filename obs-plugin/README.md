# Mona Live Output for OBS Studio

Ultra-low-latency WebRTC/SRT output plugin for OBS Studio, streaming via MonaServer2.

**Author:** Mehdi Mahdian ([m.mahdian@gmail.com](mailto:m.mahdian@gmail.com))  
**Repository:** <https://github.com/mehdimahdian/MonaServer2-GUI>  
Powered by MonaServer2 (MonaSolutions / Haivision) — GPL-2.0.

## Features

| Feature | Transport | Notes |
|---|---|---|
| **Native WebRTC output** | WHIP | Requires `libdatachannel`; server-side bridging otherwise |
| **Ultra-low latency** | SRT | 20–50 ms configurable; default transport |
| **Browser playback** | WebRTC (server) | MonaServer2 distributes to browsers via WebRTC |
| **SRT fallback** | SRT→RTMP | Automatic protocol negotiation |
| **Multi-bitrate ABR** | SRT×2 | Second SRT stream on port+1 at configurable low bitrate |
| **Remote control** | WebSocket | Scene switch, record start/stop via GUI |
| **Telemetry** | WebSocket | RTT, bitrate, packet loss, FPS, dropped frames — 1 s interval |
| **PTZ / drone control** | WebSocket | Bidirectional pan/tilt/zoom + drone GPS telemetry |

## Architecture

```
OBS Studio
  └── obs-mona-live.dll
        ├── [SRT/WHIP/RTMP transport] ────→ MonaServer2 → WebRTC/HLS → browsers
        └── [WebSocket control] ──────────→ MonaServer2 GUI Service
                                                ├── Remote control commands
                                                ├── Telemetry dashboard
                                                └── PTZ / drone telemetry
```

## Building

### Prerequisites

- CMake ≥ 3.28
- OBS Studio source (or pre-built SDK via obs-deps)
- libsrt ≥ 1.4  (`vcpkg install libsrt` or system package)
- libcurl        (`vcpkg install curl`)
- *(optional)* libdatachannel for native WebRTC: `vcpkg install libdatachannel`

### Windows (x64)

```bat
cmake -S . -B build ^
  -DCMAKE_BUILD_TYPE=Release ^
  -Dlibobs_DIR="C:/Program Files/obs-studio/cmake" ^
  -DCMAKE_TOOLCHAIN_FILE="C:/vcpkg/scripts/buildsystems/vcpkg.cmake"

cmake --build build --config Release
cmake --install build --config Release
```

### Linux

```bash
cmake -S . -B build \
  -DCMAKE_BUILD_TYPE=Release \
  -Dlibobs_DIR=/usr/lib/cmake/libobs

cmake --build build -j$(nproc)
cmake --install build
```

### macOS

```bash
cmake -S . -B build \
  -DCMAKE_BUILD_TYPE=Release \
  -Dlibobs_DIR="$(brew --prefix obs-studio)/lib/cmake/libobs"

cmake --build build
cmake --install build
```

## Settings

| Field | Default | Description |
|---|---|---|
| Protocol | SRT | WHIP / SRT / RTMP |
| Server URL | `srt://localhost:4900` | MonaServer2 SRT ingest endpoint |
| Stream Key | `live` | Identifies this stream |
| GUI Service URL | `http://localhost:8080` | MonaServer2 GUI for control/telemetry |
| SRT Latency | 20 ms | 20 for LAN, 120+ for WAN |
| Video Bitrate | 2500 kbps | Target encode bitrate |
| Multi-Bitrate ABR | off | Second stream on port+1 |
| Remote Control | on | Allow GUI to control OBS |
| Telemetry | on | Send stats to GUI every 1 s |
| PTZ / Drone | off | Bidirectional camera/drone control |

## MonaServer2 GUI Service Endpoints

| Endpoint | Description |
|---|---|
| `GET  /api/obs/status` | Current OBS plugin session state |
| `POST /api/obs/command` | Send remote command to OBS |
| `POST /api/obs/ptz` | Send PTZ command to camera |
| `POST /api/obs/scene?name=X` | Switch OBS scene |
| `POST /api/obs/record/start` | Start OBS recording |
| `POST /api/obs/record/stop` | Stop OBS recording |
| `POST /api/obs/whip/{key}` | WHIP ingest signalling endpoint |
| `WS   /hub/obs-control` | Real-time control + telemetry hub |

## WHIP (WebRTC) Notes

When `libdatachannel` is linked, the plugin performs a full WHIP negotiation:
1. HTTP POST SDP offer → `/api/obs/whip/{streamKey}`
2. MonaServer2 GUI proxies to MonaServer2's WebRTC ingest
3. ICE/DTLS handshake via libdatachannel
4. SRTP media flows directly to MonaServer2

Without `libdatachannel`, the SRT transport carries the media and MonaServer2 re-encapsulates it as WebRTC for browser delivery. End-to-end latency: ~30–70 ms.

## License

GPL-2.0 (matching OBS Studio and MonaServer2).
