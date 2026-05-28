#include "mona-output.h"
#include "muxer/mpegts.h"
#include "transport/srt-transport.h"
#include "transport/rtmp-transport.h"
#include "transport/whip-transport.h"
#include <obs-module.h>
#include <obs-output.h>
#include <util/platform.h>
#include <util/threading.h>
#include <cstring>
#include <cassert>
#include <sstream>

#define LOG_OUT(lvl,fmt,...) blog(lvl,"[mona-live] " fmt,##__VA_ARGS__)

/* ── Settings keys ───────────────────────────────────────────────────────── */
#define S_PROTOCOL          "protocol"
#define S_SERVER_URL        "server_url"
#define S_STREAM_KEY        "stream_key"
#define S_GUI_SERVICE_URL   "gui_service_url"
#define S_SRT_LATENCY       "srt_latency_ms"
#define S_VIDEO_BITRATE     "video_bitrate_kbps"
#define S_MULTIBITRATE      "multibitrate"
#define S_LOW_BITRATE       "low_bitrate_kbps"
#define S_REMOTE_CTRL       "enable_remote_control"
#define S_TELEMETRY         "enable_telemetry"
#define S_PTZ               "enable_ptz"

/* ── Forward declarations ────────────────────────────────────────────────── */
static const char   *mona_get_name(void *);
static void         *mona_create(obs_data_t *, obs_output_t *);
static void          mona_destroy(void *);
static bool          mona_start(void *);
static void          mona_stop(void *, uint64_t);
static void          mona_encoded_packet(void *, struct encoder_packet *);
static obs_properties_t *mona_get_properties(void *);
static void          mona_get_defaults(obs_data_t *);
static float         mona_get_congestion(void *);
static int           mona_get_connect_time(void *);
static uint64_t      mona_get_total_bytes(void *);
static int           mona_get_dropped_frames(void *);
static void         *mona_send_thread(void *);

/* ── Output info struct ──────────────────────────────────────────────────── */
static struct obs_output_info mona_output_info = {
	.id                   = "mona_live_output",
	.flags                = OBS_OUTPUT_AV | OBS_OUTPUT_ENCODED,
	.encoded_video_codecs = "h264",
	.encoded_audio_codecs = "aac",
	.get_name             = mona_get_name,
	.create               = mona_create,
	.destroy              = mona_destroy,
	.start                = mona_start,
	.stop                 = mona_stop,
	.encoded_packet       = mona_encoded_packet,
	.get_properties       = mona_get_properties,
	.get_defaults         = mona_get_defaults,
	.get_total_bytes      = mona_get_total_bytes,
	.get_dropped_frames   = mona_get_dropped_frames,
	.get_congestion       = mona_get_congestion,
	.get_connect_time_ms  = mona_get_connect_time,
};

void mona_output_register(void)
{
	obs_register_output(&mona_output_info);
}

/* ── Name ────────────────────────────────────────────────────────────────── */
static const char *mona_get_name(void *) { return "Mona Live Output"; }

/* ── Create ──────────────────────────────────────────────────────────────── */
static void *mona_create(obs_data_t *settings, obs_output_t *output)
{
	auto *ctx = new MonaOutput();
	ctx->output = output;

	/* Load settings */
	ctx->protocol        = (MonaProtocol)obs_data_get_int(settings, S_PROTOCOL);
	ctx->server_url      = obs_data_get_string(settings, S_SERVER_URL);
	ctx->stream_key      = obs_data_get_string(settings, S_STREAM_KEY);
	ctx->gui_service_url = obs_data_get_string(settings, S_GUI_SERVICE_URL);
	ctx->srt_latency_ms  = (int)obs_data_get_int(settings, S_SRT_LATENCY);
	ctx->video_bitrate_kbps = (int)obs_data_get_int(settings, S_VIDEO_BITRATE);
	ctx->multibitrate    = obs_data_get_bool(settings, S_MULTIBITRATE);
	ctx->low_bitrate_kbps = (int)obs_data_get_int(settings, S_LOW_BITRATE);
	ctx->enable_remote_ctrl = obs_data_get_bool(settings, S_REMOTE_CTRL);
	ctx->enable_telemetry   = obs_data_get_bool(settings, S_TELEMETRY);
	ctx->enable_ptz         = obs_data_get_bool(settings, S_PTZ);

	os_sem_init(&ctx->send_sem, 0);
	pthread_mutex_init(&ctx->packets_mutex, nullptr);

	return ctx;
}

/* ── Destroy ─────────────────────────────────────────────────────────────── */
static void mona_destroy(void *data)
{
	auto *ctx = static_cast<MonaOutput *>(data);
	os_sem_destroy(ctx->send_sem);
	pthread_mutex_destroy(&ctx->packets_mutex);

	/* Free queued packets */
	while (ctx->packets.size) {
		encoder_packet *pkt;
		deque_pop_front(&ctx->packets, &pkt, sizeof(pkt));
		obs_encoder_packet_release(pkt);
		bfree(pkt);
	}
	deque_free(&ctx->packets);

	delete ctx;
}

