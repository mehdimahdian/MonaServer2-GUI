#pragma once
#include "../transport/transport.h"
#include "ws-client.h"
#include <string>
#include <atomic>
#include <thread>
#include <functional>

/*
 * Telemetry reporter — sends JSON stats to MonaServer2 GUI every second.
 *
 * Payload (sent as WS text):
 * {
 *   "type":       "telemetry",
 *   "plugin":     "obs-mona-live",
 *   "stream_key": "...",
 *   "transport":  "SRT",
 *   "rtt_ms":     12.4,
 *   "bitrate_mbps": 2.47,
 *   "pkt_loss_pct": 0.0,
 *   "bytes_sent": 12345678,
 *   "fps_out":    29.97,
 *   "dropped_frames": 0,
 *   "ts":         1716000000
 * }
 */

using StatsProvider = std::function<TransportStats()>;
using ObsStatsProvider = std::function<void(double &fps, int &dropped)>;

class TelemetryReporter {
public:
	TelemetryReporter(WsClient *ws, std::string stream_key, std::string transport_name);
	~TelemetryReporter();

	void set_stats_provider(StatsProvider p)  { stats_fn_ = std::move(p); }
	void set_obs_stats(ObsStatsProvider p)     { obs_fn_ = std::move(p); }

	void start();
	void stop();

private:
	void report_loop();
	std::string build_json(const TransportStats &ts, double fps, int dropped) const;

	WsClient         *ws_;
	std::string       stream_key_;
	std::string       transport_name_;
	StatsProvider     stats_fn_;
	ObsStatsProvider  obs_fn_;
	std::atomic<bool> running_{false};
	std::thread       thread_;
};
