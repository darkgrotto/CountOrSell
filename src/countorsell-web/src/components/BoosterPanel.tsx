import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, BOOSTER_TYPES, BoosterDefinition } from '../services/api'

interface BoosterPanelProps {
  setCode: string
}

export default function BoosterPanel({ setCode }: BoosterPanelProps) {
  const queryClient = useQueryClient()
  const [newType, setNewType] = useState<string>(BOOSTER_TYPES[0])
  const [newVariant, setNewVariant] = useState('')
  const [newImageUrl, setNewImageUrl] = useState('')
  const [showForm, setShowForm] = useState(false)

  const { data: boosters = [], isLoading } = useQuery({
    queryKey: ['boosters', setCode],
    queryFn: () => api.getBoostersForSet(setCode),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      api.createBooster({
        setCode,
        boosterType: newType,
        artVariant: newVariant,
        imageUrl: newImageUrl || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['boosters', setCode] })
      queryClient.invalidateQueries({ queryKey: ['allBoosters'] })
      setNewVariant('')
      setNewImageUrl('')
      setShowForm(false)
    },
  })

  const toggleOwnedMutation = useMutation({
    mutationFn: (booster: BoosterDefinition) =>
      api.setBoosterOwned(booster.id, !booster.owned),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['boosters', setCode] })
      queryClient.invalidateQueries({ queryKey: ['allBoosters'] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.deleteBooster(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['boosters', setCode] })
      queryClient.invalidateQueries({ queryKey: ['allBoosters'] })
    },
  })

  // Group boosters by type
  const grouped = boosters.reduce<Record<string, BoosterDefinition[]>>((acc, b) => {
    if (!acc[b.boosterType]) acc[b.boosterType] = []
    acc[b.boosterType].push(b)
    return acc
  }, {})

  const ownedCount = boosters.filter((b) => b.owned).length

  return (
    <div className="bg-white rounded-lg shadow-md p-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-bold">Booster Packs</h3>
        <span className="text-sm text-gray-500">
          {ownedCount}/{boosters.length} owned
        </span>
      </div>

      {isLoading ? (
        <div className="text-center py-4 text-gray-400">Loading...</div>
      ) : boosters.length === 0 && !showForm ? (
        <p className="text-gray-500 text-sm mb-4">No boosters tracked yet.</p>
      ) : (
        <div className="space-y-4 mb-4">
          {Object.entries(grouped).map(([type, items]) => (
            <div key={type}>
              <h4 className="text-sm font-semibold text-gray-600 mb-2">{type}</h4>
              <div className="space-y-2">
                {items.map((booster) => (
                  <div
                    key={booster.id}
                    className="flex items-center gap-3 p-2 rounded border border-gray-100 hover:bg-gray-50"
                  >
                    <input
                      type="checkbox"
                      checked={booster.owned}
                      onChange={() => toggleOwnedMutation.mutate(booster)}
                      className="w-4 h-4 text-blue-600 rounded"
                    />
                    {booster.imageUrl && (
                      <img
                        src={booster.imageUrl}
                        alt={booster.artVariant}
                        className="w-10 h-14 object-cover rounded"
                      />
                    )}
                    <span className={`flex-1 text-sm ${booster.owned ? 'line-through text-gray-400' : ''}`}>
                      {booster.artVariant}
                    </span>
                    <button
                      onClick={() => deleteMutation.mutate(booster.id)}
                      className="text-red-400 hover:text-red-600 text-xs"
                      title="Delete"
                    >
                      X
                    </button>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {showForm ? (
        <div className="border-t pt-4 space-y-3">
          <select
            value={newType}
            onChange={(e) => setNewType(e.target.value)}
            className="w-full px-3 py-2 border rounded text-sm"
          >
            {BOOSTER_TYPES.map((type) => (
              <option key={type} value={type}>{type}</option>
            ))}
          </select>
          <input
            type="text"
            placeholder="Art variant name (e.g., 'Jace Art')"
            value={newVariant}
            onChange={(e) => setNewVariant(e.target.value)}
            className="w-full px-3 py-2 border rounded text-sm"
          />
          <input
            type="text"
            placeholder="Image URL (optional)"
            value={newImageUrl}
            onChange={(e) => setNewImageUrl(e.target.value)}
            className="w-full px-3 py-2 border rounded text-sm"
          />
          <div className="flex gap-2">
            <button
              onClick={() => createMutation.mutate()}
              disabled={!newVariant.trim() || createMutation.isPending}
              className="flex-1 bg-blue-600 text-white px-4 py-2 rounded text-sm hover:bg-blue-700 disabled:opacity-50"
            >
              {createMutation.isPending ? 'Adding...' : 'Add Booster'}
            </button>
            <button
              onClick={() => setShowForm(false)}
              className="px-4 py-2 border rounded text-sm hover:bg-gray-50"
            >
              Cancel
            </button>
          </div>
          {createMutation.isError && (
            <p className="text-red-500 text-xs">{createMutation.error.message}</p>
          )}
        </div>
      ) : (
        <button
          onClick={() => setShowForm(true)}
          className="w-full bg-blue-600 text-white px-4 py-2 rounded text-sm hover:bg-blue-700"
        >
          + Add Booster
        </button>
      )}
    </div>
  )
}
