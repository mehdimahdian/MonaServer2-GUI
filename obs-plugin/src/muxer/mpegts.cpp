#include "mpegts.h"
#include <cstring>
#include <stdexcept>

/* ── CRC32 for PAT/PMT (MPEG-TS uses CRC32/MPEG-2) ─────────────────────── */
uint32_t MpegTsMuxer::crc32(const uint8_t *data, size_t len)
{
	uint32_t crc = 0xFFFFFFFF;
	for (size_t i = 0; i < len; i++) {
		crc ^= (uint32_t)data[i] << 24;
		for (int j = 0; j < 8; j++)
			crc = (crc & 0x80000000) ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
	}
	return crc;
}

/* ── Write one 188-byte TS packet ────────────────────────────────────────── */
void MpegTsMuxer::write_ts_packet(uint8_t *pkt, uint16_t pid, bool pusi, bool keyframe,
                                   bool has_pcr, int64_t pcr_base, int64_t pcr_ext,
                                   const uint8_t *payload, int payload_len, uint8_t &cc)
{
	memset(pkt, 0xFF, TS_PACKET_SIZE);

	pkt[0] = 0x47; /* sync byte */
	pkt[1] = (pusi ? 0x40 : 0x00) | (uint8_t)((pid >> 8) & 0x1F);
	pkt[2] = (uint8_t)(pid & 0xFF);

	int adapt_len = 0;
	if (has_pcr) adapt_len = 8; /* adaptation field with PCR = 6 bytes + flags (2) */
	if (keyframe && !has_pcr) adapt_len = 2; /* random access indicator */

	int avail = 184 - adapt_len;
	bool has_adapt = (adapt_len > 0) || (payload_len < 184);

	if (payload_len < avail) {
		/* stuff with adaptation field */
		adapt_len = 184 - payload_len;
		has_adapt = true;
	}

	uint8_t afc = (has_adapt ? 0x20 : 0x00) | 0x10 | (cc & 0x0F);
	pkt[3] = afc;
	cc = (cc + 1) & 0x0F;

	int pos = 4;
	if (has_adapt) {
		int af_len = adapt_len - 1;
		pkt[pos++] = (uint8_t)af_len;
		if (af_len > 0) {
			uint8_t flags = 0x00;
			if (has_pcr)  flags |= 0x10;
			if (keyframe) flags |= 0x40; /* random_access_indicator */
			pkt[pos++] = flags;
			if (has_pcr) {
				/* PCR: 33-bit base + 6-bit reserved + 9-bit ext */
				pkt[pos+0] = (uint8_t)(pcr_base >> 25);
				pkt[pos+1] = (uint8_t)(pcr_base >> 17);
				pkt[pos+2] = (uint8_t)(pcr_base >> 9);
				pkt[pos+3] = (uint8_t)(pcr_base >> 1);
				pkt[pos+4] = (uint8_t)((pcr_base & 1) << 7) | 0x7E | (uint8_t)(pcr_ext >> 8);
				pkt[pos+5] = (uint8_t)(pcr_ext & 0xFF);
				pos += 6;
			}
			/* remaining stuffing bytes already 0xFF from memset */
			pos += af_len - 1 - (has_pcr ? 7 : 0);
		}
	}

	if (payload && payload_len > 0) {
		int copy = std::min(payload_len, (int)(TS_PACKET_SIZE - pos));
		memcpy(pkt + pos, payload, copy);
	}
}

/* ── PAT ─────────────────────────────────────────────────────────────────── */
void MpegTsMuxer::write_pat()
{
	uint8_t section[17] = {};
	section[0] = 0x00;              /* table_id: PAT */
	section[1] = 0xB0;              /* section_syntax_indicator=1, '0', reserved, section_length hi */
	section[2] = 0x0D;              /* section_length = 13 */
	section[3] = 0x00; section[4] = 0x01; /* transport_stream_id */
	section[5] = 0xC1;              /* version=0, current_next=1 */
	section[6] = 0x00;              /* section_number */
	section[7] = 0x00;              /* last_section_number */
	/* Program 1 → PMT PID 0x0100 */
	section[8]  = 0x00; section[9]  = 0x01; /* program_number */
	section[10] = 0xE1; section[11] = 0x00; /* PMT PID = 0x100, reserved=0xE */
	uint32_t crc = crc32(section, 12);
	section[12] = (crc >> 24) & 0xFF;
	section[13] = (crc >> 16) & 0xFF;
	section[14] = (crc >>  8) & 0xFF;
	section[15] = (crc      ) & 0xFF;

	/* Prepend pointer_field=0x00 */
	uint8_t with_ptr[18];
	with_ptr[0] = 0x00;
	memcpy(with_ptr + 1, section, 16);

	uint8_t pkt[TS_PACKET_SIZE];
	write_ts_packet(pkt, TS_PAT_PID, true, false, false, 0, 0, with_ptr, 17, pat_cc_);
	cb_(pkt, TS_PACKET_SIZE);
}

