import { useState, useEffect, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  api,
  SlabbedCard,
  SlabbedCardRequest,
  CardSearchResult,
  GRADING_COMPANIES,
  CARD_VARIANTS,
  getCertVerificationUrl,
} from '../services/api'

const BLANK_FORM: SlabbedCardRequest = {
  scryfallCardId: '',
  cardName: '',
  setCode: '',
  setName: '',
  collectorNumber: '',
  cardVariant: 'Regular',
  gradingCompany: 'PSA',
  grade: '',
  certificationNumber: '',
  purchaseDate: null,
  purchasedFrom: null,
  purchaseCost: null,
  notes: null,
}

export default function SlabbedCardsPage() {
  const queryClient = useQueryClient()

  const [showModal, setShowModal] = useState(false)
  const [editingSlab, setEditingSlab] = useState<SlabbedCard | null>(null)
  const [form, setForm] = useState<SlabbedCardRequest>(BLANK_FORM)
  const [formError, setFormError] = useState<string | null>(null)

  // Card search state
  const [cardSearch, setCardSearch] = useState('')
  const [searchResults, setSearchResults] = useState<CardSearchResult[]>([])
  const [searchLoading, setSearchLoading] = useState(false)
  const [showDropdown, setShowDropdown] = useState(false)
  const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const dropdownRef = useRef<HTMLDivElement>(null)

  const { data: slabs = [], isLoading, error } = useQuery({
    queryKey: ['slabbed'],
    queryFn: () => api.fetchSlabbedCards(),
  })

  const addMutation = useMutation({
    mutationFn: (req: SlabbedCardRequest) => api.addSlabbedCard(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['slabbed'] })
      closeModal()
    },
    onError: (e: Error) => setFormError(e.message),
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, req }: { id: number; req: SlabbedCardRequest }) =>
      api.updateSlabbedCard(id, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['slabbed'] })
      closeModal()
    },
    onError: (e: Error) => setFormError(e.message),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.deleteSlabbedCard(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['slabbed'] }),
  })



  function openAdd() {
    setEditingSlab(null)
    setForm(BLANK_FORM)
    setCardSearch('')
    setFormError(null)
    setShowModal(true)
  }

  function openEdit(slab: SlabbedCard) {
    setEditingSlab(slab)
    setForm({
      scryfallCardId: slab.scryfallCardId,
      cardName: slab.cardName,
      setCode: slab.setCode,
      setName: slab.setName,
      collectorNumber: slab.collectorNumber,
      cardVariant: slab.cardVariant,
      gradingCompany: slab.gradingCompany,
      grade: slab.grade,
      certificationNumber: slab.certificationNumber,
      purchaseDate: slab.purchaseDate,
      purchasedFrom: slab.purchasedFrom,
      purchaseCost: slab.purchaseCost,
      notes: slab.notes,
    })
    setCardSearch(slab.cardName)
    setFormError(null)
    setShowModal(true)
  }

  function closeModal() {
    setShowModal(false)
    setEditingSlab(null)
    setForm(BLANK_FORM)
    setCardSearch('')
    setSearchResults([])
    setShowDropdown(false)
    setFormError(null)
  }

  function handleCardSearchChange(value: string) {
    setCardSearch(value)
    setForm(f => ({ ...f, cardName: value, scryfallCardId: '', setCode: '', setName: '', collectorNumber: '' }))
    setShowDropdown(true)

    if (searchTimer.current) clearTimeout(searchTimer.current)
    if (value.length < 2) {
      setSearchResults([])
      return
    }
    searchTimer.current = setTimeout(async () => {
      setSearchLoading(true)
      try {
        const results = await api.searchCards(value)
        setSearchResults(results)
      } catch {
        setSearchResults([])
      } finally {
        setSearchLoading(false)
      }
    }, 300)
  }

  function selectCard(result: CardSearchResult) {
    setCardSearch(result.name)
    setForm(f => ({
      ...f,
      scryfallCardId: result.id,
      cardName: result.name,
      setCode: result.setCode,
      setName: result.setName,
      collectorNumber: result.collectorNumber,
    }))
    setShowDropdown(false)
    setSearchResults([])
  }

  // Close dropdown on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setShowDropdown(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!form.cardName.trim()) { setFormError('Card name is required'); return }
    if (!form.gradingCompany.trim()) { setFormError('Grading company is required'); return }
    if (!form.grade.trim()) { setFormError('Grade is required'); return }
    if (!form.certificationNumber.trim()) { setFormError('Certification number is required'); return }

    if (editingSlab) {
      updateMutation.mutate({ id: editingSlab.id, req: form })
    } else {
      addMutation.mutate(form)
    }
  }

  const isSaving = addMutation.isPending || updateMutation.isPending

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        <span className="ml-4 text-gray-500">Loading slabbed collection...</span>
      </div>
    )
  }

  if (error) {
    return (
      <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
        Error loading slabbed cards: {(error as Error).message}
      </div>
    )
  }

  const totalValue = slabs
    .filter(s => s.purchaseCost != null)
    .reduce((sum, s) => sum + (s.purchaseCost ?? 0), 0)

  return (
    <div>
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-3xl font-bold">Slabbed Collection</h1>
          <p className="text-gray-600 mt-1">Professionally graded cards</p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={openAdd}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium"
          >
            + Add Slab
          </button>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-4 mb-6">
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-blue-600">{slabs.length}</div>
          <div className="text-sm text-gray-500">Total Slabs</div>
        </div>
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-green-600">${totalValue.toFixed(2)}</div>
          <div className="text-sm text-gray-500">Total Cost</div>
        </div>
        <div className="bg-white rounded-lg shadow p-4 text-center">
          <div className="text-2xl font-bold text-purple-600">
            {[...new Set(slabs.map(s => s.gradingCompany))].join(', ') || '—'}
          </div>
          <div className="text-sm text-gray-500">Companies</div>
        </div>
      </div>

      {/* Table */}
      {slabs.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <p className="text-lg">No slabs yet.</p>
          <p className="text-sm mt-1">Click "+ Add Slab" to get started.</p>
        </div>
      ) : (
        <div className="bg-white rounded-lg shadow overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 border-b">
              <tr>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Card</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Set</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Variant</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Company</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Grade</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Cert #</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Date</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600">Cost</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {slabs.map(slab => {
                const certUrl = getCertVerificationUrl(slab.gradingCompany, slab.certificationNumber)
                return (
                  <tr key={slab.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium text-gray-900">{slab.cardName}</td>
                    <td className="px-4 py-3 text-gray-600 uppercase text-xs">{slab.setCode}</td>
                    <td className="px-4 py-3 text-gray-600">{slab.cardVariant}</td>
                    <td className="px-4 py-3">
                      <span className="px-2 py-0.5 bg-blue-100 text-blue-800 rounded text-xs font-medium">
                        {slab.gradingCompany}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-bold text-gray-900">{slab.grade}</td>
                    <td className="px-4 py-3">
                      {certUrl ? (
                        <a
                          href={certUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-blue-600 hover:underline font-mono text-xs"
                        >
                          {slab.certificationNumber}
                        </a>
                      ) : (
                        <span className="font-mono text-xs">{slab.certificationNumber}</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {slab.purchaseDate ? slab.purchaseDate.slice(0, 10) : '—'}
                    </td>
                    <td className="px-4 py-3 text-green-700 font-medium">
                      {slab.purchaseCost != null ? `$${slab.purchaseCost.toFixed(2)}` : '—'}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex gap-2">
                        <button
                          onClick={() => openEdit(slab)}
                          className="text-blue-600 hover:text-blue-800 text-xs font-medium"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => {
                            if (confirm(`Delete ${slab.cardName} (${slab.gradingCompany} ${slab.grade})?`)) {
                              deleteMutation.mutate(slab.id)
                            }
                          }}
                          className="text-red-500 hover:text-red-700 text-xs font-medium"
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Modal */}
      {showModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
            <div className="px-6 py-4 border-b flex items-center justify-between">
              <h2 className="text-xl font-bold">{editingSlab ? 'Edit Slab' : 'Add Slab'}</h2>
              <button onClick={closeModal} className="text-gray-400 hover:text-gray-600 text-2xl leading-none">&times;</button>
            </div>

            <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
              {/* Card search */}
              <div ref={dropdownRef} className="relative">
                <label className="block text-sm font-medium text-gray-700 mb-1">Card Name *</label>
                <input
                  type="text"
                  value={cardSearch}
                  onChange={e => handleCardSearchChange(e.target.value)}
                  onFocus={() => searchResults.length > 0 && setShowDropdown(true)}
                  placeholder="Type to search cards..."
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  autoComplete="off"
                />
                {showDropdown && (searchLoading || searchResults.length > 0) && (
                  <div className="absolute z-10 w-full bg-white border border-gray-200 rounded-lg shadow-lg mt-1 max-h-60 overflow-y-auto">
                    {searchLoading && (
                      <div className="px-4 py-2 text-gray-500 text-sm">Searching…</div>
                    )}
                    {!searchLoading && searchResults.map(r => (
                      <button
                        key={`${r.id}-${r.setCode}`}
                        type="button"
                        onClick={() => selectCard(r)}
                        className="w-full px-4 py-2 text-left hover:bg-blue-50 text-sm border-b last:border-0"
                      >
                        <span className="font-medium">{r.name}</span>
                        <span className="text-gray-500 ml-2 text-xs">
                          {r.setCode.toUpperCase()} #{r.collectorNumber}
                        </span>
                      </button>
                    ))}
                  </div>
                )}
                {form.setName && (
                  <p className="text-xs text-gray-500 mt-1">
                    {form.setName} &middot; #{form.collectorNumber}
                  </p>
                )}
              </div>

              {/* Card Variant */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Card Variant</label>
                <select
                  value={form.cardVariant}
                  onChange={e => setForm(f => ({ ...f, cardVariant: e.target.value }))}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                >
                  {CARD_VARIANTS.map(v => <option key={v} value={v}>{v}</option>)}
                </select>
              </div>

              {/* Grading Company + Grade */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Grading Company *</label>
                  <select
                    value={form.gradingCompany}
                    onChange={e => setForm(f => ({ ...f, gradingCompany: e.target.value }))}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  >
                    {GRADING_COMPANIES.map(c => <option key={c} value={c}>{c}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Grade *</label>
                  <input
                    type="text"
                    value={form.grade}
                    onChange={e => setForm(f => ({ ...f, grade: e.target.value }))}
                    placeholder="e.g. 9, 9.5, 10"
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  />
                </div>
              </div>

              {/* Cert Number */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Certification Number *</label>
                <input
                  type="text"
                  value={form.certificationNumber}
                  onChange={e => setForm(f => ({ ...f, certificationNumber: e.target.value }))}
                  placeholder="e.g. 12345678"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                />
              </div>

              {/* Purchase Date + From */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Purchase Date</label>
                  <input
                    type="date"
                    value={form.purchaseDate?.slice(0, 10) ?? ''}
                    onChange={e => setForm(f => ({ ...f, purchaseDate: e.target.value || null }))}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Purchased From</label>
                  <input
                    type="text"
                    value={form.purchasedFrom ?? ''}
                    onChange={e => setForm(f => ({ ...f, purchasedFrom: e.target.value || null }))}
                    placeholder="e.g. eBay, TCGPlayer"
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  />
                </div>
              </div>

              {/* Purchase Cost */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Purchase Cost ($)</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={form.purchaseCost ?? ''}
                  onChange={e => setForm(f => ({ ...f, purchaseCost: e.target.value ? parseFloat(e.target.value) : null }))}
                  placeholder="0.00"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                />
              </div>

              {/* Notes */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Notes</label>
                <textarea
                  value={form.notes ?? ''}
                  onChange={e => setForm(f => ({ ...f, notes: e.target.value || null }))}
                  rows={3}
                  placeholder="Any additional notes..."
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 resize-none"
                />
              </div>

              {formError && (
                <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-2 rounded text-sm">
                  {formError}
                </div>
              )}

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={closeModal}
                  className="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-50 text-sm"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSaving}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-lg text-sm font-medium"
                >
                  {isSaving ? 'Saving…' : editingSlab ? 'Save Changes' : 'Add Slab'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