/* ── Build transport from settings ──────────────────────────────────────── */
static std::unique_ptr<IMonaTransport> build_transport(MonaOutput *ctx)
{
	switch (ctx->protocol) {

	case MonaProtocol::WHIP: {
		/* WHIP endpoint: gui_service_url + /api/whip/<key> or direct */
		std::string ep = ctx->server_url;
		if (ep.empty()) ep = ctx->gui_service_url + "/api/whip/" + ctx->stream_key;
		return std::make_unique<WhipTransport>(ep, "");
	}

	case MonaProtocol::SRT: {
		/* Parse srt://host:port */
		std::string url = ctx->server_url;
		if (url.rfind("srt://", 0) == 0) url = url.substr(6);
		auto colon = url.rfind(':');
		std::string host = (colon != std::string::npos) ? url.substr(0, colon) : url;
		int port = (colon != std::string::npos) ? std::stoi(url.substr(colon + 1)) : 4900;
		return std::make_unique<SrtTransport>(host, port, ctx->srt_latency_ms, ctx->video_bitrate_kbps + 256);
	}

	default: /* RTMP */
		return std::make_unique<RtmpTransport>(ctx->server_url, ctx->stream_key);
	}
}

/* ── Connect control plane ───────────────────────────────────────────────── */
static void connect_control_plane(MonaOutput *ctx)
{
	if (ctx->gui_service_url.empty()) return;

	/* Derive host:port from gui_service_url (http://host:port) */
	std::string url = ctx->gui_service_url;
	if (url.rfind("http://", 0) == 0)  url = url.substr(7);
	if (url.rfind("https://", 0) == 0) url = url.substr(8);
	auto colon = url.rfind(':');
	std::string host = (colon != std::string::npos) ? url.substr(0, colon) : url;
	/* trim path component */
	auto slash = host.find('/');
	if (slash != std::string::npos) host = host.substr(0, slash);
	int port = (colon != std::string::npos) ? std::stoi(url.substr(colon + 1)) : 8080;

	ctx->ws = std::make_unique<WsClient>(host, port, "/hub/obs-control");
	ctx->ws->on_open([ctx]() {
		/* Announce ourselves to the GUI */
		std::ostringstream reg;
		reg << "{\"type\":\"register\",\"plugin\":\"obs-mona-live\","
		    << "\"version\":\"" << PLUGIN_VERSION << "\","
		    << "\"stream_key\":\"" << ctx->stream_key << "\","
		    << "\"transport\":\"" << (ctx->protocol == MonaProtocol::SRT  ? "SRT"  :
		                              ctx->protocol == MonaProtocol::WHIP ? "WHIP" : "RTMP") << "\"}";
		ctx->ws->send_text(reg.str());
		LOG_OUT(LOG_INFO, "Control-plane registered with GUI service");
	});

	if (ctx->enable_telemetry) {
		ctx->telemetry = std::make_unique<TelemetryReporter>(
			ctx->ws.get(), ctx->stream_key,
			ctx->protocol == MonaProtocol::SRT  ? "SRT"  :
			ctx->protocol == MonaProtocol::WHIP ? "WHIP" : "RTMP");

		ctx->telemetry->set_stats_provider([ctx]() -> TransportStats {
			return ctx->transport ? ctx->transport->stats() : TransportStats{};
		});
		ctx->telemetry->set_obs_stats([ctx](double &fps, int &dropped) {
			video_t *v = obs_output_video(ctx->output);
			fps     = v ? (double)video_output_get_frame_rate(v) : 0.0;
			dropped = obs_output_get_frames_dropped(ctx->output);
		});
		ctx->telemetry->start();
	}

	if (ctx->enable_ptz) {
		ctx->ptz = std::make_unique<PtzChannel>(ctx->ws.get());
		ctx->ptz->set_handler([](const PtzCommand &cmd) {
			LOG_OUT(LOG_DEBUG, "PTZ: %s = %.3f", cmd.cmd.c_str(), cmd.value);
			/*
			 * Integration point: call obs_source_set_monitoring_type or
			 * forward to a PTZ-capable camera via UDP/TCP based on cmd.cmd:
			 * "pan", "tilt", "zoom", "preset", "stop".
			 */
		});
		ctx->ptz->attach();
	}

	if (!ctx->ws->start()) {
		LOG_OUT(LOG_WARNING, "Control-plane WebSocket not available (GUI service unreachable)");
		ctx->ws.reset();
	}
}

