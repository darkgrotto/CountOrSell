import { useState, useEffect } from 'react';
import { getAuthHeaders } from '../services/auth';

interface UpdateInfo {
  updateAvailable: boolean;
  currentVersion?: string;
  availableVersion?: string;
  packageType?: string;
  description?: string;
  fileSizeBytes?: number;
  error?: string;
}

interface ApplyResult {
  applied: boolean;
  version?: string;
  setsUpdated?: number;
  cardsUpdated?: number;
  message?: string;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function UpdateCheckPanel() {
  const [updateInfo, setUpdateInfo] = useState<UpdateInfo | null>(null);
  const [isChecking, setIsChecking] = useState(true);
  const [isApplying, setIsApplying] = useState(false);
  const [applyResult, setApplyResult] = useState<ApplyResult | null>(null);

  const checkForUpdates = async () => {
    setIsChecking(true);
    setApplyResult(null);
    try {
      const response = await fetch('/api/updates/check');
      if (!response.ok) {
        throw new Error(`Failed to check for updates: ${response.statusText}`);
      }
      const data = await response.json();
      setUpdateInfo(data);
    } catch (error) {
      console.error('Update check error:', error);
      setUpdateInfo(null);
    } finally {
      setIsChecking(false);
    }
  };

  const applyUpdate = async () => {
    setIsApplying(true);
    setApplyResult(null);
    try {
      const response = await fetch('/api/updates/apply', {
        method: 'POST',
        headers: await getAuthHeaders(),
      });

      if (!response.ok) {
        throw new Error(`Failed to apply update: ${response.statusText}`);
      }

      const result: ApplyResult = await response.json();
      setApplyResult(result);

      // Recheck for updates after applying
      setTimeout(() => checkForUpdates(), 1000);
    } catch (error) {
      console.error('Update apply error:', error);
      setApplyResult({ applied: false, message: 'Failed to apply update. Please try again.' });
    } finally {
      setIsApplying(false);
    }
  };

  useEffect(() => {
    checkForUpdates();
  }, []);

  return (
    <div className="bg-white rounded-lg shadow p-4">
      <div className="flex items-center justify-between mb-2">
        <h3 className="text-lg font-semibold">Database Updates</h3>
        <button
          onClick={checkForUpdates}
          disabled={isChecking}
          className="text-sm px-3 py-1 text-blue-600 hover:text-blue-700 disabled:text-gray-400"
        >
          {isChecking ? 'Checking...' : 'Refresh'}
        </button>
      </div>

      {updateInfo?.currentVersion && (
        <p className="text-xs text-gray-500 mb-2">
          Current version: {updateInfo.currentVersion}
        </p>
      )}

      {isChecking ? (
        <p className="text-gray-500">Checking for updates...</p>
      ) : updateInfo ? (
        <div>
          {updateInfo.error ? (
            <p className="text-yellow-600 text-sm">{updateInfo.error}</p>
          ) : updateInfo.updateAvailable ? (
            <div>
              <p className="text-orange-600 mb-1">
                Update available: v{updateInfo.availableVersion}
              </p>
              {updateInfo.description && (
                <p className="text-sm text-gray-600 mb-1">{updateInfo.description}</p>
              )}
              <p className="text-xs text-gray-500 mb-2">
                {updateInfo.packageType === 'delta' ? 'Delta' : 'Full'} update
                {updateInfo.fileSizeBytes ? ` (${formatBytes(updateInfo.fileSizeBytes)})` : ''}
              </p>
              <button
                onClick={applyUpdate}
                disabled={isApplying}
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
              >
                {isApplying ? 'Downloading & Applying...' : 'Apply Update'}
              </button>
            </div>
          ) : (
            <p className="text-green-600">Up to date</p>
          )}
          {applyResult && (
            <div className="mt-2 text-sm">
              {applyResult.applied ? (
                <p className="text-green-600">
                  Updated to v{applyResult.version} — {applyResult.setsUpdated} sets, {applyResult.cardsUpdated} cards updated
                </p>
              ) : (
                <p className="text-red-600">{applyResult.message || 'Update failed'}</p>
              )}
            </div>
          )}
        </div>
      ) : (
        <p className="text-red-600">Failed to check for updates</p>
      )}
    </div>
  );
}
