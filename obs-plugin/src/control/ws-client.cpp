#include "ws-client.h"
#include <obs-module.h>
#include <cstring>
#include <sstream>
#include <random>
#include <stdexcept>

#ifdef _WIN32
#pragma comment(lib,"ws2_32.lib")
#else
#include <unistd.h>
#include <fcntl.h>
#include <netinet/tcp.h>
static void closesocket(int s) { close(s); }
#endif

#define LOG_WS(lvl,fmt,...) blog(lvl,"[mona-live/WS] " fmt,##__VA_ARGS__)

/* ── Base64 for WS key ───────────────────────────────────────────────────── */
static const char b64chars[] =
	"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

std::string WsClient::base64_encode(const uint8_t *in, size_t len)
{
	std::string out;
	for (size_t i = 0; i < len; i += 3) {
		uint32_t b = ((uint32_t)in[i] << 16)
		           | (i+1 < len ? (uint32_t)in[i+1] << 8 : 0)
		           | (i+2 < len ? (uint32_t)in[i+2]     : 0);
		out += b64chars[(b >> 18) & 63];
		out += b64chars[(b >> 12) & 63];
		out += (i+1 < len) ? b64chars[(b >> 6) & 63] : '=';
		out += (i+2 < len) ? b64chars[b & 63]        : '=';
	}
	return out;
}

std::string WsClient::random_key()
{
	uint8_t raw[16];
	std::mt19937 rng(std::random_device{}());
	std::uniform_int_distribution<int> dist(0, 255);
	for (auto &b : raw) b = (uint8_t)dist(rng);
	return base64_encode(raw, 16);
}

/* ── Constructor / destructor ────────────────────────────────────────────── */
WsClient::WsClient(std::string host, int port, std::string path)
	: host_(std::move(host)), port_(port), path_(std::move(path))
{
#ifdef _WIN32
	WSADATA wd; WSAStartup(MAKEWORD(2,2), &wd);
#endif
}

WsClient::~WsClient() { stop(); }

/* ── TCP + WS handshake ──────────────────────────────────────────────────── */
bool WsClient::tcp_connect()
{
	struct addrinfo hints{}, *res;
	hints.ai_family   = AF_UNSPEC;
	hints.ai_socktype = SOCK_STREAM;
	std::string ps = std::to_string(port_);

	if (getaddrinfo(host_.c_str(), ps.c_str(), &hints, &res) != 0) {
		LOG_WS(LOG_WARNING, "DNS failed for %s", host_.c_str());
		return false;
	}
	sock_ = socket(res->ai_family, res->ai_socktype, res->ai_protocol);
	if (sock_ == INVALID_SOCK) { freeaddrinfo(res); return false; }

	int nodelay = 1;
#ifdef _WIN32
	setsockopt(sock_, IPPROTO_TCP, TCP_NODELAY, (char*)&nodelay, sizeof(nodelay));
#else
	setsockopt(sock_, IPPROTO_TCP, TCP_NODELAY, &nodelay, sizeof(nodelay));
#endif

	int rc = ::connect(sock_, res->ai_addr, (int)res->ai_addrlen);
	freeaddrinfo(res);
	if (rc != 0) { closesocket(sock_); sock_ = INVALID_SOCK; return false; }
	return true;
}

bool WsClient::ws_handshake()
{
	std::string key = random_key();
	std::ostringstream req;
	req << "GET " << path_ << " HTTP/1.1\r\n"
	    << "Host: " << host_ << ":" << port_ << "\r\n"
	    << "Upgrade: websocket\r\n"
	    << "Connection: Upgrade\r\n"
	    << "Sec-WebSocket-Key: " << key << "\r\n"
	    << "Sec-WebSocket-Version: 13\r\n"
	    << "\r\n";
	std::string s = req.str();

	if (send(sock_, s.c_str(), (int)s.size(), 0) < 0) return false;

	/* Read until we get the full HTTP response (ends with \r\n\r\n) */
	char buf[2048] = {};
	int  total = 0;
	while (total < (int)sizeof(buf) - 1) {
		int r = recv(sock_, buf + total, sizeof(buf) - total - 1, 0);
		if (r <= 0) return false;
		total += r;
		if (strstr(buf, "\r\n\r\n")) break;
	}
	return (strstr(buf, "101") != nullptr);
}

