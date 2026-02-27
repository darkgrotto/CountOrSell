import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, AdminUserInfo, AdminStatusInfo } from '../services/api'
import { useAuth } from '../contexts/AuthContext'

// ── Shared helpers ────────────────────────────────────────────────────────────

type StatColor = 'blue' | 'green' | 'red' | 'purple' | 'amber' | 'gray'

function StatCard({ label, value, color = 'blue' }: {
  label: string
  value: string | number
  color?: StatColor
}) {
  const colorMap: Record<StatColor, string> = {
    blue:   'text-blue-600',
    green:  'text-green-600',
    red:    'text-red-600',
    purple: 'text-purple-600',
    amber:  'text-amber-600',
    gray:   'text-gray-400',
  }
  return (
    <div className="bg-white rounded-lg shadow p-4">
      <p className="text-xs text-gray-500 mb-1">{label}</p>
      <p className={`text-2xl font-bold ${colorMap[color]}`}>{value}</p>
    </div>
  )
}

// ── Users & Settings tab ──────────────────────────────────────────────────────

function AppSettingsPanel() {
  const queryClient = useQueryClient()
  const { data: regStatus, isLoading } = useQuery({
    queryKey: ['registration-status'],
    queryFn: () => api.getRegistrationStatus(),
  })

  const settingsMutation = useMutation({
    mutationFn: (enabled: boolean) => api.adminUpdateSettings(enabled),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['registration-status'] }),
  })

  const enabled = regStatus?.registrationsEnabled ?? true

  return (
    <div className="bg-white rounded-lg shadow p-4 mb-6">
      <h2 className="text-lg font-semibold mb-3">Application Settings</h2>
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-gray-700">Registrations</p>
          <p className="text-xs text-gray-500">
            {enabled ? 'New users can register accounts' : 'Registration is closed — only existing users can log in'}
          </p>
        </div>
        <button
          onClick={() => settingsMutation.mutate(!enabled)}
          disabled={isLoading || settingsMutation.isPending}
          className={`px-4 py-1.5 rounded text-sm font-medium transition-colors disabled:opacity-50 ${
            enabled
              ? 'bg-green-100 text-green-800 hover:bg-green-200'
              : 'bg-red-100 text-red-700 hover:bg-red-200'
          }`}
        >
          {enabled ? 'Enabled' : 'Disabled'}
        </button>
      </div>
    </div>
  )
}

