import { useState } from 'react'
import { useSignalR } from './hooks/useSignalR'
import Dashboard from './components/Dashboard'
import Streams from './components/Streams'
import Sessions from './components/Sessions'
import Logs from './components/Logs'
import ServiceControl from './components/ServiceControl'

type Page = 'dashboard' | 'streams' | 'sessions' | 'logs' | 'service'

const navItems: { id: Page; label: string; icon: string }[] = [
  { id: 'dashboard', label: 'Dashboard', icon: '⌂' },
  { id: 'streams',   label: 'Streams',   icon: '▶' },
  { id: 'sessions',  label: 'Sessions',  icon: '⚯' },
  { id: 'logs',      label: 'Logs',      icon: '📜' },
  { id: 'service',   label: 'Service',   icon: '⚡' },
]

export default function App() {
  const [page, setPage] = useState<Page>('dashboard')
  const state = useSignalR()

  return (
    <div className="flex h-screen overflow-hidden bg-surface">
      {/* Sidebar */}
      <aside className="w-52 flex-shrink-0 bg-surface-card border-r border-surface-border flex flex-col">
        <div className="px-5 py-5 border-b border-surface-border">
          <div className="text-lg font-bold text-text-primary">MonaServer2</div>
          <div className="text-xs text-text-muted">GUI Manager</div>
        </div>

        <nav className="flex-1 py-2">
          {navItems.map(item => (
            <button
              key={item.id}
              onClick={() => setPage(item.id)}
              className={`w-full flex items-center gap-3 px-5 py-2.5 text-sm transition-colors
                ${page === item.id
                  ? 'bg-surface-elevated text-text-primary font-medium'
                  : 'text-text-secondary hover:bg-surface-elevated hover:text-text-primary'
                }`}
            >
              <span className="text-base">{item.icon}</span>
              {item.label}
            </button>
          ))}
        </nav>

        <div className="px-5 py-4 border-t border-surface-border">
          <div className="flex items-center gap-2">
            <div className={`w-2 h-2 rounded-full ${state.connected ? 'bg-accent-green' : 'bg-accent-red'}`} />
            <span className="text-xs text-text-muted">
              {state.connected ? 'Connected' : 'Disconnected'}
            </span>
          </div>
          <div className="text-[9px] text-surface-border mt-3 leading-snug">
            Powered by MonaServer2<br/>MonaSolutions / Haivision
          </div>
        </div>
      </aside>

      {/* Content */}
      <main className="flex-1 overflow-auto">
        {page === 'dashboard' && <Dashboard status={state.status} publications={state.publications} sessions={state.sessions} />}
        {page === 'streams'   && <Streams publications={state.publications} />}
        {page === 'sessions'  && <Sessions sessions={state.sessions} />}
        {page === 'logs'      && <Logs logs={state.logs} />}
        {page === 'service'   && <ServiceControl status={state.status} />}
      </main>
    </div>
  )
}
