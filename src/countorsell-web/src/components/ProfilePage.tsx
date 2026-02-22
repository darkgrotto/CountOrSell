import { useState, FormEvent } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../services/api';

export default function ProfilePage() {
  const { user, updateProfile } = useAuth();

  // Display name form state
  const [displayName, setDisplayName] = useState(user?.displayName ?? user?.username ?? '');
  const [nameSubmitting, setNameSubmitting] = useState(false);
  const [nameSuccess, setNameSuccess] = useState('');
  const [nameError, setNameError] = useState('');

  // Password form state
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [pwSubmitting, setPwSubmitting] = useState(false);
  const [pwSuccess, setPwSuccess] = useState('');
  const [pwError, setPwError] = useState('');

  const handleDisplayNameSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setNameSuccess('');
    setNameError('');
    setNameSubmitting(true);
    try {
      await updateProfile(displayName);
      setNameSuccess('Display name updated.');
    } catch (err) {
      setNameError(err instanceof Error ? err.message : 'Update failed');
    } finally {
      setNameSubmitting(false);
    }
  };

  const handlePasswordSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setPwSuccess('');
    setPwError('');

    if (!currentPassword || !newPassword || !confirmPassword) {
      setPwError('All fields are required');
      return;
    }
    if (newPassword.length < 15) {
      setPwError('New password must be at least 15 characters');
      return;
    }
    if (newPassword !== confirmPassword) {
      setPwError('New passwords do not match');
      return;
    }
    if (newPassword === currentPassword) {
      setPwError('New password must be different from current password');
      return;
    }

    setPwSubmitting(true);
    try {
      await api.changePassword(currentPassword, newPassword);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setPwSuccess('Password changed successfully.');
    } catch (err) {
      setPwError(err instanceof Error ? err.message : 'Password change failed');
    } finally {
      setPwSubmitting(false);
    }
  };

  const inputClass = 'w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent';
  const labelClass = 'block text-sm font-medium text-gray-300 mb-2';
  const btnClass = 'px-6 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-800 disabled:cursor-not-allowed text-white font-semibold rounded transition-colors';

  return (
    <div className="max-w-lg mx-auto space-y-6">
      <h1 className="text-2xl font-bold">Profile</h1>

      {/* Display Name */}
      <div className="bg-gray-800 rounded-lg p-6">
        <h2 className="text-lg font-semibold text-white mb-4">Display Name</h2>

        {nameSuccess && (
          <div className="mb-4 p-3 bg-green-900/50 border border-green-700 rounded text-green-200 text-sm">
            {nameSuccess}
          </div>
        )}
        {nameError && (
          <div className="mb-4 p-3 bg-red-900/50 border border-red-700 rounded text-red-200 text-sm">
            {nameError}
          </div>
        )}

        <form onSubmit={handleDisplayNameSubmit} className="space-y-4">
          <div>
            <label htmlFor="display-name" className={labelClass}>Display Name</label>
            <input
              id="display-name"
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              className={inputClass}
              placeholder="Your display name"
              required
              autoComplete="name"
            />
          </div>
          <div>
            <button type="submit" disabled={nameSubmitting} className={btnClass}>
              {nameSubmitting ? 'Saving...' : 'Save'}
            </button>
          </div>
        </form>
      </div>

      {/* Change Password */}
      <div className="bg-gray-800 rounded-lg p-6">
        <h2 className="text-lg font-semibold text-white mb-4">Change Password</h2>

        {pwSuccess && (
          <div className="mb-4 p-3 bg-green-900/50 border border-green-700 rounded text-green-200 text-sm">
            {pwSuccess}
          </div>
        )}
        {pwError && (
          <div className="mb-4 p-3 bg-red-900/50 border border-red-700 rounded text-red-200 text-sm">
            {pwError}
          </div>
        )}

        <form onSubmit={handlePasswordSubmit} className="space-y-4">
          <div>
            <label htmlFor="current-password" className={labelClass}>Current Password</label>
            <input
              id="current-password"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              className={inputClass}
              placeholder="Enter current password"
              autoComplete="current-password"
            />
          </div>
          <div>
            <label htmlFor="new-password" className={labelClass}>New Password</label>
            <input
              id="new-password"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className={inputClass}
              placeholder="Enter new password"
              autoComplete="new-password"
            />
          </div>
          <div>
            <label htmlFor="confirm-password" className={labelClass}>Confirm New Password</label>
            <input
              id="confirm-password"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              className={inputClass}
              placeholder="Confirm new password"
              autoComplete="new-password"
            />
          </div>
          <div>
            <button type="submit" disabled={pwSubmitting} className={btnClass}>
              {pwSubmitting ? 'Changing...' : 'Change Password'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
