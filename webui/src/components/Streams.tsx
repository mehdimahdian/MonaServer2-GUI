import { useState } from 'react'
import type { Publication } from '../types'

function formatBytes(bps: number) {
  if (bps >= 1_048_576) return `${(bps / 1_048_576).toFixed(1)} MB/s`
  if (bps >= 1024) return `${(bps / 1024).toFixed(1)} KB/s`
  return `${bps} B/s`
}

function formatDuration(startedAt: string) {
  const secs = Math.floor((Date.now() - new Date(startedAt).getTime()) / 1000)
  const h = Math.floor(secs / 3600)
  const m = Math.floor((secs % 3600) / 60)
  const s = secs % 60
  return `${h.toString().padStart(2,'0')}:${m.toString().padStart(2,'0')}:${s.toString().padStart(2,'0')}`
}

export default function Streams({ publications }: { publications: Publication[] }) {
  const [filter, setFilter] = useState('')
  const [selected, setSelected] = useState<Publication | null>(null)

  const filtered = publications.filter(p =>
    !filter || p.name.toLowerCase().includes(filter.toLowerCase())
  )

  return (
    <div className="flex flex-col h-full">
      {/* Toolbar */}
      <div className="px-6 py-4 border-b border-surface-border flex items-center gap-4">
        <h1 className="text-xl font-bold text-text-primary">Streams</h1>
        <input
          value={filter}
          onChange={e => setFilter(e.target.value)}
          placeholder="Filter by name..."
          className="flex-1 max-w-xs bg-surface-card border border-surface-border rounded-lg px-3 py-1.5
                     text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-accent"
        />
        <span className="text-sm text-text-muted">{filtered.length} stream{filtered.length !== 1 ? 's' : ''}</span>
      </div>

      {/* Table */}
      <div className="flex-1 overflow-auto">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-surface-card border-b border-surface-border">
            <tr className="text-text-muted text-xs uppercase tracking-wide">
              <th className="px-6 py-3 text-left">Name</th>
              <th className="px-6 py-3 text-left">Protocol</th>
              <th className="px-6 py-3 text-left">Client</th>
              <th className="px-6 py-3 text-right">Subs</th>
              <th className="px-6 py-3 text-right">In</th>
              <th className="px-6 py-3 text-right">Out</th>
              <th className="px-6 py-3 text-center">Rec</th>
              <th className="px-6 py-3 text-right">Duration</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(pub => (
              <tr
                key={pub.name}
                onClick={() => setSelected(selected?.name === pub.name ? null : pub)}
                className={`border-b border-surface-border cursor-pointer transition-colors
                  ${selected?.name === pub.name ? 'bg-surface-elevated' : 'hover:bg-surface-card'}`}
              >
                <td className="px-6 py-3 font-mono text-xs text-text-primary">{pub.name}</td>
                <td className="px-6 py-3 text-text-secondary">{pub.protocol}</td>
                <td className="px-6 py-3 text-text-muted font-mono text-xs">{pub.clientAddress}</td>
                <td className="px-6 py-3 text-right text-text-secondary">{pub.subscriberCount}</td>
                <td className="px-6 py-3 text-right text-accent-blue font-mono text-xs">{formatBytes(pub.byteRateIn)}</td>
                <td className="px-6 py-3 text-right text-yellow-400 font-mono text-xs">{formatBytes(pub.byteRateOut)}</td>
                <td className="px-6 py-3 text-center">
                  {pub.isRecording && <span className="text-accent-red text-xs font-bold">● REC</span>}
                </td>
                <td className="px-6 py-3 text-right font-mono text-xs text-text-muted">
                  {formatDuration(pub.startedAt)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {filtered.length === 0 && (
          <div className="flex items-center justify-center h-48 text-text-muted">
            {publications.length === 0 ? 'No active publications' : 'No results for filter'}
          </div>
        )}
      </div>

      {/* Detail panel */}
      {selected && (
        <div className="border-t border-surface-border bg-surface-card px-6 py-4">
          <div className="text-xs text-text-muted uppercase tracking-widest mb-3">
            Tracks — {selected.name}
          </div>
          <div className="flex flex-wrap gap-3">
            {selected.tracks.map((track, i) => (
              <div key={i} className="bg-surface-elevated rounded-lg px-4 py-2 text-xs font-mono">
                <span className="text-accent text-[10px] uppercase mr-2">{track.type}</span>
                <span className="text-text-primary">{track.codec}</span>
                {track.type === 'Video' && track.width > 0 && (
                  <span className="text-text-muted ml-2">{track.width}×{track.height} {track.fps}fps</span>
                )}
                {track.type === 'Audio' && track.sampleRate > 0 && (
                  <span className="text-text-muted ml-2">{track.sampleRate}Hz {track.channels}ch</span>
                )}
              </div>
            ))}
            {selected.tracks.length === 0 && (
              <span className="text-text-muted text-xs">No track data available</span>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
