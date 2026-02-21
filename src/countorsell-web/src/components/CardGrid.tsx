// =============================================================================
// CardGrid.tsx - Card Display Grid Component
// =============================================================================
// This component displays a grid of MTG cards with search and filter
// functionality. It receives an array of cards as a prop and renders them
// in a responsive grid layout.
//
// Features:
// - Search cards by name or collector number
// - Filter by rarity (common, uncommon, rare, mythic)
// - Card images with lazy loading
// - Price display for each card
// - Rarity-colored badges
// =============================================================================

import { useState, useMemo } from 'react'
import { MtgCard } from '../services/api'
import ReserveListBadge from './ReserveListBadge'
import CardDetailModal from './CardDetailModal'

type SortField = 'number' | 'name' | 'rarity' | 'price' | 'owned' | 'needed'

// =============================================================================
// Component Props Interface
// =============================================================================

/**
 * Props for the CardGrid component.
 * The parent component (SetDetail) passes the cards array.
 */
interface CardGridProps {
  cards: MtgCard[]
  reservedCardIds?: string[]
  ownedCardIds?: string[]
  onToggleOwned?: (card: MtgCard) => void
}

// =============================================================================
// CardGrid Component
// =============================================================================

/**
 * CardGrid Component
 *
 * Displays a responsive grid of MTG cards with search and filter controls.
 * Each card shows:
 * - Card image (from Scryfall)
 * - Collector number badge
 * - Rarity indicator
 * - Card name
 * - Price (if available)
 *
 * @param cards - Array of MtgCard objects to display
 * @returns The card grid UI with search/filter controls
 */
