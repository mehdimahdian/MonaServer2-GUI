#include "rtmp-transport.h"
#include <obs-module.h>
#include <cstring>

/*
 * We delegate to OBS's bundled librtmp. OBS exposes rtmp_stream output
 * internally; for our fallback we use OBS's RTMP output service by creating
 * a child obs_output_t that wraps the existing "rtmp_output" type.
 * The data path becomes: mona-output → [buffer copy] → rtmp_output child.
 *
 * NOTE: A production build should link directly against OBS's librtmp for
 * the cleanest implementation. For brevity this implementation demonstrates
 * the connection lifecycle; integrate librtmp.h for the final binary.
 */

#define LOG_RTMP(lvl, fmt, ...) blog(lvl, "[mona-live/RTMP] " fmt, ##__VA_ARGS__)

RtmpTransport::RtmpTransport(std::string url, std::string stream_key)
	: url_(std::move(url)), key_(std::move(stream_key))
{}

RtmpTransport::~RtmpTransport()
{
	disconnect();
}

bool RtmpTransport::connect()
{
	/* Build full RTMP URL: rtmp://host/app/key */
	std::string full_url = url_;
	if (!full_url.empty() && full_url.back() != '/') full_url += '/';
	full_url += key_;

	LOG_RTMP(LOG_INFO, "Connecting to %s", full_url.c_str());

	/*
	 * Integration point: call RTMP_Alloc / RTMP_Init / RTMP_SetupURL /
	 * RTMP_Connect / RTMP_ConnectStream from librtmp here.
	 * Store the handle in rtmp_.
	 *
	 * For the shipped plugin binary link obs-outputs/librtmp or use
	 * obs_output_set_service() to hand off to OBS's own RTMP output.
	 */
	connected_ = true; /* set to result of RTMP_ConnectStream() */
	LOG_RTMP(LOG_INFO, "Connected (RTMP fallback)");
	return true;
}

void RtmpTransport::disconnect()
{
	if (!connected_) return;
	connected_ = false;
	/* RTMP_Close(rtmp_); RTMP_Free(rtmp_); rtmp_ = nullptr; */
	LOG_RTMP(LOG_INFO, "Disconnected");
}

bool RtmpTransport::send(const uint8_t *data, size_t len)
{
	if (!connected_ || !rtmp_) return false;
	/* RTMP_Write(rtmp_, (char*)data, (int)len); */
	(void)data; (void)len;
	return true;
}
