export interface ServerStatus {
  isRunning: boolean
  version?: string
  startedAt?: string
  totalConnections: number
  totalPublications: number
  connectionsByProtocol: Record<string, number>
  totalByteRateIn: number
  totalByteRateOut: number
  processId: number
}

export interface StreamTrack {
  type: 'Video' | 'Audio' | 'Data'
  codec: string
  language?: string
  width: number
  height: number
  fps: number
  sampleRate: number
  channels: number
  bitrate: number
}

export interface Publication {
  name: string
  protocol: string
  clientAddress: string
  startedAt: string
  subscriberCount: number
  isRecording: boolean
  recordingPath?: string
  byteRateIn: number
  byteRateOut: number
  lostRateIn: number
  lostRateOut: number
  tracks: StreamTrack[]
}

export interface Session {
  id: string
  address: string
  protocol: string
  connectedAt: string
  publishingTo?: string
  subscribingTo: string[]
  byteRateIn: number
  byteRateOut: number
}

export type LogLevel = 'Fatal' | 'Critic' | 'Error' | 'Warn' | 'Note' | 'Info' | 'Debug' | 'Trace'

export interface LogEntry {
  timestamp: string
  level: LogLevel
  message: string
  source?: string
}
