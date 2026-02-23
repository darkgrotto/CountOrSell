// =============================================================================
// SetList.tsx - MTG Set Browser Component
// =============================================================================
// This component displays a searchable, filterable list of all available
// Magic: The Gathering sets. Users can:
// - Search sets by name or code
// - Filter by set type (expansion, core, masters, etc.)
// - Click on a set to view its cards
//
// The component uses React Query for data fetching, which provides:
// - Automatic caching (data persists when navigating away and back)
// - Loading and error state management
// - Background refetching to keep data fresh
// =============================================================================

import { useState, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api, MtgSet, MtgCard, CardSearchResult, CardOwnershipEntry } from '../services/api'
import { useAuth } from '../contexts/AuthContext'
import CardDetailModal from './CardDetailModal'

/**
 * SetList Component
 *
 * Displays a grid of MTG sets with search and filter functionality.
 * Each set card shows:
 * - Set icon (from Scryfall)
 * - Set name
 * - Set code (e.g., "DOM")
 * - Card count
 * - Release date
 *
 * Clicking a set navigates to the SetDetail view for that set.
 *
 * @returns The set list UI with search/filter controls
 */
export default function SetList() {
  // ===========================================================================
  // State Management
  // ===========================================================================

  const { user } = useAuth()

  // Mode toggle: authenticated users can switch between browsing sets and searching cards
  type SearchMode = 'sets' | 'cards'
  const [mode, setMode] = useState<SearchMode>('sets')

  /**
   * Search term state for filtering sets by name or code.
   * Updated as the user types in the search input.
   * Empty string means no search filter is applied.
   */
  const [searchTerm, setSearchTerm] = useState('')

  /**
   * Set type filter state for filtering by set category.
   * 'all' means show all types, otherwise matches the set_type field.
   * Examples: 'expansion', 'core', 'masters', 'commander'
   */
  const [filterType, setFilterType] = useState<string>('all')

  type SortOption = 'date-desc' | 'date-asc' | 'count-desc' | 'count-asc' | 'code-asc' | 'code-desc'
  const [sortBy, setSortBy] = useState<SortOption>('date-desc')

  // Card search state (mirrors the slab form's card lookup mechanism)
  const [cardQuery, setCardQuery] = useState('')
  const [cardResults, setCardResults] = useState<CardSearchResult[]>([])
  const [cardSearching, setCardSearching] = useState(false)
  const cardTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Selected card from search results — drives the detail modal
  const [selectedResult, setSelectedResult] = useState<CardSearchResult | null>(null)

  const queryClient = useQueryClient()

  // Fetch all cards for the selected result's set (React Query caches this, so
  // navigating back to a previously viewed set is instant)
  const { data: selectedSetCards } = useQuery({
    queryKey: ['cards', selectedResult?.setCode],
    queryFn: () => api.getCards(selectedResult!.setCode),
    enabled: !!selectedResult,
  })

  // Fetch owned card quantities for the selected result's set
  const { data: selectedSetOwned = [] } = useQuery({
    queryKey: ['owned-cards', selectedResult?.setCode],
    queryFn: () => api.getOwnedCardsForSet(selectedResult!.setCode),
    enabled: !!selectedResult && !!user,
  })

  const selectedSetOwnedMap: Record<string, { variant: string; quantity: number }[]> = {}
  for (const e of selectedSetOwned as CardOwnershipEntry[]) {
    if (!selectedSetOwnedMap[e.scryfallCardId]) selectedSetOwnedMap[e.scryfallCardId] = []
    selectedSetOwnedMap[e.scryfallCardId].push({ variant: e.variant, quantity: e.quantity })
  }

  const saveVariantForResult = useMutation({
    mutationFn: ({ card, variant, quantity }: { card: MtgCard; variant: string; quantity: number }) =>
      api.setCardVariantQuantity(card.id, variant, quantity, card.name, card.set, card.collector_number),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['owned-cards', selectedResult?.setCode] })
    },
  })

  // Resolve the full MtgCard object once selectedSetCards has loaded
  const selectedFullCard = selectedSetCards?.find(c => c.id === selectedResult?.id) ?? null

  // ===========================================================================
  // Data Fetching with React Query
  // ===========================================================================

  /**
   * Fetch all sets using React Query.
   *
   * useQuery returns an object with:
   * - data: The fetched sets (undefined while loading)
   * - isLoading: True during initial fetch
   * - error: Error object if fetch failed
   *
   * The queryKey ['sets'] is used for caching - React Query will
   * return cached data if available and refetch in background.
   */
  const { data: sets, isLoading, error } = useQuery({
    // Unique key for this query - used for caching and refetching
    queryKey: ['sets'],

    // Function that fetches the data
    // Returns a Promise that resolves to MtgSet[]
    queryFn: api.getSets,
  })

  // ===========================================================================
  // Card Search Logic
  // ===========================================================================

  /**
   * Debounced card search handler. Mirrors the slab form's card lookup:
   * waits 300 ms after the last keystroke before firing the API call.
   */
  function handleCardQuery(value: string) {
    setCardQuery(value)
    if (cardTimer.current) clearTimeout(cardTimer.current)
    if (value.length < 2) {
      setCardResults([])
      return
    }
    cardTimer.current = setTimeout(async () => {
      setCardSearching(true)
      try {
        setCardResults(await api.searchCards(value))
      } catch {
        setCardResults([])
      } finally {
        setCardSearching(false)
      }
    }, 300)
  }

  // ===========================================================================
  // Filtering / Sorting Logic
  // ===========================================================================

  /**
   * Filter the sets based on search term and type filter.
   *
   * Uses optional chaining (?.) because sets may be undefined during loading.
   * The filter checks both:
   * - Name/code matches the search term (case-insensitive)
   * - Set type matches the filter (or filter is 'all')
   */
  const filteredSets = sets?.filter((set: MtgSet) => {
    // Check if search term matches set name or code
    // toLowerCase() makes the search case-insensitive
    const matchesSearch = set.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      set.code.toLowerCase().includes(searchTerm.toLowerCase())

    // Check if set type matches the filter
    // 'all' matches everything, otherwise exact match required
    const matchesType = filterType === 'all' || set.set_type === filterType

    // Only include sets that match both criteria
    return matchesSearch && matchesType
  })

  /**
   * Extract unique set types for the filter dropdown.
   *
   * Uses Set to get unique values, then spread into array.
   * Only runs when sets is defined (nullish coalescing to empty array).
   */
  const setTypes = [...new Set(sets?.map((s: MtgSet) => s.set_type) || [])]

  const sortedSets = [...(filteredSets ?? [])].sort((a, b) => {
    switch (sortBy) {
      case 'date-asc':
      case 'date-desc': {
        const da = a.released_at ? new Date(a.released_at).getTime() : 0
        const db = b.released_at ? new Date(b.released_at).getTime() : 0
        return sortBy === 'date-asc' ? da - db : db - da
      }
      case 'count-desc': return b.card_count - a.card_count
      case 'count-asc':  return a.card_count - b.card_count
      case 'code-asc':   return a.code.localeCompare(b.code)
      case 'code-desc':  return b.code.localeCompare(a.code)
      default: return 0
    }
  })

  // ===========================================================================
  // Loading State
  // ===========================================================================

  // Show spinner while data is being fetched
  if (isLoading) {
    return (
      // Centered loading spinner using Tailwind CSS
      <div className="flex justify-center items-center py-12">
        {/* Animated spinning circle */}
        {/* animate-spin: CSS animation for rotation */}
        {/* border-b-2: Only bottom border is visible, creating spinner effect */}
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    )
  }

  // ===========================================================================
  // Error State
  // ===========================================================================

  // Show error message if fetch failed
  if (error) {
    return (
      // Red-themed error alert box
      <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
        Error loading sets: {error.message}
      </div>
    )
  }

  // ===========================================================================
  // Main Render
  // ===========================================================================

  return (
    <div>
      {/* =====================================================================
          Mode Toggle (Sets / Cards) — visible to authenticated users only
          ===================================================================== */}
      {user && (
        <div className="mb-6 flex w-fit rounded-lg border border-gray-300 overflow-hidden">
          <button
            onClick={() => setMode('sets')}
            className={`px-6 py-2 text-sm font-medium transition-colors ${
              mode === 'sets' ? 'bg-blue-600 text-white' : 'bg-white text-gray-700 hover:bg-gray-50'
            }`}
          >
            Sets
          </button>
          <button
            onClick={() => setMode('cards')}
            className={`px-6 py-2 text-sm font-medium transition-colors border-l border-gray-300 ${
              mode === 'cards' ? 'bg-blue-600 text-white' : 'bg-white text-gray-700 hover:bg-gray-50'
            }`}
          >
            Cards
          </button>
        </div>
      )}

      {/* =====================================================================
          Sets Mode
          ===================================================================== */}
      {mode === 'sets' && (<>
        {/* Search and Filter Controls */}
        {/* Flex container that stacks vertically on mobile, horizontal on larger screens */}
        <div className="mb-6 flex flex-col sm:flex-row gap-4">

          {/* Search Input */}
          {/* flex-1 makes it take remaining space */}
          <input
            type="text"
            placeholder="Search sets..."
            value={searchTerm}
            // Update state on every keystroke for real-time filtering
            onChange={(e) => setSearchTerm(e.target.value)}
            className="flex-1 px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />

          {/* Type Filter Dropdown */}
          <select
            value={filterType}
            onChange={(e) => setFilterType(e.target.value)}
            className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
          >
            {/* Default option to show all types */}
            <option value="all">All Types</option>

            {/* Dynamically generated options from available set types */}
            {setTypes.map((type) => (
              <option key={type} value={type}>
                {/* Replace underscores with spaces for display */}
                {/* e.g., "draft_innovation" becomes "draft innovation" */}
                {type.replace('_', ' ')}
              </option>
            ))}
          </select>

          {/* Sort Dropdown */}
          <select
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value as SortOption)}
            className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
          >
            <option value="date-desc">Release date (newest first)</option>
            <option value="date-asc">Release date (oldest first)</option>
            <option value="count-desc">Card count (highest first)</option>
            <option value="count-asc">Card count (lowest first)</option>
            <option value="code-asc">Set code (A → Z)</option>
            <option value="code-desc">Set code (Z → A)</option>
          </select>
        </div>

        {/* Set Grid */}
        {/* Responsive grid: 1 column on mobile, 2 on medium, 3 on large screens */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">

          {/* Map through sorted/filtered sets to create set cards */}
          {sortedSets.map((set: MtgSet) => (
            // Each set is a Link that navigates to the set detail page
            <Link
              key={set.id}  // Unique key for React's reconciliation
              to={`/sets/${set.code}`}  // URL with set code parameter
              // Card styling with hover effect
              className="bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow p-4 flex items-center gap-4"
            >
              {/* Set Icon */}
              {/* Conditionally rendered if icon URL exists */}
              {set.icon_svg_uri && (
                <img
                  src={set.icon_svg_uri}
                  alt={set.name}
                  className="w-12 h-12 object-contain"
                />
              )}

              {/* Set Information */}
              {/* min-w-0 prevents flex item from overflowing */}
              <div className="flex-1 min-w-0">
                {/* Set Name - truncate if too long */}
                <h3 className="font-semibold text-lg truncate">{set.name}</h3>

                {/* Set Code and Card Count */}
                <div className="text-sm text-gray-600 flex items-center gap-2">
                  {/* Set code in monospace font, uppercase */}
                  <span className="font-mono uppercase">{set.code}</span>
                  <span>•</span>
                  <span>{set.card_count} cards</span>
                </div>

                {/* Release Date (if available) */}
                {set.released_at && (
                  <div className="text-xs text-gray-500">
                    {/* Format date for display using locale-aware formatting */}
                    Released: {new Date(set.released_at).toLocaleDateString()}
                  </div>
                )}
              </div>
            </Link>
          ))}
        </div>

        {/* Empty State */}
        {/* Show message when no sets match the filter criteria */}
        {sortedSets.length === 0 && (
          <div className="text-center py-12 text-gray-500">
            No sets found matching your criteria
          </div>
        )}
      </>)}

      {/* =====================================================================
          Cards Mode
          ===================================================================== */}
      {mode === 'cards' && (
        <div>
          {/* Search input — same debounced mechanism as the slab card lookup */}
          <input
            type="text"
            value={cardQuery}
            onChange={e => handleCardQuery(e.target.value)}
            placeholder="Search cards by name..."
            autoComplete="off"
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent mb-6"
          />

          {/* Loading spinner */}
          {cardSearching && (
            <div className="flex justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
            </div>
          )}

          {/* Results grid */}
          {!cardSearching && cardResults.length > 0 && (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {cardResults.map(r => (
                <button
                  key={`${r.id}-${r.setCode}`}
                  onClick={() => setSelectedResult(r)}
                  className="bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow p-4 text-left w-full"
                >
                  <h3 className="font-semibold text-lg">{r.name}</h3>
                  <div className="text-sm text-gray-600 flex items-center gap-2 mt-1">
                    <span className="font-mono uppercase">{r.setCode}</span>
                    <span>·</span>
                    <span>#{r.collectorNumber}</span>
                  </div>
                  <div className="text-xs text-gray-500 mt-1">{r.setName}</div>
                </button>
              ))}
            </div>
          )}

          {/* No results */}
          {!cardSearching && cardQuery.length >= 2 && cardResults.length === 0 && (
            <div className="text-center py-12 text-gray-500">
              No cards found matching "{cardQuery}"
            </div>
          )}

          {/* Prompt */}
          {!cardSearching && cardQuery.length < 2 && (
            <div className="text-center py-12 text-gray-400">
              Type at least 2 characters to search for a card
            </div>
          )}
        </div>
      )}

      {/* Card detail modal for search results */}
      <CardDetailModal
        key={selectedFullCard?.id ?? 'none'}
        card={selectedFullCard}
        onClose={() => setSelectedResult(null)}
        cardOwnership={selectedFullCard ? (selectedSetOwnedMap[selectedFullCard.id] ?? []) : []}
        onSaveVariant={user && selectedFullCard
          ? (variant, qty) => saveVariantForResult.mutate({ card: selectedFullCard, variant, quantity: qty })
          : undefined}
      />
    </div>
  )
}
