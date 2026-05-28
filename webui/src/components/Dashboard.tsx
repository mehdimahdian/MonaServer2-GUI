import type { ServerStatus, Publication, Session } from '../types'

function formatBytes(bps: number) {
  if (bps >= 1_073_741_824) return `${(bps / 1_073_741_824).toFixed(1)} GB/s`
  if (bps >= 1_048_576) return `${(bps / 1_048_576).toFixed(1)} MB/s`
  if (bps >= 1024) return `${(bps / 1024).toFixed(1)} KB/s`
  return `${bps} B/s`
}

function formatUptime(startedAt: string) {
  const secs = Math.floor((Date.now() - new Date(startedAt).getTime()) / 1000)
  const h = Math.floor(secs / 3600)
  const m = Math.floor((secs % 3600) / 60)
  const s = secs % 60
  if (h >= 24) return `${Math.floor(h / 24)}d ${h % 24}h ${m}m`
  return `${h.toString().padStart(2,'0')}h ${m.toString().padStart(2,'0')}m ${s.toString().padStart(2,'0')}s`
}

interface Props {
  status: ServerStatus | null
  publications: Publication[]
  sessions: Session[]
}

function StatCard({ label, value, sub, color = 'text-text-primary' }: {
  label: string; value: string | number; sub?: string; color?: string
}) {
  return (
    <div className="bg-surface-card rounded-xl p-5 border border-surface-border">
      <div className="text-[10px] uppercase tracking-widest text-text-muted mb-2">{label}</div>
      <div className={`text-3xl font-bold ${color}`}>{value}</div>
      {sub && <div className="text-xs text-text-muted mt-1">{sub}</div>}
    </div>
  )
}

export default function Dashboard({ status, publications, sessions: _sessions }: Props) {
  const running = status?.isRunning ?? false

  return (
    <div className="p-8 space-y-6 max-w-5xl">
      {/* Header */}
      <div className="flex items-center gap-4">
        <h1 className="text-2xl font-bold text-text-primary">Dashboard</h1>
        <span className={`px-3 py-1 rounded-full text-xs font-semibold
          ${running ? 'bg-green-900/40 text-accent-green' : 'bg-red-900/40 text-accent-red'}`}>
          {running ? 'Running' : 'Stopped'}
        </span>
        {status?.version && (
          <span className="text-xs text-text-muted">v{status.version}</span>
        )}
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard
          label="Uptime"
          value={running && status?.startedAt ? formatUptime(status.startedAt) : '--'}
        />
        <StatCard label="Connections" value={status?.totalConnections ?? 0} />
        <StatCard label="Publications" value={publications.length} />
        <StatCard label="PID" value={status?.processId || '--'} />
      </div>

      {/* Bandwidth */}
      <div className="grid grid-cols-2 gap-4">
        <StatCard
          label="Bandwidth In"
          value={formatBytes(status?.totalByteRateIn ?? 0)}
          color="text-accent-blue"
        />
        <StatCard
          label="Bandwidth Out"
          value={formatBytes(status?.totalByteRateOut ?? 0)}
          color="text-yellow-400"
        />
      </div>

      {/* Protocol breakdown */}
      {status?.connectionsByProtocol && Object.keys(status.connectionsByProtocol).length > 0 && (
        <div className="bg-surface-card rounded-xl p-5 border border-surface-border">
          <div className="text-[10px] uppercase tracking-widest text-text-muted mb-4">Connections by Protocol</div>
          <div className="flex flex-wrap gap-3">
            {Object.entries(status.connectionsByProtocol).map(([proto, count]) => (
              <div key={proto} className="flex items-center gap-2 bg-surface-elevated px-4 py-2 rounded-lg">
                <span className="text-text-muted text-sm">{proto}</span>
                <span className="text-text-primary font-bold text-lg">{count}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Recent publications mini-table */}
      {publications.length > 0 && (
        <div className="bg-surface-card rounded-xl border border-surface-border overflow-hidden">
          <div className="px-5 py-3 border-b border-surface-border text-[10px] uppercase tracking-widest text-text-muted">
            Active Publications
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-text-muted text-xs">
                <th className="px-5 py-2 text-left">Name</th>
                <th className="px-5 py-2 text-left">Protocol</th>
                <th className="px-5 py-2 text-right">Subscribers</th>
                <th className="px-5 py-2 text-right">In</th>
                <th className="px-5 py-2 text-right">Out</th>
              </tr>
            </thead>
            <tbody>
              {publications.slice(0, 8).map(pub => (
                <tr key={pub.name} className="border-t border-surface-border hover:bg-surface-elevated">
                  <td className="px-5 py-2 text-text-primary font-mono text-xs">{pub.name}</td>
                  <td className="px-5 py-2 text-text-muted">{pub.protocol}</td>
                  <td className="px-5 py-2 text-right text-text-secondary">{pub.subscriberCount}</td>
                  <td className="px-5 py-2 text-right text-accent-blue text-xs font-mono">{formatBytes(pub.byteRateIn)}</td>
                  <td className="px-5 py-2 text-right text-yellow-400 text-xs font-mono">{formatBytes(pub.byteRateOut)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
