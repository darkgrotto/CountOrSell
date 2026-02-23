import { useState, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, CollectionCardEntry, CollectionFilter, MtgSet } from '../services/api'

// =============================================================================
// Types
// =============================================================================

type SortKey = 'cardName' | 'setCode' | 'collectorNumber' | 'rarity' | 'typeLine' | 'variant' | 'quantity' | 'priceUsd' | 'lineValue'
type SortDir = 'asc' | 'desc'

// =============================================================================
// Helpers
// =============================================================================

function fmt(value: number | null | undefined): string {
  if (value == null) return '—'
  return `$${value.toFixed(2)}`
}

function primaryType(typeLine: string | null): string {
  if (!typeLine) return 'other'
  const l = typeLine.toLowerCase()
  if (l.includes('creature')) return 'creature'
  if (l.includes('instant')) return 'instant'
  if (l.includes('sorcery')) return 'sorcery'
  if (l.includes('enchantment')) return 'enchantment'
  if (l.includes('artifact')) return 'artifact'
  if (l.includes('planeswalker')) return 'planeswalker'
  if (l.includes('land')) return 'land'
  if (l.includes('battle')) return 'battle'
  return 'other'
}

function sortEntries(entries: CollectionCardEntry[], key: SortKey, dir: SortDir): CollectionCardEntry[] {
  return [...entries].sort((a, b) => {
    let av: string | number | null
    let bv: string | number | null
    switch (key) {
      case 'cardName':         av = a.cardName; bv = b.cardName; break
      case 'setCode':          av = a.setCode; bv = b.setCode; break
      case 'collectorNumber':  av = a.collectorNumber; bv = b.collectorNumber; break
      case 'rarity':           av = a.rarity; bv = b.rarity; break
      case 'typeLine':         av = a.typeLine ?? ''; bv = b.typeLine ?? ''; break
      case 'variant':          av = a.variant; bv = b.variant; break
      case 'quantity':         av = a.quantity; bv = b.quantity; break
      case 'priceUsd':         av = a.priceUsd ?? -1; bv = b.priceUsd ?? -1; break
      case 'lineValue':        av = (a.priceUsd ?? 0) * a.quantity; bv = (b.priceUsd ?? 0) * b.quantity; break
      default:                 av = ''; bv = ''
    }
    const cmp = typeof av === 'number'
      ? av - (bv as number)
      : String(av).localeCompare(String(bv))
    return dir === 'asc' ? cmp : -cmp
  })
}

// =============================================================================
// Sub-components
// =============================================================================

function SortTh({ label, sortKey, current, dir, onSort }: {
  label: string
  sortKey: SortKey
  current: SortKey
  dir: SortDir
  onSort: (k: SortKey) => void
}) {
  const active = current === sortKey
  return (
    <th
      className="px-3 py-2 text-left text-xs font-semibold text-gray-600 uppercase tracking-wide cursor-pointer select-none hover:bg-gray-100"
      onClick={() => onSort(sortKey)}
    >
      {label}
      {active && <span className="ml-1 text-blue-600">{dir === 'asc' ? '▲' : '▼'}</span>}
    </th>
  )
}

// =============================================================================
// Main Component
// =============================================================================