/* ── Start ───────────────────────────────────────────────────────────────── */
static bool mona_start(void *data)
{
	auto *ctx = static_cast<MonaOutput *>(data);

	ctx->stopping = false;
	ctx->total_bytes_sent = 0;
	uint64_t t0 = os_gettime_ns();

	ctx->transport = build_transport(ctx);
	if (!ctx->transport->connect()) {
		obs_output_signal_stop(ctx->output, OBS_OUTPUT_ERROR);
		return false;
	}

	/* Optional second transport for ABR low-bitrate track */
	if (ctx->multibitrate && ctx->protocol == MonaProtocol::SRT) {
		std::string url = ctx->server_url;
		if (url.rfind("srt://", 0) == 0) url = url.substr(6);
		auto colon = url.rfind(':');
		std::string host = (colon != std::string::npos) ? url.substr(0, colon) : url;
		int port  = (colon != std::string::npos) ? std::stoi(url.substr(colon + 1)) : 4900;
		ctx->transport_low = std::make_unique<SrtTransport>(
			host, port + 1, ctx->srt_latency_ms, ctx->low_bitrate_kbps + 64);
		if (!ctx->transport_low->connect()) {
			LOG_OUT(LOG_WARNING, "Low-bitrate ABR track failed to connect — continuing with single track");
			ctx->transport_low.reset();
		}
	}

	connect_control_plane(ctx);

	ctx->connect_time_ms = (os_gettime_ns() - t0) / 1000000;
	ctx->active = true;

	if (pthread_create(&ctx->send_thread, nullptr, mona_send_thread, ctx) != 0) {
		ctx->transport->disconnect();
		obs_output_signal_stop(ctx->output, OBS_OUTPUT_ERROR);
		return false;
	}

	if (!obs_output_begin_data_capture(ctx->output, 0)) {
		ctx->stopping = true;
		os_sem_post(ctx->send_sem);
		pthread_join(ctx->send_thread, nullptr);
		ctx->transport->disconnect();
		return false;
	}

	LOG_OUT(LOG_INFO, "Stream started → %s [%s]",
	        ctx->server_url.c_str(),
	        ctx->transport->protocol_name());
	return true;
}

/* ── Stop ────────────────────────────────────────────────────────────────── */
static void mona_stop(void *data, uint64_t)
{
	auto *ctx = static_cast<MonaOutput *>(data);
	if (!ctx->active) return;

	obs_output_end_data_capture(ctx->output);

	ctx->stopping = true;
	os_sem_post(ctx->send_sem);
	pthread_join(ctx->send_thread, nullptr);

	if (ctx->telemetry) { ctx->telemetry->stop(); ctx->telemetry.reset(); }
	if (ctx->ws)        { ctx->ws->stop();         ctx->ws.reset(); }
	if (ctx->ptz)       { ctx->ptz.reset(); }

	if (ctx->transport)     { ctx->transport->disconnect();     ctx->transport.reset(); }
	if (ctx->transport_low) { ctx->transport_low->disconnect(); ctx->transport_low.reset(); }

	ctx->active = false;
	obs_output_signal_stop(ctx->output, OBS_OUTPUT_SUCCESS);
	LOG_OUT(LOG_INFO, "Stream stopped");
}

/* ── Encoded packet callback ─────────────────────────────────────────────── */
static void mona_encoded_packet(void *data, struct encoder_packet *packet)
{
	auto *ctx = static_cast<MonaOutput *>(data);
	if (!ctx->active || ctx->stopping) return;

	/* Copy packet and push onto queue for send thread */
	encoder_packet *copy = (encoder_packet *)bzalloc(sizeof(*copy));
	obs_encoder_packet_ref(copy, packet);

	pthread_mutex_lock(&ctx->packets_mutex);
	deque_push_back(&ctx->packets, &copy, sizeof(copy));
	pthread_mutex_unlock(&ctx->packets_mutex);

	os_sem_post(ctx->send_sem);
}

/* ── Send thread ─────────────────────────────────────────────────────────── */
static void *mona_send_thread(void *data)
{
	auto *ctx = static_cast<MonaOutput *>(data);

	/* One MPEG-TS muxer instance; callback sends via transport */
	MpegTsMuxer muxer([ctx](const uint8_t *pkt, size_t len) {
		if (ctx->transport && ctx->transport->is_connected()) {
			if (ctx->transport->send(pkt, len))
				ctx->total_bytes_sent += len;
			else
				ctx->stopping = true;
		}
		/* Mirror to ABR low-bitrate track (keyframes only would be ideal; send all for now) */
		if (ctx->transport_low && ctx->transport_low->is_connected())
			ctx->transport_low->send(pkt, len);
	});

	while (!ctx->stopping) {
		os_sem_wait(ctx->send_sem);
		if (ctx->stopping) break;

		encoder_packet *pkt = nullptr;
		pthread_mutex_lock(&ctx->packets_mutex);
		if (ctx->packets.size)
			deque_pop_front(&ctx->packets, &pkt, sizeof(pkt));
		pthread_mutex_unlock(&ctx->packets_mutex);

		if (!pkt) continue;

		/* Convert OBS 90 kHz timebase */
		int64_t pts = pkt->pts;
		int64_t dts = pkt->dts;

		if (pkt->type == OBS_ENCODER_VIDEO) {
			muxer.write_video(pkt->data, pkt->size, pts, dts, pkt->keyframe);
		} else if (pkt->type == OBS_ENCODER_AUDIO) {
			muxer.write_audio(pkt->data, pkt->size, pts);
		}

		obs_encoder_packet_release(pkt);
		bfree(pkt);

		if (ctx->stopping)
			obs_output_signal_stop(ctx->output, OBS_OUTPUT_DISCONNECTED);
	}

	return nullptr;
}

