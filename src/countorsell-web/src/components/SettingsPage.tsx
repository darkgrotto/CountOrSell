import { useState } from 'react'
import UpdateCheckPanel from './UpdateCheckPanel'
import ExportPanel from './ExportPanel'
import { api } from '../services/api'

export default function SettingsPage() {
  const [pdfLoading, setPdfLoading] = useState(false)
  const [pdfError, setPdfError] = useState<string | null>(null)

  async function handleExportPdf() {
    setPdfLoading(true)
    setPdfError(null)
    try {
      await api.downloadSlabbedCardsPdf()
    } catch (e: unknown) {
      setPdfError(e instanceof Error ? e.message : 'Export failed')
    } finally {
      setPdfLoading(false)
    }
  }

  return (
    <div>
      <h1 className="text-3xl font-bold mb-6">Settings</h1>

      <div className="space-y-8">
        {/* Database Updates */}
        <section>
          <h2 className="text-xl font-semibold mb-3">Database Updates</h2>
          <UpdateCheckPanel />
        </section>

        {/* Exports */}
        <section>
          <h2 className="text-xl font-semibold mb-3">Export Data</h2>
          <div className="space-y-4">
            <ExportPanel type="cards" title="Cards (CSV / XML)" />
            <ExportPanel type="boosters" title="Boosters (CSV)" />
            <ExportPanel type="reservelist" title="Reserve List (CSV)" />

            <div className="bg-white rounded-lg shadow p-4">
              <h3 className="text-lg font-semibold mb-3">Slabbed Cards (PDF)</h3>
              <button
                onClick={handleExportPdf}
                disabled={pdfLoading}
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
              >
                {pdfLoading ? 'Exporting…' : 'Download PDF'}
              </button>
              {pdfError && (
                <p className="mt-2 text-red-600 text-sm">{pdfError}</p>
              )}
            </div>
          </div>
        </section>
      </div>
    </div>
  )
}
