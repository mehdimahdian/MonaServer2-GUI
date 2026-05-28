import { useState } from 'react'
import type { ServerStatus } from '../types'
import { startProcess, stopProcess, restartProcess } from '../hooks/useApi'

export default function ServiceControl({ status }: { status: ServerStatus | null }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const running = status?.isRunning ?? false

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try { await action() }
    catch (e) { setError(String(e)) }
    finally { setBusy(false) }
  }

  return (
    <div className="p-8 max-w-xl space-y-6">
      <h1 className="text-2xl font-bold text-text-primary">Service Control</h1>

      {/* Status */}
      <div className="bg-surface-card rounded-xl p-6 border border-surface-border">
        <div className="flex items-center gap-3 mb-2">
          <div className={`w-3 h-3 rounded-full ${running ? 'bg-accent-green' : 'bg-accent-red'}`} />
          <span className="text-text-primary font-semibold">MonaServer2</span>
        </div>
        <div className="text-sm text-text-muted">
          {running ? `Running — PID ${status?.processId}` : 'Stopped'}
        </div>
        {status?.version && (
          <div className="text-xs text-text-muted mt-1">Version: {status.version}</div>
        )}
      </div>

      {/* Controls */}
      <div className="bg-surface-card rounded-xl p-6 border border-surface-border space-y-4">
        <div className="text-xs uppercase tracking-widest text-text-muted">Process Control</div>
        <div className="flex gap-3">
          <button
            onClick={() => run(startProcess)}
            disabled={running || busy}
            className="px-6 py-2.5 rounded-lg font-medium text-sm bg-green-900/40 text-accent-green
                       disabled:opacity-40 hover:bg-green-900/60 transition-colors"
          >
            Start
          </button>
          <button
            onClick={() => run(stopProcess)}
            disabled={!running || busy}
            className="px-6 py-2.5 rounded-lg font-medium text-sm bg-red-900/40 text-accent-red
                       disabled:opacity-40 hover:bg-red-900/60 transition-colors"
          >
            Stop
          </button>
          <button
            onClick={() => run(restartProcess)}
            disabled={!running || busy}
            className="px-6 py-2.5 rounded-lg font-medium text-sm bg-yellow-900/40 text-yellow-400
                       disabled:opacity-40 hover:bg-yellow-900/60 transition-colors"
          >
            Restart
          </button>
        </div>
        {busy && (
          <div className="h-0.5 bg-surface-elevated rounded overflow-hidden">
            <div className="h-full bg-accent animate-pulse w-full" />
          </div>
        )}
      </div>

      {error && (
        <div className="bg-red-900/30 border border-red-800 rounded-xl p-4 text-sm text-accent-red">
          {error}
        </div>
      )}

      {/* Web UI link */}
      <div className="bg-surface-card rounded-xl p-6 border border-surface-border text-sm text-text-secondary">
        This is the web dashboard served at <span className="font-mono text-accent">localhost:8080</span>.
        The desktop app connects to the same service.
      </div>

      {/* Credit */}
      <p className="text-xs text-surface-border leading-relaxed">
        MonaServer2 GUI manages the MonaServer2 binary developed by MonaSolutions and sponsored by Haivision.
        MonaServer2 is licensed under GPL-3.0.{' '}
        <a href="https://github.com/MonaSolutions/MonaServer2" className="text-accent hover:underline" target="_blank" rel="noreferrer">
          github.com/MonaSolutions/MonaServer2
        </a>
      </p>
    </div>
  )
}
