import { getAuthHeaders } from './auth'

// =============================================================================
// Type Definitions
// =============================================================================

export interface MtgSet {
  id: string
  code: string
  name: string
  released_at: string | null
  set_type: string
  card_count: number
  icon_svg_uri: string | null
  scryfall_uri: string | null
}

export interface MtgCard {
  id: string
  name: string
  set: string
  set_name: string
  collector_number: string
  rarity: string
  type_line: string | null
  mana_cost: string | null
  oracle_text: string | null
  image_uris: {
    small?: string
    normal?: string
    large?: string
    png?: string
    art_crop?: string
  } | null
  card_faces?: {
    name: string
    image_uris?: {
      small?: string
      normal?: string
    }
  }[] | null
  prices: {
    usd: string | null
    usd_foil: string | null
  } | null
  color_identity?: string[]
  reserved?: boolean
  scryfall_uri: string | null
}

export interface LabelRequest {
  setCode: string
  boxType: 'Set Box' | 'Surplus Box'
}

export const BOOSTER_TYPES = [
  'Collector Booster',
  'Play Booster',
  'Jumpstart Booster',
  'Draft Booster',
  'Set Booster',
  'Sample Booster',
] as const

export interface BoosterDefinition {
  id: number
  setCode: string
  boosterType: string
  artVariant: string
  imageUrl: string | null
  owned: boolean
}

export interface BoosterRequest {
  setCode: string
  boosterType: string
  artVariant: string
  imageUrl?: string
}

export interface ReserveListCard extends MtgCard {
  owned: boolean
}

// =============================================================================
// API Configuration
// =============================================================================

const API_BASE = '/api'

// =============================================================================
// Helper Functions
// =============================================================================

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: 'Unknown error' }))
    throw new Error(error.error || `HTTP error ${response.status}`)
  }
  return response.json()
}

function authHeaders(): Record<string, string> {
  return {
    'Content-Type': 'application/json',
    ...getAuthHeaders(),
  }
}

// =============================================================================
// API Client
// =============================================================================

export const api = {
  // --- Sets (anonymous) ---
  async getSets(): Promise<MtgSet[]> {
    const response = await fetch(`${API_BASE}/sets`)
    return handleResponse<MtgSet[]>(response)
  },

  async getSet(setCode: string): Promise<MtgSet> {
    const response = await fetch(`${API_BASE}/sets/${setCode}`)
    return handleResponse<MtgSet>(response)
  },

  async getCards(setCode: string): Promise<MtgCard[]> {
    const response = await fetch(`${API_BASE}/sets/${setCode}/cards`)
    return handleResponse<MtgCard[]>(response)
  },

  // --- Labels (anonymous) ---
  async generateLabel(request: LabelRequest): Promise<Blob> {
    const response = await fetch(`${API_BASE}/labels/generate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP error ${response.status}`)
    }
    return response.blob()
  },

  // --- Boosters (authorized) ---
  async getAllBoosters(): Promise<BoosterDefinition[]> {
    const response = await fetch(`${API_BASE}/boosters`, { headers: getAuthHeaders() })
    return handleResponse<BoosterDefinition[]>(response)
  },

  async getBoostersForSet(setCode: string): Promise<BoosterDefinition[]> {
    const response = await fetch(`${API_BASE}/boosters/set/${setCode}`, { headers: getAuthHeaders() })
    return handleResponse<BoosterDefinition[]>(response)
  },

  async createBooster(request: BoosterRequest): Promise<BoosterDefinition> {
    const response = await fetch(`${API_BASE}/boosters`, {
      method: 'POST',
      headers: authHeaders(),
      body: JSON.stringify(request),
    })
    return handleResponse<BoosterDefinition>(response)
  },

  async setBoosterOwned(id: number, owned: boolean): Promise<BoosterDefinition> {
    const response = await fetch(`${API_BASE}/boosters/${id}/owned`, {
      method: 'PATCH',
      headers: authHeaders(),
      body: JSON.stringify({ owned }),
    })
    return handleResponse<BoosterDefinition>(response)
  },

  async deleteBooster(id: number): Promise<void> {
    const response = await fetch(`${API_BASE}/boosters/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    })
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP error ${response.status}`)
    }
  },

  // --- Reserve List (authorized) ---
  async getReserveList(): Promise<ReserveListCard[]> {
    const response = await fetch(`${API_BASE}/reservelist`, { headers: getAuthHeaders() })
    return handleResponse<ReserveListCard[]>(response)
  },

  async setReserveListOwned(scryfallCardId: string, owned: boolean, cardName: string, setCode: string): Promise<void> {
    const response = await fetch(`${API_BASE}/reservelist/${scryfallCardId}/owned`, {
      method: 'PATCH',
      headers: authHeaders(),
      body: JSON.stringify({ owned, cardName, setCode }),
    })
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP error ${response.status}`)
    }
  },

  async getReserveListIdsForSet(setCode: string): Promise<string[]> {
    const response = await fetch(`${API_BASE}/reservelist/set/${setCode}`, { headers: getAuthHeaders() })
    return handleResponse<string[]>(response)
  },

  // --- Card Ownership (authorized) ---
  async getOwnedCardsForSet(setCode: string): Promise<string[]> {
    const response = await fetch(`${API_BASE}/sets/${setCode}/owned-cards`, { headers: getAuthHeaders() })
    return handleResponse<string[]>(response)
  },

  async setCardOwned(scryfallCardId: string, owned: boolean, cardName: string, setCode: string, collectorNumber: string): Promise<void> {
    const response = await fetch(`${API_BASE}/cards/${scryfallCardId}/owned`, {
      method: 'PATCH',
      headers: authHeaders(),
      body: JSON.stringify({ owned, cardName, setCode, collectorNumber }),
    })
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP error ${response.status}`)
    }
  },

  async bulkSetCardsOwned(setCode: string, cards: { scryfallCardId: string; cardName: string; collectorNumber: string }[], owned: boolean): Promise<void> {
    const response = await fetch(`${API_BASE}/cards/bulk-owned`, {
      method: 'POST',
      headers: authHeaders(),
      body: JSON.stringify({ setCode, scryfallCardIds: cards, owned }),
    })
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP error ${response.status}`)
    }
  },

  // --- Utility ---
  downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  },
}
