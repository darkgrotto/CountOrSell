import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, BoosterDefinition } from '../services/api'

type OwnedFilter = 'all' | 'owned' | 'unowned'

export default function BoostersPage() {
  const queryClient = useQueryClient()
  const [filterSet, setFilterSet] = useState<string>('all')
  const [filterType, setFilterType] = useState<string>('all')
  const [filterOwned, setFilterOwned] = useState<OwnedFilter>('all')

  const { data: boosters = [], isLoading, error } = useQuery({
    queryKey: ['allBoosters'],
    queryFn: () => api.getAllBoosters(),
  })

  const { data: allSets = [] } = useQuery({
    queryKey: ['sets'],
    queryFn: () => api.getSets(),
  })

  const setNameMap = useMemo(() => {
    const map: Record<string, string> = {}
    for (const s of allSets) {
      map[s.code] = s.name
    }
    return map
  }, [allSets])

  const toggleOwnedMutation = useMutation({
    mutationFn: (booster: BoosterDefinition) =>
      api.setBoosterOwned(booster.id, !booster.owned),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['allBoosters'] })
      queryClient.invalidateQueries({ queryKey: ['boosters'] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.deleteBooster(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['allBoosters'] })
      queryClient.invalidateQueries({ queryKey: ['boosters'] })
    },
  })

  const sets = useMemo(() => [...new Set(boosters.map((b) => b.setCode))].sort(), [boosters])
  const types = useMemo(() => [...new Set(boosters.map((b) => b.boosterType))].sort(), [boosters])

  const filteredBoosters = useMemo(() => {
    return boosters.filter((b) => {
      const matchesSet = filterSet === 'all' || b.setCode === filterSet
      const matchesType = filterType === 'all' || b.boosterType === filterType
      const matchesOwned =
        filterOwned === 'all' ||
        (filterOwned === 'owned' && b.owned) ||
        (filterOwned === 'unowned' && !b.owned)
      return matchesSet && matchesType && matchesOwned
    })
  }, [boosters, filterSet, filterType, filterOwned])

  // Group filtered boosters by set+type, keyed as "setCode|boosterType"
  const groupedBySetType = useMemo(() => {
    const groups: Record<string, BoosterDefinition[]> = {}
    for (const b of filteredBoosters) {
      const key = `${b.setCode}|${b.boosterType}`
      if (!groups[key]) groups[key] = []
      groups[key].push(b)
    }
    return groups
  }, [filteredBoosters])

  const ownedCount = boosters.filter((b) => b.owned).length
  const completionPct = boosters.length > 0 ? ((ownedCount / boosters.length) * 100).toFixed(1) : '0'

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        <span className="ml-4 text-gray-500">Loading boosters...</span>
      </div>
    )
  }

  if (error) {
    return (
      <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
        Error loading boosters: {error.message}
      </div>
    )
  }

  return (
    <div>
      <h1 className="text-3xl font-bold mb-2">Boosters</h1>
      <p className="text-gray-600 mb-6">
        Track your booster pack collection across all sets.
      </p>

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-blue-600">{boosters.length}</div>
          <div className="text-sm text-gray-500">Total Boosters</div>
        </div>
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-green-600">{ownedCount}</div>
          <div className="text-sm text-gray-500">Owned</div>
        </div>
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-green-600">{completionPct}%</div>
          <div className="text-sm text-gray-500">Completion</div>
        </div>
      </div>

      {/* Filters */}
      <div className="mb-6 flex flex-col sm:flex-row gap-4">
        <select
          value={filterSet}
          onChange={(e) => setFilterSet(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All Sets</option>
          {sets.map((s) => (
            <option key={s} value={s}>{setNameMap[s] ?? s.toUpperCase()} ({s.toUpperCase()})</option>
          ))}
        </select>
        <select
          value={filterType}
          onChange={(e) => setFilterType(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All Types</option>
          {types.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
        <select
          value={filterOwned}
          onChange={(e) => setFilterOwned(e.target.value as OwnedFilter)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All</option>
          <option value="owned">Owned</option>
          <option value="unowned">Not Owned</option>
        </select>
      </div>

      <div className="text-sm text-gray-600 mb-4">
        Showing {filteredBoosters.length} of {boosters.length} boosters
      </div>

      {/* Grouped by Set + Type */}
      {Object.keys(groupedBySetType).length === 0 && (
        <div className="text-center py-12 text-gray-500">
          No boosters found matching your criteria
        </div>
      )}

      <div className="space-y-4">
        {Object.entries(groupedBySetType)
          .sort(([a], [b]) => {
            const [aSet, aType] = a.split('|')
            const [bSet, bType] = b.split('|')
            const aLabel = `${setNameMap[aSet] ?? aSet.toUpperCase()} (${aSet.toUpperCase()}) - ${aType}`
            const bLabel = `${setNameMap[bSet] ?? bSet.toUpperCase()} (${bSet.toUpperCase()}) - ${bType}`
            return aLabel.localeCompare(bLabel)
          })
          .map(([key, items]) => {
            const [setCode, boosterType] = key.split('|')
            const setName = setNameMap[setCode] ?? setCode.toUpperCase()
            return (
              <div key={key} className="bg-white rounded-lg shadow-md p-4">
                <h2 className="text-lg font-bold mb-3">
                  {setName} ({setCode.toUpperCase()}) - {boosterType}
                </h2>

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
            )
          })}
      </div>
    </div>
  )
}