function UsersPanel() {
  const { user: currentUser } = useAuth()
  const queryClient = useQueryClient()
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data: users = [], isLoading } = useQuery({
    queryKey: ['admin-users'],
    queryFn: () => api.listUsers(),
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, update }: { id: string; update: { displayName?: string; isAdmin?: boolean; isDisabled?: boolean } }) =>
      api.adminUpdateUser(id, update),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      setEditingId(null)
      setError(null)
    },
    onError: (err: Error) => setError(err.message),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.adminDeleteUser(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      setDeleteConfirmId(null)
      setError(null)
    },
    onError: (err: Error) => setError(err.message),
  })

  function startEdit(u: AdminUserInfo) {
    setEditingId(u.id)
    setEditingName(u.displayName ?? u.username)
    setError(null)
  }

  function saveEdit(id: string) {
    if (!editingName.trim()) { setError('Display name cannot be blank'); return }
    updateMutation.mutate({ id, update: { displayName: editingName.trim() } })
  }

  function toggleAdmin(u: AdminUserInfo) {
    if (u.id === currentUser?.id && u.isAdmin) {
      setError('Cannot demote your own account')
      return
    }
    setError(null)
    updateMutation.mutate({ id: u.id, update: { isAdmin: !u.isAdmin } })
  }

  function toggleDisabled(u: AdminUserInfo) {
    if (u.id === currentUser?.id && !u.isDisabled) {
      setError('Cannot disable your own account')
      return
    }
    setError(null)
    updateMutation.mutate({ id: u.id, update: { isDisabled: !u.isDisabled } })
  }

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
      </div>
    )
  }

  return (
    <>
      <AppSettingsPanel />

      <h2 className="text-lg font-semibold mb-3">Users</h2>

      {error && (
        <div className="mb-4 px-4 py-3 bg-red-100 border border-red-400 text-red-700 rounded">
          {error}
        </div>
      )}

      <div className="bg-white rounded-lg shadow overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              <th className="px-4 py-3 text-left font-semibold text-gray-600">Username</th>
              <th className="px-4 py-3 text-left font-semibold text-gray-600">Display Name</th>
              <th className="px-4 py-3 text-left font-semibold text-gray-600">Role</th>
              <th className="px-4 py-3 text-left font-semibold text-gray-600">Status</th>
              <th className="px-4 py-3 text-left font-semibold text-gray-600">Joined</th>
              <th className="px-4 py-3 text-left font-semibold text-gray-600">Last Login</th>
              <th className="px-4 py-3 text-right font-semibold text-gray-600">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map(u => {
              const isSelf = u.id === currentUser?.id
              return (
                <tr key={u.id} className={u.isDisabled ? 'bg-gray-50 opacity-60' : 'hover:bg-gray-50'}>
                  {/* Username */}
                  <td className="px-4 py-3 font-mono text-gray-800">{u.username}</td>

                  {/* Display Name — inline edit */}
                  <td className="px-4 py-3">
                    {editingId === u.id ? (
                      <div className="flex items-center gap-1">
                        <input
                          value={editingName}
                          onChange={e => setEditingName(e.target.value)}
                          onKeyDown={e => {
                            if (e.key === 'Enter') saveEdit(u.id)
                            if (e.key === 'Escape') setEditingId(null)
                          }}
                          className="border border-gray-300 rounded px-2 py-0.5 text-sm w-32 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                          autoFocus
                        />
                        <button
                          onClick={() => saveEdit(u.id)}
                          className="text-green-600 hover:text-green-800 font-bold px-1"
                          title="Save"
                        >✓</button>
                        <button
                          onClick={() => setEditingId(null)}
                          className="text-gray-400 hover:text-gray-600 px-1"
                          title="Cancel"
                        >✕</button>
                      </div>
                    ) : (
                      <button
                        onClick={() => startEdit(u)}
                        className="text-left hover:text-blue-600 group flex items-center gap-1"
                      >
                        {u.displayName ?? u.username}
                        <span className="text-gray-300 group-hover:text-blue-400 text-xs">✎</span>
                      </button>
                    )}
                  </td>

                  {/* Role toggle */}
                  <td className="px-4 py-3">
                    <button
                      onClick={() => toggleAdmin(u)}
                      className={`px-2 py-0.5 rounded text-xs font-medium transition-colors ${
                        u.isAdmin
                          ? 'bg-purple-100 text-purple-800 hover:bg-purple-200'
                          : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                      }`}
                    >
                      {u.isAdmin ? 'Admin' : 'User'}
                    </button>
                  </td>

                  {/* Status toggle */}
                  <td className="px-4 py-3">
                    <button
                      onClick={() => toggleDisabled(u)}
                      disabled={isSelf}
                      className={`px-2 py-0.5 rounded text-xs font-medium transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
                        u.isDisabled
                          ? 'bg-red-100 text-red-700 hover:bg-red-200'
                          : 'bg-green-100 text-green-700 hover:bg-green-200'
                      }`}
                      title={isSelf ? 'Cannot change your own status' : undefined}
                    >
                      {u.isDisabled ? 'Disabled' : 'Active'}
                    </button>
                  </td>

                  {/* Joined */}
                  <td className="px-4 py-3 text-gray-500 whitespace-nowrap">
                    {new Date(u.createdAt).toLocaleDateString()}
                  </td>

                  {/* Last Login */}
                  <td className="px-4 py-3 text-gray-500 whitespace-nowrap">
                    {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : '—'}
                  </td>

                  {/* Delete */}
                  <td className="px-4 py-3 text-right">
                    {deleteConfirmId === u.id ? (
                      <div className="flex items-center gap-2 justify-end">
                        <span className="text-gray-500 text-xs">Delete?</span>
                        <button
                          onClick={() => deleteMutation.mutate(u.id)}
                          className="text-red-600 hover:text-red-800 text-xs font-medium"
                        >
                          Yes
                        </button>
                        <button
                          onClick={() => setDeleteConfirmId(null)}
                          className="text-gray-400 hover:text-gray-600 text-xs"
                        >
                          No
                        </button>
                      </div>
                    ) : (
                      <button
                        onClick={() => { if (!isSelf) { setError(null); setDeleteConfirmId(u.id) } }}
                        disabled={isSelf}
                        className="text-red-400 hover:text-red-600 text-xs disabled:opacity-30 disabled:cursor-not-allowed"
                        title={isSelf ? 'Cannot delete your own account' : `Delete ${u.username}`}
                      >
                        Delete
                      </button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>

        {users.length === 0 && (
          <div className="text-center py-12 text-gray-400">No users found</div>
        )}
      </div>
    </>
  )
}

// ── System Status tab ─────────────────────────────────────────────────────────

function StatusPanel() {
  const { data: status, isLoading, isError } = useQuery({
    queryKey: ['admin-status'],
    queryFn: () => api.getAdminStatus(),
  })

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
      </div>
    )
  }

  if (isError || !status) {
    return <div className="text-red-600 py-4">Failed to load status.</div>
  }

  return <StatusContent status={status} />
}

function StatusContent({ status }: { status: AdminStatusInfo }) {
  const imagePct = status.totalCards > 0
    ? Math.round((status.cardsWithImages / status.totalCards) * 100)
    : 0

  const fmt = (n: number) => n.toLocaleString()

  return (
    <div className="space-y-8">

      {/* ── Users ── */}
      <section>
        <h2 className="text-lg font-semibold mb-3">Users</h2>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <StatCard label="Total" value={fmt(status.totalUsers)} />
          <StatCard label="Active" value={fmt(status.activeUsers)} color="green" />
          <StatCard label="Disabled" value={fmt(status.disabledUsers)} color={status.disabledUsers > 0 ? 'red' : 'gray'} />
          <StatCard label="Admins" value={fmt(status.adminUsers)} color="purple" />
        </div>
      </section>

      {/* ── Card Data ── */}
      <section>
        <h2 className="text-lg font-semibold mb-3">Card Data</h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
          <StatCard label="Sets" value={fmt(status.totalSets)} />
          <StatCard label="Cards" value={fmt(status.totalCards)} />
          <StatCard
            label="Last Synced"
            value={status.lastCardSyncedAt
              ? new Date(status.lastCardSyncedAt).toLocaleDateString()
              : 'Never'}
            color={status.lastCardSyncedAt ? 'blue' : 'gray'}
          />
        </div>
      </section>

      {/* ── Local Images ── */}
      <section>
        <h2 className="text-lg font-semibold mb-3">Local Images</h2>
        <div className="bg-white rounded-lg shadow p-4">
          <div className="flex justify-between text-sm mb-2">
            <span className="text-gray-600">Coverage</span>
            <span className="font-medium text-gray-800">
              {fmt(status.cardsWithImages)} / {fmt(status.totalCards)} cards
              <span className="ml-2 text-blue-600 font-bold">({imagePct}%)</span>
            </span>
          </div>
          <div className="w-full bg-gray-200 rounded-full h-3">
            <div
              className="bg-blue-500 h-3 rounded-full transition-all duration-500"
              style={{ width: `${imagePct}%` }}
            />
          </div>
          {status.totalCards > status.cardsWithImages && (
            <p className="text-xs text-gray-400 mt-2">
              {fmt(status.totalCards - status.cardsWithImages)} card{status.totalCards - status.cardsWithImages !== 1 ? 's' : ''} missing images
            </p>
          )}
        </div>
      </section>

      {/* ── Collection Activity ── */}
      <section>
        <h2 className="text-lg font-semibold mb-3">
          Collection Activity
          <span className="ml-2 text-sm font-normal text-gray-400">across all users</span>
        </h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
          <StatCard label="Total Copies Owned" value={fmt(status.totalOwnedCopies)} />
          <StatCard label="Unique Cards Owned" value={fmt(status.totalUniqueCardsOwned)} />
          <StatCard label="Ownership Records" value={fmt(status.totalOwnershipRecords)} color="gray" />
          <StatCard label="Reserve List Cards Owned" value={fmt(status.reserveListCardsOwned)} color="amber" />
          <StatCard
            label="Boosters Owned"
            value={status.totalBoostersDefined > 0
              ? `${fmt(status.totalBoostersOwned)} / ${fmt(status.totalBoostersDefined)}`
              : '—'}
          />
        </div>
      </section>

    </div>
  )
}

// ── SPH Catalog Panel ─────────────────────────────────────────────────────────

function SphCatalogPanel() {
  const queryClient = useQueryClient()
  const { data: checkResult, isLoading: isChecking, refetch: recheckUpdate } = useQuery({
    queryKey: ['sph-catalog-check'],
    queryFn: () => api.checkSphUpdate(),
    retry: false,
  })

  const applyMutation = useMutation({
    mutationFn: () => api.applySphUpdate(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sph-catalog-check'] })
      queryClient.invalidateQueries({ queryKey: ['sph-products'] })
    },
  })

  return (
    <div className="bg-white rounded-lg shadow p-4 mb-6">
      <h2 className="text-lg font-semibold mb-3">Sealed Product Catalog</h2>
      <p className="text-sm text-gray-500 mb-4">
        Download the latest sealed product catalog from countorsell.com.
        Used when SealedProdHelper is not directly connected to this instance.
      </p>

      {isChecking && <p className="text-sm text-gray-400">Checking for updates…</p>}

      {checkResult && !isChecking && (
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <p className="text-gray-500 text-xs mb-0.5">Local version</p>
              <p className="font-mono font-medium">{checkResult.localVersion ?? 'None'}</p>
              {checkResult.localProductCount != null && (
                <p className="text-xs text-gray-400">{checkResult.localProductCount} products</p>
              )}
            </div>
            <div>
              <p className="text-gray-500 text-xs mb-0.5">Remote version</p>
              <p className="font-mono font-medium">{checkResult.remoteVersion ?? '—'}</p>
              {checkResult.remoteProductCount != null && (
                <p className="text-xs text-gray-400">{checkResult.remoteProductCount} products</p>
              )}
            </div>
          </div>

          {checkResult.fetchError && (
            <p className="text-xs text-red-500">Remote check failed: {checkResult.fetchError}</p>
          )}

          <div className="flex items-center gap-3 pt-1">
            {checkResult.updateAvailable ? (
              <button
                onClick={() => applyMutation.mutate()}
                disabled={applyMutation.isPending}
                className="px-4 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm font-medium disabled:opacity-50"
              >
                {applyMutation.isPending ? 'Applying…' : 'Apply Update'}
              </button>
            ) : (
              <span className="text-sm text-green-600 font-medium">✓ Up to date</span>
            )}
            <button
              onClick={() => recheckUpdate()}
              className="text-sm text-gray-500 hover:text-gray-700 underline"
            >
              Re-check
            </button>
          </div>

          {applyMutation.isSuccess && (
            <p className="text-sm text-green-600">
              ✓ Applied version {applyMutation.data.version} ({applyMutation.data.productCount} products)
            </p>
          )}
          {applyMutation.isError && (
            <p className="text-sm text-red-500">
              Failed: {String((applyMutation.error as Error)?.message ?? 'Unknown error')}
            </p>
          )}
        </div>
      )}
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

type Tab = 'users' | 'status' | 'catalog'

export default function AdminPage() {
  const [tab, setTab] = useState<Tab>('users')

  const tabs: { id: Tab; label: string }[] = [
    { id: 'users',   label: 'Users & Settings' },
    { id: 'status',  label: 'System Status' },
    { id: 'catalog', label: 'Sealed Catalog' },
  ]

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Admin</h1>

      {/* Tab bar */}
      <div className="flex border-b border-gray-200 mb-6">
        {tabs.map(t => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`px-5 py-2.5 text-sm font-medium border-b-2 transition-colors ${
              tab === t.id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'users'   && <UsersPanel />}
      {tab === 'status'  && <StatusPanel />}
      {tab === 'catalog' && <SphCatalogPanel />}
    </div>
  )
}
