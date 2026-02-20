// =============================================================================
// LabelPreview.tsx - Storage Box Label Generator Component
// =============================================================================
// This component provides a UI for generating and downloading PDF labels
// for MTG card storage boxes. It displays:
// - Box type selector (Set or Surplus)
// - A download button to generate the actual PDF
//
// The labels are generated server-side using QuestPDF and downloaded as PDF files.
// Label dimensions are 3.75" x 2.75" (standard storage box label size).
// =============================================================================

import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { api } from '../services/api'

// =============================================================================
// Component Props Interface
// =============================================================================

/**
 * Props for the LabelPreview component.
 * Receives the set code from the parent SetDetail component.
 */
interface LabelPreviewProps {
  /** The MTG set code (e.g., "DOM", "MH3") */
  setCode: string
}

// =============================================================================
// LabelPreview Component
// =============================================================================

/**
 * LabelPreview Component
 *
 * Generates and downloads PDF labels for MTG card storage boxes.
 *
 * Features:
 * - Toggle between "Set" and "Surplus" box types
 * - One-click PDF download
 * - Loading state during generation
 * - Error display if generation fails
 *
 * @param setCode - The MTG set code to generate a label for
 * @returns The label generator UI
 */
export default function LabelPreview({ setCode }: LabelPreviewProps) {
  // ===========================================================================
  // State Management
  // ===========================================================================

  /**
   * Box type state - determines what type of label to generate.
   *
   * "Set Box" - For the main collection box containing the complete set
   * "Surplus Box" - For overflow/duplicate cards from the set
   *
   * This value is displayed on the label and sent to the API.
   */
  const [boxType, setBoxType] = useState<'Set Box' | 'Surplus Box'>('Set Box')

  // ===========================================================================
  // Label Generation Mutation
  // ===========================================================================

  /**
   * Mutation for generating the PDF label.
   *
   * useMutation is used instead of useQuery because:
   * - This is a POST request that creates a file
   * - It should only run when explicitly triggered by button click
   * - We need to handle the response as a downloadable blob
   *
   * The mutation:
   * - Calls the API with the set code and selected box type
   * - On success, triggers a browser download of the PDF blob
   * - Tracks loading and error states for UI feedback
   */
  const generateMutation = useMutation({
    // Function to call when mutation is triggered
    // Passes the set code and current box type selection
    mutationFn: () => api.generateLabel({ setCode, boxType }),

    // On successful generation, trigger browser download
    // The blob is saved with a descriptive filename: {SETCODE}-{BoxType}-label.pdf
    onSuccess: (blob) => {
      api.downloadBlob(blob, `${setCode.toUpperCase()}-${boxType}-label.pdf`)
    },
  })

  // ===========================================================================
  // Main Render
  // ===========================================================================

  return (
    // White card container with shadow (matches JiraSyncPanel styling)
    <div className="bg-white rounded-lg shadow-md p-6">

      {/* Panel Title */}
      <h2 className="text-xl font-bold mb-4">Label Generator</h2>

      {/* =====================================================================
          Box Type Selector
          ===================================================================== */}
      {/* Radio button group for selecting the type of storage box */}
      <div className="mb-4">
        {/* Field label */}
        <label className="block text-sm text-gray-600 mb-2">Box Type</label>

        {/* Radio button options container */}
        <div className="flex gap-4">

          {/* "Set Box" Option */}
          {/* For the main collection box */}
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="radio"
              name="boxType"                    // Groups radio buttons together
              value="Set Box"
              checked={boxType === 'Set Box'}   // Controlled by state
              onChange={() => setBoxType('Set Box')}
              className="w-4 h-4 text-blue-600"
            />
            <span>Set Box</span>
          </label>

          {/* "Surplus Box" Option */}
          {/* For overflow/duplicate cards */}
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="radio"
              name="boxType"
              value="Surplus Box"
              checked={boxType === 'Surplus Box'}
              onChange={() => setBoxType('Surplus Box')}
              className="w-4 h-4 text-blue-600"
            />
            <span>Surplus Box</span>
          </label>
        </div>
      </div>


      {/* =====================================================================
          Download Button
          ===================================================================== */}
      <button
        // Trigger the mutation when clicked
        onClick={() => generateMutation.mutate()}
        // Disable while generating to prevent duplicate requests
        disabled={generateMutation.isPending}
        // Purple styling with hover effect, gray when disabled
        className="w-full bg-blue-600 text-white py-2 px-4 rounded-lg hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
      >
        {/* Button text changes based on loading state */}
        {generateMutation.isPending ? 'Generating...' : 'Download PDF Label'}
      </button>

      {/* =====================================================================
          Error Display
          ===================================================================== */}
      {/* Only shown if the mutation failed */}
      {generateMutation.isError && (
        <div className="mt-2 text-sm text-red-600">
          Error: {generateMutation.error.message}
        </div>
      )}
    </div>
  )
}
