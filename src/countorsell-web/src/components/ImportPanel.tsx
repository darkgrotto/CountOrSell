import { useRef, useState } from 'react'
import { api, ImportResult } from '../services/api'

const SUPPORTED_FORMATS = [
  { name: 'CountOrSell CSV/XML', note: 'Native export format — full fidelity with variants & quantities' },
  { name: 'Moxfield CSV', note: 'Export via Moxfield → Collection → Download CSV' },
  { name: 'TCGPlayer CSV', note: 'Export via TCGPlayer → Collection → Export' },
  { name: 'Dragon Shield CSV', note: 'Export via Dragon Shield app → Export as CSV' },
  { name: 'Generic CSV', note: 'Any CSV with Name/Quantity columns (quantities default to 1)' },
]

export default function ImportPanel() {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  function handleFile(f: File) {
    setFile(f)
    setResult(null)
    setError(null)
  }

  function handleDrop(e: React.DragEvent) {
    e.preventDefault()
    setDragging(false)
    const f = e.dataTransfer.files[0]
    if (f) handleFile(f)
  }

  async function handleImport() {
    if (!file) return
    setLoading(true)
    setResult(null)
    setError(null)
    try {
      const res = await api.importCollection(file)
      setResult(res)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Import failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="bg-white rounded-lg shadow p-4 space-y-4">
      <div>
        <h3 className="text-lg font-semibold mb-1">Import Collection</h3>
        <p className="text-sm text-gray-500">
          Import card ownership from another site's export file. Quantities and variants are preserved where available.
        </p>
      </div>

      {/* Supported formats */}
      <details className="text-sm">
        <summary className="cursor-pointer text-blue-600 hover:underline select-none">
          Supported formats
        </summary>
        <ul className="mt-2 space-y-1 pl-2">
          {SUPPORTED_FORMATS.map(f => (
            <li key={f.name} className="flex gap-2">
              <span className="font-medium w-44 shrink-0">{f.name}</span>
              <span className="text-gray-500">{f.note}</span>
            </li>
          ))}
        </ul>
      </details>

      {/* Drop zone */}
      <div
        onClick={() => inputRef.current?.click()}
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={handleDrop}
        className={`border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors ${
          dragging ? 'border-blue-500 bg-blue-50' : 'border-gray-300 hover:border-gray-400'
        }`}
      >
        <input
          ref={inputRef}
          type="file"
          accept=".csv,.xml"
          className="hidden"
          onChange={e => { const f = e.target.files?.[0]; if (f) handleFile(f) }}
        />
        {file ? (
          <p className="text-sm font-medium text-gray-700">{file.name}</p>
        ) : (
          <p className="text-sm text-gray-500">
            Drop a CSV or XML file here, or <span className="text-blue-600 underline">click to browse</span>
          </p>
        )}
      </div>

      {/* Import button */}
      <button
        onClick={handleImport}
        disabled={!file || loading}
        className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
      >
        {loading ? 'Importing…' : 'Import'}
      </button>

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-300 text-red-700 rounded p-3 text-sm">
          {error}
        </div>
      )}

      {/* Result */}
      {result && (
        <div className="space-y-2">
          <div className="bg-green-50 border border-green-300 text-green-800 rounded p-3 text-sm space-y-1">
            <p className="font-semibold">Import complete</p>
            {result.detectedFormat && (
              <p>Detected format: <span className="font-medium">{result.detectedFormat}</span></p>
            )}
            <p>{result.imported} card {result.imported === 1 ? 'entry' : 'entries'} imported</p>
            {result.unmatched > 0 && (
              <p className="text-amber-700">{result.unmatched} unmatched</p>
            )}
          </div>

          {result.unmatchedCards.length > 0 && (
            <details className="text-sm">
              <summary className="cursor-pointer text-amber-700 hover:underline select-none">
                Show unmatched cards ({result.unmatchedCards.length}{result.unmatchedCards.length === 50 ? '+' : ''})
              </summary>
              <ul className="mt-2 pl-2 space-y-0.5 text-gray-600 max-h-48 overflow-y-auto">
                {result.unmatchedCards.map((c, i) => (
                  <li key={i}>{c}</li>
                ))}
              </ul>
            </details>
          )}
        </div>
      )}
    </div>
  )
}