export default function CollectionPage() {
  // ---------------------------------------------------------------------------
  // Filter state
  // ---------------------------------------------------------------------------
  const [search, setSearch]     = useState('')
  const [filterSetCode, setFilterSetCode]   = useState('all')
  const [filterRarity, setFilterRarity]     = useState('all')
  const [filterType, setFilterType]         = useState('all')
  const [filterColor, setFilterColor]       = useState('all')
  const [filterVariant, setFilterVariant]   = useState('all')

  // ---------------------------------------------------------------------------
  // Sort state — default: set → # → variant
  // ---------------------------------------------------------------------------
  const [sortKey, setSortKey] = useState<SortKey>('setCode')
  const [sortDir, setSortDir] = useState<SortDir>('asc')

  function handleSort(key: SortKey) {
    if (key === sortKey) setSortDir(d => d === 'asc' ? 'desc' : 'asc')
    else { setSortKey(key); setSortDir('asc') }
  }

  // ---------------------------------------------------------------------------
  // Build filter object for API call
  // ---------------------------------------------------------------------------
  const apiFilter: CollectionFilter = {
    rarity:  filterRarity  !== 'all' ? filterRarity  : undefined,
    type:    filterType    !== 'all' ? filterType    : undefined,
    color:   filterColor   !== 'all' ? filterColor   : undefined,
    variant: filterVariant !== 'all' ? filterVariant : undefined,
    setCode: filterSetCode !== 'all' ? filterSetCode : undefined,
  }

  // ---------------------------------------------------------------------------
  // Data
  // ---------------------------------------------------------------------------
  const { data: summary } = useQuery({
    queryKey: ['collection-summary'],
    queryFn: () => api.getCollectionSummary(),
  })

  const { data: entries = [], isLoading } = useQuery({
    queryKey: ['collection', apiFilter],
    queryFn: () => api.getCollection(apiFilter),
  })

  const { data: sets = [] } = useQuery({
    queryKey: ['sets'],
    queryFn: () => api.getSets(),
  })

  // ---------------------------------------------------------------------------
  // Client-side search + sort
  // ---------------------------------------------------------------------------
  const filtered = useMemo(() => {
    let rows = entries
    if (search.trim()) {
      const q = search.trim().toLowerCase()
      rows = rows.filter(e => e.cardName.toLowerCase().includes(q))
    }
    return sortEntries(rows, sortKey, sortDir)
  }, [entries, search, sortKey, sortDir])

  const filteredValue = filtered.reduce((sum, e) => sum + (e.priceUsd ?? 0) * e.quantity, 0)
  const filteredQty   = filtered.reduce((sum, e) => sum + e.quantity, 0)
  const filteredUnique = new Set(filtered.map(e => e.scryfallCardId)).size

  // ---------------------------------------------------------------------------
  // Export handlers
  // ---------------------------------------------------------------------------
  function handleExport(fn: () => Promise<void>) {
    fn().catch(err => alert(`Export failed: ${err.message}`))
  }

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------
  return (
    <div>
      <h1 className="text-3xl font-bold mb-6">My Collection</h1>

      {/* ===================================================================
          Stats Panel
          =================================================================== */}
      {summary && (
        <div className="bg-white rounded-lg shadow-md p-6 mb-6">
          <p className="text-lg font-semibold text-gray-700 mb-4">
            {summary.totalCopies.toLocaleString()} copies of{' '}
            {summary.totalUniqueCards.toLocaleString()} unique cards &bull;{' '}
            <span className="text-green-700">${summary.totalValue.toFixed(2)}</span> total value
          </p>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
            {/* By Rarity */}
            <div className="bg-gray-50 rounded p-4">
              <h3 className="text-sm font-bold text-gray-600 uppercase mb-2">By Rarity</h3>
              {Object.entries(summary.byRarity)
                .sort(([a], [b]) => {
                  const order = ['mythic', 'rare', 'uncommon', 'common', 'special', 'bonus']
                  return (order.indexOf(a.toLowerCase()) + 1 || 99) - (order.indexOf(b.toLowerCase()) + 1 || 99)
                })
                .map(([rarity, copies]) => (
                  <div key={rarity} className="flex justify-between text-sm py-0.5">
                    <span className="capitalize text-gray-700">{rarity}</span>
                    <span className="text-gray-500">
                      {copies}
                      {summary.valueByRarity[rarity] ? (
                        <span className="ml-2 text-green-600">(${summary.valueByRarity[rarity].toFixed(2)})</span>
                      ) : null}
                    </span>
                  </div>
                ))}
            </div>

            {/* By Type */}
            <div className="bg-gray-50 rounded p-4">
              <h3 className="text-sm font-bold text-gray-600 uppercase mb-2">By Type</h3>
              {Object.entries(summary.byType)
                .sort(([, a], [, b]) => b - a)
                .map(([type, copies]) => (
                  <div key={type} className="flex justify-between text-sm py-0.5">
                    <span className="capitalize text-gray-700">{type}</span>
                    <span className="text-gray-500">{copies}</span>
                  </div>
                ))}
            </div>

            {/* By Variant */}
            <div className="bg-gray-50 rounded p-4">
              <h3 className="text-sm font-bold text-gray-600 uppercase mb-2">By Variant</h3>
              {Object.entries(summary.byVariant)
                .sort(([, a], [, b]) => b - a)
                .map(([variant, copies]) => (
                  <div key={variant} className="flex justify-between text-sm py-0.5">
                    <span className="text-gray-700">{variant}</span>
                    <span className="text-gray-500">{copies}</span>
                  </div>
                ))}
            </div>
          </div>

          <hr className="border-gray-200 mb-3" />

          <div className="flex flex-wrap gap-6 text-sm">
            <span className="text-gray-700">
              <span className="font-semibold">Reserve List:</span>{' '}
              {summary.reserveListOwned} cards owned &bull;{' '}
              <span className="text-green-700">${summary.reserveListValue.toFixed(2)}</span> value
            </span>
            <span className="text-gray-700">
              <span className="font-semibold">Boosters:</span>{' '}
              {summary.boostersOwned}/{summary.boostersTotal} owned
            </span>
          </div>
        </div>
      )}

      {/* ===================================================================
          Filters
          =================================================================== */}
      <div className="bg-white rounded-lg shadow-md p-4 mb-4">
        <div className="flex flex-wrap gap-3 items-center">
          <input
            type="text"
            placeholder="Search card name..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="border rounded px-3 py-1.5 text-sm flex-1 min-w-[160px]"
          />

          {/* Set filter */}
          <select
            value={filterSetCode}
            onChange={e => setFilterSetCode(e.target.value)}
            className="border rounded px-2 py-1.5 text-sm"
          >
            <option value="all">All Sets</option>
            {(sets as MtgSet[]).map(s => (
              <option key={s.code} value={s.code}>{s.name} ({s.code.toUpperCase()})</option>
            ))}
          </select>

          {/* Rarity filter */}
          <select
            value={filterRarity}
            onChange={e => setFilterRarity(e.target.value)}
            className="border rounded px-2 py-1.5 text-sm"
          >
            <option value="all">All Rarities</option>
            {['common', 'uncommon', 'rare', 'mythic', 'special', 'bonus'].map(r => (
              <option key={r} value={r} className="capitalize">{r.charAt(0).toUpperCase() + r.slice(1)}</option>
            ))}
          </select>

          {/* Type filter */}
          <select
            value={filterType}
            onChange={e => setFilterType(e.target.value)}
            className="border rounded px-2 py-1.5 text-sm"
          >
            <option value="all">All Types</option>
            {['creature', 'instant', 'sorcery', 'enchantment', 'artifact', 'planeswalker', 'land', 'battle', 'other'].map(t => (
              <option key={t} value={t} className="capitalize">{t.charAt(0).toUpperCase() + t.slice(1)}</option>
            ))}
          </select>

          {/* Color filter */}
          <select
            value={filterColor}
            onChange={e => setFilterColor(e.target.value)}
            className="border rounded px-2 py-1.5 text-sm"
          >
            <option value="all">All Colors</option>
            <option value="W">White (W)</option>
            <option value="U">Blue (U)</option>
            <option value="B">Black (B)</option>
            <option value="R">Red (R)</option>
            <option value="G">Green (G)</option>
            <option value="multicolor">Multicolor</option>
            <option value="colorless">Colorless</option>
          </select>

          {/* Variant filter */}
          <select
            value={filterVariant}
            onChange={e => setFilterVariant(e.target.value)}
            className="border rounded px-2 py-1.5 text-sm"
          >
            <option value="all">All Variants</option>
            {['Regular', 'Foil', 'Etched Foil', 'Galaxy Foil', 'Gilded Foil', 'Surge Foil', 'Fracture Foil', 'Textured Foil', 'Serialized'].map(v => (
              <option key={v} value={v}>{v}</option>
            ))}
          </select>

          {(filterSetCode !== 'all' || filterRarity !== 'all' || filterType !== 'all' || filterColor !== 'all' || filterVariant !== 'all' || search) && (
            <button
              onClick={() => { setSearch(''); setFilterSetCode('all'); setFilterRarity('all'); setFilterType('all'); setFilterColor('all'); setFilterVariant('all') }}
              className="text-sm text-gray-500 hover:text-gray-700 underline"
            >
              Clear filters
            </button>
          )}
        </div>
      </div>

      {/* ===================================================================
          Export row
          =================================================================== */}
      <div className="bg-white rounded-lg shadow-md p-4 mb-4 flex flex-wrap items-center gap-4">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-gray-600">Summary:</span>
          <button onClick={() => handleExport(() => api.downloadCollectionSummaryCSV())} className="px-2 py-1 text-xs bg-gray-100 hover:bg-gray-200 rounded border">CSV</button>
          <button onClick={() => handleExport(() => api.downloadCollectionSummaryXML())} className="px-2 py-1 text-xs bg-gray-100 hover:bg-gray-200 rounded border">XML</button>
          <button onClick={() => handleExport(() => api.downloadCollectionSummaryPDF())} className="px-2 py-1 text-xs bg-gray-100 hover:bg-gray-200 rounded border">PDF</button>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-gray-600">Detailed:</span>
          <button onClick={() => handleExport(() => api.downloadCollectionDetailedCSV(apiFilter))} className="px-2 py-1 text-xs bg-blue-50 hover:bg-blue-100 rounded border border-blue-200">CSV</button>
          <button onClick={() => handleExport(() => api.downloadCollectionDetailedXML(apiFilter))} className="px-2 py-1 text-xs bg-blue-50 hover:bg-blue-100 rounded border border-blue-200">XML</button>
          <button onClick={() => handleExport(() => api.downloadCollectionDetailedPDF(apiFilter))} className="px-2 py-1 text-xs bg-blue-50 hover:bg-blue-100 rounded border border-blue-200">PDF</button>
        </div>
        <span className="text-xs text-gray-400">Detailed exports use current filters</span>
      </div>

      {/* ===================================================================
          Results count
          =================================================================== */}
      <p className="text-sm text-gray-600 mb-3">
        {isLoading ? 'Loading…' : (
          <>
            Showing <span className="font-semibold">{filteredQty.toLocaleString()}</span> entries
            {' '}({filteredUnique.toLocaleString()} unique cards &bull;{' '}
            <span className="text-green-700">${filteredValue.toFixed(2)}</span>)
          </>
        )}
      </p>

      {/* ===================================================================
          Table
          =================================================================== */}
      <div className="bg-white rounded-lg shadow-md overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              <SortTh label="Card Name"  sortKey="cardName"        current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="Set"        sortKey="setCode"         current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="#"          sortKey="collectorNumber" current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="Rarity"     sortKey="rarity"          current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="Type"       sortKey="typeLine"        current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="Variant"    sortKey="variant"         current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="Qty"        sortKey="quantity"        current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="$/each"     sortKey="priceUsd"        current={sortKey} dir={sortDir} onSort={handleSort} />
              <SortTh label="Total"      sortKey="lineValue"       current={sortKey} dir={sortDir} onSort={handleSort} />
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-gray-500">Loading collection…</td>
              </tr>
            )}
            {!isLoading && filtered.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-gray-500">No cards found</td>
              </tr>
            )}
            {filtered.map((entry, idx) => {
              const lineValue = (entry.priceUsd ?? 0) * entry.quantity
              const rowBg = idx % 2 === 0 ? 'bg-white' : 'bg-gray-50'
              return (
                <tr key={`${entry.scryfallCardId}-${entry.variant}`} className={`${rowBg} hover:bg-blue-50 transition-colors`}>
                  <td className="px-3 py-2 font-medium">
                    {entry.cardName}
                    {entry.isReserved && (
                      <span className="ml-1.5 inline-block text-xs font-bold text-amber-700 bg-amber-100 rounded px-1 py-0.5 leading-none">RL</span>
                    )}
                  </td>
                  <td className="px-3 py-2 text-gray-500 font-mono text-xs uppercase">{entry.setCode}</td>
                  <td className="px-3 py-2 text-gray-500 text-xs">{entry.collectorNumber}</td>
                  <td className="px-3 py-2 capitalize text-gray-600">{entry.rarity}</td>
                  <td className="px-3 py-2 text-gray-600 max-w-[180px] truncate">{primaryType(entry.typeLine)}</td>
                  <td className="px-3 py-2 text-gray-600">{entry.variant}</td>
                  <td className="px-3 py-2 text-center font-semibold">{entry.quantity}</td>
                  <td className="px-3 py-2 text-right text-gray-600">{fmt(entry.priceUsd)}</td>
                  <td className="px-3 py-2 text-right font-semibold text-green-700">{entry.priceUsd != null ? `$${lineValue.toFixed(2)}` : '—'}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
