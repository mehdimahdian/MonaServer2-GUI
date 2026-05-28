#pragma once
#include <obs-module.h>
#include <util/deque.h>
#include <util/threading.h>
#include <string>
#include <atomic>
#include <memory>
#include <cstdint>

#include "transport/transport.h"
#include "control/telemetry.h"
#include "control/ptz-channel.h"
#include "control/ws-client.h"

/* ── Transport protocol selection ───────────────────────────────────────────── */
enum class MonaProtocol { WHIP = 0, SRT = 1, RTMP = 2 };

/* ── Per-bitrate track (multi-bitrate ABR) ───────────────────────────────────  */
struct MonaTrack {
	int         index;
	int         video_bitrate_kbps;
	std::string stream_key;
	std::unique_ptr<IMonaTransport> transport;
};

/* ── Main output context ─────────────────────────────────────────────────────  */
struct MonaOutput {
	obs_output_t *output = nullptr;

	/* settings */
	MonaProtocol protocol        = MonaProtocol::SRT;
	std::string  server_url;        /* e.g. srt://host:4900  or  rtmp://host/live */
	std::string  stream_key;
	std::string  gui_service_url;   /* MonaServer2 GUI service, e.g. http://localhost:8080 */
	int          srt_latency_ms     = 20;
	int          video_bitrate_kbps = 2500;
	bool         multibitrate       = false;
	int          low_bitrate_kbps   = 800;
	bool         enable_remote_ctrl = true;
	bool         enable_telemetry   = true;
	bool         enable_ptz         = false;

	/* runtime */
	std::atomic<bool> active{false};
	std::atomic<bool> stopping{false};

	/* packet queue for the send thread */
	pthread_mutex_t    packets_mutex = PTHREAD_MUTEX_INITIALIZER;
	deque              packets{};
	os_sem_t          *send_sem = nullptr;
	pthread_t          send_thread{};

	/* transports (index 0 = primary, 1 = low-bitrate if ABR) */
	std::unique_ptr<IMonaTransport> transport;
	std::unique_ptr<IMonaTransport> transport_low; /* ABR second track */

	/* control plane */
	std::unique_ptr<WsClient>        ws;
	std::unique_ptr<TelemetryReporter> telemetry;
	std::unique_ptr<PtzChannel>      ptz;

	uint64_t total_bytes_sent = 0;
	uint64_t connect_time_ms  = 0;
};

/* ── OBS API registration ─────────────────────────────────────────────────── */
void mona_output_register(void);
