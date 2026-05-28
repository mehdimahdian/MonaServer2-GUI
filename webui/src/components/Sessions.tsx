import { useState } from 'react'
import type { Session } from '../types'

function formatDuration(connectedAt: string) {
  const secs = Math.floor((Date.now() - new Date(connectedAt).getTime()) / 1000)
  const h = Math.floor(secs / 3600)
  const m = Math.floor((secs % 3600) / 60)
  const s = secs % 60
  return `${h.toString().padStart(2,'0')}:${m.toString().padStart(2,'0')}:${s.toString().padStart(2,'0')}`
}

const PROTOCOLS = ['All', 'HTTP', 'RTMP', 'SRT', 'WebSocket', 'RTMFP']

export default function Sessions({ sessions }: { sessions: Session[] }) {
  const [protocol, setProtocol] = useState('All')

  const filtered = protocol === 'All'
    ? sessions
    : sessions.filter(s => s.protocol === protocol)

  return (
    <div className="flex flex-col h-full">
      <div className="px-6 py-4 border-b border-surface-border flex items-center gap-4">
        <h1 className="text-xl font-bold text-text-primary">Sessions</h1>
        <div className="flex gap-2">
          {PROTOCOLS.map(p => (
            <button
              key={p}
              onClick={() => setProtocol(p)}
              className={`px-3 py-1 rounded-lg text-xs font-medium transition-colors
                ${protocol === p
                  ? 'bg-accent text-white'
                  : 'bg-surface-card text-text-muted hover:text-text-primary'}`}
            >
              {p}
            </button>
          ))}
        </div>
        <span className="text-sm text-text-muted ml-auto">{filtered.length} session{filtered.length !== 1 ? 's' : ''}</span>
      </div>

      <div className="flex-1 overflow-auto">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-surface-card border-b border-surface-border">
            <tr className="text-text-muted text-xs uppercase tracking-wide">
              <th className="px-6 py-3 text-left">Address</th>
              <th className="px-6 py-3 text-left">Protocol</th>
              <th className="px-6 py-3 text-left">Publishing</th>
              <th className="px-6 py-3 text-left">Subscribing</th>
              <th className="px-6 py-3 text-right">Duration</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(session => (
              <tr key={session.id} className="border-b border-surface-border hover:bg-surface-card">
                <td className="px-6 py-3 font-mono text-xs text-text-primary">{session.address}</td>
                <td className="px-6 py-3 text-text-secondary">{session.protocol}</td>
                <td className="px-6 py-3 font-mono text-xs text-accent-green">
                  {session.publishingTo || <span className="text-text-muted">—</span>}
                </td>
                <td className="px-6 py-3 font-mono text-xs text-accent-blue">
                  {session.subscribingTo.length > 0 ? session.subscribingTo.join(', ') : <span className="text-text-muted">—</span>}
                </td>
                <td className="px-6 py-3 text-right font-mono text-xs text-text-muted">
                  {formatDuration(session.connectedAt)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {filtered.length === 0 && (
          <div className="flex items-center justify-center h-48 text-text-muted">
            No active sessions
          </div>
        )}
      </div>
    </div>
  )
}
