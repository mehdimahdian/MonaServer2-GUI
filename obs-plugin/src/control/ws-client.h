#pragma once
#include <string>
#include <functional>
#include <thread>
#include <atomic>
#include <mutex>
#include <deque>
#include <cstdint>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
using socket_t = SOCKET;
#define INVALID_SOCK INVALID_SOCKET
#else
#include <sys/socket.h>
#include <netdb.h>
using socket_t = int;
#define INVALID_SOCK (-1)
#endif

/*
 * Minimal WebSocket client (RFC 6455) over plain TCP.
 * Used to maintain the remote-control channel between the OBS plugin
 * and the MonaServer2 GUI service (/hub/obs-control).
 *
 * Supports: text & binary frames, ping/pong, auto-reconnect.
 */
class WsClient {
public:
	using MessageCallback = std::function<void(const std::string &msg)>;
	using OpenCallback    = std::function<void()>;
	using CloseCallback   = std::function<void()>;

	WsClient(std::string host, int port, std::string path);
	~WsClient();

	void on_message(MessageCallback cb) { msg_cb_ = std::move(cb); }
	void on_open(OpenCallback cb)    { open_cb_ = std::move(cb); }
	void on_close(CloseCallback cb)  { close_cb_ = std::move(cb); }

	bool start();   /* starts background receive thread */
	void stop();
	bool send_text(const std::string &text);
	bool send_binary(const uint8_t *data, size_t len);
	bool is_connected() const { return connected_; }

private:
	bool tcp_connect();
	bool ws_handshake();
	void recv_loop();
	bool decode_frame(const uint8_t *buf, size_t buf_len, size_t &frame_len, std::string &payload, uint8_t &opcode);
	bool send_frame(uint8_t opcode, const uint8_t *data, size_t len);
	static std::string base64_encode(const uint8_t *in, size_t len);
	static std::string random_key();

	std::string host_;
	int         port_;
	std::string path_;

	socket_t          sock_ = INVALID_SOCK;
	std::atomic<bool> connected_{false};
	std::atomic<bool> running_{false};
	std::thread       recv_thread_;
	std::mutex        send_mutex_;

	MessageCallback msg_cb_;
	OpenCallback    open_cb_;
	CloseCallback   close_cb_;
};
