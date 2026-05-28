#include "ptz-channel.h"
#include <obs-module.h>
#include <sstream>
#include <cstring>
#include <cstdlib>

#define LOG_PTZ(lvl,fmt,...) blog(lvl,"[mona-live/PTZ] " fmt,##__VA_ARGS__)

/* Minimal JSON field extractor — avoids a full JSON library dependency */
static bool json_get_string(const std::string &json, const char *key, std::string &out)
{
	std::string search = std::string("\"") + key + "\":\"";
	auto pos = json.find(search);
	if (pos == std::string::npos) return false;
	pos += search.size();
	auto end = json.find('"', pos);
	if (end == std::string::npos) return false;
	out = json.substr(pos, end - pos);
	return true;
}

static bool json_get_double(const std::string &json, const char *key, double &out)
{
	std::string search = std::string("\"") + key + "\":";
	auto pos = json.find(search);
	if (pos == std::string::npos) return false;
	pos += search.size();
	out = std::atof(json.c_str() + pos);
	return true;
}

PtzChannel::PtzChannel(WsClient *ws) : ws_(ws) {}

void PtzChannel::attach()
{
	ws_->on_message([this](const std::string &msg) { on_message(msg); });
}

void PtzChannel::on_message(const std::string &json)
{
	std::string type;
	if (!json_get_string(json, "type", type)) return;
	if (type != "ptz") return;

	PtzCommand cmd;
	json_get_string(json, "cmd", cmd.cmd);
	json_get_double(json, "value", cmd.value);

	LOG_PTZ(LOG_DEBUG, "Received: cmd=%s value=%.3f", cmd.cmd.c_str(), cmd.value);

	if (handler_) handler_(cmd);
}

void PtzChannel::send_drone_telemetry(double lat, double lng, double alt_m, double heading_deg)
{
	if (!ws_ || !ws_->is_connected()) return;
	std::ostringstream j;
	j << std::fixed;
	j << "{\"type\":\"drone_telemetry\","
	  << "\"lat\":"     << lat         << ","
	  << "\"lng\":"     << lng         << ","
	  << "\"alt_m\":"   << alt_m       << ","
	  << "\"heading\":" << heading_deg << "}";
	ws_->send_text(j.str());
}
