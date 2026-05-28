#include "telemetry.h"
#include <obs-module.h>
#include <chrono>
#include <sstream>
#include <iomanip>
#include <ctime>

TelemetryReporter::TelemetryReporter(WsClient *ws, std::string stream_key, std::string transport_name)
	: ws_(ws), stream_key_(std::move(stream_key)), transport_name_(std::move(transport_name))
{}

TelemetryReporter::~TelemetryReporter() { stop(); }

void TelemetryReporter::start()
{
	running_ = true;
	thread_  = std::thread(&TelemetryReporter::report_loop, this);
}

void TelemetryReporter::stop()
{
	running_ = false;
	if (thread_.joinable()) thread_.join();
}

std::string TelemetryReporter::build_json(const TransportStats &ts, double fps, int dropped) const
{
	auto now = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
	std::ostringstream j;
	j << std::fixed << std::setprecision(2);
	j << "{"
	  << "\"type\":\"telemetry\","
	  << "\"plugin\":\"obs-mona-live\","
	  << "\"stream_key\":\"" << stream_key_ << "\","
	  << "\"transport\":\"" << transport_name_ << "\","
	  << "\"rtt_ms\":"       << ts.rtt_ms       << ","
	  << "\"bitrate_mbps\":" << ts.send_rate_mbps << ","
	  << "\"pkt_loss_pct\":" << ts.packet_loss_pct << ","
	  << "\"bytes_sent\":"   << ts.bytes_sent    << ","
	  << "\"fps_out\":"      << fps              << ","
	  << "\"dropped_frames\":" << dropped        << ","
	  << "\"ts\":"           << (long long)now
	  << "}";
	return j.str();
}

void TelemetryReporter::report_loop()
{
	while (running_) {
		std::this_thread::sleep_for(std::chrono::seconds(1));
		if (!running_) break;

		TransportStats ts = stats_fn_ ? stats_fn_() : TransportStats{};
		double fps = 0.0;
		int    dropped = 0;
		if (obs_fn_) obs_fn_(fps, dropped);

		if (ws_ && ws_->is_connected()) {
			std::string msg = build_json(ts, fps, dropped);
			ws_->send_text(msg);
		}
	}
}
