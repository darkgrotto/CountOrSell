import { useState, FormEvent, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../services/api';

type TabType = 'login' | 'register';

export default function LoginPage() {
  const [activeTab, setActiveTab] = useState<TabType>('login');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { login, register } = useAuth();
  const navigate = useNavigate();

  const { data: regStatus } = useQuery({
    queryKey: ['registration-status'],
    queryFn: () => api.getRegistrationStatus(),
  });
  const registrationsEnabled = regStatus?.registrationsEnabled !== false;

  // If registrations get disabled while on the register tab, switch to login
  useEffect(() => {
    if (!registrationsEnabled && activeTab === 'register') {
      switchTab('login');
    }
  }, [registrationsEnabled]);

  const handleLoginSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setIsSubmitting(true);

    try {
      await login(username, password);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleRegisterSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setIsSubmitting(true);

    try {
      await register(username, password, displayName || undefined);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed');
    } finally {
      setIsSubmitting(false);
    }
  };

  const resetForm = () => {
    setUsername('');
    setPassword('');
    setDisplayName('');
    setError('');
  };

  const switchTab = (tab: TabType) => {
    setActiveTab(tab);
    resetForm();
  };

  return (
    <div className="min-h-screen bg-gray-900 flex items-center justify-center px-4">
      <div className="max-w-md w-full">
        <div className="bg-gray-800 rounded-lg shadow-xl overflow-hidden">
          {/* Tabs */}
          <div className="flex">
            <button
              className={`${registrationsEnabled ? 'flex-1' : 'w-full'} py-4 text-center font-semibold transition-colors ${
                activeTab === 'login'
                  ? 'bg-blue-800 text-white'
                  : 'bg-gray-700 text-gray-300 hover:bg-gray-650'
              }`}
              onClick={() => switchTab('login')}
            >
              Login
            </button>
            {registrationsEnabled && (
              <button
                className={`flex-1 py-4 text-center font-semibold transition-colors ${
                  activeTab === 'register'
                    ? 'bg-blue-800 text-white'
                    : 'bg-gray-700 text-gray-300 hover:bg-gray-650'
                }`}
                onClick={() => switchTab('register')}
              >
                Register
              </button>
            )}
          </div>

          {/* Form Content */}
          <div className="p-8">
            <h2 className="text-2xl font-bold text-white mb-6">
              {activeTab === 'login' || !registrationsEnabled ? 'Welcome Back' : 'Create Account'}
            </h2>

            {error && (
              <div className="mb-4 p-3 bg-red-900/50 border border-red-700 rounded text-red-200 text-sm">
                {error}
              </div>
            )}

            {activeTab === 'login' || !registrationsEnabled ? (
              <form onSubmit={handleLoginSubmit} className="space-y-4">
                <div>
                  <label htmlFor="login-username" className="block text-sm font-medium text-gray-300 mb-2">
                    Username
                  </label>
                  <input
                    id="login-username"
                    type="text"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent"
                    placeholder="Enter your username"
                    required
                    autoComplete="username"
                  />
                </div>

                <div>
                  <label htmlFor="login-password" className="block text-sm font-medium text-gray-300 mb-2">
                    Password
                  </label>
                  <input
                    id="login-password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent"
                    placeholder="Enter your password"
                    required
                    autoComplete="current-password"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-800 disabled:cursor-not-allowed text-white font-semibold rounded transition-colors"
                >
                  {isSubmitting ? 'Logging in...' : 'Login'}
                </button>
              </form>
            ) : (
              <form onSubmit={handleRegisterSubmit} className="space-y-4">
                <div>
                  <label htmlFor="register-username" className="block text-sm font-medium text-gray-300 mb-2">
                    Username
                  </label>
                  <input
                    id="register-username"
                    type="text"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent"
                    placeholder="Choose a username"
                    required
                    autoComplete="username"
                  />
                </div>

                <div>
                  <label htmlFor="register-password" className="block text-sm font-medium text-gray-300 mb-2">
                    Password
                  </label>
                  <input
                    id="register-password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent"
                    placeholder="Choose a password"
                    required
                    autoComplete="new-password"
                  />
                </div>

                <div>
                  <label htmlFor="register-displayname" className="block text-sm font-medium text-gray-300 mb-2">
                    Display Name <span className="text-gray-500">(optional)</span>
                  </label>
                  <input
                    id="register-displayname"
                    type="text"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent"
                    placeholder="How should we call you?"
                    autoComplete="name"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-800 disabled:cursor-not-allowed text-white font-semibold rounded transition-colors"
                >
                  {isSubmitting ? 'Creating account...' : 'Register'}
                </button>
              </form>
            )}
          </div>
        </div>

        <div className="mt-6 text-center text-gray-400 text-sm">
          <p>The only spell your collection needs.</p>
        </div>
      </div>
    </div>
  );
}
