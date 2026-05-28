#pragma once
#include "transport.h"
#include <string>
#include <atomic>
#include <cstdio>

/*
 * RTMP fallback transport using OBS's bundled librtmp.
 * Used when neither WHIP nor SRT is reachable.
 * Sends FLV-wrapped H.264+AAC the same way OBS's built-in RTMP output does.
 */
class RtmpTransport : public IMonaTransport {
public:
	RtmpTransport(std::string url, std::string stream_key);
	~RtmpTransport() override;

	bool connect()    override;
	void disconnect() override;
	bool is_connected() const override { return connected_; }
	bool send(const uint8_t *data, size_t len) override;
	const char *protocol_name() const override { return "RTMP"; }

private:
	std::string       url_;
	std::string       key_;
	std::atomic<bool> connected_{false};

	/* opaque librtmp handle — declared void* to avoid pulling in librtmp headers */
	void *rtmp_ = nullptr;
};