/* ── Properties (settings UI) ────────────────────────────────────────────── */
static obs_properties_t *mona_get_properties(void *)
{
	obs_properties_t *props = obs_properties_create();

	/* Protocol selector */
	obs_property_t *proto = obs_properties_add_list(props, S_PROTOCOL,
		obs_module_text("Protocol"), OBS_COMBO_TYPE_LIST, OBS_COMBO_FORMAT_INT);
	obs_property_list_add_int(proto, obs_module_text("WHIP (WebRTC)"), (int)MonaProtocol::WHIP);
	obs_property_list_add_int(proto, obs_module_text("SRT (Ultra-Low Latency)"), (int)MonaProtocol::SRT);
	obs_property_list_add_int(proto, obs_module_text("RTMP (Fallback)"), (int)MonaProtocol::RTMP);

	obs_properties_add_text(props, S_SERVER_URL,
		obs_module_text("ServerURL"), OBS_TEXT_DEFAULT);
	obs_properties_add_text(props, S_STREAM_KEY,
		obs_module_text("StreamKey"), OBS_TEXT_PASSWORD);
	obs_properties_add_text(props, S_GUI_SERVICE_URL,
		obs_module_text("GUIServiceURL"), OBS_TEXT_DEFAULT);

	obs_properties_add_int_slider(props, S_SRT_LATENCY,
		obs_module_text("SRTLatency"), 20, 2000, 10);
	obs_properties_add_int(props, S_VIDEO_BITRATE,
		obs_module_text("VideoBitrate"), 100, 50000, 100);

	obs_properties_add_bool(props, S_MULTIBITRATE, obs_module_text("MultibitratABR"));
	obs_properties_add_int(props, S_LOW_BITRATE,   obs_module_text("LowBitrate"), 100, 10000, 100);

	obs_properties_add_bool(props, S_REMOTE_CTRL, obs_module_text("EnableRemoteControl"));
	obs_properties_add_bool(props, S_TELEMETRY,   obs_module_text("EnableTelemetry"));
	obs_properties_add_bool(props, S_PTZ,         obs_module_text("EnablePTZ"));

	return props;
}

static void mona_get_defaults(obs_data_t *d)
{
	obs_data_set_default_int(d, S_PROTOCOL,      (int)MonaProtocol::SRT);
	obs_data_set_default_string(d, S_SERVER_URL, "srt://localhost:4900");
	obs_data_set_default_string(d, S_STREAM_KEY, "live");
	obs_data_set_default_string(d, S_GUI_SERVICE_URL, "http://localhost:8080");
	obs_data_set_default_int(d, S_SRT_LATENCY,   20);
	obs_data_set_default_int(d, S_VIDEO_BITRATE, 2500);
	obs_data_set_default_bool(d, S_MULTIBITRATE,  false);
	obs_data_set_default_int(d, S_LOW_BITRATE,    800);
	obs_data_set_default_bool(d, S_REMOTE_CTRL,   true);
	obs_data_set_default_bool(d, S_TELEMETRY,     true);
	obs_data_set_default_bool(d, S_PTZ,           false);
}

/* ── Stats callbacks ─────────────────────────────────────────────────────── */
static float mona_get_congestion(void *data)
{
	auto *ctx = static_cast<MonaOutput *>(data);
	if (!ctx->transport) return 0.0f;
	auto ts = ctx->transport->stats();
	return (float)std::min(ts.packet_loss_pct / 5.0, 1.0); /* 5% loss = full congestion */
}

static int mona_get_connect_time(void *data)
{
	return (int)static_cast<MonaOutput *>(data)->connect_time_ms;
}

static uint64_t mona_get_total_bytes(void *data)
{
	return static_cast<MonaOutput *>(data)->total_bytes_sent;
}

static int mona_get_dropped_frames(void *data)
{
	return obs_output_get_frames_dropped(static_cast<MonaOutput *>(data)->output);
}
