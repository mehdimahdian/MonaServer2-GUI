#pragma once
#include <cstdint>
#include <string>

struct TransportStats {
	double   rtt_ms         = 0.0;
	double   send_rate_mbps = 0.0;
	double   packet_loss_pct = 0.0;
	uint64_t bytes_sent     = 0;
	int      reconnects     = 0;
};

/* Pure-virtual interface all transports implement */
class IMonaTransport {
public:
	virtual ~IMonaTransport() = default;

	virtual bool connect()    = 0;
	virtual void disconnect() = 0;
	virtual bool is_connected() const = 0;

	/* Returns false on unrecoverable send error */
	virtual bool send(const uint8_t *data, size_t len) = 0;

	virtual TransportStats stats() const { return {}; }
	virtual const char *protocol_name() const = 0;
};
