#pragma once
#include "ws-client.h"
#include <string>
#include <functional>
#include <unordered_map>

/*
 * PTZ / drone control channel.
 *
 * Listens for inbound JSON commands from MonaServer2 GUI and dispatches
 * them to OBS camera-control API or forwards raw PTZ-over-IP.
 *
 * Inbound message format:
 * { "type": "ptz", "cmd": "pan",    "value": 0.5  }   // -1.0 .. 1.0
 * { "type": "ptz", "cmd": "tilt",   "value": -0.3 }
 * { "type": "ptz", "cmd": "zoom",   "value": 0.8  }   // 0.0 .. 1.0
 * { "type": "ptz", "cmd": "preset", "value": 1    }   // recall preset N
 * { "type": "ptz", "cmd": "stop"                  }
 *
 * For drone telemetry (upstream):
 * { "type": "drone_telemetry", "lat": ..., "lng": ..., "alt": ..., "heading": ... }
 */

struct PtzCommand {
	std::string cmd;
	double      value = 0.0;
};

using PtzHandler = std::function<void(const PtzCommand &)>;

class PtzChannel {
public:
	explicit PtzChannel(WsClient *ws);

	/* Register a handler for PTZ commands (called on WS receive thread) */
	void set_handler(PtzHandler h) { handler_ = std::move(h); }

	/* Wire PTZ handler into the WsClient message callback */
	void attach();

	/* Send drone telemetry upstream */
	void send_drone_telemetry(double lat, double lng, double alt_m, double heading_deg);

private:
	void on_message(const std::string &json);

	WsClient  *ws_;
	PtzHandler handler_;
};
