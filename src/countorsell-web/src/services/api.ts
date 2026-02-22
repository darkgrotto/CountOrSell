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

export const GRADING_COMPANIES = ['PSA', 'BGS', 'CGC', 'SGC', 'GAI', 'CSG'] as const

export const CARD_VARIANTS = [
  'Regular', 'Foil', 'Etched Foil', 'Galaxy Foil', 'Gilded Foil',
  'Surge Foil', 'Fracture Foil', 'Textured Foil', 'Serialized',
] as const

export interface SlabbedCard {
  id: number
  scryfallCardId: string
  cardName: string
  setCode: string
  setName: string
  collectorNumber: string
  cardVariant: string
  gradingCompany: string
  grade: string
  certificationNumber: string
  purchaseDate: string | null
  purchasedFrom: string | null
  purchaseCost: number | null
  notes: string | null
  createdAt: string
}

export interface SlabbedCardRequest {
  scryfallCardId: string
  cardName: string
  setCode: string
  setName: string
  collectorNumber: string
  cardVariant: string
  gradingCompany: string
  grade: string
  certificationNumber: string
  purchaseDate: string | null
  purchasedFrom: string | null
  purchaseCost: number | null
  notes: string | null
}

export interface CardSearchResult {
  id: string
  name: string
  setCode: string
  setName: string
  collectorNumber: string
}

export interface AdminUserInfo {
  id: string
  username: string
  displayName?: string
  isAdmin: boolean
  isDisabled: boolean
  createdAt: string
  lastLoginAt: string | null
}

export function getCertVerificationUrl(company: string, certNumber: string): string | null {
  switch (company.toUpperCase()) {
    case 'PSA': return `https://www.psacard.com/cert/${certNumber}`
    case 'BGS': return `https://www.beckett.com/grading/cert/${certNumber}`
    case 'CGC': return `https://www.cgccards.com/certlookup/${certNumber}/`
    case 'SGC': return `https://www.sgccard.com/cert/${certNumber}`
    default:    return null
  }
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

  // --- Slabbed Cards (authorized) ---
  async fetchSlabbedCards(): Promise<SlabbedCard[]> {
    const response = await fetch(`${API_BASE}/slabbed`, { headers: getAuthHeaders() })
    return handleResponse<SlabbedCard[]>(response)
  },

  async addSlabbedCard(req: SlabbedCardRequest): Promise<SlabbedCard> {
    const response = await fetch(`${API_BASE}/slabbed`, {
      method: 'POST',
      headers: authHeaders(),
      body: JSON.stringify(req),
    })
    return handleResponse<SlabbedCard>(response)
  },

  async updateSlabbedCard(id: number, req: SlabbedCardRequest): Promise<SlabbedCard> {
    const response = await fetch(`${API_BASE}/slabbed/${id}`, {
      method: 'PUT',
      headers: authHeaders(),
      body: JSON.stringify(req),
    })
    return handleResponse<SlabbedCard>(response)
  },

  async deleteSlabbedCard(id: number): Promise<void> {
    const response = await fetch(`${API_BASE}/slabbed/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    })
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP error ${response.status}`)
    }
  },

  async searchCards(q: string): Promise<CardSearchResult[]> {
    const response = await fetch(`${API_BASE}/cards/search?q=${encodeURIComponent(q)}&limit=20`, {
      headers: getAuthHeaders(),
    })
    return handleResponse<CardSearchResult[]>(response)
  },

  async downloadSlabbedCardsPdf(): Promise<void> {
    const response = await fetch(`${API_BASE}/export/slabbed/pdf`, { headers: getAuthHeaders() })
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP error ${response.status}`)
    }
    const blob = await response.blob()
    this.downloadBlob(blob, 'slabbed-collection.pdf')
  },

  // --- Auth Profile (authorized) ---
  async updateDisplayName(displayName: string): Promise<{ id: string; username: string; displayName?: string }> {
    const res = await fetch(`${API_BASE}/auth/profile`, {
      method: 'PUT',
      headers: authHeaders(),
      body: JSON.stringify({ displayName }),
    })
    return handleResponse(res)
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    const res = await fetch(`${API_BASE}/auth/password`, {
      method: 'PUT',
      headers: authHeaders(),
      body: JSON.stringify({ currentPassword, newPassword }),
    })
    if (!res.ok) {
      const body = await res.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(body.error || `HTTP error ${res.status}`)
    }
  },

  // --- App settings (public read, admin write) ---
  async getRegistrationStatus(): Promise<{ registrationsEnabled: boolean }> {
    const res = await fetch(`${API_BASE}/auth/registration-status`)
    return handleResponse(res)
  },

  async adminUpdateSettings(registrationsEnabled: boolean): Promise<{ registrationsEnabled: boolean }> {
    const res = await fetch(`${API_BASE}/admin/settings`, {
      method: 'PUT',
      headers: authHeaders(),
      body: JSON.stringify({ registrationsEnabled }),
    })
    return handleResponse(res)
  },

  // --- Admin (admin only) ---
  async listUsers(): Promise<AdminUserInfo[]> {
    const res = await fetch(`${API_BASE}/admin/users`, { headers: getAuthHeaders() })
    return handleResponse<AdminUserInfo[]>(res)
  },

  async adminUpdateUser(id: string, update: { displayName?: string; isAdmin?: boolean; isDisabled?: boolean }): Promise<AdminUserInfo> {
    const res = await fetch(`${API_BASE}/admin/users/${id}`, {
      method: 'PUT',
      headers: authHeaders(),
      body: JSON.stringify(update),
    })
    return handleResponse<AdminUserInfo>(res)
  },

  async adminDeleteUser(id: string): Promise<void> {
    const res = await fetch(`${API_BASE}/admin/users/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    })
    if (!res.ok) {
      const body = await res.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(body.error || `HTTP error ${res.status}`)
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
