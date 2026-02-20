// =============================================================================
// SetDetail.tsx - Set Detail Page Component
// =============================================================================
// This component displays the detail view for a specific MTG set, including:
// - Set information header (name, icon, card count, release date)
// - Label generator for creating storage box labels
// - Card grid showing all cards in the set
//
// The set code is extracted from the URL using React Router's useParams hook.
// Data is fetched using React Query with caching for performance.
// =============================================================================

import { useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, MtgCard } from '../services/api'
import { useAuth } from '../contexts/AuthContext'
import CardGrid from './CardGrid'
import LabelPreview from './LabelPreview'
import BoosterPanel from './BoosterPanel'

/**
 * SetDetail Component
 *
 * Displays detailed information about a specific MTG set.
 * This is the main page users see when they click on a set from the SetList.
 *
 * Features:
 * - Set header with icon, name, and metadata
 * - Label generator to create printable box labels
 * - Card grid with search and filter for all cards in the set
 *
 * URL: /sets/:setCode (e.g., /sets/DOM for Dominaria)
 *
 * @returns The set detail page UI
 */
export default function SetDetail() {
  // ===========================================================================
  // URL Parameters
  // ===========================================================================

  /**
   * Extract the setCode from the URL path.
   *
   * useParams() returns an object with URL parameters defined in the route.
   * The route "/sets/:setCode" means :setCode is a dynamic segment.
   *
   * Example: URL "/sets/DOM" → setCode = "DOM"
   *
   * TypeScript note: We use <{ setCode: string }> to type the params object.
   */
  const { setCode } = useParams<{ setCode: string }>()
  const { user } = useAuth()

  const { data: cards, isLoading, error } = useQuery({
    queryKey: ['cards', setCode],
    queryFn: () => api.getCards(setCode!),
    enabled: !!setCode,
  })

  const { data: reserveListIds = [] } = useQuery({
    queryKey: ['reservelist-ids', setCode],
    queryFn: () => api.getReserveListIdsForSet(setCode!),
    enabled: !!setCode && !!user,
  })

  const { data: ownedCardIds = [] } = useQuery({
    queryKey: ['owned-cards', setCode],
    queryFn: () => api.getOwnedCardsForSet(setCode!),
    enabled: !!setCode && !!user,
  })

  const queryClient = useQueryClient()

  const toggleOwnedMutation = useMutation({
    mutationFn: (card: MtgCard) => {
      const isOwned = ownedCardIds.includes(card.id)
      return api.setCardOwned(card.id, !isOwned, card.name, card.set, card.collector_number)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['owned-cards', setCode] })
    },
  })

  // ===========================================================================
  // Loading State
  // ===========================================================================

  // Show loading spinner while data is being fetched
  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        {/* Animated spinner */}
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
      <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
        Error loading cards: {error.message}
      </div>
    )
  }

  // ===========================================================================
  // Null Check
  // ===========================================================================

  // Return nothing if data hasn't loaded yet
  // (shouldn't happen with loading state above, but TypeScript requires it)
  if (!cards) return null

  // ===========================================================================
  // Main Render
  // ===========================================================================

  return (
    <div>
      {/* =====================================================================
          Back Navigation and Set Header
          ===================================================================== */}
      <div className="mb-6">

        {/* Back to Sets Link */}
        {/* Uses ← HTML entity for left arrow */}
        <Link to="/" className="text-blue-600 hover:text-blue-800 mb-4 inline-block">
          &larr; Back to Sets
        </Link>

        {/* Set Information Card */}
        {/* White card with shadow containing set details */}
        <div className="bg-white rounded-lg shadow-md p-6 mb-6">
          <div className="flex items-start gap-6">

            {/* Set Details */}
            <div className="flex-1">
              {/* Set Name - Large heading */}
              <h1 className="text-3xl font-bold mb-2">{setCode?.toUpperCase()}</h1>

              {/* Set Metadata - Horizontal list of info */}
              <div className="text-gray-600 flex flex-wrap gap-4">
                {/* Set Code Badge */}
                <span className="font-mono text-lg uppercase bg-gray-100 px-2 py-1 rounded">
                  {setCode}
                </span>

                {/* Ownership Stats */}
                <span className="font-medium text-green-600">
                  {ownedCardIds.length}/{cards.length} owned
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* ===================================================================
            Action Panels (Label Generator and Booster Tracking)
            =================================================================== */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
          <LabelPreview setCode={setCode!} />
          <BoosterPanel setCode={setCode!} />
        </div>
      </div>

      {/* =====================================================================
          Card Grid
          ===================================================================== */}
      <CardGrid
        cards={cards}
        reservedCardIds={reserveListIds}
        ownedCardIds={ownedCardIds}
        onToggleOwned={user ? (card) => toggleOwnedMutation.mutate(card) : undefined}
      />
    </div>
  )
}
