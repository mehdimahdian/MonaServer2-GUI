import { useState, useEffect, useCallback } from 'react'
import type { ServerStatus, Publication, Session } from '../types'

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

export function useStatus() {
  const [status, setStatus] = useState<ServerStatus | null>(null)
  const refresh = useCallback(() =>
    fetchJson<ServerStatus>('/api/status').then(setStatus).catch(console.error), [])

  useEffect(() => {
    refresh()
    const id = setInterval(refresh, 5000)
    return () => clearInterval(id)
  }, [refresh])

  return { status, refresh }
}

export function usePublications() {
  const [publications, setPublications] = useState<Publication[]>([])
  const refresh = useCallback(() =>
    fetchJson<Publication[]>('/api/publications').then(setPublications).catch(console.error), [])

  useEffect(() => { refresh() }, [refresh])
  return { publications, refresh }
}

export function useSessions() {
  const [sessions, setSessions] = useState<Session[]>([])
  const refresh = useCallback(() =>
    fetchJson<Session[]>('/api/sessions').then(setSessions).catch(console.error), [])

  useEffect(() => { refresh() }, [refresh])
  return { sessions, refresh }
}

export async function startProcess() {
  await fetch('/api/process/start', { method: 'POST' })
}

export async function stopProcess() {
  await fetch('/api/process/stop', { method: 'POST' })
}

export async function restartProcess() {
  await fetch('/api/process/restart', { method: 'POST' })
}
