import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, AdminUserInfo } from '../services/api'
import { useAuth } from '../contexts/AuthContext'

export default function AdminPage() {
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
    <div>
      <h1 className="text-2xl font-bold mb-6">User Management</h1>

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
    </div>
  )
}
