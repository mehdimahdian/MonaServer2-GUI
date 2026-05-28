import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { ServerStatus, Publication, Session, LogEntry } from '../types'

export interface SignalRState {
  connected: boolean
  status: ServerStatus | null
  publications: Publication[]
  sessions: Session[]
  logs: LogEntry[]
}

const MAX_LOGS = 2000

export function useSignalR(): SignalRState {
  const [connected, setConnected] = useState(false)
  const [status, setStatus] = useState<ServerStatus | null>(null)
  const [publications, setPublications] = useState<Publication[]>([])
  const [sessions, setSessions] = useState<Session[]>([])
  const [logs, setLogs] = useState<LogEntry[]>([])
  const connectionRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hub/monitor')
      .withAutomaticReconnect()
      .build()

    connectionRef.current = connection

    connection.on('StatusChanged', (s: ServerStatus) => setStatus(s))
    connection.on('PublicationsUpdated', (p: Publication[]) => setPublications(p))
    connection.on('SessionsUpdated', (s: Session[]) => setSessions(s))
    connection.on('LogReceived', (e: LogEntry) =>
      setLogs(prev => {
        const next = [...prev, e]
        return next.length > MAX_LOGS ? next.slice(next.length - MAX_LOGS) : next
      })
    )

    connection.onreconnected(() => setConnected(true))
    connection.onclose(() => setConnected(false))

    connection.start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false))

    return () => { connection.stop() }
  }, [])

  return { connected, status, publications, sessions, logs }
}
