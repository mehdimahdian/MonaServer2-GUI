/*
 * Mona Live Output for OBS Studio
 * Copyright (C) MonaServer2 GUI Contributors
 *
 * Push OBS Studio output to MonaServer2 via WebRTC WHIP, SRT, or RTMP.
 * Features: native WebRTC, ultra-low-latency SRT, multi-bitrate,
 *           remote control channel, telemetry, PTZ/drone control.
 *
 * GPLv2 — see LICENSE
 */

#include <obs-module.h>
#include "mona-output.h"

OBS_DECLARE_MODULE()
OBS_MODULE_USE_DEFAULT_LOCALE(PLUGIN_NAME, "en-US")

MODULE_EXPORT const char *obs_module_description(void)
{
	return "Mona Live Output — ultra-low-latency WebRTC/SRT streaming via MonaServer2 "
	       "(MonaSolutions / Haivision). GPLv2.";
}

bool obs_module_load(void)
{
	mona_output_register();
	blog(LOG_INFO, "[mona-live] plugin v%s loaded", PLUGIN_VERSION);
	return true;
}

void obs_module_unload(void)
{
	blog(LOG_INFO, "[mona-live] plugin unloaded");
}