export default function CardGrid({ cards, reservedCardIds = [], ownedCardIds = [], onToggleOwned }: CardGridProps) {
  const reservedSet = new Set(reservedCardIds)
  const ownedSet = new Set(ownedCardIds)
  // ===========================================================================
  // State Management
  // ===========================================================================

  /**
   * Search term state for filtering cards.
   * Filters by card name or collector number.
   */
  const [selectedCard, setSelectedCard] = useState<MtgCard | null>(null)
  const [searchTerm, setSearchTerm] = useState('')

  const [filterRarity, setFilterRarity] = useState<string>('all')
  const [filterType, setFilterType] = useState<string>('all')
  const [filterColor, setFilterColor] = useState<string>('all')
  const [sortBy, setSortBy] = useState<SortField>('number')

  // ===========================================================================
  // Filtering Logic
  // ===========================================================================

  /**
   * Filter cards based on search term and rarity filter.
   * Both filters must match for a card to be included.
   */
  const RARITY_ORDER: Record<string, number> = { mythic: 0, rare: 1, uncommon: 2, common: 3 }

  const filteredCards = useMemo(() => {
    const filtered = cards.filter((card) => {
      const matchesSearch = card.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        card.collector_number.includes(searchTerm)

      const matchesRarity =
        filterRarity === 'all' ? true :
        filterRarity === 'reserved' ? reservedSet.has(card.id) :
        filterRarity === 'owned' ? ownedSet.has(card.id) :
        filterRarity === 'unowned' ? !ownedSet.has(card.id) :
        card.rarity === filterRarity

      const matchesType = filterType === 'all' ? true : (() => {
        const typeLine = card.type_line?.toLowerCase() ?? ''
        if (filterType === 'legendary') return typeLine.includes('legendary')
        const primaryType = typeLine.split('—')[0].trim()
        return primaryType.includes(filterType.toLowerCase())
      })()

      const matchesColor = filterColor === 'all' ? true : (() => {
        const ci = card.color_identity ?? []
        if (filterColor === 'colorless') return ci.length === 0
        if (filterColor === 'multicolor') return ci.length >= 2
        return ci.includes(filterColor)
      })()

      return matchesSearch && matchesRarity && matchesType && matchesColor
    })

    return filtered.sort((a, b) => {
      switch (sortBy) {
        case 'name':
          return a.name.localeCompare(b.name)
        case 'rarity': {
          const ra = RARITY_ORDER[a.rarity] ?? 99
          const rb = RARITY_ORDER[b.rarity] ?? 99
          return ra - rb || a.name.localeCompare(b.name)
        }
        case 'price': {
          const pa = parseFloat(a.prices?.usd || '0')
          const pb = parseFloat(b.prices?.usd || '0')
          return pb - pa
        }
        case 'owned': {
          const ao = ownedSet.has(a.id) ? 0 : 1
          const bo = ownedSet.has(b.id) ? 0 : 1
          return ao - bo || a.name.localeCompare(b.name)
        }
        case 'needed': {
          const an = ownedSet.has(a.id) ? 1 : 0
          const bn = ownedSet.has(b.id) ? 1 : 0
          return an - bn || a.name.localeCompare(b.name)
        }
        default: // 'number'
          return a.collector_number.localeCompare(b.collector_number, undefined, { numeric: true })
      }
    })
  }, [cards, searchTerm, filterRarity, filterType, filterColor, sortBy, reservedSet, ownedSet])

  /**
   * Extract unique rarities from the cards for the filter dropdown.
   * Uses Set to deduplicate, then spread to array.
   */
  const rarities = [...new Set(cards.map((c) => c.rarity))]

  // ===========================================================================
  // Helper Functions
  // ===========================================================================

  /**
   * Gets the appropriate image URL for a card.
   *
   * Normal cards have image_uris directly on the card object.
   * Double-faced cards have images on each face in card_faces array.
   *
   * @param card - The card to get an image for
   * @returns The normal-sized image URL, or undefined if not available
   */
  const getCardImage = (card: MtgCard): string | undefined => {
    // Use local image proxy first, with Scryfall as fallback
    const proxyUrl = `/api/images/${card.id}`

    // Check if card has any image source (proxy needs the card to have been cached)
    if (card.image_uris?.normal || card.card_faces?.[0]?.image_uris?.normal) return proxyUrl

    return undefined
  }

  const getCardImageFallback = (card: MtgCard): string | undefined => {
    if (card.image_uris?.normal) return card.image_uris.normal
    if (card.card_faces?.[0]?.image_uris?.normal) return card.card_faces[0].image_uris.normal
    return undefined
  }

  /**
   * Gets the Tailwind CSS classes for a rarity badge.
   *
   * Returns background and text color classes that match the
   * traditional MTG rarity colors:
   * - Common: Gray (black expansion symbol)
   * - Uncommon: Silver
   * - Rare: Gold
   * - Mythic: Orange/Red
   *
   * @param rarity - The rarity string from the card
   * @returns Tailwind CSS class string for styling
   */
  const getRarityColor = (rarity: string): string => {
    switch (rarity) {
      case 'common': return 'bg-gray-200 text-gray-700'
      case 'uncommon': return 'bg-gray-400 text-white'
      case 'rare': return 'bg-yellow-500 text-white'
      case 'mythic': return 'bg-orange-600 text-white'
      default: return 'bg-blue-600 text-white'  // Special/bonus rarities
    }
  }

  // ===========================================================================
  // Main Render
  // ===========================================================================

  return (
    <div>
      {/* =====================================================================
          Search and Filter Controls
          ===================================================================== */}
      <div className="mb-6 flex flex-col sm:flex-row gap-4">

        {/* Search Input */}
        <input
          type="text"
          placeholder="Search cards by name or number..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="flex-1 px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
        />

        {/* Rarity Filter Dropdown */}
        <select
          value={filterRarity}
          onChange={(e) => setFilterRarity(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All Rarities</option>
          {rarities.map((rarity) => (
            <option key={rarity} value={rarity}>
              {rarity.charAt(0).toUpperCase() + rarity.slice(1)}
            </option>
          ))}
          {reservedCardIds.length > 0 && (
            <option value="reserved">Reserve List</option>
          )}
          {onToggleOwned && (
            <>
              <option value="owned">Owned</option>
              <option value="unowned">Unowned</option>
            </>
          )}
        </select>

        {/* Card Type Filter */}
        <select
          value={filterType}
          onChange={(e) => setFilterType(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All Types</option>
          <option value="creature">Creature</option>
          <option value="instant">Instant</option>
          <option value="sorcery">Sorcery</option>
          <option value="enchantment">Enchantment</option>
          <option value="artifact">Artifact</option>
          <option value="planeswalker">Planeswalker</option>
          <option value="land">Land</option>
          <option value="battle">Battle</option>
          <option value="legendary">Legendary</option>
        </select>

        {/* Color Identity Filter */}
        <select
          value={filterColor}
          onChange={(e) => setFilterColor(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All Colors</option>
          <option value="W">White</option>
          <option value="U">Blue</option>
          <option value="B">Black</option>
          <option value="R">Red</option>
          <option value="G">Green</option>
          <option value="colorless">Colorless</option>
          <option value="multicolor">Multicolor</option>
        </select>

        {/* Sort Dropdown */}
        <select
          value={sortBy}
          onChange={(e) => setSortBy(e.target.value as SortField)}
          className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
        >
          <option value="number">Sort by Number</option>
          <option value="name">Sort by Name</option>
          <option value="rarity">Sort by Rarity</option>
          <option value="price">Sort by Price</option>
          {onToggleOwned && (
            <>
              <option value="owned">Owned First</option>
              <option value="needed">Needed First</option>
            </>
          )}
        </select>
      </div>

      {/* =====================================================================
          Results Count
          ===================================================================== */}
      {/* Shows how many cards are displayed vs total */}
      <div className="text-sm text-gray-600 mb-4">
        Showing {filteredCards.length} of {cards.length} cards
      </div>

      {/* =====================================================================
          Card Grid
          ===================================================================== */}
      {/* Responsive grid that adjusts columns based on screen size */}
      {/* 2 columns on mobile, up to 6 on extra-large screens */}
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">

        {/* Map through filtered cards to create card elements */}
        {filteredCards.map((card) => (
          <div
            key={card.id}
            // Card container with hover effect
            className="bg-white rounded-lg shadow hover:shadow-lg transition-shadow overflow-hidden group"
          >
            {/* Card Image Container */}
            {/* aspect-[5/7] maintains MTG card proportions */}
            <div className="aspect-[5/7] relative cursor-pointer" onClick={() => setSelectedCard(card)}>

              {/* Card Image or Placeholder */}
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
                // Placeholder for cards without images
                <div className="w-full h-full bg-gray-200 flex items-center justify-center">
                  <span className="text-gray-400 text-xs text-center px-2">{card.name}</span>
                </div>
              )}

              {/* Collector Number Badge (top-left) */}
              {/* Semi-transparent black background for readability */}
              <div className="absolute top-2 left-2 bg-black/70 text-white text-xs px-2 py-1 rounded">
                #{card.collector_number}
              </div>

              <div className={`absolute top-2 right-2 text-xs px-2 py-1 rounded capitalize ${getRarityColor(card.rarity)}`}>
                {card.rarity.charAt(0).toUpperCase()}
              </div>

              {reservedSet.has(card.id) && <ReserveListBadge />}

              {onToggleOwned && (
                <button
                  onClick={(e) => { e.stopPropagation(); onToggleOwned(card); }}
                  className={`absolute bottom-2 right-2 w-7 h-7 rounded-full flex items-center justify-center text-sm font-bold transition-colors ${
                    ownedSet.has(card.id)
                      ? 'bg-green-500 text-white shadow-md'
                      : 'bg-black/40 text-white/60 hover:bg-black/60'
                  }`}
                  title={ownedSet.has(card.id) ? 'Mark as unowned' : 'Mark as owned'}
                >
                  {ownedSet.has(card.id) ? '\u2713' : '\u2713'}
                </button>
              )}
            </div>

            {/* Card Info (below image) */}
            <div className="p-2">
              {/* Card Name */}
              {/* truncate prevents long names from breaking layout */}
              {/* title attribute shows full name on hover */}
              <h4 className="font-medium text-sm truncate" title={card.name}>
                {card.name}
              </h4>

              {/* Price (if available) */}
              {card.prices?.usd && (
                <div className="text-xs text-green-600 font-medium">
                  ${card.prices.usd}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* =====================================================================
          Empty State
          ===================================================================== */}
      {/* Show message when no cards match the filter criteria */}
      {filteredCards.length === 0 && (
        <div className="text-center py-12 text-gray-500">
          No cards found matching your criteria
        </div>
      )}

      <CardDetailModal card={selectedCard} onClose={() => setSelectedCard(null)} />
    </div>
  )
}
