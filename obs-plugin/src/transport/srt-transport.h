#pragma once
#include "transport.h"
#include <srt/srt.h>
#include <string>
#include <atomic>

/*
 * SRT transport — ultra-low-latency (20-200 ms configurable).
 * Wraps libsrt in live mode, sending MPEG-TS payload.
 * Reports RTT, packet-loss, and bandwidth via srt_bstats().
 */
class SrtTransport : public IMonaTransport {
public:
	SrtTransport(std::string host, int port, int latency_ms, int max_bw_kbps);
	~SrtTransport() override;

	bool connect()    override;
	void disconnect() override;
	bool is_connected() const override { return sock_ != SRT_INVALID_SOCK && connected_; }
	bool send(const uint8_t *data, size_t len) override;
	TransportStats stats() const override;
	const char *protocol_name() const override { return "SRT"; }

private:
	void apply_options();

	std::string        host_;
	int                port_;
	int                latency_ms_;
	int                max_bw_kbps_;
	SRTSOCKET          sock_      = SRT_INVALID_SOCK;
	std::atomic<bool>  connected_ = false;
};