/* ── Frame encode / send ─────────────────────────────────────────────────── */
bool WsClient::send_frame(uint8_t opcode, const uint8_t *data, size_t len)
{
	std::lock_guard<std::mutex> lk(send_mutex_);

	/* Client frames must be masked (RFC 6455 §5.3) */
	std::mt19937 rng(std::random_device{}());
	uint32_t mask = rng();
	uint8_t  mask_key[4];
	mask_key[0] = (mask >> 24) & 0xFF;
	mask_key[1] = (mask >> 16) & 0xFF;
	mask_key[2] = (mask >>  8) & 0xFF;
	mask_key[3] =  mask        & 0xFF;

	std::vector<uint8_t> hdr;
	hdr.push_back(0x80 | (opcode & 0x0F)); /* FIN=1 */
	if (len < 126) {
		hdr.push_back(0x80 | (uint8_t)len);
	} else if (len < 65536) {
		hdr.push_back(0xFE);
		hdr.push_back((len >> 8) & 0xFF);
		hdr.push_back(len & 0xFF);
	} else {
		hdr.push_back(0xFF);
		for (int i = 7; i >= 0; i--) hdr.push_back((len >> (i*8)) & 0xFF);
	}
	hdr.insert(hdr.end(), mask_key, mask_key + 4);

	std::vector<uint8_t> masked(len);
	for (size_t i = 0; i < len; i++) masked[i] = data[i] ^ mask_key[i & 3];

	int r1 = send(sock_, (char*)hdr.data(),    (int)hdr.size(), 0);
	int r2 = send(sock_, (char*)masked.data(), (int)masked.size(), 0);
	return r1 >= 0 && r2 >= 0;
}

bool WsClient::send_text(const std::string &text)
{
	return send_frame(0x01, (const uint8_t *)text.c_str(), text.size());
}

bool WsClient::send_binary(const uint8_t *data, size_t len)
{
	return send_frame(0x02, data, len);
}

/* ── Receive loop ────────────────────────────────────────────────────────── */
bool WsClient::decode_frame(const uint8_t *buf, size_t buf_len, size_t &frame_len,
                              std::string &payload, uint8_t &opcode)
{
	if (buf_len < 2) return false;
	opcode = buf[0] & 0x0F;
	bool masked = (buf[1] & 0x80) != 0;
	uint64_t plen = buf[1] & 0x7F;
	size_t   hdr_size = 2;

	if (plen == 126) {
		if (buf_len < 4) return false;
		plen = ((uint64_t)buf[2] << 8) | buf[3];
		hdr_size = 4;
	} else if (plen == 127) {
		if (buf_len < 10) return false;
		plen = 0;
		for (int i = 0; i < 8; i++) plen = (plen << 8) | buf[2+i];
		hdr_size = 10;
	}

	if (masked) hdr_size += 4;
	frame_len = hdr_size + plen;
	if (buf_len < frame_len) return false;

	if (masked) {
		const uint8_t *mkey = buf + hdr_size - 4;
		payload.resize(plen);
		for (size_t i = 0; i < plen; i++)
			payload[i] = (char)(buf[hdr_size + i] ^ mkey[i & 3]);
	} else {
		payload.assign((const char *)(buf + hdr_size), plen);
	}
	return true;
}

void WsClient::recv_loop()
{
	std::vector<uint8_t> buf;
	buf.reserve(65536);
	uint8_t tmp[4096];

	while (running_) {
		int r = recv(sock_, (char *)tmp, sizeof(tmp), 0);
		if (r <= 0) { connected_ = false; break; }
		buf.insert(buf.end(), tmp, tmp + r);

		size_t offset = 0;
		while (offset < buf.size()) {
			size_t      frame_len;
			std::string payload;
			uint8_t     opcode;
			if (!decode_frame(buf.data() + offset, buf.size() - offset, frame_len, payload, opcode))
				break;
			offset += frame_len;

			if (opcode == 0x08) { connected_ = false; running_ = false; break; } /* close */
			if (opcode == 0x09) { send_frame(0x0A, nullptr, 0); }                /* ping → pong */
			if ((opcode == 0x01 || opcode == 0x02) && msg_cb_) msg_cb_(payload);
		}
		if (offset > 0) buf.erase(buf.begin(), buf.begin() + offset);
	}

	closesocket(sock_); sock_ = INVALID_SOCK;
	connected_ = false;
	if (close_cb_) close_cb_();
	LOG_WS(LOG_INFO, "Receive loop ended");
}

/* ── Public lifecycle ────────────────────────────────────────────────────── */
bool WsClient::start()
{
	if (!tcp_connect()) { LOG_WS(LOG_WARNING, "TCP connect failed"); return false; }
	if (!ws_handshake()) { closesocket(sock_); sock_ = INVALID_SOCK; LOG_WS(LOG_WARNING, "WS handshake failed"); return false; }
	connected_ = true;
	running_   = true;
	if (open_cb_) open_cb_();
	recv_thread_ = std::thread(&WsClient::recv_loop, this);
	LOG_WS(LOG_INFO, "Connected to %s:%d%s", host_.c_str(), port_, path_.c_str());
	return true;
}

void WsClient::stop()
{
	running_ = false;
	if (sock_ != INVALID_SOCK) { send_frame(0x08, nullptr, 0); closesocket(sock_); sock_ = INVALID_SOCK; }
	if (recv_thread_.joinable()) recv_thread_.join();
	connected_ = false;
}
