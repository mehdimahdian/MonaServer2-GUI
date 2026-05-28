#include "srt-transport.h"
#include <obs-module.h>
#include <cstring>
#include <netdb.h>
#ifndef _WIN32
#include <sys/socket.h>
#include <arpa/inet.h>
#else
#include <winsock2.h>
#include <ws2tcpip.h>
#endif

#define LOG_SRT(lvl, fmt, ...) blog(lvl, "[mona-live/SRT] " fmt, ##__VA_ARGS__)

SrtTransport::SrtTransport(std::string host, int port, int latency_ms, int max_bw_kbps)
	: host_(std::move(host))
	, port_(port)
	, latency_ms_(latency_ms)
	, max_bw_kbps_(max_bw_kbps)
{
	srt_startup();
}

SrtTransport::~SrtTransport()
{
	disconnect();
	srt_cleanup();
}

void SrtTransport::apply_options()
{
	/* Live mode: send-only, no encryption */
	int sender = 1;
	srt_setsockopt(sock_, 0, SRTO_SENDER, &sender, sizeof(sender));

	/* Latency — caller should use 20 ms for LAN, 120+ for WAN */
	srt_setsockopt(sock_, 0, SRTO_LATENCY, &latency_ms_, sizeof(latency_ms_));
	srt_setsockopt(sock_, 0, SRTO_PEERLATENCY, &latency_ms_, sizeof(latency_ms_));

	/* Max bandwidth guard to prevent network saturation */
	int64_t max_bw = (int64_t)max_bw_kbps_ * 1000 / 8; /* bytes/s */
	srt_setsockopt(sock_, 0, SRTO_MAXBW, &max_bw, sizeof(max_bw));

	/* Payload type: MPEG-TS */
	int ptype = SRT_LIVE_DEF_PLSIZE;
	srt_setsockopt(sock_, 0, SRTO_PAYLOADSIZE, &ptype, sizeof(ptype));

	/* Timeout for connect */
	int conn_timeout = 5000;
	srt_setsockopt(sock_, 0, SRTO_CONNTIMEO, &conn_timeout, sizeof(conn_timeout));
}

bool SrtTransport::connect()
{
	if (sock_ != SRT_INVALID_SOCK)
		disconnect();

	sock_ = srt_create_socket();
	if (sock_ == SRT_INVALID_SOCK) {
		LOG_SRT(LOG_ERROR, "srt_create_socket failed: %s", srt_getlasterror_str());
		return false;
	}

	apply_options();

	/* Resolve host */
	struct addrinfo hints{}, *res = nullptr;
	hints.ai_family   = AF_UNSPEC;
	hints.ai_socktype = SOCK_DGRAM;
	std::string port_str = std::to_string(port_);

	if (getaddrinfo(host_.c_str(), port_str.c_str(), &hints, &res) != 0 || !res) {
		LOG_SRT(LOG_ERROR, "DNS resolution failed for %s", host_.c_str());
		srt_close(sock_);
		sock_ = SRT_INVALID_SOCK;
		return false;
	}

	int rc = srt_connect(sock_, res->ai_addr, (int)res->ai_addrlen);
	freeaddrinfo(res);

	if (rc == SRT_ERROR) {
		LOG_SRT(LOG_ERROR, "srt_connect to %s:%d failed: %s", host_.c_str(), port_, srt_getlasterror_str());
		srt_close(sock_);
		sock_ = SRT_INVALID_SOCK;
		return false;
	}

	connected_ = true;
	LOG_SRT(LOG_INFO, "Connected to %s:%d  latency=%dms  maxbw=%dkbps", host_.c_str(), port_, latency_ms_, max_bw_kbps_);
	return true;
}

void SrtTransport::disconnect()
{
	if (sock_ == SRT_INVALID_SOCK) return;
	connected_ = false;
	srt_close(sock_);
	sock_ = SRT_INVALID_SOCK;
	LOG_SRT(LOG_INFO, "Disconnected");
}

bool SrtTransport::send(const uint8_t *data, size_t len)
{
	if (!connected_ || sock_ == SRT_INVALID_SOCK) return false;

	/* SRT live mode: send in 1316-byte chunks (7 × 188-byte TS packets) */
	static constexpr size_t CHUNK = 1316;
	size_t offset = 0;

	while (offset < len) {
		size_t chunk = std::min(len - offset, CHUNK);
		int rc = srt_send(sock_, reinterpret_cast<const char *>(data + offset), (int)chunk);
		if (rc == SRT_ERROR) {
			int err = srt_getlasterror(nullptr);
			if (err == SRT_EASYNCSND || err == SRT_ENOCONN) {
				connected_ = false;
				LOG_SRT(LOG_WARNING, "Send failed — connection lost: %s", srt_getlasterror_str());
				return false;
			}
			LOG_SRT(LOG_WARNING, "srt_send warning: %s", srt_getlasterror_str());
		}
		offset += chunk;
	}
	return true;
}

TransportStats SrtTransport::stats() const
{
	if (!connected_ || sock_ == SRT_INVALID_SOCK) return {};

	SRT_TRACEBSTATS s{};
	if (srt_bstats(sock_, &s, 0) != 0) return {};

	TransportStats ts;
	ts.rtt_ms          = s.msRTT;
	ts.send_rate_mbps  = s.mbpsSendRate;
	ts.packet_loss_pct = (s.pktSndLoss > 0 && s.pktSent > 0)
	                         ? (double)s.pktSndLoss / (double)s.pktSent * 100.0
	                         : 0.0;
	ts.bytes_sent = (uint64_t)s.byteSent;
	return ts;
}