/* ── PMT ─────────────────────────────────────────────────────────────────── */
void MpegTsMuxer::write_pmt()
{
	uint8_t section[32] = {};
	int pos = 0;
	section[pos++] = 0x02;              /* table_id: PMT */
	section[pos++] = 0xB0;             /* section_syntax_indicator, length hi (filled below) */
	section[pos++] = 0x00;             /* length lo — placeholder */
	section[pos++] = 0x00; section[pos++] = 0x01; /* program_number */
	section[pos++] = 0xC1;             /* version, current_next */
	section[pos++] = 0x00;             /* section_number */
	section[pos++] = 0x00;             /* last_section_number */
	section[pos++] = 0xE1; section[pos++] = 0x01; /* PCR_PID = TS_VIDEO_PID = 0x101 */
	section[pos++] = 0xF0; section[pos++] = 0x00; /* program_info_length = 0 */
	/* Video ES: stream_type=0x1B (H.264) PID=0x101 */
	section[pos++] = 0x1B;
	section[pos++] = 0xE1; section[pos++] = 0x01; /* PID 0x101 */
	section[pos++] = 0xF0; section[pos++] = 0x00; /* ES_info_length = 0 */
	/* Audio ES: stream_type=0x0F (AAC ADTS) PID=0x102 */
	section[pos++] = 0x0F;
	section[pos++] = 0xE1; section[pos++] = 0x02; /* PID 0x102 */
	section[pos++] = 0xF0; section[pos++] = 0x00; /* ES_info_length = 0 */

	/* Fill section_length = pos - 3 + 4 (CRC) */
	uint16_t sec_len = (uint16_t)(pos - 3 + 4);
	section[1] |= (sec_len >> 8) & 0x0F;
	section[2]  = sec_len & 0xFF;

	uint32_t crc = crc32(section, pos);
	section[pos++] = (crc >> 24) & 0xFF;
	section[pos++] = (crc >> 16) & 0xFF;
	section[pos++] = (crc >>  8) & 0xFF;
	section[pos++] = (crc      ) & 0xFF;

	uint8_t with_ptr[64];
	with_ptr[0] = 0x00;
	memcpy(with_ptr + 1, section, pos);

	uint8_t pkt[TS_PACKET_SIZE];
	write_ts_packet(pkt, TS_PMT_PID, true, false, false, 0, 0, with_ptr, pos + 1, pmt_cc_);
	cb_(pkt, TS_PACKET_SIZE);
}

/* ── PES packetiser ──────────────────────────────────────────────────────── */
void MpegTsMuxer::write_pes(uint16_t pid, const uint8_t *payload, size_t payload_len,
                              int64_t pts, int64_t dts, bool has_dts, bool pcr, bool keyframe)
{
	/* PES header */
	uint8_t pes_hdr[20];
	int hdr_pos = 0;

	pes_hdr[hdr_pos++] = 0x00; /* start code prefix */
	pes_hdr[hdr_pos++] = 0x00;
	pes_hdr[hdr_pos++] = 0x01;
	pes_hdr[hdr_pos++] = (pid == TS_VIDEO_PID) ? 0xE0 : 0xC0; /* stream_id */

	uint16_t pes_pkt_len = 0; /* 0 = unbounded for video */
	if (pid == TS_AUDIO_PID)
		pes_pkt_len = (uint16_t)(3 + (has_dts ? 10 : 5) + payload_len);
	pes_hdr[hdr_pos++] = (pes_pkt_len >> 8) & 0xFF;
	pes_hdr[hdr_pos++] = pes_pkt_len & 0xFF;

	uint8_t pts_dts_flag = has_dts ? 0x03 : 0x02;
	pes_hdr[hdr_pos++] = 0x80; /* marker bits */
	pes_hdr[hdr_pos++] = (pts_dts_flag << 6); /* PTS_DTS flags */
	pes_hdr[hdr_pos++] = has_dts ? 10 : 5;    /* header data length */

	auto write_ts_val = [&](int64_t ts_val, uint8_t prefix) {
		pes_hdr[hdr_pos++] = (uint8_t)(prefix | ((ts_val >> 29) & 0x0E) | 0x01);
		pes_hdr[hdr_pos++] = (uint8_t)((ts_val >> 22) & 0xFF);
		pes_hdr[hdr_pos++] = (uint8_t)(((ts_val >> 14) & 0xFE) | 0x01);
		pes_hdr[hdr_pos++] = (uint8_t)((ts_val >> 7) & 0xFF);
		pes_hdr[hdr_pos++] = (uint8_t)(((ts_val << 1) & 0xFE) | 0x01);
	};

	write_ts_val(pts, has_dts ? 0x30 : 0x20);
	if (has_dts) write_ts_val(dts, 0x10);

	/* Build full PES + payload */
	std::vector<uint8_t> pes(hdr_pos + payload_len);
	memcpy(pes.data(), pes_hdr, hdr_pos);
	memcpy(pes.data() + hdr_pos, payload, payload_len);

	/* Split into 188-byte TS packets */
	const uint8_t *src     = pes.data();
	size_t          rem     = pes.size();
	bool            first   = true;
	int64_t         pcr_base = pts; /* PCR = DTS for video */
	if (has_dts) pcr_base = dts;

	while (rem > 0) {
		uint8_t pkt[TS_PACKET_SIZE];
		bool emit_pcr = pcr && first;
		size_t room = first ? (emit_pcr ? 184 - 8 : 184) : 184;
		size_t chunk = std::min(rem, room);
		write_ts_packet(pkt, pid, first, keyframe && first, emit_pcr,
		                pcr_base, 0, src, (int)chunk, (pid == TS_VIDEO_PID) ? v_cc_ : a_cc_);
		cb_(pkt, TS_PACKET_SIZE);
		src  += chunk;
		rem  -= chunk;
		first = false;
	}
}

/* ── Public write methods ────────────────────────────────────────────────── */
void MpegTsMuxer::write_video(const uint8_t *data, size_t len,
                               int64_t pts_90khz, int64_t dts_90khz, bool keyframe)
{
	if (keyframe || pkt_count_ % PAT_INTERVAL_PKTS == 0) {
		write_pat();
		write_pmt();
	}
	pkt_count_++;
	write_pes(TS_VIDEO_PID, data, len, pts_90khz, dts_90khz, true, true, keyframe);
}

void MpegTsMuxer::write_audio(const uint8_t *data, size_t len, int64_t pts_90khz)
{
	write_pes(TS_AUDIO_PID, data, len, pts_90khz, 0, false, false, false);
}
