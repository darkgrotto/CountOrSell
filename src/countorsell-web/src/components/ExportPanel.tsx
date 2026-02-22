import { useState } from 'react';
import { getAuthHeaders } from '../services/auth';

interface ExportPanelProps {
  type: 'cards' | 'boosters' | 'reservelist';
  title?: string;
}

export default function ExportPanel({ type, title = 'Export' }: ExportPanelProps) {
  const [isDownloading, setIsDownloading] = useState(false);

  const handleExport = async (format: 'csv' | 'xml') => {
    setIsDownloading(true);
    try {
      const endpoint = getEndpoint(type, format);
      const response = await fetch(endpoint, {
        headers: await getAuthHeaders(),
      });

      if (!response.ok) {
        throw new Error(`Export failed: ${response.statusText}`);
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${type}-export.${format}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Export error:', error);
      alert('Failed to export data. Please try again.');
    } finally {
      setIsDownloading(false);
    }
  };

  const getEndpoint = (exportType: string, format: string): string => {
    switch (exportType) {
      case 'cards':
        return `/api/export/cards/${format}`;
      case 'boosters':
        return `/api/export/boosters/${format}`;
      case 'reservelist':
        return `/api/export/reservelist/${format}`;
      default:
        throw new Error(`Unknown export type: ${exportType}`);
    }
  };

  return (
    <div className="bg-white rounded-lg shadow p-4">
      <h3 className="text-lg font-semibold mb-3">{title}</h3>
      <div className="flex gap-2">
        <button
          onClick={() => handleExport('csv')}
          disabled={isDownloading}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
        >
          {isDownloading ? 'Downloading...' : 'Download CSV'}
        </button>
        {type === 'cards' && (
          <button
            onClick={() => handleExport('xml')}
            disabled={isDownloading}
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
          >
            {isDownloading ? 'Downloading...' : 'Download XML'}
          </button>
        )}
      </div>
    </div>
  );
}
