#include "whip-transport.h"
#include <obs-module.h>
#include <curl/curl.h>
#include <cstring>
#include <sstream>
#include <vector>

#define LOG_WHIP(lvl, fmt, ...) blog(lvl, "[mona-live/WHIP] " fmt, ##__VA_ARGS__)

/* ── libcurl write callback ───────────────────────────────────────────────── */
static size_t curl_write_cb(char *ptr, size_t size, size_t nmemb, void *userdata)
{
	auto *buf = reinterpret_cast<std::string *>(userdata);
	buf->append(ptr, size * nmemb);
	return size * nmemb;
}

static size_t curl_header_cb(char *buf, size_t size, size_t nitems, void *userdata)
{
	auto *location = reinterpret_cast<std::string *>(userdata);
	std::string h(buf, size * nitems);
	if (h.rfind("Location:", 0) == 0) {
		auto val = h.substr(9);
		/* trim whitespace / CR LF */
		while (!val.empty() && (val.back() == '\r' || val.back() == '\n' || val.back() == ' '))
			val.pop_back();
		if (!val.empty() && val.front() == ' ') val = val.substr(1);
		*location = val;
	}
	return size * nitems;
}

/* ── SDP offer template ───────────────────────────────────────────────────── */
std::string WhipTransport::build_sdp_offer() const
{
	/* Minimal SDP for H.264 (PT 96) + AAC (PT 97) send-only */
	std::ostringstream s;
	s << "v=0\r\n"
	  << "o=- 0 0 IN IP4 127.0.0.1\r\n"
	  << "s=Mona Live\r\n"
	  << "t=0 0\r\n"
	  << "a=group:BUNDLE 0 1\r\n"
	  /* Video */
	  << "m=video 9 UDP/TLS/RTP/SAVPF 96\r\n"
	  << "c=IN IP4 0.0.0.0\r\n"
	  << "a=setup:actpass\r\n"
	  << "a=ice-ufrag:monaobs\r\n"
	  << "a=ice-pwd:monaobspassword\r\n"
	  << "a=mid:0\r\n"
	  << "a=sendonly\r\n"
	  << "a=rtcp-mux\r\n"
	  << "a=rtpmap:96 H264/90000\r\n"
	  << "a=fmtp:96 level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42001f\r\n"
#ifdef MONA_WHIP_DATACHANNEL
	  << "a=ssrc:" << v_ssrc_ << " cname:monaobs\r\n"
#endif
	  /* Audio */
	  << "m=audio 9 UDP/TLS/RTP/SAVPF 97\r\n"
	  << "c=IN IP4 0.0.0.0\r\n"
	  << "a=setup:actpass\r\n"
	  << "a=ice-ufrag:monaobs\r\n"
	  << "a=ice-pwd:monaobspassword\r\n"
	  << "a=mid:1\r\n"
	  << "a=sendonly\r\n"
	  << "a=rtcp-mux\r\n"
	  << "a=rtpmap:97 opus/48000/2\r\n"
#ifdef MONA_WHIP_DATACHANNEL
	  << "a=ssrc:" << a_ssrc_ << " cname:monaobs\r\n"
#endif
	  ;
	return s.str();
}

/* ── WHIP HTTP signalling ─────────────────────────────────────────────────── */
bool WhipTransport::do_whip_signalling()
{
	CURL *curl = curl_easy_init();
	if (!curl) return false;

	std::string offer  = build_sdp_offer();
	std::string answer;
	std::string location;

	struct curl_slist *headers = nullptr;
	headers = curl_slist_append(headers, "Content-Type: application/sdp");
	if (!bearer_token_.empty()) {
		std::string auth = "Authorization: Bearer " + bearer_token_;
		headers = curl_slist_append(headers, auth.c_str());
	}

	curl_easy_setopt(curl, CURLOPT_URL, endpoint_url_.c_str());
	curl_easy_setopt(curl, CURLOPT_POST, 1L);
	curl_easy_setopt(curl, CURLOPT_POSTFIELDS, offer.c_str());
	curl_easy_setopt(curl, CURLOPT_POSTFIELDSIZE, (long)offer.size());
	curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, curl_write_cb);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, &answer);
	curl_easy_setopt(curl, CURLOPT_HEADERFUNCTION, curl_header_cb);
	curl_easy_setopt(curl, CURLOPT_HEADERDATA, &location);
	curl_easy_setopt(curl, CURLOPT_TIMEOUT, 10L);
	curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);

	CURLcode res = curl_easy_perform(curl);
	long http_code = 0;
	curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &http_code);
	curl_slist_free_all(headers);
	curl_easy_cleanup(curl);

	if (res != CURLE_OK) {
		LOG_WHIP(LOG_ERROR, "curl error: %s", curl_easy_strerror(res));
		return false;
	}
	if (http_code != 201) {
		LOG_WHIP(LOG_ERROR, "WHIP endpoint returned HTTP %ld (expected 201)", http_code);
		return false;
	}

	resource_url_ = location;
	LOG_WHIP(LOG_INFO, "WHIP signalling OK — resource: %s", resource_url_.c_str());

