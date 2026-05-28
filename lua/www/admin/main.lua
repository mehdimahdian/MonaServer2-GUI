-- MonaServer2 GUI — Lua API entry point
-- Deploy this directory to MonaServer2's www/admin/ folder.
-- Exposes JSON endpoints at /admin/api/* for the GUI companion service.

local json = require("json")  -- MonaServer2 has a built-in json module

function onInvocation(client, name, ...)
  -- Route /admin/api/* HTTP GET requests
  local path = client.path or ""

  if path == "/admin/api/status" then
    return handleStatus(client)
  elseif path == "/admin/api/publications" then
    return handlePublications(client)
  elseif path == "/admin/api/sessions" then
    return handleSessions(client)
  end

  client:writeError(404, "Not found: " .. path)
end

function handleStatus(client)
  local result = {
    running = true,
    version = mona.version or "unknown",
    startedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", mona.startTime or os.time()),
    totalConnections = 0,
    totalPublications = 0,
    byteRateIn = 0,
    byteRateOut = 0,
    byProtocol = {}
  }

  -- Count publications
  for name, pub in pairs(mona.publications) do
    result.totalPublications = result.totalPublications + 1
    result.byteRateIn = result.byteRateIn + (pub.byteRate or 0)
  end

  -- Count sessions by protocol
  for id, session in pairs(mona.sessions) do
    result.totalConnections = result.totalConnections + 1
    local proto = session.protocol or "unknown"
    result.byProtocol[proto] = (result.byProtocol[proto] or 0) + 1
    result.byteRateOut = result.byteRateOut + (session.byteRate or 0)
  end

  client:writeJSON(json.encode(result))
end

function handlePublications(client)
  local result = {}

  for name, pub in pairs(mona.publications) do
    local tracks = {}

    -- Video track
    if pub.video then
      table.insert(tracks, {
        type = "video",
        codec = pub.video.codec or "",
        width = pub.video.width or 0,
        height = pub.video.height or 0,
        fps = pub.video.fps or 0,
        bitrate = pub.video.bitrate or 0
      })
    end

    -- Audio track
    if pub.audio then
      table.insert(tracks, {
        type = "audio",
        codec = pub.audio.codec or "",
        sampleRate = pub.audio.sampleRate or 0,
        channels = pub.audio.channels or 0,
        bitrate = pub.audio.bitrate or 0
      })
    end

    table.insert(result, {
      name = name,
      protocol = pub.protocol or "",
      address = pub.address or "",
      startedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", pub.time or os.time()),
      subscribers = pub.count or 0,
      recording = pub.recording or false,
      recordingPath = pub.recordingPath,
      byteRateIn = pub.byteRateIn or 0,
      byteRateOut = pub.byteRateOut or 0,
      lostRateIn = pub.lostRateIn or 0,
      lostRateOut = pub.lostRateOut or 0,
      tracks = tracks
    })
  end

  client:writeJSON(json.encode(result))
end

function handleSessions(client)
  local result = {}

  for id, session in pairs(mona.sessions) do
    local subscribing = {}
    if session.subscriptions then
      for _, sub in ipairs(session.subscriptions) do
        table.insert(subscribing, sub.name or "")
      end
    end

    table.insert(result, {
      id = tostring(id),
      address = session.address or "",
      protocol = session.protocol or "",
      connectedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", session.time or os.time()),
      publishing = session.publication and session.publication.name or nil,
      subscribing = subscribing,
      byteRateIn = session.byteRateIn or 0,
      byteRateOut = session.byteRateOut or 0
    })
  end

  client:writeJSON(json.encode(result))
end
