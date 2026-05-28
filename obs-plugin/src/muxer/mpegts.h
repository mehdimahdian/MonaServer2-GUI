#pragma once
#include <cstdint>
#include <vector>
#include <functional>

/*
 * Minimal MPEG-TS muxer for H.264 + AAC/ADTS.
 *
 * Produces 188-byte TS packets suitable for SRT live-mode transport.
 * PAT and PMT are inserted every PAT_INTERVAL_PKTS video packets and on
 * each keyframe so the receiver can start decoding from any point.
 *
 * PIDs:
 *   0x0000 — PAT
 *   0x0100 — PMT
 *   0x0101 — H.264 video
 *   0x0102 — AAC audio
 */

static constexpr uint16_t TS_PAT_PID   = 0x0000;
static constexpr uint16_t TS_PMT_PID   = 0x0100;
static constexpr uint16_t TS_VIDEO_PID = 0x0101;
static constexpr uint16_t TS_AUDIO_PID = 0x0102;
static constexpr uint16_t TS_PCR_PID   = TS_VIDEO_PID;
static constexpr size_t   TS_PACKET_SIZE = 188;
static constexpr int      PAT_INTERVAL_PKTS = 150;

using TsPacketCallback = std::function<void(const uint8_t *packet, size_t len)>;

class MpegTsMuxer {
public:
	explicit MpegTsMuxer(TsPacketCallback cb) : cb_(std::move(cb)) {}

	/* Call on each OBS encoded_packet with is_keyframe information */
	void write_video(const uint8_t *data, size_t len, int64_t pts_90khz, int64_t dts_90khz, bool keyframe);
	void write_audio(const uint8_t *data, size_t len, int64_t pts_90khz);

private:
	void write_pat();
	void write_pmt();
	void write_pes(uint16_t pid, const uint8_t *payload, size_t payload_len,
	               int64_t pts, int64_t dts, bool has_dts, bool pcr, bool keyframe);

	static void write_ts_packet(uint8_t *pkt, uint16_t pid, bool pusi, bool keyframe,
	                            bool has_pcr, int64_t pcr_base, int64_t pcr_ext,
	                            const uint8_t *payload, int payload_len, uint8_t &cc);

	static uint32_t crc32(const uint8_t *data, size_t len);

	TsPacketCallback cb_;
	uint8_t v_cc_  = 0; /* video continuity counter */
	uint8_t a_cc_  = 0;
	uint8_t pat_cc_= 0;
	uint8_t pmt_cc_= 0;
	int pkt_count_ = 0;
};