#ifdef MONA_WHIP_DATACHANNEL
	return do_ice_connect(answer);
#else
	LOG_WHIP(LOG_INFO, "libdatachannel not linked — WebRTC ICE/DTLS skipped (server-side bridge active)");
	return true;
#endif
}

#ifdef MONA_WHIP_DATACHANNEL
bool WhipTransport::do_ice_connect(const std::string &sdp_answer)
{
	rtcConfiguration cfg{};
	cfg.iceServers    = nullptr;
	cfg.iceServersCount = 0;

	pc_ = rtcCreatePeerConnection(&cfg);
	if (!pc_) { LOG_WHIP(LOG_ERROR, "rtcCreatePeerConnection failed"); return false; }

	/* Add video track */
	rtcTrackInit vtrack{};
	vtrack.direction     = RTC_DIRECTION_SENDONLY;
	vtrack.codec         = RTC_CODEC_H264;
	vtrack.payloadType   = 96;
	vtrack.ssrc          = v_ssrc_;
	vtrack.name          = "video";
	vtrack.msid          = "monaobs";
	vtrack.trackId       = "video";
	vt_ = rtcAddTrackEx(pc_, &vtrack);

	/* Add audio track */
	rtcTrackInit atrack{};
	atrack.direction   = RTC_DIRECTION_SENDONLY;
	atrack.codec       = RTC_CODEC_OPUS;
	atrack.payloadType = 97;
	atrack.ssrc        = a_ssrc_;
	atrack.name        = "audio";
	atrack.msid        = "monaobs";
	atrack.trackId     = "audio";
	at_ = rtcAddTrackEx(pc_, &atrack);

	/* Apply remote SDP answer */
	rtcSetRemoteDescription(pc_, sdp_answer.c_str(), "answer");

	LOG_WHIP(LOG_INFO, "ICE/DTLS handshake initiated");
	return true;
}
#else
bool WhipTransport::do_ice_connect(const std::string &) { return true; }
#endif

/* ── Public interface ─────────────────────────────────────────────────────── */
WhipTransport::WhipTransport(std::string endpoint_url, std::string bearer_token)
	: endpoint_url_(std::move(endpoint_url))
	, bearer_token_(std::move(bearer_token))
{}

WhipTransport::~WhipTransport()
{
	disconnect();
}

bool WhipTransport::connect()
{
	if (!do_whip_signalling()) return false;
	connected_ = true;
	return true;
}

void WhipTransport::disconnect()
{
	if (!connected_) return;
	connected_ = false;

#ifdef MONA_WHIP_DATACHANNEL
	if (vt_) { rtcDeleteTrack(vt_); vt_ = nullptr; }
	if (at_) { rtcDeleteTrack(at_); at_ = nullptr; }
	if (pc_) { rtcDeletePeerConnection(pc_); pc_ = nullptr; }
#endif

	/* DELETE the WHIP resource to signal end-of-stream */
	if (!resource_url_.empty()) {
		CURL *curl = curl_easy_init();
		if (curl) {
			curl_easy_setopt(curl, CURLOPT_URL, resource_url_.c_str());
			curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, "DELETE");
			curl_easy_setopt(curl, CURLOPT_TIMEOUT, 5L);
			curl_easy_perform(curl);
			curl_easy_cleanup(curl);
		}
		resource_url_.clear();
	}

	LOG_WHIP(LOG_INFO, "Disconnected");
}

bool WhipTransport::send(const uint8_t *data, size_t len)
{
#ifdef MONA_WHIP_DATACHANNEL
	/*
	 * In a full implementation, packetise the H.264/OPUS data into RTP packets
	 * and call rtcSendMessage(vt_, ...) or rtcSendMessage(at_, ...).
	 * The mona-output.cpp layer provides raw NAL-unit H.264 from the encoder_packet.
	 */
	(void)data; (void)len;
	return connected_.load();
#else
	/* Without libdatachannel the actual media goes over SRT (server bridges it) */
	(void)data; (void)len;
	return connected_.load();
#endif
}
