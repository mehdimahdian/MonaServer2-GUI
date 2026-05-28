import { useState, useRef, useEffect } from 'react'
import type { LogEntry, LogLevel } from '../types'

const LEVELS: LogLevel[] = ['Fatal', 'Critic', 'Error', 'Warn', 'Note', 'Info', 'Debug', 'Trace']

const levelOrder: Record<LogLevel, number> = {
  Fatal: 0, Critic: 1, Error: 2, Warn: 3, Note: 4, Info: 5, Debug: 6, Trace: 7
}

const levelColors: Record<LogLevel, string> = {
  Fatal:  'text-red-400 bg-red-900/30',
  Critic: 'text-red-300 bg-red-900/20',
  Error:  'text-red-300 bg-red-900/20',
  Warn:   'text-yellow-300 bg-yellow-900/20',
  Note:   'text-blue-300 bg-blue-900/20',
  Info:   'text-green-300 bg-green-900/20',
  Debug:  'text-gray-400 bg-gray-900/20',
  Trace:  'text-gray-500 bg-gray-900/10',
}

export default function Logs({ logs }: { logs: LogEntry[] }) {
  const [minLevel, setMinLevel] = useState<LogLevel>('Info')
  const [search, setSearch] = useState('')
  const [autoScroll, setAutoScroll] = useState(true)
  const bottomRef = useRef<HTMLDivElement>(null)

  const filtered = logs.filter(e =>
    levelOrder[e.level] <= levelOrder[minLevel] &&
    (!search || e.message.toLowerCase().includes(search.toLowerCase()) ||
     (e.source?.toLowerCase().includes(search.toLowerCase())))
  )

  useEffect(() => {
    if (autoScroll) bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [filtered.length, autoScroll])

  return (
    <div className="flex flex-col h-full">
      <div className="px-6 py-3 border-b border-surface-border flex items-center gap-3 flex-wrap">
        <h1 className="text-xl font-bold text-text-primary">Logs</h1>
        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Search..."
          className="bg-surface-card border border-surface-border rounded-lg px-3 py-1.5
                     text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-accent"
        />
        <select
          value={minLevel}
          onChange={e => setMinLevel(e.target.value as LogLevel)}
          className="bg-surface-card border border-surface-border rounded-lg px-3 py-1.5
                     text-sm text-text-primary focus:outline-none focus:border-accent"
        >
          {LEVELS.map(l => <option key={l} value={l}>{l}</option>)}
        </select>
        <button
          onClick={() => setAutoScroll(!autoScroll)}
          className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-colors
            ${autoScroll ? 'bg-accent text-white' : 'bg-surface-card text-text-muted'}`}
        >
          Auto-scroll
        </button>
        <span className="text-xs text-text-muted ml-auto">{filtered.length} entries</span>
      </div>

      <div className="flex-1 overflow-auto bg-[#0A0A14] font-mono text-xs p-4 space-y-0.5">
        {filtered.map((entry, i) => (
          <div key={i} className="flex items-start gap-3 hover:bg-white/5 px-2 py-0.5 rounded">
            <span className="text-gray-600 flex-shrink-0 w-24">
              {new Date(entry.timestamp).toLocaleTimeString('en', { hour12: false, second: '2-digit', hour: '2-digit', minute: '2-digit' })}
            </span>
            <span className={`flex-shrink-0 w-14 text-center rounded px-1 text-[10px] font-bold ${levelColors[entry.level]}`}>
              {entry.level.toUpperCase()}
            </span>
            {entry.source && (
              <span className="text-gray-500 flex-shrink-0">{entry.source}</span>
            )}
            <span className="text-gray-300 break-all">{entry.message}</span>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>
    </div>
  )
}
