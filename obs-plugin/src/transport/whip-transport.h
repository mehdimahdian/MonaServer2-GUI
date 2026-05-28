#pragma once
#include "transport.h"
#include <string>
#include <atomic>

#ifdef MONA_WHIP_DATACHANNEL
#include <rtc/rtc.h>   /* libdatachannel C API */
#endif

/*
 * WHIP (WebRTC HTTP Ingest Protocol) transport.
 *
 * Flow:
 *  1. HTTP POST SDP offer to <whip_endpoint>
 *  2. Receive SDP answer (HTTP 201 Created)
 *  3. Perform ICE gathering + DTLS handshake (via libdatachannel when available)
 *  4. Send RTP packets directly to the ICE candidate address
 *
 * When libdatachannel is NOT available, falls back to signalling MonaServer2
 * to bridge the SRT stream as a WebRTC source (server-side bridging).
 *
 * WHIP is defined in: https://www.ietf.org/archive/id/draft-ietf-wish-whip-01.txt
 */
class WhipTransport : public IMonaTransport {
public:
	WhipTransport(std::string endpoint_url, std::string bearer_token);
	~WhipTransport() override;

	bool connect()    override;
	void disconnect() override;
	bool is_connected() const override { return connected_; }
	bool send(const uint8_t *data, size_t len) override;
	const char *protocol_name() const override { return "WHIP/WebRTC"; }

	/* Returns the bearer URL to delete the WHIP resource on stop */
	const std::string &resource_url() const { return resource_url_; }

private:
	bool do_whip_signalling();
	bool do_ice_connect(const std::string &sdp_answer);
	std::string build_sdp_offer() const;

	std::string        endpoint_url_;
	std::string        bearer_token_;
	std::string        resource_url_;   /* Location: header from 201 response */
	std::atomic<bool>  connected_{false};

#ifdef MONA_WHIP_DATACHANNEL
	rtcPeerConnection  *pc_  = nullptr;
	rtcTrack           *vt_  = nullptr; /* video track */
	rtcTrack           *at_  = nullptr; /* audio track */
	uint32_t            v_ssrc_ = 0x12345;
	uint32_t            a_ssrc_ = 0x67890;
#endif
};
