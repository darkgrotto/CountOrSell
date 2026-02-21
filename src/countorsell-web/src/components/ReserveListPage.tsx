import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ReserveListCard } from '../services/api'
import CardDetailModal from './CardDetailModal'

type SortField = 'name' | 'price' | 'set'
type OwnedFilter = 'all' | 'owned' | 'unowned'

export default function ReserveListPage() {
  const queryClient = useQueryClient()
  const [selectedCard, setSelectedCard] = useState<ReserveListCard | null>(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [filterRarity, setFilterRarity] = useState<string>('all')
  const [filterSet, setFilterSet] = useState<string>('all')
  const [filterOwned, setFilterOwned] = useState<OwnedFilter>('all')
  const [sortBy, setSortBy] = useState<SortField>('name')

  const { data: cards = [], isLoading, error } = useQuery({
    queryKey: ['reservelist'],
    queryFn: () => api.getReserveList(),
  })

  const toggleOwnedMutation = useMutation({
    mutationFn: (card: ReserveListCard) =>
      api.setReserveListOwned(card.id, !card.owned, card.name, card.set),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reservelist'] })
    },
  })

  const sets = useMemo(() => [...new Set(cards.map((c) => c.set))].sort(), [cards])
  const rarities = useMemo(() => [...new Set(cards.map((c) => c.rarity))], [cards])

  const filteredCards = useMemo(() => {
    let result = cards.filter((card) => {
      const matchesSearch =
        card.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        card.collector_number.includes(searchTerm)
      const matchesRarity = filterRarity === 'all' || card.rarity === filterRarity
      const matchesSet = filterSet === 'all' || card.set === filterSet
      const matchesOwned =
        filterOwned === 'all' ||
        (filterOwned === 'owned' && card.owned) ||
        (filterOwned === 'unowned' && !card.owned)
      return matchesSearch && matchesRarity && matchesSet && matchesOwned
    })

    result.sort((a, b) => {
      switch (sortBy) {
        case 'price': {
          const priceA = parseFloat(a.prices?.usd || '0')
          const priceB = parseFloat(b.prices?.usd || '0')
          return priceB - priceA
        }
        case 'set':
          return a.set.localeCompare(b.set) || a.name.localeCompare(b.name)
        default:
          return a.name.localeCompare(b.name)
      }
    })

    return result
  }, [cards, searchTerm, filterRarity, filterSet, filterOwned, sortBy])

  const ownedCount = cards.filter((c) => c.owned).length
  const totalValue = cards
    .filter((c) => c.owned && c.prices?.usd)
    .reduce((sum, c) => sum + parseFloat(c.prices!.usd!), 0)

  const getCardImage = (card: ReserveListCard): string | undefined => {
    if (card.image_uris?.normal || card.card_faces?.[0]?.image_uris?.normal)
      return `/api/images/${card.id}`
    return undefined
  }

  const getCardImageFallback = (card: ReserveListCard): string | undefined => {
    if (card.image_uris?.normal) return card.image_uris.normal
    if (card.card_faces?.[0]?.image_uris?.normal) return card.card_faces[0].image_uris.normal
    return undefined
  }

  const getRarityColor = (rarity: string): string => {
    switch (rarity) {
      case 'common': return 'bg-gray-200 text-gray-700'
      case 'uncommon': return 'bg-gray-400 text-white'
      case 'rare': return 'bg-yellow-500 text-white'
      case 'mythic': return 'bg-orange-600 text-white'
      default: return 'bg-blue-600 text-white'
    }
  }

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        <span className="ml-4 text-gray-500">Loading reserve list (this may take a moment)...</span>
      </div>
    )
  }

  if (error) {
    return (
      <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
        Error loading reserve list: {error.message}
      </div>
    )
  }

  return (
    <div>
      <h1 className="text-3xl font-bold mb-2">Reserve List</h1>
      <p className="text-gray-600 mb-6">
        Cards on the Magic: The Gathering reserve list will never be reprinted.
      </p>

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-blue-600">{cards.length}</div>
          <div className="text-sm text-gray-500">Total Cards</div>
        </div>
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-green-600">{ownedCount}</div>
          <div className="text-sm text-gray-500">Owned</div>
        </div>
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-green-600">${totalValue.toFixed(2)}</div>
          <div className="text-sm text-gray-500">Owned Value</div>
        </div>
      </div>

      {/* Filters */}
      <div className="mb-6 flex flex-col sm:flex-row gap-4">
        <input
          type="text"
          placeholder="Search cards..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="flex-1 px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
        />
        <select
          value={filterSet}
          onChange={(e) => setFilterSet(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All Sets</option>
          {sets.map((s) => (
            <option key={s} value={s}>{s.toUpperCase()}</option>
          ))}
        </select>
        <select
          value={filterRarity}
          onChange={(e) => setFilterRarity(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All Rarities</option>
          {rarities.map((r) => (
            <option key={r} value={r}>{r.charAt(0).toUpperCase() + r.slice(1)}</option>
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
        <select
          value={sortBy}
          onChange={(e) => setSortBy(e.target.value as SortField)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="name">Sort by Name</option>
          <option value="price">Sort by Price</option>
          <option value="set">Sort by Set</option>
        </select>
      </div>

      <div className="text-sm text-gray-600 mb-4">
        Showing {filteredCards.length} of {cards.length} cards
      </div>

      {/* Card Grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
        {filteredCards.map((card) => (
          <div
            key={card.id}
            className={`bg-white rounded-lg shadow hover:shadow-lg transition-shadow overflow-hidden group ${
              card.owned ? 'ring-2 ring-green-500' : ''
            }`}
          >
            <div className="aspect-[5/7] relative cursor-pointer" onClick={() => setSelectedCard(card)}>
              {getCardImage(card) ? (
                <img
                  src={getCardImage(card)}
                  alt={card.name}
                  className="w-full h-full object-cover"
                  loading="lazy"
                  onError={(e) => {
                    const fallback = getCardImageFallback(card)
                    if (fallback && e.currentTarget.src !== fallback) {
                      e.currentTarget.src = fallback
                    }
                  }}
                />
              ) : (
                <div className="w-full h-full bg-gray-200 flex items-center justify-center">
                  <span className="text-gray-400 text-xs text-center px-2">{card.name}</span>
                </div>
              )}

              <div className="absolute top-2 left-2 bg-black/70 text-white text-xs px-2 py-1 rounded">
                #{card.collector_number}
              </div>

              <div className={`absolute top-2 right-2 text-xs px-2 py-1 rounded capitalize ${getRarityColor(card.rarity)}`}>
                {card.rarity.charAt(0).toUpperCase()}
              </div>

              <div className="absolute bottom-2 left-2 bg-red-600 text-white text-xs font-bold px-1.5 py-0.5 rounded">
                RL
              </div>

              {/* Owned toggle overlay */}
              <button
                onClick={(e) => { e.stopPropagation(); toggleOwnedMutation.mutate(card); }}
                className={`absolute bottom-2 right-2 text-xs px-2 py-1 rounded font-bold ${
                  card.owned
                    ? 'bg-green-500 text-white'
                    : 'bg-white/80 text-gray-600 hover:bg-green-100'
                }`}
              >
                {card.owned ? 'Owned' : 'Want'}
              </button>
            </div>

            <div className="p-2">
              <h4 className="font-medium text-sm truncate" title={card.name}>
                {card.name}
              </h4>
              <div className="flex justify-between items-center">
                <span className="text-xs text-gray-400 uppercase">{card.set}</span>
                {card.prices?.usd && (
                  <span className="text-xs text-green-600 font-medium">${card.prices.usd}</span>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>

      {filteredCards.length === 0 && (
        <div className="text-center py-12 text-gray-500">
          No cards found matching your criteria
        </div>
      )}

      <CardDetailModal card={selectedCard} onClose={() => setSelectedCard(null)} />
    </div>
  )
}
