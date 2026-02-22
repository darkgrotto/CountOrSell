import { useEffect } from 'react'
import { MtgCard } from '../services/api'

interface CardDetailModalProps {
  card: MtgCard | null
  onClose: () => void
  isOwned?: boolean
  onToggleOwned?: () => void
}

const COLOR_STYLES: Record<string, string> = {
  W: 'bg-yellow-100 text-yellow-800',
  U: 'bg-blue-200 text-blue-800',
  B: 'bg-gray-700 text-white',
  R: 'bg-red-200 text-red-800',
  G: 'bg-green-200 text-green-800',
}

const COLOR_NAMES: Record<string, string> = {
  W: 'White',
  U: 'Blue',
  B: 'Black',
  R: 'Red',
  G: 'Green',
}

const getRarityBadge = (rarity: string): string => {
  switch (rarity) {
    case 'common': return 'bg-gray-200 text-gray-700'
    case 'uncommon': return 'bg-gray-400 text-white'
    case 'rare': return 'bg-yellow-500 text-white'
    case 'mythic': return 'bg-orange-600 text-white'
    default: return 'bg-blue-600 text-white'
  }
}

export default function CardDetailModal({ card, onClose, isOwned, onToggleOwned }: CardDetailModalProps) {
  useEffect(() => {
    if (!card) return
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKey)
    return () => document.removeEventListener('keydown', handleKey)
  }, [card, onClose])

  if (!card) return null

  const imageUrl = card.image_uris?.large
    ? `/api/images/${card.id}?size=large`
    : card.image_uris?.normal
      ? `/api/images/${card.id}`
      : card.card_faces?.[0]?.image_uris?.normal
        ? `/api/images/${card.id}`
        : undefined

  const imageFallback =
    card.image_uris?.large ??
    card.image_uris?.normal ??
    card.card_faces?.[0]?.image_uris?.normal

  const colorIdentity = card.color_identity ?? []

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-xl shadow-2xl max-w-3xl w-full max-h-[90vh] overflow-y-auto flex flex-col md:flex-row"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Card Image */}
        <div className="md:w-1/2 flex-shrink-0 p-4 flex items-center justify-center bg-gray-50 rounded-t-xl md:rounded-l-xl md:rounded-tr-none">
          {imageUrl ? (
            <img
              src={imageUrl}
              alt={card.name}
              className="max-w-full max-h-[70vh] rounded-lg object-contain"
              onError={(e) => {
                if (imageFallback && e.currentTarget.src !== imageFallback) {
                  e.currentTarget.src = imageFallback
                }
              }}
            />
          ) : (
            <div className="w-64 h-[358px] bg-gray-200 flex items-center justify-center rounded-lg">
              <span className="text-gray-400">{card.name}</span>
            </div>
          )}
        </div>

        {/* Card Details */}
        <div className="md:w-1/2 p-6 space-y-4">
          {/* Header */}
          <div>
            <h2 className="text-xl font-bold">{card.name}</h2>
            {card.mana_cost && (
              <p className="text-sm text-gray-500 mt-1">{card.mana_cost}</p>
            )}
          </div>

          {/* Type Line */}
          {card.type_line && (
            <p className="text-sm font-medium text-gray-700 border-t border-b border-gray-200 py-2">
              {card.type_line}
            </p>
          )}

          {/* Oracle Text */}
          {card.oracle_text && (
            <p className="text-sm text-gray-600 whitespace-pre-line">
              {card.oracle_text}
            </p>
          )}

          {/* Rarity */}
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-500">Rarity:</span>
            <span className={`text-xs px-2 py-0.5 rounded capitalize ${getRarityBadge(card.rarity)}`}>
              {card.rarity}
            </span>
          </div>

          {/* Set + Collector Number */}
          <div className="text-sm text-gray-600">
            {card.set_name} &middot; #{card.collector_number}
          </div>

          {/* Color Identity */}
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-500">Colors:</span>
            {colorIdentity.length > 0 ? (
              colorIdentity.map((c) => (
                <span
                  key={c}
                  className={`text-xs font-bold w-6 h-6 flex items-center justify-center rounded-full ${COLOR_STYLES[c] ?? 'bg-gray-200 text-gray-600'}`}
                  title={COLOR_NAMES[c] ?? c}
                >
                  {c}
                </span>
              ))
            ) : (
              <span className="text-xs text-gray-400">Colorless</span>
            )}
          </div>

          {/* Prices */}
          {card.prices && (card.prices.usd || card.prices.usd_foil) && (
            <div className="flex items-center gap-4 text-sm">
              {card.prices.usd && (
                <span className="text-green-600 font-medium">${card.prices.usd}</span>
              )}
              {card.prices.usd_foil && (
                <span className="text-blue-600 font-medium">${card.prices.usd_foil} foil</span>
              )}
            </div>
          )}

          {/* Scryfall Link */}
          {card.scryfall_uri && (
            <a
              href={card.scryfall_uri}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-block text-sm text-blue-600 hover:underline"
            >
              View on Scryfall
            </a>
          )}

          {/* Add to Collection */}
          {onToggleOwned && (
            <button
              onClick={onToggleOwned}
              className={`mt-4 w-full py-2 rounded-lg text-sm font-medium transition-colors ${
                isOwned
                  ? 'bg-green-100 hover:bg-green-200 text-green-800'
                  : 'bg-blue-600 hover:bg-blue-700 text-white'
              }`}
            >
              {isOwned ? '✓ In Collection' : '+ Add to Collection'}
            </button>
          )}

          {/* Close Button */}
          <button
            onClick={onClose}
            className="mt-2 w-full py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg text-sm transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  )
}
